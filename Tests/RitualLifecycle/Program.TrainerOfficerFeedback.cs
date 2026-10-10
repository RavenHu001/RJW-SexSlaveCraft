using System;
using System.Collections.Generic;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>执行真实日常驱动完成步骤，验证职责通知沿用一次性结算且只来自实际完成事件。</summary>
    private static void RunTrainerOfficerFeedbackTests()
    {
        foreach (bool forced in new[] { false, true })
            Check($"daily actual completion calls duty feedback once for the actual trainer; forced={forced}", () =>
            {
                var f = CompletedDaily(); f.driver.job.playerForced = forced;
                TestWorld.OnTrainerFeedback = () => f.toils[4].initAction();
                f.toils[4].initAction(); f.toils[4].initAction();
                Equal(1, TestWorld.TrainerFeedbackAwards, "completed duty feedback");
                Equal(f.driver.pawn, TestWorld.FeedbackTrainer, "actual feedback trainer");
                Equal(f.driver.Partner, TestWorld.FeedbackReceiver, "actual feedback receiver");
                Equal(1, TestWorld.DailyCooldowns, "reentrant duty cooldown");
            });
        Check("saved claimed daily completion does not refresh duty feedback after reconstruction", () =>
        {
            var f = CompletedDaily(); f.toils[4].initAction();
            Scribe.mode = LoadSaveMode.Saving; f.driver.ExposeData();
            var loaded = CompletedDaily();
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive; loaded.toils[4].initAction();
            Equal(1, TestWorld.TrainerFeedbackAwards, "saved claimed duty feedback");
        });
        Check("saved unfinished daily completion can grant duty feedback once", () =>
        {
            var f = CompletedDaily();
            Scribe.mode = LoadSaveMode.Saving; f.driver.ExposeData();
            var loaded = CompletedDaily();
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive;
            loaded.toils[4].initAction(); loaded.toils[4].initAction();
            Equal(1, TestWorld.TrainerFeedbackAwards, "saved unfinished duty feedback");
        });
        Check("walking, interrupted scene and missing scene data grant no duty feedback", () =>
        {
            var f = Daily(); f.toils[0].initAction(); f.driver.Finish(JobCondition.InterruptForced);
            Equal(0, TestWorld.TrainerFeedbackAwards, "walking duty feedback");
            f = Daily(); f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
            f.driver.ticks_left = 50; f.toils[3].Finish(); f.toils[4].initAction();
            Equal(0, TestWorld.TrainerFeedbackAwards, "interrupted duty feedback");
            f = CompletedDaily(); f.driver.Sexprops = null; f.toils[4].initAction();
            Equal(0, TestWorld.TrainerFeedbackAwards, "missing scene duty feedback");
        });
        Check("failed formal daily outcome does not grant or replay duty feedback", () =>
        {
            var f = CompletedDaily();
            TestWorld.OnDailyOutcome = () => throw new InvalidOperationException("outcome");
            bool thrown = false;
            try { f.toils[4].initAction(); } catch (InvalidOperationException) { thrown = true; }
            Require(thrown, "failed formal outcome was swallowed");
            TestWorld.OnDailyOutcome = null; f.toils[4].initAction();
            Equal(0, TestWorld.TrainerFeedbackAwards, "failed outcome duty feedback");
        });
        Check("receiver identity loss after trainer experience blocks later duty feedback", () =>
        {
            var f = CompletedDaily();
            TestWorld.OnTrainerProgress = () => f.comp.pawnIdentity = PawnIdentity.Unset;
            f.toils[4].initAction();
            Equal(1, TestWorld.TrainerProgressAwards, "entered trainer experience");
            Equal(0, TestWorld.TrainerFeedbackAwards, "invalid receiver duty feedback");
            Equal(1, TestWorld.DailyCooldowns, "already completed daily cooldown");
        });
        Check("task replacement during trainer experience cannot grant stale duty feedback", () =>
        {
            var f = CompletedDaily(); CompSexSlaveTraining current = null;
            TestWorld.OnTrainerProgress = () =>
            {
                var next = Daily(f.driver.pawn); current = next.comp;
                next.toils[0].initAction(); current.lastTrainingTick = 12345;
            };
            f.toils[4].initAction();
            Equal(0, TestWorld.TrainerFeedbackAwards, "stale daily duty feedback");
            Require(current.isBeingTrained, "old payout cleared replacement occupancy");
            Equal(12345, current.lastTrainingTick, "replacement cooldown");
        });
        Check("task replacement during duty notification preserves replacement state and prevents later replay", () =>
        {
            var f = CompletedDaily(); CompSexSlaveTraining current = null;
            TestWorld.OnTrainerFeedback = () =>
            {
                var next = Daily(f.driver.pawn); current = next.comp;
                next.toils[0].initAction(); current.lastTrainingTick = 12345;
            };
            f.toils[4].initAction(); f.toils[4].initAction();
            Equal(1, TestWorld.TrainerFeedbackAwards, "single entered duty notification");
            Equal(1, TestWorld.DailyCooldowns, "single cooldown before feedback");
            Require(current.isBeingTrained, "feedback callback cleared replacement occupancy");
            Equal(12345, current.lastTrainingTick, "replacement cooldown after feedback");
        });
        RunTrainerOfficerRitualFeedbackTests();
    }

    /// <summary>构造获准主持的性奴执行者，实际任职资格由另一套生产反馈用例验证。</summary>
    private static RitualFixture OfficerRitual()
    {
        var fixture = NonOwnerRitual();
        var officer = fixture.Master.TryGetComp<CompSexSlaveTraining>();
        officer.pawnIdentity = PawnIdentity.Slave; officer.slaveTrainerEnabled = true;
        fixture.Master.BoundMaster = fixture.Slave.BoundMaster;
        return fixture;
    }

    /// <summary>整场宿主只提供一个确定结果，使测试运行生产 Apply 而不模拟原版随机抽取。</summary>
    private static RitualOutcomeEffectWorker_SSCBinding FeedbackOutcome(bool positive = true)
        => new RitualOutcomeEffectWorker_SSCBinding(new RitualOutcomeEffectDef
        {
            startingQuality = positive ? 0.5f : 0f,
            outcomeChances = new List<RitualOutcomePossibility> { new RitualOutcomePossibility { Positive = positive } }
        });

    /// <summary>空出席列表避开原版记忆分发；正式后果由接口记录，认领、结算编排和职责接线执行生产源码。</summary>
    private static void ApplyFeedbackOutcome(RitualOutcomeEffectWorker_SSCBinding worker, RitualFixture fixture)
        => worker.Apply(1f, new Dictionary<Pawn, int>(), fixture.Job);

    /// <summary>真实六阶段驱动和真实整场 Apply 联合验证取消、认领重入及恢复已消费字段的行为。</summary>
    private static void RunTrainerOfficerRitualFeedbackTests()
    {
        Check("six real ritual stages do not grant duty feedback until one whole outcome completes", () =>
        {
            var ritual = OfficerRitual(); var worker = FeedbackOutcome();
            for (int phase = 1; phase <= 6; phase++)
            {
                var execution = new PhaseExecution(ritual);
                execution.StartScene(); execution.ReachPhaseEnd(); execution.FinishCurrent(JobCondition.Succeeded);
                execution.FinishCurrent(JobCondition.Succeeded);
                Equal(phase, ritual.Comp.ritualPhase, "real completed phase");
                Equal(0, TestWorld.TrainerFeedbackAwards, "per-stage feedback");
                if (phase < 6) ApplyFeedbackOutcome(worker, ritual);
            }
            TestWorld.OnTrainerFeedback = () => ApplyFeedbackOutcome(worker, ritual);
            ApplyFeedbackOutcome(worker, ritual); ApplyFeedbackOutcome(worker, ritual);
            Equal(1, TestWorld.RitualOutcomes, "whole ritual outcome");
            Equal(1, TestWorld.TrainerFeedbackAwards, "whole ritual duty feedback");
            Equal(ritual.Master, TestWorld.FeedbackTrainer, "actual ritual officer");
            Equal(ritual.Slave, TestWorld.FeedbackReceiver, "actual ritual receiver");
            Equal(1, TestWorld.RitualLetters, "single whole ritual letter");
            Equal(0, TestWorld.DailyCooldowns, "ritual daily cooldown");
        });
        Check("poor but normally completed whole ritual still grants duty feedback", () =>
        {
            var ritual = OfficerRitual(); Advance(ritual, 6);
            ApplyFeedbackOutcome(FeedbackOutcome(false), ritual);
            Equal(1, TestWorld.TrainerFeedbackAwards, "negative outcome duty feedback");
            Equal(0f, TestWorld.CombatantScore, "zero quality score");
        });
        Check("cancelled ritual does not grant duty feedback or regain it after late outcome callbacks", () =>
        {
            var ritual = OfficerRitual(); Advance(ritual, 3); ritual.End();
            var worker = FeedbackOutcome();
            ApplyFeedbackOutcome(worker, ritual); ApplyFeedbackOutcome(worker, ritual);
            Equal(0, TestWorld.TrainerFeedbackAwards, "cancelled ritual duty feedback");
            Equal(0, TestWorld.RitualOutcomes, "cancelled whole outcome");
        });
        foreach (bool alreadyClaimed in new[] { false, true })
            Check($"restored whole ritual claim controls duty feedback; claimed={alreadyClaimed}", () =>
            {
                var previous = OfficerRitual(); Advance(previous, 6); var worker = FeedbackOutcome();
                if (alreadyClaimed) ApplyFeedbackOutcome(worker, previous);
                int restoredPhase = previous.Comp.ritualPhase;
                bool restoredClaim = previous.Comp.bindingRitualOutcomeClaimed;
                previous.RemoveLordWithoutCleanup();
                // 模拟 Scribe 引用恢复后的同一场 Lord/角色与保存组件字段；不把真实新场当旧场。
                var loaded = new RitualFixture(previous.Master, previous.Slave);
                loaded.Comp.bindingRitualLord = loaded.Lord;
                loaded.Comp.ritualPhase = restoredPhase; loaded.Comp.bindingRitualOutcomeClaimed = restoredClaim;
                BindingRitualStateUtility.RecoverPawnState(loaded.Slave);
                ApplyFeedbackOutcome(worker, loaded); ApplyFeedbackOutcome(worker, loaded);
                Equal(1, TestWorld.TrainerFeedbackAwards, "restored total duty feedback");
                Equal(1, TestWorld.RitualOutcomes, "restored total whole outcome");
            });
        Check("failed formal whole ritual outcome grants no duty feedback and cannot replay", () =>
        {
            var ritual = OfficerRitual(); Advance(ritual, 6); var worker = FeedbackOutcome();
            TestWorld.OnRitualOutcome = () => throw new InvalidOperationException("ritual outcome");
            bool thrown = false;
            try { ApplyFeedbackOutcome(worker, ritual); } catch (InvalidOperationException) { thrown = true; }
            Require(thrown, "whole outcome error was swallowed");
            TestWorld.OnRitualOutcome = null; ApplyFeedbackOutcome(worker, ritual);
            Equal(0, TestWorld.TrainerFeedbackAwards, "failed whole outcome duty feedback");
            Equal(1, TestWorld.RitualOutcomes, "failed whole outcome replay");
        });
        Check("identity loss during ritual trainer experience blocks duty feedback and later letter", () =>
        {
            var ritual = OfficerRitual(); Advance(ritual, 6); var worker = FeedbackOutcome();
            TestWorld.OnTrainerProgress = () => ritual.Comp.pawnIdentity = PawnIdentity.Unset;
            ApplyFeedbackOutcome(worker, ritual);
            Equal(1, TestWorld.TrainerProgressAwards, "entered ritual trainer experience");
            Equal(0, TestWorld.TrainerFeedbackAwards, "invalid ritual receiver duty feedback");
            Equal(0, TestWorld.RitualLetters, "invalid ritual receiver letter");
            Equal(1, ritual.Job.CancelCalls, "invalid ritual cancellation");
        });
        Check("replacement ritual during formal whole outcome prevents old duty feedback", () =>
        {
            var previous = OfficerRitual(); Advance(previous, 6); var worker = FeedbackOutcome();
            RitualFixture current = null;
            TestWorld.OnRitualOutcome = () =>
            {
                previous.End(); current = new RitualFixture(previous.Master, previous.Slave); Advance(current, 2);
            };
            ApplyFeedbackOutcome(worker, previous);
            Equal(0, TestWorld.TrainerFeedbackAwards, "old whole ritual duty feedback");
            Equal(2, current.Comp.ritualPhase, "replacement phase after old outcome");
            Equal(current.Lord, current.Comp.bindingRitualLord, "replacement ownership after old outcome");
            Equal(0, TestWorld.RitualLetters, "old whole ritual letter");
        });
        Check("replacement ritual during duty feedback keeps one notification and suppresses stale letter", () =>
        {
            var previous = OfficerRitual(); Advance(previous, 6); var worker = FeedbackOutcome();
            RitualFixture current = null;
            TestWorld.OnTrainerFeedback = () =>
            {
                previous.End(); current = new RitualFixture(previous.Master, previous.Slave); Advance(current, 2);
            };
            ApplyFeedbackOutcome(worker, previous); ApplyFeedbackOutcome(worker, previous);
            Equal(1, TestWorld.TrainerFeedbackAwards, "single entered whole ritual duty feedback");
            Equal(2, current.Comp.ritualPhase, "replacement phase after duty callback");
            Equal(current.Lord, current.Comp.bindingRitualLord, "replacement ownership after duty callback");
            Equal(0, TestWorld.RitualLetters, "stale whole ritual letter after duty callback");
        });
    }
}
