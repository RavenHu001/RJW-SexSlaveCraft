using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    // 全部挂接非泛型入口，且只在本控件同步绘制范围生效。
    [HarmonyPatch(typeof(PawnPortraitIconsDrawer), nameof(PawnPortraitIconsDrawer.DrawPawnPortraitIcons))]
    internal static class Harmony_BindingRitualPortraitBadges
    {
        public static void Postfix(Rect __0, Pawn __1, ref bool __8)
            => BindingRitualSelectionVisuals.Active?.DrawPortrait(__0, __1, ref __8);
    }

    [HarmonyPatch(typeof(PawnRitualRoleSelectionWidget), "ExtraTipContents")]
    internal static class Harmony_BindingRitualPortraitTip
    {
        public static void Postfix(Pawn __0, ref string __result)
        {
            string extra = BindingRitualSelectionVisuals.Active?.ExtraTip(__0);
            if (!string.IsNullOrEmpty(extra)) __result = string.IsNullOrEmpty(__result) ? extra : __result + "\n\n" + extra;
        }
    }

    [HarmonyPatch(typeof(DragAndDropWidget), nameof(DragAndDropWidget.DropArea))]
    internal static class Harmony_BindingRitualRoleSlot
    {
        public static void Postfix(int __0, Rect __1, object __3)
            => BindingRitualSelectionVisuals.Active?.RecordSlot(__0, __1, __3);
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.BeginScrollView))]
    internal static class Harmony_BindingRitualVisualScrollBegin
    {
        public static void Prefix() => BindingRitualSelectionVisuals.Active?.BeginScroll();
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.EndScrollView))]
    internal static class Harmony_BindingRitualVisualScrollEnd
    {
        // EndScrollView 之前仍在原版的滚动坐标和裁剪内，避免 UI 缩放和滚动偏移误差。
        public static void Prefix() => BindingRitualSelectionVisuals.Active?.DrawDropFeedback();
        public static void Postfix() => BindingRitualSelectionVisuals.Active?.EndScroll();
    }
}
