// AI 辅助生成
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>行为限制两板块的协调绘制器；分组只决定展示，不混用两个模块的保存或总开关。</summary>
    public static class SSCBehaviorControlUI
    {
        public const float MinimumSidebarWidth = 260f;
        /// <summary>与 ITab 的主栏/留白公式一致；宽度不足改用同一绘制器的窗口，禁止负宽侧栏。</summary>
        public static bool CanFitSidebar(float screenWidth) => Mathf.Min(740f, screenWidth - 16f) >= 370f + MinimumSidebarWidth;
        /// <summary>显示资格不取旧保护总开关；解绑后保存选择及具体原因仍可查看。</summary>
        public static bool CanInspect(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && (comp.pawnIdentity == PawnIdentity.Slave || comp.romanceConfig != null || comp.restrictionConfig != null ||
                SSCRestrictionResolver.IsApplicable(pawn) || SSCSelfTrainingEligibility.IsEligible(pawn));
        }
        /// <summary>在一个外层滚动区域连续绘制两个独立边框；旧模块提前 return 不会退出浪漫板块。</summary>
        public static void DrawPawn(Listing_Standard listing, Pawn pawn, bool showSexSwitches = false)
        {
            DrawBlock(listing, "SSC_Romance_SexSection".Translate(), inner =>
            {
                if (showSexSwitches) Dialog_SSCRestrictions.DrawGlobalSwitches(inner);
                SSCRestrictionUI.DrawPawn(inner, pawn);
            });
            listing.Gap(14f);
            DrawBlock(listing, "SSC_Romance_RomanceSection".Translate(), inner => SSCRomanceUI.DrawPawn(inner, pawn));
        }
        /// <summary>内容只绘制一次并据实际推进高度设置边框，避免测量时重复调用按钮或初始化提交。</summary>
        private static void DrawBlock(Listing_Standard listing, string title, Action<Listing_Standard> content)
        {
            float top = listing.CurHeight, width = listing.ColumnWidth;
            var inner = new Listing_Standard { maxOneColumn = true };
            float height;
            inner.Begin(new Rect(12f, top + 12f, Mathf.Max(1f, width - 24f), 100000f));
            try { inner.Label(title); inner.GapLine(6f); content(inner); }
            finally { height = inner.CurHeight + 24f; inner.End(); }
            listing.GetRect(height);
            // 只描边，不在绘制完内容后填充背景，保证控件与文字不被覆盖。
            Widgets.DrawBox(new Rect(0f, top, width, height));
        }
    }
    /// <summary>七项浪漫偏好沿独立 GetView/TrySet；不注册为通用能力，不创建隐式默认配置。</summary>
    public static class SSCRomanceUI
    {
        /// <summary>保留子板块标题、七项保存选择及底部命令，去除额外说明；阶段与载体资格仍由真实查询／提交校验。</summary>
        public static void DrawPawn(Listing_Standard listing, Pawn pawn)
        {
            foreach (SSCRomanceSetting setting in Enum.GetValues(typeof(SSCRomanceSetting)))
            {
                if (setting == SSCRomanceSetting.InitiateRomance || setting == SSCRomanceSetting.AcceptRomance || setting == SSCRomanceSetting.InitiateOnlyMaster)
                { listing.GapLine(6f); listing.Label(("SSC_Romance_Group_" + setting).Translate()); }
                DrawSetting(listing, pawn, setting);
            }
            listing.GapLine();
            DrawCommands(listing, pawn);
        }
        /// <summary>底部三个按钮纵向排列，长译文按实际宽度换行；整组使用最初捕获的执行者，点击不重拍人格。</summary>
        private static void DrawCommands(Listing_Standard listing, Pawn pawn)
        {
            listing.Label("SSC_Romance_CommandTitle".Translate());
            string failure = SSCRomanceCommandUtility.ActorFailure(pawn);
            var original = SSCRomanceCommandUtility.Capture(pawn);
            bool oldEnabled = GUI.enabled;
            try
            {
                GUI.enabled = oldEnabled && failure == null;
                foreach (SSCRomanceAction action in Enum.GetValues(typeof(SSCRomanceAction)))
                {
                    var context = original.ForTarget(null, action);
                    string label = ("SSC_Romance_Action_" + action).Translate();
                    Rect button = listing.GetRect(Mathf.Max(32f, Text.CalcHeight(label, Mathf.Max(1f, listing.ColumnWidth - 12f)) + 12f));
                    if (Widgets.ButtonText(button, label) && GUI.enabled) SSCRomanceCommandMenu.OpenTargets(context);
                    listing.Gap(4f);
                }
            }
            finally { GUI.enabled = oldEnabled; }
        }
        /// <summary>同一行快照用于尺寸、绘制和提交；禁止点击时重新 GetView 为旧请求续权。</summary>
        private static void DrawSetting(Listing_Standard listing, Pawn pawn, SSCRomanceSetting setting)
        {
            var view = SSCRomancePreferences.GetView(pawn, setting);
            string label = ("SSC_Romance_Setting_" + setting).Translate();
            float labelWidth = Mathf.Max(1f, listing.ColumnWidth - 34f);
            float labelHeight = Mathf.Max(28f, Text.CalcHeight(label, labelWidth));
            Rect row = listing.GetRect(labelHeight + 8f);
            bool previous = GUI.enabled, value = view.Saved;
            Color color = GUI.color;
            try
            {
                GUI.enabled = previous && view.CanChange;
                if (!GUI.enabled) GUI.color = color * new Color(.6f, .6f, .6f, 1f);
                Widgets.Label(new Rect(row.x, row.y, labelWidth, labelHeight), label);
                Widgets.Checkbox(row.xMax - 24f, row.y, ref value, 24f, !GUI.enabled);
                if (GUI.enabled && Widgets.ButtonInvisible(new Rect(row.x, row.y, labelWidth, labelHeight))) value = !value;
                if (GUI.enabled && value != view.Saved && !SSCRomancePreferences.TrySet(pawn, setting, value, view.Context, out var reason))
                    Messages.Message(SSCRomanceCommandUtility.Explain(reason.ToString()), pawn, MessageTypeDefOf.RejectInput, false);
            }
            finally { GUI.enabled = previous; GUI.color = color; }
        }
    }
}
