// AI 辅助生成：真实 Harmony 验证生产命中、定时淡出和最终绘制变换；GPU 与伤害为边界。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using UnityEngine;
using Verse;
internal static class Program
{
 private sealed class TestVerb:Verb_MeleeAttackSSCPna {internal bool Cast()=>TryCastShot();}
 private sealed class Fixture
 {
  internal readonly Map Map=new Map();
  internal readonly Pawn Caster=new Pawn{DrawPos=new Vector3(10,0,10),Position=new IntVec3(10,10)};
  internal readonly Pawn Target=new Pawn{DrawPos=new Vector3(11,0,10),Position=new IntVec3(11,10)};
  internal readonly Thing Weapon=new Thing{def=new ThingDef{defName=SSCPnaSpearGlowUtility.WeaponDefName}};
  internal readonly TestVerb Verb=new TestVerb();
  internal JitterHandler Handler=>Caster.Drawer.Handler;
  internal float Alpha=>SSCPnaSpearGlowUtility.GetAlpha(Weapon);
  internal Fixture()
  {
   Scribe.mode=LoadSaveMode.Inactive;Find.CurrentMap=Map;Caster.Map=Map;Target.Map=Map;Weapon.MapHeld=Map;
   Caster.equipment.Primary=Weapon;Verb.CasterPawn=Caster;Verb.EquipmentSource=Weapon;Verb.SetTarget(Target);
   Verb.NativeCast=()=>{Caster.Drawer.Notify_MeleeAttackOn(Target);return true;};
   SSCPnaWeaponEffectDefOf.SSC_PnaAbsorptionHeart=new FleckDef();Graphics.Sent.Clear();
   PawnRenderUtility.NextMaterial=new Material{mainTexture=new Texture2D(),color=new Color(.3f,.2f,.4f,1)};
   PawnRenderUtility.NextMesh=MeshPool.plane10;
   PawnRenderUtility.NextMatrix=new Matrix4x4{m00=2,m02=.3f,m03=4,m11=0,m13=6,m20=-.3f,m22=2,m23=8,m33=1};
  }
  internal void Draw()=>PawnRenderUtility.DrawEquipmentAiming(Weapon,default,0);
 }
 private static int passed,failed;
 private static void Run(string name,Action body)
 {
        if (!SscTestProfile.ShouldRun(name)) return;try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
 private static void Check(bool value,string message){if(!value)throw new Exception(message);}
 private static void Near(double a,double b)=>Check(Math.Abs(a-b)<.0001,$"{a} != {b}");
 private static int Main(string[] args)
 {
  var harmony=new Harmony("ssc.tests.pna-weapon-effects");harmony.PatchAll(Assembly.GetExecutingAssembly());
  Run("真实Harmony注册绘制、回位、卸装与近战出手补丁",()=>Near(4,harmony.GetPatchedMethods().Count()));
  foreach(bool hit in new[]{true,false})Run("仅命中泛光且原返回值保持 hit="+hit,()=>
  {
   var f=new Fixture();f.Verb.NativeCast=()=>{f.Caster.Drawer.Notify_MeleeAttackOn(f.Target);return hit;};
   Check(f.Verb.Cast()==hit,"原返回值");Near(1,f.Verb.BaseCalls);Near(hit?1:0,f.Alpha);Near(.35,f.Handler.CurrentOffset.x);
  });
  foreach(string gate in new[]{"busy","despawned","target-map","saving","no-weapon","wrong-weapon","not-primary"})Run("不适用的出手不亮 "+gate,()=>
  {
   var f=new Fixture();switch(gate){case "busy":f.Caster.stances.FullBodyBusy=true;break;case "despawned":f.Caster.Spawned=false;break;
    case "target-map":f.Target.Map=new Map();break;case "saving":Scribe.mode=LoadSaveMode.Saving;break;case "no-weapon":f.Verb.EquipmentSource=null;break;
    case "wrong-weapon":f.Weapon.def.defName="Other";break;case "not-primary":f.Caster.equipment.Primary=new Thing();break;}
   f.Verb.Cast();Near(1,f.Verb.BaseCalls);Near(0,f.Alpha);
  });
  Run("命中后目标销毁仍有接触泛光",()=>{var f=new Fixture();f.Verb.NativeCast=()=>{f.Caster.Drawer.Notify_MeleeAttackOn(f.Target);f.Target.Spawned=false;return true;};f.Verb.Cast();Near(1,f.Alpha);});
  Run("父类异常继续传播且不提前泛光",()=>{var f=new Fixture();f.Verb.NativeCast=()=>throw new InvalidOperationException();bool threw=false;try{f.Verb.Cast();}catch(InvalidOperationException){threw=true;}Check(threw,"原异常");Near(0,f.Alpha);});
  Run("新一次刺空清掉上一次反馈",()=>{var f=new Fixture();f.Verb.Cast();Near(1,f.Alpha);f.Verb.NativeCast=()=>false;f.Verb.Cast();Near(0,f.Alpha);});
  Run("接触反馈不依赖护甲或主人药效免疫",()=>{var f=new Fixture();f.Target.Master=true;f.Target.Valid=false;f.Verb.Cast();Near(1,f.Alpha);Near(0,f.Map.flecks.Sent.Count);});
  Run("原版回收后继续泛光，48tick时熄灭",()=>
  {
   var f=new Fixture();f.Verb.Cast();float previous=1;
   for(int i=1;i<=47;i++){f.Handler.ProcessPostTickVisuals(1);float a=f.Alpha;Check(a<=previous&&a>0,"截止前仍亮且不回升");previous=a;}
   Near(0,f.Handler.CurrentOffset.x);f.Handler.ProcessPostTickVisuals(1);Near(0,f.Alpha);
  });
  Run("18tick满亮，随后30tick淡出",()=>
  {
   var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(18);Near(1,f.Alpha);
   f.Handler.ProcessPostTickVisuals(1);Check(f.Alpha<1&&f.Alpha>0,"后半段开始淡出");
   f.Handler.ProcessPostTickVisuals(29);Near(0,f.Alpha);Near(0,f.Handler.CurrentOffset.x);
  });
  Run("保持与淡出区间的亮度连续",()=>
  {
   var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(18);Near(1,f.Alpha);
   f.Handler.ProcessPostTickVisuals(15);Near(.5,f.Alpha);
   f.Handler.ProcessPostTickVisuals(15);Near(0,f.Alpha);
  });
  Run("批量视觉tick和逐tick相同",()=>{var a=new Fixture();a.Verb.Cast();for(int i=0;i<33;i++)a.Handler.ProcessPostTickVisuals(1);float expected=a.Alpha;var b=new Fixture();b.Verb.Cast();b.Handler.ProcessPostTickVisuals(33);Near(expected,b.Alpha);});
  Run("暂停和多次绘制查询不推进进度",()=>{var f=new Fixture();f.Verb.Cast();for(int i=0;i<10;i++)Near(1,f.Alpha);f.Handler.ProcessPostTickVisuals(0);Near(1,f.Alpha);});
  Run("走路和目标移动不影响进度",()=>{var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(5);float a=f.Alpha;f.Caster.DrawPos=new Vector3(90,0,90);f.Target.DrawPos=default;Near(a,f.Alpha);});
  Run("额外同向受击不能延长泛光",()=>{var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(33);float a=f.Alpha;f.Handler.AddOffset(.2f,90);Near(a,f.Alpha);f.Handler.ProcessPostTickVisuals(15);Near(0,f.Alpha);});
  Run("提前回位不提前熄灭，到期后外力不能重新点亮",()=>{var f=new Fixture();f.Verb.Cast();f.Handler.CurrentOffset=default;Near(1,f.Alpha);f.Handler.ProcessPostTickVisuals(48);Near(0,f.Alpha);f.Handler.AddOffset(.35f,90);Near(0,f.Alpha);});
  Run("淡出后外力不能使亮度回升",()=>{var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(33);float a=f.Alpha;f.Handler.AddOffset(.2f,90);Near(a,f.Alpha);});
  Run("同格较短回位仍显示完整0.8秒",()=>{var f=new Fixture();f.Target.Position=f.Caster.Position;f.Verb.Cast();f.Handler.ProcessPostTickVisuals(14);Near(0,f.Handler.CurrentOffset.x);Near(1,f.Alpha);f.Handler.ProcessPostTickVisuals(34);Near(0,f.Alpha);});
  Run("已有外部偏移不改变本次泛光寿命",()=>{var f=new Fixture();f.Target.Position=f.Caster.Position;f.Handler.AddOffset(.17f,90);f.Verb.Cast();f.Handler.ProcessPostTickVisuals(33);Near(.5,f.Alpha);f.Handler.ProcessPostTickVisuals(15);Near(0,f.Alpha);});
  Run("各方向和斜向保持相同定时淡出",()=>
  {
   foreach(var row in new[]{(x:1f,z:0f),(x:-1f,z:0f),(x:0f,z:1f),(x:0f,z:-1f),(x:1f,z:1f)})
   {var f=new Fixture();f.Target.DrawPos=new Vector3(10+row.x,0,10+row.z);f.Verb.Cast();Near(1,f.Alpha);f.Handler.ProcessPostTickVisuals(33);Near(.5,f.Alpha);f.Handler.ProcessPostTickVisuals(15);Near(0,f.Alpha);}
  });
  Run("不同持有者的反馈独立推进",()=>{var a=new Fixture();a.Verb.Cast();var b=new Fixture();b.Verb.Cast();a.Handler.ProcessPostTickVisuals(48);Near(0,a.Alpha);Near(1,b.Alpha);});
  foreach(string gate in new[]{"dead","destroyed","despawned","switch-map","unequip","save"})Run("持有状态失效清理 "+gate,()=>
  {
   var f=new Fixture();f.Verb.Cast();switch(gate){case "dead":f.Caster.Dead=true;break;case "destroyed":f.Weapon.Destroyed=true;break;case "despawned":f.Caster.Spawned=false;break;case "switch-map":f.Caster.Map=new Map();break;case "unequip":f.Caster.equipment.Primary=null;break;case "save":Scribe.mode=LoadSaveMode.Saving;break;}Near(0,f.Alpha);
  });
  foreach(bool flip in new[]{false,true})Run("最终矩阵和翻面对齐 flip="+flip,()=>
  {
   var f=new Fixture();f.Verb.Cast();PawnRenderUtility.NextMesh=flip?MeshPool.plane10Flip:MeshPool.plane10;f.Draw();Near(3,Graphics.Sent.Count);
   var a=Graphics.Sent[0];var halo=Graphics.Sent[1];var b=Graphics.Sent[2];Check(!a.Overlay&&halo.Overlay&&b.Overlay,"原武器、柔边光晕、刃部泛光各一次");
   Near(a.Matrix.m00,b.Matrix.m00);Near(a.Matrix.m02,b.Matrix.m02);Near(a.Matrix.m03,b.Matrix.m03);Near(a.Matrix.m23,b.Matrix.m23);Near(a.Matrix.m13+.001,b.Matrix.m13);Near(a.Layer,b.Layer);
   Check(ReferenceEquals(a.Material,PawnRenderUtility.NextMaterial),"原材质保留");Near(.3,a.Material.color.r);Near(.85,b.Color.a);Check(b.Material.shader==ShaderDatabase.MoteGlow,"泛光材质");
   Near(a.Matrix.m03,halo.Matrix.m03);Near(a.Matrix.m23,halo.Matrix.m23);Near(a.Matrix.m13+.0005,halo.Matrix.m13);
   Near(a.Matrix.m00,halo.Matrix.m00);Near(a.Matrix.m02,halo.Matrix.m02);Near(.8,halo.Color.a);
   Check(((Texture2D)halo.Material.mainTexture).Path=="Things/Mote/BrightFlash","柔边纹理独立于原矛头，避免重影");
   foreach(var v in b.Mesh.vertices)Check(flip?v.x<0:v.x>0,"正确一侧的矛尖");foreach(var uv in b.Mesh.uv)Check(uv.x>=SSCPnaSpearGlowRenderer.BladeStartU,"不采样木杆绑带");
  });
  Run("未命中仅绘制一次原武器",()=>{var f=new Fixture();f.Draw();Near(1,Graphics.Sent.Count);});
  Run("非当前地图不叠画",()=>{var f=new Fixture();f.Verb.Cast();Find.CurrentMap=new Map();f.Draw();Near(1,Graphics.Sent.Count);});
  Run("未知网格不猜测UV，保留原绘制",()=>{var f=new Fixture();f.Verb.Cast();PawnRenderUtility.NextMesh=new Mesh();f.Draw();Near(1,Graphics.Sent.Count);});
  Run("裁切区域覆盖完整矛尖",()=>{var m=SSCPnaSpearGlowRenderer.CreateMask(false);Near(4,m.vertices.Length);Near(6,m.triangles.Length);Near(183d/256-.5,m.vertices[0].x);Near(.5,m.vertices[2].x);Near(183d/256,m.uv[0].x);Near(1,m.uv[2].x);});
  foreach(bool flip in new[]{false,true})Run("光晕围绕矛头中心扩至4.5倍 flip="+flip,()=>
  {
   var mesh=SSCPnaSpearGlowRenderer.CreateHalo(flip);
   float minX=mesh.vertices.Min(v=>v.x),maxX=mesh.vertices.Max(v=>v.x);
   float minZ=mesh.vertices.Min(v=>v.z),maxZ=mesh.vertices.Max(v=>v.z);
   Near(67d/256*4.5,maxX-minX);Near(28d/256*4.5,maxZ-minZ);
   Near((flip?-1:1)*(216.5/256-.5),(minX+maxX)*.5);Near(0,(minZ+maxZ)*.5);
   Near(0,mesh.uv[0].x);Near(1,mesh.uv[2].x);
  });
  Run("光晕和刃部共享淡出进度",()=>
  {
   var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(33);float alpha=f.Alpha;f.Draw();
   Near(alpha*.8,Graphics.Sent[1].Color.a);Near(alpha*.85,Graphics.Sent[2].Color.a);
  });
  Run("两层网格显式提供白色顶点色",()=>
  {
   foreach(bool flip in new[]{false,true})foreach(var mesh in new[]{SSCPnaSpearGlowRenderer.CreateHalo(flip),SSCPnaSpearGlowRenderer.CreateMask(flip)})
   {Near(4,mesh.colors.Length);foreach(var c in mesh.colors){Near(1,c.r);Near(1,c.g);Near(1,c.b);Near(1,c.a);}}
  });
  Run("光晕不使用原版共享材质且不修改共享纹理",()=>
  {
   var f=new Fixture();var texture=ContentFinder<Texture2D>.Get("Things/Mote/BrightFlash");
   var original=MaterialPool.MatFrom(texture,ShaderDatabase.MoteGlow,new Color(1,1,1,1));
   original.color=new Color(.2f,.4f,.6f,.8f);f.Verb.Cast();f.Draw();var halo=Graphics.Sent[1].Material;
   Check(!ReferenceEquals(original,halo),"SSC独占材质");Check(ReferenceEquals(texture,halo.mainTexture),"纹理只读复用");
   Near(.2,original.color.r);Near(.8,original.color.a);Near(1,halo.color.r);Near(1,halo.color.a);
  });
  Run("同帧不同武器的亮度不会相互覆盖",()=>
  {
   var a=new Fixture();a.Verb.Cast();a.Handler.ProcessPostTickVisuals(33);float alpha=a.Alpha;
   var b=new Fixture();b.Verb.Cast();Find.CurrentMap=a.Map;a.Draw();Find.CurrentMap=b.Map;b.Draw();
   Near(6,Graphics.Sent.Count);Near(alpha*.8,Graphics.Sent[1].Color.a);Near(.8,Graphics.Sent[4].Color.a);
   Near(alpha*.85,Graphics.Sent[2].Color.a);Near(.85,Graphics.Sent[5].Color.a);
  });
  Run("渲染资源类声明原版启动初始化标记",()=>Check(typeof(SSCPnaSpearGlowRenderer).IsDefined(typeof(StaticConstructorOnStartupAttribute)),"资源类声明主线程初始化"));
  Run("到期后两层效果同时消失",()=>
  {
   var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(48);f.Draw();Near(1,Graphics.Sent.Count);
  });
  Run("原偏移为零的命中仍能开始计时",()=>{var f=new Fixture();f.Verb.NativeCast=()=>true;f.Verb.Cast();Near(1,f.Alpha);f.Handler.ProcessPostTickVisuals(48);Near(0,f.Alpha);});
  Run("下一次命中重启本次计时",()=>{var f=new Fixture();f.Verb.Cast();f.Handler.ProcessPostTickVisuals(33);Near(.5,f.Alpha);f.Verb.Cast();Near(1,f.Alpha);f.Handler.ProcessPostTickVisuals(48);Near(0,f.Alpha);});
  Run("超大批次到期且负数批次不推进",()=>{var f=new Fixture();f.Verb.Cast();SSCPnaSpearGlowUtility.Advance(f.Handler,-1);Near(1,f.Alpha);SSCPnaSpearGlowUtility.Advance(f.Handler,int.MaxValue);Near(0,f.Alpha);});
  Run("未知IL布局明确拒绝",()=>{bool threw=false;try{Harmony_SSCPnaSpearGlowDraw.Transpiler(new[]{new CodeInstruction(OpCodes.Ret)}).ToArray();}catch(InvalidOperationException){threw=true;}Check(threw,"不静默错挂");});
  Run("受药爱心尺寸旋转速度和颜色不变",()=>{var f=new Fixture();SSCPnaWeaponEffectsUtility.ShowAbsorption(f.Target);var d=f.Map.flecks.Sent.Single();Near(.36,d.scale);Near(180,d.rotation);Near(.22,d.velocity.Value.z);Near(10.6,d.spawnPosition.z);Near(2.45,d.instanceColor.Value.b);});
  foreach(string gate in new[]{"invalid","master","despawned","other-map","offscreen","no-def","saving"})Run("爱心原门槛 "+gate,()=>
  {
   var f=new Fixture();switch(gate){case "invalid":f.Target.Valid=false;break;case "master":f.Target.Master=true;break;case "despawned":f.Target.Spawned=false;break;case "other-map":Find.CurrentMap=new Map();break;case "offscreen":f.Map.Visible=false;break;case "no-def":SSCPnaWeaponEffectDefOf.SSC_PnaAbsorptionHeart=null;break;case "saving":Scribe.mode=LoadSaveMode.Saving;break;}SSCPnaWeaponEffectsUtility.ShowAbsorption(f.Target);Near(0,f.Map.flecks.Sent.Count);
  });
  string root=Path.GetFullPath(args.Length>0?args[0]:".");
  Run("尖梭定义和贴图撤下，爱心XML参数保留",()=>{var d=XDocument.Load(Path.Combine(root,"1.7/Defs/EffecterDefs/SSC_PnaWeaponEffects.xml")).Root.Elements().ToArray();Near(1,d.Length);Check((string)d[0].Element("defName")=="SSC_PnaAbsorptionHeart","爱心保留");Near(.06,(double)d[0].Element("fadeInTime"));Near(.64,(double)d[0].Element("solidTime"));Near(.5,(double)d[0].Element("fadeOutTime"));Check(!File.Exists(Path.Combine(root,"1.7/Textures/Things/Mote/SSC_PnaStabSpindle.png")),"旧资源撤下");});
  Run("专用动作仍接原伤害，普通柄击不接泛光",()=>{var d=XDocument.Load(Path.Combine(root,"1.7/Defs/ManeuverDefs/SSC_PnaPrimitiveManeuvers.xml"));Check(d.Descendants("verbClass").Single().Value=="SexSlaveCraft.Verb_MeleeAttackSSCPna","专用动作");Check(d.Descendants("meleeDamageDef").Single().Value=="SSC_Damage_PnaStaffStab","伤害不变");});
  WhipPoseTests.Register(Run,root);
  WhipHitTests.Register(Run,root);
  SilkWhipPoseTests.Register(Run,root);
  Console.WriteLine($"结果：{passed}/{passed+failed} 项通过。");return failed==0?0:1;
 }
}
