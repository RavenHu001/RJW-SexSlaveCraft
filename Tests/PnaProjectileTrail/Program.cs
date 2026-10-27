// AI 辅助生成：直接运行生产视觉组件；实际 GPU 外观及 Bullet 命中由编译／游戏验收覆盖。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class Program
{
    private sealed class TestProjectile : Projectile_SSCPnaTrail
    {
        public void Move(Vector3 destination, int delta=1)
        { NativeMove = _ => Position=destination; TickInterval(delta); }
        public void Finish() { NativeMove = _ => { Destroyed=true;Spawned=false; }; TickInterval(1); }
    }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly TestProjectile Projectile = new TestProjectile();
        internal readonly CompProperties_SSCPnaTrail Props = new CompProperties_SSCPnaTrail { fleckDef=new FleckDef() };
        internal readonly CompSSCPnaTrail Comp = new CompSSCPnaTrail();
        internal Fixture()
        {
            Find.CurrentMap=Map;Projectile.Map=Map;Projectile.Component=Comp;
            Projectile.def=new ThingDef { graphicData=new GraphicData { drawSize=new Vector2(.5f,.5f) } };
            Comp.parent=Projectile;Comp.props=Props;Comp.PostSpawnSetup(false);
        }
        internal void Seed(float x=0,float z=0) { Projectile.Move(new Vector3(x,0,z)); }
    }
    private static int passed,failed;
    private static void Run(string name, Action body)
    {
        if (!SscTestProfile.ShouldRun(name)) return; try { body();passed++;Console.WriteLine("PASS "+name); } catch(Exception e) { failed++;Console.WriteLine("FAIL "+name+": "+e.Message); } }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static void Near(double a,double b) { Check(Math.Abs(a-b)<.0001,$"{a} != {b}"); }
    private static int Main(string[] args)
    {
        Run("首次采样不从地图原点拉线",()=>{var f=new Fixture();f.Seed(30,40);Check(f.Map.flecks.Sent.Count==0,"无初始长线");});
        foreach(var row in new[]{(X:.3f,Z:0f,A:90f),(X:-.3f,Z:0f,A:-90f),(X:0f,Z:.3f,A:0f),(X:0f,Z:-.3f,A:180f),(X:.2f,Z:.2f,A:45f)})
            Run("片段方向与移动一致 "+row,()=>
            {
                var f=new Fixture();f.Seed();f.Projectile.Move(new Vector3(row.X,0,row.Z));
                var data=f.Map.flecks.Sent.Single();Near(row.A,data.rotation);
                double distance=Math.Sqrt(row.X*row.X+row.Z*row.Z);
                double offset=.25+f.Props.tailGap+f.Props.overlap/2;
                Near(row.X/2-row.X/distance*offset,data.spawnPosition.x);
                Near(row.Z/2-row.Z/distance*offset,data.spawnPosition.z);
                Near(f.Props.width,data.exactScale.Value.x);
                Near(Math.Sqrt(row.X*row.X+row.Z*row.Z)+f.Props.overlap,data.exactScale.Value.z);
                Check(data.def==f.Props.fleckDef,"复用配置");Near(.95,data.instanceColor.Value.a);
                Check(data.ageTicksOverride==-1,"保留原版初始化语义");
            });
        Run("高速弩矢分段无间隙",()=>
        {
            var f=new Fixture();f.Seed();f.Projectile.Move(new Vector3(.55f,0,0));
            Check(f.Map.flecks.Sent.Count==2,".55 格分两段");
            var a=f.Map.flecks.Sent[0];var b=f.Map.flecks.Sent[1];
            Check(b.spawnPosition.x-a.spawnPosition.x<a.exactScale.Value.z,"相邻段覆盖");
        });
        Run("原版移动完整执行一次且先于取样",()=>
        {
            var f=new Fixture();f.Seed();f.Projectile.Move(new Vector3(.3f,0,0),3);
            Check(f.Projectile.BaseCalls==2 && f.Projectile.LastDelta==3,"原 delta 原样传入一次");
            Near(-.395,f.Map.flecks.Sent.Single().spawnPosition.x);
            Check(typeof(Projectile_SSCPnaTrail).GetMethods(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                .All(m=>m.Name!="Impact" && m.Name!="Launch"),"不替换命中或发射入口");
        });
        foreach(float size in new[]{.5f,.6f,1f})
            Run("尾迹完整前缘在弹丸贴图之后 "+size,()=>
            {
                var f=new Fixture();f.Projectile.def.graphicData.drawSize=new Vector2(size,size);
                f.Seed();f.Projectile.Move(new Vector3(.55f,0,0));
                double arrowRear=.55-size/2;
                foreach(var data in f.Map.flecks.Sent)
                {
                    double trailFront=data.spawnPosition.x+data.exactScale.Value.z/2;
                    Check(trailFront<=arrowRear-f.Props.tailGap+.0001,"含软边重叠的完整前缘不覆盖箭身");
                    Near(.26,data.exactScale.Value.x);
                }
                var last=f.Map.flecks.Sent.Last();
                Near(arrowRear-f.Props.tailGap,last.spawnPosition.x+last.exactScale.Value.z/2);
                Near(.55,f.Projectile.Position.x);
            });
        foreach(float invalid in new[]{-.01f,float.NaN,float.PositiveInfinity})
            Run("非法尾端间隙只关闭视觉 "+invalid,()=>
            {var f=new Fixture();f.Props.tailGap=invalid;f.Seed();f.Projectile.Move(new Vector3(.3f,0,0));Check(f.Map.flecks.Sent.Count==0 && f.Projectile.BaseCalls==2,"移动不受影响");});
        Run("缺少图形配置仍可安全生成尾迹",()=>
        {var f=new Fixture();f.Projectile.def=null;f.Seed();f.Projectile.Move(new Vector3(.3f,0,0));Check(f.Map.flecks.Sent.Count==1,"未知尺寸按零半长降级");});
        Run("停顿重复采样不叠加粒子",()=>{var f=new Fixture();f.Seed();for(int i=0;i<30;i++)f.Comp.NotifyPosition();Check(f.Map.flecks.Sent.Count==0,"不原地堆叠");});
        Run("命中销毁后不继续喷射",()=>{var f=new Fixture();f.Seed();f.Projectile.Finish();Check(f.Map.flecks.Sent.Count==0,"无末尾幽灵粒子");Check(f.Projectile.BaseCalls==2,"先执行原版命中边界");});
        Run("离开当前地图后重新进入不跨地图连线",()=>
        {var f=new Fixture();f.Seed();Find.CurrentMap=new Map();f.Projectile.Move(new Vector3(20,0,20));Find.CurrentMap=f.Map;f.Projectile.Move(new Vector3(30,0,30));Check(f.Map.flecks.Sent.Count==0,"重建历史");});
        Run("镜头外移动不生成粒子或补画整段历史",()=>
        {var f=new Fixture();f.Seed();f.Map.Visible=false;f.Projectile.Move(new Vector3(2,0,0));f.Map.Visible=true;f.Projectile.Move(new Vector3(2.2f,0,0));Check(f.Map.flecks.Sent.Count==1,"只补可见新段");});
        Run("传送拒绝超长段后可恢复",()=>
        {var f=new Fixture();f.Seed();f.Projectile.Move(new Vector3(30,0,30));Check(f.Map.flecks.Sent.Count==0,"不画传送");f.Projectile.Move(new Vector3(30.2f,0,30));Check(f.Map.flecks.Sent.Count==1,"恢复");});
        Run("加载重新生成不恢复旧视觉历史",()=>
        {var f=new Fixture();f.Seed();f.Comp.PostSpawnSetup(true);f.Projectile.Move(new Vector3(3,0,3));Check(f.Map.flecks.Sent.Count==0,"新样本");});
        foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
            Run("拒绝非法宽度和分段配置 "+invalid,()=>
            {
                var f=new Fixture();f.Seed();f.Props.width=invalid;f.Projectile.Move(new Vector3(.3f,0,0));Check(f.Map.flecks.Sent.Count==0,"非法宽度");
                f.Props.width=.26f;f.Props.maxSegmentLength=invalid;f.Projectile.Move(new Vector3(.6f,0,0));Check(f.Map.flecks.Sent.Count==0,"非法分段");
            });
        Run("极小分段配置仍有固定预算",()=>
        {var f=new Fixture();f.Seed();f.Props.maxSegmentLength=float.Epsilon;f.Projectile.Move(new Vector3(.55f,0,0));Check(f.Map.flecks.Sent.Count==16,"最多 16 段");});
        Run("坏坐标后重新采样不污染后续",()=>
        {var f=new Fixture();f.Seed();f.Projectile.Move(new Vector3(float.NaN,0,0));f.Projectile.Move(new Vector3(2,0,0));Check(f.Map.flecks.Sent.Count==0,"恢复首样本");});
        Run("缺少粒子定义时仍执行原版移动",()=>
        {var f=new Fixture();f.Props.fleckDef=null;f.Projectile.Move(new Vector3(1,0,0));Check(f.Projectile.BaseCalls==1 && f.Map.flecks.Sent.Count==0,"只关闭视觉");});
        Run("错误挂载不抛异常",()=>{new CompSSCPnaTrail{parent=new Thing(),props=new CompProperties_SSCPnaTrail()}.NotifyPosition();});
        Console.WriteLine($"结果：{passed}/{passed+failed} 项通过。");return failed==0?0:1;
    }
}
