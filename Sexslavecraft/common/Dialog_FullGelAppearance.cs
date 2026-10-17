// AI 辅助生成。独立窗口组合1.7原版控件；实际行高驱动布局，不复制原版实现。
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class Dialog_FullGelAppearance : Window
    {
        private const string HexControl = "SSC_GelHex", ColorControl = "SSC_GelComponents";
        private const float Gap = 16;
        // 固定打开时的身体；失效窗口不能把草稿写入后来选中的角色或新身体。
        private readonly Pawn pawn;
        private readonly HediffWithComps owner;
        private readonly FullGelAppearance original;
        private readonly FullGelAppearanceDraft draft;
        private static readonly List<Color> Palette = new List<Color>
        {
            new Color(FullGelAppearanceUtility.DefaultRed, FullGelAppearanceUtility.DefaultGreen, FullGelAppearanceUtility.DefaultBlue, 1),
            new Color(.65f,.20f,.35f,1), new Color(.15f,.40f,.65f,1), new Color(.15f,.55f,.35f,1),
            new Color(.65f,.45f,.15f,1), new Color(.70f,.70f,.70f,1), new Color(1,0,0,1),
            new Color(0,1,0,1), new Color(0,0,1,1), new Color(0,0,0,1), new Color(1,1,1,1)
        };
        // 色轮拖拽、延迟输入和页签状态跨GUI事件保留；拖动时不刷新地图角色。
        private bool dragging, hexInvalid, applyPending, rgbMode;
        private bool? pendingRgb;
        private string previousFocus, hex, opacityBuffer;
        private string[] componentBuffers = new string[6];
        private Color componentColor;
        private Vector2 scroll;
        private float contentHeight = 600;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(720, UI.screenWidth - 32), Mathf.Min(700, UI.screenHeight - 32));

        internal Dialog_FullGelAppearance(Pawn pawn, HediffWithComps owner)
        {
            this.pawn = pawn; this.owner = owner;
            original = FullGelAppearanceUtility.Read(owner); draft = new FullGelAppearanceDraft(original);
            ResetBuffers(); optionalTitle = "SSC_GelAppearance_Title".Translate() + " — " + pawn.LabelShort;
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true;
            closeOnClickedOutside = true; closeOnAccept = false;
        }

        private void ResetBuffers()
        {
            // 预设/恢复后清除旧文本缓冲，防止失焦提交反写旧颜色。
            componentBuffers = new string[6]; componentColor = draft.Rgb;
            hex = "#" + ColorUtility.ToHtmlStringRGB(draft.Rgb); opacityBuffer = null; hexInvalid = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (TextBlock.Default())
            {
                bool active = ReferenceEquals(FullGelAppearanceUtility.GetOwner(pawn), owner);
                bool commitMode = pendingRgb.HasValue;
                // 固定底栏在滚动区外；用实际控件底边回填内容高度，支持窄屏及长译文。
                Rect viewport = new Rect(0, 0, inRect.width, Mathf.Max(1, inRect.height - 52));
                float width = Mathf.Max(1, viewport.width - 20);
                Widgets.BeginScrollView(viewport, ref scroll, new Rect(0, 0, width, contentHeight));
                try
                {
                    string description = (!active ? "SSC_GelAppearance_NoBody" : FullGelAppearanceUtility.ShouldRender(pawn)
                        ? "SSC_GelAppearance_Description" : "SSC_GelAppearance_Disabled").Translate();
                    float y = Label(0, 0, width, description) + Gap;
                    bool columns = width >= 500;
                    float leftWidth = columns ? Mathf.Min(240, width * .42f) : width;
                    float rightX = columns ? leftWidth + 24 : 0;
                    float rightWidth = columns ? width - rightX : width;
                    Color before = draft.Rgb;
                    // 色轮控制H/S；独立明度滑块提供直接调整深浅的入口。
                    float wheelSize = Mathf.Min(220, leftWidth);
                    Widgets.HSVColorWheel(new Rect((leftWidth - wheelSize) / 2, y, wheelSize, wheelSize), ref draft.Rgb, ref dragging);
                    float leftY = Label(0, y + wheelSize + 12, leftWidth, "SSC_GelAppearance_Brightness".Translate()) + 4;
                    Color.RGBToHSV(draft.Rgb, out float hue, out float saturation, out float value);
                    float nextValue = Widgets.HorizontalSlider(new Rect(0, leftY, leftWidth, 26), value, 0, 1);
                    if (nextValue != value) draft.Rgb = Color.HSVToRGB(hue, saturation, nextValue);
                    leftY += 26 + Gap;
                    // 一次显示RGB或HSV三个分量；读取原版Aggregator实际底边，不假定行高。
                    float rightY = columns ? y : leftY;
                    float half = (rightWidth - 8) / 2;
                    if (Widgets.ButtonText(new Rect(rightX, rightY, half, 30), rgbMode ? "RGB ✓" : "RGB"))
                    { GUI.FocusControl(null); pendingRgb = true; }
                    if (Widgets.ButtonText(new Rect(rightX + half + 8, rightY, half, 30), rgbMode ? "HSV" : "HSV ✓"))
                    { GUI.FocusControl(null); pendingRgb = false; }
                    var aggregator = new RectAggregator(new Rect(rightX, rightY + 38, rightWidth, 0), 17508032);
                    var visible = rgbMode ? Widgets.ColorComponents.Red | Widgets.ColorComponents.Green | Widgets.ColorComponents.Blue
                        : Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value;
                    Widgets.ColorTextfields(ref aggregator, ref draft.Rgb, ref componentBuffers, ref componentColor,
                        previousFocus, ColorControl, visible, visible);
                    rightY = aggregator.Rect.yMax + Gap;
                    if (before.r != draft.Rgb.r || before.g != draft.Rgb.g || before.b != draft.Rgb.b)
                    { draft.Edited(); hex = "#" + ColorUtility.ToHtmlStringRGB(draft.Rgb); hexInvalid = false; }
                    rightY = DrawHex(rightX, rightY, rightWidth);
                    // 色块预览真实Alpha；棋盘预览不创建贴图或调用角色肖像渲染器。
                    rightY = Label(rightX, rightY + Gap, rightWidth, "SSC_GelAppearance_Preview".Translate()) + 6;
                    float labelHeight = Mathf.Max(Text.CalcHeight("SSC_GelAppearance_Old".Translate(), half),
                        Text.CalcHeight("SSC_GelAppearance_New".Translate(), half));
                    Widgets.Label(new Rect(rightX, rightY, half, labelHeight), "SSC_GelAppearance_Old".Translate());
                    Widgets.Label(new Rect(rightX + half + 8, rightY, half, labelHeight), "SSC_GelAppearance_New".Translate());
                    rightY += labelHeight + 4;
                    SSCUIWidgets.Swatch(new Rect(rightX, rightY, half, 40), original.Color);
                    SSCUIWidgets.Swatch(new Rect(rightX + half + 8, rightY, half, 40), draft.Value.Color);
                    rightY += 40;
                    // 色板置于两列较低底边之后，跟随控件实际高度推进；预设不改变不透明度。
                    y = (columns ? Mathf.Max(leftY, rightY) : rightY) + Gap;
                    y = Label(0, y, width, "SSC_GelAppearance_Presets".Translate()) + 6;
                    int count = Math.Max(1, (int)((width + 2) / 36));
                    float paletteHeight = (float)Math.Ceiling((double)Palette.Count / count) * 34;
                    if (Widgets.ColorSelector(new Rect(0, y, width, paletteHeight), ref draft.Rgb, Palette,
                        out float usedHeight, colorSize: 28))
                    { GUI.FocusControl(null); draft.Edited(); ResetBuffers(); }
                    y += usedHeight + Gap;
                    // 滑块与数字输入共用百分比；原版颜色控件重建Alpha不会改变不透明度。
                    y = Label(0, y, width, "SSC_GelAppearance_Opacity".Translate() + " " + draft.OpacityPercent + "%") + 4;
                    int oldOpacity = draft.OpacityPercent;
                    draft.OpacityPercent = (int)Widgets.HorizontalSlider(new Rect(0, y, Mathf.Max(1, width - 88), 30),
                        draft.OpacityPercent, 0, 100, roundTo: 1);
                    if (draft.OpacityPercent != oldOpacity) opacityBuffer = null;
                    Widgets.TextFieldNumeric(new Rect(width - 78, y, 78, 30), ref draft.OpacityPercent, ref opacityBuffer, 0, 100);
                    if (draft.OpacityPercent != oldOpacity) draft.Edited();
                    contentHeight = Label(0, y + 30 + Gap, width, "SSC_GelAppearance_SkinOnly".Translate()) + 8;
                }
                finally { Widgets.EndScrollView(); }
                // 先让旧页数值失焦提交，下一事件再切换页签，保留最后输入。
                if (commitMode && pendingRgb.HasValue)
                { rgbMode = pendingRgb.Value; pendingRgb = null; ResetBuffers(); }
                if (applyPending)
                {
                    applyPending = false;
                    if (active && !hexInvalid && FullGelAppearanceUtility.Apply(pawn, owner, draft.Value, draft.ResetRequested))
                    { Close(); return; }
                }
                // 恢复只影响配色草稿；确认颜色不会回写或重置在ITab中切换的个人渲染状态。
                float buttonWidth = (inRect.width - 20) / 3;
                float buttonY = inRect.height - 38;
                if (Widgets.ButtonText(new Rect(0, buttonY, buttonWidth, 38), "SSC_GelAppearance_Reset".Translate()))
                { GUI.FocusControl(null); draft.Reset(); ResetBuffers(); }
                if (Widgets.ButtonText(new Rect(buttonWidth + 10, buttonY, buttonWidth, 38), "Cancel".Translate())) Close();
                if (SSCUIWidgets.Button(new Rect(2 * buttonWidth + 20, buttonY, buttonWidth, 38),
                    "Accept".Translate(), active && !hexInvalid))
                { GUI.FocusControl(null); applyPending = true; }
                if (Event.current.type == EventType.layout) previousFocus = GUI.GetNameOfFocusedControl();
            }
        }

        private float DrawHex(float x, float y, float width)
        {
            y = Label(x, y, width, "SSC_GelAppearance_Hex".Translate()) + 4;
            GUI.SetNextControlName(HexControl);
            string next = Widgets.TextField(new Rect(x, y, width, 30), hex);
            if (next != hex)
            {
                hex = next; hexInvalid = !draft.SetHex(hex);
                if (!hexInvalid) { componentColor = draft.Rgb; componentBuffers = new string[6]; }
            }
            y += 30;
            if (hexInvalid) y = Label(x, y + 4, width, "SSC_GelAppearance_InvalidHex".Translate());
            return y;
        }

        private static float Label(float x, float y, float width, string text)
        {
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(x, y, width, height), text);
            return y + height;
        }
    }
}
