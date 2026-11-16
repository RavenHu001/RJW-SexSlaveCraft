using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Need), nameof(Need.DrawOnGUI))]
    internal static class Harmony_CorruptionNeedBar
    {
        internal static float GetFillFraction(Need need)
            => need is Need_Corruption corruption ? corruption.DisplayFraction : need.CurLevelPercentage;

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var percentage = AccessTools.PropertyGetter(typeof(Need), nameof(Need.CurLevelPercentage));
            var fillBar = AccessTools.Method(typeof(Widgets), nameof(Widgets.FillableBar), new[] { typeof(Rect), typeof(float) });
            var matches = codes.Select((code, index) => index)
                .Where(index => index + 1 < codes.Count && codes[index].Calls(percentage) && codes[index + 1].Calls(fillBar))
                .ToArray();
            if (matches.Length != 1)
            {
                Log.Warning("[SSC] Could not locate the vanilla need bar fill call. Corruption bar uses vanilla fill.");
                return codes;
            }

            // 只替换 FillableBar 的取值，保留原版布局、调试按钮、表头和所有需求的实际百分比。
            var fillValue = codes[matches[0]];
            fillValue.opcode = OpCodes.Call;
            fillValue.operand = AccessTools.Method(typeof(Harmony_CorruptionNeedBar), nameof(GetFillFraction));
            return codes;
        }
    }
}
