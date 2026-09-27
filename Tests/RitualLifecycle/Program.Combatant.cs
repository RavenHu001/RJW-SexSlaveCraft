using System;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static (JobDriver_Training driver, Toil[] toils, CompSexSlaveTraining comp) CompletedDaily()
    {
        var f = Daily();
        f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
        f.driver.ticks_left = 1; f.toils[3].tickAction(); f.toils[3].Finish();
        return f;
    }

    private static void CheckCombatantDailySettlement()
    {
        Check("daily outcome, body experience and combatant experience share one score", () =>
        {
            var f = CompletedDaily(); f.toils[4].initAction(); f.toils[4].initAction();
            Equal(1, TestWorld.ScoreCalls, "score samples");
            Equal(40f, TestWorld.OutcomeScore, "formal outcome score");
            Equal(40f, TestWorld.BodyScore, "body experience score");
            Equal(40f, TestWorld.CombatantScore, "combatant score");
            Equal(1, TestWorld.ProcessSexCalls, "native processing");
            Equal(1, TestWorld.DailyOutcomes, "outcome");
            Equal(1, TestWorld.DailyCooldowns, "cooldown");
            Equal(1, TestWorld.TrainerProgressAwards, "trainer award");
            Equal(1, TestWorld.CombatantProgressAwards, "combatant award");
        });
        Check("reentry during native processing or formal outcome does not repeat settlement", () =>
        {
            var f = CompletedDaily();
            TestWorld.OnProcessSex = () => f.toils[4].initAction();
            TestWorld.OnDailyOutcome = () => f.toils[4].initAction();
            f.toils[4].initAction();
            Equal(1, TestWorld.ProcessSexCalls, "native reentry");
            Equal(1, TestWorld.ScoreCalls, "score reentry");
            Equal(1, TestWorld.DailyOutcomes, "outcome reentry");
            Equal(1, TestWorld.CombatantProgressAwards, "combatant reentry");
        });
        Check("failed formal outcome grants no combatant experience or replay of partial payouts", () =>
        {
            var f = CompletedDaily();
            TestWorld.OnDailyOutcome = () => throw new InvalidOperationException("outcome");
            bool thrown = false;
            try { f.toils[4].initAction(); } catch (InvalidOperationException) { thrown = true; }
            Require(thrown, "outcome error was swallowed");
            TestWorld.OnDailyOutcome = null; f.toils[4].initAction();
            Equal(1, TestWorld.ProcessSexCalls, "partial payout replay");
            Equal(0, TestWorld.CombatantProgressAwards, "failed outcome award");
        });
        Check("saved completed job cannot claim combatant experience after reconstruction", () =>
        {
            var f = CompletedDaily(); f.toils[4].initAction();
            Scribe.mode = LoadSaveMode.Saving; f.driver.ExposeData();
            Require((bool)Scribe_Values.Saved["sscTrainerProgressAwarded"], "old save key missing");
            var loaded = CompletedDaily();
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive; loaded.toils[4].initAction();
            Equal(1, TestWorld.ProcessSexCalls, "loaded native processing");
            Equal(1, TestWorld.CombatantProgressAwards, "loaded repeated award");
        });
        Check("saved unfinished job can finish and claim once", () =>
        {
            var f = CompletedDaily();
            Scribe.mode = LoadSaveMode.Saving; f.driver.ExposeData();
            var loaded = CompletedDaily();
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.driver.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive;
            loaded.toils[4].initAction(); loaded.toils[4].initAction();
            Equal(1, TestWorld.CombatantProgressAwards, "loaded unfinished award");
        });
        Check("interrupted scene and missing scene data grant no combatant experience", () =>
        {
            var f = Daily(); f.toils[0].initAction(); f.toils[2].initAction(); f.toils[3].initAction();
            f.driver.ticks_left = 50; f.toils[3].Finish(); f.toils[4].initAction();
            Equal(0, TestWorld.CombatantProgressAwards, "interrupted award");
            f = CompletedDaily(); f.driver.Sexprops = null; f.toils[4].initAction();
            Equal(0, TestWorld.CombatantProgressAwards, "missing scene award");
        });
        Check("separate daily jobs for the same trainer each grant combatant experience", () =>
        {
            for (int i = 0; i < 2; i++) { var f = CompletedDaily(); f.toils[4].initAction(); }
            Equal(2, TestWorld.CombatantProgressAwards, "separate events");
        });
        Check("binding ritual completion does not use the combatant daily award event", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            execution.StartScene(); execution.ReachPhaseEnd(); execution.FinishCurrent(JobCondition.Succeeded);
            Equal(0, TestWorld.CombatantProgressAwards, "ritual award");
        });
    }
}
