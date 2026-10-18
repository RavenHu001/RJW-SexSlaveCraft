// AI 辅助生成。实际运行局部刷新及点击；普通查询不枚举全局候选。
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    static void DirectReadTests()
    {
        Check("direct: owner and trainer available before any global build",()=>{
            var f=Fixture();var trainer=Master();f.slave.Training.selectedTrainer=trainer;
            var ship=new Gravship();Find.CurrentGravship=ship;ship.Pawns.Add(trainer);
            var s=new SSCBondOverviewSession(Current.Game,()=>0);
            var v=s.ObserveCard(f.slave,f.map,0);
            Require(v.Snapshot.Master.Pawn==f.master&&v.Snapshot.AssignedTrainer==trainer);
            Require(ThingOwnerUtility.MapQueryCount==0&&ship.Things.Enumerations==0&&ship.Pawns.Enumerations==0);
        });
        Check("direct: bridle candidates are deduplicated and chain checked without repair",()=>{
            var f=Fixture();var bridle=SSCBondUtility.GetBridle(f.master);var dead=Slave(f.master,"Dead");dead.Dead=true;
            var stale=Slave(Master());var destroyed=Slave(f.master);destroyed.Destroyed=true;
            var discarded=Slave(f.master);discarded.Discarded=true;
            bridle.targets.Add(f.slave);bridle.targets.Add(stale);bridle.targets.Add(f.master);bridle.targets.Add(null);
            int count=bridle.targets.Count;var chain=SSCBondUtility.GetChain(f.slave);
            var snapshot=SSCBondOverviewUtility.BuildLocal(f.master,f.map);
            Require(snapshot.TotalSlaveCount==2&&snapshot.LivingSlaveCount==1&&snapshot.Slaves.Any(e=>e.Pawn==dead));
            Require(bridle.targets.Count==count&&SSCBondUtility.GetChain(f.slave)==chain&&chain.target==f.master);
            Require(ThingOwnerUtility.MapQueryCount==0&&ThingOwnerUtility.Visits.Count==0);
        });
        Check("direct: local location follows parent without enumerating container contents",()=>{
            var f=Fixture();var box=new Container();Place(box,f.map);box.Contents.Add(f.slave);
            f.slave.Spawned=false;f.slave.ParentHolder=box;
            var snapshot=SSCBondOverviewUtility.BuildLocal(f.master,f.map);
            Require(snapshot.Slaves[0].Location.Container==box&&ThingOwnerUtility.MapQueryCount==0&&ThingOwnerUtility.Visits.Count==0);
        });
        Check("direct: zero is an ordinary empty count without provisional marker",()=>{
            var p=Master();Place(p,new Map());Find.CurrentMap=p.Map;Find.Selector.Objects.Add(p);
            var s=Ready(p,p.Map);Widgets.CapturedLabels=new();Paint(new Gizmo_SSCBondOverview(s));
            Require(s.Card.Display.OwnedHeading=="Slaves: 0"&&s.Card.Display.SlaveHeading=="Sex slaves: 0");
            Require(Widgets.CapturedLabels.Any(i=>i.value==SSCBondOverviewText.T("EmptySlaves"))&&
                !Widgets.CapturedLabels.Any(i=>i.value.Contains("Checking")||i.value.Contains("+")));
        });
        Check("direct: global diagnostic results are not accepted as local candidates",()=>{
            var f=Fixture();SSCBondUtility.GetBridle(f.master).targets.Clear();var full=SSCBondOverviewUtility.Build(f.master,f.map);
            var local=SSCBondOverviewUtility.BuildLocal(f.master,f.map);Require(full.TotalSlaveCount==1&&local.TotalSlaveCount==0);
        });
        Check("direct: changed master trainer and bridle are visible immediately",()=>{
            var f=Fixture();var s=Ready(f.slave,f.map);var v=s.Card;var master=Master();
            SSCBondUtility.GetChain(f.slave).target=master;f.slave.Training.selectedTrainer=master;
            var child=Slave(master);SSCBondUtility.GetChain(child).target=f.slave;
            Hediff_BridleOfSexSlave.AddToPawn(f.slave,child);s.ObserveCard(f.slave,f.map,1);
            Require(v.Snapshot.Master.Pawn==master&&v.Snapshot.AssignedTrainer==master&&v.Snapshot.Slaves.Single().Pawn==child&&ThingOwnerUtility.MapQueryCount==0);
        });
        Check("direct: equal-size replacement is re-read from current bridle on local timer",()=>{
            var f=Fixture();double now=0;var s=new SSCBondOverviewSession(Current.Game,()=>now);var v=s.ObserveCard(f.master,f.map,0);var old=v.Snapshot;
            var another=Slave(Master());SSCBondUtility.GetChain(another).target=f.master;SSCBondUtility.GetBridle(f.master).targets[0]=another;
            now=1.99;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);Require(old==v.Snapshot);
            now=2;s.ObserveCard(f.master,f.map,2);s.Update(2,true,f.map);
            Require(v.Snapshot.Slaves.Single().Pawn==another&&ThingOwnerUtility.MapQueryCount==0);
        });
        Check("direct: repeated registration and paint reuse snapshot and text caches",()=>{
            var f=Fixture();var s=new SSCBondOverviewSession(Current.Game,()=>0);var v=s.ObserveCard(f.master,f.map,0);
            var first=v.Snapshot;var g=new Gizmo_SSCBondOverview(s);Paint(g);int revision=v.Revision,truncations=UIExtensions.Truncations;
            for(int i=0;i<200;i++){s.ObserveCard(f.master,f.map,0);Paint(g);}
            Require(ReferenceEquals(first,v.Snapshot)&&v.Revision==revision&&UIExtensions.Truncations==truncations&&ThingOwnerUtility.MapQueryCount==0);
        });
        foreach(bool trainer in new[]{false,true})Check("direct: uncached ship location clicks resolve live "+(trainer?"trainer":"bond"),()=>{
            var f=Fixture();var ship=new Gravship();Find.CurrentGravship=ship;
            Pawn source=trainer?f.slave:f.master,target=trainer?f.master:f.slave;
            if(trainer)source.Training.selectedTrainer=target;
            target.Spawned=false;ship.Pawns.Add(target);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(source);
            var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(source,f.map,0);
            var location=trainer?s.Card.Snapshot.AssignedTrainerLocation:s.Card.Snapshot.Slaves[0].Location;
            Require(location.Kind==SSCBondOverviewLocationKind.Unknown&&ship.Things.Enumerations==0);
            var g=new Gizmo_SSCBondOverview(s,trainer?SSCBondOverviewPanel.Trainer:SSCBondOverviewPanel.Slaves);
            var result=Click(g,65,50);Require(result.State==GizmoState.Interacted);g.ProcessInput(result.InteractEvent);
            Require(CameraJumper.Last.WorldObject==ship&&CameraJumper.Jumps==1&&ship.Things.Enumerations==1);
        });
        Check("direct: unknown location and changed relation still reject click safely",()=>{
            var f=Fixture();f.slave.Spawned=false;var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(f.master,f.map,0);
            var g=new Gizmo_SSCBondOverview(s);var result=Click(g,65,50);g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
            result=Click(g,65,50);SSCBondUtility.GetChain(f.slave).target=Master();g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
        });
        foreach(var panel in new[]{SSCBondOverviewPanel.Master,SSCBondOverviewPanel.Trainer,SSCBondOverviewPanel.Slaves})
            Check("native card: independent width and standard background "+panel,()=>{
                var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s,panel);
                float expected=panel==SSCBondOverviewPanel.Master?75:130;
                Require(g.GetWidth(400)==expected&&g.GetWidth(120)==Math.Min(expected,120));Paint(g);
                Require(GenUI.LastRect.width==expected&&GenUI.LastTexture==Command.BGTex&&GenUI.LastMaterial==null);
            });
        Check("native card: shrunk low-light disabled and caller color are respected",()=>{
            var f=Fixture();var g=new Gizmo_SSCBondOverview(Ready(f.master,f.map));GUI.color=new(.2f,.3f,.4f);
            g.GizmoOnGUI(new(0,0),300,new(){lowLight=true,shrunk=true});
            Require(GenUI.LastTexture==Command.BGTexShrunk&&GenUI.LastMaterial==TexUI.GrayscaleGUI&&
                GenUI.LastColor.Equals(Command.LowLightBgColor)&&GUI.color.r==.2f);
            g.Disabled=true;Paint(g);Require(GenUI.LastMaterial==TexUI.GrayscaleGUI&&GenUI.LastColor.Equals(Color.white)&&GUI.color.r==.2f);
        });
        foreach(var panel in new[]{SSCBondOverviewPanel.Master,SSCBondOverviewPanel.Trainer})
            Check("native card: portrait and actions fit native panel without names "+panel,()=>{
                var f=Fixture();f.slave.Training.selectedTrainer=f.master;Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
                var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(f.slave,f.map,0);
                Widgets.CapturedLabels=new();var g=new Gizmo_SSCBondOverview(s,panel);Paint(g);
                Require(Widgets.CapturedLabels.All(i=>i.rect.x>=0&&i.rect.xMax<=g.GetWidth(300)));
                Require(!Widgets.CapturedLabels.Any(i=>i.value==f.master.LabelShort));
            });
    }
}
