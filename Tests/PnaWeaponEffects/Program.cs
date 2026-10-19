// AI 辅助生成：直接运行生产视觉工具及 Verb 子类；原版攻击与渲染保留为边界。
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class Program
{
    private sealed class TestVerb:Verb_MeleeAttackSSCPna { internal bool Cast()=>TryCastShot(); }
    private sealed class Fixture
    {
        internal readonly Map Map=new Map();
        internal readonly Pawn Caster=new Pawn{DrawPos=new Vector3(10,0,10)};
        internal readonly Pawn Target=new Pawn{DrawPos=new Vector3(11,0,10)};
        internal readonly TestVerb Verb=new TestVerb();
        internal Fixture()
        {
            Scribe.mode=LoadSaveMode.Inactive;Find.CurrentMap=Map;Caster.Map=Map;Target.Map=Map;
            Verb.CasterPawn=Caster;Verb.SetTarget(Target);Verb.NativeCast=()=>true;
            SSCPnaWeaponEffectDefOf.SSC_Mote_PnaStabSpindle=new ThingDef{thingClass=typeof(Mote_SSCPnaStab)};
            SSCPnaWeaponEffectDefOf.SSC_PnaAbsorptionHeart=new FleckDef();
        }
    }
    private static int passed,failed;
    private static void Run(string name,Action test)
    {try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Near(double a,double b)=>Check(Math.Abs(a-b)<.0001,$"{a} != {b}");
    private static int Main(string[] args)
    {
        foreach(bool hit in new[]{true,false})
            Run("出手与刺空均显示且原返回值保持 hit="+hit,()=>
            {
                var f=new Fixture();f.Verb.NativeCast=()=>hit;
                Check(f.Verb.Cast()==hit,"保留返回值");Near(1,f.Verb.BaseCalls);
                Check(f.Map.Motes.Single().def==SSCPnaWeaponEffectDefOf.SSC_Mote_PnaStabSpindle,"仅一个刺芒 Mote");
                Near(0,f.Map.flecks.Sent.Count);
            });
        foreach(string gate in new[]{"busy","despawned","target-map","offscreen","other-map","no-def","saving"})
            Run("拒绝或不可见攻击不显示 "+gate,()=>
            {
                var f=new Fixture();
                switch(gate)
                {
                    case "busy":f.Caster.stances.FullBodyBusy=true;break;
                    case "despawned":f.Caster.Spawned=false;break;
                    case "target-map":f.Target.Map=new Map();break;
                    case "offscreen":f.Map.Visible=false;break;
                    case "other-map":Find.CurrentMap=new Map();break;
                    case "no-def":SSCPnaWeaponEffectDefOf.SSC_Mote_PnaStabSpindle=null;break;
                    case "saving":Scribe.mode=LoadSaveMode.Saving;break;
                }
                f.Verb.NativeCast=()=>false;Check(!f.Verb.Cast(),"返回原值");Near(1,f.Verb.BaseCalls);Near(0,f.Map.Motes.Count);
            });
        Run("命中后目标离图仍使用出手快照",()=>
        {
            var f=new Fixture();f.Verb.NativeCast=()=>{f.Target.Spawned=false;f.Target.Map=null;f.Target.DrawPos=new Vector3(90,0,90);return true;};
            f.Verb.Cast();var data=f.Map.Motes.Single();data.TickAt(.1f);
            Near(11,data.exactPosition.x);Near(10,data.exactPosition.z);
        });
        Run("原攻击异常不被吞掉且不提前生成攻击纹",()=>
        {
            var f=new Fixture();f.Verb.NativeCast=()=>throw new InvalidOperationException();
            bool threw=false;try{f.Verb.Cast();}catch(InvalidOperationException){threw=true;}
            Check(threw,"原异常保持");Near(1,f.Verb.BaseCalls);Near(0,f.Map.Motes.Count);
        });
        foreach(var row in new[]{(x:1f,z:0f,angle:90f),(x:-1f,z:0f,angle:-90f),(x:0f,z:1f,angle:0f),(x:0f,z:-1f,angle:180f),(x:1f,z:1f,angle:45f)})
            Run("刺击纹跟随出手方向 "+row,()=>
            {
                var f=new Fixture();f.Target.DrawPos=new Vector3(10+row.x,0,10+row.z);f.Verb.Cast();
                var data=f.Map.Motes.Single();Near(row.angle,data.exactRotation);
                double distance=Math.Sqrt(row.x*row.x+row.z*row.z),dx=row.x/distance,dz=row.z/distance;
                double tailX=10+dx*.25,tailZ=10+dz*.25;
                float initial=data.linearScale.z;data.TickAt(.05f);float mid=data.linearScale.z;
                data.TickAt(.1f);float full=data.linearScale.z;
                Check(initial<mid && mid<full,"向前伸展，不是静止亮斑");Near(Math.Min(1.6,distance*1.5),full);
                float width=data.linearScale.x;
                foreach(float age in new[]{.1f,.25f,.41f})
                {
                    data.TickAt(age);Near(tailX,data.exactPosition.x-dx*data.linearScale.z*.5);
                    Near(tailZ,data.exactPosition.z-dz*data.linearScale.z*.5);
                }
                Check(data.linearScale.x<width*.05f,"后期明显收细");
                Near(1,f.Map.Motes.Count);
            });
        foreach(float distance in new[]{0f,8f,float.NaN,float.PositiveInfinity})
            Run("异常或远距离不拉出长攻击纹 "+distance,()=>
            {var f=new Fixture();f.Target.DrawPos=new Vector3(10+distance,0,10);f.Verb.Cast();Near(0,f.Map.Motes.Count);});
        Run("刺芒遵守原版Mote饱和边界",()=>
        {var f=new Fixture();f.Map.Saturated=true;Check(f.Verb.Cast(),"原攻击继续");Near(0,f.Map.Motes.Count);});
        Run("刺芒更新完整调用原版时间边界并尊重销毁",()=>
        {
            var f=new Fixture();f.Verb.Cast();var data=f.Map.Motes.Single();
            data.TickAt(.1f);Near(1,data.BaseIntervals);var before=data.linearScale;
            data.TickAt(.43f);Check(data.Destroyed,"原版销毁结果保持");Near(2,data.BaseIntervals);
            Near(before.x,data.linearScale.x);Near(before.z,data.linearScale.z);
        });
        Run("受药倒置单心尺寸位置及减慢上浮速度",()=>
        {
            var f=new Fixture();SSCPnaWeaponEffectsUtility.ShowAbsorption(f.Target);
            var data=f.Map.flecks.Sent.Single();Check(data.def==SSCPnaWeaponEffectDefOf.SSC_PnaAbsorptionHeart,"独立心形定义");
            Near(.36,data.scale);Near(11,data.spawnPosition.x);Near(10.6,data.spawnPosition.z);
            Near(.22,data.velocity.Value.z);Near(0,data.velocity.Value.x);Near(180,data.rotation);Near(-1,data.ageTicksOverride);
            // 解码 Core Heart 的中心 RGB 约为 (205,72,104)/255，乘色后必须为紫色而不是粉红。
            var tint=data.instanceColor.Value;double r=205d/255*tint.r,g=72d/255*tint.g,b=104d/255*tint.b;
            Check(b>r && r>g*2,"补偿原图后的主色为紫色");
        });
        foreach(string gate in new[]{"null","invalid","master","despawned","no-map","offscreen","other-map","no-def","saving","nan"})
            Run("受药不可显示边界 "+gate,()=>
            {
                var f=new Fixture();Pawn p=f.Target;
                switch(gate)
                {
                    case "null":p=null;break;
                    case "invalid":p.Valid=false;break;
                    case "master":p.Master=true;break;
                    case "despawned":p.Spawned=false;break;
                    case "no-map":p.Map=null;break;
                    case "offscreen":f.Map.Visible=false;break;
                    case "other-map":Find.CurrentMap=new Map();break;
                    case "no-def":SSCPnaWeaponEffectDefOf.SSC_PnaAbsorptionHeart=null;break;
                    case "saving":Scribe.mode=LoadSaveMode.Saving;break;
                    case "nan":p.DrawPos=new Vector3(float.NaN,0,10);break;
                }
                SSCPnaWeaponEffectsUtility.ShowAbsorption(p);Near(0,f.Map.flecks.Sent.Count);
            });
        Run("受药独立调用不按tick吞掉反馈且不改身体位置",()=>
        {
            var f=new Fixture();SSCPnaWeaponEffectsUtility.ShowAbsorption(f.Target);SSCPnaWeaponEffectsUtility.ShowAbsorption(f.Target);
            Near(2,f.Map.flecks.Sent.Count);Near(11,f.Target.DrawPos.x);Near(10,f.Target.DrawPos.z);Check(f.Target.Valid && !f.Target.Master,"状态未改变");
        });
        string root=Path.GetFullPath(args.Length>0?args[0]:".");
        Run("效果定义使用原版普通材质及短生命周期",()=>
        {
            var defs=XDocument.Load(Path.Combine(root,"1.7/Defs/EffecterDefs/SSC_PnaWeaponEffects.xml")).Root.Elements().ToArray();
            Near(2,defs.Length);
            foreach(var def in defs)
            {
                Check(def.Element("graphicData").Element("shaderType").Value=="Mote","普通材质");
                var timing=def.Element("mote")??def;
                Check(timing.Element("realTime").Value=="false","游戏时间");
                Check(!def.Descendants().Any(x=>x.Name.LocalName.Contains("Sound") || x.Name.LocalName.Contains("glow")),"不增加声音或发光");
            }
            double Life(XElement d)=>new[]{"fadeInTime","solidTime","fadeOutTime"}.Sum(n=>(double)d.Element(n));
            Near(Mote_SSCPnaStab.Lifetime,Life(defs[0].Element("mote")));Near(1.2,Life(defs[1]));
            Check((string)defs[0].Attribute("ParentName")=="MoteBase","继承原版非存档Mote生命周期");
            Check(defs[0].Element("thingClass").Value==typeof(Mote_SSCPnaStab).FullName,"专用动画类型");
            Check(defs[0].Element("graphicData").Element("texPath").Value=="Things/Mote/SSC_PnaStabSpindle","专用尖梭纹理");
            Check(File.Exists(Path.Combine(root,"1.7/Textures/Things/Mote/SSC_PnaStabSpindle.png")),"纹理入库");
            Check(defs[0].Element("altitudeLayer").Value=="MetaOverlays","刺击纹处于角色和武器上方");
            Check((string)defs[1].Attribute("ParentName")=="FleckBase_Thrown","使用原版移动回收系统");
            Check(defs[1].Element("graphicData").Element("texPath").Value=="Things/Mote/Heart","引用原版心形资源");
        });
        Run("仅携液刺击连接专用verb且普通柄击不变",()=>
        {
            var maneuver=XDocument.Load(Path.Combine(root,"1.7/Defs/ManeuverDefs/SSC_PnaPrimitiveManeuvers.xml"));
            Check(maneuver.Descendants("verbClass").Single().Value=="SexSlaveCraft.Verb_MeleeAttackSSCPna","专用动作连接");
            var staff=XDocument.Load(Path.Combine(root,"1.7/Defs/ThingsDefs/SSC_PnaPrimitiveWeapons.xml")).Root.Elements()
                .Single(x=>(string)x.Element("defName")=="SSC_Weapon_PnaSpikeStaff");
            Check(staff.Descendants("capacities").Elements("li").Any(x=>x.Value=="Blunt"),"保留普通柄击");
            Check(!staff.Descendants("impactFleck").Any() && !maneuver.Descendants("impactFleck").Any(),"没有追加接触反馈");
        });
        Console.WriteLine($"结果：{passed}/{passed+failed} 项通过。");return failed==0?0:1;
    }
}
