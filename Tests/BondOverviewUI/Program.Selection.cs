// AI 辅助生成。批选只改变原版选择；验证现时锁链、地图范围、完整鼠标序列及通知后的再次校验。
using SexSlaveCraft;
using RimWorld;
using UnityEngine;
using Verse;

internal static partial class Program
{
    static void SelectionTests()
    {
        Check("bulk selection: only current-map living spawned selectable current bonds are selected",()=>{
            var f=Fixture();var valid=Slave(f.master,"Valid");Place(valid,f.map);
            var otherMap=new Map();Find.Maps.Add(otherMap);Place(Slave(f.master,"Remote"),otherMap);
            var dead=Slave(f.master,"Dead");dead.Dead=true;Place(dead,f.map);
            var invalid=Slave(f.master,"Destroyed");invalid.Destroyed=true;Place(invalid,f.map);
            var discarded=Slave(f.master,"Discarded");discarded.Discarded=true;Place(discarded,f.map);
            var hidden=Slave(f.master,"Held");hidden.ParentHolder=new Container();
            var unselectable=Slave(f.master,"Not selectable");unselectable.def.selectable=false;Place(unselectable,f.map);
            var stranger=Slave(Master(),"Stranger");Place(stranger,f.map);f.master.Training.selectedTrainer=stranger;
            var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.SelectAll);var result=Click(g,35,35);
            g.ProcessInput(result.InteractEvent);g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.Objects.ToHashSet().SetEquals(new object[]{f.slave,valid})&&CameraJumper.Jumps==0);
            Require(SSCBondUtility.GetBoundMaster(f.slave)==f.master&&SSCBondUtility.GetBoundMaster(valid)==f.master&&f.master.Training.selectedTrainer==stranger);
        });
        Check("bulk selection: local map supplements missing bridle before any global scan",()=>{
            var f=Fixture();SSCBondUtility.GetBridle(f.master).targets.Clear();int builds=0;
            var s=new SSCBondOverviewSession(Current.Game,()=>0);
            s.ObserveCard(f.master,f.map,0);Require(s.Card.Snapshot.TotalSlaveCount==0);
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.SelectAll);var result=Click(g,35,35);g.ProcessInput(result.InteractEvent);
            Require(builds==0&&ThingOwnerUtility.MapQueryCount==0&&Find.Selector.Objects.Single()==f.slave);
        });
        Check("bulk selection: rebind after painting is excluded at dispatch",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.SelectAll);
            var result=Click(g,35,35);SSCBondUtility.GetChain(f.slave).target=Master();g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.Objects.Single()==f.master);
        });
        Check("bulk selection: selection notifications cannot select a subsequently rebound candidate",()=>{
            var f=Fixture();var second=Slave(f.master,"Second");Place(second,f.map);var s=Ready(f.master,f.map);
            Find.Selector.OnSelected=p=>{if(p==f.slave)SSCBondUtility.GetChain(second).target=Master();};
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.SelectAll);var result=Click(g,35,35);g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.Objects.Single()==f.slave);
        });
        Check("bulk selection: native 200 limit leaves complete overview candidates intact",()=>{
            var f=Fixture();for(int i=0;i<249;i++)Place(Slave(f.master,"P"+i),f.map);var s=Ready(f.master,f.map);var snapshot=s.Card.Snapshot;
            var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.SelectAll);var result=Click(g,35,35);g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.NumSelected==200&&snapshot.TotalSlaveCount==250&&ReferenceEquals(snapshot,s.Card.Snapshot));
        });
        foreach(string state in new[]{"source","disabled","gui","right","revision"})Check("bulk selection: reject stale or prohibited input "+state,()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.SelectAll);
            if(state=="disabled")g.Disabled=true;if(state=="gui")GUI.enabled=false;
            var result=Click(g,35,35,state=="right"?1:0);if(state=="revision")s.Card.Changed();
            if(state=="source"){var other=Master();Place(other,f.map);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(other);s.ObserveCard(other,f.map,1);}
            var before=Find.Selector.Objects.ToArray();g.ProcessInput(result.InteractEvent);Require(Find.Selector.Objects.SequenceEqual(before));
        });
        Check("auto layout: original Small header and three scaled portraits fit together",()=>{
            var f=Fixture();Place(Slave(f.master,"B"),f.map);Place(Slave(f.master,"C"),f.map);var s=Ready(f.master,f.map);
            Widgets.CapturedFonts=new();Widgets.CapturedLabels=new();GUI.CapturedTextures=new();Paint(new Gizmo_SSCBondOverview(s));
            Require(Widgets.CapturedFonts.Single()==GameFont.Small&&Widgets.CapturedLabels.Single().value==s.Card.Display.OwnedHeading);
            var portraits=GUI.CapturedTextures.Where(p=>p.image==null).ToArray();Require(portraits.Length==3&&portraits.All(p=>p.rect.width==33));
        });
        Check("auto layout: many portraits retain fixed capture sizes and do not scan on repaint",()=>{
            var f=Fixture();for(int i=0;i<499;i++)Place(Slave(f.master,"P"+i),f.map);var s=Ready(f.master,f.map);int scans=ThingOwnerUtility.MapQueryCount;
            PortraitsCache.Requests.Clear();Paint(new Gizmo_SSCBondOverview(s));
            Require(PortraitsCache.Requests.Count==500&&PortraitsCache.Requests.All(p=>p.size.x==40&&p.size.y==40)&&ThingOwnerUtility.MapQueryCount==scans&&s.Card.Display.RowCount==0);
            foreach(int count in new[]{1,3,20,500,5000,100000}){
                var grid=SSCBondOverviewLayout.Fit(count,118,33);
                Require(grid.Side>0&&grid.Columns*grid.Rows>=count&&grid.Columns*grid.Side<=118.01f&&grid.Rows*grid.Side<=33.01f);
            }
        });
        Check("master button: bottom text overlaps enlarged portrait without showing name",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            Widgets.CapturedLabels=new();GUI.CapturedTextures=new();Paint(new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Master));
            var image=GUI.CapturedTextures.Single(p=>p.image==null).rect;var label=Widgets.CapturedLabels.Single();
            Require(image.width==56&&label.value==SSCBondOverviewText.T("SelectMaster")&&label.rect.y<image.yMax&&label.rect.yMax<=75);
        });
    }
}
