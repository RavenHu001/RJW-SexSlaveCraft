using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static class DriverLifecycleTests
{
    public static void Run(Action<bool, string> check)
    {
        var f = new Scene();
        check(f.Driver is JobDriver_Masturbate && f.Driver.Toils.Count == 4,
            "真实驱动保留 RJW 三步并追加独立结算步骤");
        f.Driver.AnimationStart = driver =>
        {
            check(driver.Sexprops.dictionaryKey == f.Hand && driver.Sexprops.resolved.Interaction.Def == f.Hand,
                "动画开始前应已准备完整的具体交互");
            driver.duration = driver.ticks_left = driver.sex_ticks = 5;
        };
        f.StartScene();
        check(f.Driver.StartCalls == 1 && f.Driver.AnimationInitCalls == 1 && f.Driver.duration == 5,
            "生产包装器必须保留 RJW Start 及动画框架追加的初始化回调");
        f.Finish();
        check(f.Driver.EndCalls == 1 && SexUtility.AftersexCalls == 1 && SSCSelfTrainingFeedback.Calls == 1,
            "动画计时结束应收尾一次、执行 RJW 后处理并发放一次反馈");
        check(SexUtility.Events.SequenceEqual(new[] { "RJW outcome", "SSC feedback" }),
            "SSC 结算必须发生在 RJW 后处理之后");
        check(Near(f.Actor.needs.Corruption.CurLevel, 0.2315f) && CorruptionUtility.AddCalls == 1,
            "正常完成按场景输入发放 3.15 个百分点");

        f = new Scene();
        f.Actor.Training.RegisterManualSelfTraining(f.Driver.job, f.Breast);
        f.StartScene();
        check(f.Driver.Sexprops.dictionaryKey == f.Breast && f.Driver.Sexprops.isRevese && SexUtility.SelectionCalls == 0,
            "手动部位选择传入真实驱动且不被自动选型覆盖");
        f.Finish();
        check(SSCSelfTrainingFeedback.Calls == 1, "手动选择沿用同一完成结算路径");

        f = new Scene();
        f.Actor.Training.RegisterManualSelfTraining(f.Driver.job, f.Breast);
        f.Actor.Available.Remove(f.Breast);
        f.StartScene();
        check(f.Actor.jobs.curDriver == null && f.Driver.StartCalls == 0 && SSCSelfTrainingFeedback.Calls == 0 &&
            Messages.Rejections.Contains("SSC_SelfTraining_InteractionChanged"),
            "所选部位在走路期间失效时拒绝启动，不改选其他部位或发奖");

        foreach (bool afterStart in new[] { false, true })
        {
            f = new Scene();
            if (afterStart) f.StartScene();
            f.Actor.jobs.EndCurrentJob(JobCondition.InterruptForced);
            f.Driver.Toils[3].initAction(); // delayed completion callback after cancellation
            check(CorruptionUtility.AddCalls == 0 && SSCSelfTrainingFeedback.Calls == 0 && SexUtility.AftersexCalls == 0,
                afterStart ? "场景中取消不发放完整收益" : "走路时取消不发放完整收益");
            check(f.Driver.EndCalls == (afterStart ? 1 : 0), "取消仅清理实际进入的场景");
        }

        foreach (var change in new Action<Pawn>[]
        {
            pawn => pawn.Drafted = true,
            pawn => pawn.Chain = null,
            pawn => pawn.NativeCanMasturbate = false,
            pawn => pawn.Map = null
        })
        {
            f = new Scene(); f.StartScene(); change(f.Actor); f.Driver.TickForHost();
            check(f.Actor.jobs.curDriver == null && f.Driver.EndCalls == 1 && SSCSelfTrainingFeedback.Calls == 0,
                "执行中失去身体、锁链、地图或非征召条件时安全中止");
        }

        f = new Scene();
        f.Driver.CanRunStart = false;
        f.StartScene();
        check(f.Actor.jobs.curDriver == null && f.Driver.StartCalls == 0 && SSCSelfTrainingFeedback.Calls == 0,
            "RJW Start 被守卫拒绝时不得捕获有效场景或结算");

        f = new Scene(); f.StartScene();
        f.Driver.Toils[3].initAction();
        check(SSCSelfTrainingFeedback.Calls == 0 && CorruptionUtility.AddCalls == 0,
            "计时未结束时提前调用结算步骤无效");
        f.Driver.Sexprops.partner = new Pawn();
        f.Driver.TickForHost();
        check(f.Actor.jobs.curDriver == null && SSCSelfTrainingFeedback.Calls == 0,
            "场景交互参与者变更后不能把双人或旧交互作为自我调教结算");

        f = new Scene(); f.StartScene();
        var old = f.Driver;
        f.Actor.jobs.EndCurrentJob(JobCondition.InterruptForced);
        var replacement = new JobDriver_Masturbate { pawn = f.Actor, job = new Job { loadID = 999 } };
        f.Actor.jobs.curDriver = replacement;
        int ended = f.Actor.jobs.EndCalls;
        old.Toils[1].initAction();
        old.Toils[3].initAction();
        check(f.Actor.jobs.curDriver == replacement && f.Actor.jobs.EndCalls == ended && SSCSelfTrainingFeedback.Calls == 0,
            "旧任务延迟启动或结算回调不能结束新任务或发奖");

        f = new Scene(); f.StartScene();
        f.Actor.needs.Corruption.CurLevel = 0.3f;
        f.Actor.relations.Opinions[f.Actor.Master] = -100;
        f.Actor.Master = new Pawn();
        f.Finish();
        check(Near(SSCSelfTrainingFeedback.LastSnapshot.Score, 19f) && Near(f.Actor.needs.Corruption.CurLevel, 0.3315f),
            "场景开始后改变关系或恶堕率不重新计算本次评分");

        f = new Scene(); f.StartScene();
        SSCSelfTrainingFeedback.OnFeedback = () =>
        {
            SSCSelfTrainingFeedback.OnFeedback = null;
            f.Driver.Toils[3].initAction();
        };
        f.Finish();
        check(SSCSelfTrainingFeedback.Calls == 1 && CorruptionUtility.AddCalls == 1,
            "反馈期间重入结算也只能领取一次");

        foreach (float current in new[] { 0.49f, 0.5f, 0.6f })
        {
            f = new Scene(); f.Actor.needs.Corruption.CurLevel = current; f.StartScene(); f.Finish();
            check(Near(f.Actor.needs.Corruption.CurLevel, Math.Max(current, 0.5f)) && SSCSelfTrainingFeedback.Calls == 1,
                "结算尊重阶段上限，已满或超过上限仍提供完成反馈且不扣回数值");
        }

        f = new Scene();
        f.Actor.Training.RegisterManualSelfTraining(f.Driver.job, f.Breast);
        f.Reload();
        f.StartScene();
        check(f.Driver.Sexprops.dictionaryKey == f.Breast && SexUtility.SelectionCalls == 0,
            "走路期间存读档后按原 Job 编号领取手动部位选择");
        f.Finish();
        check(SSCSelfTrainingFeedback.Calls == 1, "走路期间读档的手动任务可正常完成一次");

        f = new Scene(); f.StartScene();
        f.Driver.TickForHost(); f.Driver.TickForHost();
        f.Driver.orgasmstick = 2; f.Driver.orgasmStartTick = 11;
        f.Driver.ticks_between_hearts = 4; f.Driver.ticks_between_hits = 5; f.Driver.ticks_between_thrusts = 6;
        var beforeLoad = f.Driver;
        f.Reload();
        check(f.Driver != beforeLoad && f.Driver.Sexprops != beforeLoad.Sexprops && f.Driver.Sexprops.pawn == f.Actor,
            "读档创建新驱动和交互对象，并解析到恢复后的 Pawn 引用");
        check(f.Driver.ticks_left == 10 && f.Driver.sex_ticks == 10 && f.Driver.duration == 12 &&
            f.Driver.orgasmstick == 2 && f.Driver.orgasmStartTick == 11 && f.Driver.ticks_between_hearts == 4 &&
            f.Driver.ticks_between_hits == 5 && f.Driver.ticks_between_thrusts == 6 && f.Driver.Toils[1].defaultDuration == 12,
            "PostLoadInit 重建 toils 不覆盖已保存的场景进度和节奏");
        f.Actor.relations.Opinions[f.Actor.Master] = -100;
        f.Actor.needs.Corruption.CurLevel = 0.3f;
        f.Finish();
        check(f.Driver.StartCalls == 0 && f.Driver.AnimationInitCalls == 0 && SexUtility.SelectionCalls == 1,
            "执行中读档不会重新调用 Start 或重选交互");
        check(Near(f.Actor.needs.Corruption.CurLevel, 0.3315f) && SSCSelfTrainingFeedback.Calls == 1 &&
            SSCSelfTrainingFeedback.LastSnapshot.RawOpinionAtStart == 50,
            "执行中读档使用原始评分快照并正常结算一次");

        f = new Scene();
        f.Actor.Training.RegisterManualSelfTraining(f.Driver.job, f.Breast);
        f.StartScene(); f.Driver.TickForHost(); f.Reload();
        check(f.Driver.Sexprops.dictionaryKey == f.Breast && f.Driver.Sexprops.resolved.Interaction.Def == f.Breast,
            "执行中读档保留手动交互定义并重新解析部位");
        f.Finish();
        check(SexUtility.SelectionCalls == 0 && SSCSelfTrainingFeedback.Calls == 1,
            "手动场景恢复后不回退随机选型且可完成");

        f = new Scene(); f.StartScene(); f.Reload();
        f.Actor.jobs.EndCurrentJob(JobCondition.InterruptForced);
        check(SSCSelfTrainingFeedback.Calls == 0 && f.Driver.EndCalls == 1,
            "读档恢复后中断仍不结算完整收益");

        f = new Scene(); f.StartScene();
        // Inject a saved, already-claimed callback boundary while the Job is still
        // current, so this checks the persisted claim rather than current-job checks.
        f.Driver.ticks_left = 0;
        f.Driver.Toils[3].initAction();
        f.Reload(); f.Driver.ticks_left = 0; f.Driver.Toils[3].initAction();
        check(SSCSelfTrainingFeedback.Calls == 1 && CorruptionUtility.AddCalls == 1,
            "领取标记随存档恢复，重复完成回调不能再次发奖");
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.000001f;

    private sealed class Scene
    {
        public Pawn Actor;
        public JobDriver_SelfTraining Driver;
        public readonly InteractionDef Hand = new() { defName = "hand", Extension = new() { Type = xxx.rjwSextype.Masturbation } };
        public readonly InteractionDef Breast = new() { defName = "breast", Extension = new() { Type = xxx.rjwSextype.Masturbation, Reverse = true } };

        public Scene()
        {
            SexUtility.SelectionCalls = SexUtility.AftersexCalls = CorruptionUtility.AddCalls = SSCSelfTrainingFeedback.Calls = 0;
            SexUtility.Events.Clear(); SexUtility.SexInteractions.Clear(); Messages.Rejections.Clear();
            SSCSelfTrainingFeedback.OnFeedback = null; SSCSelfTrainingFeedback.LastSnapshot = null;
            Scribe.mode = LoadSaveMode.Inactive; Scribe.LoadedPawns.Clear();
            SexUtility.SexInteractions.AddRange(new[] { Hand, Breast });
            Actor = new Pawn { Master = new Pawn { LabelShort = "master" } };
            Actor.Available.UnionWith(new[] { Hand, Breast });
            Actor.relations.Opinions[Actor.Master] = 50;
            Driver = CreateDriver(Actor, 47);
            Driver.BuildForHost(); Driver.AdvanceForHost(); // enter walking toil
        }
        private static JobDriver_SelfTraining CreateDriver(Pawn actor, int id)
        {
            var driver = new JobDriver_SelfTraining
            {
                pawn = actor,
                job = new Job { loadID = id, playerForced = true, targetA = new(actor), targetC = new(new IntVec3(3, 3)) }
            };
            actor.jobs.curDriver = driver; actor.CurJob = driver.job;
            return driver;
        }
        public void StartScene() => Driver.AdvanceForHost();
        public void Finish()
        {
            for (int i = 0; i < 100 && Actor.jobs.curDriver == Driver; i++) Driver.TickForHost();
            if (Actor.jobs.curDriver == Driver) throw new Exception("Scene did not finish within the expected host ticks.");
        }
        public void Reload()
        {
            Scribe.Data = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving;
            Driver.ExposeData(); Actor.Training.ExposeForTest();
            var previous = Actor;
            Actor = new Pawn { Master = previous.Master, Trainer = previous.Trainer, Chain = previous.Chain,
                ChainCap = previous.ChainCap, Stage = previous.Stage, AllowSelfTraining = previous.AllowSelfTraining };
            Actor.Available.UnionWith(previous.Available);
            Actor.needs.Corruption.CurLevel = previous.needs.Corruption.CurLevel;
            foreach (var pair in previous.relations.Opinions) Actor.relations.Opinions[pair.Key] = pair.Value;
            Scribe.LoadedPawns[previous] = Actor;
            Driver = CreateDriver(Actor, Driver.job.loadID);
            foreach (var mode in new[] { LoadSaveMode.LoadingVars, LoadSaveMode.ResolvingCrossRefs, LoadSaveMode.PostLoadInit })
            {
                Scribe.mode = mode; Driver.ExposeData(); Actor.Training.ExposeForTest();
            }
            Scribe.mode = LoadSaveMode.Inactive;
        }
    }
}
