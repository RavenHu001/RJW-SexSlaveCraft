// AI 辅助生成：区分禁止提取与删除准备；孕期等待保留身体状态，不处理绑定。
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>手术、任务及最终提取共用准入；只有主人身份要求删除准备状态。</summary>
    public static class SSCPersonalityExcretionAdmission
    {
        /// <summary>不以印记或绑定代替主人身份；怀孕含隐藏妊娠及临产，结束后自然恢复手动资格。</summary>
        public static bool CanExtract(Pawn pawn) => pawn != null && !MustRemovePreparation(pawn) &&
            !SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(pawn);

        /// <summary>主人删除准备；孕期只等待，不能以通用CanExtract反推ShouldRemove。</summary>
        public static bool MustRemovePreparation(Pawn pawn) => pawn != null && SSCIdentityUtility.IsMaster(pawn);

        /// <summary>为手动工作提供孕期拒绝原因；身份及其他旧前置仍沿原模块处理。</summary>
        public static string PregnancyBlockReason(Pawn pawn) => SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(pawn)
            ? "SSC_Birth_ExcretionBlocked".Translate().ToString() : null;

        /// <summary>快照／放置回调后复查，阻止后续清源；只销毁本次新建的未提交凝胶，孕期准备保留。</summary>
        internal static bool TryContinueExtraction(Pawn pawn, Thing preparedProduct)
        {
            if (CanExtract(pawn)) return true;
            // 源人格尚未剥离，撤回本次产物，避免拒绝后仍留下可植入的重复人格快照。
            if (preparedProduct != null && !preparedProduct.Destroyed) preparedProduct.Destroy(DestroyMode.Vanish);
            RemoveForbiddenPreparation(pawn);
            return false;
        }

        /// <summary>主人不保留排泄准备状态；不删除完成／恢复期状态，不接触锁链、缰绳或人格记录。</summary>
        public static void RemoveForbiddenPreparation(Pawn pawn)
        {
            if (!MustRemovePreparation(pawn) || pawn?.health?.hediffSet == null) return;
            foreach (var record in pawn.health.hediffSet.hediffs.Where(h =>
                h.def == SSCDefOf.SSC_PersonalityExcreting || h.def?.defName == "Hediff_SSC_PersonalityExcreting").ToList())
                pawn.health.RemoveHediff(record);
        }
    }
}
