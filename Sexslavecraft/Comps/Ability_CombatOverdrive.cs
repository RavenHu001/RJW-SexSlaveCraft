using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅补充终极记录与征召校验；效果、冷却和存档沿用原版 Ability。</summary>
    public class Ability_CombatOverdrive : Ability
    {
        public Ability_CombatOverdrive() { }
        public Ability_CombatOverdrive(Pawn pawn) : base(pawn) { }
        public Ability_CombatOverdrive(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed ||
                    !CombatantSpecializationUtility.HasFinalState(pawn)) return false;
                if (!pawn.Drafted) return "SSC_CombatOverdriveRequiresDrafted".Translate();
                return base.CanCast;
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 排队后可能解除征召或抽取人格。必须在原版 PreActivate 扣除冷却前拒绝。
            if (!CanCast) return false;
            return base.Activate(target, dest);
        }
    }
}
