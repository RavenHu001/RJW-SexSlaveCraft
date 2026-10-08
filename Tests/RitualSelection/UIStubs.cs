using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Drawing records prove scoped hooks/order/state restoration, not Unity rasterization or font metrics.
internal static class UiRecorder
{
    internal sealed class Mark { public string Kind, Text; public Rect Rect; public Color Color; }
    public static readonly List<Mark> Marks = new List<Mark>();
    public static void Add(string kind, Rect rect, string text = null)
        => Marks.Add(new Mark { Kind = kind, Rect = rect, Text = text, Color = GUI.color });
    public static void Reset()
    {
        Marks.Clear(); Time.realtimeSinceStartup = 0f; Event.current.mousePosition = new Vector2(-1000f, -1000f);
        GUI.color = Color.white; Verse.Text.Font = Verse.GameFont.Small; Verse.Text.Anchor = TextAnchor.UpperLeft;
        Verse.Find.WindowStack.Window = null;
    }
}

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1f, 1f, 1f);
    }
    public enum TextAnchor { UpperLeft, MiddleCenter }
    public class Texture2D { }
    public static class Time { public static float realtimeSinceStartup; }
    public sealed class Event { public static Event current = new Event(); public Vector2 mousePosition; }
    public static class GUI
    {
        public static Color color = Color.white;
        public static void DrawTexture(Rect rect, Texture2D texture) => UiRecorder.Add("check", rect);
    }
}

namespace Verse
{
    public enum GameFont { Tiny, Small }
    public static class Mouse
    {
        public static bool IsOver(Rect rect) => Event.current.mousePosition.x >= rect.x && Event.current.mousePosition.x < rect.xMax
            && Event.current.mousePosition.y >= rect.y && Event.current.mousePosition.y < rect.yMax;
    }
    public static class Text
    {
        public static GameFont Font = GameFont.Small;
        public static TextAnchor Anchor = TextAnchor.UpperLeft;
        public static bool WordWrap = true;
        public static Vector2 CalcSize(string text) => new Vector2(text == "SSC_RitualSelection_HostBadge" || text == "SSC_RitualSelection_TargetBadge" ? 24f : (text?.Length ?? 0) * 6f, 17f);
        public static float CalcHeight(string text, float width) => Math.Max(17f, (float)Math.Ceiling(CalcSize(text).x / Math.Max(1f, width)) * 17f);
    }
    public struct TextBlock : IDisposable
    {
        private readonly GameFont oldFont;
        private readonly TextAnchor oldAnchor;
        private readonly Color oldColor;
        private readonly bool oldWordWrap;
        public TextBlock(GameFont? font = null, TextAnchor? anchor = null, Color? color = null)
            : this(font, anchor, null, color) { }
        public TextBlock(GameFont? font, TextAnchor? anchor, bool? wordWrap, Color? color)
        {
            oldFont = Text.Font; oldAnchor = Text.Anchor; oldColor = GUI.color; oldWordWrap = Text.WordWrap;
            if (font.HasValue) Text.Font = font.Value;
            if (anchor.HasValue) Text.Anchor = anchor.Value;
            if (color.HasValue) GUI.color = color.Value;
            if (wordWrap.HasValue) Text.WordWrap = wordWrap.Value;
        }
        public void Dispose() { Text.Font = oldFont; Text.Anchor = oldAnchor; GUI.color = oldColor; Text.WordWrap = oldWordWrap; }
    }
    public static class Widgets
    {
        public static readonly Texture2D CheckboxOnTex = new Texture2D();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void BeginScrollView(Rect rect, ref Vector2 position, Rect viewRect, bool showScrollbars = true) { UiRecorder.Add("begin", rect); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void EndScrollView() { UiRecorder.Add("end", default); }
        public static void DrawBoxSolidWithOutline(Rect rect, Color background, Color outline, int thickness = 1) => UiRecorder.Add("badge", rect);
        public static void DrawBox(Rect rect, int thickness = 1, Texture2D texture = null) => UiRecorder.Add("border", rect);
        public static void Label(Rect rect, string text) => UiRecorder.Add("label", rect, text);
        public static void DrawLine(Vector2 start, Vector2 end, Color color, float width) => UiRecorder.Add("slash", new Rect(start.x, start.y, end.x - start.x, end.y - start.y));
    }
    public static class TooltipHandler { public static void TipRegion(Rect rect, string text) => UiRecorder.Add("tip", rect, text); }
    public static class UI
    {
        public static int screenWidth = 1000, screenHeight = 800;
        public static Vector2 MousePositionOnUI => new Vector2(Event.current.mousePosition.x, screenHeight - Event.current.mousePosition.y);
    }
    public enum WindowLayer { Super }
    public sealed class WindowStack
    {
        public Action Window;
        public void ImmediateWindow(int id, Rect rect, WindowLayer layer, Action draw) { Window = draw; UiRecorder.Add("window", rect); }
    }
    public static class Find { public static readonly WindowStack WindowStack = new WindowStack(); }
}

namespace RimWorld
{
    public static class PawnPortraitIconsDrawer
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DrawPawnPortraitIcons(Rect portraitRect, Verse.Pawn p, bool required, bool grayedOut,
            ref float curX, ref float curY, float iconSize, bool showIdeoIcon, out bool tooltipActive)
        { tooltipActive = NativeIconHovered; UiRecorder.Add("native-icons", portraitRect); }
        public static bool NativeIconHovered;
    }
}
