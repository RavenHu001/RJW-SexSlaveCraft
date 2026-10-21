// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>固定选中的结果索引，仍执行生产 Apply 的准入、一次认领与结算顺序。</summary>
    private sealed class DevelopmentOutcomeWorker : RitualOutcomeEffectWorker_SSCBinding
    {
        private readonly int selected;
        internal DevelopmentOutcomeWorker(int selected) : base(new RitualOutcomeEffectDef
        {
            givesDevelopmentPoints = true,
            startingQuality = .5f,
            outcomeChances = Enumerable.Range(0, 4).Select(_ => new RitualOutcomePossibility()).ToList()
        }) { this.selected = selected; }
        public override RitualOutcomePossibility GetOutcome(float quality, LordJob_Ritual ritual)
            => def.outcomeChances[selected];
    }

    /// <summary>将文化和处理器明确挂在本场仪式上；不借用主持者或目标的文化。</summary>
    private static DevelopmentOutcomeWorker DevelopmentRitual(RitualFixture fixture, int outcome = 3)
    {
        var worker = new DevelopmentOutcomeWorker(outcome);
        fixture.Job.Ritual.outcomeEffect = worker;
        fixture.Job.Ritual.ideo = new OutcomeIdeo { Fluid = true };
        return worker;
    }

    /// <summary>覆盖设置缺省与往返、真实完成认领、开关时机、中断、重入和信件实际增量。</summary>
    private static void RunRitualDevelopmentPointsTests()
    {
        Check("ritual development production settings default off and missing key loads off", () =>
        {
            var settings = new SSCSettings();
            Require(!settings.enableRitualDevelopmentPoints, "new configuration enabled reward");
            settings.enableRitualDevelopmentPoints = true;
            Scribe.mode = LoadSaveMode.LoadingVars;
            settings.ExposeRitualDevelopmentPointsSettings();
            Require(!settings.enableRitualDevelopmentPoints, "old configuration enabled reward");
        });
        foreach (bool enabled in new[] { false, true })
            Check($"ritual development production setting roundtrip enabled={enabled}", () =>
            {
                var original = new SSCSettings { enableRitualDevelopmentPoints = enabled };
                Scribe.mode = LoadSaveMode.Saving; original.ExposeRitualDevelopmentPointsSettings();
                var loaded = new SSCSettings { enableRitualDevelopmentPoints = !enabled };
                Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeRitualDevelopmentPointsSettings();
                Equal(enabled, loaded.enableRitualDevelopmentPoints, "saved preference");
            });

        Check("disabled reward preserves all other whole ritual outcomes and existing points", () =>
        {
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); Advance(f, 6);
            f.Job.Ritual.ideo.development.Points = 4;
            ApplyFeedbackOutcome(worker, f);
            Equal(0, f.Job.Ritual.ideo.development.Calls, "disabled vanilla calls");
            Equal(4, f.Job.Ritual.ideo.development.Points, "existing points");
            Equal(1, TestWorld.RitualOutcomes, "other outcome");
            Equal(1, TestWorld.TrainerProgressAwards, "other progress");
            Equal(1, TestWorld.RitualLetters, "normal letter");
            Require(!TestWorld.RitualLetterTexts.Single().Contains("DevelopmentPointsAwarded"), "disabled reward text");
        });
        for (int index = 0; index < 4; index++)
        {
            int outcome = index;
            Check($"completed ritual forwards actual outcome index={outcome} exactly once", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var f = new RitualFixture(); var worker = DevelopmentRitual(f, outcome); Advance(f, 6);
                // 给原版边界指定奖励，不用宿主公式替代原生专项的真实曲线断言。
                int reward = new[] { 0, 0, 1, 2 }[outcome];
                var dev = f.Job.Ritual.ideo.development; dev.Reward = reward;
                ApplyFeedbackOutcome(worker, f); ApplyFeedbackOutcome(worker, f);
                Equal(1, dev.Calls, "single payout"); Equal(outcome, dev.LastOutcome, "outcome index");
                Equal(reward, dev.Points, "reward boundary"); Equal(1, TestWorld.RitualOutcomes, "single other payout");
            });
        }
        Check("def capability and setting must both allow development reward", () =>
        {
            SSCMod.settings.enableRitualDevelopmentPoints = true;
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); worker.def.givesDevelopmentPoints = false;
            Advance(f, 6); ApplyFeedbackOutcome(worker, f);
            Equal(0, f.Job.Ritual.ideo.development.Calls, "def-disabled payout");
        });
        foreach (bool fluid in new[] { false, true })
            Check($"fixed culture or missing culture cannot enter vanilla payout; missing={fluid}", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var f = new RitualFixture(); var worker = DevelopmentRitual(f); var dev = f.Job.Ritual.ideo.development;
                if (fluid) f.Job.Ritual.ideo = null; else f.Job.Ritual.ideo.Fluid = false;
                Advance(f, 6); ApplyFeedbackOutcome(worker, f); Equal(0, dev.Calls, "inapplicable culture payout");
            });
        Check("vanilla refusal leaves points and development letter unchanged", () =>
        {
            SSCMod.settings.enableRitualDevelopmentPoints = true;
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); var dev = f.Job.Ritual.ideo.development;
            dev.Points = 4; dev.Eligible = false; Advance(f, 6); ApplyFeedbackOutcome(worker, f);
            Equal(4, dev.Points, "refused points"); Equal(1, dev.Calls, "refusal calls");
            Require(!TestWorld.RitualLetterTexts.Single().Contains("DevelopmentPointsAwarded"), "refused reward text");
        });
        Check("letter reports actual capped delta instead of nominal award", () =>
        {
            SSCMod.settings.enableRitualDevelopmentPoints = true;
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); var dev = f.Job.Ritual.ideo.development;
            dev.Points = 9; Advance(f, 6); ApplyFeedbackOutcome(worker, f);
            Equal(10, dev.Points, "cap");
            Require(TestWorld.RitualLetterTexts.Single().Contains("DevelopmentPointsAwarded|9|10|1"), "actual before/after/delta");
        });
        Check("already capped reward reports no development points", () =>
        {
            SSCMod.settings.enableRitualDevelopmentPoints = true;
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); var dev = f.Job.Ritual.ideo.development;
            dev.Points = 10; Advance(f, 6); ApplyFeedbackOutcome(worker, f);
            Equal(10, dev.Points, "unchanged cap");
            Require(TestWorld.RitualLetterTexts.Single().Contains("NoDevelopmentPointsAwarded"), "zero actual award text");
        });
        foreach (bool enabledAtResolution in new[] { false, true })
            Check($"setting is read at resolution enabled={enabledAtResolution}", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = !enabledAtResolution;
                var f = new RitualFixture(); var worker = DevelopmentRitual(f); Advance(f, 6);
                SSCMod.settings.enableRitualDevelopmentPoints = enabledAtResolution; ApplyFeedbackOutcome(worker, f);
                Equal(enabledAtResolution ? 1 : 0, f.Job.Ritual.ideo.development.Calls, "current preference");
                // 已完成场次不会因后来重新开启而补发。
                SSCMod.settings.enableRitualDevelopmentPoints = true; ApplyFeedbackOutcome(worker, f);
                Equal(enabledAtResolution ? 1 : 0, f.Job.Ritual.ideo.development.Calls, "no retroactive payout");
            });
        Check("cancelled partial ritual never pays development points", () =>
        {
            SSCMod.settings.enableRitualDevelopmentPoints = true;
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); Advance(f, 3); f.End();
            ApplyFeedbackOutcome(worker, f); ApplyFeedbackOutcome(worker, f);
            Equal(0, f.Job.Ritual.ideo.development.Calls, "cancelled payout");
        });
        Check("reentrant vanilla reward callback cannot claim ritual again", () =>
        {
            SSCMod.settings.enableRitualDevelopmentPoints = true;
            var f = new RitualFixture(); var worker = DevelopmentRitual(f); Advance(f, 6);
            var dev = f.Job.Ritual.ideo.development; dev.OnGain = () => ApplyFeedbackOutcome(worker, f);
            ApplyFeedbackOutcome(worker, f); Equal(1, dev.Calls, "reentrant payout"); Equal(2, dev.Points, "reentrant points");
        });
        foreach (bool claimed in new[] { false, true })
            Check($"restored completion claim controls development points claimed={claimed}", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var previous = new RitualFixture(); var worker = DevelopmentRitual(previous); Advance(previous, 6);
                if (claimed) ApplyFeedbackOutcome(worker, previous);
                var culture = previous.Job.Ritual.ideo;
                int phase = previous.Comp.ritualPhase; bool savedClaim = previous.Comp.bindingRitualOutcomeClaimed;
                previous.RemoveLordWithoutCleanup();
                // 与现有恢复夹具相同：恢复组件字段及引用，生命周期判断继续调用生产代码。
                var loaded = new RitualFixture(previous.Master, previous.Slave); DevelopmentRitual(loaded);
                loaded.Job.Ritual.ideo = culture; loaded.Comp.bindingRitualLord = loaded.Lord;
                loaded.Comp.ritualPhase = phase; loaded.Comp.bindingRitualOutcomeClaimed = savedClaim;
                BindingRitualStateUtility.RecoverPawnState(loaded.Slave);
                ApplyFeedbackOutcome(worker, loaded); ApplyFeedbackOutcome(worker, loaded);
                Equal(1, culture.development.Calls, "restored payout count"); Equal(2, culture.development.Points, "restored points");
            });
    }
}
