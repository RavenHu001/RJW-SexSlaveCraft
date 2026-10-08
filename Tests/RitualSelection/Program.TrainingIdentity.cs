using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunTrainingIdentityTests()
    {
        foreach (string status in new[] { "殖民者", "奴隶", "囚犯" })
        foreach (bool rules in new[] { true, false })
            Run($"非 SSC 性奴的直接角色提交和强制开始均拒绝：{status}, rules={rules}", () =>
            {
                var f = new Fixture(); f.Target.SexSlave = false;
                f.Target.IsColonist = status == "殖民者";
                f.Target.IsSlave = status == "奴隶";
                f.Target.IsPrisonerOfColony = status == "囚犯";
                SSCMod.settings.enableSexSlaveProtectionRules = rules;
                f.Open(); Assert(f.HostSlot()); Counters.Reset();
                Assert(!f.A.TryAssign(f.Target, f.TargetRole, out _));
                Assert(f.A.FirstAssignedPawn("slave") == null && Counters.Eligibility == 0 && Counters.Reach == 0);
                Assert(!f.TargetRole.AppliesToPawn(f.Target, out string reason, f.Spot, assignments: f.A));
                Assert(reason == "SSC_Training_TargetIdentityRequired");
                var assignments = new RitualRoleAssignments(f.Ritual, f.Spot);
                assignments.ForcedRolesForReading["master"] = f.Host;
                assignments.ForcedRolesForReading["slave"] = f.Target;
                f.Ritual.behavior.TryExecuteOn(f.Spot, f.Host, f.Ritual, null, assignments, true);
                Assert(f.Ritual.behavior.Executions == 0 && Counters.Eligibility == 0);
                SSCMod.settings.enableSexSlaveProtectionRules = true;
            });
        Run("已选及强制目标撤销 SSC 性奴身份后确认回调不能启动", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth();
            f.A.ForcedRolesForReading["slave"] = f.Target; f.Target.SexSlave = false;
            Counters.Reset();
            Assert(!f.Window.Confirm() && f.Ritual.behavior.Executions == 0 && !f.Window.Closed);
            Assert(Counters.Eligibility == 0 && f.A.FirstAssignedPawn("slave") == f.Target);
        });
        Run("RJW 身体查询回调切出性奴身份后真实选角立即拒绝", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot());
            f.Target.OnEligibility = () => f.Target.SexSlave = false;
            Assert(!f.TargetSlot() && f.A.FirstAssignedPawn("slave") == null);
        });
        Run("一次提交内的成功缓存不能绕过随后发生的身份变化", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot());
            using (BindingRitualSelectionUtility.BeginOperation(f.A))
            {
                Assert(BindingRitualSelectionUtility.ValidateAttempt(f.A, f.TargetRole, f.Target, null, out _));
                f.Target.SexSlave = false;
                Assert(!BindingRitualSelectionUtility.ValidateAttempt(f.A, f.TargetRole, f.Target, null, out string reason));
                Assert(reason == "SSC_Training_TargetIdentityRequired");
            }
        });
    }
}
