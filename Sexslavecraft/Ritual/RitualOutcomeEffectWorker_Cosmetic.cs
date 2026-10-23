// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class RitualOutcomeEffectWorker_Cosmetic : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Cosmetic() { }
        public RitualOutcomeEffectWorker_Cosmetic(RitualOutcomeEffectDef def) : base(def) { }

        public override RitualOutcomePossibility GetOutcome(float quality, LordJob_Ritual ritual)
        {
            Pawn recipient = ritual.PawnWithRole(SSCCosmeticUtility.RecipientRole);
            // 正常结果失去业务资格直接选失败项，不抽随机数；死亡保护在共用效果入口。
            if (!SSCCosmeticUtility.RitualRecipientReport(recipient, ritual.Ritual, ritual.Map).Accepted)
                return def.outcomeChances.First(o => !o.Positive);
            // 资格仍有效才使用原版质量修正与一次加权抽取；不另建成功率或重抽结果。
            return base.GetOutcome(quality, ritual);
        }

        protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
            RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            // 消费已经在前置阶段完成；直接使用同一次结果选择升档与反应严重度，不再次扣料。
            Pawn recipient = jobRitual.PawnWithRole(SSCCosmeticUtility.RecipientRole);
            bool applied = SSCCosmeticUtility.ApplyResult(recipient, outcome.Positive,
                outcome.Positive ? SSCCosmeticUtility.RitualSuccess : SSCCosmeticUtility.RitualFailure);
            // 死亡或销毁对象由共用入口跳过身体效果，结果信仍交给原版正常生成。
            extraOutcomeDesc = applied ? "SSC_Cosmetic_RitualResult".Translate(recipient.LabelShort) :
                "SSC_Cosmetic_TargetLost".Translate();
        }
    }
}
