// AI 辅助生成。显式诊断链接生产全局查询；验证权限、迟到动作及与普通会话的隔离。
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    static void DiagnosticTests()
    {
        foreach (string gate in new[]{"dev","setting","both"}) Check("diagnostic: gated at creation "+gate,()=>{
            var f=Fixture();Prefs.DevMode=gate=="setting";SSCMod.settings.enableDebugGizmos=gate=="dev";
            Require(f.master.Training.BondOverviewDiagnosticGizmo(f.master)==null&&ThingOwnerUtility.MapQueryCount==0);
        });
        Check("diagnostic: construction and ordinary UI never invoke the global query",()=>{
            var f=Fixture();Prefs.DevMode=SSCMod.settings.enableDebugGizmos=true;
            for(int i=0;i<100;i++)Require(f.master.Training.BondOverviewDiagnosticGizmo(f.master)!=null);
            var s=Ready(f.master,f.map);for(int i=0;i<100;i++){Time.realtimeSinceStartup=i;s.ObserveCard(f.master,f.map,i);s.Update(i,true,f.map);Paint(new Gizmo_SSCBondOverview(s));}
            Require(ThingOwnerUtility.MapQueryCount==0&&ThingOwnerUtility.Visits.Count==0);
        });
        Check("diagnostic: explicit invocation finds omission but never repairs or injects it",()=>{
            var f=Fixture();var bridle=SSCBondUtility.GetBridle(f.master);bridle.targets.Clear();
            var s=Current.Game.GetComponent<SSCBondRelationGameComponent>().Overview;s.ObserveCard(f.master,f.map,0);
            var snapshot=s.Card.Snapshot;int revision=s.Card.Revision;var chain=SSCBondUtility.GetChain(f.slave);
            Prefs.DevMode=SSCMod.settings.enableDebugGizmos=true;
            var command=(Command_Action)f.master.Training.BondOverviewDiagnosticGizmo(f.master);command.action();
            Require(ThingOwnerUtility.MapQueryCount==1&&Messages.Last==SSCBondOverviewText.Format("DebugQueryResult",0,1,1));
            Require(bridle.targets.Count==0&&chain==SSCBondUtility.GetChain(f.slave)&&chain.target==f.master&&
                s.Card.Snapshot==snapshot&&s.Card.Revision==revision&&snapshot.TotalSlaveCount==0);
            Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);
            Require(s.Card.Snapshot.TotalSlaveCount==0&&ThingOwnerUtility.MapQueryCount==1);
        });
        Check("diagnostic: broken provider is reported without damaging local display",()=>{
            var f=Fixture();Place(new BrokenHolder(),f.map);var s=Ready(f.master,f.map);var snapshot=s.Card.Snapshot;
            Prefs.DevMode=SSCMod.settings.enableDebugGizmos=true;
            ((Command_Action)f.master.Training.BondOverviewDiagnosticGizmo(f.master)).action();
            Require(Log.Errors==1&&Messages.Last==SSCBondOverviewText.T("DebugQueryFailed")&&s.Card.Snapshot==snapshot);
            int scans=ThingOwnerUtility.MapQueryCount;Time.realtimeSinceStartup=2;s.ObserveCard(f.master,f.map,1);s.Update(1,true,f.map);
            Require(s.Card.Snapshot.TotalSlaveCount==1&&ThingOwnerUtility.MapQueryCount==scans);
        });
        foreach(string state in new[]{"dev","setting","settings-null","source","game","loading","destroyed","map"})
            Check("diagnostic: delayed command rejects "+state,()=>{
                var f=Fixture();Prefs.DevMode=SSCMod.settings.enableDebugGizmos=true;
                var command=(Command_Action)f.master.Training.BondOverviewDiagnosticGizmo(f.master);
                if(state=="dev")Prefs.DevMode=false;if(state=="setting")SSCMod.settings.enableDebugGizmos=false;
                if(state=="settings-null")SSCMod.settings=null;
                if(state=="source"){Find.Selector.Objects.Clear();Find.Selector.Objects.Add(f.slave);}
                if(state=="game")Current.Game=new();if(state=="loading")Scribe.mode=LoadSaveMode.PostLoadInit;
                if(state=="destroyed")f.master.Destroyed=true;if(state=="map")Find.CurrentMap=new();
                command.action();Require(ThingOwnerUtility.MapQueryCount==0);
            });
    }

    private sealed class BrokenHolder : Thing, IThingHolder
    {
        public IEnumerable<Thing> HeldThings => throw new InvalidOperationException("broken diagnostic provider");
    }
}
