// AI 辅助生成
using System;
using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>通过生产恢复入口建立测试人格成长，后续经验只允许从真实任务结算写入。</summary>
    private static void SetGrowth(Pawn pawn, float experience)
        => Require(SSCMasterProgressionUtility.RestoreRecord(pawn, experience), "growth setup failed");

    private static float Growth(Pawn pawn) => SSCMasterProgressionUtility.GetExperience(pawn);

    /// <summary>完整运行日常场景但停在收益步骤前，使取消与正常完成使用相同的真实驱动。</summary>
    private static void CompleteGrowthDailyScene((JobDriver_Training driver, Toil[] toils, CompSexSlaveTraining comp) f)
    {
        f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
        f.driver.ticks_left = 1; f.toils[3].tickAction(); f.toils[3].Finish();
    }

    /// <summary>完成认领、经验归属、评分独立和健康回调重入均联通真实成长工具。</summary>
    private static void RunMasterProgressionTests()
    {
        foreach (bool forced in new[] { false, true })
            Check($"Master daily reward is fixed and claimed once; forced={forced}", () =>
            {
                var f = CompletedDaily(); f.driver.job.playerForced = forced; SetGrowth(f.driver.pawn, 11);
                // 原有结算必须看到升级前的经验；跨阶段加成不能参与本次评分。
                TestWorld.OnDailyOutcome = () => Equal(11f, Growth(f.driver.pawn), "experience during scoring outcome");
                TestWorld.OnTrainerFeedback = () => f.toils[4].initAction();
                f.toils[4].initAction(); f.toils[4].initAction();
                Equal(12f, Growth(f.driver.pawn), "daily +1, including reentrant callbacks");
                Equal(1, SSCMasterProgressionUtility.GetRecord(f.driver.pawn).CurStageIndex, "daily threshold stage");
                Equal(1, TestWorld.DailyOutcomes, "single formal outcome");
                // 消息观察的是生产经验入口输出；重入及第二次调用不能重复报升阶。
                var message = Messages.Received.Single(m => m.Text == "SSC_MasterProgression_StageAdvanced");
                Require(message.Targets.Pawns.Single() == f.driver.pawn &&
                    message.Type == RimWorld.MessageTypeDefOf.PositiveEvent && !message.Historical, "level-up target and message type");
            });
        Check("full specialization and zero score still provide Master daily practice", () =>
        {
            var f = CompletedDaily(); SetGrowth(f.driver.pawn, 5);
            f.comp.specializationProgress = 1f;
            TestWorld.DailyScoreOverride = 0f;
            f.toils[4].initAction();
            Equal(0f, TestWorld.OutcomeScore, "zero score fixture");
            Equal(6f, Growth(f.driver.pawn), "fixed practice despite full target");
        });
        Check("ordinary daily interruption grants no Master practice", () =>
        {
            var f = Daily(); SetGrowth(f.driver.pawn, 5);
            f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
            f.driver.ticks_left = 50; f.toils[3].Finish(); f.driver.Finish(JobCondition.InterruptForced);
            f.toils[4].initAction();
            Equal(5f, Growth(f.driver.pawn), "interrupted daily");
        });
        foreach (bool claimed in new[] { false, true })
            Check($"daily saved claim and real Hediff severity survive reconstruction; claimed={claimed}", () =>
            {
                var f = CompletedDaily(); SetGrowth(f.driver.pawn, 8);
                if (claimed) f.toils[4].initAction();
                // 执行真实 Job 与 Hediff 字段读写。宿主字典不模拟原版跨对象引用解析。
                Scribe.mode = LoadSaveMode.Saving; f.driver.ExposeData();
                SSCMasterProgressionUtility.GetRecord(f.driver.pawn).ExposeData();
                Scribe.mode = LoadSaveMode.Inactive;
                var loaded = CompletedDaily(); SetGrowth(loaded.driver.pawn, 0);
                Scribe.mode = LoadSaveMode.LoadingVars; loaded.driver.ExposeData();
                SSCMasterProgressionUtility.GetRecord(loaded.driver.pawn).ExposeData();
                Scribe.mode = LoadSaveMode.PostLoadInit; loaded.driver.ExposeData();
                Scribe.mode = LoadSaveMode.Inactive; loaded.toils[4].initAction(); loaded.toils[4].initAction();
                Equal(9f, Growth(loaded.driver.pawn), "one reward across reconstruction");
            });
        Check("trainer officer daily work grants no experience to the officer or its owner", () =>
        {
            var f = Daily(); Pawn actor = f.driver.pawn;
            Pawn owner = TestWorld.NewPawn(); owner.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
            SetGrowth(owner, 8); SetGrowth(actor, 4);
            actor.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Slave;
            actor.TryGetComp<CompSexSlaveTraining>().slaveTrainerEnabled = true;
            actor.BoundMaster = owner; f.driver.Partner.BoundMaster = owner;
            f.comp.restrictionConfig.rules.receiveTraining = true;
            CompleteGrowthDailyScene(f); f.toils[4].initAction();
            Equal(1, TestWorld.DailyOutcomes, "officer actually completed legal daily work");
            Equal(4f, Growth(actor), "inactive officer growth"); Equal(8f, Growth(owner), "no proxy owner reward");
        });
        Check("authorized other Master earns its own practice without rewarding the bound owner", () =>
        {
            var f = Daily(); SetGrowth(f.driver.pawn, 4);
            Pawn owner = TestWorld.NewPawn(); owner.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
            SetGrowth(owner, 8); f.driver.Partner.BoundMaster = owner;
            f.comp.restrictionConfig.rules.receiveTraining = true;
            CompleteGrowthDailyScene(f); f.toils[4].initAction();
            Equal(1, TestWorld.DailyOutcomes, "authorized non-owner actually completed");
            Equal(5f, Growth(f.driver.pawn), "actual Master performer"); Equal(8f, Growth(owner), "unrelated owner");
        });
        Check("modern Master with no growth record receives no implicit baseline from daily work", () =>
        {
            var f = CompletedDaily(); f.toils[4].initAction();
            Equal(-1f, Growth(f.driver.pawn), "no implicit creation");
        });
        foreach (string boundary in new[] { "target", "actor", "hollow", "dead", "replacement", "exception" })
            Check($"daily reward stops at invalid completion boundary: {boundary}", () =>
            {
                var f = CompletedDaily(); SetGrowth(f.driver.pawn, 5);
                if (boundary == "exception") TestWorld.OnDailyOutcome = () => throw new InvalidOperationException("failed outcome");
                else TestWorld.OnTrainerFeedback = () =>
                {
                    if (boundary == "target") f.comp.pawnIdentity = PawnIdentity.Unset;
                    if (boundary == "actor") f.driver.pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                    if (boundary == "hollow") f.driver.pawn.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
                    if (boundary == "dead") f.driver.pawn.Dead = true;
                    if (boundary == "replacement") Daily(f.driver.pawn);
                };
                bool thrown = false;
                try { f.toils[4].initAction(); } catch (InvalidOperationException) { thrown = true; }
                Equal(boundary == "exception", thrown, "expected exception");
                TestWorld.OnDailyOutcome = TestWorld.OnTrainerFeedback = null;
                f.toils[4].initAction();
                Equal(5f, Growth(f.driver.pawn), "invalid or replayed reward");
            });
        foreach (bool ritual in new[] { false, true })
            Check($"real practice respects cap; ritual={ritual}", () =>
            {
                Pawn actor;
                if (ritual)
                {
                    var f = new RitualFixture(); actor = f.Master; SetGrowth(actor, 98); Advance(f, 6);
                    ApplyFeedbackOutcome(FeedbackOutcome(), f);
                    ApplyFeedbackOutcome(FeedbackOutcome(), f);
                }
                else
                {
                    var f = CompletedDaily(); actor = f.driver.pawn; SetGrowth(actor, 99); f.toils[4].initAction();
                }
                Equal(100f, Growth(actor), "highest stage cap");
                Equal(3, SSCMasterProgressionUtility.GetRecord(actor).CurStageIndex, "highest stage index");
                Equal(0f, SSCMasterProgressionUtility.TryAddExperience(actor, 1), "full record remains capped");
                Equal(1, Messages.Received.Count(m => m.Text == "SSC_MasterProgression_StageAdvanced"),
                    "one level-up across cap and repeated whole/daily rewards");
            });
        Check("six real ritual stages grant +3 only at whole outcome, including poor outcomes", () =>
        {
            var f = new RitualFixture(); SetGrowth(f.Master, 0); f.Comp.specializationProgress = 1f;
            var worker = FeedbackOutcome(false);
            for (int phase = 1; phase <= 6; phase++)
            {
                var execution = new PhaseExecution(f);
                execution.StartScene(); execution.ReachPhaseEnd(); execution.FinishCurrent(JobCondition.Succeeded);
                Equal(0f, Growth(f.Master), "no per-phase reward");
                if (phase < 6) ApplyFeedbackOutcome(worker, f);
            }
            TestWorld.OnRitualOutcome = () => Equal(0f, Growth(f.Master), "growth before ritual scoring outcome");
            ApplyFeedbackOutcome(worker, f); ApplyFeedbackOutcome(worker, f);
            Equal(3f, Growth(f.Master), "one whole +3 regardless of quality");
            Equal(1, TestWorld.RitualLetters, "one result letter");
        });
        Check("officer in ritual master role does not receive or proxy Master experience", () =>
        {
            var f = OfficerRitual(); SetGrowth(f.Master, 4); SetGrowth(f.Slave.BoundMaster, 8);
            Advance(f, 6); ApplyFeedbackOutcome(FeedbackOutcome(), f);
            Equal(1, TestWorld.RitualOutcomes, "officer actually hosted whole ritual");
            Equal(4f, Growth(f.Master), "role label is not SSC Master identity");
            Equal(8f, Growth(f.Slave.BoundMaster), "owner receives no proxy credit");
        });
        Check("cancelled ritual cannot regain practice through late Apply", () =>
        {
            var f = new RitualFixture(); SetGrowth(f.Master, 5); Advance(f, 3); f.End();
            ApplyFeedbackOutcome(FeedbackOutcome(), f); ApplyFeedbackOutcome(FeedbackOutcome(), f);
            Equal(5f, Growth(f.Master), "cancelled ritual reward");
        });
        foreach (bool claimed in new[] { false, true })
            Check($"restored whole ritual claim controls Master practice; claimed={claimed}", () =>
            {
                var f = new RitualFixture(); SetGrowth(f.Master, 8); Advance(f, 6); var worker = FeedbackOutcome();
                if (claimed) ApplyFeedbackOutcome(worker, f);
                int phase = f.Comp.ritualPhase; bool savedClaim = f.Comp.bindingRitualOutcomeClaimed;
                f.RemoveLordWithoutCleanup();
                // 模拟已恢复的 Lord 及主从引用；认领、恢复核对和 Apply 执行生产算法。
                var loaded = new RitualFixture(f.Master, f.Slave);
                loaded.Comp.bindingRitualLord = loaded.Lord;
                loaded.Comp.ritualPhase = phase; loaded.Comp.bindingRitualOutcomeClaimed = savedClaim;
                BindingRitualStateUtility.RecoverPawnState(loaded.Slave);
                ApplyFeedbackOutcome(worker, loaded); ApplyFeedbackOutcome(worker, loaded);
                Equal(11f, Growth(loaded.Master), "single +3 across ritual restore");
            });
        foreach (string boundary in new[] { "target", "actor", "replacement", "exception" })
            Check($"whole ritual reward stops at invalid completion boundary: {boundary}", () =>
            {
                var f = new RitualFixture(); SetGrowth(f.Master, 5); Advance(f, 6); var worker = FeedbackOutcome();
                TestWorld.OnRitualOutcome = () =>
                {
                    if (boundary == "target") f.Comp.pawnIdentity = PawnIdentity.Unset;
                    if (boundary == "actor") f.Master.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                    if (boundary == "replacement") { f.End(); var next = new RitualFixture(f.Master, f.Slave); Advance(next, 2); }
                    if (boundary == "exception") throw new InvalidOperationException("failed whole outcome");
                };
                bool thrown = false;
                try { ApplyFeedbackOutcome(worker, f); } catch (InvalidOperationException) { thrown = true; }
                Equal(boundary == "exception", thrown, "expected ritual exception");
                TestWorld.OnRitualOutcome = null; ApplyFeedbackOutcome(worker, f);
                Equal(5f, Growth(f.Master), "invalid or replayed whole reward");
            });
        foreach (bool ritual in new[] { false, true })
            Check($"stage-change health callback cannot replay or continue stale outcome; ritual={ritual}", () =>
            {
                if (ritual)
                {
                    var f = new RitualFixture(); SetGrowth(f.Master, 11); Advance(f, 6); var worker = FeedbackOutcome();
                    RitualFixture next = null;
                    f.Master.health.OnHediffChanged = () =>
                    {
                        ApplyFeedbackOutcome(worker, f); f.End(); next = new RitualFixture(f.Master, f.Slave); Advance(next, 2);
                    };
                    ApplyFeedbackOutcome(worker, f); ApplyFeedbackOutcome(worker, f);
                    Equal(14f, Growth(f.Master), "single entered ritual award");
                    Equal(0, TestWorld.RitualLetters, "no stale letter after health callback");
                    Equal(2, next.Comp.ritualPhase, "replacement ritual phase preserved");
                }
                else
                {
                    var f = CompletedDaily(); SetGrowth(f.driver.pawn, 11); CompSexSlaveTraining next = null;
                    f.driver.pawn.health.OnHediffChanged = () =>
                    {
                        f.toils[4].initAction(); var replacement = Daily(f.driver.pawn); next = replacement.comp;
                        replacement.toils[0].initAction(); next.lastTrainingTick = 12345;
                    };
                    f.toils[4].initAction(); f.toils[4].initAction();
                    Equal(12f, Growth(f.driver.pawn), "single entered daily award");
                    Require(next.isBeingTrained, "old outcome cleared replacement occupancy");
                    Equal(12345, next.lastTrainingTick, "replacement cooldown preserved");
                }
            });
    }
}
