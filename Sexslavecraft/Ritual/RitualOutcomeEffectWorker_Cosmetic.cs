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
            return base.GetOutcome(quality, ritual);
        }

        protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
            RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            Pawn recipient = jobRitual.PawnWithRole(SSCCosmeticUtility.RecipientRole);
            bool applied = SSCCosmeticUtility.ApplyResult(recipient, outcome.Positive,
                outcome.Positive ? SSCCosmeticUtility.RitualSuccess : SSCCosmeticUtility.RitualFailure);
            extraOutcomeDesc = applied ? "SSC_Cosmetic_RitualResult".Translate(recipient.LabelShort) :
                "SSC_Cosmetic_TargetLost".Translate();
        }
    }
}
