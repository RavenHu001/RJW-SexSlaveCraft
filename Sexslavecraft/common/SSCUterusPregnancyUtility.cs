// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>两类已取证妊娠的局部倍率；不扫描已有妊娠或调整种族及全局身体资源属性。</summary>
    public static class SSCUterusPregnancyUtility
    {
        public const float DurationFactor = 0.8f;
        public const float NativeRateFactor = 1.25f;

        /// <summary>同时限定定义及具体类型，避免机械妊娠等 BasePregnancy 派生类顺带获得收益。</summary>
        public static bool IsSupported(Hediff pregnancy)
        {
            if (pregnancy?.def == null) return false;
            var type = pregnancy.GetType();
            return (type == typeof(Hediff_Pregnant) &&
                    (pregnancy.def.defName == "Pregnant" || pregnancy.def.defName == "PregnantHuman")) ||
                (type == typeof(rjw.Hediff_HumanlikePregnancy) && pregnancy.def.defName == "RJW_pregnancy") ||
                (type == typeof(rjw.Hediff_BestialPregnancy) && pregnancy.def.defName == "RJW_pregnancy_beast");
        }

        /// <summary>读取该次妊娠已有的判定；无组件或不支持的类型保持普通待遇。</summary>
        private static float GetDurationFactor(Hediff pregnancy) => IsSupported(pregnancy)
            ? pregnancy.TryGetComp<HediffComp_SSCUterusPregnancy>()?.DurationFactor ?? 1f : 1f;

        /// <summary>在原版正常速率算出后应用一次固定因子，保留原方法的饥饿与临产逻辑。</summary>
        public static float ScaleNativeRate(float rate, Hediff_Pregnant pregnancy)
            => float.IsFinite(rate) && rate > 0f ? rate / GetDurationFactor(pregnancy) : rate;

        /// <summary>只缩放 RJW 每次重新算出的原始时长，不读取或累乘已有起止时间。</summary>
        public static float ScaleRjwDuration(float duration, rjw.Hediff_BasePregnancy pregnancy)
            => float.IsFinite(duration) && duration > 0f ? duration * GetDurationFactor(pregnancy) : duration;
    }
}
