using System;
using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Attraction;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void ResetRjw()
    {
        RJWHookupSettings.HookupsEnabled = RJWHookupSettings.QuickHookupsEnabled = true;
        RJWHookupSettings.NymphosCanCheat = RJWSettings.WildMode = RJWSettings.HippieMode = LovePartnerRelationUtility.Lovers = false;
        CasualSex_Helper.AllowedSettings = SexAppraiser.Accepted = true; SexAppraiser.LastResult = null;
        CasualSex_Helper.OnCanHaveSex = null;
        DefDatabase<JobDef>.Definitions.Clear();
        DefDatabase<JobDef>.Definitions.Add("SSC_Job_PetAffectionFollowup", SSCDefOf.SSC_Job_PetAffectionFollowup);
    }
    private static void ConfigurePair(Pawn pet, Pawn master) { }
    private static void RunUtilityCases()
    {
        Run("固定成年猫狗配对可用且不依赖接近距离或普通空闲", () =>
        {
            foreach (bool dog in new[] { false, true })
            {
                var p = Pair(); p.pet.Cat = !dog; p.pet.Dog = dog;
                p.pet.Position = new(99, 99); p.pet.Idle = p.master.Idle = false;
                Assert(PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "固定猫狗合法配对被拒绝");
                Assert(SexAppraiser.LastResult.Actor == p.pet && SexAppraiser.LastResult.Target == p.master, "吸引力评估改变了发起者/主人");
            }
            Assert(Rand.Chances.Count == 0, "只读资格检查不应消耗触发骰");
        });
        Run("后续拒绝兔、残留效果、无训练组件及实际主人不匹配", () =>
        {
            foreach (int kind in Enumerable.Range(0, 5))
            {
                var p = Pair();
                if (kind == 0) p.pet.Cat = p.pet.Dog = false;
                if (kind == 1) p.pet.HasTraining = false;
                if (kind == 2) p.pet.BoundMaster = null;
                if (kind == 3) p.pet.BoundMaster = new Pawn();
                if (kind == 4) p.master = p.pet;
                Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "无效资格/绑定放行 " + kind);
            }
            Assert(!PetAffectionFollowupUtility.CanContinue(null, null), "null配对放行");
        });
        Run("后续双方须人形、18岁、成年发展阶段及RJW成年类别", () =>
        {
            for (int side = 0; side < 2; side++)
                for (int kind = 0; kind < 6; kind++)
                {
                    var p = Pair(); Pawn subject = side == 0 ? p.pet : p.master;
                    if (kind == 0) subject.RaceProps.Humanlike = false;
                    if (kind == 1) subject.ageTracker.AgeBiologicalYears = 17;
                    if (kind == 2) subject.DevelopmentalStage = DevelopmentalStage.Child;
                    if (kind == 3) subject.AdultCategory = false;
                    if (kind == 4) subject.ageTracker = null;
                    if (kind == 5) subject.RaceProps = null;
                    Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "成年或人形边界放行 " + side + "/" + kind);
                }
        });
        Run("后续双方生命意识睡眠征召精神及战斗条件逐项拒绝", () =>
        {
            for (int side = 0; side < 2; side++)
                for (int kind = 0; kind < 10; kind++)
                {
                    var p = Pair(); Pawn subject = side == 0 ? p.pet : p.master;
                    if (kind == 0) subject.Dead = true;
                    if (kind == 1) subject.Destroyed = true;
                    if (kind == 2) subject.Spawned = false;
                    if (kind == 3) subject.Downed = true;
                    if (kind == 4) subject.Drafted = true;
                    if (kind == 5) subject.InMentalState = true;
                    if (kind == 6) subject.health.capacities.CanBeAwake = false;
                    if (kind == 7) subject.AwakeNow = false;
                    if (kind == 8) subject.Fighting = true;
                    if (kind == 9) subject.health = null;
                    Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "状态边界放行 " + side + "/" + kind);
                }
            var differentMap = Pair(); differentMap.master.Map = new();
            Assert(!PetAffectionFollowupUtility.CanContinue(differentMap.pet, differentMap.master), "跨图配对放行");
        });
        Run("后续保留RJW双向身体冷却与目标资格", () =>
        {
            for (int side = 0; side < 2; side++)
                for (int kind = 0; kind < 4; kind++)
                {
                    var p = Pair(); Pawn subject = side == 0 ? p.pet : p.master;
                    if (kind == 0) subject.CanHaveSex = false;
                    if (kind == 1) subject.ReadyForLovin = false;
                    if (kind == 2) subject.ReadyForHookup = false;
                    if (kind == 3) subject.CanTargetHookup = false;
                    Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "RJW边界放行 " + side + "/" + kind);
                }
        });
        Run("后续尊重全局Hookup与QuickHookup开关", () =>
        {
            var p = Pair(); RJWHookupSettings.HookupsEnabled = false;
            Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "Hookups关闭仍可用");
            RJWHookupSettings.HookupsEnabled = true; RJWHookupSettings.QuickHookupsEnabled = false;
            Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "Quick关闭仍可用");
        });
        Run("后续吸引力与玩家配对设置分别拒绝固定配对", () =>
        {
            var p = Pair(); SexAppraiser.Accepted = false;
            Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "吸引力拒绝未生效");
            SexAppraiser.Accepted = true; CasualSex_Helper.AllowedSettings = false;
            Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "配对设置拒绝未生效");
            NoRollOrStart(p.pet);
        });
        Run("双方需存在欲求，主人身份不能代替意愿", () =>
        {
            for (int side = 0; side < 2; side++)
            {
                var p = Pair(); (side == 0 ? p.pet : p.master).Horny = false;
                Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "无欲求配对放行");
            }
            var lovers = Pair(); lovers.pet.Horny = lovers.master.Horny = false; LovePartnerRelationUtility.Lovers = true;
            Assert(PetAffectionFollowupUtility.CanContinue(lovers.pet, lovers.master), "真实爱侣应沿用原资格");
        });
        Run("非爱侣双方休闲意愿拒绝，模式和既有RJW例外照常生效", () =>
        {
            for (int side = 0; side < 2; side++)
            {
                Reset(); var p = Pair(); Pawn subject = side == 0 ? p.pet : p.master; subject.FoolAround = false;
                Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "单端休闲拒绝被忽略");
                subject.BeerGoggles = true;
                Assert(PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "BeerGoggles原例外未保留");
                subject.BeerGoggles = false; subject.Nympho = subject.Frustrated = true;
                Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "未开Cheat仍放行");
                RJWHookupSettings.NymphosCanCheat = true;
                Assert(PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "NymphosCanCheat原例外未保留");
            }
        });
        Run("Wild和Hippie仅跳过欲求与休闲检查，仍保留身体和配对门槛", () =>
        {
            foreach (bool wild in new[] { false, true })
            {
                Reset(); var p = Pair(); RJWSettings.WildMode = wild; RJWSettings.HippieMode = !wild;
                p.pet.Horny = p.master.Horny = p.pet.FoolAround = p.master.FoolAround = false;
                Assert(PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "模式未沿用");
                p.master.ReadyForLovin = false;
                Assert(!PetAffectionFollowupUtility.CanContinue(p.pet, p.master), "模式绕过冷却");
            }
        });
        Run("准备仅接受当前确切非强制Wait和有效专用JobDef", () =>
        {
            for (int kind = 0; kind < 5; kind++)
            {
                Reset(); var p = Pair(); Job wait = p.waiting;
                if (kind == 0) wait = null;
                if (kind == 1) wait = new Job { def = JobDefOf.Wait };
                if (kind == 2) wait.playerForced = true;
                if (kind == 3) DefDatabase<JobDef>.Definitions.Clear();
                if (kind == 4) SSCDefOf.SSC_Job_PetAffectionFollowup.driverClass = typeof(JobDriver_SexQuick);
                Assert(PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, wait) == null, "准备无效等待/JobDef放行 " + kind);
                NoRollOrStart(p.pet);
            }
        });
        Run("统一许可先于20%概率，拒绝不消耗骰且不抢占工作", () =>
        {
            var p = Pair(); SSCRestrictionJobGuard.PrepareResult = false;
            Assert(PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting) == null, "许可拒绝仍返回候选");
            NoRollOrStart(p.pet);
            Assert(SSCRestrictionJobGuard.PrepareCalls == 1 && SSCRestrictionJobGuard.Source == SSCRestrictionEvent.PetAffection &&
                SSCRestrictionJobGuard.PreparedActor == p.pet && SSCRestrictionJobGuard.PreparedJob.GetTarget(TargetIndex.A).Pawn == p.master &&
                !SSCRestrictionJobGuard.PreparedJob.playerForced && JobMaker.Returned.Count == 1, "许可方向/来源或回收不正确");
        });
        Run("概率未命中撤销候选，命中只准备固定主人而不提前启动", () =>
        {
            var p = Pair(); Rand.ChanceResult = false;
            Assert(PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting) == null && Rand.Chances.Single() == .20f &&
                SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 1 && p.master.CurJob == p.waiting,
                "未命中处理错误");
            Reset(); p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting);
            Assert(prepared != null && Rand.Chances.Single() == .20f && prepared.GetTarget(TargetIndex.A).Pawn == p.master &&
                p.pet.jobs.StartCalls == 0 && SSCRestrictionJobGuard.CancelCalls == 0, "命中仍提前启动或变更目标");
        });
        Run("准备回调异常撤销Pending并回收候选，保留原等待", () =>
        {
            var p = Pair(); var original = new InvalidOperationException("prepare"); SSCRestrictionJobGuard.OnPrepare = () => throw original;
            Exception observed = null; try { PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting); } catch (Exception error) { observed = error; }
            Assert(ReferenceEquals(observed, original) && SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 1 && p.master.CurJob == p.waiting,
                "预检异常泄漏候选或提前清理等待");
        });
        Run("交接登记精确等待并从当前时刻延长600tick准备窗口", () =>
        {
            var p = Pair(); p.waiting.startTick -= 120;
            Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting);
            Assert(PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting), "调度命中失败");
            Assert(SSCRestrictionJobGuard.RegisteredJob == prepared && SSCRestrictionJobGuard.RegisteredWait == p.waiting &&
                SSCRestrictionJobGuard.RegisteredTarget == p.master && p.waiting.expiryInterval == 720 && p.master.CurJob == p.waiting,
                "等待交接或过期计算错误");
        });
        Run("交接前失效释放仅原等待，候选只回收一次且不重新掷骰", () =>
        {
            var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting); p.pet.BoundMaster = null;
            Assert(!PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting), "失效绑定仍启动");
            Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1 && JobMaker.Returned.Count == 1 && Rand.Chances.Count == 1,
                "失效交接清理/回收/概率错误");
        });
        Run("StartJob拒绝及抛异常清原等待但不回收已由引擎接管的Job", () =>
        {
            foreach (bool throwing in new[] { false, true })
            {
                Reset(); var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting);
                p.pet.jobs.AcceptStart = false; if (throwing) p.pet.jobs.OnStart = _ => throw new InvalidOperationException("start");
                try { Assert(!PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting), "StartJob拒绝仍返回成功"); }
                catch (InvalidOperationException error) when (throwing && error.Message == "start") { }
                Assert(p.master.CurJob == null && p.master.jobs.EndCalls == 1 && SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 0,
                    "安装失败清理泄漏等待/二次回收");
            }
        });
        Run("调度异常时保留主人和宠物后来替换的新工作及无关队列", () =>
        {
            var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting);
            Job masterWork = new(), petWork = new(), queued = new();
            p.pet.jobs.OnStart = _ => { p.pet.jobs.jobQueue.EnqueueLast(queued); p.master.jobs.curJob = masterWork; p.pet.jobs.curJob = petWork; throw new InvalidOperationException("start"); };
            try { PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting); } catch (InvalidOperationException) { }
            Assert(p.master.CurJob == masterWork && p.pet.CurJob == petWork && p.pet.jobs.jobQueue.Single().job == queued,
                "异常清理修改了后来工作或无关命令");
        });
        Run("资格查询重入替换宠物任务或主人等待后不得准备候选", () =>
        {
            foreach (bool actor in new[] { false, true })
            {
                Reset(); var p = Pair(); Job replacement = new(); bool fired = false;
                CasualSex_Helper.OnCanHaveSex = () =>
                { if (fired) return; fired = true; if (actor) p.pet.jobs.curJob = replacement; else p.master.jobs.curJob = replacement; };
                Assert(PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting) == null, "资格查询替换后仍准备");
                NoRollOrStart(p.pet); Assert(JobMaker.Made == 0 && SSCRestrictionJobGuard.PrepareCalls == 0, "已变更的源任务仍发起预检");
            }
        });
        Run("Prepare登记重入替换精确等待或复用编号后不掷骰", () =>
        {
            foreach (bool reuse in new[] { false, true })
            {
                Reset(); var p = Pair();
                SSCRestrictionJobGuard.OnPrepare = () => { if (reuse) p.waiting.loadID++; else p.master.jobs.curJob = new Job { def = JobDefOf.Wait }; };
                Assert(PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting) == null && Rand.Chances.Count == 0 &&
                    SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 1, "Prepare重入仍掷骰或泄漏候选");
            }
        });
        Run("Register等待登记重入替换宠物任务或主人等待后不安装候选", () =>
        {
            foreach (bool actor in new[] { false, true })
            {
                Reset(); var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting); Job replacement = new();
                SSCRestrictionJobGuard.OnRegister = () => { if (actor) p.pet.jobs.curJob = replacement; else p.master.jobs.curJob = replacement; };
                Assert(!PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting) && p.pet.jobs.StartCalls == 0 &&
                    SSCRestrictionJobGuard.CancelCalls == 1 && JobMaker.Returned.Count == 1, "Register重入仍安装或泄漏候选");
                if (actor) Assert(p.pet.CurJob == replacement && p.master.CurJob == null, "清理误改新宠物工作或泄漏原等待");
                else Assert(p.master.CurJob == replacement && p.master.jobs.EndCalls == 0, "清理误改新主人工作");
            }
        });
        Run("StartJob把同候选实例回收到新编号后不认领或撤销新场景事件", () =>
        {
            var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting);
            p.pet.jobs.OnStart = current => current.loadID++;
            Assert(!PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting), "同引用池复用冒充安装成功");
            Assert(p.pet.CurJob == prepared && SSCRestrictionJobGuard.CancelCalls == 0 && JobMaker.Returned.Count == 0 && p.master.CurJob == null,
                "池复用的新事件被旧finally取消或二次归池");
        });
        Run("StartJob拒绝时只清本次候选队列而保留宠物无关命令", () =>
        {
            var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting); Job unrelated = new();
            p.pet.jobs.AcceptStart = false;
            p.pet.jobs.OnStart = current => { p.pet.jobs.jobQueue.EnqueueLast(unrelated); p.pet.jobs.jobQueue.EnqueueLast(current); };
            Assert(!PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting), "被拒绝候选返回成功");
            Assert(p.pet.jobs.jobQueue.Single().job == unrelated && p.master.CurJob == null && SSCRestrictionJobGuard.CancelCalls == 1,
                "安装失败清空他人队列或残留本候选");
        });
        Run("等待实例同引用但新编号在Register中出现时不误清池复用任务", () =>
        {
            var p = Pair(); Job prepared = PetAffectionFollowupUtility.TryPrepare(p.pet, p.master, p.waiting);
            SSCRestrictionJobGuard.OnRegister = () => p.waiting.loadID++;
            Assert(!PetAffectionFollowupUtility.TryStart(p.pet, p.master, prepared, p.waiting) && p.pet.jobs.StartCalls == 0 &&
                p.master.CurJob == p.waiting && p.master.jobs.EndCalls == 0, "池复用的新等待被旧finally清理");
        });
    }
}
