// 实际摄入、属性、思绪、倒计时及健康Scribe；只隔离完整世界服务，不仿写药效算法。
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static HediffDef gelEffect;
    private static ThoughtDef gelEffectMood;
    private static Action<Pawn> gelEffectOnMake, gelEffectOnAdded;
    private static Action<Pawn> gelBeforeDoer;
    private static HediffDef gelIngestionObserver;

    private static void GelThirdStepTests(string repo, Harmony harmony)
    {
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_CorruptionGelEffect.xml"));
        gelEffect = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode("HediffDef"), false);
        gelEffectMood = DirectXmlToObject.ObjectFromXml<ThoughtDef>(xml.DocumentElement.SelectSingleNode("ThoughtDef"), false);
        DefDatabase<HediffDef>.Add(gelEffect); DefDatabase<ThoughtDef>.Add(gelEffectMood);
        // 读取实际物品XML；ResourceBase的引擎基础字段沿现有商品夹具，摄入及药品组件不另写参数。
        DefDatabase<EffecterDef>.Add(new EffecterDef { defName = "EatVegetarian" });
        DefDatabase<SoundDef>.Add(new SoundDef { defName = "Meal_Eat" });
        xml.Load(Path.Combine(repo, "1.7/Defs/ThingsDefs/SSC_CorruptionGel.xml"));
        var authored = xml.DocumentElement.SelectSingleNode("ThingDef");
        var product = DefDatabase<ThingDef>.GetNamed("SSC_CorruptionGel");
        product.ingestible = DirectXmlToObject.ObjectFromXml<IngestibleProperties>(authored.SelectSingleNode("ingestible"), false);
        product.comps = DirectXmlToObject.ObjectFromXml<List<CompProperties>>(authored.SelectSingleNode("comps"), false);
        product.thingClass = typeof(ThingWithComps); product.ingestible.parent = product;
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Require(errors.Count == 0, string.Join('\n', errors));
        // 无营养、无固定食物中毒风险的原生基础属性；不替代IngestedCalculateAmounts或扣份逻辑。
        StatDefOf.Nutrition = GelIngestionStat("Nutrition", 0);
        StatDefOf.FoodPoisonChanceFixedHuman = GelIngestionStat("FoodPoisonChanceFixedHuman", 0);
        var oldStoryteller = Current.Game.storyteller;
        Current.Game.storyteller = Bare<Storyteller>(); Current.Game.storyteller.difficulty = new Difficulty();
        var work = DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"); bool oldCacheable = work.cacheable;
        work.cacheable = true; work.Worker.SetCacheability(false);
        var maker = AccessTools.Method(typeof(HediffMaker), nameof(HediffMaker.MakeHediff));
        var addition = AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
            new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) });
        harmony.Patch(maker, postfix: new HarmonyMethod(typeof(Program), nameof(GelEffectMakeCallback)));
        harmony.Patch(addition, postfix: new HarmonyMethod(typeof(Program), nameof(GelEffectAddedCallback)) { priority = Priority.Last });
        var ingestionNotify = AccessTools.Method(typeof(Hediff), nameof(Hediff.Notify_IngestedThing));
        harmony.Patch(ingestionNotify, prefix: new HarmonyMethod(typeof(Program), nameof(GelBeforeDoerCallback)));
        gelIngestionObserver = AddGelDef(new HediffDef { defName = "GelIngestionObserver", hediffClass = typeof(Hediff), initialSeverity = 1 });
        harmony.CreateClassProcessor(typeof(SSCCorruptionGelIngestedPatch)).Patch();
        // 原版扣份、分堆、Doer和非成瘾PrePost继续执行；声音、历史事件、原世界预约服务属于宿主边界。
        var reservations = AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis");
        var drugPost = AccessTools.Method(typeof(CompDrug), nameof(CompDrug.PostIngested));
        harmony.Patch(reservations, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(drugPost, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        try
        {
            Check("gel native ingestion consumes one item for an ordinary pawn without granting needs", () =>
            {
                var p = GelConsumer(); var item = ThingMaker.MakeThing(product); item.stackCount = 3;
                Equal(0, p.needs.AllNeeds.Count); Require(p.TryGetComp<CompSexSlaveTraining>() == null);
                item.Ingested(p, 1f); Equal(2, item.stackCount);
                var effect = SSCCorruptionGelEffectUtility.Find(p); Require(effect != null); Equal(60000, effect.Timer.ticksToDisappear);
                Near(.95f, GelWork(p)); Equal(0, p.needs.AllNeeds.Count);
                var cache = (System.Collections.IDictionary)AccessTools.Field(typeof(StatWorker), "temporaryStatCache").GetValue(work.Worker);
                Require(cache.Contains(p), "must exercise the real stat cache");
            });
            Check("gel repeat doses refresh the same owner without stacking mood or work", () =>
            {
                var p = GelConsumer(); Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect));
                var effect = SSCCorruptionGelEffectUtility.Find(p); effect.Timer.ticksToDisappear = 900;
                Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); Require(ReferenceEquals(effect, SSCCorruptionGelEffectUtility.Find(p)));
                Equal(60000, effect.Timer.ticksToDisappear); Near(1, effect.Severity); Near(.95f, GelWork(p));
                Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == gelEffect));
                Require(GelMoodActive(p)); Near(20, gelEffectMood.stages[0].baseMoodEffect);
                p.health.AddHediff(gelEffect); Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == gelEffect)); Near(.95f, GelWork(p));
            });
            Check("gel pre-Doer health replacement cannot receive an old ingestion dose", () =>
            {
                var p = GelConsumer(); p.health.AddHediff(gelIngestionObserver);
                var oldHealth = p.health; var item = ThingMaker.MakeThing(product); item.stackCount = 3;
                gelBeforeDoer = pawn => pawn.health = new Pawn_HealthTracker(pawn);
                try { item.Ingested(p, 1f); }
                finally { gelBeforeDoer = null; }
                Require(!ReferenceEquals(oldHealth, p.health)); Equal(2, item.stackCount);
                Require(SSCCorruptionGelEffectUtility.Find(p) == null, "old dose must not bind to the replacement health tracker");
                item.Ingested(p, 1f); Require(SSCCorruptionGelEffectUtility.Find(p) != null);
            });
            Check("gel pre-Doer personality replacement cannot renew the old body's dose", () =>
            {
                var p = GelConsumer(); Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect));
                var effect = SSCCorruptionGelEffectUtility.Find(p); effect.Timer.ticksToDisappear = 1234;
                p.health.AddHediff(gelIngestionObserver); var item = ThingMaker.MakeThing(product); item.stackCount = 3;
                gelBeforeDoer = pawn => { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var mutation)); mutation.Dispose(); };
                try { item.Ingested(p, 1f); }
                finally { gelBeforeDoer = null; }
                Equal(2, item.stackCount); Require(ReferenceEquals(effect, SSCCorruptionGelEffectUtility.Find(p)));
                Equal(1234, effect.Timer.ticksToDisappear);
                item.Ingested(p, 1f); Equal(60000, effect.Timer.ticksToDisappear);
            });
            Check("gel nested native ingestion restores the outer pawn's original context", () =>
            {
                var outer = GelConsumer(); var inner = GelConsumer(); outer.health.AddHediff(gelIngestionObserver);
                var a = ThingMaker.MakeThing(product); a.stackCount = 3; var b = ThingMaker.MakeThing(product); b.stackCount = 3;
                gelBeforeDoer = _ => b.Ingested(inner, 1f);
                try { a.Ingested(outer, 1f); }
                finally { gelBeforeDoer = null; }
                Equal(2, a.stackCount); Equal(2, b.stackCount);
                Require(SSCCorruptionGelEffectUtility.IsActive(SSCCorruptionGelEffectUtility.Find(outer)));
                Require(SSCCorruptionGelEffectUtility.IsActive(SSCCorruptionGelEffectUtility.Find(inner)));
            });
            Check("gel failed native ingestion releases its context and cannot authorize a late Doer", () =>
            {
                var p = GelConsumer(); p.health.AddHediff(gelIngestionObserver); var item = ThingMaker.MakeThing(product); item.stackCount = 3;
                gelBeforeDoer = _ => throw new InvalidOperationException("gel ingestion fixture failure");
                bool threw = false;
                try { item.Ingested(p, 1f); } catch (InvalidOperationException) { threw = true; }
                finally { gelBeforeDoer = null; }
                Require(threw); Equal(3, item.stackCount);
                product.ingestible.outcomeDoers[0].DoIngestionOutcome(p, item, 1);
                Require(SSCCorruptionGelEffectUtility.Find(p) == null, "failed ingestion must not leave a reusable dose context");
                item.Ingested(p, 1f); Equal(2, item.stackCount); Require(SSCCorruptionGelEffectUtility.Find(p) != null);
            });
            Check("gel native timer expiry and health removal clear cached effects", () =>
            {
                var p = GelConsumer(true); Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect));
                var effect = SSCCorruptionGelEffectUtility.Find(p); Near(.95f, GelWork(p)); Require(GelMoodActive(p));
                var thoughts = new List<Thought>(); p.needs.mood.thoughts.situational.AppendMoodThoughts(gelEffectMood, thoughts);
                Equal(1, thoughts.Count); Near(20, thoughts[0].MoodOffset());
                var thoughtCache = (System.Collections.IList)AccessTools.Field(typeof(SituationalThoughtHandler), "cachedThoughts").GetValue(p.needs.mood.thoughts.situational);
                Require(thoughtCache.Count > 0, "must populate the real situational thought cache");
                effect.Timer.SetDuration(1); effect.PostTick(); Require(effect.ShouldRemove); Near(1, GelWork(p)); Require(!GelMoodActive(p));
                Equal(0, thoughtCache.Count);
                p.health.RemoveHediff(effect); Require(SSCCorruptionGelEffectUtility.Find(p) == null); Near(1, GelWork(p));
            });
            Check("gel masters consume an item without an effect and reject external additions", () =>
            {
                var p = GelConsumer(); AttachToggleTraining(p).pawnIdentity = PawnIdentity.Master;
                var item = ThingMaker.MakeThing(product); item.stackCount = 2; item.Ingested(p, 1f); Equal(1, item.stackCount);
                Require(SSCCorruptionGelEffectUtility.Find(p) == null); p.health.AddHediff(gelEffect);
                Require(SSCCorruptionGelEffectUtility.Find(p) == null); Near(1, GelWork(p)); Require(!GelMoodActive(p));
            });
            Check("gel identity selection immediately clears the effect and leaving master does not restore it", () =>
            {
                var p = GelConsumer(true); var training = AttachToggleTraining(p); training.ReplaceMasterQualification(true);
                Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); Near(.95f, GelWork(p));
                Require(SSCIdentityUtility.TrySetIdentity(p, PawnIdentity.Master));
                Require(SSCCorruptionGelEffectUtility.Find(p) == null); Near(1, GelWork(p)); Require(!GelMoodActive(p));
                Require(SSCIdentityUtility.TrySetIdentity(p, PawnIdentity.Unset)); Require(SSCCorruptionGelEffectUtility.Find(p) == null);
            });
            Check("gel hollow and personality mutation pause effects while the body timer continues", () =>
            {
                var p = GelConsumer(); Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); var effect = SSCCorruptionGelEffectUtility.Find(p);
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var mutation));
                try { Near(1, GelWork(p)); Require(!GelMoodActive(p)); effect.PostTick(); Equal(59999, effect.Timer.ticksToDisappear); }
                finally { mutation.Dispose(); }
                Near(.95f, GelWork(p)); Require(GelMoodActive(p));
                var hollow = DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_SSC_PersonalityExcreted_Done") ??
                    AddGelDef(new HediffDef { defName = "Hediff_SSC_PersonalityExcreted_Done", hediffClass = typeof(Hediff), initialSeverity = 1 });
                var oldHollow = SSCDefOf.SSC_PersonalityExcreted_Done; SSCDefOf.SSC_PersonalityExcreted_Done = hollow;
                try
                {
                    var record = p.health.AddHediff(hollow); Near(1, GelWork(p)); Require(!GelMoodActive(p));
                    p.health.RemoveHediff(record); Near(.95f, GelWork(p)); Equal(59999, effect.Timer.ticksToDisappear);
                }
                finally { SSCDefOf.SSC_PersonalityExcreted_Done = oldHollow; }
            });
            Check("gel use and production costs multiply and full batch pauses only production", () =>
            {
                var p = GelPawn(); GelToggle(p, true); Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); Near(.855f, GelWork(p));
                gelAccept = false; GelTick(GelComp(p), 120000); Near(.95f, GelWork(p));
                p.health.RemoveHediff(SSCCorruptionGelEffectUtility.Find(p)); Near(1, GelWork(p)); gelAccept = true;
            });
            Check("gel a new owner created during MakeHediff is not adopted by the old dose", () =>
            {
                var p = GelConsumer(); Hediff_SSCCorruptionGelEffect replacement = null;
                gelEffectOnMake = pawn => { Require(SSCCorruptionGelEffectUtility.Apply(pawn, gelEffect)); replacement = SSCCorruptionGelEffectUtility.Find(pawn); replacement.Timer.ticksToDisappear = 1234; };
                try { Require(!SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); }
                finally { gelEffectOnMake = null; }
                Require(ReferenceEquals(replacement, SSCCorruptionGelEffectUtility.Find(p))); Equal(1234, replacement.Timer.ticksToDisappear);
            });
            Check("gel health add callbacks cannot transfer a dose to another owner or personality", () =>
            {
                var p = GelConsumer(); Hediff_SSCCorruptionGelEffect replacement = null;
                gelEffectOnAdded = pawn => { pawn.health.RemoveHediff(SSCCorruptionGelEffectUtility.Find(pawn)); replacement = (Hediff_SSCCorruptionGelEffect)pawn.health.AddHediff(gelEffect); replacement.Timer.ticksToDisappear = 1200; };
                try { Require(!SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); }
                finally { gelEffectOnAdded = null; }
                Require(ReferenceEquals(replacement, SSCCorruptionGelEffectUtility.Find(p))); Equal(1200, replacement.Timer.ticksToDisappear);
                SSCPersonalityRuntimeUtility.MutationScope mutation = null;
                var other = GelConsumer(); gelEffectOnAdded = pawn => Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out mutation));
                try { Require(!SSCCorruptionGelEffectUtility.Apply(other, gelEffect)); Require(SSCCorruptionGelEffectUtility.Find(other) == null); }
                finally { gelEffectOnAdded = null; mutation?.Dispose(); }
            });
            Check("gel remaining duration survives native Scribe and re-save without a fresh dose", () =>
            {
                var p = GelConsumer(); Require(SSCCorruptionGelEffectUtility.Apply(p, gelEffect)); SSCCorruptionGelEffectUtility.Find(p).Timer.ticksToDisappear = 1234;
                var restored = Load(Save(p)); SSCCorruptionGelEffectUtility.RefreshBodyState(restored);
                Equal(1234, SSCCorruptionGelEffectUtility.Find(restored).Timer.ticksToDisappear);
                restored = Load(Save(restored)); Equal(1234, SSCCorruptionGelEffectUtility.Find(restored).Timer.ticksToDisappear);
                // 模拟完整加载后的原生协调入口，主人旧档不能保留孤立药效。
                AttachToggleTraining(restored).pawnIdentity = PawnIdentity.Master; SSCAbilityToggleUtility.SyncMarkers(restored);
                Require(SSCCorruptionGelEffectUtility.Find(restored) == null);
                string invalid = Save(p); Edit(invalid, d => d.Descendants("ticksToDisappear").Single().Remove());
                var expired = Load(invalid); SSCCorruptionGelEffectUtility.RefreshBodyState(expired);
                Require(SSCCorruptionGelEffectUtility.Find(expired) == null);
            });
            Check("gel clone and personality filters reject source medicine without removing the clone's own dose", () =>
            {
                Require(!(bool)AccessTools.Method(typeof(RabbitCloneUtility), "ShouldKeepSscHediff").Invoke(null, new object[] { gelEffect }));
                var stored = new CompPersonalityStore(); stored.hediffTags[gelEffect] = 1; var copy = new CompPersonalityStore(); copy.CopyFrom(stored);
                Require(!copy.hediffTags.ContainsKey(gelEffect)); var source = GelConsumer(); var clone = GelConsumer();
                Require(SSCCorruptionGelEffectUtility.Apply(source, gelEffect)); Require(SSCCorruptionGelEffectUtility.Apply(clone, gelEffect));
                var own = SSCCorruptionGelEffectUtility.Find(clone); own.Timer.ticksToDisappear = 4321;
                AccessTools.Method(typeof(RabbitCloneUtility), "CopySscHediffsFromSource").Invoke(null, new object[] { source, clone });
                Require(ReferenceEquals(own, SSCCorruptionGelEffectUtility.Find(clone))); Equal(4321, own.Timer.ticksToDisappear);
            });
            GelFourthStepTests(repo, harmony);
        }
        finally
        {
            gelEffectOnMake = gelEffectOnAdded = null; gelAccept = true;
            gelBeforeDoer = null;
            Current.Game.storyteller = oldStoryteller;
            work.cacheable = oldCacheable; work.Worker.DeleteStatCache(); work.Worker.SetCacheability(work.immutable);
            harmony.Unpatch(maker, AccessTools.Method(typeof(Program), nameof(GelEffectMakeCallback)));
            harmony.Unpatch(addition, AccessTools.Method(typeof(Program), nameof(GelEffectAddedCallback)));
            harmony.Unpatch(ingestionNotify, AccessTools.Method(typeof(Program), nameof(GelBeforeDoerCallback)));
            harmony.Unpatch(reservations, AccessTools.Method(typeof(Program), nameof(Skip)));
            harmony.Unpatch(drugPost, AccessTools.Method(typeof(Program), nameof(Skip)));
        }
    }

    private static Pawn GelConsumer(bool mood = false)
    {
        var p = NewPawn(); p.ageTracker = new Pawn_AgeTracker(p);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(p.ageTracker, 0);
        p.story = new Pawn_StoryTracker(p); p.mindState = new Pawn_MindState(p); p.carryTracker = new Pawn_CarryTracker(p);
        if (mood)
        {
            if (DefDatabase<NeedDef>.GetNamedSilentFail("Mood") == null)
                DefDatabase<NeedDef>.Add(new NeedDef { defName = "Mood", needClass = typeof(Need_Mood) });
            p.needs.mood = new Need_Mood(p) { def = DefDatabase<NeedDef>.GetNamed("Mood") };
            p.needs.AllNeeds.Add(p.needs.mood);
        }
        return p;
    }
    private static bool GelMoodActive(Pawn p) => gelEffectMood.Worker.CurrentState(p).Active;
    private static StatDef GelIngestionStat(string name, float value)
    {
        var stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
        if (stat == null) { stat = new StatDef { defName = name, defaultBaseValue = value }; DefDatabase<StatDef>.Add(stat); }
        return stat;
    }
    private static void GelEffectMakeCallback(HediffDef def, Pawn pawn)
    {
        if (def != gelEffect) return; var callback = gelEffectOnMake; gelEffectOnMake = null; callback?.Invoke(pawn);
    }
    private static void GelEffectAddedCallback(Hediff hediff)
    {
        if (hediff.def != gelEffect) return; var callback = gelEffectOnAdded; gelEffectOnAdded = null; callback?.Invoke(hediff.pawn);
    }
    private static void GelBeforeDoerCallback(Hediff __instance)
    {
        if (__instance.def != gelIngestionObserver) return;
        var callback = gelBeforeDoer; gelBeforeDoer = null; callback?.Invoke(__instance.pawn);
    }
}
