// AI 辅助生成：真实 Harmony 命中入口、最终武器矩阵与半弧网格，不模拟 GPU。
using System;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class WhipHitTests
{
    private sealed class Attack : RimWorld.Verb_MeleeAttackDamage { internal bool Cast() => TryCastShot(); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Pawn Owner = new Pawn();
        internal readonly Pawn Target = new Pawn();
        internal readonly ThingWithComps Weapon = new ThingWithComps { def = new ThingDef {defName=SSCPnaWhipPoseUtility.WeaponDefName} };
        internal readonly Attack Verb = new Attack();
        internal readonly TickManager Clock = new TickManager {TicksGame=100};
        internal readonly Material Original = new Material { mainTexture = new Texture2D {Path="item"}, shader=new Shader(), color=new Color(.3f,.4f,.5f,1) };
        internal Fixture()
        {
            Find.CurrentMap=Map;Find.TickManager=Clock;Scribe.mode=LoadSaveMode.Inactive;
            Owner.Map=Target.Map=Weapon.MapHeld=Map;Owner.equipment.Primary=Weapon;
            Verb.CasterPawn=Owner;Verb.EquipmentSource=Weapon;Verb.SetTarget(Target);Verb.NativeCast=()=>true;
            PawnRenderUtility.NextMaterial=Original;PawnRenderUtility.NextMesh=MeshPool.plane10;
            PawnRenderUtility.NextMatrix=new Matrix4x4 {m00=1.3f,m02=.4f,m20=-.4f,m22=1.3f,m03=5,m23=6,m13=.02f,m33=1};
            Graphics.Sent.Clear();
        }
        internal float Alpha => SSCPnaWhipHitUtility.GetAlpha(Weapon);
        internal void Draw()=>PawnRenderUtility.DrawEquipmentAiming(Weapon,default,0);
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Near(double a,double b)=>Check(Math.Abs(a-b)<.0001,$"{a} != {b}");
    internal static void Register(Action<string,Action> run,string root)
    {
        foreach(bool hit in new[]{false,true})run("持鞭命中才叠画半弧而打空仍播放姿态 "+hit,()=>{
            var f=new Fixture();f.Verb.NativeCast=()=>hit;Check(f.Verb.Cast()==hit,"native result");Near(1,f.Verb.BaseCalls);
            Check(SSCPnaWhipPoseUtility.GetPose(f.Weapon)==SSCPnaWhipPose.Attack,"animation independent");
            f.Draw();Near(hit?2:1,Graphics.Sent.Count);Near(0,f.Map.flecks.Sent.Count);
        });
        foreach(bool flip in new[]{false,true})foreach(int angle in new[]{0,90,180,270})
            run($"宽半弧与鞭束共用四方向最终矩阵 {flip}/{angle}",()=>{
                var f=new Fixture();f.Verb.Cast();double a=angle*Math.PI/180;
                PawnRenderUtility.NextMesh=flip?MeshPool.plane10Flip:MeshPool.plane10;
                PawnRenderUtility.NextMatrix=new Matrix4x4 {m00=(float)Math.Cos(a),m20=-(float)Math.Sin(a),m02=(float)Math.Sin(a),m22=(float)Math.Cos(a),m03=7,m23=9,m13=.05f,m33=1};
                f.Draw();Near(2,Graphics.Sent.Count);var trail=Graphics.Sent[0];var whip=Graphics.Sent[1];
                Check(trail.Overlay&&!whip.Overlay,"trail first, solid whip above");
                Near(whip.Matrix.m00,trail.Matrix.m00);Near(whip.Matrix.m02,trail.Matrix.m02);
                Near(whip.Matrix.m20,trail.Matrix.m20);Near(whip.Matrix.m22,trail.Matrix.m22);
                Near(whip.Matrix.m03,trail.Matrix.m03);Near(whip.Matrix.m23,trail.Matrix.m23);
                float grip=(50f/256f-.5f)*(flip?-1:1);
                Near(7,whip.Matrix.m03+whip.Matrix.m00*grip);
                Near(9,whip.Matrix.m23+whip.Matrix.m20*grip);
                Near(whip.Matrix.m13-.0005,trail.Matrix.m13);Near(whip.Layer,trail.Layer);
                Check(trail.Material.shader==ShaderDatabase.Mote,"normal alpha shader");
                Check(((Texture2D)trail.Material.mainTexture).Path=="Things/Mote/BrightFlash","soft edge source");
                Check(!ReferenceEquals(trail.Material,f.Original),"private material");Near(.3,f.Original.color.r);
            });
        foreach(var row in new[]{(0,.65),(1,.825),(2,1d),(5,1d),(6,1d),(12,.5),(17,1d/12),(18,0d)})
            run("残影亮度和收回同步 "+row.Item1,()=>{var f=new Fixture();f.Verb.Cast();f.Clock.TicksGame+=row.Item1;Near(row.Item2,f.Alpha);});
        run("暂停重复绘制不推进且批量游戏tick正常淡出",()=>{
            var f=new Fixture();f.Verb.Cast();for(int i=0;i<8;i++)Near(.65,f.Alpha);
            f.Clock.TicksGame+=12;Near(.5,f.Alpha);f.Clock.TicksGame+=6;Near(0,f.Alpha);
        });
        foreach(string gate in new[]{"save","other-map","unequip","destroyed","despawned","dead","other-weapon","no-clock","clock-replaced","clock-backward","busy"})
            run("半弧复用姿态失效清理 "+gate,()=>{
                var f=new Fixture();if(gate=="busy")f.Owner.stances.FullBodyBusy=true;f.Verb.Cast();
                switch(gate){case "save":Scribe.mode=LoadSaveMode.Saving;break;case "other-map":Find.CurrentMap=new Map();break;
                    case "unequip":f.Owner.equipment.Primary=null;break;case "destroyed":f.Weapon.Destroyed=true;break;
                    case "despawned":f.Owner.Spawned=false;break;case "dead":f.Owner.Dead=true;break;
                    case "other-weapon":f.Weapon.def.defName="Other";break;case "no-clock":Find.TickManager=null;break;
                    case "clock-replaced":Find.TickManager=new TickManager{TicksGame=100};break;case "clock-backward":f.Clock.TicksGame=99;break;}
                Near(0,f.Alpha);f.Draw();Check(Graphics.Sent.All(r=>!r.Overlay),"no leftover trail");
            });
        run("新一次打空清除旧命中残影",()=>{var f=new Fixture();f.Verb.Cast();Check(f.Alpha>0,"hit");f.Verb.NativeCast=()=>false;f.Verb.Cast();Near(0,f.Alpha);});
        run("受击目标销毁后残影仍绑定鞭子而非目标位置",()=>{
            var f=new Fixture();f.Verb.NativeCast=()=>{f.Target.Destroyed=true;f.Target.Spawned=false;f.Target.Map=null;return true;};
            f.Verb.Cast();f.Owner.DrawPos=new Vector3(100,0,100);Check(f.Alpha>0,"held swing remains");
            f.Draw();Near(2,Graphics.Sent.Count);Near(0,f.Map.flecks.Sent.Count);
        });
        run("药效免疫及普通柄击仍有接触残影",()=>{
            var f=new Fixture();f.Target.Master=true;f.Target.Valid=false;f.Verb.EquipmentSource=null;
            f.Verb.Cast();f.Draw();Near(2,Graphics.Sent.Count);Near(0,f.Map.flecks.Sent.Count);
        });
        run("父类异常不开始姿态或残影",()=>{
            var f=new Fixture();f.Verb.NativeCast=()=>throw new InvalidOperationException();bool threw=false;
            try{f.Verb.Cast();}catch(InvalidOperationException){threw=true;}Check(threw,"exception kept");Near(0,f.Alpha);
        });
        run("未知网格保持原武器且不猜测残影方向",()=>{var f=new Fixture();f.Verb.Cast();PawnRenderUtility.NextMesh=new Mesh();f.Draw();Near(1,Graphics.Sent.Count);});
        run("不同武器不串亮度且私有材质底色不变",()=>{
            var a=new Fixture();a.Verb.Cast();a.Clock.TicksGame+=2;a.Draw();var bright=Graphics.Sent[0];Near(.95,bright.Color.a);
            Near(1,bright.Color.r);Near(.4,bright.Color.g);Near(1,bright.Color.b);
            a.Clock.TicksGame+=10;a.Draw();Near(.475,Graphics.Sent[2].Color.a);Near(.95,bright.Color.a);Near(1,bright.Material.color.a);
            var b=new Fixture();Near(0,b.Alpha);
        });
        foreach(bool flip in new[]{false,true})run("半弧网格宽度端部柔边镜像与三角朝上 "+flip,()=>{
            var mesh=SSCPnaWhipHitUtility.CreateMesh(flip);
            Near(185,mesh.vertices.Length);Near(864,mesh.triangles.Length);
            var normal=SSCPnaWhipHitUtility.CreateMesh(false);
            for(int i=0;i<mesh.vertices.Length;i++){
                var v=mesh.vertices[i];Near(flip?-normal.vertices[i].x:normal.vertices[i].x,v.x);Near(normal.vertices[i].z,v.z);
                double x=(flip?-v.x:v.x)-SSCPnaWhipHitUtility.PivotX;
                double radius=Math.Sqrt(x*x+v.z*v.z);Check(radius>=.1299&&radius<=.7301,"wider radial band");
                if(i%5==0||i%5==4)Near(0,mesh.colors[i].a);
            }
            for(int i=0;i<mesh.triangles.Length;i+=3){
                var a=mesh.vertices[mesh.triangles[i]];var b=mesh.vertices[mesh.triangles[i+1]];var c=mesh.vertices[mesh.triangles[i+2]];
                Check((b.z-a.z)*(c.x-a.x)-(b.x-a.x)*(c.z-a.z)>0,"upward face");
            }
            // 0° 位于第31段：其径向范围穿过真实挥击态鞭束的中后段，而非画在目标旁边。
            int zero=31*5;Near(0,mesh.vertices[zero].z);Near(flip?.03796875:-.03796875,mesh.vertices[zero].x);
            Check(mesh.colors[zero+2].a>.65f,"brighter overlap region");
            // 参数沿弧长从上向下推进：起点在上，末端在下，绝大多数弧片留在上半侧。
            Check(mesh.vertices[2].z>0&&mesh.vertices[182].z<0,"top to bottom sweep");
            Check(mesh.vertices.Count(v=>v.z>0)>mesh.vertices.Length*.8,"upper half coverage");
            Near(.6,Math.Sqrt(Math.Pow(mesh.vertices[4].x-mesh.vertices[0].x,2)+Math.Pow(mesh.vertices[4].z-mesh.vertices[0].z,2)));
            Check(mesh.colors.Take(5).All(c=>c.a<.0001)&&mesh.colors.Skip(180).All(c=>c.a<.0001),"tapered arc ends");
        });
        run("撤下旧目标弧痕Fleck与运行贴图并保持受药爱心参数",()=>{
            var defs=XDocument.Load(Path.Combine(root,"1.7/Defs/EffecterDefs/SSC_PnaWeaponEffects.xml")).Root.Elements("FleckDef").ToArray();
            Near(1,defs.Length);Check((string)defs[0].Element("defName")=="SSC_PnaAbsorptionHeart","heart only");
            Near(.64,(double)defs[0].Element("solidTime"));Check(!File.Exists(Path.Combine(root,"1.7/Textures/Things/Mote/SSC_PnaWhipHitArc.png")),"old asset removed");
        });
    }
}
