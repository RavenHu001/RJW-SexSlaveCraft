// AI 辅助生成。使用真实仪式许可与名单，只隔离殖民地和地图生成边界。
using RimWorld;
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void TrainerRitualSelectionChecks()
    {
        Check("binding ritual with an empty host slot skips the incapacitated first trainer", () =>
        {
            // Spawned 与 Map 已由本宿主提供；本例的三人均属于同一殖民地。
            var colonist = AccessTools.PropertyGetter(typeof(Pawn), nameof(Verse.Pawn.IsColonist));
            environment.Patch(colonist, prefix: new HarmonyMethod(typeof(Program), nameof(True)));
            try
            {
                Pawn target = Pawn(PawnIdentity.Slave);
                Pawn first = Pawn(PawnIdentity.Master);
                Pawn second = Pawn(PawnIdentity.Master);
                var training = target.TryGetComp<CompSexSlaveTraining>();
                training.ReplaceAssignedTrainers(new[] { first, second });
                Set(first.health, "healthState", PawnHealthState.Down);
                var ritualTarget = new TargetInfo(IntVec3.Zero, map);

                Require(BindingRitualSelectionUtility.HasEligibleHost(target, ritualTarget, out _));
                Require(!SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(first, target, true), false).Allowed);
                Require(SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(second, target, true), false).Allowed);

                var roles = Bare<RitualRoleAssignments>();
                Set(roles, "forcedRoles", new Dictionary<string, Pawn> { ["master"] = first });
                var hostRole = new RitualRole_BindingMaster { id = "master" };
                Require(!BindingRitualSelectionUtility.HasEligibleHost(target, ritualTarget, out _, roles, hostRole, true));

                training.RemoveAssignedTrainer(second);
                Require(!BindingRitualSelectionUtility.HasEligibleHost(target, ritualTarget, out _, skipReason: true));
            }
            finally { environment.Unpatch(colonist, HarmonyPatchType.Prefix, environment.Id); }
        });
    }
}
