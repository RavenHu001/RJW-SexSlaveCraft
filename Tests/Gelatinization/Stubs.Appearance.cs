// Only engine/UI/serialization boundaries are modeled; production dialog, draft and comp are linked.
using System;
using System.Collections.Generic;
using System.Globalization;
namespace UnityEngine
{
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Rect { public float x,y,width,height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;} }
    public enum EventType { layout, repaint, mouseUp }
    public class Event { public static Event current = new Event(); public EventType type; }
    public static class GUI
    {
        public static bool enabled=true; private static string focus="";
        public static void SetNextControlName(string value) { }
        public static void FocusControl(string value) { focus=value??""; }
        public static string GetNameOfFocusedControl()=>focus;
    }
    public static class ColorUtility
    {
        // 这里只模拟六位RGB解析边界；真实1.7实现及控件签名另由实际引用构建核对。
        public static string ToHtmlStringRGB(Color c)=>$"{(byte)Math.Round(c.r*255):X2}{(byte)Math.Round(c.g*255):X2}{(byte)Math.Round(c.b*255):X2}";
        public static bool TryParseHtmlString(string s,out Color c)
        {
            c=default;
            if(s==null||s.Length!=7||s[0]!='#'||!uint.TryParse(s.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint v))return false;
            c=new Color((v>>16)/255f,((v>>8)&255)/255f,(v&255)/255f,1);return true;
        }
    }
}
namespace RimWorld
{
    public static class PawnsFinder { public static List<Verse.Pawn> AllMapsWorldAndTemporary_AliveOrDead=new List<Verse.Pawn>(); }
    public static class PortraitsCache { public static int Dirty; public static void SetDirty(Verse.Pawn pawn){Dirty++;} }
}
namespace Verse
{
    public static class BaseContent { public static readonly UnityEngine.Texture WhiteTex=new UnityEngine.Texture("White"); }
    public class Pawn_DrawTracker { public PawnRenderer renderer=new PawnRenderer(); }
    public class PawnRenderer { public int Dirty; public void SetAllGraphicsDirty(){Dirty++;} }
    public static class UI { public static float screenWidth=1280,screenHeight=720; }
    public class Window
    {
        // 窗口关闭只记状态，避免模型误把Close当成自动提交生产外观数据。
        public string optionalTitle; public bool forcePause,absorbInputAroundWindow,doCloseX,closeOnClickedOutside,closeOnAccept,Closed;
        public virtual UnityEngine.Vector2 InitialSize=>default;
        public virtual void DoWindowContents(UnityEngine.Rect rect) { }
        public void Close(){Closed=true;}
    }
    public class WindowStack { public List<Window> Windows=new List<Window>(); public void Add(Window window){Windows.Add(window);} }
    public static class Find { public static WindowStack WindowStack=new WindowStack(); }
    public sealed class TextBlock : IDisposable { public static TextBlock Default()=>new TextBlock(); public void Dispose() { } }
    public struct RectAggregator { public RectAggregator(UnityEngine.Rect rect,int hash) { } }
    public static class Widgets
    {
        // 脚本化输入每项只消费一次；生产DoWindowContents决定草稿、确认与取消的实际顺序。
        [Flags] public enum ColorComponents { Red=1,Green=2,Blue=4,Hue=8,Sat=16,Value=32,None=0,All=63 }
        public static int Wheels,Fields,Selectors,Sliders;
        public static UnityEngine.Color? Rgb;
        public static float? Slider;
        public static string Hex;
        public static readonly HashSet<string> Buttons=new HashSet<string>();
        public static void BeginScrollView(UnityEngine.Rect viewport,ref UnityEngine.Vector2 scroll,UnityEngine.Rect content) { }
        public static void EndScrollView() { }
        public static void Label(UnityEngine.Rect rect,string text) { }
        public static void DrawBoxSolid(UnityEngine.Rect rect,UnityEngine.Color color) { }
        public static bool ButtonText(UnityEngine.Rect rect,string text)=>UnityEngine.GUI.enabled&&Buttons.Remove(text);
        public static void HSVColorWheel(UnityEngine.Rect rect,ref UnityEngine.Color color,ref bool dragging,float? value=null,string name=null)
        {Wheels++;if(Rgb.HasValue){color=Rgb.Value;Rgb=null;}}
        public static bool ColorTextfields(ref RectAggregator aggregator,ref UnityEngine.Color color,ref string[] buffers,ref UnityEngine.Color cb,string previous,string name,ColorComponents editable,ColorComponents visible){Fields++;return false;}
        public static bool ColorSelector(UnityEngine.Rect rect,ref UnityEngine.Color color,List<UnityEngine.Color> colors,out float height){Selectors++;height=54;return false;}
        public static string TextField(UnityEngine.Rect rect,string text){if(Hex==null)return text;string next=Hex;Hex=null;return next;}
        public static float HorizontalSlider(UnityEngine.Rect rect,float value,float min,float max,bool middleAlignment=false,string label=null,string leftAlignedLabel=null,string rightAlignedLabel=null,float roundTo=-1)
        {Sliders++;if(!Slider.HasValue)return value;float next=Slider.Value;Slider=null;return next;}
        public static void TextFieldNumeric<T>(UnityEngine.Rect rect,ref T value,ref string buffer,float min=0,float max=1e9f) where T:struct { }
    }
}
namespace SexSlaveCraft
{
    public static class SSCDefOf { public static readonly Verse.HediffDef SSC_FullGelatinizedBody=new Verse.HediffDef("SSC_FullGelatinizedBody"); }
}
