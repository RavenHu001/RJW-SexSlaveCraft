// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只对缺成长版本的旧主人补零经验；现代空记录、空壳与旧克隆均不推断成长。</summary>
    public static class SSCMasterProgressionMigration
    {
        public static void MigratePawn(Pawn pawn)
        {
            // 引用尚未恢复时不判断空壳或克隆。角色版本为当前值时直接退出，
            // 即使当前身份是主人，也尊重现代记录明确没有成长的结果。
            if (Scribe.mode != LoadSaveMode.Inactive) return;
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.masterProgressionVersion >= SSCMasterProgressionUtility.CurrentVersion) return;

            // 旧有效主人只补基础阶段，不按绑定数量猜历史经验；已有成长也不重置。
            // 建立失败保留旧版本，让后续显式迁移仍可重试，而不是标记已完成却丢记录。
            if (SSCIdentityUtility.IsMaster(pawn) && !SSCMasterProgressionUtility.IsHollow(pawn) &&
                !RabbitCloneUtility.IsRabbitClone(pawn) && !SSCMasterProgressionUtility.EnsureInitialRecord(pawn)) return;

            // 非主人、空壳和克隆同样完成格式迁移，但不因此获得阶段；此处不清旧克隆身份。
            comp.masterProgressionVersion = SSCMasterProgressionUtility.CurrentVersion;
        }
    }
}
