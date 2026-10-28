// AI 辅助生成：所有RJW角色共用的原生高潮频率属性，不保存第二份个人倍率。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>读取原版Stat管线的最终系数；身体档位、其他Hediff、基因等均由Stat系统合成。</summary>
    public static class SSCOrgasmFrequencyUtility
    {
        public const string DefName = "SSC_OrgasmFrequency";

        /// <summary>使用当前运行Def，缺定义时不创建或写入默认配置。</summary>
        public static StatDef Definition => DefDatabase<StatDef>.GetNamedSilentFail(DefName);

        /// <summary>默认正常推进；不要求SSC身份／身体记录。使用请求重算，避免读到过期的身体阶段缓存。</summary>
        public static float GetMultiplier(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead || pawn.Destroyed || pawn.Discarded) return 1f;
            var stat = Definition;
            if (stat == null) return 1f;
            // GetValue(StatRequest)执行原版offset／factor／最终处理；不再次乘配置倍率，不依赖TickManager缓存。
            float value = stat.Worker.GetValue(StatRequest.For(pawn));
            return float.IsFinite(value) && value >= 1f ? value : 1f;
        }
    }
}
