// AI 辅助生成：区分禁止提取与删除准备；孕期及精神状态期间保留准备。
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>手术、任务及最终提取共用准入；只有主人身份要求删除准备状态。</summary>
    public static class SSCPersonalityExcretionAdmission
    {
        /// <summary>统一检查能否提取人格。暂时封锁含隐藏妊娠、临产和受方的精神状态。</summary>
        public static bool CanExtract(Pawn pawn) => pawn != null && !MustRemovePreparation(pawn) &&
#if SSC_RW17
            !HasMentalState(pawn) &&
#endif
            !SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(pawn);

        /// <summary>只按主人身份撤销准备。孕期和精神状态结束后，已保留的进度可以继续使用。</summary>
        public static bool MustRemovePreparation(Pawn pawn) => pawn != null && SSCIdentityUtility.IsMaster(pawn);

        /// <summary>为手动工作提供孕期拒绝原因；身份及其他旧前置仍沿原模块处理。</summary>
        public static string PregnancyBlockReason(Pawn pawn) => SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(pawn)
            ? "SSC_Birth_ExcretionBlocked".Translate().ToString() : null;

#if SSC_RW17
        // 原版 Pawn.InMentalState 会读取精神追踪器。角色生成或恢复中可能尚未建立追踪器。
        private static bool HasMentalState(Pawn pawn) => pawn?.mindState?.mentalStateHandler != null && pawn.InMentalState;

        /// <summary>为玩家手动指派工作说明临时阻塞。任务可用性仍由 CanExtract 决定。</summary>
        internal static string TemporaryBlockReason(Pawn pawn) => HasMentalState(pawn)
            ? "SSC_SlaveBreakdown_ExcretionBlocked".Translate().ToString() : PregnancyBlockReason(pawn);
#endif

        /// <summary>快照和放置回调后复查。遇到临时封锁时，销毁本次未提交的凝胶并保留准备。</summary>
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
