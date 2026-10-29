// AI 辅助生成：主人身份禁止人格排泄，不迁移或清理绑定关系。
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>统一人格排泄的主人身份限制，供手术、任务和最终提取复查。</summary>
    public static class SSCPersonalityExcretionAdmission
    {
        /// <summary>按当前SSC身份判定，不以印记、永久资格或是否拥有绑定性奴代替主人身份。</summary>
        public static bool CanExtract(Pawn pawn) => pawn != null && !SSCIdentityUtility.IsMaster(pawn);

        /// <summary>主人不保留排泄准备状态；不删除完成／恢复期状态，不接触锁链、缰绳或人格记录。</summary>
        public static void RemoveForbiddenPreparation(Pawn pawn)
        {
            if (CanExtract(pawn) || pawn?.health?.hediffSet == null) return;
            foreach (var record in pawn.health.hediffSet.hediffs.Where(h =>
                h.def == SSCDefOf.SSC_PersonalityExcreting || h.def?.defName == "Hediff_SSC_PersonalityExcreting").ToList())
                pawn.health.RemoveHediff(record);
        }
    }
}
