// AI 辅助生成：实际 XML、生产业务、原版 TraitSet/Hediff/阶段和概率；不启动整场游戏。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using Verse.AI.Group;

internal static class Entry
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        var info = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        var dirs = new[] { game, Path.Combine(repo, "1.7/Assemblies") }.Concat(info.RootElement.GetProperty("references")
            .EnumerateArray().Select(x => Path.GetDirectoryName(x.GetProperty("path").GetString()))).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (var dir in dirs)
            {
                string path = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { repo, game });
    }
}

internal static class Program
{
    private static int passed, failed, traitAdds, traitRemoves, surgeryRolls;
    private static readonly List<string> errors = new();
    private static bool bootstrap = true;
    private static Harmony harmony;
    private static List<Map> maps = new();
    private static Faction player;
    private static Map map;
    private static TraitDef beauty;
    private static HediffDef reaction;
    private static RitualBehaviorDef behavior;
    private static RitualOutcomeEffectDef outcome;
    private static RecipeDef surgery;
    private static string saveDirectory;
    private static readonly Dictionary<Thing, bool> reach = new();
    private static readonly HashSet<Thing> area = new(), forbidden = new(), reserved = new();
    private static ushort itemHash = 100;
    private static int thingId = 1000000;
    private static readonly List<Pawn> previewHosts = new();
    private static string nativeStartReason;
    private static readonly Dictionary<string, Def> loadedDefs = new();

    internal static int Run(string repo, string game)
    {
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(game, "Version.txt")).Trim()}, MVID: {typeof(Pawn).Module.ModuleVersionId:N}");
        harmony = new Harmony("ssc.tests.cosmetic.native");
        void Patch(MethodBase m, string name) => harmony.Patch(m, prefix: new HarmonyMethod(typeof(Program), name));
        saveDirectory = Path.Combine(repo, ".builds/beauty-native/userdata");
        Directory.CreateDirectory(Path.Combine(saveDirectory, "Config"));
        File.WriteAllText(Path.Combine(saveDirectory, "Config/ModsConfig.xml"),
            "<ModsConfigData><version>1.7.4985</version><activeMods/><knownExpansions/></ModsConfigData>");
        Patch(AccessTools.PropertyGetter(typeof(GenFilePaths), nameof(GenFilePaths.SaveDataFolderPath)), nameof(SaveDirectory));
        Patch(AccessTools.Method(typeof(ModLister), "RebuildModList"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ContentFinder<UnityEngine.Texture2D>), "Get"), nameof(NoTexture));
        Patch(AccessTools.Method(typeof(MaterialPool), nameof(MaterialPool.MatFrom), new[] { typeof(string) }), nameof(NoMaterial));
        Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps)), nameof(Maps));
        Patch(AccessTools.PropertyGetter(typeof(Faction), nameof(Faction.OfPlayer)), nameof(Player));
        // 标准 CLR 宿主没有渲染器、人物栏和整图能力初始化；这些通知不属于被测写入算法。
        Patch(AccessTools.Method(typeof(Pawn), nameof(Verse.Pawn.Notify_DisabledWorkTypesChanged)), nameof(Skip));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange)), nameof(Skip));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged)), nameof(Skip));
        Patch(AccessTools.Method(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty)), nameof(Skip));
        foreach (var type in new[] { typeof(Translator), typeof(TranslatorFormattedStringExtensions) })
            foreach (var m in AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "Translate" && m.ReturnType == typeof(TaggedString)
                && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))) Patch(m, nameof(Translate));
        foreach (var name in new[] { "Error", "Warning", "Message" })
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(m, nameof(LogBoundary));
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == "Message")) Patch(m, nameof(Skip));
        // 只观察 TraitSet 的原版增删次数，不替换返回值或列表变更。
        Patch(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.GainTrait)), nameof(CountAdd));
        Patch(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.RemoveTrait)), nameof(CountRemove));
        Patch(AccessTools.Method(typeof(SurgeryOutcomeSuccess), nameof(SurgeryOutcomeSuccess.Apply)), nameof(CountSurgeryRoll));
        try
        {
            var gameRoot = Bare<Game>(); gameRoot.tickManager = new TickManager(); gameRoot.uniqueIDsManager = new UniqueIDsManager(); gameRoot.questManager = new QuestManager();
            Current.Game = gameRoot;
            SetStatic(typeof(UnityData), "mainThreadId", Environment.CurrentManagedThreadId);
            SetStatic(typeof(Prefs), "data", new PrefsData());
            var pack = Bare<ModContentPack>(); pack.assemblies = new ModAssemblyHandler(pack);
            pack.assemblies.loadedAssemblies.Add(typeof(SSCCosmeticUtility).Assembly);
            ((List<ModContentPack>)AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null)).Add(pack);
            RuntimeHelpers.RunClassConstructor(typeof(SSCDefOf).TypeHandle);
            SetStatic(typeof(ModsConfig), "ideologyActive", true);
            SetStatic(typeof(ModsConfig), "biotechActive", true);
            bootstrap = false;
            Check("native XML inheritance and cross references", () => LoadResources(repo, game));
            if (surgery == null || reaction == null || outcome == null || beauty == null) return 1;
            TraitTests();
            ReactionTests();
            RoleAndOutcomeTests();
            StageAndMaterialTests();
            Check("four languages have matching keys, parameters and target fields", () => LanguageTests(repo));
        }
        finally { harmony.UnpatchAll(harmony.Id); Current.Game = null; }
        Console.WriteLine($"{passed}/{passed + failed} cases passed");
        return failed == 0 ? 0 : 1;
    }

    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object obj, string field, object value) => (AccessTools.Field(obj.GetType(), field) ?? throw new Exception(field)).SetValue(obj, value);
    private static void SetStatic(Type type, string field, object value) => (AccessTools.Field(type, field) ?? throw new Exception(field)).SetValue(null, value);
    private static bool Skip() => false;
    private static bool NoTexture(ref UnityEngine.Texture2D __result) { __result = null; return false; }
    private static bool NoMaterial(ref UnityEngine.Material __result) { __result = null; return false; }
    private static bool Maps(ref List<Map> __result) { __result = maps; return false; }
    private static bool SaveDirectory(ref string __result) { __result = saveDirectory; return false; }
    private static bool Player(ref Faction __result) { __result = player; return false; }
    private static bool Translate(string key, ref TaggedString __result) { __result = key; return false; }
    private static bool LogBoundary(MethodBase __originalMethod, object[] __args)
    { if (!bootstrap && __originalMethod.Name == "Error") errors.Add((string)__args[0]); return false; }
    private static void CountAdd() => traitAdds++;
    private static void CountRemove() => traitRemoves++;
    private static void CountSurgeryRoll() => surgeryRolls++;
    private static void Require(bool test, string reason = "assertion failed") { if (!test) throw new Exception(reason); }
    private static void Equal<T>(T expected, T actual) { Require(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, actual {actual}"); }
    private static void Near(float expected, float actual) => Require(Math.Abs(expected - actual) < 0.0001f, $"expected {expected}, actual {actual}");
    private static void Check(string name, Action action)
    {
        errors.Clear();
        try { action(); Require(errors.Count == 0, string.Join('\n', errors)); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex + "\n" + string.Join('\n', errors)); }
    }

    private static XmlElement Xml(XElement node) { var doc = new XmlDocument(); doc.LoadXml(node.ToString()); return doc.DocumentElement; }
    private static void Add<T>(T def) where T : Def, new() { DefDatabase<T>.Add(def); loadedDefs[def.defName] = def; }
    private static ThingDef Item(string name)
    {
        var def = Bare<ThingDef>(); def.defName = name; def.label = name; def.category = ThingCategory.Item;
        def.thingClass = typeof(Thing); def.stackLimit = 75; def.virtualDefs = new(); def.comps = new(); def.shortHash = ++itemHash; def.destroyable = true; Add(def); return def;
    }

    private static void LoadResources(string repo, string game)
    {
        // 只提供交叉引用边界的命名 Def；被测 XML、父定义和组件均从生产/本机文件读取。
        SkillDefOf.Medicine = new SkillDef { defName = "Medicine", skillLabel = "medicine" }; Add(SkillDefOf.Medicine);
        WorkTypeDefOf.Doctor = new WorkTypeDef { defName = "Doctor", relevantSkills = new() { SkillDefOf.Medicine }, workTags = WorkTags.Caring | WorkTags.Commoner | WorkTags.AllWork };
        Add(WorkTypeDefOf.Doctor);
        Add(new StatDef { defName = "MedicalOperationSpeed" }); Add(new StatDef { defName = "WorkSpeedGlobal" });
        Add(new RoomStatDef { defName = "Impressiveness" });
        Add(new EffecterDef { defName = "Surgery" }); Add(new SoundDef { defName = "Recipe_Surgery" });
        Add(new InspirationDef { defName = "Inspired_Surgery" });
        Item("Purple_Aphrodisiac_Nanofluid"); Item("MedicineIndustrial"); Item("MedicineUltratech");
        ThingDefOf.MedicineHerbal = Item("MedicineHerbal");
        var human = Bare<ThingDef>(); human.defName = "Human"; human.race = new RaceProperties { intelligence = Intelligence.Humanlike }; Add(human);
        Add(new ResearchProjectDef { defName = "SSC_RES_PNASimpleApp" }); Add(new ResearchProjectDef { defName = "SterileMaterials" });
        Add(new ResearchTabDef { defName = "SSC_ResearchTabDef" }); Add(new IssueDef { defName = "Ritual" });
        Add(new MemeDef { defName = "SSC_Meme_MasterSlaveOrder" });
        Add(new RulePackDef { defName = "NamerRitualFestival" });
        Add(new RitualObligationTargetFilterDef { defName = "RitualSpotOrAltar" });
        foreach (var name in new[] { "Spectate", "ArriveToCell", "Idle" }) Add(new DutyDef { defName = name });
        var nodes = XDocument.Load(Path.Combine(game, "Data/Core/Defs/RecipeDefs/Recipes_Surgery_Misc.xml")).Root.Elements().Where(n => (string)n.Attribute("Name") == "SurgeryFlesh")
            .Concat(XDocument.Load(Path.Combine(game, "Data/Core/Defs/RecipeDefs/SurgeryOutcomeEffectDefs.xml")).Root.Elements().Where(n => (string)n.Attribute("Name") == "SurgeryOutcomeBase"))
            .Concat(Directory.GetFiles(Path.Combine(repo, "1.7/Defs"), "SSC_Cosmetic*.xml", SearchOption.AllDirectories).SelectMany(p => XDocument.Load(p).Root.Elements())).Select(Xml).ToList();
        XmlInheritance.Clear();
        foreach (var n in nodes) XmlInheritance.TryRegister(n, null);
        XmlInheritance.Resolve();
        foreach (var original in nodes.Skip(2))
        {
            var node = (XmlElement)XmlInheritance.GetResolvedNodeFor(original).CloneNode(true);
            node.RemoveAttribute("ParentName"); node.RemoveAttribute("Abstract");
            switch (node.Name)
            {
                case "RecipeDef": surgery = DirectXmlToObject.ObjectFromXml<RecipeDef>(node, false); Add(surgery); break;
                case "ResearchProjectDef": Add(DirectXmlToObject.ObjectFromXml<ResearchProjectDef>(node, false)); break;
                case "SurgeryOutcomeEffectDef": Add(DirectXmlToObject.ObjectFromXml<SurgeryOutcomeEffectDef>(node, false)); break;
                case "HediffDef": reaction = DirectXmlToObject.ObjectFromXml<HediffDef>(node, false); Add(reaction); break;
                case "PreceptDef": Add(DirectXmlToObject.ObjectFromXml<PreceptDef>(node, false)); break;
                case "DutyDef": Add(DirectXmlToObject.ObjectFromXml<DutyDef>(node, false)); break;
                case "RitualPatternDef": Add(DirectXmlToObject.ObjectFromXml<RitualPatternDef>(node, false)); break;
                case "RitualBehaviorDef": behavior = DirectXmlToObject.ObjectFromXml<RitualBehaviorDef>(node, false); Add(behavior); break;
                case "RitualOutcomeEffectDef": outcome = DirectXmlToObject.ObjectFromXml<RitualOutcomeEffectDef>(node, false); Add(outcome); break;
                default: throw new Exception(node.Name);
            }
        }
        XmlInheritance.Clear(); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        SSCDefOf.SSC_Hediff_NanoCosmeticReaction = reaction; SSCDefOf.SSC_Surgery_BeautyEnhancement = surgery;
        behavior.ResolveReferences(); reaction.ResolveReferences(); surgery.ResolveReferences();
        var research = DefDatabase<ResearchProjectDef>.GetNamed("SSC_RES_PNACosmeticApplication");
        Near(1500, research.baseCost); Equal(TechLevel.Industrial, research.techLevel); Equal(2, research.prerequisites.Count);
        Near(4000, surgery.workAmount); Near(1, surgery.surgerySuccessChanceFactor); Equal("MedicalOperationSpeed", surgery.workSpeedStat.defName);
        Near(10, surgery.ingredients[0].GetBaseCount()); Near(3, surgery.ingredients[1].GetBaseCount());
        Require(!surgery.fixedIngredientFilter.Allows(ThingDefOf.MedicineHerbal));
        Equal(2, surgery.surgeryOutcomeEffect.outcomes.Count); Equal(6, surgery.surgeryOutcomeEffect.comps.Count);
        Require(surgery.surgeryOutcomeEffect.outcomes[0] is SurgeryOutcomeSuccess);
        Require(surgery.surgeryOutcomeEffect.outcomes[1] is SurgeryOutcome_CosmeticFailure && surgery.surgeryOutcomeEffect.outcomes[1].failure);
        Require(surgery.surgeryOutcomeEffect.outcomes.All(o => o.totalDamage == 0));
        Require(!outcome.givesDevelopmentPoints && !outcome.allowAttachableOutcome);
        Near(.4f, outcome.startingQuality); Equal("Medicine", ((RitualOutcomeComp_PawnSkill)outcome.comps[0]).skillDef.defName);
        Require(behavior.roles.All(r => r.required && !r.countsAsParticipant && !r.substitutable && r.addToLord));
        beauty = new TraitDef { defName = "Beauty", degreeDatas = new List<TraitDegreeData>() };
        foreach (int degree in new[] { -2, -1, 1, 2, 3 }) beauty.degreeDatas.Add(new TraitDegreeData { degree = degree, label = "beauty" + degree });
        Add(beauty);
    }

    private static Pawn Pawn(int? degree = null)
    {
        player ??= new Faction { def = new FactionDef { defName = "Player", isPlayer = true } };
        if (map == null) { map = new Map(); map.listerThings = new ListerThings(ListerThingsUse.Global); maps.Add(map); }
        var def = Bare<ThingDef>(); def.defName = "TestHuman"; def.category = ThingCategory.Pawn; def.stackLimit = 1;
        def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
        def.race.body = new BodyDef { defName = "TestBody", corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Torso" } } };
        def.race.body.ResolveReferences();
        Set(def.race, "fleshType", new FleshTypeDef { defName = "Normal", isOrganic = true });
        def.race.lifeStageAges.Add(new LifeStageAge { def = new LifeStageDef { defName = "Adult", developmentalStage = DevelopmentalStage.Adult } });
        var p = new Pawn { def = def, Name = new NameSingle("Fixture") };
        p.kindDef = new PawnKindDef { defName = "TestColonist", race = def };
        Set(p, "factionInt", player); Set(p, "mapIndexOrState", (sbyte)0);
        p.health = new Pawn_HealthTracker(p); p.story = new Pawn_StoryTracker(p); p.needs = new Pawn_NeedsTracker(p);
        p.guest = new Pawn_GuestTracker(p); p.mindState = new Pawn_MindState(p); p.ageTracker = new Pawn_AgeTracker(p);
        p.inventory = new Pawn_InventoryTracker(p); p.carryTracker = new Pawn_CarryTracker(p);
        Set(p.ageTracker, "cachedLifeStageIndex", 0); p.ageTracker.AgeBiologicalTicks = 30L * GenDate.TicksPerYear;
        p.ideo = new Pawn_IdeoTracker(p);
        // 资格和质量使用真实技能；不依赖完整工作/背景 Def 的禁用缓存计算。
        p.skills = new Pawn_SkillTracker(p); p.skills.GetSkill(SkillDefOf.Medicine).Level = 10;
        if (degree.HasValue) p.story.traits.allTraits.Add(new Trait(beauty, degree.Value) { pawn = p });
        return p;
    }

    private static void TraitTests()
    {
        foreach (var pair in new[] { (-2, -1), (-1, 0), (0, 1), (1, 2) })
            Check($"real TraitSet one-step {pair.Item1} -> {pair.Item2}", () =>
            {
                var p = Pawn(pair.Item1 == 0 ? null : pair.Item1); traitAdds = traitRemoves = 0;
                Require(SSCCosmeticUtility.ApplyResult(p, true, .05f));
                var result = p.story.traits.allTraits.Where(t => t.def == beauty).ToArray();
                if (pair.Item2 == 0) Equal(0, result.Length); else { Equal(1, result.Length); Equal(pair.Item2, result[0].Degree); }
                Equal(pair.Item1 == 0 ? 0 : 1, traitRemoves); Equal(pair.Item2 == 0 ? 0 : 1, traitAdds);
                Near(.05f, p.health.hediffSet.GetFirstHediffOfDef(reaction).Severity);
            });
        foreach (int degree in new[] { 2, 3 }) Check("vanilla/modded cap preserved " + degree, () =>
        {
            var p = Pawn(degree); var before = p.story.traits.allTraits[0];
            Require(!SSCCosmeticUtility.NextBeauty(p, out _, out _).Accepted); Equal(before, p.story.traits.allTraits[0]);
        });
        Check("reverse native conflict and suppressed conflicting trait", () =>
        {
            var p = Pawn(); var other = new TraitDef { defName = "Other", conflictingTraits = new List<TraitDef> { beauty }, degreeDatas = new() { new TraitDegreeData { label = "other" } } };
            p.story.traits.allTraits.Add(new Trait(other) { suppressedByTrait = true });
            Require(!SSCCosmeticUtility.NextBeauty(p, out _, out _).Accepted);
        });
        foreach (string state in new[] { "gene", "suppressed", "duplicate", "unknown", "missingDegree" }) Check("unsupported Beauty refused: " + state, () =>
        {
            var p = Pawn(1); var trait = p.story.traits.allTraits[0];
            if (state == "gene") trait.sourceGene = new Gene();
            if (state == "suppressed") trait.suppressedByTrait = true;
            if (state == "duplicate") p.story.traits.allTraits.Add(new Trait(beauty, -1));
            if (state == "unknown") p.story.traits.allTraits[0] = new Trait(beauty, 0);
            var degree = beauty.degreeDatas.Single(d => d.degree == 2);
            if (state == "missingDegree") beauty.degreeDatas.Remove(degree);
            try { Require(!SSCCosmeticUtility.NextBeauty(p, out _, out _).Accepted); }
            finally { if (!beauty.degreeDatas.Contains(degree)) beauty.degreeDatas.Add(degree); }
        });
        Check("surgery allows prisoner, ritual rejects prisoner", () =>
        {
            var p = Pawn(); Set(p, "factionInt", new Faction { def = new FactionDef { defName = "Other" } }); Set(p.guest, "hostFactionInt", player); Set(p.guest, "guestStatusInt", GuestStatus.Prisoner);
            Require(SSCCosmeticUtility.RecipientReport(p, true).Accepted); Require(!SSCCosmeticUtility.RecipientReport(p, false).Accepted);
        });
        Check("surgery allows downed recipient and refuses downed doctor", () =>
        {
            var p = Pawn(); Set(p.health, "healthState", PawnHealthState.Down);
            Require(SSCCosmeticUtility.RecipientReport(p, true).Accepted);
            Require(!SSCCosmeticUtility.DoctorReport(p, Pawn()).Accepted);
        });
        Check("doctor Medical 7/8 and self-surgery admission", () =>
        {
            var doctor = Pawn(); doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 7;
            Require(!SSCCosmeticUtility.DoctorReport(doctor, null).Accepted);
            doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 8;
            // 不提供患者目标时只验证医生个人资格；真实地图可达性留给实机验收。
            Require(SSCCosmeticUtility.DoctorReport(doctor, null).Accepted);
            Require(!SSCCosmeticUtility.DoctorReport(doctor, doctor).Accepted);
        });
    }

    private static void ReactionTests()
    {
        foreach (float severity in new[] { .05f, .25f, .15f, .60f }) Check("real reaction severity/decay " + severity, () =>
        {
            var p = Pawn(); SSCCosmeticUtility.ApplyResult(p, false, severity);
            var h = (HediffWithComps)p.health.hediffSet.GetFirstHediffOfDef(reaction);
            Near(severity, h.Severity); Require(!SSCCosmeticUtility.RecipientReport(p, true).Accepted);
            var comp = h.TryGetComp<HediffComp_SeverityPerDay>(); Require(comp != null);
            Require(((HediffCompProperties_SeverityPerDay)comp.props).showHoursToRecover);
            Equal(((int)Math.Round(severity / .1f * 24)).ToString() + "LetterHour", comp.CompLabelInBracketsExtra);
            Require(((HediffCompProperties_SeverityPerDay)comp.props).showDaysToRecover);
            // 实际组件每次 TicksPerDay/粒度的 TickInterval；不复制其恢复公式。
            for (int i = 0; i < (int)Math.Ceiling(severity / .1f * 60000 / 200f) + 2; i++)
            { float delta = 0; comp.CompPostTickInterval(ref delta, 200); h.Severity += delta; }
            Require(h.ShouldRemove);
        });
        Check("reaction thresholds and work penalties", () =>
        {
            var p = Pawn(); var h = HediffMaker.MakeHediff(reaction, p);
            foreach (var entry in new[] { (.199f, .05f, .95f), (.20f, .10f, .85f), (.50f, .20f, .70f) })
            { h.Severity = entry.Item1; Near(entry.Item2, h.CurStage.painOffset); Near(entry.Item3, h.CurStage.statFactors.Single().value); }
        });
        Check("reaction merge takes maximum and keeps a single instance", () =>
        {
            var p = Pawn(); SSCCosmeticUtility.ApplyResult(p, false, .60f); SSCCosmeticUtility.ApplyResult(p, false, .25f);
            Near(.60f, p.health.hediffSet.GetFirstHediffOfDef(reaction).Severity); Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == reaction));
        });
        Check("dead/null target has no cosmetic effect", () =>
        {
            Require(!SSCCosmeticUtility.ApplyResult(null, true, .05f)); var p = Pawn(1); Set(p.health, "healthState", PawnHealthState.Dead);
            Require(!SSCCosmeticUtility.ApplyResult(p, true, .05f)); Equal(1, p.story.traits.allTraits[0].Degree); Equal(0, p.health.hediffSet.hediffs.Count);
        });
    }

    private static void RoleAndOutcomeTests()
    {
        Check("Medical 5/6 host threshold and mature spectator without Beauty gate", () =>
        {
            var p = Pawn(3); p.skills.GetSkill(SkillDefOf.Medicine).Level = 5;
            Require(!RitualRole_CosmeticHost.Report(p, TargetInfo.Invalid, null).Accepted);
            p.skills.GetSkill(SkillDefOf.Medicine).Level = 6;
            Require(RitualRole_CosmeticHost.Report(p, TargetInfo.Invalid, null).Accepted);
            Require(new RitualBehaviorWorker_Cosmetic(behavior).PawnCanFillRole(p, null, out _, TargetInfo.Invalid));
        });
        Check("native disabled Medical skill rejects host", () =>
        {
            var p = Pawn(); p.kindDef.disabledWorkTags = WorkTags.Caring;
            var previous = SkillDefOf.Medicine.disablingWorkTags;
            SkillDefOf.Medicine.disablingWorkTags = WorkTags.Caring;
            try
            {
                p.skills.Notify_SkillDisablesChanged();
                Require(p.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled);
                Require(!RitualRole_CosmeticHost.Report(p, TargetInfo.Invalid, null).Accepted);
            }
            finally { SkillDefOf.Medicine.disablingWorkTags = previous; }
        });
        Check("same-pawn roles rejected regardless of selected slot", () =>
        {
            var p = Pawn(); var job = Job(p, p);
            Require(!behavior.roles[0].AppliesToPawn(p, out _, TargetInfo.Invalid, job, job.assignments));
            Require(!behavior.roles[1].AppliesToPawn(p, out _, TargetInfo.Invalid, job, job.assignments));
        });
        Check("cap reached after scheduling produces fixed failure without random draw", () =>
        {
            var p = Pawn(2); var job = Job(Pawn(), p); var worker = (RitualOutcomeEffectWorker_Cosmetic)job.Ritual.outcomeEffect;
            Rand.PushState(991);
            float expected = Rand.Value; Rand.PopState();
            Rand.PushState(991);
            try { Require(!worker.GetOutcome(1, job).Positive); Near(expected, Rand.Value); }
            finally { Rand.PopState(); }
            LookTargets look = p;
            object[] args = { new Dictionary<Pawn, int>(), job, outcome.outcomeChances[1], null, look };
            AccessTools.Method(typeof(RitualOutcomeEffectWorker_Cosmetic), "ApplyExtraOutcome").Invoke(worker, args);
            Equal(2, p.story.traits.allTraits[0].Degree); Near(.60f, p.health.hediffSet.GetFirstHediffOfDef(reaction).Severity);
        });
        Check("special surgery failure and final ineligibility bypass success roll", () =>
        {
            var p = Pawn(2); surgeryRolls = 0;
            surgery.Worker.ApplyOnPawn(p, null, null, new(), null); Equal(0, surgeryRolls);
            Equal(2, p.story.traits.allTraits[0].Degree); Near(.25f, p.health.hediffSet.GetFirstHediffOfDef(reaction).Severity);
            var other = Pawn(); Require(surgery.surgeryOutcomeEffect.outcomes[1].Apply(0, surgery, null, other, null));
            Near(.25f, other.health.hediffSet.GetFirstHediffOfDef(reaction).Severity); Equal(1, other.health.hediffSet.hediffs.Count);
        });
        Check("native success weighting baseline and maximum", () =>
        {
            var worker = new RitualOutcomeEffectWorker_Cosmetic(outcome);
            foreach (var item in new[] { (.4f, .6153846f), (.5f, .6666667f), (.6f, .7058824f), (.7f, .7368421f), (1f, .8f) })
                Near(item.Item2, worker.GetOutcomeChanceAtQuality(null, outcome.outcomeChances[0], item.Item1));
        });
        Check("native quality components Medical curve, no custom probability or Apply", () =>
        {
            var skill = (RitualOutcomeComp_PawnSkill)outcome.comps[0];
            Near(0, skill.curve.Evaluate(6)); Near(.1f, skill.curve.Evaluate(10)); Near(.2f, skill.curve.Evaluate(15)); Near(.3f, skill.curve.Evaluate(20));
            Equal(typeof(RitualOutcomeEffectWorker_FromQuality), typeof(RitualOutcomeEffectWorker_Cosmetic).GetMethod("Apply").DeclaringType);
            Equal(typeof(Recipe_Surgery), typeof(Recipe_BeautyEnhancement).GetMethod("CompletableEver").DeclaringType);
            Equal(typeof(Recipe_Surgery), typeof(Recipe_BeautyEnhancement).GetMethod("AvailableOnNow").DeclaringType);
            var job = Job(Pawn(), Pawn());
            Require(IdeoDevelopmentUtility.GetDevelopmentPointsOverOutcomeIndexCurveForRitual(job.Ritual.ideo, job.Ritual) == null);
            Require(!job.Ritual.outcomeEffect.SupportsAttachableOutcomeEffect);
        });
        Check("meme required, same ideology, null context preview", () =>
        {
            var p = Pawn(); var ideo = new Ideo { memes = new() { DefDatabase<MemeDef>.GetNamed("SSC_Meme_MasterSlaveOrder") } };
            var ritual = new Precept_Ritual { ideo = ideo }; Set(p.ideo, "ideo", ideo);
            Require(SSCCosmeticUtility.CultureReport(p, ritual).Accepted); Require(SSCCosmeticUtility.CultureReport(p, null).Accepted);
            Set(p.ideo, "ideo", new Ideo()); Require(!SSCCosmeticUtility.CultureReport(p, ritual).Accepted);
            Set(p.ideo, "ideo", ideo); ideo.memes.Clear(); Require(!SSCCosmeticUtility.CultureReport(p, ritual).Accepted);
        });
        Check("no Ideology leaves surgery available and ritual gated", () =>
        {
            SetStatic(typeof(ModsConfig), "ideologyActive", false);
            try { Require(SSCCosmeticUtility.RecipientReport(Pawn(), true).Accepted); Require(!SSCCosmeticUtility.HasRequiredMeme(new Precept_Ritual())); }
            finally { SetStatic(typeof(ModsConfig), "ideologyActive", true); }
        });
    }

    private static void StageAndMaterialTests()
    {
        Check("native stage structure: zero preparation, essential consumption before 5000 ticks", () =>
        {
            Equal(3, behavior.stages.Count); Near(0, behavior.stages[0].ProgressPerTick(null)); Near(0, behavior.stages[1].ProgressPerTick(null)); Near(1, behavior.stages[2].ProgressPerTick(null));
            Require(behavior.stages[1].essential && behavior.stages[1].postAction is RitualStageAction_ConsumeCosmeticMaterials && behavior.stages[1].preAction == null);
            Require(behavior.stages[0].endTriggers.Single() is StageEndTrigger_CosmeticMaterialsReady);
            Require(behavior.stages[1].endTriggers.Single() is StageEndTrigger_Instant);
            Near(1, ((StageEndTrigger_DurationPercentage)behavior.stages[2].endTriggers.Single()).percentage); Equal(5000, behavior.durationTicks.min);
        });
        // 地图路径、禁用与活动范围作为边界注入；真实地面列表、数量检查、SplitOff/Destroy 和 native ended 仍执行。
        harmony.Patch(AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable)), prefix: new HarmonyMethod(typeof(Program), nameof(Closest)));
        harmony.Patch(AccessTools.Method(typeof(ReservationUtility), nameof(ReservationUtility.CanReserve)), prefix: new HarmonyMethod(typeof(Program), nameof(Reach)));
        harmony.Patch(AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new[] { typeof(Thing), typeof(Pawn) }), prefix: new HarmonyMethod(typeof(Program), nameof(Forbidden)));
        // 仅隔离地图出生/销毁通知，不替换 SplitOff 数量、Destroy 状态或原版 ApplyOutcome 的 ended/cancelled。
        harmony.Patch(AccessTools.Method(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.DropCount)), prefix: new HarmonyMethod(typeof(Program), nameof(Drop)));
        harmony.Patch(AccessTools.Method(typeof(ThingOwner), "NotifyAdded"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(ThingOwner), "NotifyRemoved"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn)), prefix: new HarmonyMethod(typeof(Program), nameof(Despawn)));
        harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DirtyMapMesh)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(LordManager), nameof(LordManager.RemoveLord)), prefix: new HarmonyMethod(typeof(Program), nameof(RemoveLord)));
        harmony.Patch(AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.UpdateTarget)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        map.attackTargetsCache ??= Bare<AttackTargetsCache>();
        Check("supply precheck accepts distant ground stacks without consumption", () =>
        {
            ResetStacks(); var host = Pawn(); var fluid = Stack("Purple_Aphrodisiac_Nanofluid", 20); var herb = Stack("MedicineHerbal", 2);
            area.Clear(); Set(fluid, "positionInt", new IntVec3(70, 0, 70));
            Require(RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(new TargetInfo(new IntVec3(10, 0, 10), map), host).Accepted);
            Equal(20, fluid.stackCount); Equal(2, herb.stackCount); Equal(0, host.inventory.innerContainer.Count);
        });
        Check("native fetch and precheck agree on split stacks and inventory shortfall", () =>
        {
            ResetStacks(); var host = Pawn(); Stack("Purple_Aphrodisiac_Nanofluid", 12); Stack("Purple_Aphrodisiac_Nanofluid", 8); Stack("MedicineHerbal", 2);
            JobDefOf.TakeCountToInventory = new JobDef { defName = "TakeCountToInventory" };
            var giver = new JobGiver_TakeCountToInventory { def = DefDatabase<ThingDef>.GetNamed("Purple_Aphrodisiac_Nanofluid"), count = 20 };
            var target = new TargetInfo(new IntVec3(10, 0, 10), map);
            Require(!RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host).Accepted);
            Require(giver.TryIssueJobPackage(host, default).Job == null);
            Inventory(host, "Purple_Aphrodisiac_Nanofluid", 8);
            Require(RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host).Accepted);
            var fetch = giver.TryIssueJobPackage(host, default).Job;
            Equal(12, fetch.count); Equal(JobDefOf.TakeCountToInventory, fetch.def); Equal(12, fetch.targetA.Thing.stackCount);
        });
        Check("one missing, forbidden, unreachable or reserved supply rejected", () =>
        {
            ResetStacks(); var host = Pawn(); var fluid = Stack("Purple_Aphrodisiac_Nanofluid", 20); var herb = Stack("MedicineHerbal", 1);
            var target = new TargetInfo(new IntVec3(10, 0, 10), map);
            Require(!RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host).Accepted); herb.stackCount = 2;
            forbidden.Add(fluid); Require(!RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host).Accepted); forbidden.Clear();
            reach[fluid] = false; Require(!RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host).Accepted); reach.Clear();
            reserved.Add(fluid); Require(!RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host).Accepted);
        });
        Check("host duty reuses vanilla fetch jobs then vanilla arrival", () =>
        {
            var duty = DefDatabase<DutyDef>.GetNamed("SSC_PrepareCosmeticMaterials");
            Equal(ThinkTreeDutyHook.HighPriority, duty.hook);
            var nodes = duty.thinkNode.subNodes;
            Equal(4, nodes.Count);
            Equal(20, ((JobGiver_TakeCountToInventory)nodes[0]).count);
            Equal("Purple_Aphrodisiac_Nanofluid", ((JobGiver_TakeCountToInventory)nodes[0]).def.defName);
            Equal(2, ((JobGiver_TakeCountToInventory)nodes[1]).count);
            Require(nodes[2] is JobGiver_GotoTravelDestination && nodes[3] is JobGiver_Idle);
            Equal(duty, behavior.stages[0].BehaviorForRole("host").dutyDef);
        });
        Check("arrival requires host inventory and actual return to assigned position", () =>
        {
            var host = Pawn(); var recipient = Pawn(); var job = Job(host, recipient);
            var trigger = behavior.stages[0].endTriggers.Single();
            var arrived = AccessTools.Method(trigger.GetType(), "ArrivedCheck");
            job.AddTagForPawn(host, "Arrived"); job.AddTagForPawn(recipient, "Arrived");
            Set(host, "positionInt", new IntVec3(10, 0, 10)); host.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("SSC_PrepareCosmeticMaterials"), host.Position);
            Require(!(bool)arrived.Invoke(trigger, new object[] { "host", job }));
            Inventory(host, "Purple_Aphrodisiac_Nanofluid", 20); Inventory(host, "MedicineHerbal", 2);
            Require((bool)arrived.Invoke(trigger, new object[] { "host", job }));
            Set(host, "positionInt", new IntVec3(70, 0, 70));
            Require(!(bool)arrived.Invoke(trigger, new object[] { "host", job }));
        });
        Check("mandatory action consumes host inventory only and preserves surplus", () =>
        {
            ResetStacks(); var ground = Stack("Purple_Aphrodisiac_Nanofluid", 50);
            var host = Pawn(); var recipient = Pawn(); Inventory(recipient, "MedicineHerbal", 4);
            Inventory(host, "Purple_Aphrodisiac_Nanofluid", 12); Inventory(host, "Purple_Aphrodisiac_Nanofluid", 13); Inventory(host, "MedicineHerbal", 3);
            var job = Job(host, recipient); behavior.stages[1].postAction.Apply(job);
            Equal(5, host.inventory.Count(ground.def)); Equal(1, host.inventory.Count(ThingDefOf.MedicineHerbal));
            Equal(50, ground.stackCount); Equal(4, recipient.inventory.Count(ThingDefOf.MedicineHerbal));
            Require(!(bool)AccessTools.Field(typeof(LordJob_Ritual), "ended").GetValue(job));
        });
        Check("pre-consumption interruption drops host materials and leaves audience inventory", () =>
        {
            ResetStacks(); var host = Pawn(); var recipient = Pawn();
            Inventory(host, "Purple_Aphrodisiac_Nanofluid", 20); Inventory(host, "MedicineHerbal", 2); Inventory(recipient, "MedicineHerbal", 4);
            var job = Job(host, recipient); behavior.stages[0].pawnLeaveAction.ApplyToPawn(job, recipient);
            Equal(4, recipient.inventory.Count(ThingDefOf.MedicineHerbal));
            behavior.stages[0].interruptedAction.Apply(job);
            Equal(0, host.inventory.innerContainer.Count); Equal(4, recipient.inventory.Count(ThingDefOf.MedicineHerbal));
            Equal(20, map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Purple_Aphrodisiac_Nanofluid")).Sum(t => t.stackCount));
            Require(behavior.stages[2].interruptedAction == null && behavior.stages[2].pawnLeaveAction == null);
        });
        Check("missing second material consumes nothing, native cancellation cannot produce result later", () =>
        {
            ResetStacks(); var host = Pawn(); Inventory(host, "Purple_Aphrodisiac_Nanofluid", 20); var fluid = host.inventory.innerContainer[0]; var p = Pawn(); var job = Job(host, p);
            job.lord.ownedPawns.Add(p); p.lord = job.lord;
            // 实际 Transition/GotoToil 先分配下一阶段职责，再由生产 postAction 取消并执行原版 Lord.Cleanup。
            // 若回归为 preAction，GotoToil 会在清理后重设职责，本例必须失败。
            var source = new ProbeToil { lord = job.lord }; var target = new ProbeToil { lord = job.lord };
            var graph = new StateGraph(); graph.AddToil(source); graph.AddToil(target);
            Set(job.lord, "graph", graph); Set(job.lord, "curLordToil", source);
            var transition = new Transition(source, target);
            var apply = new TransitionAction_Custom((Action)(() => behavior.stages[1].postAction.Apply(job)));
            transition.AddPostAction(apply);
            transition.Execute(job.lord);
            Require(p.mindState.duty == null && p.lord == null, "cancelled transition left ritual duties or ownership");
            Equal(20, fluid.stackCount); Require(!fluid.Destroyed);
            Require((bool)AccessTools.Field(typeof(LordJob_Ritual), "ended").GetValue(job));
            Require((bool)AccessTools.Field(typeof(LordJob_Ritual), "cancelled").GetValue(job));
            job.ApplyOutcome(1f);
            Equal(0, p.story.traits.allTraits.Count); Equal(0, p.health.hediffSet.hediffs.Count);
        });
        StartPreviewTests();
    }

    private static void StartPreviewTests()
    {
        // 隔离原版全局活动/行星/寻路环境；执行生产开场判断、角色资格、取料报告及真实背包数量。
        var nativeStart = AccessTools.Method(typeof(RitualBehaviorWorker), nameof(RitualBehaviorWorker.CanStartRitualNow));
        var hostList = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.FreeColonistsSpawned));
        var canReach = AccessTools.Method(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach),
            new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) });
        harmony.Patch(nativeStart, prefix: new HarmonyMethod(typeof(Program), nameof(NativeStart)));
        harmony.Patch(hostList, prefix: new HarmonyMethod(typeof(Program), nameof(PreviewHosts)));
        harmony.Patch(canReach, prefix: new HarmonyMethod(typeof(Program), nameof(ReachTarget)));
        map.mapPawns = Bare<MapPawns>();
        try
        {
            Check("start preview preserves native rejection before cosmetic checks", () =>
            {
                var job = Job(Pawn(), Pawn()); nativeStartReason = "NativeDenied";
                try { Equal("NativeDenied", job.Ritual.behavior.CanStartRitualNow(job.selectedTarget, job.Ritual)); }
                finally { nativeStartReason = null; }
            });
            Check("unselected host keeps no-host and no-material reasons and finds a supplied candidate", () =>
            {
                ResetStacks(); previewHosts.Clear(); var host = Pawn(); var job = Job(host, Pawn());
                var worker = job.Ritual.behavior;
                Equal("SSC_Cosmetic_RolesRequired", worker.CanStartRitualNow(job.selectedTarget, job.Ritual));
                host.skills.GetSkill(SkillDefOf.Medicine).Level = 5; previewHosts.Add(host);
                Equal("SSC_Cosmetic_RolesRequired", worker.CanStartRitualNow(job.selectedTarget, job.Ritual));
                host.skills.GetSkill(SkillDefOf.Medicine).Level = 6;
                Equal("SSC_Cosmetic_MaterialsRequired", worker.CanStartRitualNow(job.selectedTarget, job.Ritual));
                var supplied = Pawn(); Set(supplied.ideo, "ideo", job.Ritual.ideo); previewHosts.Add(supplied);
                Inventory(supplied, "Purple_Aphrodisiac_Nanofluid", 20); Inventory(supplied, "MedicineHerbal", 2);
                Equal<string>(null, worker.CanStartRitualNow(job.selectedTarget, job.Ritual));
                Equal(20, supplied.inventory.Count(DefDatabase<ThingDef>.GetNamed("Purple_Aphrodisiac_Nanofluid")));
                Equal(2, supplied.inventory.Count(ThingDefOf.MedicineHerbal));
            });
            Check("selected host must meet skill and supply conditions without falling back to another pawn", () =>
            {
                ResetStacks(); previewHosts.Clear(); var host = Pawn(); var job = Job(host, Pawn());
                var supplied = Pawn(); Set(supplied.ideo, "ideo", job.Ritual.ideo); previewHosts.Add(supplied);
                Inventory(supplied, "Purple_Aphrodisiac_Nanofluid", 20); Inventory(supplied, "MedicineHerbal", 2);
                var forced = new Dictionary<string, Pawn> { ["host"] = host }; var worker = job.Ritual.behavior;
                Equal("SSC_Cosmetic_MaterialsRequired", worker.CanStartRitualNow(job.selectedTarget, job.Ritual, forcedForRole: forced));
                Inventory(host, "Purple_Aphrodisiac_Nanofluid", 20); Inventory(host, "MedicineHerbal", 2);
                Equal<string>(null, worker.CanStartRitualNow(job.selectedTarget, job.Ritual, forcedForRole: forced));
                host.skills.GetSkill(SkillDefOf.Medicine).Level = 5;
                Equal("SSC_Cosmetic_MedicineRequired", worker.CanStartRitualNow(job.selectedTarget, job.Ritual, forcedForRole: forced));
                Equal(20, host.inventory.Count(DefDatabase<ThingDef>.GetNamed("Purple_Aphrodisiac_Nanofluid")));
                Equal(2, host.inventory.Count(ThingDefOf.MedicineHerbal));
            });
        }
        finally
        {
            foreach (var method in new[] { nativeStart, hostList, canReach }) harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
            previewHosts.Clear(); nativeStartReason = null;
        }
    }

    private static bool NativeStart(ref string __result) { __result = nativeStartReason; return false; }
    private static bool PreviewHosts(ref List<Pawn> __result) { __result = previewHosts; return false; }
    private static bool ReachTarget(ref bool __result) { __result = true; return false; }
    private static LordJob_Ritual Job(Pawn host, Pawn recipient)
    {
        var ideo = new Ideo { memes = new() { DefDatabase<MemeDef>.GetNamed("SSC_Meme_MasterSlaveOrder") } };
        Set(host.ideo, "ideo", ideo); Set(recipient.ideo, "ideo", ideo);
        var ritual = new Precept_Ritual { def = DefDatabase<PreceptDef>.GetNamed("SSC_CosmeticRitualPrecept"), ideo = ideo,
            behavior = new RitualBehaviorWorker_Cosmetic(behavior), outcomeEffect = new RitualOutcomeEffectWorker_Cosmetic(outcome) };
        var assignments = Bare<RitualRoleAssignments>();
        Set(assignments, "ritual", ritual); Set(assignments, "allRoles", behavior.roles);
        Set(assignments, "assignedRoles", new Dictionary<string, List<Pawn>> { ["host"] = new() { host }, ["recipient"] = new() { recipient } });
        Set(assignments, "spectators", new List<Pawn>());
        var target = new TargetInfo(new IntVec3(10, 0, 10), map);
        var job = new LordJob_Ritual(target, ritual, null, behavior.stages, assignments);
        map.lordManager ??= Bare<LordManager>(); map.lordManager.map = map;
        job.lord = new Lord { lordManager = map.lordManager }; Set(job.lord, "curJob", job);
        return job;
    }
    private static bool Despawn(Thing __instance)
    {
        __instance.Map.listerThings.Remove(__instance); Set(__instance, "mapIndexOrState", (sbyte)-1); return false;
    }
    private static bool RemoveLord(Lord oldLord) { oldLord.Cleanup(); return false; }
    private sealed class ProbeToil : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns) pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("Idle"));
        }
    }
    private static bool Closest(Map map, ThingRequest thingReq, Predicate<Thing> validator, ref Thing __result)
    {
        __result = map.listerThings.ThingsMatching(thingReq).FirstOrDefault(t => t.Spawned &&
            (!reach.TryGetValue(t, out bool value) || value) && (validator == null || validator(t)));
        return false;
    }
    private static bool Reach(LocalTargetInfo target, ref bool __result) { __result = !reserved.Contains(target.Thing); return false; }
    // 放置是地图边界；原版 Multi/DropThing 选择角色与数量，真实 ThingOwner.Take 移出背包。
    private static bool Drop(Pawn_InventoryTracker __instance, ThingDef def, int count)
    {
        foreach (var stack in __instance.innerContainer.Where(t => t.def == def).ToList())
        {
            int take = Math.Min(count, stack.stackCount); var ground = __instance.innerContainer.Take(stack, take);
            Set(ground, "mapIndexOrState", (sbyte)0); map.listerThings.Add(ground); count -= take; if (count == 0) break;
        }
        return false;
    }
    private static void Inventory(Pawn pawn, string defName, int count)
    {
        var thing = new Thing { def = DefDatabase<ThingDef>.GetNamed(defName), stackCount = count, thingIDNumber = ++thingId };
        Require(pawn.inventory.innerContainer.TryAdd(thing, canMergeWithExistingStacks: false));
    }
    private static bool Forbidden(Thing t, ref bool __result) { __result = forbidden.Contains(t); return false; }
    private static void ResetStacks() { map.listerThings = new ListerThings(ListerThingsUse.Global); area.Clear(); reach.Clear(); forbidden.Clear(); reserved.Clear(); }
    private static Thing Stack(string name, int count)
    {
        var t = new Thing { def = DefDatabase<ThingDef>.GetNamed(name), stackCount = count, thingIDNumber = ++thingId };
        t.Position = new IntVec3(10 + area.Count, 0, 10); Set(t, "mapIndexOrState", (sbyte)0); map.listerThings.Add(t); area.Add(t); return t;
    }

    private static void LanguageTests(string repo)
    {
        var baseline = XDocument.Load(Path.Combine(repo, "1.7/Languages/English/Keyed/SSC_Cosmetic.xml")).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (string lang in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            var data = XDocument.Load(Path.Combine(repo, $"1.7/Languages/{lang}/Keyed/SSC_Cosmetic.xml")).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
            Equal(string.Join(',', baseline.Keys.Order()), string.Join(',', data.Keys.Order()));
            foreach (string key in baseline.Keys)
                Equal(string.Join(',', System.Text.RegularExpressions.Regex.Matches(baseline[key], @"\{\d+\}").Select(m => m.Value).Order()),
                    string.Join(',', System.Text.RegularExpressions.Regex.Matches(data[key], @"\{\d+\}").Select(m => m.Value).Order()));
            foreach (string path in Directory.GetFiles(Path.Combine(repo, $"1.7/Languages/{lang}/DefInjected"), "SSC_Cosmetic.xml", SearchOption.AllDirectories))
            {
                string type = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
                var expected = XDocument.Load(Path.Combine(repo, $"1.7/Languages/English/DefInjected/{type}/SSC_Cosmetic.xml"))
                    .Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                var actual = XDocument.Load(path).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                Equal(string.Join(',', expected.Keys.Order()), string.Join(',', actual.Keys.Order()));
                foreach (string key in actual.Keys)
                {
                    Equal(string.Join(',', System.Text.RegularExpressions.Regex.Matches(expected[key], @"\{[^{}]+\}").Select(m => m.Value).Order()),
                        string.Join(',', System.Text.RegularExpressions.Regex.Matches(actual[key], @"\{[^{}]+\}").Select(m => m.Value).Order()));
                    var parts = key.Split('.'); object value = loadedDefs[parts[0]];
                    foreach (string part in parts.Skip(1))
                        value = int.TryParse(part, out int index) ? ((System.Collections.IList)value)[index] :
                            (AccessTools.Field(value.GetType(), part) ?? throw new Exception("Unknown injected field: " + key)).GetValue(value);
                    Require(value is string && !string.IsNullOrWhiteSpace(actual[key]), "Invalid translated field: " + key);
                }
            }
        }
        foreach (var n in XDocument.Load(Path.Combine(repo, "1.7/Defs/RitualDefs/SSC_CosmeticRitual.xml")).Root.Elements())
            Equal("Ludeon.RimWorld.Ideology", (string)n.Attribute("MayRequire"));
    }
}
