// AI 辅助生成：保留旧载体字段和缺组件默认，不在实际生产路径遍历注册表。
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCAbilityToggleUtility
    {
        // 稳定 ID 只用于注册／查询；它们不是新增的 Scribe 键，也不会产生同名人格偏好副本。
        public const string MilkProduction = "SSC_MilkProduction";
        public const string UterusFertility = "SSC_UterusFertility";
        public const string GelRendering = "SSC_GelRendering";

        /// <summary>从旧训练组件读取泌乳选择；没有组件时仍按旧生产入口放行，不创建组件或人格字典键。</summary>
        /// <remarks>是否存在泌乳状态、营养和容量条件由实际消费者负责；此入口只读取原始偏好。</remarks>
        public static bool GetMilkProductionEnabled(Pawn pawn)
            => GetMilkProductionEnabled(pawn?.TryGetComp<CompSexSlaveTraining>());

        /// <summary>复用调用方已有组件，避免产奶／特化的高频查询重复查找；缺组件默认开启。</summary>
        /// <remarks>不走注册表或人格资格检查，保持原字段的默认值及旧生产路径成本。</remarks>
        public static bool GetMilkProductionEnabled(CompSexSlaveTraining comp)
            => comp?.milkProductionEnabled ?? true;
    }
}
