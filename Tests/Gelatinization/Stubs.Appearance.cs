// Only engine/UI/serialization boundaries are modeled; production dialog, draft and comp are linked.
using System;
using System.Collections.Generic;
using System.Globalization;
namespace UnityEngine
{
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
        public float yMax {get=>y+height;set=>height=value-y;}
        public float xMax=>x+width;
    }
    public enum EventType { layout, repaint, mouseUp }
    public class Event { public static Event current = new Event(); public EventType type; }
    public static class GUI
    {
        public static Color color=new Color(1,1,1,1); public static bool enabled=true; private static string focus="";
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
    public static class Text
    {
        public static float LineHeight=24;
        public static float CalcHeight(string value,float width)=>LineHeight*(float)Math.Max(1,Math.Ceiling(value.Length*7d/Math.Max(1,width)));
    }
    public static class TooltipHandler { public static int Tips; public static void TipRegion(UnityEngine.Rect rect,string text){Tips++;} }
    public struct RectAggregator { public UnityEngine.Rect Rect; public RectAggregator(UnityEngine.Rect rect,int hash) {Rect=rect;} }
    public static class Widgets
    {
        // 脚本化输入每项只消费一次；生产DoWindowContents决定草稿、确认与取消的实际顺序。
        [Flags] public enum ColorComponents { Red=1,Green=2,Blue=4,Hue=8,Sat=16,Value=32,None=0,All=63 }
        public static int Wheels,Fields,Selectors,Sliders;
        public static UnityEngine.Color? Rgb;
        public static float? Slider, Brightness;
        public static bool Toggle, IgnoreButtonDisabled, ThrowCheckbox;
        public static float FieldHeight=102;
        public static ColorComponents Visible;
        public static UnityEngine.Rect WheelRect, FieldRect, PaletteRect, ContentRect, HexRect;
        public static readonly List<(string Kind,UnityEngine.Rect Rect,UnityEngine.Color Color,bool Disabled)> Drawn=new();
        public static string Hex;
        public static readonly HashSet<string> Buttons=new HashSet<string>();
        public static void BeginScrollView(UnityEngine.Rect viewport,ref UnityEngine.Vector2 scroll,UnityEngine.Rect content) {ContentRect=content;}
        public static void EndScrollView() { }
        public static void Label(UnityEngine.Rect rect,string text) {Drawn.Add((text,rect,UnityEngine.GUI.color,false));}
        public static void DrawBoxSolid(UnityEngine.Rect rect,UnityEngine.Color color) {Drawn.Add(("Swatch",rect,color,false));}
        public static bool ButtonText(UnityEngine.Rect rect,string text,bool drawBackground=true,bool doMouseoverSound=true,bool active=true)
        {Drawn.Add((text,rect,UnityEngine.GUI.color,!active));return (IgnoreButtonDisabled || UnityEngine.GUI.enabled&&active)&&Buttons.Remove(text);}
        // 模拟本机原版只依赖disabled阻止拖动切换，故意不读取GUI.enabled。
        public static void CheckboxLabeled(UnityEngine.Rect rect,string label,ref bool value,bool disabled=false)
        {Drawn.Add((label,rect,UnityEngine.GUI.color,disabled));if(ThrowCheckbox)throw new InvalidOperationException("UI boundary");if(Toggle&&!disabled){value=!value;Toggle=false;}}
        public static void HSVColorWheel(UnityEngine.Rect rect,ref UnityEngine.Color color,ref bool dragging,float? value=null,string name=null)
        {Wheels++;WheelRect=rect;if(Rgb.HasValue){color=Rgb.Value;Rgb=null;}}
        public static bool ColorTextfields(ref RectAggregator aggregator,ref UnityEngine.Color color,ref string[] buffers,ref UnityEngine.Color cb,string previous,string name,ColorComponents editable,ColorComponents visible){Fields++;Visible=visible;aggregator.Rect.height=FieldHeight;FieldRect=aggregator.Rect;return false;}
        public static bool ColorSelector(UnityEngine.Rect rect,ref UnityEngine.Color color,List<UnityEngine.Color> colors,out float height,UnityEngine.Texture icon=null,int colorSize=22,int colorPadding=2,Action<UnityEngine.Color,UnityEngine.Rect> extraOnGUI=null)
        {Selectors++;int cols=Math.Max(1,(int)((rect.width+colorPadding)/(colorSize+colorPadding*3)));height=(float)Math.Ceiling((double)colors.Count/cols)*(colorSize+colorPadding*3)-colorPadding;PaletteRect=new UnityEngine.Rect(rect.x,rect.y,rect.width,height);return false;}
        public static string TextField(UnityEngine.Rect rect,string text){HexRect=rect;if(Hex==null)return text;string next=Hex;Hex=null;return next;}
        public static float HorizontalSlider(UnityEngine.Rect rect,float value,float min,float max,bool middleAlignment=false,string label=null,string leftAlignedLabel=null,string rightAlignedLabel=null,float roundTo=-1)
        {Sliders++;if(max==1){if(!Brightness.HasValue)return value;float b=Brightness.Value;Brightness=null;return b;}if(!Slider.HasValue)return value;float next=Slider.Value;Slider=null;return next;}
        public static void TextFieldNumeric<T>(UnityEngine.Rect rect,ref T value,ref string buffer,float min=0,float max=1e9f) where T:struct { }
    }
}
namespace SexSlaveCraft
{
    public static class SSCDefOf { public static readonly Verse.HediffDef SSC_FullGelatinizedBody=new Verse.HediffDef("SSC_FullGelatinizedBody"); }
}

namespace SexSlaveCraft
{
    // 只替代ITab容器边界，生产区块负责状态、文字测量、按钮及提交路径。
    public partial class ITab_SexSlaveTraining
    {
        private const float SectionPadding=12, SectionTitleHeight=28, IdentityListingSpacing=2;
        internal static void DrawTrainer(Verse.Pawn p,bool qualified)
        {
            bool slave=p.Training.pawnIdentity==PawnIdentity.Slave;
            DrawTrainerIdentityToggle(new Verse.Listing_Standard(),p,new TrainerIdentityView
            {IsSlave=slave,Enabled=slave?p.Training.slaveTrainerEnabled:p.Training.pawnIdentity==PawnIdentity.Master,
             Qualified=qualified,Label="trainer",Tooltip="reason",ToggleHeight=30,SectionHeight=100,
             MasterQualificationLabel="qualification",MasterQualificationTooltip="reason",MasterQualificationHeight=24});
        }
        private static void DrawSection(UnityEngine.Rect rect,string title,Action<UnityEngine.Rect> draw)
        {draw(new UnityEngine.Rect(rect.x+12,rect.y+40,rect.width-24,rect.height-52));}
        internal static float GelHeight(Verse.Pawn pawn,float width)=>BuildGelAppearanceView(pawn,width).Height;
        internal static void DrawGel(Verse.Pawn pawn,float width)
        {var view=BuildGelAppearanceView(pawn,width);if(view.Height>0)DrawGelAppearanceSection(new UnityEngine.Rect(0,0,width,view.Height),view);}
        internal static Action CaptureGel(Verse.Pawn pawn,float width)
        {var view=BuildGelAppearanceView(pawn,width);return ()=>DrawGelAppearanceSection(new UnityEngine.Rect(0,0,width,view.Height),view);}
    }
}

namespace Verse
{
    public partial class Pawn
    {
        public SexSlaveCraft.CompSexSlaveTraining Training=new();
        public bool TrainerQualified;
        public T TryGetComp<T>() where T:class=>Training as T;
    }
    public class Listing_Standard
    {
        public UnityEngine.Rect GetRect(float height)=>new UnityEngine.Rect(0,0,280,height);
        public void Gap(float height){}
    }
}
namespace SexSlaveCraft
{
    // 资格/人格组件是引擎边界；任职setter与UI提交函数均使用生产源码。
    public enum PawnIdentity {Unset,Slave,Master}
    public class CompSexSlaveTraining
    {public PawnIdentity pawnIdentity;public bool slaveTrainerEnabled,trainerIdentityInitialized;}
    public static class TrainerSpecializationUtility
    {public static bool HasTrainerQualification(Verse.Pawn pawn,out int failure){failure=0;return pawn.TrainerQualified;}}
}
