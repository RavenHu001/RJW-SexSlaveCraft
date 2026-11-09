using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static bool foxCanTouch;

    private static void FoxEmergencyTendTests(string repo)
    {
        if (!SscTestProfile.IncludesCore) return;

        var boundary = new Harmony("ssc.tests.fox-emergency-tend-engine");
        var maps = AccessTools.Field(typeof(Game), "maps");
        object oldMaps = maps.GetValue(Current.Game);
        var quests = AccessTools.Field(typeof(Game), "questManager");
        object oldQuests = quests.GetValue(Current.Game);
        ProgramState oldState = Current.ProgramState;
        var fleshType = AccessTools.Field(typeof(RaceProperties), "fleshType");
        var bloodDef = AccessTools.Field(typeof(RaceProperties), "bloodDef");
        object oldFleshType = fleshType.GetValue(human.race);
        object oldBloodDef = bloodDef.GetValue(human.race);
        var oldLifeStages = human.race.lifeStageAges;
        try
        {
            maps.SetValue(Current.Game, new List<Map> { Bare<Map>(), Bare<Map>() });
            quests.SetValue(Current.Game, new QuestManager());
            Current.ProgramState = ProgramState.Playing;
            fleshType.SetValue(human.race, new FleshTypeDef { isOrganic = true });
            bloodDef.SetValue(human.race, new ThingDef { defName = "FoxFixtureBlood" });
            human.race.lifeStageAges = new() { new LifeStageAge { def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult } } };
            void Patch(System.Reflection.MethodBase method, string prefix)
                => boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));

            Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld)), nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.CanBeAwake)), nameof(RabbitHasAwakeCapacity));
            Patch(AccessTools.Method(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.CapableOf)), nameof(FoxHasCapacity));
            Patch(AccessTools.Method(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach),
                new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) }), nameof(FoxCanApproach));
            Patch(AccessTools.Method(typeof(ReachabilityImmediate), nameof(ReachabilityImmediate.CanReachImmediate),
                new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(Map), typeof(PathEndMode), typeof(Pawn) }), nameof(FoxCanTouch));
            Patch(AccessTools.Method(typeof(RestUtility), nameof(RestUtility.CurrentBed), new[] { typeof(Pawn) }), nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(Pawn), nameof(Verse.Pawn.DrawPos)), nameof(Skip));
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(MoteMaker)).Where(m => m.Name == nameof(MoteMaker.ThrowText)))
                Patch(method, nameof(Skip));

            AbilityDef skill = null;
            HediffDef terminal = null;
            Check("fox emergency tend selects a distant ally and treats all wounds on contact", () =>
            {
                (skill, terminal) = LoadFoxEmergencyTendDefs(repo);
                Require(!ModsConfig.BiotechActive);
                var caster = FoxAbilityPawn();
                var patient = FoxAbilityPawn();
                AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(patient.health, PawnHealthState.Down);
                var injury = FoxTendFixtureHediff<Hediff_Injury>(patient);
                var second = FoxTendFixtureHediff<Hediff_Injury>(patient);
                var missing = (Hediff_MissingPart)FoxTendFixtureHediff<Hediff_MissingPart>(patient,
                    new BodyPartRecord { def = new BodyPartDef { defName = "FoxFixtureFinger", label = "finger" }, parent = patient.RaceProps.body.corePart });
                missing.IsFresh = true;
                var disease = FoxTendFixtureHediff<HediffWithComps>(patient);
                Require(injury.TendableNow() && second.TendableNow() && missing.TendableNow() && disease.TendableNow(),
                    "fixture needs two treatable injuries, a fresh missing part and a treatable disease");
                var ability = GrantFoxEmergencyTend(caster, terminal, skill);
                var target = new LocalTargetInfo(patient);
                foxCanTouch = false;
                Require(ability.verb.ValidateTarget(target, false) && ability.CanApplyOn(target), "distant wound target rejected");
                var job = ability.GetJob(target, LocalTargetInfo.Invalid);
                Equal(typeof(JobDriver_CastAbilityMelee), job.def.driverClass);
                Equal(target, job.targetA);
                Require(!ability.Activate(target, LocalTargetInfo.Invalid));
                Equal(0, ability.CooldownTicksRemaining);

                foxCanTouch = true;
                Require(ability.Activate(target, LocalTargetInfo.Invalid), $"contact cast rejected: {ability.CanCast.Reason}");
                Require(!injury.TendableNow() && !second.TendableNow() && !missing.IsFresh && disease.TendableNow());
                Require(injury.TryGetComp<HediffComp_TendDuration>().tendQuality <= .70f);
                Equal(60000, ability.CooldownTicksRemaining);
            });
            if (skill == null || terminal == null) return;

            Check("fox emergency tend rejects stale targets and withdrawn abilities before cooldown", () =>
            {
                var caster = FoxAbilityPawn();
                var patient = FoxAbilityPawn();
                var injury = FoxTendFixtureHediff<Hediff_Injury>(patient);
                var ability = GrantFoxEmergencyTend(caster, terminal, skill);
                foxCanTouch = true;
                var target = new LocalTargetInfo(patient);
                Require(ability.verb.ValidateTarget(target, false));
                injury.Tended(.4f, .70f, 1);
                Require(!ability.CanApplyOn(target) && !ability.Activate(target, LocalTargetInfo.Invalid));
                Equal(0, ability.CooldownTicksRemaining);

                var newInjury = FoxTendFixtureHediff<Hediff_Injury>(patient);
                var slot = AccessTools.Field(typeof(Thing), "mapIndexOrState");
                slot.SetValue(patient, (sbyte)1);
                Require(!ability.Activate(target, LocalTargetInfo.Invalid));
                slot.SetValue(patient, (sbyte)0);
                caster.health.RemoveHediff(caster.health.hediffSet.GetFirstHediffOfDef(terminal));
                Require(caster.abilities.GetAbility(skill) == null);
                var replacement = GrantFoxEmergencyTend(caster, terminal, skill);
                Require(!ReferenceEquals(replacement, ability) && !ability.Activate(target, LocalTargetInfo.Invalid));
                Require(newInjury.TendableNow());
                Equal(0, ability.CooldownTicksRemaining);
                Equal(0, replacement.CooldownTicksRemaining);
            });

            Check("fox emergency tend cooldown survives native ability tracker save and reload", () =>
            {
                var caster = FoxAbilityPawn();
                var ability = GrantFoxEmergencyTend(caster, terminal, skill);
                ability.StartCooldown(38110);
                var tracker = caster.abilities;
                string path = Path.Combine(saveDirectory, "fox-emergency-tend.xml");
                Scribe.saver.InitSaving(path, "FoxEmergencyTend");
                Scribe_Deep.Look(ref tracker, "abilities", caster);
                Scribe.saver.FinalizeSaving();

                tracker = null;
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref tracker, "abilities", caster);
                Scribe.loader.FinalizeLoading();
                caster.abilities = tracker;
                var restored = tracker.GetAbility(skill);
                Require(restored != null && !ReferenceEquals(restored, ability));
                Equal(38110, restored.CooldownTicksRemaining);
                Require(!restored.CanCast && ((Verb_CastAbility)restored.verb).Ability == restored);
            });
        }
        finally
        {
            boundary.UnpatchAll(boundary.Id);
            maps.SetValue(Current.Game, oldMaps);
            quests.SetValue(Current.Game, oldQuests);
            Current.ProgramState = oldState;
            fleshType.SetValue(human.race, oldFleshType);
            bloodDef.SetValue(human.race, oldBloodDef);
            human.race.lifeStageAges = oldLifeStages;
        }
    }

    private static (AbilityDef, HediffDef) LoadFoxEmergencyTendDefs(string repo)
    {
        T Parse<T>(string path, string xpath) where T : Def, new()
        {
            var xml = new XmlDocument();
            xml.Load(Path.Combine(repo, "1.7/Defs", path));
            var def = DirectXmlToObject.ObjectFromXml<T>(xml.SelectSingleNode(xpath), false);
            var placeholder = DefDatabase<T>.GetNamedSilentFail(def.defName);
            if (placeholder != null) AccessTools.Method(typeof(DefDatabase<T>), "Remove").Invoke(null, new object[] { placeholder });
            DefDatabase<T>.Add(def);
            return def;
        }

        StatDef Stat(string name, float value)
        {
            var stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
            if (stat != null) return stat;
            stat = new StatDef { defName = name, defaultBaseValue = value };
            DefDatabase<StatDef>.Add(stat);
            return stat;
        }

        Stat("ResearchSpeed", 1f);
        Stat("MedicalTendSpeed", 1f);
        StatDefOf.MedicalTendQuality = Stat("MedicalTendQuality", 3f);
        DefDatabase<JobDef>.Add(new JobDef { defName = "CastAbilityOnThingMelee", driverClass = typeof(JobDriver_CastAbilityMelee) });
        var skill = Parse<AbilityDef>("AbilityDefs/SSC_PetFoxEmergencyTend.xml", "Defs/AbilityDef");
        var terminal = Parse<HediffDef>("HediffDefs/SSC_HediffDefs_PetSpecializations.xml", "Defs/HediffDef[defName='SSC_Hediff_PetFox_Final']");
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        return (skill, terminal);
    }

    private static Pawn FoxAbilityPawn()
    {
        var pawn = Pawn(PawnIdentity.Slave);
        pawn.jobs = new Pawn_JobTracker(pawn);
        pawn.mindState = new Pawn_MindState(pawn);
        pawn.abilities = new Pawn_AbilityTracker(pawn);
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        PutOnTransferMap(pawn);
        return pawn;
    }

    private static Hediff FoxTendFixtureHediff<T>(Pawn patient, BodyPartRecord part = null) where T : Hediff
    {
        var def = new HediffDef
        {
            defName = "FoxFixture" + typeof(T).Name, label = "fixture wound",
            tendable = true, hediffClass = typeof(T), initialSeverity = 1f,
            comps = new() { new HediffCompProperties_TendDuration() }
        };
        var hediff = HediffMaker.MakeHediff(def, patient, part ?? patient.RaceProps.body.corePart);
        hediff.Severity = 1f;
        patient.health.hediffSet.hediffs.Add(hediff);
        return hediff;
    }

    private static Ability GrantFoxEmergencyTend(Pawn caster, HediffDef terminal, AbilityDef def)
    {
        var mark = caster.health.AddHediff(terminal);
        var clock = AccessTools.Field(typeof(TickManager), "ticksGameInt");
        while (!caster.IsHashIntervalTick(60))
            clock.SetValue(Find.TickManager, Find.TickManager.TicksGame + 1);
        float adjustment = 0f;
        mark.TryGetComp<SexSlaveCraft.HediffComp_GiveAbility>().CompPostTick(ref adjustment);
        var ability = caster.abilities.GetAbility(def);
        Require(ability is Ability_PetFoxEmergencyTend);
        return ability;
    }

    private static bool FoxHasCapacity(ref bool __result) { __result = true; return false; }
    private static bool FoxCanApproach(ref bool __result) { __result = true; return false; }
    private static bool FoxCanTouch(ref bool __result) { __result = foxCanTouch; return false; }
}
