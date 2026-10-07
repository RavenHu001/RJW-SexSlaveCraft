using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static (Pawn pet, Pawn master, Job waiting, JobDriver_PetAffection driver, Toil[] toils) Affection()
    {
        var p = Pair(); p.master.jobs.curJob = null;
        var job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, p.master);
        var driver = new JobDriver_PetAffection { pawn = p.pet, job = job };
        p.pet.jobs.curJob = job; p.pet.jobs.curDriver = driver;
        Toil[] toils = driver.BuildToils();
        Toils_General.NativeWaitWithInit = () => p.master.jobs.curJob = p.waiting;
        toils[1].initAction(); Toils_General.NativeWaitWithInit = null;
        Assert(driver.EndCondition == null && p.master.CurJob == p.waiting, "亲昵原生等待输出未认领");
        return (p.pet, p.master, p.waiting, driver, toils);
    }
    private static void RunAffectionCases()
    {
        Run("未完成亲昵与途中取消不会判定或启动后续", () =>
        {
            var p = Affection();
            NoRollOrStart(p.pet); Assert(PetSpecializationUtility.RewardCalls == 0, "读条期间提前奖励");
            p.driver.NotifyFinish(JobCondition.Incompletable);
            NoRollOrStart(p.pet); Assert(p.master.CurJob == null && PetSpecializationUtility.RewardCalls == 0, "取消未清等待或发奖");
        });
        Run("亲昵结算失败不消耗后续判定，完成回调结束为不可完成", () =>
        {
            var p = Affection(); PetSpecializationUtility.CompletionResult = false;
            p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Incompletable);
            NoRollOrStart(p.pet);
            Assert(p.driver.EndCondition == JobCondition.Incompletable && PetSpecializationUtility.RewardCalls == 0 && p.master.CurJob == null,
                "未完成亲昵仍尝试后续");
        });
        Run("概率未中保留已完成奖励并释放亲昵等待，同回调不重掷", () =>
        {
            var p = Affection(); Rand.ChanceResult = false;
            p.toils[2].initAction(); p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Succeeded);
            Assert(PetSpecializationUtility.RewardCalls == 1 && Rand.Chances.Count == 1 && p.pet.jobs.StartCalls == 0 &&
                p.master.CurJob == null && p.master.jobs.EndCalls == 1, "miss重复结算/掷骰或等待未结束");
        });
        Run("许可拒绝或身体失格仍保留亲昵奖励，仅结束本次等待", () =>
        {
            foreach (bool permission in new[] { false, true })
            {
                Reset(); var p = Affection();
                if (permission) SSCRestrictionJobGuard.PrepareResult = false; else p.master.AdultCategory = false;
                p.toils[2].initAction(); p.toils[2].initAction();
                Assert(PetSpecializationUtility.RewardCalls == 1 && p.master.CurJob == null && p.master.jobs.EndCalls == 1,
                    "无后续使已完成亲昵奖回滚或遗漏清理");
                NoRollOrStart(p.pet);
            }
        });
        Run("后续命中移交原等待，旧亲昵finish不能释放后续等待", () =>
        {
            var p = Affection();
            p.pet.jobs.OnStart = _ => p.driver.NotifyFinish(JobCondition.InterruptForced);
            p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Succeeded);
            Assert(PetSpecializationUtility.RewardCalls == 1 && Rand.Chances.Count == 1 && p.pet.jobs.StartCalls == 1 &&
                p.master.CurJob == p.waiting && p.master.jobs.EndCalls == 0 && SSCRestrictionJobGuard.RegisteredWait == p.waiting,
                "handoff旧亲昵清理误杀了新准备等待");
        });
        Run("后续掷骰回调重入完成不重复奖励判定或启动", () =>
        {
            var p = Affection(); Rand.OnChance = () => p.toils[2].initAction();
            p.toils[2].initAction();
            Assert(PetSpecializationUtility.RewardCalls == 1 && Rand.Chances.Count == 1 && p.pet.jobs.StartCalls == 1,
                "骰子重入产生重复奖励或后续");
        });
        Run("后续预检回调重入完成不再次查询许可或掷骰", () =>
        {
            var p = Affection(); SSCRestrictionJobGuard.OnPrepare = () => p.toils[2].initAction();
            p.toils[2].initAction();
            Assert(PetSpecializationUtility.RewardCalls == 1 && SSCRestrictionJobGuard.PrepareCalls == 1 && Rand.Chances.Count == 1,
                "预检重入重复判定");
        });
        Run("后续预检换掉宠物新命令时撤销候选且不接替新工作", () =>
        {
            var p = Affection(); var replacement = new Job();
            SSCRestrictionJobGuard.OnPrepare = () => { p.pet.jobs.curJob = replacement; p.pet.jobs.curDriver = null; };
            p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.InterruptForced);
            Assert(p.pet.CurJob == replacement && p.pet.jobs.StartCalls == 0 && Rand.Chances.Count == 0 &&
                SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 1 && p.master.CurJob == null && PetSpecializationUtility.RewardCalls == 1,
                "预检重入替换任务后仍调度/掷骰或撤销了亲昵结果");
        });
        Run("后续骰中后主人换工作保留新工作且撤销未安装候选", () =>
        {
            var p = Affection(); var replacement = new Job(); Rand.OnChance = () => p.master.jobs.curJob = replacement;
            p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Succeeded);
            Assert(p.master.CurJob == replacement && p.master.jobs.EndCalls == 0 && p.pet.jobs.StartCalls == 0 &&
                SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 1 && PetSpecializationUtility.RewardCalls == 1,
                "概率回调替换目标后未撤销候选或误清新工作");
        });
        Run("后续StartJob失败仍保留亲昵奖励且不再尝试", () =>
        {
            var p = Affection(); p.pet.jobs.AcceptStart = false;
            p.toils[2].initAction(); p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Succeeded);
            Assert(PetSpecializationUtility.RewardCalls == 1 && Rand.Chances.Count == 1 && p.pet.jobs.StartCalls == 1 &&
                p.master.CurJob == null && p.master.jobs.EndCalls == 1, "安装失败回滚奖励/重新启动或等待残留");
        });
        Run("后续已判定事实保存恢复，同一完成任务不补发奖励或重掷", () =>
        {
            var p = Affection(); Rand.ChanceResult = false; p.toils[2].initAction();
            Scribe.mode = LoadSaveMode.Saving; p.driver.ExposeData();
            Assert(Scribe.Values.ContainsKey("sscAffectionFollowupEvaluated") && (bool)Scribe.Values["sscAffectionFollowupEvaluated"], "已判定字段未保存");
            var loaded = new JobDriver_PetAffection { pawn = p.pet, job = p.driver.job }; p.pet.jobs.curDriver = loaded;
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData(); Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            loaded.BuildToils()[2].initAction();
            Assert(PetSpecializationUtility.RewardCalls == 1 && Rand.Chances.Count == 1 && p.pet.jobs.StartCalls == 0,
                "恢复同一完成任务重复奖励/掷骰");
        });
        Run("旧亲昵存档缺少新判定字段保持false，不在ExposeData中发起事件", () =>
        {
            var p = Affection(); Scribe.mode = LoadSaveMode.Saving; p.driver.ExposeData(); Scribe.Values.Remove("sscAffectionFollowupEvaluated");
            var loaded = new JobDriver_PetAffection { pawn = p.pet, job = p.driver.job }; p.pet.jobs.curDriver = loaded;
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData(); Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            NoRollOrStart(p.pet); loaded.BuildToils()[2].initAction();
            Assert(PetSpecializationUtility.RewardCalls == 1 && Rand.Chances.Count == 1, "旧字段缺席改变完成逻辑或读档初始化事件");
        });
        Run("亲昵中玩家给宠物追加任务优先，奖励保留但不掷后续骰", () =>
        {
            var p = Affection(); Job command = new() { playerForced = true }; p.pet.jobs.jobQueue.EnqueueLast(command);
            p.toils[2].initAction(); p.driver.NotifyFinish(JobCondition.Succeeded);
            Assert(PetSpecializationUtility.RewardCalls == 1 && p.master.CurJob == null && p.pet.jobs.jobQueue.Single().job == command,
                "追加命令影响亲昵奖或被清空");
            NoRollOrStart(p.pet);
        });
    }
}
