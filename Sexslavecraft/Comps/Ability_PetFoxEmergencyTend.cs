using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public class Ability_PetFoxEmergencyTend : Ability
    {
        public Ability_PetFoxEmergencyTend() { }
        public Ability_PetFoxEmergencyTend(Pawn pawn) : base(pawn) { }
        public Ability_PetFoxEmergencyTend(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonistPlayerControlled ||
                    !pawn.Awake() || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                    SSCPersonalityRuntimeUtility.IsMutating(pawn) ||
                    !PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetFox) ||
                    pawn.abilities?.GetAbility(def) != this)
                    return "SSC_PetFoxEmergencyTend_InvalidCaster".Translate();

                return base.CanCast;
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // PreActivate 会先开始冷却。到达前失格、已包扎完毕及旧技能实例都在此拒绝。
            if (!CanCast || !CanApplyOn(target) || !pawn.CanReachImmediate(target, PathEndMode.Touch))
                return false;

            return base.Activate(target, dest);
        }
    }
}
