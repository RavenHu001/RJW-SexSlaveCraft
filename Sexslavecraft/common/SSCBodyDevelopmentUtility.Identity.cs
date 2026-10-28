// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>身体开发的定义分类；人格载荷过滤不依赖宿主是否已经接受开发。</summary>
    public static class SSCBodyDevelopmentUtility
    {
        /// <summary>只识别身体开发与身体微械配置，不扩大成对所有 SSC 健康状态的过滤。</summary>
        public static bool IsBodyStateDef(HediffDef definition)
            => definition?.defName == "SSC_UterusDevelopment" || definition?.defName == "SSC_StomachDevelopment" ||
                definition?.defName == "SSC_OrgasmSensitivity";
    }
}
