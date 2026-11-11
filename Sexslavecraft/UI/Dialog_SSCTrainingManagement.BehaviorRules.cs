// AI 辅助生成
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private Vector2 behaviorRulesScroll;
        private (float Sex, float Romance) behaviorPanelHeights;

        /// <summary>两组规则共用滚动区，每组只绘制一次，窄窗顺序排列。</summary>
        private void DrawBehaviorRules(Rect page)
        {
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            if (!SSCBehaviorControlUI.CanInspect(source)) return;
            var edit = new PageEdit(source, comp, Page.BehaviorRules, contextRevision);
            float width = Mathf.Max(1f, viewport.width - 16f);
            bool twoColumns = width >= SettingsTwoColumnWidth;
            float columnWidth = twoColumns ? (width - SettingsColumnGap) / 2f : width;

            // 上次测量用于绘制底色和滚动条，每个板块在同一事件中只绘制一次。
            float height = twoColumns ? Mathf.Max(behaviorPanelHeights.Sex, behaviorPanelHeights.Romance) :
                behaviorPanelHeights.Sex + SettingsColumnGap + behaviorPanelHeights.Romance;
            behaviorRulesScroll.y = Mathf.Clamp(behaviorRulesScroll.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref behaviorRulesScroll,
                new Rect(0f, 0f, width, Mathf.Max(height, viewport.height)));
            try
            {
                float sexHeight = DrawPanel(0f, 0f, behaviorPanelHeights.Sex, DrawSex);
                float romanceX = twoColumns ? columnWidth + SettingsColumnGap : 0f;
                float romanceY = twoColumns ? 0f : sexHeight + SettingsColumnGap;
                float romanceHeight = DrawPanel(romanceX, romanceY, behaviorPanelHeights.Romance, DrawRomance);
                behaviorPanelHeights = (sexHeight, romanceHeight);
            }
            finally { Widgets.EndScrollView(); }

            // 两块各自绘制底色和控件，留出面板间距，点击区域跟随列表坐标。
            float DrawPanel(float x, float y, float measuredHeight, Action<Listing_Standard> draw)
            {
                Widgets.DrawBoxSolid(new Rect(x, y, columnWidth, measuredHeight), new Color(.18f, .21f, .23f));
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(new Rect(x, y, columnWidth, 100000f));
                try { draw(listing); return listing.CurHeight; }
                finally { listing.End(); }
            }

            void DrawSex(Listing_Standard listing)
                => SSCBehaviorControlUI.DrawSexSection(listing, edit.Pawn, showSexSwitches: false, prepare: Prepare);

            void DrawRomance(Listing_Standard listing)
                => SSCBehaviorControlUI.DrawRomanceSection(listing, edit.Pawn, Prepare, context => OpenBehaviorTargets(edit, context));

            Action Prepare(Rect rect, object key, Action submit)
            {
                // 规则按钮还依赖按下时的锁链及绑定主人。
                if (Event.current.type != EventType.mouseDown)
                    return PreparePageAction(edit, rect, key, submit);
                var chain = SSCBondUtility.GetChain(edit.Pawn);
                var master = chain?.LinkedPawn;
                return PreparePageAction(edit, rect, key, () =>
                {
                    if (SSCBondUtility.GetChain(edit.Pawn) == chain && chain?.LinkedPawn == master)
                        submit();
                });
            }
        }

        /// <summary>绑定原页面及实际菜单实例，后端同时验证命令的原链及目标。</summary>
        private void OpenBehaviorTargets(PageEdit edit, SSCRomanceCommandContext context)
        {
            if (!PageEditIsCurrent(edit)) return;
            var menu = SSCRomanceCommandMenu.CreateTargetMenu(context, popup => EditPopupIsCurrent(popup, edit));
            // 候选筛选可能调用外部逻辑，完成后再确认当前窗口仍对应原角色和页面。
            if (menu != null && PageEditIsCurrent(edit)) ShowEditPopup(menu);
        }
    }
}
