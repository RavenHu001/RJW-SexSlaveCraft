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
            var s=Ready(f.master,f.map);var g=new Command_SSCBondSelection(s, selectMaster: false);var result=Click(g,35,35);
            g.ProcessInput(result.InteractEvent);g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.Objects.ToHashSet().SetEquals(new object[]{f.slave,valid})&&CameraJumper.Jumps==0);
            Require(SSCBondUtility.GetBoundMaster(f.slave)==f.master&&SSCBondUtility.GetBoundMaster(valid)==f.master&&f.master.Training.selectedTrainer==stranger);
        });
        Check("bulk selection: local map supplements missing bridle before any global scan",()=>{
            var f=Fixture();SSCBondUtility.GetBridle(f.master).targets.Clear();int builds=0;
            var s=new SSCBondOverviewSession(Current.Game,()=>0);
            s.ObserveCard(f.master,f.map);Require(s.Card.Snapshot.TotalSlaveCount==0);
            var g=new Command_SSCBondSelection(s, selectMaster: false);var result=Click(g,35,35);g.ProcessInput(result.InteractEvent);
            Require(builds==0&&ThingOwnerUtility.MapQueryCount==0&&Find.Selector.Objects.Single()==f.slave);
        });
        Check("bulk selection: rebind after painting is excluded at dispatch",()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Command_SSCBondSelection(s, selectMaster: false);
            var result=Click(g,35,35);SSCBondUtility.GetChain(f.slave).target=Master();g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.Objects.Single()==f.master);
        });
        Check("bulk selection: selection notifications cannot select a subsequently rebound candidate",()=>{
            var f=Fixture();var second=Slave(f.master,"Second");Place(second,f.map);var s=Ready(f.master,f.map);
            Find.Selector.OnSelected=p=>{if(p==f.slave)SSCBondUtility.GetChain(second).target=Master();};
            var g=new Command_SSCBondSelection(s, selectMaster: false);var result=Click(g,35,35);g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.Objects.Single()==f.slave);
        });
        Check("bulk selection: native 200 limit leaves complete overview candidates intact",()=>{
            var f=Fixture();for(int i=0;i<249;i++)Place(Slave(f.master,"P"+i),f.map);var s=Ready(f.master,f.map);var snapshot=s.Card.Snapshot;
            var g=new Command_SSCBondSelection(s, selectMaster: false);var result=Click(g,35,35);g.ProcessInput(result.InteractEvent);
            Require(Find.Selector.NumSelected==200&&snapshot.TotalSlaveCount==250&&ReferenceEquals(snapshot,s.Card.Snapshot));
        });
        foreach(string state in new[]{"source","disabled","gui","right","revision"})Check("bulk selection: reject stale or prohibited input "+state,()=>{
            var f=Fixture();var s=Ready(f.master,f.map);var g=new Command_SSCBondSelection(s, selectMaster: false);
            if(state=="disabled")g.Disabled=true;if(state=="gui")GUI.enabled=false;
            var result=Click(g,35,35,state=="right"?1:0);if(state=="revision")s.Card.Changed();
            if(state=="source"){var other=Master();Place(other,f.map);Find.Selector.Objects.Clear();Find.Selector.Objects.Add(other);s.ObserveCard(other,f.map);}
            var before=Find.Selector.Objects.ToArray();g.ProcessInput(result.InteractEvent);Require(Find.Selector.Objects.SequenceEqual(before));
        });
        foreach (bool switchMap in new[] { false, true })
            Check($"native selection: replacing the command after changing {(switchMap ? "map" : "source")} rejects the old release", () =>
            {
                var f = Fixture();
                var component = Current.Game.GetComponent<SSCBondRelationGameComponent>();
                var earlier = component.GetOverviewGizmos(f.master).OfType<Command_SSCBondSelection>().Single();
                ButtonEvent(earlier, EventType.mouseDown, 35, 35);

                Pawn owner = f.master;
                Pawn member = f.slave;
                if (switchMap)
                {
                    var nextMap = new Map();
                    Find.Maps.Add(nextMap);
                    Find.CurrentMap = nextMap;
                    Place(owner, nextMap);
                    Place(member, nextMap);
                }
                else
                {
                    owner = Master();
                    member = Slave(owner);
                    Place(owner, f.map);
                    Place(member, f.map);
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(owner);
                }

                component.GameComponentUpdate();
                var current = component.GetOverviewGizmos(owner).OfType<Command_SSCBondSelection>().Single();
                Require(!earlier.Visible && current != earlier);
                var released = ButtonEvent(current, EventType.mouseUp, 35, 35);
                Require(Event.current.type == EventType.used && released.State != GizmoState.Interacted);
                current.ProcessInput(released.InteractEvent);
                Require(Find.Selector.Objects.Single() == owner);

                var fresh = Click(current, 35, 35);
                current.ProcessInput(fresh.InteractEvent);
                Require(Find.Selector.Objects.Single() == member);
            });
        Check("native selection: releasing outside cancels the captured press", () =>
        {
            var f = Fixture();
            var command = new Command_SSCBondSelection(Ready(f.master, f.map), selectMaster: false);
            ButtonEvent(command, EventType.mouseDown, 35, 35);
            var released = ButtonEvent(command, EventType.mouseUp, 90, 90);
            command.ProcessInput(released.InteractEvent);
            Require(Find.Selector.Objects.Single() == f.master);

            var fresh = Click(command, 35, 35);
            command.ProcessInput(fresh.InteractEvent);
            Require(Find.Selector.Objects.Single() == f.slave);
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
            Widgets.CapturedLabels=new();GUI.CapturedTextures=new();Paint(new Command_SSCBondSelection(s, selectMaster: true));
            var image=GUI.CapturedTextures.Single(p=>p.image==null).rect;var label=Widgets.CapturedLabels.Single();
            Require(image.x==0&&image.y==0&&image.width==75&&image.height==75&&label.value==SSCBondOverviewText.T("SelectMaster")&&label.rect.y<image.yMax&&label.rect.yMax<=75);
        });
        Check("master button: narrowed square still fills its bounds with a cached portrait",()=>{
            var f=Fixture();Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);var s=Ready(f.slave,f.map);
            GUI.CapturedTextures=new();new Command_SSCBondSelection(s, selectMaster: true).GizmoOnGUI(new(10,20),50,default);
            var image=GUI.CapturedTextures.Single(p=>p.image==null).rect;
            Require(image.x==10&&image.y==20&&image.width==50&&image.height==50&&PortraitsCache.Requests.Single().size.x==56);
        });
        // 使用实际四语模板验证数量和按钮边界；字符宽度替身不代表实机字体或缩放验收。
        foreach (var (lang, width) in new[]
        {
            ("ChineseSimplified", 100f), ("ChineseTraditional", 130f), ("English", 130f), ("Russian", 100f)
        })
            Check("owned header: two-digit count survives "+lang+" width "+width,()=>{
                var xml=System.Xml.Linq.XDocument.Load(SscTestPaths.Combine(root,"Languages",lang,"Keyed/SSC_BondOverview.xml"));
                Verse.Extensions.Translations["SSC_BondOverview_OwnedCount"]=xml.Root.Element("SSC_BondOverview_OwnedCount").Value;
                LanguageDatabase.activeLanguage=new();var f=Fixture();for(int i=0;i<11;i++)Place(Slave(f.master,"P"+i),f.map);
                var s=Ready(f.master,f.map);Widgets.CapturedLabels=new();Widgets.CapturedFonts=new();
                new Gizmo_SSCBondOverview(s).GizmoOnGUI(new(0,0),width,default);
                var title=Widgets.CapturedLabels.Single();
                Require(title.value.EndsWith("12")&&Text.CalcSize(title.value).x<=title.rect.width&&title.rect.xMax<=width-28);
                Require(Widgets.CapturedFonts.Single()==GameFont.Small);
            });
        Check("owned header: long label truncates before count and warm repaint does not remeasure",()=>{
            Verse.Extensions.Translations["SSC_BondOverview_OwnedCount"]=new string('L',80)+": {0}";
            LanguageDatabase.activeLanguage=new();var f=Fixture();var s=Ready(f.master,f.map);var g=new Gizmo_SSCBondOverview(s);
            Widgets.CapturedLabels=new();Paint(g);var title=Widgets.CapturedLabels.Single();
            Require(title.value.EndsWith("1")&&title.value.Contains("...")&&Text.CalcSize(title.value).x<=title.rect.width);
            int measurements=Text.Measurements,truncations=UIExtensions.Truncations;Widgets.CapturedLabels.Clear();
            for(int i=0;i<10;i++)Paint(g);
            Require(Text.Measurements==measurements&&UIExtensions.Truncations==truncations&&Widgets.CapturedLabels.All(i=>ReferenceEquals(i.value,title.value)));
        });
    }
}
