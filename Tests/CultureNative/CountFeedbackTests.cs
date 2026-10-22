// AI 辅助生成：步骤 3 实际生产数量反馈、原版结算/缓存、观察方向和查询工作量。
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn[] BoundGroup(Pawn master, int count)
    {
        // 健康列表布置链与反向候选，后续遍历/校验仍执行生产代码；不伪造人数返回值。
        var slaves = Enumerable.Range(0, count).Select(_ => MakePawn()).ToArray();
        foreach (var slave in slaves) Chain(slave, master);
        BindCandidates(master, slaves);
        return slaves;
    }

    private static void CountFeedbackTests(string game)
    {
        Check("quantity config rejects missing/invalid/foreign limits and invalid conditions", () =>
        {
            // 合法值独立列举；新字段默认零必须保持旧条件可用，而不能将零当数量不限档。
            foreach (var condition in new[] { SSCCultureThoughtCondition.MasterCountExcess,
                SSCCultureThoughtCondition.BoundSlaveCountExcess, SSCCultureThoughtCondition.OtherMasterBoundSlaveCountExcess })
            {
                foreach (int limit in new[] {-1, 0, 2, 4, int.MaxValue})
                    Require(new SSCCultureThoughtExtension {condition=condition,countLimit=limit}.ConfigErrors().Any());
                foreach (int limit in new[] {1,3})
                    Require(!new SSCCultureThoughtExtension {condition=condition,countLimit=limit}.ConfigErrors().Any());
            }
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.MasterSelf,countLimit=1}.ConfigErrors().Any());
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p);
            var wrong=new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.MasterSelf,countLimit=1};
            Require(!SSCCultureCountUtility.TryGetMoodExcess(p,wrong,out _));
            Require(!SSCCultureCountUtility.TryGetSocialExcess(p,p,wrong,out _));
        });

        foreach (var sample in new[] { (tier:"One",limit:1), (tier:"Three",limit:3) })
        {
            Check("literal master-count curve including zero and distant cap: " + sample.tier, () =>
            {
                // 期望为字面量表，不从生产曲线反推；每个人数重建独立生产上下文。
                float[] expected=sample.limit==1 ? new[] {0f,0,-6,-15,-27,-27,-27,-27}
                    : new[] {0f,0,0,0,-6,-15,-27,-27};
                for (int count=0;count<expected.Length;count++)
                {
                    FeedbackContext(); var p=Believer(new[] {"MasterCount_"+sample.tier}); MakeMap(p);
                    MakeMap(Enumerable.Range(0,count).Select(_=>MakePawn(PawnIdentity.Master)).ToArray());
                    Equal(expected[count],MoodSum(p));
                    if (expected[count]!=0) Equal(expected[count],Mood(p).Single().MoodOffset());
                    else Equal(0,Mood(p).Count);
                }
                FeedbackContext(); var far=Believer(new[] {"MasterCount_"+sample.tier}); MakeMap(far);
                MakeMap(Enumerable.Range(0,100).Select(_=>MakePawn(PawnIdentity.Master)).ToArray());
                Equal(-27f,MoodSum(far));
            });
            Check("literal own-count curve and native three-stage social curve: " + sample.tier, () =>
            {
                float[] expected=sample.limit==1 ? new[] {0f,0,-3,-9,-18,-18,-18,-18}
                    : new[] {0f,0,0,0,-3,-9,-18,-18};
                int[] social=sample.limit==1 ? new[] {0,0,-5,-15,-30,-30,-30,-30}
                    : new[] {0,0,0,0,-5,-15,-30,-30};
                for (int count=0;count<expected.Length;count++)
                {
                    FeedbackContext(); var owner=Believer(new[] {"SlaveCount_"+sample.tier},PawnIdentity.Master);
                    var observer=Believer(new[] {"SlaveCount_"+sample.tier}); MakeMap(owner,observer); BoundGroup(owner,count);
                    Equal(expected[count],MoodSum(owner)); Equal(social[count],Opinion(observer,owner));
                    if (social[count]!=0)
                    {
                        var thought=(Thought_SituationalSocial)Social(observer,owner).Single();
                        Equal(social[count],(int)thought.OpinionOffset());
                        Equal(social[count]==-5?0:social[count]==-15?1:2,thought.CurStageIndex);
                        Equal(observer.Ideo.GetPrecept(D("SlaveCount_"+sample.tier)),thought.sourcePrecept);
                    }
                    else Equal(0,Social(observer,owner).Count);
                }
                FeedbackContext(); var far=Believer(new[] {"SlaveCount_"+sample.tier},PawnIdentity.Master);
                var viewer=Believer(new[] {"SlaveCount_"+sample.tier}); MakeMap(far,viewer); BoundGroup(far,100);
                Equal(-18f,MoodSum(far)); Equal(-30,Opinion(viewer,far));
            });
        }

        Check("observer owns tolerance; unlimited target and non-master observers across cultures", () =>
        {
            FeedbackContext(); var a=Believer(new[] {"SlaveCount_One"}); var b=Believer(new[] {"SlaveCount_Unlimited"},PawnIdentity.Master);
            var c=Believer(new[] {"SlaveCount_Three"}); var unlimited=Believer(new[] {"SlaveCount_Unlimited"});
            MakeMap(a,b,c,unlimited); BoundGroup(b,3);
            Require(a.Ideo!=b.Ideo && !SSCCultureStateUtility.IsMaster(a));
            Equal(0f,MoodSum(b)); Equal(-15,Opinion(a,b)); Equal(0,Opinion(c,b)); Equal(0,Opinion(unlimited,b));
            Equal(0,Opinion(b,a)); Equal(0,Opinion(a,a));
            // 反向评价可独立成立，双方各用自己的上限，不因已经存在 A→B 就自动镜像。
            a.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master; BoundGroup(a,5);
            tick+=100; Equal(0,Opinion(b,a)); Equal(-15,Opinion(c,a)); Equal(-15,Opinion(a,b));
        });
        Check("both quantity-social participants require roster, unlike existing general master attitudes", () =>
        {
            FeedbackContext(); var a=Believer(new[] {"SlaveCount_One","Master_HolyMaster"});
            var owner=Believer(Array.Empty<string>(),PawnIdentity.Master); BoundGroup(owner,4);
            var hidden=Believer(new[] {"SlaveCount_One"}); MakeMap(a);
            Equal(15,Opinion(a,owner)); Equal(0,Opinion(hidden,owner));
            MakeMap(owner); tick+=100; Equal(-15,Opinion(a,owner)); Equal(0,Opinion(hidden,owner));
            MakeMap(hidden); tick+=100; Equal(-30,Opinion(hidden,owner));
            Set(owner.guest,"guestStatusInt",GuestStatus.Prisoner); tick+=100; Equal(15,Opinion(a,owner));
        });
        Check("master totals span maps/caravans and include receiver, not hidden/prisoner/qualification-only pawns", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"MasterCount_One"},PawnIdentity.Master); var second=MakePawn(PawnIdentity.Master);
            MakeMap(p,second); MakeMap(p); var caravan=new Caravan(); Set(caravan,"factionInt",player);
            var third=MakePawn(PawnIdentity.Master); caravan.pawns.TryAdd(third); worldObjects.Caravans.Add(caravan);
            var hidden=MakePawn(PawnIdentity.Master); var prisoner=MakePawn(PawnIdentity.Master);
            Set(prisoner.guest,"guestStatusInt",GuestStatus.Prisoner); MakeMap(prisoner,MakePawn());
            Equal(-15f,MoodSum(p));
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset; AdvanceNeeds(p,250); Equal(-6f,MoodSum(p));
            Equal(-9999999,p.playerSettings.displayOrder); Require(!SSCCultureStateUtility.IsMaster(p));
        });
        Check("bound counts keep foreign/off-map objects and reject stale duplicate dead discarded self candidates", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p);
            var foreign=MakePawn(faction:new Faction()); Chain(foreign,p); var offmap=MakePawn(); Chain(offmap,p);
            var stale=MakePawn(); Chain(stale,MakePawn(PawnIdentity.Master)); var dead=MakePawn(); Chain(dead,p); Set(dead.health,"healthState",PawnHealthState.Dead);
            var discarded=MakePawn(); Chain(discarded,p); Set(discarded,"mapIndexOrState",(sbyte)-3);
            BindCandidates(p,foreign,foreign,offmap,stale,dead,discarded,p,null);
            var before=SSCBondUtility.GetBridle(p).targets.ToArray(); Equal(-3f,MoodSum(p));
            Require(before.SequenceEqual(SSCBondUtility.GetBridle(p).targets)); Equal(0,p.relations.DirectRelations.Count);
            Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("pure player caravan can receive own-count mood and quantity social without a loaded map", () =>
        {
            // 只走原版商队人物栏来源；绑定对象仍在名册外，不能因无地图或未显示而截断绑定人数。
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"});
            var owner=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); BoundGroup(owner,3);
            var caravan=new Caravan(); Set(caravan,"factionInt",player); caravan.pawns.TryAdd(p); caravan.pawns.TryAdd(owner);
            worldObjects.Caravans.Add(caravan); Equal(0,maps.Count);
            Equal(-9f,MoodSum(owner)); Equal(-15,Opinion(p,owner));
        });
        Check("two quantity mood groups fully stack to -45 and approved mixed example to -36", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"MasterCount_One","SlaveCount_One"},PawnIdentity.Master); MakeMap(p);
            MakeMap(MakePawn(PawnIdentity.Master),MakePawn(PawnIdentity.Master),MakePawn(PawnIdentity.Master)); BoundGroup(p,5);
            Equal(-45f,MoodSum(p)); Equal(2,Mood(p).Count);
            // 独立四主场景：主人上限 1、本人名下上限 3、实际绑定 5，分别为 -27 与 -9。
            FeedbackContext(); var q=Believer(new[] {"MasterCount_One","SlaveCount_Three"},PawnIdentity.Master); MakeMap(q);
            MakeMap(MakePawn(PawnIdentity.Master),MakePawn(PawnIdentity.Master),MakePawn(PawnIdentity.Master)); BoundGroup(q,5);
            Equal(-36f,MoodSum(q));
        });
        Check("quantity social adds native attitude and final inhumanized factor without permanent relation", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One","Master_HolyMaster"});
            var owner=Believer(Array.Empty<string>(),PawnIdentity.Master); MakeMap(p,owner); BoundGroup(owner,4);
            Equal(-15,Opinion(p,owner)); Equal(-15,p.relations.OpinionOf(owner)); Equal(2,Social(p,owner).Count);
            // 前面的原版取证用例已加载真实 Inhumanized 阶段配置；此处验证负向叠加仍经过最终系数。
            var h=new Hediff {pawn=p,def=DefDatabase<HediffDef>.GetNamed("Inhumanized")}; p.health.hediffSet.hediffs.Add(h);
            Equal(-15,Opinion(p,owner)); Equal(0,p.relations.OpinionOf(owner));
            p.health.hediffSet.hediffs.Remove(h); Equal(-15,p.relations.OpinionOf(owner));
            Equal(0,p.relations.DirectRelations.Count); Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });

        CountLifecycleTests();
        CountWorkTests();
    }

    private static void CountLifecycleTests()
    {
        Check("own quantity mood cap drops through every step via actual NeedsTracker scheduling", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p); var group=BoundGroup(p,8);
            Equal(-18f,MoodSum(p)); var thought=Mood(p).Single();
            foreach (var sample in new (int count,float value)[] {(5,-18f),(3,-9f),(2,-3f),(1,0f),(0,0f)})
            {
                // 真解绑条件为失去链；保留旧反向候选，生产迭代器必须排除失效对象。
                for(int i=sample.count;i<group.Length;i++) group[i].health.hediffSet.hediffs.Clear();
                AdvanceNeeds(p,250); Equal(sample.value,MoodSum(p));
                // inactive 的原版 thought 阶段为 -1，不应绕过原版 Active 过滤后强读 MoodOffset。
                if (sample.value!=0) Equal(sample.value,thought.MoodOffset());
                else Require(!((Thought_Situational)thought).Active);
            }
        });
        Check("master quantity mood activates and falls back through actual roster refresh", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"MasterCount_One"}); MakeMap(p);
            Equal(0f,MoodSum(p)); var owners=Enumerable.Range(0,8).Select(_=>MakePawn(PawnIdentity.Master)).ToArray(); MakeMap(owners);
            AdvanceNeeds(p,250); Equal(-27f,MoodSum(p));
            foreach (var sample in new (int count,float value)[] {(3,-15f),(2,-6f),(1,0f),(0,0f)})
            {
                for(int i=sample.count;i<owners.Length;i++) owners[i].TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
                AdvanceNeeds(p,250); Equal(sample.value,MoodSum(p));
            }
        });
        Check("retarget updates both real candidate lists and independent owner mood/observer social", () =>
        {
            FeedbackContext(); var a=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master);
            var b=Believer(new[] {"SlaveCount_Three"},PawnIdentity.Master); var observer=Believer(new[] {"SlaveCount_One"}); MakeMap(a,b,observer);
            var slaves=BoundGroup(a,3); BoundGroup(b,3); Equal(-9f,MoodSum(a)); Equal(0f,MoodSum(b));
            Equal(-15,Opinion(observer,a)); Equal(-15,Opinion(observer,b));
            // 仅布置换绑后的真实结果，不声称这里执行了完整绑定事务；文化层不能自行修复反向候选。
            SSCBondUtility.GetChain(slaves[0]).target=b;
            SSCBondUtility.GetBridle(a).targets.Remove(slaves[0]); SSCBondUtility.GetBridle(b).targets.Add(slaves[0]);
            AdvanceNeeds(a,250); b.needs.mood.thoughts.ThoughtInterval();
            Equal(-3f,MoodSum(a)); Equal(-3f,MoodSum(b)); Equal(-5,Opinion(observer,a)); Equal(-30,Opinion(observer,b));
        });
        Check("dual social cache budget under every relative generation phase; cap and recovery", () =>
        {
            // 穷举 100 种相位：先有人数代，再在相位内首查双人社交；不能手工 dirty 掩盖第二层缓存。
            for(int phase=0;phase<100;phase++)
            {
                var cache=FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"}); var owner=Believer(Array.Empty<string>(),PawnIdentity.Master);
                MakeMap(p,owner); var slaves=BoundGroup(owner,1); Require(cache.TryGetBoundSlaveCount(owner,out _));
                tick+=phase; Equal(0,Opinion(p,owner));
                var extra=Enumerable.Range(0,7).Select(_=>MakePawn()).ToArray();
                foreach(var s in extra) { Chain(s,owner); SSCBondUtility.GetBridle(owner).targets.Add(s); }
                for(int i=0;i<200;i++) { tick++; _=Opinion(p,owner); }
                Equal(-30,Opinion(p,owner));
                foreach(var s in extra) s.health.hediffSet.hediffs.Clear();
                for(int i=0;i<200;i++) { tick++; _=Opinion(p,owner); }
                Equal(0,Opinion(p,owner));
            }
        });
        Check("social cap recovers through every native stage and target identity change", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"}); var owner=Believer(Array.Empty<string>(),PawnIdentity.Master);
            MakeMap(p,owner); var group=BoundGroup(owner,8); Equal(-30,Opinion(p,owner));
            foreach(var sample in new (int count,int value)[] {(5,-30),(3,-15),(2,-5),(1,0),(0,0)})
            {
                for(int i=sample.count;i<group.Length;i++) group[i].health.hediffSet.hediffs.Clear();
                for(int i=0;i<200;i++) { tick++; _=Opinion(p,owner); }
                Equal(sample.value,Opinion(p,owner));
            }
            foreach(var s in group) Chain(s,owner); tick+=200; Equal(-30,Opinion(p,owner));
            owner.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
            tick+=100; Equal(0,Opinion(p,owner));
        });
        Check("retained own-count thought stops immediately on loss of identity or cultural mounting", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p); BoundGroup(p,3);
            Equal(-9f,MoodSum(p)); var thought=Mood(p).Single();
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset; Equal(0f,thought.MoodOffset());
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master; Equal(-9f,thought.MoodOffset());
            p.Ideo.RemovePrecept(p.Ideo.GetPrecept(D("SlaveCount_One")),true); Add(p.Ideo,"SlaveCount_Unlimited");
            Equal(0f,thought.MoodOffset());
        });
        Check("loading DLC-off unready rollback and Game switch reject stale quantity feedback", () =>
        {
            var cache=FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p); BoundGroup(p,3);
            Equal(-9f,MoodSum(p)); var thought=Mood(p).Single();
            Scribe.mode=LoadSaveMode.LoadingVars;
            try { Equal(0f,thought.MoodOffset()); } finally { Scribe.mode=LoadSaveMode.Inactive; }
            Equal(-9f,thought.MoodOffset());
            SetStatic(typeof(ModsConfig),"ideologyActive",false);
            try { Equal(0f,thought.MoodOffset()); } finally { SetStatic(typeof(ModsConfig),"ideologyActive",true); }
            Set(cache,"ready",false); Equal(0f,thought.MoodOffset()); cache.LoadedGame(); Equal(-9f,thought.MoodOffset());
            tick+=50; Equal(-9f,thought.MoodOffset()); SSCBondUtility.GetBridle(p).targets.Clear(); tick-=1; Equal(0f,thought.MoodOffset());
            // 切档还包括 Current.Game 暂为空的间隙；取消阵营根替身，确保不会访问无效的原版全局根。
            var previous=Current.Game; Current.Game=null;
            harmony.Unpatch(AccessTools.PropertyGetter(typeof(Faction),nameof(Faction.OfPlayer)),HarmonyPatchType.Prefix,harmony.Id);
            try { Equal(0f,thought.MoodOffset()); }
            finally { Current.Game=previous; PatchGetter(typeof(Faction),nameof(Faction.OfPlayer),nameof(Player)); }
            FeedbackContext(); Equal(0f,thought.MoodOffset()); Require(!cache.TryGetBoundSlaveCount(p,out _));
        });
        Check("real Scribe saves neither bound caches nor roster references; loaded component rebuilds quantities", () =>
        {
            var source=FeedbackContext(); var p=Believer(new[] {"MasterCount_One","SlaveCount_One"},PawnIdentity.Master);
            var observer=Believer(new[] {"SlaveCount_One"}); MakeMap(p,observer,MakePawn(PawnIdentity.Master)); BoundGroup(p,3);
            Equal(-15f,MoodSum(p)); Equal(-15,Opinion(observer,p));
            string path=Path.Combine(saveDirectory,"quantity-cache.xml");
            try
            {
                Scribe.saver.InitSaving(path,"QuantityCache"); Scribe_Deep.Look(ref source,"cache"); Scribe.saver.FinalizeSaving();
                Require(!System.Xml.Linq.XDocument.Load(path).Root.Element("cache").Elements().Any());
                SSCCultureGameComponent loaded=null;
                Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded,"cache",Current.Game); Scribe.loader.FinalizeLoading();
                Require(!loaded.TryGetBoundSlaveCount(p,out _)); Current.Game.components.Clear(); Current.Game.components.Add(loaded);
                loaded.LoadedGame(); calls.Clear();
                p.needs.mood.thoughts.ThoughtInterval(); tick+=100;
                Equal(-15f,MoodSum(p)); Equal(-15,Opinion(observer,p));
                Equal(2,CallCount("CountBoundSlaves"));
            }
            finally { Scribe.mode=LoadSaveMode.Inactive; }
        });
        Check("real Scribe existing finite precepts acquire current Def components after load", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p); BoundGroup(p,3);
            var source=p.Ideo; source.culture=new CultureDef {defName="QuantityFixtureCulture"}; DefDatabase<CultureDef>.Add(source.culture);
            source.description="[TODO:QuantityFixtureCulture.description]"; source.memberName="[TODO:member]"; Set(source,"id",71);
            foreach(var precept in source.PreceptsListForReading) precept.Init(source);
            string path=Path.Combine(saveDirectory,"quantity-culture.xml");
            try
            {
                Scribe.saver.InitSaving(path,"QuantityCulture"); Scribe_Deep.Look(ref source,"ideo"); Scribe.saver.FinalizeSaving();
                Ideo loaded=null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded,"ideo"); Scribe.loader.FinalizeLoading();
                Require(loaded.HasPrecept(D("SlaveCount_One"))); Equal(2,loaded.GetPrecept(D("SlaveCount_One")).def.comps.Count);
                Set(p.ideo,"ideo",loaded); Equal(-9f,MoodSum(p)); Equal(loaded,Mood(p).Single().sourcePrecept.ideo);
            }
            finally { Scribe.mode=LoadSaveMode.Inactive; }
        });

        // 只隔离改宗的外部 UI/历史根，SetIdeo 和文化 cache ID 刷新仍使用原版实现。
        var boundary=new Harmony("ssc.tests.culture.count-conversion");
        boundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.WindowStack)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackWindows)));
        boundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.HistoryEventsManager)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackHistory)));
        boundary.Patch(AccessTools.Method(typeof(HistoryEventsManager),nameof(HistoryEventsManager.RecordEvent)),prefix:new HarmonyMethod(typeof(Program),nameof(SkipBootstrap)));
        try
        {
            Check("actual conversion limited-unlimited-limited refreshes quantity mood and social sources", () =>
            {
                FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master);
                var target=Believer(new[] {"SlaveCount_Unlimited"},PawnIdentity.Master); MakeMap(p,target); BoundGroup(p,4); BoundGroup(target,4);
                Equal(-18f,MoodSum(p)); Equal(-30,Opinion(p,target));
                var unlimited=Culture(); Add(unlimited,"SlaveCount_Unlimited"); player.ideos.SetPrimary(unlimited); p.ideo.SetIdeo(unlimited);
                Equal(0f,MoodSum(p)); Equal(0,Opinion(p,target));
                var limited=Culture(); Add(limited,"SlaveCount_Three"); player.ideos.SetPrimary(limited); p.ideo.SetIdeo(limited);
                Equal(-3f,MoodSum(p)); Equal(-5,Opinion(p,target));
                Require(Mood(p).All(t=>t.sourcePrecept.ideo==limited)); Require(Social(p,target).Cast<Thought>().All(t=>t.sourcePrecept.ideo==limited));
            });
            Check("native precept replacement cache ID refreshes finite and unlimited quantities in both directions", () =>
            {
                FeedbackContext(); Current.Game.World.ideoManager=new IdeoManager {classicMode=true};
                var p=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); var owner=Believer(Array.Empty<string>(),PawnIdentity.Master);
                MakeMap(p,owner); BoundGroup(p,3); BoundGroup(owner,3); Equal(-9f,MoodSum(p)); Equal(-15,Opinion(p,owner));
                var ideo=p.Ideo; int old=ideo.currentCacheId;
                ideo.RemovePrecept(ideo.GetPrecept(D("SlaveCount_One")),true); Add(ideo,"SlaveCount_Unlimited");
                Require(ideo.currentCacheId!=old); p.ideo.IdeoTrackerTickInterval(1); Equal(0f,MoodSum(p)); Equal(0,Opinion(p,owner));
                ideo.RemovePrecept(ideo.GetPrecept(D("SlaveCount_Unlimited")),true); Add(ideo,"SlaveCount_One");
                p.ideo.IdeoTrackerTickInterval(1); Equal(-9f,MoodSum(p)); Equal(-15,Opinion(p,owner));
            });
        }
        finally { boundary.UnpatchAll(boundary.Id); }
    }

    private static void CountWorkTests()
    {
        Check("same owner mood and many observers share one count per generation without unrelated scans", () =>
        {
            FeedbackContext(); var owner=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); BoundGroup(owner,3);
            var observers=Enumerable.Range(0,12).Select(_=>Believer(new[] {"SlaveCount_One"})).ToArray();
            var untouched=MakePawn(PawnIdentity.Master); BoundGroup(untouched,5); MakeMap(observers.Append(owner).Append(untouched).ToArray());
            calls.Clear(); Equal(-9f,MoodSum(owner)); foreach(var p in observers) Equal(-15,Opinion(p,owner));
            for(int i=0;i<30;i++) { owner.needs.mood.thoughts.ThoughtInterval(); Equal(-9f,MoodSum(owner)); }
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("CountBoundSlaves"));
            Equal(0,CallCount("CountMasters")); Equal(0,CallCount("CollectManagedChained")); Equal(0,CallCount("Query"));
            tick+=100; owner.needs.mood.thoughts.ThoughtInterval(); foreach(var p in observers) Equal(-15,Opinion(p,owner));
            Equal(2,CallCount("CollectMembers")); Equal(2,CallCount("CountBoundSlaves"));
        });
        Check("finite valid zero is cached; non-master own mood and unlimited observer do no population work", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SlaveCount_One"}); var unlimited=Believer(new[] {"MasterCount_Unlimited","SlaveCount_Unlimited"});
            var owner=Believer(new[] {"SlaveCount_One"},PawnIdentity.Master); MakeMap(p,unlimited,owner);
            calls.Clear(); Equal(0f,MoodSum(p)); Equal(0f,MoodSum(unlimited)); Equal(0,Opinion(unlimited,owner)); Equal(0,calls.Count);
            // 有限档达标仍需一次必要统计，成功零人数可以复用；不将所有零效果都误要求为零查询。
            Equal(0f,MoodSum(owner)); for(int i=0;i<20;i++) { owner.needs.mood.thoughts.ThoughtInterval(); Equal(0,Opinion(p,owner)); }
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("CountBoundSlaves")); Equal(0,CallCount("CountMasters"));
        });
        Check("master total prepares once across believers, without bound/environment/leader counts", () =>
        {
            FeedbackContext(); var observers=Enumerable.Range(0,8).Select(_=>Believer(new[] {"MasterCount_One"})).ToArray();
            MakeMap(observers); MakeMap(MakePawn(PawnIdentity.Master),MakePawn(PawnIdentity.Master));
            calls.Clear(); foreach(var p in observers) Equal(-6f,MoodSum(p));
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("CountMasters")); Equal(0,CallCount("CountBoundSlaves"));
            Equal(0,CallCount("CollectManagedChained")); Equal(0,CallCount("Query"));
        });
    }
}
