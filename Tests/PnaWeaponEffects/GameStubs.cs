// AI 辅助生成：真实 Harmony 执行补丁；仅提供回位和绘制边界，不模拟 GPU 或伤害。
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public struct Matrix4x4 {public float m00,m02,m03,m11,m13,m20,m22,m23,m33;}
 public class Texture {} public class Texture2D:Texture {public string Path;} public class Shader {}
 public class Material {public string name;public Texture mainTexture;public Shader shader;public Color color;public Material(){}public Material(Shader s){shader=s;}public Material(Material m){mainTexture=m.mainTexture;shader=m.shader;color=m.color;}}
 public class MaterialPropertyBlock {public Color Color;public void SetColor(int p,Color c)=>Color=c;}
 public class Mesh {public string name;public Vector3[] vertices;public Vector2[] uv;public Color[] colors;public int[] triangles;public void SetTriangles(int[] t,int i)=>triangles=t;public void RecalculateNormals(){}public void RecalculateBounds(){}}
 public static class Graphics {
  public sealed class Record {public Mesh Mesh;public Matrix4x4 Matrix;public Material Material;public int Layer;public bool Overlay;public Color Color;}
  public static readonly List<Record> Sent=new List<Record>();
  [MethodImpl(MethodImplOptions.NoInlining)] public static void DrawMesh(Mesh m,Matrix4x4 x,Material t,int l)=>Sent.Add(new Record{Mesh=m,Matrix=x,Material=t,Layer=l});
  public static void DrawMesh(Mesh m,Matrix4x4 x,Material t,int l,object c,int s,MaterialPropertyBlock p)=>Sent.Add(new Record{Mesh=m,Matrix=x,Material=t,Layer=l,Overlay=true,Color=p.Color});
 }
}
namespace Verse {
 public struct IntVec3 {public int x,z;public IntVec3(int x,int z){this.x=x;this.z=z;}public static bool operator ==(IntVec3 a,IntVec3 b)=>a.x==b.x&&a.z==b.z;public static bool operator !=(IntVec3 a,IntVec3 b)=>!(a==b);public override bool Equals(object o)=>o is IntVec3 b&&this==b;public override int GetHashCode()=>HashCode.Combine(x,z);}
 public class Thing {public bool Spawned=true,Destroyed;public Map Map,MapHeld;public Vector3 DrawPos;public IntVec3 Position;public ThingDef def;}
 public class ThingWithComps:Thing {}
 public class ThingDef {public string defName;}
 public class Pawn:Thing {public bool Valid=true,Master,Dead;public Pawn_StanceTracker stances=new Pawn_StanceTracker();public Pawn_EquipmentTracker equipment=new Pawn_EquipmentTracker();public Pawn_DrawTracker Drawer;public Pawn(){Drawer=new Pawn_DrawTracker(this);}}
 public class Pawn_StanceTracker {public bool FullBodyBusy;}
 public class Pawn_EquipmentTracker {public Thing Primary;public int RemoveCalls;[MethodImpl(MethodImplOptions.NoInlining)]public void Notify_EquipmentRemoved(ThingWithComps eq){RemoveCalls++;}}
 public class Pawn_DrawTracker {
  private readonly JitterHandler jitterer=new JitterHandler();private readonly Pawn pawn;
  public JitterHandler Handler=>jitterer;public Pawn_DrawTracker(Pawn p){pawn=p;}
  public void Notify_MeleeAttackOn(Thing t){double dx=t.DrawPos.x-pawn.DrawPos.x,dz=t.DrawPos.z-pawn.DrawPos.z;jitterer.AddOffset(t.Position!=pawn.Position?.5f:.25f,(float)(Math.Atan2(dx,dz)*180/Math.PI));}
 }
 public class JitterHandler {
  public Vector3 CurrentOffset;private float JitterDropPerTick=.018f;
  public void AddOffset(float d,float angle){double a=angle*Math.PI/180;CurrentOffset.x+=(float)Math.Sin(a)*d;CurrentOffset.z+=(float)Math.Cos(a)*d;double n=Math.Sqrt(CurrentOffset.x*CurrentOffset.x+CurrentOffset.z*CurrentOffset.z);if(n>.35){CurrentOffset.x*=(float)(.35/n);CurrentOffset.z*=(float)(.35/n);}}
  [MethodImpl(MethodImplOptions.NoInlining)] public void ProcessPostTickVisuals(int ticksPassed){double n=Math.Sqrt(CurrentOffset.x*CurrentOffset.x+CurrentOffset.z*CurrentOffset.z);float d=ticksPassed*JitterDropPerTick;if(n<=d)CurrentOffset=default;else if(n>0){CurrentOffset.x*=(float)((n-d)/n);CurrentOffset.z*=(float)((n-d)/n);}}
 }
 public static class PawnRenderUtility {
  public static Mesh NextMesh;public static Matrix4x4 NextMatrix;public static Material NextMaterial;
  [MethodImpl(MethodImplOptions.NoInlining)] public static void DrawEquipmentAiming(Thing eq,Vector3 loc,float angle){Graphics.DrawMesh(NextMesh,NextMatrix,NextMaterial,4);}
 }
 public static class MeshPool {public static Mesh plane10=new Mesh(),plane10Flip=new Mesh();}
 public static class ShaderDatabase {public static Shader MoteGlow=new Shader();public static Shader Mote=new Shader();}
 public class StaticConstructorOnStartupAttribute:Attribute {}
 public static class ContentFinder<T> where T:class {private static readonly Dictionary<string,T> Cache=new Dictionary<string,T>();public static T Get(string path){if(!Cache.TryGetValue(path,out var value))Cache[path]=value=(new Texture2D{Path=path} as T);return value;}}
 public static class ShaderPropertyIDs {public const int Color=1;}
 public static class MaterialPool {
  private static readonly Dictionary<Texture2D,Material> Cache=new Dictionary<Texture2D,Material>();
  public static Material MatFrom(Texture2D t,Shader s,Color c){if(!Cache.TryGetValue(t,out var m))Cache[t]=m=new Material{mainTexture=t,shader=s,color=c};return m;}
  public static Material MatFrom(string path,Shader s,Color c)=>MatFrom(new Texture2D{Path=path},s,c);
 }
 public struct LocalTargetInfo {public Thing Thing;}
 public class FleckDef {}
 public class Map {public bool Visible=true;public FleckManager flecks=new FleckManager();}
 public class FleckManager {public readonly List<FleckCreationData> Sent=new List<FleckCreationData>();public void CreateFleck(FleckCreationData d)=>Sent.Add(d);}
 public struct FleckCreationData {public FleckDef def;public Vector3 spawnPosition;public Vector3? exactScale,velocity;public Color? instanceColor;public float rotation,scale;public int ageTicksOverride;}
 public static class GenView {public static bool ShouldSpawnMotesAt(this Vector3 p,Map m,bool drawOffscreen=true)=>m.Visible;}
 public class TickManager {public int TicksGame;}
 public static class Find {public static Map CurrentMap;public static TickManager TickManager=new TickManager();}
 public enum LoadSaveMode {Inactive,Saving,LoadingVars,PostLoadInit}public static class Scribe {public static LoadSaveMode mode;}
}
namespace RimWorld {
 public class DefOfAttribute:Attribute {}public static class DefOfHelper {public static void EnsureInitializedInCtor(Type t){}}
 public class Verb_MeleeAttack {public Verse.Pawn CasterPawn;public Verse.Thing EquipmentSource;protected Verse.LocalTargetInfo currentTarget;public Verse.LocalTargetInfo CurrentTarget=>currentTarget;public int BaseCalls;public Func<bool> NativeCast;public void SetTarget(Verse.Thing t)=>currentTarget=new Verse.LocalTargetInfo{Thing=t};[MethodImpl(MethodImplOptions.NoInlining)] protected virtual bool TryCastShot(){BaseCalls++;return NativeCast();}}
 public class Verb_MeleeAttackDamage:Verb_MeleeAttack {}
 public static class FleckMaker {public static Verse.FleckCreationData GetDataStatic(Vector3 p,Verse.Map m,Verse.FleckDef d,float scale=1f)=>new Verse.FleckCreationData{def=d,spawnPosition=p,scale=scale,ageTicksOverride=-1};}
}
namespace SexSlaveCraft {
 internal static class SSCPnaExposureUtility {internal static bool IsValidTarget(Verse.Pawn p)=>p!=null&&p.Valid&&!p.Dead;}
 internal static class SSCIdentityUtility {internal static bool IsMaster(Verse.Pawn p)=>p.Master;}
}
