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
            var sexReset = SSCRestrictionEditor.GetResetView(pawn);
            DrawBlock(listing, "SSC_Romance_SexSection".Translate(), inner =>
            {
                if (showSexSwitches) Dialog_SSCRestrictions.DrawGlobalSwitches(inner);
                SSCRestrictionUI.DrawPawn(inner, pawn, true);
            }, rect => DrawReset(rect, sexReset.CanReset, "SSC_BehaviorReset_SexTip", sexReset.Reason, () =>
            {
                if (!SSCRestrictionEditor.TryReset(pawn, sexReset.Context, out var reason)) ResetFailed(pawn, reason);
            }));
            listing.Gap(14f);
            var romanceReset = SSCRomancePreferences.GetResetView(pawn);
            DrawBlock(listing, "SSC_Romance_RomanceSection".Translate(), inner => SSCRomanceUI.DrawPawn(inner, pawn),
                rect => DrawReset(rect, romanceReset.CanReset, "SSC_BehaviorReset_RomanceTip", romanceReset.Reason, () =>
                {
                    if (!SSCRomancePreferences.TryReset(pawn, romanceReset.Context, out var reason)) ResetFailed(pawn, reason);
                }));
        }
        /// <summary>内容只绘制一次并据实际推进高度设置边框，避免测量时重复调用按钮或初始化提交。</summary>
        private static void DrawBlock(Listing_Standard listing, string title, Action<Listing_Standard> content, Action<Rect> reset, bool drawDivider = true)
        {
            float top = listing.CurHeight, width = listing.ColumnWidth;
            var inner = new Listing_Standard { maxOneColumn = true };
            float height;
            inner.Begin(new Rect(12f, top + 12f, Mathf.Max(1f, width - 24f), 100000f));
            try
            {
                DrawHeader(inner, title, reset);
                if (drawDivider) inner.GapLine(6f);
                else inner.Gap(6f);
                content(inner);
            }
            finally { height = inner.CurHeight + 24f; inner.End(); }
            listing.GetRect(height);
            // 只描边，不在绘制完内容后填充背景，保证控件与文字不被覆盖。
            Widgets.DrawBox(new Rect(0f, top, width, height));
        }

        /// <summary>按译文实测首行按钮，长标题在剩余空间换行；局部文字状态不会泄露到其他区域。</summary>
        private static void DrawHeader(Listing_Standard listing, string title, Action<Rect> reset)
        {
            var anchor = Text.Anchor; bool wrap = Text.WordWrap;
            try
            {
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                string label = "SSC_BehaviorReset_Button".Translate();
                float buttonWidth = Mathf.Min(listing.ColumnWidth * .46f, Mathf.Max(72f, Text.CalcSize(label).x + 18f));
                float titleWidth = Mathf.Max(1f, listing.ColumnWidth - buttonWidth - 8f);
                float buttonHeight = Mathf.Max(28f, Text.CalcHeight(label, Mathf.Max(1f, buttonWidth - 12f)) + 8f);
                Rect header = listing.GetRect(Mathf.Max(buttonHeight, Text.CalcHeight(title, titleWidth)));
                Widgets.Label(new Rect(header.x, header.y, titleWidth, header.height), title);
                reset(new Rect(header.x + titleWidth + 8f, header.y, buttonWidth, buttonHeight));
            }
            finally { Text.Anchor = anchor; Text.WordWrap = wrap; }
        }

        /// <summary>共享标题按钮仅绘制和分发原请求；禁用时灰显，事件后恢复外层 GUI 状态。</summary>
        private static void DrawReset(Rect rect, bool canReset, string tipKey, string reason, Action submit)
        {
            bool enabled = GUI.enabled; Color color = GUI.color;
            try
            {
                GUI.enabled = enabled && canReset;
                if (!GUI.enabled) GUI.color = color * new Color(.55f, .55f, .55f);
                if (Widgets.ButtonText(rect, "SSC_BehaviorReset_Button".Translate()) && GUI.enabled) submit();
            }
            finally { GUI.enabled = enabled; GUI.color = color; }
            string tip = tipKey.Translate();
            if (reason != null) tip += "\n\n" + ("SSC_BehaviorReset_" + reason).Translate();
            TooltipHandler.TipRegion(rect, tip);
        }

        /// <summary>只提示当前点击失败；无需新增确认窗口、成功消息或社交／心情事件。</summary>
        private static void ResetFailed(Pawn pawn, string reason)
            => Messages.Message(("SSC_BehaviorReset_" + (reason ?? "Unavailable")).Translate(), pawn, MessageTypeDefOf.RejectInput, false);
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
        {
            for (int i = 0; i < groups.Length; i++)
            {
                DrawGroup(listing, pawn, groups[i], i == 0);
                DrawTargets(listing, pawn, i == 1);
            }
            listing.GapLine();
            DrawCommands(listing, pawn);
        }
        /// <summary>子标题居中并统一下横线的绘制颜色；恢复 GUI 状态，避免影响后续开关及禁用样式。</summary>
        private static void DrawSubtitle(Listing_Standard listing, string label)
        {
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                listing.Label(label);
                listing.GapLine(6f);
            }
            finally { Text.Anchor = previousAnchor; GUI.color = previousColor; }
        }

        /// <summary>各语言统一使用单列，一行一个开关；先捕获整组视图，绘制回调不能替后续条目重拍授权。</summary>
        private static void DrawGroup(Listing_Standard listing, Pawn pawn, SSCRomanceSetting[] settings, bool first)
        {
            if (!first) listing.GapLine(6f);
            DrawSubtitle(listing, ("SSC_Romance_Group_" + settings[0]).Translate());
            var views = new SSCRomanceView[settings.Length];
            var labels = new string[settings.Length];
            for (int i = 0; i < settings.Length; i++)
            { views[i] = SSCRomancePreferences.GetView(pawn, settings[i]); labels[i] = ("SSC_Romance_Setting_" + settings[i]).Translate(); }
            for (int i = 0; i < settings.Length; i++)
            {
                float width = listing.ColumnWidth;
                float labelWidth = Mathf.Max(1f, width - 34f);
                float height = Mathf.Max(28f, Text.CalcHeight(labels[i], labelWidth));
                Rect row = listing.GetRect(height + 8f);
                DrawSetting(new Rect(row.x, row.y, width, row.height), pawn, settings[i], views[i], labels[i]);
            }
        }
        /// <summary>主动对象与被动接受保证复用多选绘制；先捕获整组原视图，点击不能为旧请求重新取得人格权限。</summary>
        private static void DrawTargets(Listing_Standard listing, Pawn pawn, bool passive)
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
                foreach (string label in labels) if (Text.CalcHeight(label, Mathf.Max(1f, candidateWidth - 12f)) > 24f) fits = false;
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
                Widgets.DrawBoxSolid(new Rect(area.x + indent, area.y, area.width - indent, area.height), new Color(.14f, .14f, .14f, 1f));
                GUI.color = new Color(.75f, .75f, .75f, 1f);
                Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Widgets.Label(new Rect(area.x + indent + padding, area.y + padding, width, titleHeight), title);
                for (int i = 0; i < settings.Length; i++)
                {
                    var view = views[i];
                    Rect button = new Rect(area.x + indent + padding + (i % columns) * (buttonWidth + gap),
                        area.y + padding + titleHeight + gap + (i / columns) * (buttonHeight + gap), buttonWidth, buttonHeight);
                    GUI.enabled = previousEnabled && view.CanChange;
                    GUI.color = Color.white;
                    Color fill = !GUI.enabled ? new Color(.18f, .18f, .18f, 1f) :
                        view.Saved ? new Color(.30f, .38f, .24f, 1f) : new Color(.22f, .22f, .22f, 1f);
                    Widgets.DrawBoxSolid(button, fill);
                    GUI.color = GUI.enabled ? Color.white : new Color(.55f, .55f, .55f, 1f);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(button.x + 6f, button.y + 4f, Mathf.Max(1f, button.width - 12f), button.height - 8f), labels[i]);
                    if (GUI.enabled && Widgets.ButtonInvisible(button) && GUI.enabled &&
                        !SSCRomancePreferences.TrySet(pawn, settings[i], !view.Saved, view.Context, out var reason))
                        Messages.Message(SSCRomanceCommandUtility.Explain(reason.ToString()), pawn, MessageTypeDefOf.RejectInput, false);
                    TooltipHandler.TipRegion(button, SettingTip(settings[i], view));
                }
            }
            finally { GUI.color = previousColor; GUI.enabled = previousEnabled; Text.Anchor = previousAnchor; Text.WordWrap = previousWrap; }
        }
        /// <summary>底部三个按钮纵向排列，长译文按实际宽度换行；整组使用最初捕获的执行者，点击不重拍人格。</summary>
        private static void DrawCommands(Listing_Standard listing, Pawn pawn)
        {
            DrawSubtitle(listing, "SSC_Romance_CommandTitle".Translate());
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
                    if (!GUI.enabled) DrawDisabledCommand(button, label);
                    else if (Widgets.ButtonText(button, label) && GUI.enabled) SSCRomanceCommandMenu.OpenTargets(context);
                    // 每个命令只显示一句明确动作的说明；禁用原因仍按当前资格追加。
                    string tip = ("SSC_Romance_CommandTip_" + action).Translate();
                    if (failure != null) tip += "\n\n" + SSCRomanceCommandUtility.Explain(failure);
                    TooltipHandler.TipRegion(button, tip);
                    listing.Gap(4f);
                }
            }
            finally { GUI.enabled = oldEnabled; }
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
        private static void DrawSetting(Rect row, Pawn pawn, SSCRomanceSetting setting, SSCRomanceView view, string label)
        {
            float labelWidth = Mathf.Max(1f, row.width - 34f);
            float labelHeight = row.height - 8f;
            bool previous = GUI.enabled, value = view.Saved;
            Color color = GUI.color;
            try
            {
                GUI.enabled = previous && view.CanChange;
                if (!GUI.enabled) GUI.color = color * new Color(.6f, .6f, .6f, 1f);
                Widgets.Label(new Rect(row.x, row.y, labelWidth, labelHeight), label);
                Widgets.Checkbox(row.xMax - 24f, row.y + Mathf.Max(0f, (labelHeight - 24f) / 2f), ref value, 24f, !GUI.enabled);
                if (GUI.enabled && Widgets.ButtonInvisible(new Rect(row.x, row.y, labelWidth, labelHeight))) value = !value;
                if (GUI.enabled && value != view.Saved && !SSCRomancePreferences.TrySet(pawn, setting, value, view.Context, out var reason))
                    Messages.Message(SSCRomanceCommandUtility.Explain(reason.ToString()), pawn, MessageTypeDefOf.RejectInput, false);
            }
            finally { GUI.enabled = previous; GUI.color = color; }
            TooltipHandler.TipRegion(row, SettingTip(setting, view));
        }
        /// <summary>按钮与开关共享当前视图的悬停说明，资格原因只展示，不为旧点击重新捕获上下文。</summary>
        private static string SettingTip(SSCRomanceSetting setting, SSCRomanceView view)
        {
            string key = setting == SSCRomanceSetting.InitiateToMaster || setting == SSCRomanceSetting.InitiateToSexSlave || setting == SSCRomanceSetting.InitiateToOthers
                ? "SSC_Romance_TargetMeaning" : setting <= SSCRomanceSetting.InitiateBreakup ? "SSC_Romance_ActiveMeaning" :
                setting <= SSCRomanceSetting.AcceptProposal ? "SSC_Romance_PassiveMeaning" : "SSC_Romance_MustAcceptMeaning";
            // 三项被动接受保证共用相同说明格式；仅按各自视图追加资格与阶段信息。
            string tip = key.Translate();
            if (!view.CanChange) tip += "\n\n" + SSCRomanceCommandUtility.Explain(view.Reason.ToString());
            if (!view.Unlocked) tip += "\n" + "SSC_Romance_RequiredStage".Translate(SSCRomanceResolver.RequiredStage(setting));
            return tip;
        }

    }
}
