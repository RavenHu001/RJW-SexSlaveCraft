// AI 辅助生成：独立调教窗口与健康记录共用的敏感度档位文案。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>档位即时本地化；界面与健康名称使用同一有效档位，不缓存旧语言。</summary>
    internal static class SSCOrgasmSensitivityPresentation
    {
        /// <summary>稳定档位的显示名；关闭与四档都由 Keyed 资源提供。</summary>
        internal static string Level(int level) => ("SSC_Sensitivity_Level" + level).Translate();

        /// <summary>使用已加载参数说明各档效果。</summary>
        internal static string OptionLabel(int level)
        {
            string label = Level(level);
            var config = SSCOrgasmSensitivityConfig.Get(SSCOrgasmSensitivityUtility.Definition);
            if (level > 0 && config?.Valid == true)
                label += "SSC_Sensitivity_OptionParameters".Translate(config.Multipliers[level - 1].ToString("0.###"),
                    config.Offsets[level - 1].ToString("+0.##;-0.##;0"));
            return label;
        }
    }
}
