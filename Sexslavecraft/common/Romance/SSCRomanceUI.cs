// AI 辅助生成
using System;
using RimWorld;
using UnityEngine;
using Verse;
using static SexSlaveCraft.SSCTrainingWindowTheme;

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
            DrawSexSection(listing, pawn, showSexSwitches);
            listing.Gap(14f);
            DrawRomanceSection(listing, pawn);
        }

        /// <summary>绘制性行为板块。调教窗口传入折叠状态并接管按压提交。</summary>
        internal static void DrawSexSection(Listing_Standard listing, Pawn pawn, bool showSexSwitches,
            Func<Rect, object, Action, Action> prepare = null, bool expanded = true, Action toggle = null)
        {
            // 恢复默认使用本组原始快照，写入和刷新仍交给各自的业务模块。
            var sexReset = SSCRestrictionEditor.GetResetView(pawn);
            string fullTitle = "SSC_Romance_SexSection".Translate();
            bool embedded = prepare != null;
            string title = embedded ? "SSC_Romance_SexSectionShort".Translate().ToString() : fullTitle;
            DrawBlock(listing, title, inner =>
            {
                if (showSexSwitches) Dialog_SSCRestrictions.DrawGlobalSwitches(inner);
                SSCRestrictionUI.DrawPawn(inner, pawn, true, prepare);
            }, rect => DrawReset(rect, sexReset.CanReset, "SSC_BehaviorReset_SexTip", sexReset.Reason, () =>
            {
                if (!SSCRestrictionEditor.TryReset(pawn, sexReset.Context, out var reason)) ResetFailed(pawn, reason);
            }, "SexReset", prepare), embedded, embedded ? fullTitle : null,
                expanded, toggle == null ? null : rect => DrawDisclosure(rect, expanded, "ToggleSexRules", toggle, prepare));
        }

        /// <summary>绘制浪漫板块。调教窗口管理折叠状态、目标菜单和按压凭据。</summary>
        internal static void DrawRomanceSection(Listing_Standard listing, Pawn pawn,
            Func<Rect, object, Action, Action> prepare = null, Action<SSCRomanceCommandContext> openTargets = null,
            bool expanded = true, Action toggle = null)
        {
            var romanceReset = SSCRomancePreferences.GetResetView(pawn);
            DrawBlock(listing, "SSC_Romance_RomanceSection".Translate(), inner => SSCRomanceUI.DrawPawn(inner, pawn, prepare, openTargets),
                rect => DrawReset(rect, romanceReset.CanReset, "SSC_BehaviorReset_RomanceTip", romanceReset.Reason, () =>
                {
                    if (!SSCRomancePreferences.TryReset(pawn, romanceReset.Context, out var reason)) ResetFailed(pawn, reason);
                }, "RomanceReset", prepare), prepare != null, null,
                expanded, toggle == null ? null : rect => DrawDisclosure(rect, expanded, "ToggleRomanceRules", toggle, prepare));
        }
        /// <summary>内容只绘制一次并据实际推进高度设置边框，避免测量时重复调用按钮或初始化提交。</summary>
        private static void DrawBlock(Listing_Standard listing, string title, Action<Listing_Standard> content,
            Action<Rect> reset, bool embedded, string titleTip, bool expanded, Action<Rect> disclosure)
        {
            float top = listing.CurHeight, width = listing.ColumnWidth;
            var inner = new Listing_Standard { maxOneColumn = true };
            var previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            float height;
            inner.Begin(new Rect(12f, top + 12f, Mathf.Max(1f, width - 24f), 100000f));
            try
            {
                // Begin 会套用列表默认字号。嵌入窗口的测量和绘制统一用概况正文的字号。
                if (embedded)
                {
                    Text.Font = GameFont.Medium;
                    Text.WordWrap = true;
                }
                // 收起时只绘制标题与箭头，恢复默认和规则均不会注册点击。
                DrawHeader(inner, title, expanded ? reset : null, embedded, titleTip, disclosure);
                if (expanded)
                {
                    if (embedded) inner.Gap(8f);
                    else inner.GapLine(6f);
                    content(inner);
                }
            }
            finally
            {
                height = inner.CurHeight + 24f;
                if (embedded)
                {
                    Text.Font = previousFont;
                    Text.WordWrap = previousWrap;
                }
                inner.End();
            }
            listing.GetRect(height);
            // 调教窗口在外层绘制面板底色，独立旧入口继续使用边框。
            if (!embedded) Widgets.DrawBox(new Rect(0f, top, width, height));
        }

        /// <summary>按译文实测首行按钮，长标题在剩余空间换行；局部文字状态不会泄露到其他区域。</summary>
        private static void DrawHeader(Listing_Standard listing, string title, Action<Rect> reset, bool embedded, string titleTip,
            Action<Rect> disclosure)
        {
            var anchor = Text.Anchor; bool wrap = Text.WordWrap;
            try
            {
                Text.Anchor = embedded ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
                Text.WordWrap = true;
                string label = "SSC_BehaviorReset_Button".Translate();
                float buttonWidth = reset == null ? 0f :
                    Mathf.Min(listing.ColumnWidth * .46f, Mathf.Max(72f, Text.CalcSize(label).x + 18f));
                float padding = embedded ? 6f : 0f;
                float arrowWidth = disclosure == null ? 0f : Mathf.Min(20f, listing.ColumnWidth * .18f);
                float buttonGap = reset == null ? 0f : 8f;
                float titleWidth = Mathf.Max(1f, listing.ColumnWidth - buttonWidth - buttonGap - 2f * padding - arrowWidth);
                float buttonHeight = reset == null ? 28f :
                    Mathf.Max(28f, Text.CalcHeight(label, Mathf.Max(1f, buttonWidth - 12f)) + 8f);
                if (embedded) title = "<b>" + title + "</b>";
                Rect header = listing.GetRect(Mathf.Max(buttonHeight, Text.CalcHeight(title, titleWidth)) + 2f * padding);
                if (embedded) Widgets.DrawBoxSolid(header, HeaderColor);
                var titleRect = new Rect(header.x + padding + arrowWidth, header.y + padding, titleWidth, header.height - 2f * padding);
                Widgets.Label(titleRect, title);
                if (titleTip != null) TooltipHandler.TipRegion(titleRect, titleTip);
                // 标题按钮和恢复按钮使用独立的矩形，点击恢复不会同时折叠板块。
                disclosure?.Invoke(new Rect(header.x, header.y, Mathf.Max(1f, header.width - buttonWidth - buttonGap), header.height));
                float buttonY = embedded ? header.y + (header.height - buttonHeight) / 2f : header.y;
                reset?.Invoke(new Rect(titleRect.xMax + buttonGap, buttonY, buttonWidth, buttonHeight));
            }
            finally { Text.Anchor = anchor; Text.WordWrap = wrap; }
        }

        /// <summary>标题是独立的折叠按钮，点击区域在恢复默认按钮之前结束。</summary>
        private static void DrawDisclosure(Rect rect, bool expanded, string key, Action toggle,
            Func<Rect, object, Action, Action> prepare)
        {
            using (new TextTint(CopperColor))
                Widgets.Label(new Rect(rect.x + 6f, rect.y, 18f, rect.height), expanded ? "▾" : "▸");
            if (!GUI.enabled) return;
            Widgets.DrawHighlightIfMouseover(rect);
            var submit = prepare == null ? toggle : prepare(rect, key, toggle);
            if (Widgets.ButtonInvisible(rect)) submit?.Invoke();
        }

        /// <summary>宽处并排绘制两组并取较高的一组；窄处按原阅读顺序堆叠。</summary>
        internal static void DrawRulePair(Listing_Standard listing, Action<Listing_Standard> first, Action<Listing_Standard> second)
        {
            const float gap = 12f;
            const float minimumColumnWidth = 230f;
            float columnWidth = (listing.ColumnWidth - gap) / 2f;
            if (columnWidth < minimumColumnWidth)
            {
                first(listing);
                listing.Gap(gap);
                second(listing);
                return;
            }

            float y = listing.CurHeight;
            float firstHeight = DrawColumn(0f, first);
            float secondHeight = DrawColumn(columnWidth + gap, second);
            listing.GetRect(Mathf.Max(firstHeight, secondHeight));

            float DrawColumn(float x, Action<Listing_Standard> draw)
            {
                var font = Text.Font;
                var column = new Listing_Standard { maxOneColumn = true };
                column.Begin(new Rect(x, y, columnWidth, 100000f));
                // 原版 Begin 会重置字号，子列沿用父板块的 Medium 测量和绘制。
                Text.Font = font;
                try { draw(column); return column.CurHeight; }
                finally { column.End(); Text.Font = font; }
            }
        }

        /// <summary>共享标题按钮仅绘制和分发原请求；禁用时灰显，事件后恢复外层 GUI 状态。</summary>
        private static void DrawReset(Rect rect, bool canReset, string tipKey, string reason, Action submit, string key,
            Func<Rect, object, Action, Action> prepare)
        {
            bool enabled = GUI.enabled; Color color = GUI.color;
            try
            {
                GUI.enabled = enabled && canReset;
                if (!GUI.enabled) GUI.color = color * new Color(.55f, .55f, .55f);
                if (GUI.enabled && prepare != null) submit = prepare(rect, key, submit);
                string label = "SSC_BehaviorReset_Button".Translate();
                bool clicked = prepare != null ? DrawEmbeddedButton(rect, label) : Widgets.ButtonText(rect, label);
                if (clicked && GUI.enabled) submit?.Invoke();
            }
            finally { GUI.enabled = enabled; GUI.color = color; }
            string tip = tipKey.Translate();
            if (reason != null) tip += "\n\n" + ("SSC_BehaviorReset_" + reason).Translate();
            TooltipHandler.TipRegion(rect, tip);
        }

        /// <summary>只提示当前点击失败；无需新增确认窗口、成功消息或社交／心情事件。</summary>
        private static void ResetFailed(Pawn pawn, string reason)
            => Messages.Message(("SSC_BehaviorReset_" + (reason ?? "Unavailable")).Translate(), pawn, MessageTypeDefOf.RejectInput, false);

        /// <summary>调教窗口的按钮用深灰蓝底，文字按实际宽度换行，禁用时只绘制。</summary>
        internal static bool DrawEmbeddedButton(Rect rect, string label)
        {
            var anchor = Text.Anchor;
            var color = GUI.color;
            bool wrap = Text.WordWrap;
            try
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(rect, GUI.enabled ? SelectedColor : ControlColor);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), DividerColor);
                if (GUI.enabled) Widgets.DrawHighlightIfMouseover(rect);
                using var tint = new TextTint(GUI.enabled ? PrimaryText : DisabledText);
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = true;
                Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, Mathf.Max(1f, rect.width - 12f), rect.height - 8f), label);
                return GUI.enabled && Widgets.ButtonInvisible(rect);
            }
            finally { Text.Anchor = anchor; Text.WordWrap = wrap; GUI.color = color; }
        }
    }
    /// <summary>十一项浪漫偏好沿独立 GetView/TrySet；不注册为通用能力，不创建隐式默认配置。</summary>
    public static class SSCRomanceUI
    {
        private static readonly SSCRomanceSetting[][] groups =
        {
            new[] { SSCRomanceSetting.InitiateRomance, SSCRomanceSetting.InitiateProposal, SSCRomanceSetting.InitiateBreakup },
            new[] { SSCRomanceSetting.AcceptRomance, SSCRomanceSetting.AcceptProposal }
        };
        /// <summary>保留分组和命令，常驻只显示标签，说明放入悬停；首组复用主标题下横线，所有子标题下方另画横线。</summary>
        public static void DrawPawn(Listing_Standard listing, Pawn pawn)
            => DrawPawn(listing, pawn, null, null);

        /// <summary>允许外层调教窗口保留原视图，并在命令按钮响应时接管目标菜单。</summary>
        internal static void DrawPawn(Listing_Standard listing, Pawn pawn, Func<Rect, object, Action, Action> prepare,
            Action<SSCRomanceCommandContext> openTargets)
        {
            if (prepare != null)
            {
                // 对象按钮紧跟各自的规则组，只有命令区域使用整个面板宽度。
                SSCBehaviorControlUI.DrawRulePair(listing, left => DrawGroupWithTargets(left, 0, true),
                    right => DrawGroupWithTargets(right, 1, true));
                listing.Gap(8f);
            }
            else
            {
                for (int i = 0; i < groups.Length; i++)
                    DrawGroupWithTargets(listing, i, i == 0);
                listing.GapLine();
            }
            DrawCommands(listing, pawn, prepare, openTargets ?? SSCRomanceCommandMenu.OpenTargets);

            // 规则和对象许可放进同一列，保持各自的阅读与点击顺序。
            void DrawGroupWithTargets(Listing_Standard target, int index, bool first)
            {
                DrawGroup(target, pawn, groups[index], first, prepare);
                DrawTargets(target, pawn, index == 1, prepare);
            }
        }
        /// <summary>子标题居中并统一下横线的绘制颜色；恢复 GUI 状态，避免影响后续开关及禁用样式。</summary>
        private static void DrawSubtitle(Listing_Standard listing, string label, bool embedded)
        {
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                Text.Anchor = embedded ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                using (new TextTint(embedded ? SecondaryText : GUI.contentColor)) listing.Label(label);
                if (embedded)
                {
                    var line = listing.GetRect(6f);
                    Widgets.DrawBoxSolid(new Rect(line.x, line.y + 2f, line.width, 1f), DividerColor);
                }
                else listing.GapLine(6f);
            }
            finally { Text.Anchor = previousAnchor; GUI.color = previousColor; }
        }

        /// <summary>各语言统一使用单列，一行一个开关；先捕获整组视图，绘制回调不能替后续条目重拍授权。</summary>
        private static void DrawGroup(Listing_Standard listing, Pawn pawn, SSCRomanceSetting[] settings, bool first,
            Func<Rect, object, Action, Action> prepare)
        {
            if (!first)
            {
                if (prepare == null) listing.GapLine(6f);
                else listing.Gap(6f);
            }
            DrawSubtitle(listing, ("SSC_Romance_Group_" + settings[0]).Translate(), prepare != null);
            var views = new SSCRomanceView[settings.Length];
            var labels = new string[settings.Length];
            for (int i = 0; i < settings.Length; i++)
            { views[i] = SSCRomancePreferences.GetView(pawn, settings[i]); labels[i] = ("SSC_Romance_Setting_" + settings[i]).Translate(); }
            for (int i = 0; i < settings.Length; i++)
            {
                float width = listing.ColumnWidth;
                float labelWidth = Mathf.Max(1f, width - 34f);
                float height = Mathf.Max(prepare != null ? 32f : 28f, Text.CalcHeight(labels[i], labelWidth));
                Rect row = listing.GetRect(height + 8f);
                DrawSetting(new Rect(row.x, row.y, width, row.height), pawn, settings[i], views[i], labels[i], prepare);
            }
        }
        /// <summary>主动对象与被动接受保证复用多选绘制；先捕获整组原视图，点击不能为旧请求重新取得人格权限。</summary>
        private static void DrawTargets(Listing_Standard listing, Pawn pawn, bool passive, Func<Rect, object, Action, Action> prepare)
        {
            // 展示类别共用短标签，实际提交仍分别指向主动许可或被动保证，不能混用保存源。
            var labelsFor = new[] { SSCRomanceSetting.InitiateToMaster, SSCRomanceSetting.InitiateToSexSlave, SSCRomanceSetting.InitiateToOthers };
            var settings = passive
                ? new[] { SSCRomanceSetting.AlwaysAcceptMaster, SSCRomanceSetting.AlwaysAcceptSexSlave, SSCRomanceSetting.AlwaysAcceptOthers }
                : labelsFor;
            var views = new SSCRomanceView[3];
            var labels = new string[3];
            for (int i = 0; i < settings.Length; i++)
            {
                views[i] = SSCRomancePreferences.GetView(pawn, settings[i]);
                labels[i] = (views[i].Saved ? "✓ " : "□ ") + ("SSC_Romance_Setting_" + labelsFor[i]).Translate();
            }
            const float indent = 12f, padding = 8f, gap = 6f;
            float width = Mathf.Max(1f, listing.ColumnWidth - indent - padding * 2f);
            int columns = 3;
            // 长译文优先减少列数，单列仍可按实际高度换行，不挤压按钮文字或越过外框。
            while (columns > 1)
            {
                float candidateWidth = Mathf.Max(1f, (width - gap * (columns - 1)) / columns);
                bool fits = true;
                float candidateTextWidth = Mathf.Max(1f, candidateWidth - 12f);
                foreach (string label in labels)
                {
                    if (prepare != null ? Text.CalcSize(label).x > candidateTextWidth :
                        Text.CalcHeight(label, candidateTextWidth) > 24f)
                        fits = false;
                }
                if (fits) break;
                columns--;
            }
            float buttonWidth = Mathf.Max(1f, (width - gap * (columns - 1)) / columns);
            float buttonHeight = 32f;
            foreach (string label in labels) buttonHeight = Mathf.Max(buttonHeight, Text.CalcHeight(label, Mathf.Max(1f, buttonWidth - 12f)) + 12f);
            string title = (passive ? "SSC_Romance_AcceptTargetTitle" : "SSC_Romance_TargetTitle").Translate();
            float titleHeight = Text.CalcHeight(title, width);
            int rows = (settings.Length + columns - 1) / columns;
            listing.Gap(4f);
            Rect area = listing.GetRect(padding * 2f + titleHeight + gap + rows * buttonHeight + (rows - 1) * gap);
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousEnabled = GUI.enabled, previousWrap = Text.WordWrap;
            try
            {
                GUI.color = Color.white;
                bool embedded = prepare != null;
                Widgets.DrawBoxSolid(new Rect(area.x + indent, area.y, area.width - indent, area.height),
                    embedded ? ControlColor : new Color(.14f, .14f, .14f, 1f));
                GUI.color = embedded ? Color.white : new Color(.75f, .75f, .75f, 1f);
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                using (new TextTint(embedded ? SecondaryText : GUI.contentColor))
                    Widgets.Label(new Rect(area.x + indent + padding, area.y + padding, width, titleHeight), title);
                for (int i = 0; i < settings.Length; i++)
                {
                    var view = views[i];
                    Rect button = new Rect(area.x + indent + padding + (i % columns) * (buttonWidth + gap),
                        area.y + padding + titleHeight + gap + (i / columns) * (buttonHeight + gap), buttonWidth, buttonHeight);
                    GUI.enabled = previousEnabled && view.CanChange;
                    GUI.color = Color.white;
                    Color fill = !GUI.enabled ? (embedded ? ControlColor : new Color(.18f, .18f, .18f)) :
                        embedded ? (view.Saved ? SelectedColor : HeaderColor) :
                        view.Saved ? new Color(.30f, .38f, .24f) : new Color(.22f, .22f, .22f);
                    Widgets.DrawBoxSolid(button, fill);
                    GUI.color = GUI.enabled || embedded ? Color.white : new Color(.55f, .55f, .55f, 1f);
                    using var targetTint = new TextTint(embedded && !GUI.enabled ? DisabledText : GUI.contentColor);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(button.x + 6f, button.y + 4f, Mathf.Max(1f, button.width - 12f), button.height - 8f), labels[i]);
                    var setting = settings[i];
                    Action submit = () => SubmitSetting(pawn, setting, view);
                    if (GUI.enabled && prepare != null) submit = prepare(button, setting, submit);
                    if (GUI.enabled && Widgets.ButtonInvisible(button) && GUI.enabled) submit?.Invoke();
                    TooltipHandler.TipRegion(button, SettingTip(setting, view));
                }
            }
            finally { GUI.color = previousColor; GUI.enabled = previousEnabled; Text.Anchor = previousAnchor; Text.WordWrap = previousWrap; }
        }
        /// <summary>调教窗口内的直接命令优先横排，宽度不足时减少列数；整组共用原执行者凭据。</summary>
        private static void DrawCommands(Listing_Standard listing, Pawn pawn, Func<Rect, object, Action, Action> prepare,
            Action<SSCRomanceCommandContext> openTargets)
        {
            DrawSubtitle(listing, "SSC_Romance_CommandTitle".Translate(), prepare != null);
            string failure = SSCRomanceCommandUtility.ActorFailure(pawn);
            var original = SSCRomanceCommandUtility.Capture(pawn);
            var actions = (SSCRomanceAction[])Enum.GetValues(typeof(SSCRomanceAction));
            var labels = Array.ConvertAll(actions, action => ("SSC_Romance_Action_" + action).Translate().ToString());
            float gap = prepare == null ? 4f : 8f;
            int columns = 1;
            if (prepare != null)
            {
                // 用最长命令实测可放的列数；单列时仍以实际文字高度换行。
                float minimumWidth = 1f;
                foreach (string label in labels)
                    minimumWidth = Mathf.Max(minimumWidth, Text.CalcSize(label).x + 12f);
                columns = Math.Clamp((int)((listing.ColumnWidth + gap) / (minimumWidth + gap)), 1, actions.Length);
            }
            float cellWidth = (listing.ColumnWidth - (columns - 1) * gap) / columns;
            bool oldEnabled = GUI.enabled;
            try
            {
                GUI.enabled = oldEnabled && failure == null;
                for (int first = 0; first < actions.Length; first += columns)
                {
                    // 每行按最高按钮推进列表，点击矩形与绘制矩形使用同一尺寸。
                    int count = Math.Min(columns, actions.Length - first);
                    float height = 32f;
                    for (int i = first; i < first + count; i++)
                        height = Mathf.Max(height, Text.CalcHeight(labels[i], Mathf.Max(1f, cellWidth - 12f)) + 12f);
                    Rect row = listing.GetRect(height);
                    for (int i = 0; i < count; i++)
                    {
                        var action = actions[first + i];
                        string label = labels[first + i];
                        var button = new Rect(row.x + i * (cellWidth + gap), row.y, cellWidth, row.height);
                        DrawCommand(button, action, label);
                    }
                    listing.Gap(gap);
                }
            }
            finally { GUI.enabled = oldEnabled; }

            // 命令从整组原执行者快照派生，点击时只进入当前动作的目标菜单。
            void DrawCommand(Rect button, SSCRomanceAction action, string label)
            {
                var context = original.ForTarget(null, action);
                if (prepare != null)
                {
                    var submit = GUI.enabled ? prepare(button, action, () => openTargets(context)) : null;
                    if (SSCBehaviorControlUI.DrawEmbeddedButton(button, label)) submit?.Invoke();
                }
                else if (!GUI.enabled) DrawDisabledCommand(button, label);
                else if (Widgets.ButtonText(button, label) && GUI.enabled) openTargets(context);
                string tip = ("SSC_Romance_CommandTip_" + action).Translate();
                if (failure != null) tip += "\n\n" + SSCRomanceCommandUtility.Explain(failure);
                TooltipHandler.TipRegion(button, tip);
            }
        }
        /// <summary>禁用命令使用灰底灰字且不创建点击入口；悬停仍可显示原因，绘制后恢复文本与颜色状态。</summary>
        private static void DrawDisabledCommand(Rect rect, string label)
        {
            Color color = GUI.color;
            var anchor = Text.Anchor;
            bool wrap = Text.WordWrap;
            try
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(rect, new Color(.24f, .24f, .24f, 1f));
                GUI.color = new Color(.65f, .65f, .65f, 1f);
                Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = true;
                Widgets.Label(new Rect(rect.x + 4f, rect.y + 2f, Mathf.Max(1f, rect.width - 8f), Mathf.Max(1f, rect.height - 4f)), label);
            }
            finally { GUI.color = color; Text.Anchor = anchor; Text.WordWrap = wrap; }
        }
        /// <summary>同一行快照用于尺寸、绘制和提交；禁止点击时重新 GetView 为旧请求续权。</summary>
        private static void DrawSetting(Rect row, Pawn pawn, SSCRomanceSetting setting, SSCRomanceView view, string label,
            Func<Rect, object, Action, Action> prepare)
        {
            float labelWidth = Mathf.Max(1f, row.width - 34f);
            float labelHeight = row.height - 8f;
            bool previous = GUI.enabled, value = view.Saved;
            Color color = GUI.color;
            TextAnchor anchor = Text.Anchor;
            try
            {
                GUI.enabled = previous && view.CanChange;
                if (!GUI.enabled && prepare == null) GUI.color = color * new Color(.6f, .6f, .6f, 1f);
                if (prepare != null) Text.Anchor = TextAnchor.MiddleLeft;
                Action submit = () => SubmitSetting(pawn, setting, view);
                if (GUI.enabled && prepare != null) submit = prepare(row, setting, submit);
                using (new TextTint(prepare != null && !GUI.enabled ? DisabledText : GUI.contentColor))
                    Widgets.Label(new Rect(row.x, row.y, labelWidth, labelHeight), label);
                float iconX = row.xMax - 24f;
                float iconY = row.y + Mathf.Max(0f, (labelHeight - 24f) / 2f);
                if (prepare != null)
                {
                    // 图标沿用原版纹理。点击图标或文字都通过整行按钮提交原按压。
                    Widgets.CheckboxDraw(iconX, iconY, view.Saved, !GUI.enabled);
                    if (GUI.enabled && Widgets.ButtonInvisible(row)) value = !value;
                }
                else
                {
                    Widgets.Checkbox(iconX, iconY, ref value, 24f, !GUI.enabled);
                    if (GUI.enabled && Widgets.ButtonInvisible(new Rect(row.x, row.y, labelWidth, labelHeight))) value = !value;
                }
                if (GUI.enabled && value != view.Saved) submit?.Invoke();
            }
            finally { GUI.enabled = previous; GUI.color = color; Text.Anchor = anchor; }
            TooltipHandler.TipRegion(row, SettingTip(setting, view));
        }

        /// <summary>使用原视图的保存值和凭据，偏好接口负责核对当前绑定与解锁。</summary>
        private static void SubmitSetting(Pawn pawn, SSCRomanceSetting setting, SSCRomanceView view)
        {
            if (!SSCRomancePreferences.TrySet(pawn, setting, !view.Saved, view.Context, out var reason))
                Messages.Message(SettingFailureText(setting, reason), pawn, MessageTypeDefOf.RejectInput, false);
        }
        /// <summary>开关与对象按钮的提交失败共用具体阶段说明，其余原因继续走原入口。</summary>
        private static string SettingFailureText(SSCRomanceSetting setting, SSCRomanceReason reason)
            => reason == SSCRomanceReason.Stage
                ? "SSC_Romance_RequiredStage".Translate(SSCRomanceResolver.StageName(SSCRomanceResolver.RequiredStage(setting)))
                : SSCRomanceCommandUtility.Explain(reason.ToString());

        /// <summary>按钮与开关共享当前视图的悬停说明，资格原因只展示，不为旧点击重新捕获上下文。</summary>
        private static string SettingTip(SSCRomanceSetting setting, SSCRomanceView view)
        {
            string key = setting == SSCRomanceSetting.InitiateToMaster || setting == SSCRomanceSetting.InitiateToSexSlave || setting == SSCRomanceSetting.InitiateToOthers
                ? "SSC_Romance_TargetMeaning" : setting <= SSCRomanceSetting.InitiateBreakup ? "SSC_Romance_ActiveMeaning" :
                setting <= SSCRomanceSetting.AcceptProposal ? "SSC_Romance_PassiveMeaning" : "SSC_Romance_MustAcceptMeaning";
            // 三项被动接受保证共用相同说明格式；仅按各自视图追加资格与阶段信息。
            string tip = key.Translate();
            // 阶段不足直接显示具体名称，避免先给泛化原因再重复一条数字阶段提示。
            if (!view.CanChange && view.Reason != SSCRomanceReason.Stage) tip += "\n\n" + SSCRomanceCommandUtility.Explain(view.Reason.ToString());
            if (!view.Unlocked) tip += "\n\n" + "SSC_Romance_RequiredStage".Translate(SSCRomanceResolver.StageName(SSCRomanceResolver.RequiredStage(setting)));
            return tip;
        }

    }
}
