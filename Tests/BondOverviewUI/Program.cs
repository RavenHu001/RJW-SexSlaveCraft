// AI 辅助生成。直接运行生产会话、导航和 GUI 入口，以确定性计数验证刷新与输入边界。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    static int cases, failures;
    static string root;
    static readonly Dictionary<string,string> english = new();
    static void Require(bool value,string reason="assertion failed"){if(!value)throw new Exception(reason);}
    static void Check(string name,Action action)
    {
        cases++; Reset();
        try{action();Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex);}
    }
    static void Reset()
    {
        Current.Game=new Game();Current.ProgramState=ProgramState.Playing;Scribe.mode=LoadSaveMode.Inactive;
        Find.Maps.Clear();Find.World=new object();Find.WorldObjects=new();Find.WorldPawns=new();Find.CurrentGravship=null;
        Find.Selector=new();Find.WindowStack=new();Find.CurrentMap=null;
        PawnsFinder.TemporaryPawns.Clear();PawnsFinder.TransportMembers.Clear();SSCRestrictionGameComponent.Pawns.Clear();
        ThingOwnerUtility.Visits.Clear();ThingOwnerUtility.MapQueryCount=0;
        CameraJumper.Jumps=CameraJumper.Selects=CameraJumper.Checks=0;CameraJumper.Allow=true;
        PortraitsCache.Calls=0;PortraitsCache.Requests.Clear();TargetHighlighter.Calls=0;
        Widgets.Labels=Widgets.ScrollDepth=Widgets.ThrowAt=0;Widgets.NextScroll=null;Widgets.ThrowLabel=false;
        Widgets.CapturedLabels=null;Widgets.CapturedButtons=null;GUI.CapturedTextures=null;Widgets.ResetButtonCapture();
        Time.frameCount=0;Time.realtimeSinceStartup=0;LanguageDatabase.activeLanguage=new();Log.Errors=0;
        GUI.enabled=true;GUI.color=new(1,1,1);Text.Font=GameFont.Small;Text.Anchor=TextAnchor.UpperLeft;Text.WordWrap=false;
        Prefs.UIScale=1;UIExtensions.Truncations=0;
        TrainerAssignmentUtility.Candidates.Clear();TrainerAssignmentUtility.CandidateQueries=0;TrainerAssignmentUtility.AssignmentCheck=(a,b)=>true;
        SSCRestrictionTrainerAssignment.AllowAssign=SSCRestrictionTrainerAssignment.AllowClear=true;
        Event.current=new(){type=EventType.repaint,mousePosition=new(-100,-100)};
        Verse.Extensions.Translations.Clear();foreach(var pair in english)Verse.Extensions.Translations[pair.Key]=pair.Value;
    }
    static Pawn Master()=>new(){Training={pawnIdentity=PawnIdentity.Master}};
    static Pawn Slave(Pawn master,string name="Slave")
    {var pawn=new Pawn{LabelShort=name};Require(SSCBondUtility.Bind(master,pawn));return pawn;}
    static void Place(Thing thing,Map map){thing.Spawned=true;thing.Map=map;map.Things.Add(thing);}
    static (Pawn master,Pawn slave,Map map) Fixture()
    {
        var master=Master();var slave=Slave(master);var map=new Map();Find.Maps.Add(map);Find.CurrentMap=map;
        Place(master,map);Place(slave,map);Find.Selector.Objects.Add(master);return(master,slave,map);
    }
    static SSCBondOverviewSnapshot View(Pawn source,params Pawn[] candidates)=>SSCBondOverviewUtility.BuildFromCandidates(source,Find.CurrentMap,candidates);
    static SSCBondOverviewSession Ready(Pawn source,Map map)
    {var session=new SSCBondOverviewSession(Current.Game,()=>Time.realtimeSinceStartup);session.ObserveCard(source,map,0);session.Update(0,true,map);return session;}
    static void Paint(Gizmo gizmo,float width=300)=>gizmo.GizmoOnGUI(new(0,0),width,default);
    static GizmoResult Click(Gizmo gizmo,float x,float y,int button=0)
    {
        Event.current=new(){type=EventType.mouseDown,button=button,mousePosition=new(x,y)};
        var down=gizmo.GizmoOnGUI(new(0,0),300,default);if(down.State==GizmoState.Interacted)return down;
        Event.current=new(){type=EventType.mouseUp,button=button,mousePosition=new(x,y)};
        return gizmo.GizmoOnGUI(new(0,0),300,default);
    }

    static int Main(string[] args)
    {
        root=SscTestPaths.RepositoryRoot(args.FirstOrDefault());
        foreach(var path in Directory.GetFiles(SscTestPaths.Combine(root,"Languages/English/Keyed"),"*.xml"))
            foreach(var e in XDocument.Load(path).Root.Elements())english[e.Name.LocalName]=e.Value;
        SessionTests();NavigationTests();GuiTests();CacheTests();RevisionTests();DirectReadTests();IconButtonTests();ResourceTests();
        Console.WriteLine($"{cases-failures}/{cases} passed");return failures==0?0:1;
    }

    // 调度用测试时钟推进，断言实际构建次数，不以机器耗时阈值替代限频语义。
    static void SessionTests()
    {
        Check("session: observe and repeat GUI never build",()=>{
            int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>0,(a,m)=>{builds++;return View(a);});
            for(int i=0;i<100;i++)s.ObserveCard(p,null,0);Require(builds==0);s.Update(0,true,null);Require(builds==1);
            for(int i=0;i<100;i++){s.ObserveCard(p,null,0);s.Update(0,true,null);}Require(builds==1);
        });
        Check("session: same source card and details share one query",()=>{
            int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>0,(a,m)=>{builds++;return View(a);});
            var card=s.ObserveCard(p,null,0);Require(ReferenceEquals(card,s.OpenDetails(p,null)));s.Update(0,true,null);Require(builds==1&&s.Card.Snapshot==s.Details.Snapshot);
        });
        foreach(bool negative in new[]{false,true})Check("session: real-time interval "+(negative?5:2),()=>{
            double now=0;int builds=0;var p=negative?new Pawn():Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);now=negative?4.99:1.99;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(builds==1);
            now=negative?5:2;s.ObserveCard(p,null,2);s.Update(2,true,null);Require(builds==2&&s.Card.ShowCard==!negative);
        });
        Check("session: empty identity missing reverse index is discovered",()=>{
            var f=Fixture();f.master.Training.pawnIdentity=PawnIdentity.Unset;SSCBondUtility.GetBridle(f.master).targets.Clear();
            var s=Ready(f.master,f.map);Require(s.Card.ShowCard&&s.Card.Snapshot.TotalSlaveCount==1);
        });
        Check("session: card disappearance releases cache while details stays pinned",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var state=s.OpenDetails(f.master,f.map);s.Update(3,false,f.map);
            Require(s.Card==null&&s.Details==state&&state.Snapshot!=null);s.ReleaseDetails();Require(s.Details==null&&state.Snapshot==null);
        });
        Check("session: no heartbeat expires after two full frames",()=>{
            var p=Master();var s=Ready(p,null);var old=s.Card;s.Update(3,true,null);Require(s.Card==null&&old.Snapshot==null);
        });
        Check("session: map change never serves old map snapshot",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var state=s.Card;s.ObserveCard(f.master,new Map(),1);Require(state.Snapshot?.ViewedMap!=f.map&&state.Snapshot!=null&&state.Pending);
        });
        Check("session: source change drops old pending data",()=>{
            var p=Master();var s=Ready(p,null);var previous=s.Card;var next=Master();s.ObserveCard(next,null,1);
            Require(previous.Snapshot==null&&s.Card.Source==next&&s.Card.Snapshot?.Source==next);
        });
        Check("session: manual requests merge and respect global cooldown",()=>{
            double now=0;int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);for(int i=0;i<20;i++)s.RequestRefresh(s.Card);
            now=1.99;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(builds==1&&s.Card.Pending);now=2;s.ObserveCard(p,null,2);s.Update(2,true,null);Require(builds==2&&!s.Card.Pending);
        });
        Check("session: two sources alternate without duplicate frame builds",()=>{
            double now=0;var order=new List<Pawn>();var a=Master();var b=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{order.Add(p);return View(p);});
            s.ObserveCard(a,null,0);s.OpenDetails(b,null);s.Update(0,true,null);now=2;s.ObserveCard(a,null,1);s.Update(1,true,null);
            Require(order.Count==2&&order.Distinct().Count()==2);
        });
        Check("session: slow query backs off without catch-up",()=>{
            double now=0;int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;now+=1;return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);Require(s.Card.NextRefresh==11);now=8;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(builds==1);
            now=40;s.ObserveCard(p,null,2);s.Update(2,true,null);Require(builds==2);
        });
        Check("session: clock rollback resets deadlines",()=>{
            double now=100;int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);now=1;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(builds==2);
        });
        Check("session: supplement exceptions preserve local rows and throttle reports",()=>{
            double now=0;int errors=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>throw new Exception("fixture"),e=>errors++);
            s.ObserveCard(p,null,0);s.Update(0,true,null);Require(s.Card.Failed&&s.Card.Snapshot?.State==SSCBondOverviewQueryState.Ready&&errors==1);
            now=2;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(errors==1);
        });
        foreach(bool loading in new[]{false,true})Check("session: clear on "+(loading?"loading":"cross game"),()=>{
            var p=Master();var s=Ready(p,null);var view=s.OpenDetails(p,null);if(loading)Scribe.mode=LoadSaveMode.PostLoadInit;else Current.Game=new();
            s.Update(1,true,null);Require(s.Card==null&&s.Details==null&&view.Snapshot==null);
        });
        Check("session: destroyed window source does not block another card",()=>{
            double now=0;int builds=0;var a=Master();var b=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{builds++;return View(p);});
            s.ObserveCard(a,null,0);s.OpenDetails(b,null);b.Destroyed=true;s.Update(0,true,null);Require(builds==1&&s.Card.Snapshot!=null);
        });
        Check("session: retained empty session does not retain released pawn",()=>{
            var pair=Released();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Require(!pair.weak.IsAlive);GC.KeepAlive(pair.session);
        });
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static (SSCBondOverviewSession session,WeakReference weak) Released()
    {var p=new Pawn();var weak=new WeakReference(p);var s=Ready(p,null);s.Clear();return(s,weak);}

    // 构造真实锁链和旧快照，再移动或改绑对象，观察生产导航最终传给相机的目标。
    static void NavigationTests()
    {
        Check("navigation: map select and cross-map move use current target",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);var other=new Map();Find.Maps.Add(other);f.slave.Map=other;
            int scans=ThingOwnerUtility.MapQueryCount;Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _));
            Require(CameraJumper.Last.Thing==f.slave&&CameraJumper.Selects==1&&ThingOwnerUtility.MapQueryCount==scans);
        });
        Check("navigation: map container is selected instead of held pawn",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);var box=new Container();Place(box,f.map);f.slave.Spawned=false;f.slave.ParentHolder=box;box.Contents.Add(f.slave);
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _));Require(CameraJumper.Last.Thing==box);
        });
        Check("navigation: deceased corpse and revival are re-resolved",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);var corpse=new Corpse{InnerPawn=f.slave};Place(corpse,f.map);f.slave.Dead=true;f.slave.Spawned=false;f.slave.ParentHolder=corpse;
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _)&&CameraJumper.Last.Thing==corpse);
            f.slave.Dead=false;f.slave.ParentHolder=null;f.slave.Spawned=true;Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _)&&CameraJumper.Last.Thing==f.slave);
        });
        foreach(bool selectable in new[]{false,true})Check("navigation: world object selectable "+selectable,()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);var caravan=new Caravan{SelectableNow=selectable};f.slave.Spawned=false;f.slave.ParentHolder=caravan;
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _));Require(CameraJumper.Last.WorldObject==caravan&&CameraJumper.Selects==(selectable?1:0));
        });
        Check("navigation: world tile dispatches jump only",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);f.slave.Spawned=false;f.slave.Tile=new(true);
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _)&&CameraJumper.Last.Tile.Valid&&CameraJumper.Selects==0);
        });
        Check("navigation: ship A to B scans each cargo list only once on click",()=>{
            var f=Fixture();f.slave.Spawned=false;var a=new Gravship();a.Pawns.Add(f.slave);Find.CurrentGravship=a;var snapshot=View(f.master,f.slave);
            a.Pawns.Remove(f.slave);var b=new Gravship();b.Pawns.Add(f.slave);Find.CurrentGravship=b;Find.WorldObjects.AllWorldObjects.Add(a);
            for(int i=0;i<20000;i++)b.Things.Add(new Thing());a.Things.Enumerations=b.Things.Enumerations=0;
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _));Require(CameraJumper.Last.WorldObject==b);
            Require(a.Things.Enumerations==1&&b.Things.Enumerations==1);
        });
        Check("navigation: component parent retains container location",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);var box=new Thing();Place(box,f.map);f.slave.Spawned=false;f.slave.ParentHolder=new ThingComp{parent=box};
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _)&&CameraJumper.Last.Thing==box);
        });
        foreach(string reason in new[]{"cycle","unknown","rebound","foreign","destroyed","game","loading","cannot"})Check("navigation rejects "+reason,()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);var entry=snapshot.Slaves[0];
            switch(reason){
                case "cycle":var box=new Container();box.ParentHolder=box;f.slave.Spawned=false;f.slave.ParentHolder=box;break;
                case "unknown":f.slave.Spawned=false;break;
                case "rebound":SSCBondUtility.GetChain(f.slave).target=Master();break;
                case "foreign":entry=View(f.master,f.slave).Slaves[0];break;
                case "destroyed":f.master.Destroyed=true;break;
                case "game":Current.Game=new();break;
                case "loading":Scribe.mode=LoadSaveMode.PostLoadInit;break;
                case "cannot":CameraJumper.Allow=false;break;
            }
            Require(!SSCBondOverviewNavigation.TryNavigate(snapshot,entry,out var failure)&&failure!=SSCBondOverviewNavigationFailure.None&&CameraJumper.Jumps==0);
        });
        Check("navigation: same master replacement chain remains valid",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);f.slave.health.hediffSet.hediffs.Remove(SSCBondUtility.GetChain(f.slave));
            f.slave.health.AddHediff(new Hediff_ChainOfSexSlave{def=SSCDefOf.ChainOfSexSlave,target=f.master});
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _));
        });
        Check("navigation: non-selectable map target dispatches only jump",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);f.slave.def.selectable=false;
            Require(SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _)&&CameraJumper.Jumps==1&&CameraJumper.Selects==0);
        });
        Check("navigation: despawned world object is unavailable",()=>{
            var f=Fixture();var snapshot=View(f.master,f.slave);f.slave.Spawned=false;f.slave.ParentHolder=new Caravan{Spawned=false};
            Require(!SSCBondOverviewNavigation.TryNavigate(snapshot,snapshot.Slaves[0],out _)&&CameraJumper.Jumps==0);
        });
    }

    // 运行生产 GUI 和 ProcessInput；替身只记录画像、按钮事件与相机分派。
    static void GuiTests()
    {
        Check("entry: ordinary mode identity and actual bindings are visible",()=>{
            var f=Fixture();var game=Current.Game.GetComponent<SSCBondRelationGameComponent>();var gizmo=f.master.Training.BondOverviewGizmo(f.master);
            Require(gizmo!=null);game.GameComponentUpdate();Require(game.Overview.Card.Snapshot.TotalSlaveCount==1);
        });
        Check("entry: ordinary empty identity has no card but is probed",()=>{
            var f=Fixture();var p=new Pawn();Place(p,f.map);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(p);
            Require(p.Training.BondOverviewGizmo(p)==null);var game=Current.Game.GetComponent<SSCBondRelationGameComponent>();game.GameComponentUpdate();Require(game.Overview.Card.Snapshot!=null&&!game.Overview.Card.ShowCard);
        });
        foreach(string invalid in new[]{"multi","corpse","destroyed","offmap","component","other-map"})Check("entry rejects "+invalid,()=>{
            var f=Fixture();switch(invalid){case "multi":Find.Selector.Objects.Add(new Thing());break;case "corpse":f.master.Dead=true;break;case "destroyed":f.master.Destroyed=true;break;case "offmap":f.master.Spawned=false;break;case "component":f.master.Training=null;break;case "other-map":Find.CurrentMap=new();break;}
            Require(!SSCBondOverviewAccess.IsCardSource(f.master));
        });
        Check("gizmo: repeated paint neither builds nor scans cargo",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var gizmo=new Gizmo_SSCBondOverview(s);var ship=new Gravship();Find.CurrentGravship=ship;ship.Things.Add(new Thing());
            int maps=ThingOwnerUtility.MapQueryCount;for(int i=0;i<100;i++){Event.current.type=i%2==0?EventType.layout:EventType.repaint;Paint(gizmo);}
            Require(ship.Things.Enumerations==0&&ThingOwnerUtility.MapQueryCount==maps);
        });
        Check("gizmo: left click dispatches once and consumes stored action",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);var result=Click(g,65,45);Require(result.State==GizmoState.Interacted);
            g.ProcessInput(result.InteractEvent);g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==1);
        });
        foreach(string state in new[]{"right","disabled","multi","revision","rebound"})Check("gizmo blocks stale or non-left action "+state,()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);if(state=="disabled")GUI.enabled=false;
            var result=Click(g,65,45,state=="right"?1:0);
            if(state=="multi")Find.Selector.Objects.Add(new Thing());if(state=="revision")s.Card.Changed();if(state=="rebound")SSCBondUtility.GetChain(f.slave).target=Master();
            g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
        });
        Check("gizmo: view-all is separate from portrait and reuses same window",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);var result=Click(g,114,14);g.ProcessInput(result.InteractEvent);
            var window=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();Require(window?.Source==f.master&&CameraJumper.Jumps==0);
            Dialog_SSCBondOverview.Open(s,f.master);Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==window);window.Close();Require(s.Details==null);
        });
        Check("window: replacement keeps new source subscription",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var previous=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
            Dialog_SSCBondOverview.Open(s,f.slave);Require(previous.Source==null&&s.Details.Source==f.slave);
        });
        Check("window: source change closes overview and releases details",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
            var window=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();window.WindowUpdate();Require(window.Source==null&&s.Details==null&&!window.forcePause);
        });
        Check("window: cross-game closes and releases all view references",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            Current.Game=new();w.WindowUpdate();Require(w.Source==null&&s.Card==null&&s.Details==null);
        });
        Check("window: source death closes current-selection overview",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);f.master.Dead=true;
            Time.realtimeSinceStartup=2;s.Update(1,false,f.map);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();w.WindowUpdate();
            Require(w.Source==null&&s.Details==null);
        });
        Check("window: successful navigation closes and releases subscription",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            // 替身的滚动区域不改变鼠标坐标，直接使用内容局部坐标验证动作分派。
            Event.current=new(){type=EventType.mouseDown,mousePosition=new(20,45)};w.DoWindowContents(new(0,0,760,500));
            Require(CameraJumper.Jumps==1&&!w.IsOpen&&s.Details==null);
        });
        Check("window: scroll view exits even if row drawing throws",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            Widgets.ThrowAt=3;try{w.DoWindowContents(new(0,0,760,500));}catch(InvalidOperationException){}Require(Widgets.ScrollDepth==0);
        });
        Check("window: clamped initial size respects small UI screen",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);int oldW=UI.screenWidth,oldH=UI.screenHeight;
            try{UI.screenWidth=640;UI.screenHeight=480;var w=new Dialog_SSCBondOverview(s,f.master);Require(w.InitialSize.x<=608&&w.InitialSize.y<=448);}finally{UI.screenWidth=oldW;UI.screenHeight=oldH;}
        });
        Check("window: 500 portraits clip visible grid and final member remains reachable",()=>{
            var f=Fixture();for(int i=0;i<499;i++)Place(Slave(f.master,i.ToString("D4")),f.map);var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);
            var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();w.DoWindowContents(new(0,0,760,500));Require(PortraitsCache.Calls<=63&&PortraitsCache.Requests.All(r=>r.size.x==56&&r.size.y==56));
            var last=s.Details.Snapshot.Slaves[^1].Pawn;Widgets.NextScroll=100000;w.DoWindowContents(new(0,0,760,500));PortraitsCache.Requests.Clear();w.DoWindowContents(new(0,0,760,500));
            Require(PortraitsCache.Requests.Any(r=>r.pawn==last)&&Widgets.ScrollDepth==0);
        });
        Check("gizmo: fixed 40 portraits and dual-direction preview",()=>{
            var f=Fixture();for(int i=0;i<8;i++)Place(Slave(f.master,i.ToString()),f.map);
            // 正常 Bind 不制造双向异常；直接构造旧档式锁链，验证界面仍保留两侧事实。
            f.master.health.AddHediff(new Hediff_ChainOfSexSlave{def=SSCDefOf.ChainOfSexSlave,target=Master()});
            var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);Paint(g);Require(PortraitsCache.Calls==2&&PortraitsCache.Requests.All(r=>r.size.x==40&&r.size.y==40));
            Require(!g.GroupsWith(g)&&!g.InheritInteractionsFrom(g));
        });
        Check("hover: cycle after container moved is safe and never scans ship",()=>{
            var f=Fixture();f.slave.Spawned=false;var box=new Container();Place(box,f.map);f.slave.ParentHolder=box;var s=Ready(f.master,f.map);
            box.Spawned=false;box.ParentHolder=box;var ship=new Gravship();Find.CurrentGravship=ship;
            SSCBondOverviewNavigation.Highlight(s.Card.Snapshot.Slaves[0].Location);Require(TargetHighlighter.Calls==0&&ship.Things.Enumerations==0);
        });
        Check("GUI: exception restores caller state",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);GUI.color=new(.1f,.2f,.3f);Text.Font=GameFont.Tiny;Text.Anchor=TextAnchor.MiddleCenter;Text.WordWrap=true;
            Widgets.ThrowLabel=true;try{Paint(g);}catch(InvalidOperationException){}Require(GUI.color.r==.1f&&Text.Font==GameFont.Tiny&&Text.Anchor==TextAnchor.MiddleCenter&&Text.WordWrap);
        });
        foreach(int count in new[]{0,1,1000})Check("layout: clipped range and end reach "+count,()=>{
            SSCBondOverviewLayout.VisibleRange(count,100,100+count*SSCBondOverviewLayout.RowHeight-1,100,out int first,out int end);Require(first>=0&&end<=count&&end-first<6&&end==count);
        });
        foreach(float width in new[]{1f,40f,55f,300f})Check("layout: card capacity fits width "+width,()=>{
            int capacity=SSCBondOverviewLayout.CardCapacity(width);Require(capacity>=0&&capacity<=5&&(capacity==0||6+(capacity-1)*44+40<=width));
        });
        Check("display: language switch rebuilds text and component absence differs from None",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var first=s.Card.Display;LanguageDatabase.activeLanguage=new();Require(s.Card.Display!=first);
            Require(SSCBondOverviewText.Specialization(null)!=SSCBondOverviewText.Specialization(SexSlaveSpecializationType.None));
        });
        Check("UI: opening painting hovering and closing never mutates saved bond state",()=>{
            var f=Fixture();var chain=SSCBondUtility.GetChain(f.slave);var bridle=SSCBondUtility.GetBridle(f.master);var original=bridle.targets.ToArray();
            var identity=f.slave.Training.pawnIdentity;var trainer=f.slave.Training.selectedTrainer;var relations=f.slave.relations.CacheNotifications;
            var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);Paint(g);Dialog_SSCBondOverview.Open(s,f.master);
            var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();w.DoWindowContents(new(0,0,760,500));w.Close();
            Require(SSCBondUtility.GetChain(f.slave)==chain&&chain.target==f.master&&original.SequenceEqual(bridle.targets)&&
                identity==f.slave.Training.pawnIdentity&&trainer==f.slave.Training.selectedTrainer&&relations==f.slave.relations.CacheNotifications);
        });
    }

    // 验证展示缓存的结果、工作量、容量及失效边界，并用受控时钟检查所有刷新原因。
    static void CacheTests()
    {
        Check("cache: cold display keeps every candidate without formatting rows",()=>{
            var f=Fixture();for(int i=0;i<1000;i++){var p=Slave(f.master,"Extra"+i);Place(p,f.map);}
            var s=Ready(f.master,f.map);var display=s.Card.Display;
            Require(display.Snapshot.TotalSlaveCount==1001&&display.RowCount==0);
            Paint(new Gizmo_SSCBondOverview(s));Require(display.RowCount==0);
            Dialog_SSCBondOverview.Open(s,f.master);Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>().DoWindowContents(new(0,0,760,500));
            Require(display.RowCount>0&&display.RowCount<=64&&display.Snapshot.TotalSlaveCount==1001);
        });
        Check("cache: hovered card formats only the hovered row",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Event.current.mousePosition=new(65,50);Paint(new Gizmo_SSCBondOverview(s));
            Require(s.Card.Display.RowCount==1&&ThingOwnerUtility.MapQueryCount==1);
        });
        Check("cache: row text matches full immutable snapshot fields and reuses object",()=>{
            var f=Fixture();f.slave.Dead=true;var s=Ready(f.master,f.map);var entry=s.Card.Snapshot.Slaves[0];var d=s.Card.Display;var text=d.Row(entry);
            Require(text.Name==SSCBondOverviewText.Format("DeadName",entry.Name));
            Require(text.Details==SSCBondOverviewText.Format("RowDetails",entry.ChainStage,SSCBondOverviewText.Specialization(entry.Specialization),SSCBondOverviewText.Location(entry.Location)));
            Require(ReferenceEquals(text,d.Row(entry)));
        });
        Check("cache: row FIFO stays bounded and evicted rows regenerate",()=>{
            var f=Fixture();for(int i=0;i<100;i++){var p=Slave(f.master,"Extra"+i);Place(p,f.map);}
            var s=Ready(f.master,f.map);var d=s.Card.Display;var first=d.Row(d.Snapshot.Slaves[0]);
            foreach(var e in d.Snapshot.Slaves)d.Row(e);
            Require(d.RowCount==64&&!ReferenceEquals(first,d.Row(d.Snapshot.Slaves[0]))&&d.RowCount==64);
        });
        Check("cache: truncation hits retain native result including narrow widths",()=>{
            var f=Fixture();var view=Ready(f.master,f.map).Card;string name=new('X',160);
            foreach(float width in new[]{1f,40f,200f,2000f}){
                string expected=name.Truncate(width);int calls=UIExtensions.Truncations;
                Require(view.Truncate(name,width)==expected&&UIExtensions.Truncations==calls+1);
                Require(view.Truncate(name,width)==expected&&UIExtensions.Truncations==calls+1);
            }
        });
        Check("cache: width font scale and language distinguish truncation keys",()=>{
            var f=Fixture();var view=Ready(f.master,f.map).Card;string name=new('X',160);
            view.Truncate(name,40);view.Truncate(name,80);Text.Font=GameFont.Tiny;view.Truncate(name,80);
            Prefs.UIScale=1.25f;view.Truncate(name,80);Require(UIExtensions.Truncations==4&&view.TruncatedCount==4);
            view.Truncate(name,80);Require(UIExtensions.Truncations==4);
            LanguageDatabase.activeLanguage=new();view.Truncate(name,80);Require(UIExtensions.Truncations==5&&view.TruncatedCount==1);
        });
        Check("cache: arbitrary widths remain bounded",()=>{
            var f=Fixture();var view=Ready(f.master,f.map).Card;string name=new('X',160);
            for(int i=0;i<1000;i++)view.Truncate(name,i+1);
            int calls=UIExtensions.Truncations;Require(view.TruncatedCount==256);
            view.Truncate(name,1);Require(UIExtensions.Truncations==calls+1&&view.TruncatedCount==256);
        });
        foreach(string change in new[]{"revision","map","source","release"})Check("cache: clears presentation on "+change,()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var v=s.Card;var d=v.Display;d.Row(v.Snapshot.Slaves[0]);v.Truncate("Name",20);
            if(change=="revision")v.Changed();else if(change=="map")v.SetMap(new());else if(change=="source")s.ObserveCard(Master(),f.map,1);else s.Clear();
            Require(v.TruncatedCount==0&&(change=="revision"?v.Display!=d&&v.Display.RowCount==0:v.Display==null));
        });
        Check("cache: language switch renews templates and row text",()=>{
            var f=Fixture();var v=Ready(f.master,f.map).Card;var d=v.Display;string old=d.Count;d.Row(v.Snapshot.Slaves[0]);
            Verse.Extensions.Translations["SSC_BondOverview_Counts"]="Translated {0}/{1}";LanguageDatabase.activeLanguage=new();
            Require(v.Display!=d&&v.Display.RowCount==0&&v.Display.Count!=old&&v.Display.Count.StartsWith("Translated"));
        });
        Check("cache: warm window never repeats native truncation",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            w.DoWindowContents(new(0,0,760,500));int calls=UIExtensions.Truncations;
            for(int i=0;i<100;i++)w.DoWindowContents(new(0,0,760,500));Require(UIExtensions.Truncations==calls);
            w.DoWindowContents(new(0,0,360,500));Require(UIExtensions.Truncations>calls);
        });
        foreach(string reason in new[]{"manual","source","reopen","map"})Check("budget: repeated "+reason+" cannot bypass shared deadline",()=>{
            double now=0;int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;return View(a);});
            for(int frame=0;frame<=500;frame++){
                now=frame*.01;
                if(reason=="source")p=Master();if(reason=="reopen")s.Clear();
                s.ObserveCard(p,reason=="map"?new Map():null,frame);if(reason=="manual")s.RequestRefresh(s.Card);
                s.Update(frame,true,s.Card.Map);
            }
            Require(builds==3);
        });
        Check("budget: pending negative result respects five second interval",()=>{
            double now=0;int builds=0;var p=new Pawn();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);s.RequestRefresh(s.Card);now=2;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(builds==1&&s.Card.Pending);
            now=5;s.ObserveCard(p,null,2);s.Update(2,true,null);Require(builds==2&&!s.Card.Pending);
        });
        foreach(bool fail in new[]{false,true})Check("budget: slow "+(fail?"failure":"query")+" also gates new source and caps backoff",()=>{
            double now=0;int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;now+=4;if(fail)throw new Exception();return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);Require(s.Card.NextRefresh==34);
            p=Master();now=33.99;s.ObserveCard(p,null,1);s.RequestRefresh(s.Card);s.Update(1,true,null);Require(builds==1&&s.Card.Snapshot?.Source==p);
            now=34;s.ObserveCard(p,null,2);s.Update(2,true,null);Require(builds==2);
        });
        Check("budget: two pending sources remain fair under sustained requests",()=>{
            double now=0;var order=new List<Pawn>();var a=Master();var b=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{order.Add(p);return View(p);});
            s.OpenDetails(b,null);for(int frame=0;frame<=600;frame++){
                now=frame*.01;s.ObserveCard(a,null,frame);s.RequestRefresh(s.Card);s.RequestRefresh(s.Details);s.Update(frame,true,null);s.Update(frame,true,null);
            }
            Require(order.SequenceEqual(new[]{a,b,a,b}));
        });
        Check("budget: stale cached row requests refresh but still rejects rebound navigation",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var snap=s.Card.Snapshot;s.Card.Display.Row(snap.Slaves[0]);
            SSCBondUtility.GetChain(f.slave).target=Master();s.RequestRefresh(s.Card);
            Require(!SSCBondOverviewNavigation.TryNavigate(snap,snap.Slaves[0],out _)&&CameraJumper.Jumps==0&&s.Card.Pending);
        });
    }

    // 分页、独立关系区域与显式写入口；授权策略仍用真实后端专项验证，本宿主控制边界返回值。
    static void RevisionTests()
    {
        Check("paging: every member is reachable with fixed portraits and zero additional queries",()=>{
            var f=Fixture();for(int i=0;i<16;i++)Place(Slave(f.master,"P"+i.ToString("D2")),f.map);
            var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);var seen=new HashSet<Pawn>();int maps=ThingOwnerUtility.MapQueryCount;
            for(int page=0;page<9;page++){
                Event.current=new(){type=EventType.repaint,mousePosition=new(-100,-100)};PortraitsCache.Requests.Clear();Paint(g);
                foreach(var request in PortraitsCache.Requests){seen.Add(request.pawn);Require(request.size.x==40);}
                var result=Click(g,118,48);g.ProcessInput(result.InteractEvent);
            }
            Require(seen.SetEquals(s.Card.Snapshot.Slaves.Select(e=>e.Pawn))&&ThingOwnerUtility.MapQueryCount==maps&&s.Card.Page==8);
        });
        Check("paging: previous next bounds navigation and refresh are independent",()=>{
            var f=Fixture();for(int i=0;i<11;i++)Place(Slave(f.master,"P"+i),f.map);var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            var next=Click(g,118,48);g.ProcessInput(next.InteractEvent);Require(s.Card.Page==1&&CameraJumper.Jumps==0);
            Event.current=new(){type=EventType.repaint,mousePosition=new(-100,-100)};s.Card.Changed();Paint(g);Require(s.Card.Page==1);
            var previous=Click(g,12,48);g.ProcessInput(previous.InteractEvent);Require(s.Card.Page==0);
            var first=Click(g,12,48);g.ProcessInput(first.InteractEvent);Require(s.Card.Page==0);
            s.Card.Page=100;Event.current.type=EventType.repaint;Paint(g);Require(s.Card.Page==5);
            foreach(var e in s.Card.Snapshot.Slaves.Skip(1))SSCBondUtility.GetChain(e.Pawn).target=null;
            Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);Paint(g);Require(s.Card.Page==0);
        });
        Check("paging: page click never consumes portrait navigation",()=>{
            var f=Fixture();for(int i=0;i<7;i++)Place(Slave(f.master,"P"+i),f.map);var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            var next=Click(g,118,48);g.ProcessInput(next.InteractEvent);var expected=s.Card.Snapshot.Slaves[2].Pawn;
            var result=Click(g,43,45);g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==1&&CameraJumper.Last.Thing==expected);
            s.ObserveCard(Master(),f.map,1);Require(s.Card.Page==0);
        });
        Check("panels: master and assigned trainer are separate even when same person",()=>{
            var f=Fixture();f.slave.Training.selectedTrainer=f.master;Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
            var game=Current.Game.GetComponent<SSCBondRelationGameComponent>();game.GetOverviewGizmos(f.slave).ToArray();game.GameComponentUpdate();
            var panels=game.GetOverviewGizmos(f.slave).Cast<Gizmo_SSCBondOverview>().ToArray();
            Require(panels.Select(g=>g.Panel).SequenceEqual(new[]{SSCBondOverviewPanel.Master,SSCBondOverviewPanel.Trainer}));
            Require(game.Overview.Card.Snapshot.TotalSlaveCount==0&&game.Overview.Card.Snapshot.Master.Pawn==f.master);
        });
        Check("panels: ordinary master has only slaves panel and no empty directions",()=>{
            var f=Fixture();var game=Current.Game.GetComponent<SSCBondRelationGameComponent>();game.GetOverviewGizmos(f.master).ToArray();game.GameComponentUpdate();
            var panels=game.GetOverviewGizmos(f.master).Cast<Gizmo_SSCBondOverview>().ToArray();Require(panels.Length==1&&panels[0].Panel==SSCBondOverviewPanel.Slaves);
        });
        Check("panels: unset identity retains a raw trainer without adding a master or slave",()=>{
            var f=Fixture();var p=new Pawn();p.Training.selectedTrainer=f.master;Place(p,f.map);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(p);
            var game=Current.Game.GetComponent<SSCBondRelationGameComponent>();game.GetOverviewGizmos(p).ToArray();game.GameComponentUpdate();
            Require(game.GetOverviewGizmos(p).Cast<Gizmo_SSCBondOverview>().Single().Panel==SSCBondOverviewPanel.Trainer&&game.Overview.Card.Snapshot.TotalSlaveCount==0);
        });
        Check("window: three members use compact initial size",()=>{
            var f=Fixture();Place(Slave(f.master,"B"),f.map);Place(Slave(f.master,"C"),f.map);var s=Ready(f.master,f.map);
            var w=new Dialog_SSCBondOverview(s,f.master);Require(w.InitialSize.x<=680&&w.InitialSize.y<=300);
        });
        Check("portrait: fixed larger camera zoom and layout-only events do not render",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);Event.current.type=EventType.layout;Paint(g);Require(PortraitsCache.Calls==0);
            Event.current.type=EventType.repaint;Paint(g);Require(PortraitsCache.LastZoom==1.5f);
        });
        Check("trainer navigation: assigned relation is separately captured without changing bond count",()=>{
            var f=Fixture();var trainer=Master();Place(trainer,f.map);f.slave.Training.selectedTrainer=trainer;
            var snapshot=SSCBondOverviewUtility.Build(f.slave,f.map);Require(snapshot.Master.Pawn==f.master&&snapshot.TotalSlaveCount==0&&snapshot.AssignedTrainerLocation.Subject==trainer);
            Require(SSCBondOverviewNavigation.TryNavigateTrainer(snapshot,out _)&&CameraJumper.Last.Thing==trainer);
        });
        foreach(string change in new[]{"assignment","game","loading","destroyed","self"})Check("trainer navigation rejects stale "+change,()=>{
            var f=Fixture();f.slave.Training.selectedTrainer=f.master;var snapshot=SSCBondOverviewUtility.Build(f.slave,f.map);
            if(change=="assignment")f.slave.Training.selectedTrainer=Master();if(change=="game")Current.Game=new();if(change=="loading")Scribe.mode=LoadSaveMode.PostLoadInit;
            if(change=="destroyed")f.master.Destroyed=true;if(change=="self")f.slave.Training.selectedTrainer=f.slave;
            Require(!SSCBondOverviewNavigation.TryNavigateTrainer(snapshot,out _)&&CameraJumper.Jumps==0);
        });
        Check("trainer location: shares ship index and click resolves new ship",()=>{
            var f=Fixture();var trainer=Master();trainer.Spawned=false;f.slave.Training.selectedTrainer=trainer;
            var old=new Gravship();old.Pawns.Add(trainer);f.master.Spawned=false;old.Pawns.Add(f.master);Find.CurrentGravship=old;for(int i=0;i<200;i++)old.Things.Add(new Thing());
            // 完整 Build 各有一次候选发现和位置索引；主人与调教员共用后者，点击则只有一次位置索引。
            var snapshot=SSCBondOverviewUtility.Build(f.slave,f.map);Require(snapshot.AssignedTrainerLocation.WorldObject==old&&snapshot.Master.Location.WorldObject==old&&
                snapshot.TotalSlaveCount==0&&old.Things.Enumerations==2);
            var moved=new Gravship();old.Pawns.Remove(trainer);moved.Pawns.Add(trainer);Find.WorldObjects.AllWorldObjects.Add(moved);
            old.Things.Enumerations=moved.Things.Enumerations=0;
            Require(SSCBondOverviewNavigation.TryNavigateTrainer(snapshot,out _)&&CameraJumper.Last.WorldObject==moved&&old.Things.Enumerations==1&&moved.Things.Enumerations==1);
        });
        Check("trainer menu: explicit click opens once and candidate enumeration is not a GUI cost",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);TrainerAssignmentUtility.Candidates.Add(f.master);
            var s=Ready(f.slave,f.map);var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Trainer);
            for(int i=0;i<100;i++)Paint(g);Require(TrainerAssignmentUtility.CandidateQueries==0&&f.slave.Training.selectedTrainer==null);
            var result=Click(g,86,14);g.ProcessInput(result.InteractEvent);g.ProcessInput(result.InteractEvent);
            Require(Find.WindowStack.WindowOfType<FloatMenu>()!=null&&TrainerAssignmentUtility.CandidateQueries==1&&f.slave.Training.selectedTrainer==null);
        });
        Check("trainer menu: commit uses shared backend preserves master and queues refresh",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var trainer=Master();Place(trainer,f.map);TrainerAssignmentUtility.Candidates.Add(trainer);
            var s=Ready(f.slave,f.map);var chain=SSCBondUtility.GetChain(f.slave);var options=SSCTrainerAssignmentMenu.Options(f.slave,()=>s.RequestRefresh(s.Card));
            Require(f.slave.Training.selectedTrainer==null);options[0].action();
            Require(f.slave.Training.selectedTrainer==trainer&&chain==SSCBondUtility.GetChain(f.slave)&&chain.target==f.master&&s.Card.Pending);
        });
        foreach(string change in new[]{"component","assignment","owner","identity","candidate","backend","game","loading","context","destroyed"})
            Check("trainer menu: delayed callback rejects "+change,()=>{
                var f=Fixture();var trainer=Master();TrainerAssignmentUtility.Candidates.Add(trainer);bool current=true;var options=SSCTrainerAssignmentMenu.Options(f.slave,current:()=>current);
                if(change=="component")f.slave.Training=new(){pawnIdentity=PawnIdentity.Slave};if(change=="assignment")f.slave.Training.selectedTrainer=f.master;
                if(change=="owner")SSCBondUtility.GetChain(f.slave).target=Master();if(change=="identity")f.slave.Training.pawnIdentity=PawnIdentity.Unset;
                if(change=="candidate")TrainerAssignmentUtility.AssignmentCheck=(a,b)=>false;if(change=="backend")SSCRestrictionTrainerAssignment.AllowAssign=false;
                if(change=="game")Current.Game=new();if(change=="loading")Scribe.mode=LoadSaveMode.PostLoadInit;if(change=="context")current=false;if(change=="destroyed")f.slave.Destroyed=true;
                int messages=Messages.Calls;options[0].action();Require(f.slave.Training.selectedTrainer!=trainer&&Messages.Calls==messages+1);
            });
        Check("trainer menu: clear respects disabled option and successful clear preserves binding",()=>{
            var f=Fixture();f.slave.Training.selectedTrainer=f.master;SSCRestrictionTrainerAssignment.AllowClear=false;
            Require(SSCTrainerAssignmentMenu.Options(f.slave)[^1].action==null);SSCRestrictionTrainerAssignment.AllowClear=true;
            var options=SSCTrainerAssignmentMenu.Options(f.slave);options[^1].action();Require(f.slave.Training.selectedTrainer==null&&SSCBondUtility.GetBoundMaster(f.slave)==f.master);
        });
        Check("trainer menu: external source selection prevents stale gizmo menu submission",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);TrainerAssignmentUtility.Candidates.Add(f.master);
            var s=Ready(f.slave,f.map);Gizmo_SSCBondOverview.OpenTrainerMenu(s,s.Card);var menu=Find.WindowStack.WindowOfType<FloatMenu>();
            Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.master);menu.options[0].action();Require(f.slave.Training.selectedTrainer==null);
        });
        Check("UI labels: master overview has only short count names and no empty directions or row details",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Widgets.CapturedLabels=new();Paint(new Gizmo_SSCBondOverview(s));
            Require(Widgets.CapturedLabels.Count==1&&Widgets.CapturedLabels[0].value==s.Card.Display.OwnedHeading);
            Widgets.CapturedLabels.Clear();Dialog_SSCBondOverview.Open(s,f.master);Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>().DoWindowContents(new(0,0,760,500));
            Require(Widgets.CapturedLabels.Count==2&&Widgets.CapturedLabels[0].value==s.Card.Display.SlaveHeading&&Widgets.CapturedLabels[1].value==f.slave.LabelShort);
        });
        Check("UI labels: narrow trainer header reserves change button space",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            Widgets.CapturedLabels=new();Dialog_SSCBondOverview.Open(s,f.slave);Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>().DoWindowContents(new(0,0,220,500));
            var label=Widgets.CapturedLabels.Single(i=>i.value.StartsWith("Assigned")||i.rect.y==108&&i.rect.x==0);
            Require(label.rect.xMax<=160);
        });
        Check("trainer avatar: explicit click navigates trainer while master avatar navigates owner",()=>{
            var f=Fixture();var trainer=Master();Place(trainer,f.map);f.slave.Training.selectedTrainer=trainer;Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
            var s=Ready(f.slave,f.map);var g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Trainer);var click=Click(g,65,45);g.ProcessInput(click.InteractEvent);
            Require(CameraJumper.Last.Thing==trainer&&CameraJumper.Jumps==1);
            g=new Gizmo_SSCBondOverview(s,SSCBondOverviewPanel.Master);click=Click(g,65,45);g.ProcessInput(click.InteractEvent);Require(CameraJumper.Last.Thing==f.master&&CameraJumper.Jumps==2);
        });
        Check("trainer menu: own change rereads direct relationships and retains refresh deadline",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);TrainerAssignmentUtility.Candidates.Add(f.master);
            var s=Ready(f.slave,f.map);double deadline=s.Card.NextRefresh;Gizmo_SSCBondOverview.OpenTrainerMenu(s,s.Card);
            Find.WindowStack.WindowOfType<FloatMenu>().options[0].action();Require(s.Card.Snapshot?.AssignedTrainer==f.master&&s.Card.Snapshot.Master.Pawn==f.master&&s.Card.Pending&&s.Card.NextRefresh==deadline);
        });
    }

    // 校验发布目录中的实际 XML，并逐字节对照源码镜像，动态枚举状态也必须有键。
    static void ResourceTests()
    {
        var englishXml=XDocument.Load(SscTestPaths.Combine(root,"Languages/English/Keyed/SSC_BondOverview.xml"));
        var reference=englishXml.Root.Elements().ToDictionary(e=>e.Name.LocalName,e=>e.Value);
        foreach(string lang in new[]{"ChineseSimplified","ChineseTraditional","English","Russian"})Check("resources: keys placeholders and mirror "+lang,()=>{
            string path=SscTestPaths.Combine(root,"Languages",lang,"Keyed/SSC_BondOverview.xml");
            var nodes=XDocument.Load(path).Root.Elements().ToArray();Require(nodes.Length==reference.Count&&nodes.Select(e=>e.Name.LocalName).Distinct().Count()==nodes.Length);
            foreach(var node in nodes){Require(reference.ContainsKey(node.Name.LocalName)&&!string.IsNullOrWhiteSpace(node.Value));
                Require(Regex.Matches(reference[node.Name.LocalName],@"\{\d+\}").Select(m=>m.Value).Order().SequenceEqual(Regex.Matches(node.Value,@"\{\d+\}").Select(m=>m.Value).Order()));}
            Require(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(root,"Sexslavecraft/Languages",lang,"Keyed/SSC_BondOverview.xml"))));
        });
        Check("resources: all structured state keys exist",()=>{
            foreach(var value in Enum.GetValues<SSCBondOverviewNavigationFailure>())Require(reference.ContainsKey("SSC_BondOverview_Failure_"+value));
            foreach(var value in Enum.GetValues<SSCBondOverviewLocationKind>())Require(reference.ContainsKey("SSC_BondOverview_Location_"+value));
            foreach(var value in Enum.GetValues<SSCBondOverviewTrainerState>())Require(reference.ContainsKey("SSC_BondOverview_Trainer_"+value));
        });
    }
}
