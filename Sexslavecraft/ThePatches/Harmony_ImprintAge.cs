// AI 辅助生成：地图和世界角色共同经过此年龄推进点。
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在地图与世界角色共用的生理年龄推进入口落实分化态25岁上限。</summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker), "TickBiologicalAge")]
    internal static class Harmony_ImprintAge
    {
        /// <summary>已达上限的分化态跳过原推进；不足25岁沿原版速率成长，无印记者不受影响。</summary>
        internal static bool Prefix(Pawn ___pawn, out bool __state)
        {
            // __state 仅在本次前后缀之间传递：标识分化态不足25岁、仍被允许执行的这一次推进。
            __state = false;
            if (___pawn?.ageTracker == null || SSCImprintUtility.GetImprintLockedIdentity(___pawn) == PawnIdentity.Unset) return true;
            // 日常外部超龄交给每日检查；这里只阻止自然衰老，不反复执行回春。
            __state = ___pawn.ageTracker.AgeBiologicalTicks < SSCImprintUtility.YoungAgeTicks;
            return __state;
        }

        /// <summary>仅截断本次原版推进越过25岁的部分，外部直接改龄仍由即时／每日检查纠正。</summary>
        internal static void Postfix(Pawn ___pawn, bool __state)
        {
            // 跨过 25 岁的那一批原版推进即时截断；其余年龄不进入纠正入口。
            if (__state && ___pawn.ageTracker.AgeBiologicalTicks > SSCImprintUtility.YoungAgeTicks)
                SSCImprintUtility.CorrectAge(___pawn);
        }
    }
}
