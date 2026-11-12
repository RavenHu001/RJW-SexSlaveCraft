// AI 辅助生成
using System;
using UnityEngine;
using Verse;
using static SexSlaveCraft.SSCTrainingWindowTheme;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private Vector2 behaviorRulesScroll;
        private (float Sex, float Romance) behaviorPanelHeights;
        // 新窗口从两块都收起开始。同一窗口换角色时保留玩家选择的展开状态。
        private bool sexRulesExpanded;
        private bool romanceRulesExpanded;

        /// <summary>两个独立折叠板块上下排列，共用一条按实际内容测高的滚动区。</summary>
        private void DrawBehaviorRules(Rect page)
        {
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            if (!SSCBehaviorControlUI.CanInspect(source)) return;
            var edit = new PageEdit(source, comp, Page.BehaviorRules, contextRevision);
            float width = Mathf.Max(1f, viewport.width - 16f);
            // 滚动视口在绘制前需要内容高度。复用上一事件的测量，避免为测高重复绘制并响应控件。
            float height = behaviorPanelHeights.Sex + SettingsColumnGap + behaviorPanelHeights.Romance;
            behaviorRulesScroll.y = Mathf.Clamp(behaviorRulesScroll.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref behaviorRulesScroll,
                new Rect(0f, 0f, width, Mathf.Max(height, viewport.height)));
            try
            {
                float sexHeight = DrawPanel(0f, behaviorPanelHeights.Sex, DrawSex);
                float romanceHeight = DrawPanel(sexHeight + SettingsColumnGap, behaviorPanelHeights.Romance, DrawRomance);
                behaviorPanelHeights = (sexHeight, romanceHeight);
            }
            finally { Widgets.EndScrollView(); }

            // 底色使用缓存高度，第二块从本次测得的第一块末尾开始，折叠后两块可立即重新靠拢。
            float DrawPanel(float y, float measuredHeight, Action<Listing_Standard> draw)
            {
                using var scope = new SSCBondOverviewWidgets.GuiScope(false);
                Widgets.DrawBoxSolid(new Rect(0f, y, width, measuredHeight), SettingsPanelColor);
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(new Rect(0f, y, width, 100000f));
                try { draw(listing); return listing.CurHeight; }
                finally { listing.End(); }
            }

            void DrawSex(Listing_Standard listing)
                => SSCBehaviorControlUI.DrawSexSection(listing, edit.Pawn, showSexSwitches: false, prepare: Prepare,
                    expanded: sexRulesExpanded, toggle: () => ToggleBehaviorPanel(ref sexRulesExpanded));

            void DrawRomance(Listing_Standard listing)
                => SSCBehaviorControlUI.DrawRomanceSection(listing, edit.Pawn, Prepare, context => OpenBehaviorTargets(edit, context),
                    expanded: romanceRulesExpanded, toggle: () => ToggleBehaviorPanel(ref romanceRulesExpanded));

            // 所有嵌入控件共用页面凭据，按压时额外记录本次绑定。
            Action Prepare(Rect rect, object key, Action submit)
            {
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

        /// <summary>折叠改变命中位置，撤销原布局上的待提交按压和弹窗。</summary>
        private void ToggleBehaviorPanel(ref bool expanded)
        {
            expanded = !expanded;
            // 两块都收起时回到标题区，避免留在原内容的滚动偏移。
            if (!sexRulesExpanded && !romanceRulesExpanded) behaviorRulesScroll = Vector2.zero;
            InvalidateWindowContext();
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
