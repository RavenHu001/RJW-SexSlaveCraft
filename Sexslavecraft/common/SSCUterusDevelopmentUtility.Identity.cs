// AI 辅助生成：与身体是否存在该状态无关的定义身份，供人格标签边界共用。
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCUterusDevelopmentUtility
    {
        public const string DefName = "SSC_UterusDevelopment";

        /// <summary>按稳定定义名识别身体记录；过滤旧／外部人格标签时不依赖当前身体或 DefOf 初始化。</summary>
        public static bool IsBodyStateDef(HediffDef definition) => definition?.defName == DefName;
    }
}
