using System.Linq;
using Verse.AI;
using Verse.AI.Group;

namespace ProgressionEducation
{
    public class LordJob_AttendClass : LordJob { }
}

internal static partial class Program
{
    private static void RunEducationCompatibilityTests()
    {
        Check("automatic daily training yields when target enters class during travel and releases occupancy", () =>
        {
            var f = Daily();
            f.toils[0].initAction();
            Require(f.comp.isBeingTrained, "training was not prepared");
            Require(!f.driver.FailConditions.Any(condition => condition()), "ordinary target rejected");
            f.driver.Partner.lord = new Lord { LordJob = new ProgressionEducation.LordJob_AttendClass() };
            Require(f.driver.FailConditions.Any(condition => condition()), "class did not stop pending training");
            f.driver.Finish(JobCondition.Incompletable);
            Require(!f.comp.isBeingTrained, "prepared occupancy leaked");
            Equal(0, TestWorld.DailyCooldowns, "class avoidance must not count as completion");
            Equal(0, TestWorld.DailyOutcomes, "class avoidance must not award training");
        });
        Check("manual daily training and unrelated lords keep their previous behavior", () =>
        {
            var f = Daily();
            f.driver.Partner.lord = new Lord { LordJob = new LordJob() };
            Require(!f.driver.FailConditions.Any(condition => condition()), "unrelated lord rejected");
            f.driver.Partner.lord = new Lord { LordJob = new ProgressionEducation.LordJob_AttendClass() };
            f.driver.job.playerForced = true;
            Require(!f.driver.FailConditions.Any(condition => condition()), "manual command rejected");
            f.driver.job.playerForced = false;
            Require(f.driver.FailConditions.Any(condition => condition()), "automatic command not rejected");
            f.driver.Partner.lord = null;
            Require(!f.driver.FailConditions.Any(condition => condition()), "finished class still rejected");
        });
    }
}
