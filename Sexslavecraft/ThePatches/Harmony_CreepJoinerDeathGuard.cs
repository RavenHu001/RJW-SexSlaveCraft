// AI 辅助生成
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>兼容旧档中只有 form、缺失 aggressive 的神秘加入者，防止其死亡反应中断原版收尾。</summary>
    [HarmonyPatch(typeof(Pawn_CreepJoinerTracker), nameof(Pawn_CreepJoinerTracker.Notify_CreepJoinerKilled))]
    public static class Harmony_CreepJoinerDeathGuard
    {
        public static bool Prefix(Pawn_CreepJoinerTracker __instance)
        {
            // 完整数据保留原版行为，包括合法的死亡反应；不改写定义、人格或保存字段。
            if (__instance.aggressive != null) return true;

            // 原版在 Disabled 为 false 时直接读取 aggressive.workerType。
            // 缺失定义时没有可执行反应，只跳过此回调，让 Pawn.Kill 继续清除临时死亡标志。
            // 保留诊断警告，避免把坏存档误报为一次正常完成的神秘加入者反应。
            if (!__instance.Disabled)
                Log.Warning($"[SSC] {__instance.Pawn?.LabelShort ?? "Unknown pawn"} 的神秘加入者记录缺少 aggressive；跳过无效死亡反应，继续死亡收尾。");
            return false;
        }
    }
}
