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

internal static class Program
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
        Time.frameCount=0;Time.realtimeSinceStartup=0;LanguageDatabase.activeLanguage=new();Log.Errors=0;
        GUI.enabled=true;GUI.color=new(1,1,1);Text.Font=GameFont.Small;Text.Anchor=TextAnchor.UpperLeft;Text.WordWrap=false;
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
    {Event.current=new(){type=EventType.mouseDown,button=button,mousePosition=new(x,y)};return gizmo.GizmoOnGUI(new(0,0),300,default);}

    static int Main(string[] args)
    {
        root=SscTestPaths.RepositoryRoot(args.FirstOrDefault());
        foreach(var path in Directory.GetFiles(SscTestPaths.Combine(root,"Languages/English/Keyed"),"*.xml"))
            foreach(var e in XDocument.Load(path).Root.Elements())english[e.Name.LocalName]=e.Value;
        SessionTests();NavigationTests();GuiTests();ResourceTests();
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
            var f=Fixture();var s=Ready(f.master,f.map);var state=s.Card;s.ObserveCard(f.master,new Map(),1);Require(state.Snapshot==null&&state.Pending);
        });
        Check("session: source change drops old pending data",()=>{
            var p=Master();var s=Ready(p,null);var previous=s.Card;var next=Master();s.ObserveCard(next,null,1);
            Require(previous.Snapshot==null&&s.Card.Source==next&&s.Card.Snapshot==null);
        });
        Check("session: manual requests merge and respect global cooldown",()=>{
            double now=0;int builds=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>{builds++;return View(a);});
            s.ObserveCard(p,null,0);s.Update(0,true,null);for(int i=0;i<20;i++)s.RequestRefresh(s.Card);
            now=.2;s.ObserveCard(p,null,1);s.Update(1,true,null);Require(builds==1);now=.25;s.ObserveCard(p,null,2);s.Update(2,true,null);Require(builds==2);
        });
        Check("session: two sources alternate without duplicate frame builds",()=>{
            double now=0;var order=new List<Pawn>();var a=Master();var b=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(p,m)=>{order.Add(p);return View(p);});
            s.ObserveCard(a,null,0);s.OpenDetails(b,null);s.Update(0,true,null);now=.25;s.ObserveCard(a,null,1);s.Update(1,true,null);
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
        Check("session: exceptions remove old rows and throttle reports",()=>{
            double now=0;int errors=0;var p=Master();var s=new SSCBondOverviewSession(Current.Game,()=>now,(a,m)=>throw new Exception("fixture"),e=>errors++);
            s.ObserveCard(p,null,0);s.Update(0,true,null);Require(s.Card.Failed&&s.Card.Snapshot==null&&errors==1);
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
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);var result=Click(g,20,45);Require(result.State==GizmoState.Interacted);
            g.ProcessInput(result.InteractEvent);g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==1);
        });
        foreach(string state in new[]{"right","disabled","multi","revision","rebound"})Check("gizmo blocks stale or non-left action "+state,()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);if(state=="disabled")GUI.enabled=false;
            var result=Click(g,20,45,state=="right"?1:0);
            if(state=="multi")Find.Selector.Objects.Add(new Thing());if(state=="revision")s.Card.Changed();if(state=="rebound")SSCBondUtility.GetChain(f.slave).target=Master();
            g.ProcessInput(result.InteractEvent);Require(CameraJumper.Jumps==0);
        });
        Check("gizmo: view-all is separate from portrait and reuses same window",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);var result=Click(g,250,10);g.ProcessInput(result.InteractEvent);
            var window=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();Require(window?.Source==f.master&&CameraJumper.Jumps==0);
            Dialog_SSCBondOverview.Open(s,f.master);Require(Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>()==window);window.Close();Require(s.Details==null);
        });
        Check("window: replacement keeps new source subscription",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var previous=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            Dialog_SSCBondOverview.Open(s,f.slave);Require(previous.Source==null&&s.Details.Source==f.slave);
        });
        Check("window: source stays pinned when selection changes",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);
            var window=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();window.WindowUpdate();Require(window.Source==f.master&&s.Details.Source==f.master&&!window.forcePause);
        });
        Check("window: cross-game closes and releases all view references",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            Current.Game=new();w.WindowUpdate();Require(w.Source==null&&s.Card==null&&s.Details==null);
        });
        Check("window: source death retains pinned view with death label",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);f.master.Dead=true;
            Time.realtimeSinceStartup=2;s.Update(1,false,f.map);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();w.WindowUpdate();
            Require(w.Source==f.master&&s.Details.Display.Title.Contains("dead"));
        });
        Check("window: successful navigation closes and releases subscription",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            // 替身的滚动区域不改变鼠标坐标，直接使用内容局部坐标验证动作分派。
            Event.current=new(){type=EventType.mouseDown,mousePosition=new(660,140)};w.DoWindowContents(new(0,0,760,500));
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
        Check("window: 500 rows only request visible fixed-size portraits including final row",()=>{
            var f=Fixture();for(int i=0;i<499;i++)Place(Slave(f.master,i.ToString("D4")),f.map);var s=Ready(f.master,f.map);Dialog_SSCBondOverview.Open(s,f.master);
            var w=Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();w.DoWindowContents(new(0,0,760,500));Require(PortraitsCache.Calls<12&&PortraitsCache.Requests.All(r=>r.size.x==48&&r.size.y==48));
            var last=s.Details.Snapshot.Slaves[^1].Pawn;Widgets.NextScroll=100000;w.DoWindowContents(new(0,0,760,500));PortraitsCache.Requests.Clear();w.DoWindowContents(new(0,0,760,500));
            Require(PortraitsCache.Requests.Any(r=>r.pawn==last)&&Widgets.ScrollDepth==0);
        });
        Check("gizmo: fixed 40 portraits and dual-direction preview",()=>{
            var f=Fixture();for(int i=0;i<8;i++)Place(Slave(f.master,i.ToString()),f.map);
            // 正常 Bind 不制造双向异常；直接构造旧档式锁链，验证界面仍保留两侧事实。
            f.master.health.AddHediff(new Hediff_ChainOfSexSlave{def=SSCDefOf.ChainOfSexSlave,target=Master()});
            var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);Paint(g);Require(PortraitsCache.Calls==5&&PortraitsCache.Requests.All(r=>r.size.x==40&&r.size.y==40));
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
            SSCBondOverviewLayout.VisibleRange(count,100,100+count*64-1,100,out int first,out int end);Require(first>=0&&end<=count&&end-first<6&&end==count);
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
