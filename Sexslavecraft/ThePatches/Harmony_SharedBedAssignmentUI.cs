using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只在床位分配的姓名区域绘制标记，不修改全局 Pawn 名称或其他建筑的界面。</summary>
    [HarmonyPatch]
    public static class Harmony_SSC_SharedBedAssignmentUI
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Dialog_AssignBuildingOwner), "DrawAssignedRow");
            yield return AccessTools.Method(typeof(Dialog_AssignBuildingOwner), "DrawUnassignedRow");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.LabelEllipses), new[] { typeof(Rect), typeof(string) });
            MethodInfo replacement = AccessTools.Method(typeof(Harmony_SSC_SharedBedAssignmentUI), nameof(DrawPawnLabel));
            FieldInfo assignable = AccessTools.Field(typeof(Dialog_AssignBuildingOwner), "assignable");
            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(label))
                {
                    yield return instruction;
                    continue;
                }
                yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, assignable);
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
        }

        public static void DrawPawnLabel(Rect rect, string label, Pawn pawn, CompAssignableToPawn assignable)
        {
            if (!(assignable is CompAssignableToPawn_Bed) || !SSCIdentityUtility.IsSexSlave(pawn))
            {
                Widgets.LabelEllipses(rect, label);
                return;
            }

            string badge = "SSC_SharedBedBadge".Translate();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Rect badgeRect;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                float width = Mathf.Min(Text.CalcSize(badge).x + 8f, rect.width * 0.45f);
                badgeRect = new Rect(rect.x, rect.y + (rect.height - 22f) * 0.5f, width, 22f);
                Widgets.DrawBoxSolid(badgeRect, new Color(0.65f, 0.3f, 0.6f, 0.25f));
                GUI.color = new Color(1f, 0.65f, 0.83f, oldColor.a);
                Widgets.Label(badgeRect, badge);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }

            TooltipHandler.TipRegion(badgeRect, GetTooltip(pawn));
            rect.xMin = badgeRect.xMax + 5f;
            Widgets.LabelEllipses(rect, label);
        }

        public static string GetTooltip(Pawn pawn)
        {
            string target = "SSC_SharedBedNoPartner".Translate();
            if (SSCSharedBedUtility.TryGetPartner(pawn, out Pawn partner, out bool isMaster))
                target = (isMaster ? "SSC_SharedBedMaster" : "SSC_SharedBedTrainer").Translate(partner.LabelShortCap);
            string threshold = SSCSharedBedUtility.HasMeaningfulCorruption(pawn)
                ? "SSC_SharedBedThresholdMet".Translate().ToString()
                : "SSC_SharedBedThresholdUnmet".Translate().ToString();
            return "SSC_SharedBedTooltip".Translate(target, threshold);
        }
    }
}
