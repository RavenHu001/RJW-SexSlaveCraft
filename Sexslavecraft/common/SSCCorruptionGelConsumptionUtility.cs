// AI 辅助生成：凝胶独立消费收益；本人真实事件和额度归任务，药效只提供当前身体准入。
using System;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCCorruptionGelConsumptionUtility
    {
        internal const double GainPerOrgasm = .02d;
        // 上限采用游戏Need的float端点，确保五次能达到原生0.1阈值，账本仍精确保存float差额。
        internal const double SceneLimit = (double).10f;

        /// <summary>先裁剪当前阶段与剩余额度，再通过原需求维护历史／特质；不创建需求或自动进阶。</summary>
        internal static void Apply(Pawn pawn, Hediff_SSCCorruptionGelEffect effect, Need_Corruption need, long count,
            double remaining, Func<bool> isCurrent, Action<double> reserve)
        {
            bool Current() => isCurrent() && ReferenceEquals(pawn?.needs?.TryGetNeed<Need_Corruption>(), need) &&
                ReferenceEquals(SSCCorruptionGelEffectUtility.Find(pawn), effect) && SSCCorruptionGelEffectUtility.IsActive(effect);
            if (need == null || count <= 0 || !double.IsFinite(remaining) || remaining <= 0d || remaining > SceneLimit || !Current()) return;
            float current = need.CurLevel;
            float cap = SSCMod.settings?.useOldScoring == true ? need.MaxLevel :
                Math.Min(need.MaxLevel, TrainingOutcomeUtility.GetChainCap(pawn));
            if (!float.IsFinite(current) || current < 0f || !float.IsFinite(cap)) return;
            double gain = Math.Max(0d, Math.Min(count * GainPerOrgasm, Math.Min(remaining, (double)cap - current)));
            if (gain <= 0d) return;
            double ceiling = current + gain;
            float target = (float)ceiling;
            // 单次使用原生最近舍入，不能逐次向下造成里程碑永远差一格；只有越过总额度才退一格。
            target = Math.Min(target, cap);
            if ((double)target - current > remaining) target = MathF.BitDecrement(target);
            double actual = (double)target - current;
            if (actual <= 0d || !Current() || need.CurLevel != current) return;
            // SetCorruption先写值再同步特质；预先记下精确增量，重入／异常或之后下降不返还额度。
            reserve(actual);
            need.SetCorruption(target);
        }
    }
}
