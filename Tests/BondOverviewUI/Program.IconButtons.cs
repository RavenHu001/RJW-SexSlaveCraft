// AI 辅助生成。验证图标替换后的实际输入分派及主人入口移除，不改变数据／权限替身。
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    static void IconButtonTests()
    {
        Check("icon actions: master has no overview button in normal or compact layout",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Master);Widgets.CapturedButtons=new();
            foreach(float width in new[]{130f,55f}){
                Event.current=new(){type=EventType.mouseDown,mousePosition=new(Math.Min(width,75)-10,12)};
                var result=g.GizmoOnGUI(new(0,0),width,default);g.ProcessInput(result.InteractEvent);
                Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null&&Widgets.CapturedButtons.Count==0);
            }
            Event.current=new(){type=EventType.mouseDown,mousePosition=new(15,45)};
            var clicked=g.GizmoOnGUI(new(0,0),55,default);g.ProcessInput(clicked.InteractEvent);
            Require(CameraJumper.Last.Thing==f.master&&CameraJumper.Jumps==3&&CameraJumper.Selects==3);
        });
        Check("icon actions: overview has a framed image and opens once without navigating",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            Widgets.CapturedButtons=new();GUI.CapturedTextures=new();var result=Click(g,114,14);
            Require(Widgets.CapturedButtons.Select(b=>b.rect).Distinct().Count()==1&&Widgets.CapturedButtons.All(b=>b.label=="")&&GUI.CapturedTextures.Any(i=>i.image==TexButton.Search));
            g.ProcessInput(result.InteractEvent);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();g.ProcessInput(result.InteractEvent);
            Require(w?.Source==f.master&&CameraJumper.Jumps==0&&s.Details==s.Card);
        });
        foreach(string state in new[]{"right","disabled","gui","revision"})Check("icon actions: reject inappropriate input "+state,()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            if(state=="disabled")g.Disabled=true;if(state=="gui")GUI.enabled=false;
            var result=Click(g,114,14,state=="right"?1:0);if(state=="revision")s.Card.Changed();g.ProcessInput(result.InteractEvent);
            Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null&&CameraJumper.Jumps==0);
        });
        Check("icon actions: edit button opens menu without changing trainer or master",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);TrainerAssignmentUtility.Candidates.Add(f.master);
            var s=Ready(f.slave,f.map);Widgets.CapturedButtons=new();GUI.CapturedTextures=new();var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Trainer);
            var result=Click(g,86,14);g.ProcessInput(result.InteractEvent);
            Require(Find.WindowStack.WindowOfType<FloatMenu>()!=null&&f.slave.Training.selectedTrainer==null&&SSCBondUtility.GetBoundMaster(f.slave)==f.master);
            Require(Widgets.CapturedButtons.Select(b=>b.rect).Distinct().Count()==2&&Widgets.CapturedButtons.All(b=>b.label=="")&&GUI.CapturedTextures.Any(i=>i.image==TexButton.Rename));
        });
        Check("icon actions: overview edit icon still uses the shared assignment menu",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);TrainerAssignmentUtility.Candidates.Add(f.master);
            var s=Ready(f.slave,f.map);Dialog_SSCBondOverview.Open(s,f.slave);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            Event.current=new(){type=EventType.mouseDown,mousePosition=new(720,120)};w.DoWindowContents(new(0,0,760,500));
            Require(Find.WindowStack.WindowOfType<FloatMenu>()==null);
            Event.current=new(){type=EventType.mouseUp,mousePosition=new(720,120)};w.DoWindowContents(new(0,0,760,500));
            Require(Find.WindowStack.WindowOfType<FloatMenu>()!=null&&f.slave.Training.selectedTrainer==null&&CameraJumper.Jumps==0);
        });
        Check("button release: down captures and next-frame release opens overview exactly once",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            var down=ButtonEvent(g,EventType.mouseDown,114,14);g.ProcessInput(down.InteractEvent);
            Require(down.State!=GizmoState.Interacted&&Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null);
            g.ClearPending();Event.current=new(){type=EventType.repaint,mousePosition=new(114,14)};Paint(g);
            var up=ButtonEvent(g,EventType.mouseUp,114,14);Require(up.State==GizmoState.Interacted);g.ProcessInput(up.InteractEvent);
            var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();Require(w?.Source==f.master);
            w.Close();g.ProcessInput(up.InteractEvent);Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null);
        });
        Check("auto grid: no page buttons remain and all portraits are present",()=>{
            var f=Fixture();for(int i=0;i<10;i++)Place(Slave(f.master,"Extra"+i),f.map);
            var s=Ready(f.master,f.map);Widgets.CapturedButtons=new();Paint(new Gizmo_SSCBondOverview(s));
            Require(Widgets.CapturedButtons.Count==1&&PortraitsCache.Calls==11&&CameraJumper.Jumps==0);
        });
        Check("button release: release without a matching press does not open overview",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            var up=ButtonEvent(g,EventType.mouseUp,114,14);g.ProcessInput(up.InteractEvent);
            Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null);
        });
        Check("button release: dragging out cancels and a later normal click still works",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            ButtonEvent(g,EventType.mouseDown,114,14);var up=ButtonEvent(g,EventType.mouseUp,140,90);g.ProcessInput(up.InteractEvent);
            Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null);
            up=Click(g,114,14);g.ProcessInput(up.InteractEvent);Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()?.Source==f.master);
        });
        foreach(string change in new[]{"source","disabled","gui"})Check("button release: intervening "+change+" rejects the release",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            ButtonEvent(g,EventType.mouseDown,114,14);
            if(change=="source"){
                var other=Master();Place(other,f.map);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(other);s.ObserveCard(other,f.map,1);
            }
            if(change=="disabled")g.Disabled=true;if(change=="gui")GUI.enabled=false;
            var up=ButtonEvent(g,EventType.mouseUp,114,14);g.ProcessInput(up.InteractEvent);
            Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null&&CameraJumper.Jumps==0);
        });
        Check("button release: right-click cannot change trainer",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Trainer);var up=Click(g,86,14,1);g.ProcessInput(up.InteractEvent);
            Require(Find.WindowStack.WindowOfType<FloatMenu>()==null&&f.slave.Training.selectedTrainer==null);
        });
        Check("native layout: master square title click locates and selects the bound owner",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Master);var result=Click(g,5,5);g.ProcessInput(result.InteractEvent);
            Require(g.GetWidth(300)==GenUI.LastRect.height&&CameraJumper.Last.Thing==f.master&&CameraJumper.Selects==1);
            Require(SSCBondUtility.GetBoundMaster(f.slave)==f.master&&Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==null);
        });
        Check("native layout: whole-square action still rejects changed owner before dispatch",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Master);var result=Click(g,5,5);
            SSCBondUtility.GetChain(f.slave).target=Master();g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
        });
        Check("native layout: owned header includes count and provisional marker without a separate number",()=>{
            var f=Fixture();Place(Slave(f.master,"B"),f.map);Place(Slave(f.master,"C"),f.map);
            Verse.Extensions.Translations["SSC_BondOverview_OwnedCount"]="拥有性奴：{0}";LanguageDatabase.activeLanguage=new();
            var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(f.master,f.map,0);Widgets.CapturedLabels=new();
            Paint(new Gizmo_SSCBondOverview(s));Require(Widgets.CapturedLabels.Single().value=="拥有性奴：3+");
            s.Update(0,true,f.map);Widgets.CapturedLabels.Clear();Paint(new Gizmo_SSCBondOverview(s));
            Require(Widgets.CapturedLabels.Single().value=="拥有性奴：3");
        });
        Check("native layout: auto-sized portraits stay inside the card and outside header buttons",()=>{
            var f=Fixture();for(int i=0;i<4;i++)Place(Slave(f.master,"Extra"+i),f.map);
            var s=Ready(f.master,f.map);Widgets.CapturedButtons=new();GUI.CapturedTextures=new();Paint(new Gizmo_SSCBondOverview(s));
            var portraits=GUI.CapturedTextures.Where(t=>t.image==null).Select(t=>t.rect).ToArray();
            Require(portraits.Length==5&&portraits.All(r=>r.width==r.height&&r.width<=33&&r.x>=0&&r.xMax<=130&&r.yMax<=75));
            foreach(var portrait in portraits)foreach(var button in Widgets.CapturedButtons)
                Require(portrait.xMax<=button.rect.x||portrait.x>=button.rect.xMax||portrait.yMax<=button.rect.y||portrait.y>=button.rect.yMax);
        });
    }
    static GizmoResult ButtonEvent(Gizmo gizmo,EventType type,float x,float y,int button=0)
    { Event.current=new(){type=type,button=button,mousePosition=new(x,y)};return gizmo.GizmoOnGUI(new(0,0),300,default); }
}
