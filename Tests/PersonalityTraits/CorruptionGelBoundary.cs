// AI 辅助生成：旧人格载荷模型的Def分类边界；凝胶机制及真实流转由原生分区验证。
#if SSC_RW17
namespace SexSlaveCraft
{
    /// <summary>宿主未链接凝胶生产组件，只提供身体记录的稳定Def分类，不模拟资格、库存或生产。</summary>
    public static class SSCCorruptionGelProductionUtility
    {
        public static bool IsBodyStateDef(Verse.HediffDef def) => def?.defName == "SSC_Hediff_CorruptionGelProduction";
    }

    /// <summary>宿主未加载凝胶受药状态机，只提供药效分类；实际过滤与身体药效保存由原生凝胶专项验证。</summary>
    public static class SSCCorruptionGelEffectUtility
    {
        public static bool IsEffectDef(Verse.HediffDef def) => def?.defName == "SSC_Hediff_CorruptionGelEffect";
    }
}
#endif
