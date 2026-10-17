// AI 辅助生成。只承接已核对的时钟、选择、GUI、肖像和相机边界；调度及导航编译生产代码。
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x=x; this.y=y; } public static Vector2 zero => default; }
    public struct Rect
    {
        public float x, y, width, height;
        public float xMax => x+width; public float yMax => y+height;
        public Rect(float x,float y,float width,float height) { this.x=x;this.y=y;this.width=width;this.height=height; }
        public bool Contains(Vector2 p) => p.x>=x && p.x<xMax && p.y>=y && p.y<yMax;
    }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} }
    public enum TextAnchor { UpperLeft, MiddleCenter }
    public enum EventType { layout, repaint, mouseDown, used }
    public class Event { public static Event current = new(); public EventType type; public int button; public Vector2 mousePosition; public void Use()=>type=EventType.used; }
    public static class Time { public static float realtimeSinceStartup; public static int frameCount; }
    public static class GUI { public static Color color=new(1,1,1); public static bool enabled=true; public static void DrawTexture(Rect rect, object image){} }
    public static partial class Mathf { public static float Min(float x,float y)=>Math.Min(x,y); }
}
namespace Verse
{
    public enum GameFont { Tiny, Small }
    public static class Text { public static GameFont Font; public static TextAnchor Anchor; public static bool WordWrap; }
    public static class UI { public static int screenWidth=1200,screenHeight=900; }
    public static class Mouse { public static bool IsOver(Rect rect)=>rect.Contains(Event.current.mousePosition); }
    public static class TooltipHandler { public static int Calls; public static void TipRegion(Rect rect,string text){Calls++;} }
    public static class LanguageDatabase { public static object activeLanguage = new(); }
    public static class Log { public static int Errors; public static void Error(string text){Errors++;} }
    public static class Messages { public static int Calls; public static void Message(string text,object def,bool historical){Calls++;} }
    public readonly struct Rot4 { public static Rot4 South=>default; }
    public class ThingDef { public bool selectable=true; }
    public partial class Thing { public ThingDef def=new(); public string LabelShort=>"Container"; }
    public partial class Map { public WorldObject Parent=new(); }
    public partial class Game
    {
        readonly Dictionary<Type,object> components=new();
        public T GetComponent<T>() where T:class
        { if(!components.TryGetValue(typeof(T),out var value)) components[typeof(T)]=value=Activator.CreateInstance(typeof(T),this); return value as T; }
    }
    public abstract partial class GameComponent { public virtual void GameComponentUpdate(){} }
    public static partial class Find { public static Map CurrentMap; public static RimWorld.Selector Selector=new(); public static WindowStack WindowStack=new(); }
    public enum GizmoState { Clear,Mouseover,Interacted }
    public struct GizmoRenderParms { }
    public readonly struct GizmoResult
    { public readonly GizmoState State; public readonly Event InteractEvent; public GizmoResult(GizmoState state,Event ev=null){State=state;InteractEvent=ev;} }
    public partial class Gizmo
    {
        public bool Disabled;
        public virtual float GetWidth(float max)=>max;
        public virtual bool GroupsWith(Gizmo other)=>true;
        public virtual bool InheritInteractionsFrom(Gizmo other)=>true;
        public virtual GizmoResult GizmoOnGUI(Vector2 topLeft,float max,GizmoRenderParms parms)=>default;
        public virtual void ProcessInput(Event ev){}
    }
    public abstract class Window
    {
        public string optionalTitle;
        public bool forcePause,doCloseX,doCloseButton,absorbInputAroundWindow,closeOnAccept,IsOpen;
        public virtual Vector2 InitialSize=>default;
        public virtual void PostOpen(){IsOpen=true;}
        public virtual void PostClose(){IsOpen=false;}
        public virtual void WindowUpdate(){}
        public abstract void DoWindowContents(Rect rect);
        public void Close()=>Find.WindowStack.Remove(this);
    }
    public class WindowStack
    {
        readonly List<Window> windows=new();
        public T WindowOfType<T>() where T:class=>windows.OfType<T>().FirstOrDefault();
        public void Add(Window window)
        { var old=windows.FirstOrDefault(w=>w.GetType()==window.GetType()); if(old!=null) Remove(old); windows.Add(window);window.PostOpen(); }
        public void Remove(Window window){if(windows.Remove(window))window.PostClose();}
    }
    public static class Widgets
    {
        public static int Labels, ScrollDepth; public static float? NextScroll;
        public static bool ThrowLabel; public static int ThrowAt;
        public static void DrawBoxSolid(Rect rect,Color color){}
        public static void DrawHighlight(Rect rect){}
        public static void Label(Rect rect,string value){Labels++; if(ThrowLabel||ThrowAt>0&&Labels==ThrowAt)throw new InvalidOperationException("GUI fixture");}
        public static bool ButtonText(Rect rect,string label,bool active=true)
        { if(active&&GUI.enabled&&Event.current.type==EventType.mouseDown&&Event.current.button==0&&Mouse.IsOver(rect)){Event.current.Use();return true;}return false; }
        public static void CheckboxLabeled(Rect rect,string label,ref bool value,bool disabled=false){}
        public static void BeginScrollView(Rect rect,ref Vector2 scroll,Rect content){ScrollDepth++;if(NextScroll.HasValue){scroll.y=NextScroll.Value;NextScroll=null;}}
        public static void EndScrollView(){ScrollDepth--;}
    }
    public static class UIExtensions { public static string Truncate(this string text,float width)=>text; }
    public static class CameraJumper
    {
        public static int Jumps,Selects,Checks; public static bool Allow=true; public static GlobalTargetInfo Last;
        public static bool CanJump(GlobalTargetInfo target){Checks++;return Allow&&(target.HasThing&&target.Thing.Spawned&&Find.Maps.Contains(target.Thing.Map)||target.HasWorldObject&&target.WorldObject.Spawned||target.Tile.Valid);}
        public static void TryJump(GlobalTargetInfo target){Jumps++;Last=target;}
        public static void TryJumpAndSelect(GlobalTargetInfo target){Jumps++;Selects++;Last=target;}
    }
}
namespace RimWorld
{
    public class Selector
    { public readonly List<object> Objects=new(); public int NumSelected=>Objects.Count; public Thing SingleSelectedThing=>NumSelected==1?Objects[0]as Thing:null; }
    public static class MessageTypeDefOf { public static object RejectInput=new(); }
    public static class PortraitsCache
    { public static int Calls; public static readonly List<(Pawn pawn,Vector2 size)> Requests=new(); public static object Get(Pawn pawn,Vector2 size,Rot4 rot){Calls++;Requests.Add((pawn,size));return null;} }
    public static class TargetHighlighter { public static int Calls; public static void Highlight(GlobalTargetInfo target,bool arrow,bool colonistBar){Calls++;} }
}
namespace RimWorld.Planet
{
    public partial class WorldObject { public bool SelectableNow=true; public string LabelCap="World object"; }
    public readonly struct GlobalTargetInfo
    {
        public readonly Thing Thing; public readonly WorldObject WorldObject; public readonly PlanetTile Tile;
        public bool HasThing=>Thing!=null; public bool HasWorldObject=>WorldObject!=null;
        public static GlobalTargetInfo Invalid=>default;
        public GlobalTargetInfo(Thing thing){Thing=thing;WorldObject=null;Tile=default;}
        public GlobalTargetInfo(WorldObject obj){Thing=null;WorldObject=obj;Tile=default;}
        public GlobalTargetInfo(PlanetTile tile){Thing=null;WorldObject=null;Tile=tile;}
    }
}
