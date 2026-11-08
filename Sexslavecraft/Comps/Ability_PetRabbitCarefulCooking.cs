using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class Ability_PetRabbitCarefulCooking : Ability
    {
        public Ability_PetRabbitCarefulCooking() { }
        public Ability_PetRabbitCarefulCooking(Pawn pawn) : base(pawn) { }
        public Ability_PetRabbitCarefulCooking(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonistPlayerControlled || !pawn.Awake() ||
                    !ReferenceEquals(pawn.abilities?.GetAbility(def), this) ||
                    SSCPersonalityRuntimeUtility.IsMutating(pawn) ||
                    !PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit))
                    return false;

                return base.CanCast;
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 原版 Activate 会先扣冷却。旧技能引用在换入另一个终极兔人格后也必须拒绝。
            if (target.Pawn != pawn || !CanCast) return false;
            return base.Activate(target, dest);
        }
    }
}
