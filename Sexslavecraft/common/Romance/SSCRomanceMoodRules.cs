// AI 辅助生成
using System;

namespace SexSlaveCraft
{
    /// <summary>互动前直接关系，不使用 Ex 关系或当前好感推断。</summary>
    public enum SSCRomanceOldRelation { None, Lover, Fiance, Spouse }

    /// <summary>尚待第四批切换的持续思想公式；不再承担任何事件心情算法。</summary>
    public static class SSCRomanceMoodRules
    {
        /// <summary>缺需求是中性修正，不能当作零恶堕；25% 与 75% 使用精确包含边界。</summary>
        public static int CorruptionModifier(float? current) => !current.HasValue ? 0 : current.Value < .25f ? -1 : current.Value >= .75f ? 1 : 0;
        /// <summary>只有有效限制才计算；普通／受虐狂互斥，恶堕只加入一次。</summary>
        public static int Continuous(int stage, bool active, bool passive, bool masochist, float? corruption)
        {
            active &= stage >= 2; passive &= stage >= 3;
            if (!active && !passive) return 0;
            int value = stage >= 4 ? (active && passive ? 2 : active ? 1 : 0) :
                stage == 3 ? (active && passive ? -3 : active ? -1 : -2) : -3;
            return Math.Clamp(value + (masochist ? 2 : 0) + CorruptionModifier(corruption), -4, 4);
        }

    }
}
