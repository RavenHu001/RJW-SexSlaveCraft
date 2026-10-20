// AI 辅助生成：丝鞭三姿态、独立物品尺寸、共用残影及工业材质隔离回归。
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class SilkWhipPoseTests
{
    private sealed class Attack : RimWorld.Verb_MeleeAttackDamage { internal bool Cast() => TryCastShot(); }
    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Pawn Owner = new Pawn();
        internal readonly Pawn Target = new Pawn();
        internal readonly ThingWithComps Weapon = new ThingWithComps {
            def = new ThingDef { defName = SSCPnaWhipPoseUtility.SilkWeaponDefName }
        };
        internal readonly Attack Verb = new Attack();
        internal readonly TickManager Clock = new TickManager { TicksGame = 1000 };
        internal readonly Material Original = new Material {
            mainTexture = new Texture2D { Path = "silk item" }, shader = new Shader(), color = new Color(.3f,.4f,.5f,1)
        };
        internal Fixture()
        {
            Find.TickManager = Clock; Find.CurrentMap = Map; Scribe.mode = LoadSaveMode.Inactive;
            Owner.Map = Target.Map = Weapon.MapHeld = Map; Owner.equipment.Primary = Weapon;
            Weapon.Graphic.drawSize = new Vector2(.75f,.75f);
            Verb.CasterPawn = Owner; Verb.EquipmentSource = Weapon; Verb.SetTarget(Target); Verb.NativeCast = () => true;
            PawnRenderUtility.NextMaterial = Original; PawnRenderUtility.NextMesh = MeshPool.plane10;
            PawnRenderUtility.NextMatrix = new Matrix4x4 { m00=.75f,m22=.75f,m03=7,m23=9,m13=.05f,m33=1 };
            Graphics.Sent.Clear();
        }
        internal void Draw() => PawnRenderUtility.DrawEquipmentAiming(Weapon,default,0);
    }
    private static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    private static void Near(double a,double b) => Check(Math.Abs(a-b)<.0001,$"{a} != {b}");

    internal static void Register(Action<string,Action> run,string root)
    {
        foreach(bool flip in new[]{false,true}) foreach(int angle in new[]{0,53,90,180,270,307})
            run($"丝鞭三阶段共用握持点与1.75手持缩放 {flip}/{angle}",()=>{
                var f=new Fixture(); double r=angle*Math.PI/180;
                var input=new Matrix4x4 {m00=(float)Math.Cos(r)*.75f,m20=-(float)Math.Sin(r)*.75f,
                    m02=(float)Math.Sin(r)*.75f,m22=(float)Math.Cos(r)*.75f,m03=7,m23=9,m13=.05f,m33=1};
                PawnRenderUtility.NextMatrix=input;PawnRenderUtility.NextMesh=flip?MeshPool.plane10Flip:MeshPool.plane10;
                foreach(var row in new[]{(-1,"Idle"),(0,"Attack"),(5,"Attack"),(6,"Recovery"),(17,"Recovery"),(18,"Idle")}){
                    if(row.Item1==0)f.Verb.Cast();if(row.Item1>=0)f.Clock.TicksGame=1000+row.Item1;
                    Graphics.Sent.Clear();f.Draw();var body=Graphics.Sent.Single(x=>!x.Overlay);
                    Check(((Texture2D)body.Material.mainTexture).Path==SSCPnaWhipRenderer.SilkTexturePrefix+row.Item2,"silk texture family");
                    var m=body.Matrix;Near(1.75*1.75,m.m00*m.m00+m.m20*m.m20);Near(1.75*1.75,m.m02*m.m02+m.m22*m.m22);
                    float grip=(50f/256f-.5f)*(flip?-1:1);Near(7,m.m03+m.m00*grip);Near(9,m.m23+m.m20*grip);
                    Near(input.m13,m.m13);Near(.75,f.Weapon.Graphic.drawSize.x);Near(.75,f.Weapon.Graphic.drawSize.y);
                    if(row.Item2=="Idle") {Near(Math.Abs(m.m00),Math.Abs(m.m20));Check(m.m20*(flip?1:-1)<0,"butt down");}
                    else {
                        Near(input.m00*1.75/.75,m.m00);Near(input.m20*1.75/.75,m.m20);
                        var trail=Graphics.Sent.Single(x=>x.Overlay);Near(m.m03,trail.Matrix.m03);Near(m.m23,trail.Matrix.m23);
                        Near(m.m00,trail.Matrix.m00);Near(m.m20,trail.Matrix.m20);Near(m.m13-.0005,trail.Matrix.m13);
                    }
                    Near(row.Item2=="Idle"?1:2,Graphics.Sent.Count);
                }
                Check(((Texture2D)f.Original.mainTexture).Path=="silk item","item texture unchanged");
            });
        foreach(bool hit in new[]{false,true}) run("丝鞭命中与打空均出手，只有命中产生工业残影 "+hit,()=>{
            var f=new Fixture();f.Verb.NativeCast=()=>hit;Check(f.Verb.Cast()==hit,"native result");Near(1,f.Verb.BaseCalls);
            f.Draw();Near(hit?2:1,Graphics.Sent.Count);Near(0,f.Map.flecks.Sent.Count);
            Check(SSCPnaWhipPoseUtility.GetPose(f.Weapon)==SSCPnaWhipPose.Attack,"pose triggered");
        });
        foreach(bool silkFirst in new[]{false,true}) run("两代鞭子共享原材质时贴图缓存仍隔离 "+silkFirst,()=>{
            var f=new Fixture();f.Verb.NativeCast=()=>false;
            foreach(bool silk in new[]{silkFirst,!silkFirst,silkFirst}){
                f.Weapon.def.defName=silk?SSCPnaWhipPoseUtility.SilkWeaponDefName:SSCPnaWhipPoseUtility.WeaponDefName;
                f.Weapon.Graphic.drawSize=new Vector2(silk?.75f:1.4f,silk?.75f:1.4f);
                float size=silk?.75f:1.4f;
                PawnRenderUtility.NextMatrix=new Matrix4x4{m00=size,m22=size,m03=7,m23=9,m33=1};
                Graphics.Sent.Clear();f.Verb.Cast();f.Draw();
                var record=Graphics.Sent.Single();var texture=(Texture2D)record.Material.mainTexture;
                Check(texture.Path==(silk?SSCPnaWhipRenderer.SilkTexturePrefix:SSCPnaWhipRenderer.TexturePrefix)+"Attack","no crossed cache");
                Near(silk?1.75:1.4,record.Matrix.m00);Near(.3,f.Original.color.r);
            }
            Check(((Texture2D)f.Original.mainTexture).Path=="silk item","source material not changed");
        });
        foreach(string gate in new[]{"saving","unequip","dead","despawned","map","destroyed","clock","backward"})
            run("丝鞭复用姿态清理且无残留特效 "+gate,()=>{
                var f=new Fixture();f.Verb.Cast();
                switch(gate){case "saving":Scribe.mode=LoadSaveMode.Saving;break;case "unequip":f.Owner.equipment.Primary=null;break;
                    case "dead":f.Owner.Dead=true;break;case "despawned":f.Owner.Spawned=false;break;
                    case "map":f.Owner.Map=new Map();break;case "destroyed":f.Weapon.Destroyed=true;break;
                    case "clock":Find.TickManager=new TickManager{TicksGame=1000};break;case "backward":f.Clock.TicksGame=999;break;}
                Check(SSCPnaWhipPoseUtility.GetPose(f.Weapon)==SSCPnaWhipPose.Idle,"expired or invalid");
                f.Draw();Check(Graphics.Sent.All(x=>!x.Overlay),"no leftover hit effect");
            });
        run("丝鞭手持倍率保留外部矩阵变化与非等比Graphic尺寸",()=>{
            var f=new Fixture();f.Weapon.Graphic.drawSize=new Vector2(.75f,.5f);
            var input=new Matrix4x4{m00=1.5f,m22=1f,m03=7,m23=9,m13=.1f,m33=1};
            var m=SSCPnaWhipRenderer.SilkHeldMatrix(f.Weapon,input);Near(3.5,m.m00);Near(3.5,m.m22);
            Near(7,m.m03);Near(9,m.m23);Near(.1,m.m13);Near(.75,f.Weapon.Graphic.drawSize.x);
        });
        run("丝鞭三张运行图来自明确选图且柄部标定一致",()=>{
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Docs/Art/SSC_PnaSilkWhip/256/poses-export.json")));
            foreach(var pose in doc.RootElement.GetProperty("poses").EnumerateArray()){
                string name=pose.GetProperty("pose").GetString();
                var export=File.ReadAllBytes(Path.Combine(root,"Docs/Art/SSC_PnaSilkWhip/256",pose.GetProperty("output").GetString()));
                Check(export.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"1.7/Textures/Things/Item/Equipment/SSC_PnaSilkWhip_"+name+".png"))),"runtime image matches export");
                string source=Path.Combine(root,"Docs/Art/SSC_PnaSilkWhip",pose.GetProperty("source").GetString());
                Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).Equals(pose.GetProperty("source_sha256").GetString(),StringComparison.OrdinalIgnoreCase),"selected source unchanged");
                Check(Math.Abs(pose.GetProperty("handle_width_px").GetDouble()-64)<.3,"shared handle width");
                var center=pose.GetProperty("handle_center_px");Check(Math.Abs(center[0].GetDouble()-50)<.5&&Math.Abs(center[1].GetDouble()-128)<.5,"shared grip anchor");
                Check(export[16]==0&&export[17]==0&&export[18]==1&&export[19]==0&&export[20]==0&&export[21]==0&&export[22]==1&&export[23]==0,"PNG IHDR 256 square");
            }
            var defs=XDocument.Load(Path.Combine(root,"1.7/Defs/ThingsDefs/SSC_PnaSpacerWeapons.xml"));
            var whip=defs.Root.Elements("ThingDef").Single(d=>(string)d.Element("defName")==SSCPnaWhipPoseUtility.SilkWeaponDefName);
            Near(.75,(double)whip.Element("graphicData").Element("drawSize"));
            Check((string)whip.Element("graphicData").Element("texPath")=="Things/Item/Equipment/SSC_PnaSilkWhip","item stays separate");
        });
    }
}
