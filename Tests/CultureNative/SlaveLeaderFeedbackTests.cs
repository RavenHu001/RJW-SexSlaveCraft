// AI 辅助生成：原版正式职位上的 SSC 性奴、互斥领袖阶段、主人奖励去重与真实刷新路径。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static (SSCCultureGameComponent cache,Pawn observer,Pawn leader,Precept_RoleSingle role) SlaveLeaderContext(string tier, PawnIdentity observerIdentity=PawnIdentity.Unset)
    {
        // 性奴与主人为不同人物/互斥身份；真实锁链单独布置，原版任职资格与生产查询不替换。
        var cache=FeedbackContext(); var observer=Believer(new[] {"Master_"+tier},observerIdentity);
        var leader=Believer(Array.Empty<string>(),PawnIdentity.Slave); Chain(leader,MakePawn(PawnIdentity.Master));
        MakeMap(observer,leader); var primary=Culture(); player.ideos.SetPrimary(primary);
        return (cache,observer,leader,Role(primary,leader));
    }

    private static void SlaveLeaderFeedbackTests()
    {
        foreach(var sample in new[] {(tier:"Deference",mood:-6f,social:-10,own:4f,ordinary:-3f,reward:3f),
            (tier:"HolyMaster",mood:-12f,social:-20,own:10f,ordinary:-6f,reward:6f)})
        {
            Check("formal SSC slave leader has exactly one mood stage and observer-directed social: "+sample.tier, () =>
            {
                var s=SlaveLeaderContext(sample.tier); Require(s.cache.TryGetLeader(out var result));
                Equal(SSCCultureLeaderState.SexSlave,result.State); Equal(s.leader,result.Pawn);
                Equal(sample.mood,MoodSum(s.observer)); Equal(1,Mood(s.observer).Count);
                var thought=Mood(s.observer).Single(); Equal(2,thought.CurStageIndex); Equal(sample.mood,thought.MoodOffset());
                Equal(s.observer.Ideo.GetPrecept(D("Master_"+sample.tier)),thought.sourcePrecept);
                Equal(sample.social,Opinion(s.observer,s.leader)); Equal(sample.social,s.observer.relations.OpinionOf(s.leader));
                var social=(Thought_SituationalSocial)Social(s.observer,s.leader).Single();
                Equal((float)sample.social,social.OpinionOffset()); Equal(thought.sourcePrecept,social.sourcePrecept);
                // 不要求文化相同，不自动对称，不评价自身；查询不能写身份、链、反向索引或任职。
                Require(s.observer.Ideo!=s.leader.Ideo); Equal(0,Opinion(s.leader,s.observer)); Equal(0,Opinion(s.observer,s.observer));
                Equal(PawnIdentity.Slave,s.leader.TryGetComp<CompSexSlaveTraining>().pawnIdentity);
                Equal(s.leader,s.role.ChosenPawnSingle()); Require(SSCBondUtility.GetBridle(s.leader)==null);
                Equal(0,s.observer.relations.DirectRelations.Count); Equal(0,s.observer.needs.mood.thoughts.memories.Memories.Count);
            });
            Check("current masters keep slave and ordinary non-master leader penalties: "+sample.tier, () =>
            {
                var s=SlaveLeaderContext(sample.tier,PawnIdentity.Master);
                Equal(sample.own+sample.mood,MoodSum(s.observer)); Equal(2,Mood(s.observer).Count);
                Equal(sample.social,Opinion(s.observer,s.leader));
                s.leader.health.hediffSet.hediffs.Clear(); AdvanceNeeds(s.observer,250);
                Equal(sample.own+sample.ordinary,MoodSum(s.observer)); Equal(0,Opinion(s.observer,s.leader));
            });
            Check("master leader reward is absent for masters including self but remains for other believers: "+sample.tier, () =>
            {
                FeedbackContext(); var incumbent=Believer(new[] {"Master_"+sample.tier},PawnIdentity.Master);
                var otherMaster=Believer(new[] {"Master_"+sample.tier},PawnIdentity.Master);
                var believer=Believer(new[] {"Master_"+sample.tier}); MakeMap(incumbent,otherMaster,believer);
                var ideo=Culture(); player.ideos.SetPrimary(ideo); Role(ideo,incumbent);
                Equal(sample.own,MoodSum(incumbent)); Equal(sample.own,MoodSum(otherMaster)); Equal(sample.reward,MoodSum(believer));
                Require(Mood(incumbent).All(t=>t.def!=T("Master_"+sample.tier+"_Leader")));
                // 身份变化通过真实需求调度收敛，不能保留创建于非主人时期的领袖奖励。
                believer.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master;
                AdvanceNeeds(believer,150); Equal(sample.own,MoodSum(believer));
                believer.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
                AdvanceNeeds(believer,150); Equal(sample.reward,MoodSum(believer));
            });
            Check("native needs follows slave/ordinary/master/vacancy without accumulating duplicate mood: "+sample.tier, () =>
            {
                var s=SlaveLeaderContext(sample.tier); Equal(sample.mood,MoodSum(s.observer));
                s.leader.health.hediffSet.hediffs.Clear(); AdvanceNeeds(s.observer,250);
                Equal(sample.ordinary,MoodSum(s.observer)); Equal(1,Mood(s.observer).Single().CurStageIndex);
                s.leader.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master;
                AdvanceNeeds(s.observer,250); Equal(sample.reward,MoodSum(s.observer));
                Equal(0,Mood(s.observer).Single().CurStageIndex); Set(s.role,"chosenPawn",null);
                AdvanceNeeds(s.observer,250); Equal(0f,MoodSum(s.observer)); Equal(0,Mood(s.observer).Count);
            });
            Check("slave leader social adds slave attitude and retains native final opinion factors: "+sample.tier, () =>
            {
                var s=SlaveLeaderContext(sample.tier); Add(s.observer.Ideo,"SexSlave_Sacred");
                Equal(20+sample.social,Opinion(s.observer,s.leader)); Equal(2,Social(s.observer,s.leader).Count);
                Equal(20+sample.social,s.observer.relations.OpinionOf(s.leader));
                // 使用本机前例装载的真实非人化阶段，最终好感仍经过原版系数。
                var hediff=new Hediff {pawn=s.observer,def=DefDatabase<HediffDef>.GetNamed("Inhumanized")};
                s.observer.health.hediffSet.hediffs.Add(hediff); Equal(0,s.observer.relations.OpinionOf(s.leader));
                s.observer.health.hediffSet.hediffs.Remove(hediff);
            });
        }
        Check("slave leader remains an ordinary slave for own attitude; self social is always absent", () =>
        {
            var s=SlaveLeaderContext("HolyMaster"); Add(s.leader.Ideo,"Master_HolyMaster");
            // 自己也是领袖时负面政治反馈仍保留；实际主人 +5 独立，不能凭任职把自己改成 SSC 主人。
            Equal(-7f,MoodSum(s.leader)); Equal(0,Opinion(s.leader,s.leader));
            Equal(-12f,Mood(s.leader).Single(t=>t.def==T("Master_HolyMaster_Leader")).MoodOffset());
            Require(!SSCCultureStateUtility.IsMaster(s.leader));
        });
        Check("personal master dislike and negative master-leader mood are not removed by positive deduplication", () =>
        {
            FeedbackContext(); var observer=Believer(new[] {"Master_Hostile"},PawnIdentity.Master);
            var incumbent=MakePawn(PawnIdentity.Master); MakeMap(observer,incumbent); var primary=Culture(); player.ideos.SetPrimary(primary); Role(primary,incumbent);
            Equal(-18f,MoodSum(observer)); Equal(2,Mood(observer).Count);
        });
        Check("preselection is not a slave leader and failed formal eligibility is not an appointed slave", () =>
        {
            var s=SlaveLeaderContext("HolyMaster"); s.leader.health.hediffSet.hediffs.Clear();
            Equal(-6f,MoodSum(s.observer)); Equal(0,Opinion(s.observer,s.leader));
            Chain(s.leader,MakePawn(PawnIdentity.Master)); Set(s.leader.guest,"guestStatusInt",GuestStatus.Slave);
            tick+=100; s.observer.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(s.observer)); Equal(0,Opinion(s.observer,s.leader));
            // 原版法律奴隶的任职限制保持；SSC 锁链性奴可为自由殖民者，两种判定不能混为一谈。
            Set(s.leader.guest,"guestStatusInt",GuestStatus.Guest); tick+=100;
            s.observer.needs.mood.thoughts.ThoughtInterval(); Equal(-12f,MoodSum(s.observer)); Equal(-20,Opinion(s.observer,s.leader));
        });
        Check("non-leader chained targets, moral guide, faction pointer, invalid and conflicting incumbents do not receive leader opinion", () =>
        {
            var s=SlaveLeaderContext("HolyMaster"); var other=Believer(Array.Empty<string>(),PawnIdentity.Slave);
            Chain(other,MakePawn(PawnIdentity.Master)); MakeMap(other);
            Equal(0,Opinion(s.observer,other)); Set(s.role,"chosenPawn",null); player.leader=s.leader;
            Role(s.role.ideo,s.leader,false); tick+=100; s.observer.needs.mood.thoughts.ThoughtInterval();
            Equal(0f,MoodSum(s.observer)); Equal(0,Opinion(s.observer,s.leader));
            Role(s.role.ideo,s.leader); Role(s.role.ideo,other); tick+=100;
            s.observer.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(s.observer));
            Equal(0,Opinion(s.observer,s.leader)); Equal(0,Opinion(s.observer,other));
        });
        Check("leader social uses observer belief without introducing a general attitude roster restriction", () =>
        {
            var s=SlaveLeaderContext("Deference");
            var outsider=Believer(new[] {"Master_HolyMaster"},faction:new Faction {def=new FactionDef {defName="ForeignCultureFixture"}});
            Equal(0f,MoodSum(outsider)); Equal(-20,Opinion(outsider,s.leader));
            // 目标必须是正式有效的玩家领袖，但观察者范围保持原版文化态度规则。
            var hidden=Believer(new[] {"Master_Deference"}); Equal(0f,MoodSum(hidden)); Equal(-10,Opinion(hidden,s.leader));
        });
        Check("leader mood and many social observers share one roster and leader query per lazy generation", () =>
        {
            var s=SlaveLeaderContext("HolyMaster"); var viewers=Enumerable.Range(0,12).Select(_=>Believer(new[] {"Master_Deference"})).ToArray();
            MakeMap(viewers); calls.Clear(); Equal(-12f,MoodSum(s.observer));
            foreach(var viewer in viewers) { Equal(-6f,MoodSum(viewer)); Equal(-10,Opinion(viewer,s.leader)); }
            for(int i=0;i<30;i++) { s.observer.needs.mood.thoughts.ThoughtInterval(); Equal(-12f,MoodSum(s.observer)); }
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("Query"));
            Equal(0,CallCount("CountMasters")); Equal(0,CallCount("CountBoundSlaves")); Equal(0,CallCount("CountManagedChained"));
            tick+=100; s.observer.needs.mood.thoughts.ThoughtInterval(); Equal(-20,Opinion(s.observer,s.leader));
            Equal(2,CallCount("CollectMembers")); Equal(2,CallCount("Query"));
        });
        Check("ordinary non-chained social target short-circuits before any population or leader query", () =>
        {
            FeedbackContext(); var observer=Believer(new[] {"Master_HolyMaster"}); var ordinary=Believer(Array.Empty<string>());
            calls.Clear(); Equal(0,Opinion(observer,ordinary)); Equal(0,calls.Count);
        });
        SlaveLeaderLifecycleTests();
    }

    private static void SlaveLeaderLifecycleTests()
    {
        Check("all lazy/social relative phases converge after appointment changes and chain removal", () =>
        {
            // 共享领袖快照与原版双人缓存均为 100 tick；穷举相位，不手工 dirty。
            for(int phase=0;phase<100;phase++)
            {
                var s=SlaveLeaderContext("HolyMaster"); var replacement=Believer(Array.Empty<string>(),PawnIdentity.Slave);
                Chain(replacement,MakePawn(PawnIdentity.Master)); MakeMap(replacement);
                Require(s.cache.TryGetLeader(out _)); tick+=phase; Equal(-20,Opinion(s.observer,s.leader)); Equal(0,Opinion(s.observer,replacement));
                Set(s.role,"chosenPawn",new IdeoRoleInstance(s.role) {pawn=replacement});
                for(int i=0;i<200;i++) { tick++; _=Opinion(s.observer,s.leader); _=Opinion(s.observer,replacement); }
                Equal(0,Opinion(s.observer,s.leader)); Equal(-20,Opinion(s.observer,replacement));
                replacement.health.hediffSet.hediffs.Clear();
                for(int i=0;i<200;i++) { tick++; _=Opinion(s.observer,replacement); }
                Equal(0,Opinion(s.observer,replacement));
            }
        });
        Check("slave-leader cache is not serialized and loading rollback or Game switch cannot retain the old leader", () =>
        {
            var s=SlaveLeaderContext("HolyMaster"); Equal(-12f,MoodSum(s.observer)); Equal(-20,Opinion(s.observer,s.leader));
            var source=s.cache; string path=Path.Combine(saveDirectory,"slave-leader-cache.xml");
            try
            {
                Scribe.saver.InitSaving(path,"SlaveLeaderCache"); Scribe_Deep.Look(ref source,"cache"); Scribe.saver.FinalizeSaving();
                Require(!System.Xml.Linq.XDocument.Load(path).Root.Element("cache").Elements().Any());
                SSCCultureGameComponent loaded=null;
                Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded,"cache",Current.Game); Scribe.loader.FinalizeLoading();
                Require(!loaded.TryGetLeader(out _)); Current.Game.components.Clear(); Current.Game.components.Add(loaded); loaded.LoadedGame();
                Require(loaded.TryGetLeader(out var leader)); Equal(SSCCultureLeaderState.SexSlave,leader.State);
                tick+=50; loaded.TryGetLeader(out _); s.leader.health.hediffSet.hediffs.Clear(); tick--;
                Require(loaded.TryGetLeader(out leader)); Equal(SSCCultureLeaderState.NonMaster,leader.State);
                FeedbackContext(); Require(!loaded.TryGetLeader(out _)); tick+=100;
                s.observer.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(s.observer)); Equal(0,Opinion(s.observer,s.leader));
            }
            finally { Scribe.mode=LoadSaveMode.Inactive; }
        });
        Check("real SetIdeo and native precept replacement remove and restore slave-leader mood and social", () =>
        {
            // 只隔离改宗的 UI/历史根；复用既有边界，文化及人物缓存失效仍走原版。
            var boundary=new Harmony("ssc.tests.culture.slave-leader-conversion");
            boundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.WindowStack)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackWindows)));
            boundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.HistoryEventsManager)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackHistory)));
            boundary.Patch(AccessTools.Method(typeof(HistoryEventsManager),nameof(HistoryEventsManager.RecordEvent)),prefix:new HarmonyMethod(typeof(Program),nameof(SkipBootstrap)));
            try
            {
                var s=SlaveLeaderContext("HolyMaster"); Current.Game.World.ideoManager=new IdeoManager {classicMode=true};
                Equal(-12f,MoodSum(s.observer)); Equal(-20,Opinion(s.observer,s.leader));
                var next=Culture(); Add(next,"Master_Deference"); player.ideos.SetPrimary(next); Role(next,s.leader);
                s.observer.ideo.SetIdeo(next); Equal(-6f,MoodSum(s.observer)); Equal(-10,Opinion(s.observer,s.leader));
                next.RemovePrecept(next.GetPrecept(D("Master_Deference")),true); Add(next,"Master_Indifference");
                s.observer.ideo.IdeoTrackerTickInterval(1); Equal(0f,MoodSum(s.observer)); Equal(0,Opinion(s.observer,s.leader));
                next.RemovePrecept(next.GetPrecept(D("Master_Indifference")),true); Add(next,"Master_Deference");
                s.observer.ideo.IdeoTrackerTickInterval(1); Equal(-6f,MoodSum(s.observer)); Equal(-10,Opinion(s.observer,s.leader));
            }
            finally { boundary.UnpatchAll(boundary.Id); }
        });
    }
}
