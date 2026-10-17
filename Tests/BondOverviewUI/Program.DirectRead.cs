// AI 辅助生成。实际运行本地查询、补漏调度及点击；引擎替身只记录遍历和绘制边界。
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
            var s=new SSCBondOverviewSession(Current.Game,()=>0,(p,m)=>throw new Exception("must not build"));
            var v=s.ObserveCard(f.slave,f.map,0);
            Require(v.Snapshot.Master.Pawn==f.master&&v.Snapshot.AssignedTrainer==trainer&&!v.Snapshot.CoverageComplete);
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
        Check("direct: provisional zero says checking instead of no bound slaves",()=>{
            var p=Master();Place(p,new Map());Find.CurrentMap=p.Map;Find.Selector.Objects.Add(p);
            Current.ProgramState=ProgramState.Entry;var unavailable=SSCBondOverviewUtility.BuildLocal(p,p.Map);
            Current.ProgramState=ProgramState.Playing;
            Require(!SSCBondOverviewUtility.BuildLocal(p,p.Map,unavailable).CoverageComplete);
            var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(p,p.Map,0);
            Widgets.CapturedLabels=new();Paint(new Gizmo_SSCBondOverview(s));
            Require(!s.Card.Snapshot.CoverageComplete&&s.Card.Display.SlaveHeading=="Known slaves: 0");
            Require(Widgets.CapturedLabels.Any(i=>i.value==SSCBondOverviewText.T("SupplementPending"))&&
                !Widgets.CapturedLabels.Any(i=>i.value==SSCBondOverviewText.T("EmptySlaves")));
            s.Update(0,true,p.Map);Require(s.Card.Snapshot.CoverageComplete);
        });
        Check("direct: missing reverse index with unset identity still gets global supplement",()=>{
            var f=Fixture();f.master.Training.pawnIdentity=PawnIdentity.Unset;SSCBondUtility.GetBridle(f.master).targets.Clear();
            var s=new SSCBondOverviewSession(Current.Game,()=>0);var v=s.ObserveCard(f.master,f.map,0);
            Require(!v.ShowCard&&v.Snapshot.TotalSlaveCount==0&&!v.Snapshot.CoverageComplete);
            s.Update(0,true,f.map);Require(v.ShowCard&&v.Snapshot.TotalSlaveCount==1&&v.Snapshot.CoverageComplete);
        });
        Check("direct: known supplemental references survive local refresh but not rebinding",()=>{
            var f=Fixture();SSCBondUtility.GetBridle(f.master).targets.Clear();var full=SSCBondOverviewUtility.Build(f.master,f.map);
            f.slave.Spawned=false;f.map.Things.Remove(f.slave);
            var local=SSCBondOverviewUtility.BuildLocal(f.master,f.map,full);
            Require(local.CoverageComplete&&local.TotalSlaveCount==1&&ThingOwnerUtility.MapQueryCount==1);
            var supplemented=SSCBondOverviewUtility.BuildWithKnown(f.master,f.map,local);Require(supplemented.TotalSlaveCount==1);
            SSCBondUtility.GetChain(f.slave).target=Master();local=SSCBondOverviewUtility.BuildLocal(f.master,f.map,supplemented);
            Require(local.TotalSlaveCount==0);
        });
        foreach(bool game in new[]{false,true})Check("direct: rejects known references from another "+(game?"game":"source"),()=>{
            var f=Fixture();var previous=SSCBondOverviewUtility.Build(f.master,f.map);SSCBondUtility.GetBridle(f.master).targets.Clear();
            Pawn source=f.master;if(game)Current.Game=new();else source=Master();
            var local=SSCBondOverviewUtility.BuildLocal(source,f.map,previous);Require(!local.CoverageComplete&&local.TotalSlaveCount==0);
        });
        Check("direct: source switching during global backoff does not delay owner or trainer",()=>{
            var f=Fixture();double now=0;int builds=0;
            var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{builds++;now+=3;return View(p,f.slave);});
            s.ObserveCard(f.master,f.map,0);s.Update(0,true,f.map);Require(builds==1);
            now=4;f.slave.Training.selectedTrainer=f.master;var v=s.ObserveCard(f.slave,f.map,1);s.OpenDetails(f.slave,f.map);s.Update(1,true,f.map);
            Require(builds==1&&v.Snapshot.Master.Pawn==f.master&&v.Snapshot.AssignedTrainer==f.master&&!v.Snapshot.CoverageComplete);
        });
        Check("direct: same source changed owner trainer and bridle refreshes without global scan",()=>{
            var f=Fixture();double now=0;int builds=0;
            var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{builds++;now+=3;return View(p);});
            var v=s.ObserveCard(f.slave,f.map,0);s.Update(0,true,f.map);now=4;
            var master=Master();SSCBondUtility.GetChain(f.slave).target=master;f.slave.Training.selectedTrainer=master;
            // 构造旧存档中身份与实际锁链不一致的关系；查询不得套用新绑定事务的资格限制。
            var child=Slave(master);SSCBondUtility.GetChain(child).target=f.slave;
            var bridle=new Hediff_BridleOfSexSlave{def=SSCDefOf.BridleOfSexSlave};bridle.targets.Add(child);
            f.slave.health.AddHediff(bridle);s.ObserveCard(f.slave,f.map,1);
            Require(builds==1&&v.Snapshot.Master.Pawn==master&&v.Snapshot.AssignedTrainer==master&&v.Snapshot.Slaves.Single().Pawn==child);
        });
        Check("direct: equal size in-place changes are picked up by local timer during backoff",()=>{
            var f=Fixture();double now=0;int builds=0;
            var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{builds++;now+=3;return View(p,f.slave);});
            var v=s.ObserveCard(f.master,f.map,0);s.Update(0,true,f.map);var old=v.Snapshot;
            var another=Slave(Master());SSCBondUtility.GetChain(another).target=f.master;
            SSCBondUtility.GetBridle(f.master).targets[0]=another;
            now=4;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);Require(ReferenceEquals(old,v.Snapshot));
            now=5;s.ObserveCard(f.master,f.map,2);s.Update(2,true,f.map);
            Require(builds==1&&v.Snapshot.Slaves.Any(e=>e.Pawn==another)&&v.Snapshot.Slaves.Any(e=>e.Pawn==f.slave));
        });
        Check("direct: repeated registration and paint reuse snapshot and text caches",()=>{
            var f=Fixture();var s=new SSCBondOverviewSession(Current.Game,()=>0);var v=s.ObserveCard(f.master,f.map,0);
            var first=v.Snapshot;var g=new Gizmo_SSCBondOverview(s);Paint(g);int revision=v.Revision,truncations=UIExtensions.Truncations;
            for(int i=0;i<200;i++){s.ObserveCard(f.master,f.map,0);Paint(g);}
            Require(ReferenceEquals(first,v.Snapshot)&&v.Revision==revision&&UIExtensions.Truncations==truncations&&ThingOwnerUtility.MapQueryCount==0);
        });
        Check("direct: failed supplement preserves verified direct facts and retry deadline",()=>{
            var f=Fixture();double now=0;int builds=0;
            var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{builds++;throw new Exception("supplement");});
            var v=s.ObserveCard(f.master,f.map,0);s.Update(0,true,f.map);var old=v.Snapshot;
            Require(v.Failed&&v.Snapshot.Slaves.Single().Pawn==f.slave&&!v.Snapshot.CoverageComplete);
            now=1;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);Require(builds==1&&ReferenceEquals(old,v.Snapshot));
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
            var result=Click(g,20,45);Require(result.State==GizmoState.Interacted);g.ProcessInput(result.InteractEvent);
            Require(CameraJumper.Last.WorldObject==ship&&CameraJumper.Jumps==1&&ship.Things.Enumerations==1);
        });
        Check("direct: unknown location and changed relation still reject click safely",()=>{
            var f=Fixture();f.slave.Spawned=false;var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(f.master,f.map,0);
            var g=new Gizmo_SSCBondOverview(s);var result=Click(g,20,45);g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
            result=Click(g,20,45);SSCBondUtility.GetChain(f.slave).target=Master();g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
        });
        foreach(var panel in new[]{SSCBondOverviewPanel.Master,SSCBondOverviewPanel.Trainer,SSCBondOverviewPanel.Slaves})
            Check("native card: independent width and standard background "+panel,()=>{
                var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s,panel);
                float expected=panel==SSCBondOverviewPanel.Master?180:panel==SSCBondOverviewPanel.Trainer?220:300;
                Require(g.GetWidth(400)==expected&&g.GetWidth(120)==120);Paint(g);
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
            Check("native card: portrait name and actions fit narrow panel "+panel,()=>{
                var f=Fixture();f.slave.Training.selectedTrainer=f.master;Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
                var s=new SSCBondOverviewSession(Current.Game,()=>0);s.ObserveCard(f.slave,f.map,0);
                Widgets.CapturedLabels=new();var g=new Gizmo_SSCBondOverview(s,panel);Paint(g);
                Require(Widgets.CapturedLabels.All(i=>i.rect.x>=0&&i.rect.xMax<=g.GetWidth(300)));
                var name=Widgets.CapturedLabels.Single(i=>i.rect.x==54);Require(name.rect.xMax<=(panel==SSCBondOverviewPanel.Trainer?144:180));
            });
    }
}
