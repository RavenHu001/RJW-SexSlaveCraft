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

        public override void ExposeData()
        {
            base.ExposeData();
            // 上轮可用技能的存档保存了原版 Verb_CastAbility。本轮改为自定义 Verb 后，
            // 原版 VerbTracker 会在跨引用阶段替换类型，但不重绑后建 Verb 的 Ability。
            // 仅在读档收尾补 owner，保留 Ability 原实例、剩余预热与已保存冷却。
            if (Scribe.mode == LoadSaveMode.PostLoadInit && def != null && verb is Verb_CastAbility castVerb)
                castVerb.ability = this;
        }

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
            // 目标意识和同格变化必须在进入 base.Activate 前拒绝；选取资格允许远处目标。
            // 被撤销后仍留在任务中的旧 Ability 不能替代当前实际授予的实例完成施放。
            if (!CanCast || pawn.abilities?.GetAbility(def) != this ||
                !PetCatAbilityUtility.CanApply(pawn, target.Pawn, out _) || !CanApplyOn(target)) return false;
            return base.Activate(target, dest);
        }
    }
}
