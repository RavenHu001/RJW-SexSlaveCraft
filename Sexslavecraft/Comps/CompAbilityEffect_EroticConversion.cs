using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class CompProperties_EroticConversion : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef; // 用于在 XML 中指定读取哪个 Hediff 的阶段
        public CompProperties_EroticConversion()
        {
            this.compClass = typeof(CompAbilityEffect_EroticConversion);
        }
    }

    public class CompAbilityEffect_EroticConversion : CompAbilityEffect
    {
        public new CompProperties_EroticConversion Props => (CompProperties_EroticConversion)this.props;

        // 验证：是否允许对该目标施法
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn targetPawn = target.Pawn;
            Pawn caster = parent.pawn;

            if (targetPawn == null) return false;

            // 拦截：对方信仰已经和施法者相同
            if (targetPawn.Ideo != null && caster.Ideo != null && targetPawn.Ideo == caster.Ideo)
            {
                if (throwMessages)
                {
                    // 屏幕上方弹出纯白色普通提示，不会产生 Debug 红字
                    Messages.Message(Strings.Message_SameIdeologyNoEffect, targetPawn, MessageTypeDefOf.NeutralEvent, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        // 施法：按阶段扣除认可度
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn targetPawn = target.Pawn;
            Pawn caster = parent.pawn;

            if (targetPawn?.Ideo == null || caster?.Ideo == null) return;

            // 获取施法者身上的指定 Hediff (即你的 SSC_Exp_Oral)
            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            float multiplier = 1.0f; // 默认不扣

            if (hediff != null)
            {
                // 根据 XML 中定义的阶段获取打折比例
                switch (hediff.CurStageIndex)
                {
                    case 0: // 开发中 (0 ~ 0.3)
                        multiplier = 7f / 8f;
                        break;
                    case 1: // 熟练 (0.3 ~ 0.7)
                        multiplier = 3f / 4f;
                        break;
                    case 2: // 敏感 (0.7 ~ 1.0)
                        multiplier = 3f / 5f;
                        break;
                    case 3: // 完全恶堕 (1.0)
                    default:
                        multiplier = 1f / 2f;
                        break;
                }
            }

            // 计算与扣除
            float currentCertainty = targetPawn.ideo.Certainty;
            float newCertainty = currentCertainty * multiplier;
            float reductionAmount = currentCertainty - newCertainty;

            targetPawn.ideo.OffsetCertainty(-reductionAmount);

            // 视觉反馈：在目标头上飘字显示扣除了多少
            string text = Strings.Float_CertaintyReduction((1f - multiplier).ToStringPercent());
            MoteMaker.ThrowText(targetPawn.DrawPos, targetPawn.Map, text, UnityEngine.Color.magenta);

            // 彻底转化判定
            if (targetPawn.ideo.Certainty <= 0.01f)
            {
                targetPawn.ideo.SetIdeo(caster.Ideo);
                targetPawn.ideo.OffsetCertainty(0.5f); // 给予 50% 初始信仰
                Messages.Message(Strings.Message_EroticConversionComplete(targetPawn.NameShortColored.ToString()), targetPawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}