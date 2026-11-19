// AI 辅助生成：1.7 两条独立场次收益的配置、额度裁剪与需求写入。
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCCorruptionGainDef : Def
    {
        public int cooldownTicks = -1;
        public double gainPerOrgasmPoints;
        public double maxPerScenePoints;

        internal double Gain { get; private set; }
        internal double SceneLimit { get; private set; }
        private bool Valid => cooldownTicks >= 0 && double.IsFinite(gainPerOrgasmPoints) &&
            double.IsFinite(maxPerScenePoints) && gainPerOrgasmPoints / 100d > 0d &&
            gainPerOrgasmPoints <= maxPerScenePoints && maxPerScenePoints <= 100d &&
            (float)(maxPerScenePoints / 100d) > 0f;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            // 游戏需求用 0～1 的 float。累计上限取原生端点，使从零累计五笔 2 点可到达原生 10%。
            // 每次计算和已发量仍用 double，避免将逐次浮点舍入误差累加到场次账本。
            bool valid = Valid;
            Gain = valid ? gainPerOrgasmPoints / 100d : 0d;
            SceneLimit = valid ? (float)(maxPerScenePoints / 100d) : 0d;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (!Valid)
                yield return "Expected a nonnegative cooldown and finite gain/scene cap with 0 < gain <= scene cap <= 100 percentage points.";
        }
    }

    [DefOf]
    public static class SSCCorruptionGainDefOf
    {
        public static SSCCorruptionGainDef SSC_CorruptionGain_Gel;
        public static SSCCorruptionGainDef SSC_CorruptionGain_Partner;

        static SSCCorruptionGainDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SSCCorruptionGainDefOf));
    }

    internal enum SSCCorruptionGainSource
    {
        Gel,
        Partner
    }

    internal static class SSCCorruptionGainUtility
    {
        private static SSCCorruptionGainDef Profile(SSCCorruptionGainSource source) => source == SSCCorruptionGainSource.Gel
            ? SSCCorruptionGainDefOf.SSC_CorruptionGain_Gel : SSCCorruptionGainDefOf.SSC_CorruptionGain_Partner;

        // 两条适配先查额度和冷却，避免在冷却期间重新捕获参与者或原药效。
        // NaN 表示坏的/中途快照的账本已封闭；配置降低时，正常的已发量仅耗尽余额。
        internal static bool CanApply(Pawn pawn, Need_Corruption need, SSCCorruptionGainSource source, double granted) =>
            need != null && granted >= 0d && granted < (Profile(source)?.SceneLimit ?? 0d) &&
            SSCCulturePartnerCorruptionGameComponent.Current?.CanClaim(pawn, source) == true;

        /// <summary>调用者须已复查原事件和该途径资格。只在一次真实事件上尝试领取。</summary>
        internal static void Apply(Pawn pawn, Need_Corruption need, SSCCorruptionGainSource source, long count, ref double granted)
        {
            var profile = Profile(source);
            var cooldowns = SSCCulturePartnerCorruptionGameComponent.Current;
            if (count <= 0 || !double.IsFinite(granted) || granted < 0d || profile == null || cooldowns == null ||
                need == null || !ReferenceEquals(pawn?.needs?.TryGetNeed<Need_Corruption>(), need)) return;

            // 正冷却只允许同一 tick 领取一笔。关闭冷却后，凝胶可结算外层观察合并到的本人次数，
            // 传道仍由适配层按一次有效观察提交一笔，施加者数量不参与乘算。
            double remaining = profile.SceneLimit - granted;
            float before = need.CurLevel;
            float cap = need.MaxLevel;
            if (!float.IsFinite(before) || before < 0f || !float.IsFinite(cap)) return;
            double gain = Math.Min(profile.Gain * (profile.cooldownTicks > 0 ? 1L : count), Math.Min(remaining, (double)cap - before));
            if (gain <= 0d) return;

            // 使用单次最近舍入保留可达的阶段端点；若舍入会透支场次额度，仅退一格 float。
            float target = Math.Min((float)(before + gain), cap);
            if ((double)target - before > remaining) target = MathF.BitDecrement(target);
            double actual = (double)target - before;
            if (actual <= 0d || !cooldowns.TryClaim(pawn, source, profile.cooldownTicks)) return;

            // SetCorruption 先写值再通知成长效果。写入前锁定本途径冷却和实际额度，
            // 避免同步回调重入。之后因其他效果降低恶堕率也不返还额度。
            granted += actual;
            need.SetCorruption(target);
        }
    }
}
