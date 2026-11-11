// AI 辅助生成
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private Vector2 behaviorRulesScroll;
        private float behaviorRulesHeight;
        private (object Key, Action Submit) behaviorPress;

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

            // Listing 推进后才能得到完整高度。用上次高度设置滚动条，避免同一事件重复执行控件。
            behaviorRulesScroll.y = Mathf.Clamp(behaviorRulesScroll.y, 0f, Mathf.Max(0f, behaviorRulesHeight - viewport.height));
            Widgets.BeginScrollView(viewport, ref behaviorRulesScroll,
                new Rect(0f, 0f, width, Mathf.Max(behaviorRulesHeight, viewport.height)));
            try
            {
                if (twoColumns)
                {
                    // 两列从同一顶线起排，较高的一侧决定共用滚动区域的高度。
                    float sexHeight = DrawColumn(0f, DrawSex);
                    float romanceHeight = DrawColumn(columnWidth + SettingsColumnGap, DrawRomance);
                    behaviorRulesHeight = Mathf.Max(sexHeight, romanceHeight);
                    Widgets.DrawBoxSolid(new Rect(columnWidth + SettingsColumnGap / 2f, 0f, 1f, behaviorRulesHeight), DividerColor);
                }
                else
                {
                    behaviorRulesHeight = DrawColumn(0f, listing =>
                    {
                        DrawSex(listing);
                        listing.GapLine(SettingsColumnGap);
                        DrawRomance(listing);
                    });
                }
            }
            finally { Widgets.EndScrollView(); }

            // 每列使用独立的局部坐标，确保列表控件和命中区域一起偏移。
            float DrawColumn(float x, Action<Listing_Standard> draw)
            {
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(new Rect(x, 0f, columnWidth, 100000f));
                try { draw(listing); return listing.CurHeight; }
                finally { listing.End(); }
            }

            void DrawSex(Listing_Standard listing)
                => SSCBehaviorControlUI.DrawSexSection(listing, edit.Pawn, showSexSwitches: false, prepare: Prepare);

            void DrawRomance(Listing_Standard listing)
                => SSCBehaviorControlUI.DrawRomanceSection(listing, edit.Pawn, Prepare, context => OpenBehaviorTargets(edit, context));

            Action Prepare(Rect rect, object key, Action submit) => PrepareBehaviorAction(edit, rect, key, submit);
        }

        /// <summary>只在按下时签发控件请求，松手不能从新视图取得角色、配置或绑定。</summary>
        private Action PrepareBehaviorAction(PageEdit edit, Rect rect, object key, Action submit)
        {
            var input = Event.current;
            if (input.button != 0 || !Mouse.IsOver(rect)) return null;
            if (input.type == EventType.mouseDown && PageEditIsCurrent(edit))
            {
                // 即使角色和页面未变，原锁链或主人引用变化也会让本次按压失效。
                var chain = SSCBondUtility.GetChain(edit.Pawn);
                var master = chain?.LinkedPawn;
                behaviorPress = (key, () =>
                {
                    if (PageEditIsCurrent(edit) && SSCBondUtility.GetChain(edit.Pawn) == chain && chain?.LinkedPawn == master)
                        submit();
                });
            }
            var request = behaviorPress.Submit;
            if ((input.rawType != EventType.mouseDown && input.rawType != EventType.mouseUp) ||
                request == null || !Equals(behaviorPress.Key, key)) return null;
            return () =>
            {
                // 原生控件响应前可能已撤销请求。只消费刚才匹配到的同一个回调。
                if (!ReferenceEquals(behaviorPress.Submit, request)) return;
                behaviorPress = default;
                request();
            };
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
