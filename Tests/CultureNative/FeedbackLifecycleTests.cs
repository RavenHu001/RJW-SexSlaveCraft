// AI 辅助生成：原版需求散列调度、改宗/改革缓存、外层资格与只读生命周期。
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 改宗仍执行原版 SetIdeo 和文化/人物缓存；只隔离无界面宿主的窗口根与历史事件存储。
    private static readonly WindowStack feedbackWindows = new WindowStack();
    private static readonly HistoryEventsManager feedbackHistory = Bare<HistoryEventsManager>();
    private static bool FeedbackWindows(ref WindowStack __result) { __result=feedbackWindows; return false; }
    private static bool FeedbackHistory(ref HistoryEventsManager __result) { __result=feedbackHistory; return false; }

    private static void AdvanceNeeds(Pawn p, int ticks)
    {
        // 不自行实现 150 tick 公式；逐 tick 推进实际 NeedsTracker，使原版决定何时调用 NeedInterval。
        for(int i=0;i<ticks;i++) { tick++; p.needs.NeedsTrackerTickInterval(1); }
    }

    private static void LifecycleFeedbackTests()
    {
        Check("actual needs hash scheduling replaces envy by the next 150-tick interval", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Envy"});
            Equal(-6f,MoodSum(p)); var old=Mood(p).Single(); Chain(p,MakePawn(PawnIdentity.Master));
            // 未调度前旧阶段仍在原版缓存中；这不是直接重查 worker 后伪装成刷新成功。
            Equal(-6f,MoodSum(p)); AdvanceNeeds(p,150); Equal(10f,MoodSum(p));
            Equal(old,Mood(p).Single()); Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("environment and leader converge through native needs plus lazy snapshots within 250 ticks", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred","Master_HolyMaster"});
            var leader=MakePawn(PawnIdentity.Master); var slave=MakePawn(); Chain(slave,leader); MakeMap(p,leader,slave);
            var ideo=Culture(); player.ideos.SetPrimary(ideo); Role(ideo,leader);
            Equal(7f,MoodSum(p));
            var added=MakePawn(); Chain(added,leader); MakeMap(added);
            leader.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
            AdvanceNeeds(p,250); Equal(-4f,MoodSum(p));
            Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("actual owner mood follows retarget/self/dead owner without identity or bond writes", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"Master_HolyMaster"});
            var a=MakePawn(PawnIdentity.Master); var b=MakePawn(PawnIdentity.Master); var chain=Chain(p,a);
            Equal(5f,MoodSum(p)); chain.target=b; AdvanceNeeds(p,150); Equal(5f,MoodSum(p));
            chain.target=p; AdvanceNeeds(p,150); Equal(0f,MoodSum(p));
            chain.target=b; Set(b.health,"healthState",PawnHealthState.Dead); AdvanceNeeds(p,150); Equal(0f,MoodSum(p));
            Equal(PawnIdentity.Unset,p.TryGetComp<CompSexSlaveTraining>().pawnIdentity); Equal(b,chain.LinkedPawn);
            Require(SSCBondUtility.GetBridle(a)==null && SSCBondUtility.GetBridle(b)==null);
        });
        Check("native outer developmental/trait qualifications and absence of ideology remain effective", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Envy"});
            Equal(-6f,MoodSum(p));
            // 使用真实 TraitSet 的 disallowedThoughts，不给 worker 另加绕过原版资格的特殊通道。
            var td=new TraitDef {defName="FixtureThoughtBlocker",degreeDatas=new() {new TraitDegreeData {degree=0,disallowedThoughts=new() {T("SexSlave_Envy_Self")}}}};
            p.story.traits.allTraits.Add(new Trait(td));
            p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
            p.story.traits.allTraits.Clear(); p.needs.mood.thoughts.ThoughtInterval(); Equal(-6f,MoodSum(p));
            p.def.race.lifeStageAges[0].def.developmentalStage=DevelopmentalStage.Baby;
            p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
            p.def.race.lifeStageAges[0].def.developmentalStage=DevelopmentalStage.Adult;
            Set(p.ideo,"ideo",null); p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
        });
        Check("DLC off/loading/component reset and game switch cannot publish stale environment or leader results", () =>
        {
            var cache=FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred"}); var slave=MakePawn(); Chain(slave,MakePawn(PawnIdentity.Master)); MakeMap(p,slave);
            Equal(1f,MoodSum(p)); var thought=Mood(p).Single();
            Scribe.mode=LoadSaveMode.LoadingVars;
            try { Equal(0f,thought.MoodOffset()); } finally { Scribe.mode=LoadSaveMode.Inactive; }
            cache.LoadedGame(); p.needs.mood.thoughts.ThoughtInterval(); Equal(1f,MoodSum(p));
            SetStatic(typeof(ModsConfig),"ideologyActive",false);
            try { p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p)); } finally { SetStatic(typeof(ModsConfig),"ideologyActive",true); }
            // 原人物在新 Game 的名册中不存在；worker 不得持有上一个组件或人物人口结果。
            FeedbackContext(); p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
        });
        Check("native social target subhuman filtering remains enabled", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred"}); var target=Believer(Array.Empty<string>()); Chain(target,MakePawn(PawnIdentity.Master));
            target.mutant=new Pawn_MutantTracker(target); Set(target.mutant,"def",new MutantDef {consideredSubhuman=true});
            // 原版 handler 的目标筛选发生在 worker 之后，不能把 worker 成立直接当作最终评价存在。
            Require(T("SexSlave_Sacred_Social").Worker.CurrentSocialState(p,target).Active);
            Equal(0,Opinion(p,target));
        });

        // 这些边界补丁只围住原版改宗的外部设施；缓存算法、SetIdeo、改革和 NeedsTracker 不替换。
        var conversionBoundary=new Harmony("ssc.tests.culture.conversion-boundary");
        conversionBoundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.WindowStack)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackWindows)));
        conversionBoundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.HistoryEventsManager)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackHistory)));
        conversionBoundary.Patch(AccessTools.Method(typeof(HistoryEventsManager),nameof(HistoryEventsManager.RecordEvent)),prefix:new HarmonyMethod(typeof(Program),nameof(SkipBootstrap)));
        try
        {
            // 改宗会检查配偶数量；提供真实非 implied 定义，夹具人物本来就没有配偶。
            PawnRelationDefOf.Spouse=new PawnRelationDef {defName="Spouse"};
            Check("actual SetIdeo clears old mood/social caches and adopts the new observer precepts", () =>
            {
                FeedbackContext(); var p=Believer(new[] {"SexSlave_Envy"}); var target=Believer(Array.Empty<string>()); Chain(target,MakePawn(PawnIdentity.Master));
                Equal(-6f,MoodSum(p)); Equal(20,Opinion(p,target));
                var next=Culture(); Add(next,"SexSlave_Respect"); player.ideos.SetPrimary(next);
                p.ideo.SetIdeo(next); Equal(next,p.Ideo); Equal(0f,MoodSum(p)); Equal(10,Opinion(p,target));
                Require(Social(p,target).Cast<Thought>().All(t=>t.sourcePrecept.ideo==next));
                p.ideo.SetIdeo(Culture()); Equal(0f,MoodSum(p)); Equal(0,Opinion(p,target));
            });
            Check("actual reform cache ID drives IdeoTracker refresh and removes old stages/social", () =>
            {
                FeedbackContext(); Current.Game.World.ideoManager=new IdeoManager {classicMode=true};
                var p=Believer(new[] {"SexSlave_Envy"}); var target=Believer(Array.Empty<string>()); Chain(target,MakePawn(PawnIdentity.Master));
                Equal(-6f,MoodSum(p)); Equal(20,Opinion(p,target));
                int oldId=p.Ideo.currentCacheId;
                // 经原版移除模因兼容维护，艳羡须被合法普通档替换，而非测试直接清空 thought 缓存。
                p.Ideo.memes.Clear(); p.Ideo.foundation.EnsurePreceptsCompatibleWithMemes(new() {mainMeme},new(),Parms());
                Require(p.Ideo.currentCacheId!=oldId); Require(!p.Ideo.HasPrecept(D("SexSlave_Envy")));
                p.ideo.IdeoTrackerTickInterval(1);
                Require(Mood(p).All(t=>t.def!=T("SexSlave_Envy_Self")));
                Require(Social(p,target).Cast<Thought>().All(t=>t.def!=T("SexSlave_Envy_Social")));
            });
        }
        finally { conversionBoundary.UnpatchAll(conversionBoundary.Id); }
    }
}
