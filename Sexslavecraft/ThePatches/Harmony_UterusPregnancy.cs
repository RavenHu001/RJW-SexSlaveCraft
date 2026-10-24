// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅修改原版单次妊娠的正常进度速率，不改写整段 Tick 或全局资源增长。</summary>
    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.TickInterval))]
    public static class Harmony_UterusNativePregnancy
    {
        /// <summary>匹配唯一的孕期速率计算；构建变化导致匹配失败时完整保留原方法并报告。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var growth = AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.BodyResourceGrowthSpeed));
            var pawn = AccessTools.Field(typeof(Hediff), nameof(Hediff.pawn));
            var race = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.RaceProps));
            var days = AccessTools.Field(typeof(RaceProperties), nameof(RaceProperties.gestationPeriodDays));
            var getProgress = AccessTools.PropertyGetter(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.GestationProgress));
            var setProgress = AccessTools.PropertySetter(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.GestationProgress));
            var matches = new List<int>();
            // 以真实调用、孕期分母及随后的进度写入为锚，不绑定 IL 偏移或局部变量编号。
            for (int i = 0; i + 17 < codes.Count; i++)
                if (codes[i].Calls(growth) && codes[i + 1].opcode == OpCodes.Ldarg_0 &&
                    codes[i + 2].LoadsField(pawn) && codes[i + 3].Calls(race) && codes[i + 4].LoadsField(days) &&
                    codes[i + 5].opcode == OpCodes.Ldc_R4 && Equals(codes[i + 5].operand, 60000f) &&
                    codes[i + 6].opcode == OpCodes.Mul && codes[i + 7].opcode == OpCodes.Div && codes[i + 8].IsStloc() &&
                    codes[i + 11].Calls(getProgress) && codes[i + 12].IsLdloc() &&
                    codes[i + 15].opcode == OpCodes.Mul && codes[i + 16].opcode == OpCodes.Add && codes[i + 17].Calls(setProgress))
                    matches.Add(i + 8);
            if (matches.Count != 1)
            {
                Log.Error("[SSC] 子宫开发孕期速率接入失败：预期唯一计算位置，实际为 " + matches.Count + "；保留原版孕期。");
                return codes;
            }
            int at = matches[0];
            // 栈顶为正常速率，再送入本次妊娠；原存储位置上的分支/块边界前移以包含新增计算。
            var load = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[at]).MoveBlocksFrom(codes[at]);
            codes.InsertRange(at, new[] { load, new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(SSCUterusPregnancyUtility), nameof(SSCUterusPregnancyUtility.ScaleNativeRate))) });
            return codes;
        }
    }

    /// <summary>保留 RJW 原始时长计算及 Initialize 写时间，只处理本次返回的结果。</summary>
    [HarmonyPatch(typeof(rjw.Hediff_BasePregnancy), "GetGestationPeriod")]
    public static class Harmony_UterusRjwPregnancy
    {
        /// <summary>读取妊娠已有待遇，每次对新的原始结果应用一次 0.8，重复初始化不累乘。</summary>
        public static void Postfix(rjw.Hediff_BasePregnancy __instance, ref float __result)
            => __result = SSCUterusPregnancyUtility.ScaleRjwDuration(__result, __instance);
    }
}
