// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCRomancePreferences
    {
        /// <summary>破坏宿主前完成预检：未知版本或无法承载非默认选择时拒绝，不清源或丢弃偏好。</summary>
        public static bool CanRestore(Pawn pawn, SSCRomanceConfig snapshot)
            => pawn != null && snapshot?.IsSupported != false && (snapshot == null || snapshot.IsDefault || pawn.TryGetComp<CompSexSlaveTraining>() != null);
        /// <summary>精确恢复独立副本；缺失快照表示模块默认，不保留宿主旧人格选择。</summary>
        public static bool Restore(Pawn pawn, SSCRomanceConfig snapshot)
        {
            if (!CanRestore(pawn, snapshot)) return false;
            // 缺字段来源必须覆盖成默认；不能按 true 合并，否则会混入宿主的旧人格选择。
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp != null) comp.romanceConfig = snapshot?.Copy();
            return true;
        }
        /// <summary>清除当前人格的浪漫选择；调用方负责成功清源时机和撤销旧命令，不触碰其他能力。</summary>
        public static void Clear(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp != null) comp.romanceConfig = null;
        }
    }
}
