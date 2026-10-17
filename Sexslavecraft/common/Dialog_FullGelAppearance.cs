// AI 辅助生成。组合 1.7 原版 Widgets，不复制原版颜色选择器源码。
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class Dialog_FullGelAppearance : Window
    {
        private const string HexControl = "SSC_GelHex", ColorControl = "SSC_GelComponents";
        // 打开时固定目标与原始外观；不追随玩家后来选中的角色。
        private readonly Pawn pawn;
        private readonly HediffWithComps owner;
        private readonly FullGelAppearance original;
        private readonly FullGelAppearanceDraft draft;
        // 色板只提供RGB预设；首项与恢复默认共用S90基色，独立透明度不会被预设覆盖。
        private static readonly List<Color> Palette = new List<Color>
        {
            new Color(FullGelAppearanceUtility.DefaultRed,FullGelAppearanceUtility.DefaultGreen,FullGelAppearanceUtility.DefaultBlue,1), new Color(.65f,.20f,.35f,1), new Color(.15f,.40f,.65f,1),
            new Color(.15f,.55f,.35f,1), new Color(.65f,.45f,.15f,1), new Color(.70f,.70f,.70f,1),
            new Color(1,0,0,1), new Color(0,1,0,1), new Color(0,0,1,1), new Color(0,0,0,1), new Color(1,1,1,1)
        };
        // 原版拖拽/延迟输入需要跨GUI事件保留状态，不能在每次重绘时重建缓冲。
        private bool dragging, hexInvalid, applyPending;
        private string previousFocus, hex, opacityBuffer;
        private string[] componentBuffers = new string[6];
        private Color componentColor;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(680f, UI.screenWidth - 32f), Mathf.Min(650f, UI.screenHeight - 32f));

        internal Dialog_FullGelAppearance(Pawn pawn, HediffWithComps owner)
        {
            // 构造仅复制草稿；强制暂停便于编辑，关闭窗口由Window处理且不提交。
            this.pawn = pawn; this.owner = owner;
            original = FullGelAppearanceUtility.Read(owner); draft = new FullGelAppearanceDraft(original);
            ResetBuffers(); optionalTitle = "SSC_GelAppearance_Title".Translate() + " — " + pawn.LabelShort;
            forcePause = true; absorbInputAroundWindow = true; doCloseX = true;
            closeOnClickedOutside = true; closeOnAccept = false;
        }

        private void ResetBuffers()
        {
            // 预设/重置后清除旧文本缓冲，防止旧的延迟输入再次覆盖刚选定的颜色。
            componentBuffers = new string[6]; componentColor = draft.Rgb;
            hex = "#" + ColorUtility.ToHtmlStringRGB(draft.Rgb); opacityBuffer = null; hexInvalid = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (TextBlock.Default())
            {
                // 每轮核对原Hediff身份；状态失效后仍能关闭窗口，但不再允许确认。
                bool active = ReferenceEquals(FullGelAppearanceUtility.GetOwner(pawn), owner);
                // 底部按钮固定在滚动区外；较小屏幕可滚动访问完整颜色控件。
                Rect viewport = new Rect(0, 0, inRect.width, Mathf.Max(1, inRect.height - 48));
                Rect content = new Rect(0, 0, Mathf.Max(1, viewport.width - 20), 470);
                Widgets.BeginScrollView(viewport, ref scroll, content);
                try
                {
                    Widgets.Label(new Rect(0, 0, content.width, 44), (!active ? "SSC_GelAppearance_NoBody" :
                        SSCMod.settings?.enableFullGelatinizedBodyTint == true ? "SSC_GelAppearance_Description" : "SSC_GelAppearance_Disabled").Translate());
                    // 色轮和RGB/HSV共享同一草稿；仅检测RGB，避免Color.a被原版重建影响不透明度。
                    Color before = draft.Rgb;
                    Widgets.HSVColorWheel(new Rect(0, 54, 150, 150), ref draft.Rgb, ref dragging);
                    // 原版数值控件负责 RGB/HSV 与编辑缓冲同步；亮度不强制固定。
                    var aggregator = new RectAggregator(new Rect(170, 54, Mathf.Max(100, content.width - 170), 0), 17508032);
                    Widgets.ColorTextfields(ref aggregator, ref draft.Rgb, ref componentBuffers, ref componentColor, previousFocus,
                        ColorControl, Widgets.ColorComponents.All, Widgets.ColorComponents.All);
                    // 颜色改变时更新HEX展示并退出“恢复默认”状态；未编辑时保留精确浮点默认值。
                    if (before.r != draft.Rgb.r || before.g != draft.Rgb.g || before.b != draft.Rgb.b)
                    {
                        draft.Edited(); hex = "#" + ColorUtility.ToHtmlStringRGB(draft.Rgb); hexInvalid = false;
                    }
                    // 预设只改草稿，随后同步所有输入缓冲，不触发角色图形刷新。
                    if (Widgets.ColorSelector(new Rect(0, 244, content.width, 54), ref draft.Rgb, Palette, out _))
                    { draft.Edited(); ResetBuffers(); }
                    // HEX接受完整六位RGB；输入错误时展示提示，保留当前有效色并禁止应用。
                    Widgets.Label(new Rect(0, 308, 100, 30), "SSC_GelAppearance_Hex".Translate());
                    GUI.SetNextControlName(HexControl);
                    string nextHex = Widgets.TextField(new Rect(105, 308, 140, 30), hex);
                    if (nextHex != hex)
                    {
                        hex = nextHex; hexInvalid = !draft.SetHex(hex);
                        if (!hexInvalid) { componentColor = draft.Rgb; componentBuffers = new string[6]; }
                    }
                    if (hexInvalid) Widgets.Label(new Rect(255, 308, content.width - 255, 30), "SSC_GelAppearance_InvalidHex".Translate());
                    // 滑块与数值框共用整数百分比。滑块变化时丢弃旧文本，避免数值框反写。
                    int oldOpacity = draft.OpacityPercent;
                    draft.OpacityPercent = (int)Widgets.HorizontalSlider(new Rect(0, 370, Mathf.Max(100, content.width - 85), 26),
                        draft.OpacityPercent, 0, 100, label: "SSC_GelAppearance_Opacity".Translate() + " " + draft.OpacityPercent + "%", roundTo: 1);
                    if (draft.OpacityPercent != oldOpacity) opacityBuffer = null;
                    Widgets.TextFieldNumeric(new Rect(content.width - 75, 360, 70, 30), ref draft.OpacityPercent, ref opacityBuffer, 0, 100);
                    if (draft.OpacityPercent != oldOpacity) draft.Edited();
                    // 样块比较基色而非透明背景混色；不透明度由上方百分比单独表达。
                    Widgets.Label(new Rect(0, 410, 100, 26), "SSC_GelAppearance_Old".Translate());
                    Widgets.DrawBoxSolid(new Rect(105, 410, 95, 26), new Color(original.Color.r, original.Color.g, original.Color.b, 1));
                    Widgets.Label(new Rect(225, 410, 100, 26), "SSC_GelAppearance_New".Translate());
                    Widgets.DrawBoxSolid(new Rect(330, 410, 95, 26), draft.Rgb);
                    Widgets.Label(new Rect(0, 444, content.width, 26), "SSC_GelAppearance_SkinOnly".Translate());
                }
                finally { Widgets.EndScrollView(); }

                // 先让延迟输入框失焦，在下一次 GUI 事件读到其提交值后再应用。
                if (applyPending)
                {
                    applyPending = false;
                    if (active && !hexInvalid && FullGelAppearanceUtility.Apply(pawn, owner, draft.Value, draft.ResetRequested)) { Close(); return; }
                }
                // 恢复默认也先保留为草稿；取消/右上角关闭/Esc均不调用Apply。
                float buttonWidth = (inRect.width - 20) / 3;
                float y = inRect.height - 38;
                if (Widgets.ButtonText(new Rect(0, y, buttonWidth, 38), "SSC_GelAppearance_Reset".Translate()))
                { GUI.FocusControl(null); draft.Reset(); ResetBuffers(); }
                if (Widgets.ButtonText(new Rect(buttonWidth + 10, y, buttonWidth, 38), "Cancel".Translate())) Close();
                // 局部禁用确认并在finally恢复GUI状态，不影响其他窗口的按钮可用性。
                bool enabled = GUI.enabled;
                try
                {
                    GUI.enabled = enabled && active && !hexInvalid;
                    if (Widgets.ButtonText(new Rect(2 * buttonWidth + 20, y, buttonWidth, 38), "Accept".Translate()))
                    { GUI.FocusControl(null); applyPending = true; }
                }
                finally { GUI.enabled = enabled; }
                // 原版ColorTextfields以焦点变化提交文本，记录Layout焦点供下一轮识别失焦。
                if (Event.current.type == EventType.layout) previousFocus = GUI.GetNameOfFocusedControl();
            }
        }
    }
}
