using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RabbitCookingTests(Harmony harmony, string repo, string game)
    {
        if (!SscTestProfile.IncludesCore) return;
        HediffDef ordinary = null, final = null, effect = null;
        AbilityDef abilityDef = null;
        StatDef cookSpeed = null;
        var boundary = new Harmony("ssc.tests.rabbit-cooking-engine");
        var maps = AccessTools.Field(typeof(Game), "maps");
        var originalMaps = maps.GetValue(Current.Game);
        var questManager = AccessTools.Field(typeof(Game), "questManager");
        var originalQuestManager = questManager.GetValue(Current.Game);
        try
        {
            boundary.Patch(AccessTools.Method(typeof(ShaderDatabase), "TryLoadShader"),
                prefix: new HarmonyMethod(typeof(Program), nameof(RabbitNoShader)));
            boundary.Patch(AccessTools.Method(typeof(Graphic_Single), nameof(Graphic_Single.Init)),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            boundary.Patch(AccessTools.Method(typeof(SolidColorMaterials), nameof(SolidColorMaterials.SimpleSolidColorMaterial)),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            boundary.Patch(AccessTools.Method(typeof(MaterialPool), nameof(MaterialPool.MatFrom), new[] { typeof(MaterialRequest) }),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            Check("rabbit cooking authored stages multiply the native cooking curve output", () =>
            {
                (ordinary, final, effect, abilityDef, cookSpeed) = LoadRabbitCookingDefs(repo, game);
                var pawn = RabbitCook();
                void Expect(float input, float expected)
                {
                    cookSpeed.Worker.FinalizeValue(StatRequest.For(pawn), ref input, true);
                    Require(Math.Abs(input - expected) < .0001f, $"expected {expected}, actual {input}");
                }

                Expect(0, .4f);
                Expect(20, 1.6f);
                var training = pawn.health.AddHediff(ordinary);
                training.Severity = .2f;
                Expect(20, 1.84f);
                training.Severity = .5f;
                Expect(20, 2.08f);
                pawn.health.RemoveHediff(training);
                pawn.health.AddHediff(final);
                Expect(20, 2.4f);
                var buff = pawn.health.AddHediff(effect);
                Expect(0, .9f);
                Expect(20, 3.6f);
                pawn.health.RemoveHediff(buff);
                Expect(20, 2.4f);
            });
            if (cookSpeed == null) return;

            // Map layout and world layer checks require the game host. Keep the native ability,
            // verb, Hediff effect, and cooldown while supplying just those environment checks.
            maps.SetValue(Current.Game, new List<Map> { Bare<Map>() });
            questManager.SetValue(Current.Game, new QuestManager());
            boundary.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld)),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            boundary.Patch(AccessTools.PropertyGetter(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.CanBeAwake)),
                prefix: new HarmonyMethod(typeof(Program), nameof(RabbitHasAwakeCapacity)));
            boundary.Patch(AccessTools.Method(typeof(Verb_CastAbility), nameof(Verb_CastAbility.CanHitTarget)),
                prefix: new HarmonyMethod(typeof(Program), nameof(RabbitSelfCanHit)));
            boundary.Patch(AccessTools.Method(typeof(Verb), "CausesTimeSlowdown"),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            Check("rabbit cooking self cast retains the current bill and starts the native timer and cooldown", () =>
            {
                var pawn = RabbitCook();
                PutOnTransferMap(pawn);
                var bill = Bare<Bill_Production>();
                bill.recipe = new RecipeDef { workSkill = SkillDefOf.Cooking };
                var job = new Job { bill = bill };
                pawn.jobs.curJob = job;
                pawn.jobs.curDriver = new JobDriver_DoBill { pawn = pawn, job = job };
                var mark = pawn.health.AddHediff(final);
                var ability = GrantRabbitCooking(pawn, mark, abilityDef);
                Require(!ability.Activate(RabbitCook(), LocalTargetInfo.Invalid));
                Equal(0, ability.CooldownTicksRemaining);
                ability.QueueCastingJob(pawn, LocalTargetInfo.Invalid);
                Equal(job, pawn.CurJob);
                Equal(10000, pawn.health.hediffSet.GetFirstHediffOfDef(effect).TryGetComp<HediffComp_Disappears>().ticksToDisappear);
                Equal(60000, ability.CooldownTicksRemaining);
                Require(!ability.Activate(pawn, LocalTargetInfo.Invalid));
            });

            Check("rabbit cooking native save retains active time and cooldown on the same body", () =>
            {
                var pawn = RabbitCook();
                var mark = pawn.health.AddHediff(final);
                var ability = GrantRabbitCooking(pawn, mark, abilityDef);
                var timer = pawn.health.AddHediff(effect).TryGetComp<HediffComp_Disappears>();
                timer.ticksToDisappear = 3811;
                ability.StartCooldown(53811);
                var tracker = pawn.abilities;
                string path = Path.Combine(saveDirectory, "rabbit-cooking-state.xml");
                Scribe.saver.InitSaving(path, "RabbitCooking");
                Scribe_Deep.Look(ref tracker, "abilities", pawn);
                timer.CompExposeData();
                Scribe.saver.FinalizeSaving();

                tracker = null;
                var restoredTimer = HediffMaker.MakeHediff(effect, pawn).TryGetComp<HediffComp_Disappears>();
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref tracker, "abilities", pawn);
                restoredTimer.CompExposeData();
                Scribe.loader.FinalizeLoading();
                var restoredAbility = tracker.GetAbility(abilityDef);
                Require(restoredAbility != null && !ReferenceEquals(ability, restoredAbility));
                Equal(53811, restoredAbility.CooldownTicksRemaining);
                Equal(3811, restoredTimer.ticksToDisappear);
            });

            Check("rabbit cooking production skips both poison causes only during qualifying meal output", () =>
            {
                harmony.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_RabbitCooking")).Patch();
                var cook = RabbitCook();
                var meal = RabbitMeal("RabbitTestMeal", FoodPreferability.MealSimple);
                var other = RabbitMeal("RabbitTestOtherMeal", FoodPreferability.MealSimple);
                var kibble = RabbitMeal("RabbitTestKibble", FoodPreferability.DesperateOnly);
                var recipe = new RecipeDef
                {
                    workSkill = SkillDefOf.Cooking,
                    products = new() { new ThingDefCountClass(meal, 4), new ThingDefCountClass(kibble, 1) }
                };
                var bill = Bare<Bill_Production>();
                bill.recipe = recipe;
                cook.jobs.curJob = new Job { bill = bill };
                cook.jobs.curDriver = new JobDriver_DoBill { pawn = cook, job = cook.CurJob };
                void Expect(ThingDef def, FoodPoisonCause? cause)
                {
                    var product = new ThingWithComps { def = def, stackCount = 4 };
                    product.InitializeComps();
                    product.Notify_RecipeProduced(cook);
                    var comp = product.TryGetComp<CompFoodPoisonable>();
                    Equal(cause.HasValue ? 1f : 0f, (float)AccessTools.Field(typeof(CompFoodPoisonable), "poisonPct").GetValue(comp));
                    if (cause.HasValue) Equal(cause.Value, (FoodPoisonCause)AccessTools.Field(typeof(CompFoodPoisonable), "cause").GetValue(comp));
                }

                cook.health.AddHediff(final);
                Expect(meal, FoodPoisonCause.FilthyKitchen);
                var timer = cook.health.AddHediff(effect).TryGetComp<HediffComp_Disappears>();
                Expect(meal, null);
                Expect(other, FoodPoisonCause.FilthyKitchen);
                Expect(kibble, FoodPoisonCause.FilthyKitchen);
                recipe.workSkill = null;
                Expect(meal, FoodPoisonCause.FilthyKitchen);
                recipe.workSkill = SkillDefOf.Cooking;
                RoomStatDefOf.FoodPoisonChance.roomlessScore = 0f;
                Expect(meal, null);
                timer.ticksToDisappear = 0;
                Expect(meal, FoodPoisonCause.IncompetentCook);
            });

            Check("rabbit cooking personality cleanup removes the effect and rejects the previous ability instance", () =>
            {
                var pawn = RabbitCook();
                PutOnTransferMap(pawn);
                var mark = pawn.health.AddHediff(final);
                var oldAbility = GrantRabbitCooking(pawn, mark, abilityDef);
                pawn.health.AddHediff(effect);
                oldAbility.StartCooldown(50000);
                var store = new CompPersonalityStore();
                PetSpecializationUtility.StoreExclusivePetTags(store, pawn);
                Require(store.HasTag(final) && !store.HasTag(effect));
                ExcretionUtility.CleanConsumerData(pawn);
                Require(!pawn.health.hediffSet.HasHediff(effect) && pawn.abilities.GetAbility(abilityDef) == null);
                PetSpecializationUtility.ApplyExclusivePetTags(pawn, store);
                var newAbility = GrantRabbitCooking(pawn, pawn.health.hediffSet.GetFirstHediffOfDef(final), abilityDef);
                Equal(0, newAbility.CooldownTicksRemaining);
                Require(!oldAbility.Activate(pawn, LocalTargetInfo.Invalid));
            });
        }
        finally
        {
            boundary.UnpatchAll(boundary.Id);
            maps.SetValue(Current.Game, originalMaps);
            questManager.SetValue(Current.Game, originalQuestManager);
        }
    }

    private static (HediffDef, HediffDef, HediffDef, AbilityDef, StatDef) LoadRabbitCookingDefs(string repo, string game)
    {
        XmlDocument Read(string root, string path)
        {
            var document = new XmlDocument();
            document.Load(Path.Combine(root, path));
            return document;
        }
        T Parse<T>(XmlNode node) where T : Def, new()
        {
            var def = DirectXmlToObject.ObjectFromXml<T>(node, false);
            DefDatabase<T>.Add(def);
            return def;
        }

        DefDatabase<StatCategoryDef>.Add(new StatCategoryDef { defName = "PawnWork" });
        DefDatabase<StatDef>.Add(new StatDef { defName = "CleaningSpeed", defaultBaseValue = 1f });
        DefDatabase<StatDef>.Add(new StatDef { defName = "WorkSpeedGlobal", defaultBaseValue = 1f });
        DefDatabase<PawnCapacityDef>.Add(new PawnCapacityDef { defName = "Sight" });
        DefDatabase<PawnCapacityDef>.Add(new PawnCapacityDef { defName = "Manipulation" });
        DefDatabase<JobDef>.Add(new JobDef { defName = "CastAbilityOnThing" });
        SkillDefOf.Cooking = new SkillDef { defName = "Cooking" };
        DefDatabase<SkillDef>.Add(SkillDefOf.Cooking);
        StatDefOf.Ability_Duration = new StatDef { defName = "Ability_Duration" };
        DefDatabase<StatDef>.Add(StatDefOf.Ability_Duration);
        StatDefOf.Nutrition = new StatDef { defName = "Nutrition", defaultBaseValue = 1f };
        StatDefOf.FoodPoisonChance = new StatDef { defName = "FoodPoisonChance", defaultBaseValue = 1f };
        RoomStatDefOf.FoodPoisonChance = new RoomStatDef { roomlessScore = 1f };
        human.race.lifeStageAges = new() { new LifeStageAge { def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult } } };

        var runtime = Path.Combine(repo, "1.7");
        Parse<StatDef>(Read(runtime, "Defs/StatDefs/SSC_PetRabbitCookingFactor.xml").DocumentElement.FirstChild);
        var baseStats = Read(game, "Data/Core/Defs/Stats/Stats_Pawns_WorkRecipes.xml");
        var patch = Read(runtime, "Patches/SSC_PetRabbitCooking.xml");
        Require(DirectXmlToObject.ObjectFromXml<PatchOperation>(patch.DocumentElement.FirstChild, false).Apply(baseStats));
        var cookSpeed = Parse<StatDef>(baseStats.SelectSingleNode("Defs/StatDef[defName='CookSpeed']"));
        var ability = Parse<AbilityDef>(Read(runtime, "Defs/AbilityDefs/SSC_PetRabbitCarefulCooking.xml").DocumentElement.FirstChild);
        var hediffs = Read(runtime, "Defs/HediffDefs/SSC_HediffDefs_PetSpecializations.xml");
        HediffDef ReadHediff(string name)
        {
            var placeholder = DefDatabase<HediffDef>.GetNamedSilentFail(name);
            if (placeholder != null) AccessTools.Method(typeof(DefDatabase<HediffDef>), "Remove").Invoke(null, new object[] { placeholder });
            return Parse<HediffDef>(hediffs.SelectSingleNode($"Defs/HediffDef[defName='{name}']"));
        }
        var ordinary = ReadHediff("SSC_Hediff_PetRabbit");
        var final = ReadHediff("SSC_Hediff_PetRabbit_Final");
        var effect = ReadHediff("SSC_Hediff_PetRabbitCarefulCooking");
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        return (ordinary, final, effect, ability, cookSpeed);
    }

    private static Pawn RabbitCook()
    {
        var pawn = Pawn(PawnIdentity.Slave);
        pawn.jobs = new Pawn_JobTracker(pawn);
        pawn.stances = new Pawn_StanceTracker(pawn);
        pawn.mindState = new Pawn_MindState(pawn);
        pawn.abilities = new Pawn_AbilityTracker(pawn);
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        return pawn;
    }

    private static Ability GrantRabbitCooking(Pawn pawn, Hediff mark, AbilityDef ability)
    {
        var clock = AccessTools.Field(typeof(TickManager), "ticksGameInt");
        for (int i = 0; i < 60 && !pawn.IsHashIntervalTick(60); i++)
            clock.SetValue(Find.TickManager, Find.TickManager.TicksGame + 1);
        float adjustment = 0f;
        mark.TryGetComp<HediffComp_PetRabbitCookingAbility>().CompPostTick(ref adjustment);
        var granted = pawn.abilities.GetAbility(ability);
        Require(granted != null, "terminal Hediff did not grant the ability");
        return granted;
    }

    private static ThingDef RabbitMeal(string name, FoodPreferability preferability)
    {
        var def = Bare<ThingDef>();
        def.defName = name;
        def.thingClass = typeof(ThingWithComps);
        def.comps = new() { new CompProperties_FoodPoisonable() };
        def.ingestible = new IngestibleProperties { parent = def, preferability = preferability };
        return def;
    }

    private static bool RabbitNoShader(out UnityEngine.Shader result, ref bool __result)
    {
        result = null;
        __result = true;
        return false;
    }

    private static bool RabbitHasAwakeCapacity(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool RabbitSelfCanHit(Verb_CastAbility __instance, ref bool __result)
    {
        if (__instance.Ability is not Ability_PetRabbitCarefulCooking) return true;
        __result = true;
        return false;
    }
}
