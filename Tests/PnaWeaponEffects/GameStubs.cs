// AI 辅助生成：隔离原版攻击与 Fleck 提交边界，不模拟 GPU、护甲概率或药效。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
    }
}
namespace Verse
{
    public class Thing
    {
        public bool Spawned=true,Destroyed;
        public Map Map;
        public Vector3 DrawPos;
    }
    public class Pawn:Thing
    {
        public bool Valid=true,Master;
        public Pawn_StanceTracker stances=new Pawn_StanceTracker();
    }
    public class Pawn_StanceTracker { public bool FullBodyBusy; }
    public struct LocalTargetInfo { public Thing Thing; }
    public class FleckDef { }
    public class ThingDef { public Type thingClass; public MoteProperties mote=new MoteProperties(); }
    public class MoteProperties { public float Lifespan=.42f; }
    public abstract class Mote:Thing
    {
        public ThingDef def;
        public Vector3 exactPosition,linearScale;
        public Color instanceColor;
        public float exactRotation,AgeSecs;
        public int BaseIntervals;
        protected virtual void TimeInterval(float deltaTime)
        {BaseIntervals++;if(AgeSecs>=def.mote.Lifespan)Destroyed=true;}
        public void TickAt(float age){AgeSecs=age;TimeInterval(1f/60f);}
    }
    public class Map
    {
        public bool Visible=true,Saturated;
        public FleckManager flecks=new FleckManager();
        public readonly List<Mote> Motes=new List<Mote>();
    }
    public class FleckManager
    {
        public readonly List<FleckCreationData> Sent=new List<FleckCreationData>();
        public void CreateFleck(FleckCreationData data)=>Sent.Add(data);
    }
    public struct FleckCreationData
    {
        public FleckDef def;
        public Vector3 spawnPosition;
        public Vector3? exactScale,velocity;
        public Color? instanceColor;
        public float rotation,scale;
        public int ageTicksOverride;
    }
    public static class GenView
    { public static bool ShouldSpawnMotesAt(this Vector3 position,Map map,bool drawOffscreen=true)=>map.Visible; }
    public static class Find { public static Map CurrentMap; }
    public enum LoadSaveMode { Inactive,Saving,LoadingVars,PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
}
namespace RimWorld
{
    public class DefOfAttribute:Attribute { }
    public static class DefOfHelper { public static void EnsureInitializedInCtor(Type type){ } }
    public class Verb_MeleeAttackDamage
    {
        public Verse.Pawn CasterPawn;
        protected Verse.LocalTargetInfo currentTarget;
        public int BaseCalls;
        public Func<bool> NativeCast;
        public void SetTarget(Verse.Thing target)=>currentTarget=new Verse.LocalTargetInfo{Thing=target};
        protected virtual bool TryCastShot(){BaseCalls++;return NativeCast();}
    }
    public static class FleckMaker
    {
        public static Verse.FleckCreationData GetDataStatic(Vector3 position,Verse.Map map,Verse.FleckDef def,float scale=1f)
            =>new Verse.FleckCreationData{def=def,spawnPosition=position,scale=scale,ageTicksOverride=-1};
    }
    public static class MoteMaker
    {
        public static Verse.Mote MakeStaticMote(Vector3 position,Verse.Map map,Verse.ThingDef def,float scale=1f,
            bool makeOffscreen=false,float exactRot=0f)
        {
            if(map.Saturated)return null;
            var mote=(Verse.Mote)Activator.CreateInstance(def.thingClass);
            mote.def=def;mote.Map=map;mote.exactPosition=position;mote.exactRotation=exactRot;
            map.Motes.Add(mote);return mote;
        }
    }
}
namespace SexSlaveCraft
{
    internal static class SSCPnaExposureUtility
    { internal static bool IsValidTarget(Verse.Pawn pawn)=>pawn!=null && pawn.Valid; }
    internal static class SSCIdentityUtility
    { internal static bool IsMaster(Verse.Pawn pawn)=>pawn.Master; }
}
