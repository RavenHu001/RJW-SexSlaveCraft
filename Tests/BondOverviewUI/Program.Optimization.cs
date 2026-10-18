// AI 辅助生成。缓存复用必须以完整事实相同为前提；本套件保留真实定时刷新、输入及导航路径。
using SexSlaveCraft;
using UnityEngine;
using Verse;
using RimWorld.Planet;

internal static partial class Program
{
    static void OptimizationTests()
    {
        Check("optimization: unchanged timers preserve snapshot row tooltip truncation and revision",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var v=s.Card;var snapshot=v.Snapshot;var display=v.Display;
            var row=display.Row(snapshot.Slaves[0]);var tooltip=row.Tooltip;v.Truncate("Very long text",12);
            int revision=v.Revision,calls=UIExtensions.Truncations;
            for(int i=1;i<=5;i++){Time.realtimeSinceStartup=i*2;s.ObserveCard(f.master,f.map,i);s.Update(i,true,f.map);v.Truncate("Very long text",12);}
            Require(v.Snapshot==snapshot&&v.Display==display&&v.Display.Row(snapshot.Slaves[0])==row&&
                ReferenceEquals(row.Tooltip,tooltip)&&revision==v.Revision&&calls==UIExtensions.Truncations&&ThingOwnerUtility.MapQueryCount==0);
        });
        Check("optimization: merged no-op request still reads locally without discarding caches",()=>{
            var f=Fixture();int builds=0;var s=new SSCBondOverviewSession(Current.Game,()=>0,(p,m)=>{builds++;return SSCBondOverviewUtility.BuildLocal(p,m);});
            var v=s.ObserveCard(f.master,f.map,0);var display=v.Display;for(int i=0;i<20;i++)s.RequestRefresh(v);s.Update(0,true,f.map);
            Require(builds==2&&v.Display==display&&!v.Pending);
        });
        foreach(string change in new[]{"source-name","slave-name","stage","dead","identity","specialization","map","map-name","count","order","owner"})
            Check("optimization: unchanged-reference facts invalidate caches on "+change,()=>{
                var f=Fixture();Pawn other=null;if(change=="order")other=Slave(f.master,"ZZZ");
                var s=Ready(f.master,f.map);var snapshot=s.Card.Snapshot;var display=s.Card.Display;
                if(change=="source-name")f.master.LabelShort="New owner name";
                if(change=="slave-name")f.slave.LabelShort="New slave name";
                if(change=="stage")SSCBondUtility.GetChain(f.slave).Severity=.9f;
                if(change=="dead")f.slave.Dead=true;
                if(change=="identity")f.master.Training.pawnIdentity=PawnIdentity.Unset;
                if(change=="specialization")f.slave.Training.specializationType=SexSlaveSpecializationType.Cow;
                if(change=="map")f.slave.Map=new Map();
                if(change=="map-name")f.map.Parent.LabelCap="Renamed map";
                if(change=="count")Slave(f.master,"Added");
                if(change=="order")other.LabelShort="AAA";
                if(change=="owner")SSCBondUtility.GetChain(f.slave).target=Master();
                Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);
                Require(s.Card.Snapshot!=snapshot&&s.Card.Display!=display&&ThingOwnerUtility.MapQueryCount==0);
            });
        foreach(string change in new[]{"name","state","dead","reference","location"})Check("optimization: trainer invalidation on "+change,()=>{
            var f=Fixture();var trainer=Master();Place(trainer,f.map);f.slave.Training.selectedTrainer=trainer;
            Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);var old=s.Card.Snapshot;var display=s.Card.Display;
            if(change=="name")trainer.LabelShort="Renamed trainer";
            if(change=="state")trainer.Training.TrainerActive=false;
            if(change=="dead")trainer.Dead=true;
            if(change=="reference")f.slave.Training.selectedTrainer=f.master;
            if(change=="location")trainer.Map=new Map();
            Time.realtimeSinceStartup=2;s.ObserveCard(f.slave,f.map,1);s.Update(1,true,f.map);
            Require(s.Card.Snapshot!=old&&s.Card.Display!=display);
        });
        foreach(bool world in new[]{false,true})Check("optimization: location label captured before rename "+world,()=>{
            var f=Fixture();var box=new Container();var caravan=new Caravan();
            f.slave.Spawned=false;if(world)f.slave.ParentHolder=caravan;else{Place(box,f.map);f.slave.ParentHolder=box;}
            var s=Ready(f.master,f.map);var old=s.Card.Snapshot;string label=old.Slaves[0].Location.Name;
            if(world)caravan.LabelCap="New caravan";else box.LabelShort="New box";
            Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);
            Require(old.Slaves[0].Location.Name==label&&s.Card.Snapshot!=old&&s.Card.Snapshot.Slaves[0].Location.Name!=label);
        });
        Check("optimization: world tile value change invalidates same-subject location",()=>{
            var f=Fixture();f.slave.Spawned=false;f.slave.Tile=new(true,1);var s=Ready(f.master,f.map);var old=s.Card.Snapshot;
            f.slave.Tile=new(true,2);Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);
            Require(s.Card.Snapshot!=old&&s.Card.Snapshot.Slaves[0].Location.Kind==SSCBondOverviewLocationKind.WorldTile);
        });
        Check("optimization: same-master chain replacement keeps presentation but rechecks current chain",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var old=s.Card.Snapshot;
            var chain=SSCBondUtility.GetChain(f.slave);f.slave.health.hediffSet.hediffs.Remove(chain);
            f.slave.health.AddHediff(new Hediff_ChainOfSexSlave{def=chain.def,target=f.master,Severity=chain.Severity});
            Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);
            Require(s.Card.Snapshot==old&&SSCBondOverviewNavigation.TryNavigate(old,old.Slaves[0],out _));
            SSCBondUtility.GetChain(f.slave).target=Master();Require(!SSCBondOverviewNavigation.TryNavigate(old,old.Slaves[0],out _));
        });
        Check("optimization: language changes clear complete tooltip while snapshot can remain",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var old=s.Card.Display.Row(s.Card.Snapshot.Slaves[0]).Tooltip;
            Verse.Extensions.Translations["SSC_BondOverview_RowDetails"]="Translated {0} {1} {2}";LanguageDatabase.activeLanguage=new();
            var text=s.Card.Display.Row(s.Card.Snapshot.Slaves[0]);Require(text.Tooltip!=old&&text.Tooltip.Contains("Translated")&&ReferenceEquals(text.Tooltip,text.Tooltip));
        });
        Check("optimization: trainer complete tooltip is cached and content remains equivalent",()=>{
            var f=Fixture();f.slave.Training.selectedTrainer=f.master;var s=Ready(f.slave,f.map);var display=s.Card.Display;
            Require(display.TrainerTooltip==display.TrainerName+"\n"+display.TrainerDetails&&ReferenceEquals(display.TrainerTooltip,display.TrainerTooltip));
        });
        Check("optimization: master and trainer share location within a build only",()=>{
            var f=Fixture();f.slave.Training.selectedTrainer=f.master;var a=SSCBondOverviewUtility.BuildLocal(f.slave,f.map);var b=SSCBondOverviewUtility.BuildLocal(f.slave,f.map);
            Require(ReferenceEquals(a.Master.Location,a.AssignedTrainerLocation)&&!ReferenceEquals(a.Master.Location,b.Master.Location)&&a.SamePresentation(b));
        });
        Check("optimization: trainer who is also a bound member shares the same location",()=>{
            var f=Fixture();f.master.Training.selectedTrainer=f.slave;var view=SSCBondOverviewUtility.BuildLocal(f.master,f.map);
            Require(ReferenceEquals(view.Slaves[0].Location,view.AssignedTrainerLocation));
        });
        foreach(string kind in new[]{"map","world","temporary","caravan","ship","container","dead","duplicate","stale"})
            Check("optimization: diagnostic count matches full query on "+kind,()=>{
                var f=Fixture();var omitted=Slave(f.master,"Omitted");SSCBondUtility.GetBridle(f.master).targets.Remove(omitted);
                if(kind=="map")Place(omitted,f.map);
                if(kind=="world")Find.WorldPawns.AllPawnsAlive.Add(omitted);
                if(kind=="temporary")RimWorld.PawnsFinder.TemporaryPawns.Add(omitted);
                if(kind=="caravan"){var caravan=new Caravan();caravan.Contents.Add(omitted);omitted.ParentHolder=caravan;Find.WorldObjects.AllWorldObjects.Add(caravan);}
                if(kind=="ship"){var ship=new Gravship();ship.Pawns.Add(omitted);Find.CurrentGravship=ship;}
                if(kind=="container"){var box=new Container();Place(box,f.map);box.Contents.Add(omitted);omitted.ParentHolder=box;}
                if(kind=="dead"){omitted.Dead=true;Find.WorldPawns.AllPawnsDead.Add(omitted);}
                if(kind=="duplicate"){Place(omitted,f.map);RimWorld.PawnsFinder.TemporaryPawns.Add(omitted);SSCBondUtility.GetBridle(f.master).targets.Add(f.slave);}
                if(kind=="stale"){Place(omitted,f.map);SSCBondUtility.GetBridle(f.master).targets.Add(omitted);SSCBondUtility.GetChain(omitted).target=Master();}
                var local=SSCBondOverviewUtility.BuildLocal(f.master,f.map);var full=SSCBondOverviewUtility.Build(f.master,f.map);var result=SSCBondOverviewUtility.Diagnose(f.master);
                Require(result.State==SSCBondOverviewQueryState.Ready&&result.IndexedCount==local.TotalSlaveCount&&result.GlobalCount==full.TotalSlaveCount&&result.MissingCount==full.TotalSlaveCount-local.TotalSlaveCount);
            });
    }
}
