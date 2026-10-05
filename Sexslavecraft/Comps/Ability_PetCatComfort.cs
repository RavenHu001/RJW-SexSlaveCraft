using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>猫终极安抚的施放边界；共用原版技能的冷却、任务和存档。</summary>
    public class Ability_PetCatComfort : Ability
    {
        public Ability_PetCatComfort() { }
        public Ability_PetCatComfort(Pawn pawn) : base(pawn) { }
        public Ability_PetCatComfort(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                if (!PetCatAbilityUtility.CanUse(pawn, out string reason)) return reason.Translate();
                // 征召与未征召共用技能；冷却是否结束仍由原版 Ability 决定。
                return base.CanCast;
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 原版 PreActivate 先扣冷却再执行效果。排队期间的终极资格、
            // 目标、距离和视线变化必须在进入 base.Activate 前拒绝。
            if (!CanCast || !CanApplyOn(target)) return false;
            return base.Activate(target, dest);
        }
    }
}
