// AI 辅助生成
using System;

namespace SexSlaveCraft
{
    /// <summary>PNA 身体负荷的纯数学；持久值仍是原始严重度 S，L=S/2 只用于计算和显示。</summary>
    public static class SSCPnaMath
    {
        public const float MaximumSeverity = 2f;
        public const double HighLoadThreshold = 0.97;
        public const float RemovalSeverity = 0.01f;
        public const int TicksPerDay = 60000;
        public const int UpdateInterval = 150;
        public const float OrdinaryExposureDose = 0.08f;
        // 两种性交来源共用归一化负荷；调用方先择强，同一方向只进入一次共享输入。
        public const float MasterInfestationDose = 0.08f;
        public const float ProducerSexDose = 0.02f;
        // 真实输入后已处于末期时对已有成瘾的额外增量；信息展示也读取同一数值。
        public const float TerminalAddictionIncrease = 0.2f;

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>异常浮点数不被推断为满值；旧档合法原始值不翻倍或减半。</summary>
        public static float NormalizeSeverity(float severity)
            => IsFinite(severity) ? Math.Max(0f, Math.Min(MaximumSeverity, severity)) : 0f;

        public static float GetLoad(float severity) => NormalizeSeverity(severity) / MaximumSeverity;

        /// <summary>按实际经过的游戏天分段积分；等于末期门槛时进入慢性侧。</summary>
        public static float AdvanceSeverity(float severity, double days)
        {
            double load = GetLoad(severity);
            if (double.IsNaN(days) || double.IsInfinity(days) || days <= 0) return (float)(load * MaximumSeverity);
            if (load > HighLoadThreshold)
            {
                double highDays = (load - HighLoadThreshold) / 0.25;
                if (days <= highDays) return (float)((load - 0.25 * days) * MaximumSeverity);
                days -= highDays;
                load = HighLoadThreshold;
            }
            load = Math.Max(0, (load + 0.15) * Math.Exp(-0.20 * days) - 0.15);
            float result = (float)(load * MaximumSeverity);
            return result <= RemovalSeverity ? 0f : result;
        }

        /// <summary>预测只计算，不推进组件、添加记录或触发满值事件。</summary>
        public static float AddDose(float severity, float normalizedDose)
        {
            float current = NormalizeSeverity(severity);
            if (!IsFinite(normalizedDose) || normalizedDose <= 0f) return current;
            return (float)Math.Min(MaximumSeverity, current + (double)normalizedDose * MaximumSeverity);
        }
    }
}
