// AI 辅助生成
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>为 SSC 自定义结果处理器接通原版曲线；结算及三个原版显示入口共用此查询。</summary>
    [HarmonyPatch(typeof(IdeoDevelopmentUtility),
        nameof(IdeoDevelopmentUtility.GetDevelopmentPointsOverOutcomeIndexCurveForRitual))]
    public static class Harmony_RitualDevelopmentPoints
    {
        /// <summary>仅修改 SSC 仪式；关闭时撤掉曲线，开启时为随时举办仪式补齐原版奖励曲线。</summary>
        public static void Postfix(Precept_Ritual ritual, ref SimpleCurve __result)
        {
            // 按实际结果处理器判断，不用显示名称、主持身份或其他仪式的模因推断归属。
            if (!(ritual?.outcomeEffect is RitualOutcomeEffectWorker_SSCBinding worker)) return;

            // 此判断每次查询都读取偏好，使运行中切换同时影响奖励预览、来源列表与实际收益。
            if (!worker.GivesDevelopmentPoints)
            {
                __result = null;
                return;
            }

            // 仅补当前 SSC 随时举办模式；保留其他补丁已提供的非空曲线，不改变日期仪式规则。
            if (ritual.isAnytime && __result == null)
                __result = SSCRitualDevelopmentPointsUtility.GetVanillaAnytimeCurve();
        }
    }

    /// <summary>读取本机版本已核对的原版共享曲线；不复制奖励公式，也不修改共享曲线数据。</summary>
    internal static class SSCRitualDevelopmentPointsUtility
    {
        // 只缓存字段元数据；首次需要时读取值，让原版完成自身静态初始化。
        private static readonly FieldInfo AnytimeCurveField = AccessTools.Field(typeof(IdeoDevelopmentUtility),
            "AnytimeAndFuneralRitualDevelopmentPointsOverOutcomeIndex");
        private static bool missingCurveReported;

        /// <summary>读取原版曲线；版本不匹配时停止新增收益并只报告一次，避免界面查询刷屏。</summary>
        internal static SimpleCurve GetVanillaAnytimeCurve()
        {
            // 返回原版只读使用的对象；所有调用方只 Evaluate/枚举，不能向该共享对象添加点。
            var curve = AnytimeCurveField?.GetValue(null) as SimpleCurve;
            if (curve != null) return curve;

            // 不回退到硬编码数值，防止新游戏版本的规则变化被静默掩盖。
            if (!missingCurveReported)
            {
                missingCurveReported = true;
                Log.Error("[SSC] Vanilla anytime ritual development curve is unavailable; SSC ritual development points are disabled.");
            }
            return null;
        }
    }
}
