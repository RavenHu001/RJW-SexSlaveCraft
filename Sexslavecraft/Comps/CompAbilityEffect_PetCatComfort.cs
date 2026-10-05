using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class CompProperties_AbilityPetCatComfort : CompProperties_AbilityEffect
    {
        public HediffDef encouragementHediff;
        public int durationTicks = 30000;

        public CompProperties_AbilityPetCatComfort()
        {
            compClass = typeof(CompAbilityEffect_PetCatComfort);
        }
    }

    /// <summary>单一安抚效果，实际生效时才选择恢复精神状态或刷新激励。</summary>
    public class CompAbilityEffect_PetCatComfort : CompAbilityEffect
    {
        public new CompProperties_AbilityPetCatComfort Props => (CompProperties_AbilityPetCatComfort)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string reason;
            bool allowed = PetCatAbilityUtility.IsEffectConfigured(Props.encouragementHediff, Props.durationTicks);
            if (!allowed) reason = "SSC_PetCatComfortMissingEffect";
            else allowed = PetCatAbilityUtility.CanSelectTarget(parent.pawn, target.Pawn, out reason);
            if (!allowed)
            {
                if (throwMessages)
                    Messages.Message(reason.Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 原版排队与 WarmupTick 都调用此入口，因此这里不能把远处可达目标拒绝。
            // 同格限制由 Verb 开始预热、任务过程及 Ability.Activate 分别复查。
            return Valid(target);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // Apply 不检查原版冷却，因为此时 PreActivate 已经启动它。
            // 恢复分支与激励分支只能完成一个，不附带亲昵经验或后续互动。
            if (PetCatAbilityUtility.Apply(parent.pawn, target.Pawn, Props.encouragementHediff, Props.durationTicks))
                base.Apply(target, dest);
        }
    }
}
