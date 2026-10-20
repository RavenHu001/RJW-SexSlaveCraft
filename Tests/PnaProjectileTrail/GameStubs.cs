// AI 辅助生成：只提供移动完成与 Fleck 输出边界；不模拟原版命中、渲染或粒子曲线。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) { this.x=x;this.y=y; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
            => new Vector3(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t, a.z+(b.z-a.z)*t);
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r,float g,float b,float a) { this.r=r;this.g=g;this.b=b;this.a=a; }
    }
}
namespace Verse
{
    public class FleckDef { }
    public class CompProperties { public Type compClass; }
    public class Thing
    {
        public ThingDef def;
        public bool Spawned=true, Destroyed;
        public Map Map;
        public ThingComp Component;
        public T TryGetComp<T>() where T:ThingComp => Component as T;
    }
    public class ThingDef { public GraphicData graphicData; }
    public class GraphicData { public Vector2 drawSize; }
    public class ThingComp
    {
        public Thing parent;
        public CompProperties props;
        public virtual void PostSpawnSetup(bool respawningAfterLoad) { }
    }
    public abstract class Projectile : Thing
    {
        public Vector3 Position;
        public Vector3 DrawPos => Position;
        protected virtual void TickInterval(int delta) { }
    }
    public class Map
    {
        public bool Visible=true;
        public FleckManager flecks = new FleckManager();
    }
    public class FleckManager
    {
        public readonly List<FleckCreationData> Sent = new List<FleckCreationData>();
        public void CreateFleck(FleckCreationData data) { Sent.Add(data); }
    }
    public struct FleckCreationData
    {
        public FleckDef def;
        public Vector3 spawnPosition;
        public Vector3? exactScale;
        public Color? instanceColor;
        public float rotation;
        public int ageTicksOverride;
    }
    public static class GenView
    {
        public static bool ShouldSpawnMotesAt(this Vector3 loc, Map map, bool drawOffscreen=true) => map.Visible;
    }
    public static class Find { public static Map CurrentMap; }
}
namespace RimWorld
{
    public class Bullet : Verse.Projectile
    {
        public int BaseCalls, LastDelta;
        public Action<int> NativeMove;
        protected override void TickInterval(int delta)
        { BaseCalls++;LastDelta=delta;NativeMove?.Invoke(delta); }
    }
    public static class FleckMaker
    {
        public static Verse.FleckCreationData GetDataStatic(Vector3 loc, Verse.Map map, Verse.FleckDef def, float scale=1)
            => new Verse.FleckCreationData { def=def,spawnPosition=loc,ageTicksOverride=-1 };
    }
}
