// AI 辅助生成：第二批执行真实配方、原版结果选择、药物质量和医疗账单准入。
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static RecipeDef uterusSurgery;
    private static ResearchProjectDef uterusResearch;
    private static readonly Dictionary<ResearchProjectDef, float> researchProgress = new();
    private static readonly List<string> surgeryMessages = new();
    private static ThingDef concentratedPna;
    private static ThingDef[] medicines;
    private static HediffDef vagina;
    private static int surgeryRolls, injuryCalls, injuryDamage, tales, consumedStacks;
    private static float? forcedQuality;
    private static Action<Pawn> afterOutcome;
    private static Faction surgeryPlayer;

    /// <summary>注册本批宿主边界，加载真实生产资源，再分组验证启动和结果。</summary>
    private static void SurgeryTests(string repo, string game, Harmony harmony)
    {
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        // 仍执行原版结果遍历及 Death/Failure/Success.Apply；只控制质量来稳定覆盖成功和失败。
        Patch(AccessTools.Method(typeof(SurgeryOutcomeEffectDef), "GetQuality"), nameof(ControlQuality));
        harmony.Patch(AccessTools.Method(typeof(SurgeryOutcomeEffectDef), "GetOutcome"),
            prefix: new HarmonyMethod(typeof(Program), nameof(ObserveOutcome)),
            postfix: new HarmonyMethod(typeof(Program), nameof(AfterOutcome)));
        // 独立宿主不运行全套伤害与信件 UI；记录原版实际请求的损伤数量和部位。
        Patch(AccessTools.Method(typeof(HealthUtility), nameof(HealthUtility.GiveRandomSurgeryInjuries)), nameof(InjuryBoundary));
        Patch(AccessTools.Method(typeof(SurgeryOutcome), "SendLetter"), nameof(Skip));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == "Message"))
            Patch(method, nameof(MessageBoundary));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(TaleRecorder)).Where(m => m.Name == "RecordTale"))
            Patch(method, nameof(TaleBoundary));
        Patch(AccessTools.PropertyGetter(typeof(Faction), nameof(Faction.OfPlayer)), nameof(SurgeryPlayer));
        // 材料夹具从未生成到地图；销毁仍走原版，仅隔离遍历完整地图的预定／指定清理通知。
        Patch(AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis"), nameof(Skip));
        harmony.Patch(AccessTools.Method(typeof(RecipeWorker), nameof(RecipeWorker.ConsumeIngredient)),
            prefix: new HarmonyMethod(typeof(Program), nameof(ObserveConsumption)));
        // 测试 CLR 使用可独立运行的 Harmony 2；生产游戏使用 Harmony 3，不反射其版本化类特性。
        // 使用无业务逻辑的转发器，避免 Harmony 2 读取生产类上的 Harmony 3 特性；实际执行生产 Postfix。
        harmony.Patch(AccessTools.Method(typeof(Bill_Medical), nameof(Bill_Medical.PawnAllowedToStartAnew)),
            postfix: new HarmonyMethod(typeof(Program), nameof(UterusAdmission)));
        Check("batch2 XML native inheritance, research and ingredient configuration", () => LoadSurgeryResources(repo, game));
        if (uterusSurgery == null || uterusResearch == null) return;
        SurgeryEligibilityTests();
        SurgeryAdmissionTests();
        SurgeryResultTests();
        MedicineQualityTests();
        MedicalConsumptionTests();
        SurgeryAdmissionRegressionTests(harmony);
    }

    /// <summary>从本机原版父定义与生产 XML 解析资源；未加载的原版系统只提供命名引用。</summary>
    private static void LoadSurgeryResources(string repo, string game)
    {
        // SSCDefOf 还包含其他模块的严格引用；只在初始化这些未纳入宿主的引用时隔离预期日志。
        bootstrap = true;
        try { RuntimeHelpers.RunClassConstructor(typeof(SSCDefOf).TypeHandle); }
        finally { bootstrap = false; }
        void Add<T>(T def) where T : Def, new() => DefDatabase<T>.Add(def);
        SkillDefOf.Medicine = new SkillDef { defName = "Medicine", skillLabel = "医术" }; Add(SkillDefOf.Medicine);
        Add(new StatDef { defName = "MedicalOperationSpeed" });
        StatDefOf.MedicalPotency = new StatDef { defName = "MedicalPotency", defaultBaseValue = 1f }; Add(StatDefOf.MedicalPotency);
        Add(new EffecterDef { defName = "Surgery" }); Add(new SoundDef { defName = "Recipe_Surgery" });
        Add(new InspirationDef { defName = "Inspired_Surgery" });
        Add(new HediffDef { defName = "Sterilized", hediffClass = typeof(Hediff) });
        foreach (string name in new[] { "ImplantIUD", "RemoveIUD", "Vasectomy", "ReverseVasectomy" })
            Add(new RecipeDef { defName = name });
        Add(new ResearchTabDef { defName = "SSC_ResearchTabDef" });
        foreach (string name in new[] { "SSC_RES_BodyPartTraining", "SSC_RES_PNAConcentration" })
            Add(new ResearchProjectDef { defName = name });
        concentratedPna = SurgeryItem("Purple_Aphrodisiac_Nanofluid_Plus");
        // 医疗效力从当前游戏原始药物 Def 读取，避免手工设定高低顺序代替被测计算。
        var rawItems = XDocument.Load(Path.Combine(game, "Data/Core/Defs/ThingDefs_Items/Items_Resource_Manufactured.xml"));
        medicines = new[] { "MedicineHerbal", "MedicineIndustrial", "MedicineUltratech" }.Select(name =>
        {
            var def = SurgeryItem(name); def.thingClass = typeof(Medicine);
            var node = rawItems.Root.Elements("ThingDef").Single(n => (string)n.Element("defName") == name);
            def.statBases = new() { new StatModifier { stat = StatDefOf.MedicalPotency,
                value = float.Parse(node.Element("statBases").Element("MedicalPotency").Value, CultureInfo.InvariantCulture) } };
            return def;
        }).ToArray();
        // 手术只引用 Human；另一个实际人形种族会在后续注入用例中检查。
        var race = Bare<ThingDef>(); race.defName = "Human"; race.race = human.race; Add(race);
        var nodes = XDocument.Load(Path.Combine(game, "Data/Core/Defs/RecipeDefs/Recipes_Surgery_Misc.xml")).Root.Elements()
            .Where(n => (string)n.Attribute("Name") == "SurgeryFlesh")
            .Concat(XDocument.Load(Path.Combine(game, "Data/Core/Defs/RecipeDefs/SurgeryOutcomeEffectDefs.xml")).Root.Elements()
                .Where(n => (string)n.Attribute("Name") == "SurgeryOutcomeBase"))
            .Concat(XDocument.Load(Path.Combine(repo, "1.7/Defs/ResearchDefs/SSC_UterusResearch.xml")).Root.Elements())
            .Concat(XDocument.Load(Path.Combine(repo, "1.7/Defs/RecipeDefs/SSC_UterusSurgery.xml")).Root.Elements())
            .Select(n => { var xml = new XmlDocument(); xml.LoadXml(n.ToString()); return xml.DocumentElement; }).ToList();
        XmlInheritance.Clear();
        foreach (var node in nodes) XmlInheritance.TryRegister(node, null);
        XmlInheritance.Resolve();
        foreach (var original in nodes.Where(n => n.GetAttribute("Abstract") != "True"))
        {
            var node = (XmlElement)XmlInheritance.GetResolvedNodeFor(original).CloneNode(true);
            node.RemoveAttribute("ParentName"); node.RemoveAttribute("Abstract");
            // DLC 的原版失败条目不适用于本手术；宿主已有对应命名 Hediff，保留实际结果列表。
            foreach (XmlElement element in node.SelectNodes(".//*[@MayRequire]")) element.RemoveAttribute("MayRequire");
            if (node.Name == "RecipeDef") { uterusSurgery = DirectXmlToObject.ObjectFromXml<RecipeDef>(node, false); Add(uterusSurgery); }
            else if (node.Name == "ResearchProjectDef") { uterusResearch = DirectXmlToObject.ObjectFromXml<ResearchProjectDef>(node, false); Add(uterusResearch); }
            else if (node.Name == "SurgeryOutcomeEffectDef") Add(DirectXmlToObject.ObjectFromXml<SurgeryOutcomeEffectDef>(node, false));
        }
        XmlInheritance.Clear(); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        uterusSurgery.ResolveReferences();
        SSCDefOf.SSC_RES_UterusDevelopment = uterusResearch;
        SSCDefOf.SSC_Surgery_StartUterusDevelopment = uterusSurgery;
        Current.Game.researchManager = new ResearchManager();
        Current.Game.questManager = new QuestManager();
        AccessTools.Field(typeof(ResearchManager), "progress").SetValue(Current.Game.researchManager, researchProgress);
        surgeryPlayer = new Faction { def = new FactionDef { defName = "SurgeryPlayer", isPlayer = true, techLevel = TechLevel.Industrial } };
        researchProgress[uterusResearch] = float.MaxValue;
        vagina = new HediffDef_SexPart { defName = "FixtureVagina", hediffClass = typeof(Hediff),
            genitalFamily = GenitalFamily.Vagina, genitalTags = new() };
        DefDatabase<HediffDef>.Add(vagina);
        Near(2000, uterusResearch.baseCost); Equal(TechLevel.Medieval, uterusResearch.techLevel);
        Equal("SSC_ResearchTabDef", uterusResearch.tab.defName);
        Equal("SSC_RES_BodyPartTraining,SSC_RES_PNAConcentration", string.Join(',', uterusResearch.prerequisites.Select(p => p.defName)));
        Require(uterusResearch.requiredResearchBuilding == null && uterusResearch.requiredResearchFacilities.NullOrEmpty());
        Equal(0, uterusResearch.TechprintCount);
        Near(4000, uterusSurgery.workAmount); Near(0, uterusSurgery.deathOnFailedSurgeryChance);
        Equal(10, uterusSurgery.skillRequirements.Single().minLevel);
        Require(uterusSurgery.targetsBodyPart && uterusSurgery.hideBodyPartNames && uterusSurgery.anesthetize);
        Equal(uterusResearch, uterusSurgery.researchPrerequisite); Equal(Gender.Female, uterusSurgery.genderPrerequisite);
        Equal(typeof(Recipe_StartUterusDevelopment), uterusSurgery.Worker.GetType());
        Near(5, uterusSurgery.ingredients[0].GetBaseCount()); Near(6, uterusSurgery.ingredients[1].GetBaseCount());
        Require(uterusSurgery.ingredients[0].filter.Allows(concentratedPna));
        foreach (var medicine in medicines)
        {
            Require(uterusSurgery.ingredients[1].filter.Allows(medicine));
            Require(uterusSurgery.fixedIngredientFilter.Allows(medicine) && uterusSurgery.defaultIngredientFilter.Allows(medicine));
        }
        Equal("SurgeryOutcomeBase", uterusSurgery.surgeryOutcomeEffect.defName);
        Require(uterusSurgery.surgeryOutcomeEffect.outcomes.Any(o => o is SurgeryOutcome_Death));
        Require(uterusSurgery.surgeryOutcomeEffect.outcomes.Any(o => o is SurgeryOutcome_Failure && o.totalDamage > 0));
        Equal(6, uterusSurgery.surgeryOutcomeEffect.comps.Count);
        Require(uterusSurgery.addsHediff == null && uterusSurgery.addsHediffOnFailure == null);
    }

    /// <summary>建立真实物品 Def，隔离图形构造；过滤器、属性和消费仍使用原版实现。</summary>
    private static ThingDef SurgeryItem(string name)
    {
        var def = Bare<ThingDef>(); def.defName = name; def.label = name; def.category = ThingCategory.Item;
        def.thingClass = typeof(Thing); def.stackLimit = 75; def.destroyable = true; def.comps = new(); def.virtualDefs = new();
        DefDatabase<ThingDef>.Add(def); return def;
    }

    /// <summary>创建有真实年龄、技能、身份组件及当前器官记录的独立成年身体。</summary>
    private static Pawn SurgeryPawn(PawnIdentity identity = PawnIdentity.Slave)
    {
        var pawn = NewPawn(); var def = Bare<ThingDef>(); def.defName = "SurgeryRace" + pawn.thingIDNumber;
        def.category = ThingCategory.Pawn; def.race = new RaceProperties { intelligence = Intelligence.Humanlike, body = human.race.body };
        var corpseCategory = Bare<ThingCategoryDef>(); corpseCategory.defName = "CorpsesHumanlike";
        AccessTools.Field(typeof(RaceProperties), "fleshType").SetValue(def.race, new FleshTypeDef { defName = "Normal",
            isOrganic = true, corpseCategory = corpseCategory });
        def.race.lifeStageAges.Add(new LifeStageAge { minAge = 0,
            def = new LifeStageDef { defName = "FixtureAdult", developmentalStage = DevelopmentalStage.Adult } });
        pawn.def = def; pawn.gender = Gender.Female;
        pawn.kindDef = new PawnKindDef { defName = "SurgeryKind", race = def };
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        AccessTools.Field(typeof(Pawn_AgeTracker), "growth").SetValue(pawn.ageTracker, 1f);
        pawn.ageTracker.AgeBiologicalTicks = 30L * GenDate.TicksPerYear;
        pawn.story = new Pawn_StoryTracker(pawn); pawn.skills = new Pawn_SkillTracker(pawn);
        pawn.mindState = new Verse.AI.Pawn_MindState(pawn);
        pawn.skills.GetSkill(SkillDefOf.Medicine).Level = 10;
        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(pawn,
            new List<ThingComp> { new CompSexSlaveTraining { parent = pawn, pawnIdentity = identity } });
        // 原生器官类别可以是不育器官；它只负责证明身体结构，并不为手术提供额外生育能力。
        pawn.health.AddHediff(vagina, pawn.RaceProps.body.corePart);
        return pawn;
    }

    /// <summary>创建真实部位医疗账单，并挂到当前患者的原版账单栈。</summary>
    private static Bill_Medical SurgeryBill(Pawn patient)
    {
        var bill = new Bill_Medical(uterusSurgery, null);
        AccessTools.Field(typeof(Bill_Medical), "part").SetValue(bill, patient.RaceProps.body.corePart);
        patient.BillStack.AddBill(bill); return bill;
    }

    /// <summary>检查完整业务报告拒绝且原因可读；菜单候选是否保留由原生菜单回归单独验证。</summary>
    private static void Refused(Pawn pawn, string reason)
    {
        var report = uterusSurgery.Worker.AvailableReport(pawn);
        Require(!report.Accepted && report.Reason.Contains(reason), report.Reason);
    }

    /// <summary>验证资格来自当前成年身体和 SSC 身份，不要求锁链、可受精标签或其他生育属性。</summary>
    private static void SurgeryEligibilityTests()
    {
        Check("batch2 unbound adult female SSC slave with non-fertile vagina is eligible", () =>
        {
            var pawn = SurgeryPawn(); Require(SSCUterusSurgeryUtility.RecipientReport(pawn).Accepted);
            Require(uterusSurgery.Worker.AvailableReport(pawn).Accepted);
            Equal(pawn.RaceProps.body.corePart, uterusSurgery.Worker.GetPartsToApplyOn(pawn, uterusSurgery).Single());
            Require(SSCUterusDevelopmentUtility.Find(pawn) == null); Near(1, Factor(pawn));
        });
        Check("batch2 missing research stays locked in full eligibility report", () =>
        {
            researchProgress[uterusResearch] = 0;
            try { Refused(SurgeryPawn(), "研究"); }
            finally { researchProgress[uterusResearch] = float.MaxValue; }
        });
        foreach (var identity in new[] { PawnIdentity.Unset, PawnIdentity.Master }) Check("batch2 SSC identity required: " + identity,
            () => Refused(SurgeryPawn(identity), "身份"));
        Check("batch2 male body cannot start", () => { var pawn = SurgeryPawn(); pawn.gender = Gender.Male; Require(!uterusSurgery.Worker.AvailableOnNow(pawn)); });
        Check("batch2 vanilla Adult seventeen-year body still cannot start", () =>
        { var pawn = SurgeryPawn(); pawn.ageTracker.AgeBiologicalTicks = 17L * GenDate.TicksPerYear; Refused(pawn, "成年"); });
        Check("batch2 missing age tracker cannot start", () => { var pawn = SurgeryPawn(); pawn.ageTracker = null; Refused(pawn, "成年"); });
        Check("batch2 removed current organ cannot start", () =>
        { var pawn = SurgeryPawn(); pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(vagina)); Refused(pawn, "器官"); });
        Check("batch2 detached organ reference cannot qualify", () =>
        { var pawn = SurgeryPawn(); pawn.health.hediffSet.GetFirstHediffOfDef(vagina).Part = new BodyPartRecord(); Refused(pawn, "器官"); });
        Check("batch2 no Torso never falls back to whole body", () =>
        {
            var pawn = SurgeryPawn(); pawn.def.race.body = new BodyDef { defName = "NoTorso", corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Core" } } };
            pawn.def.race.body.ResolveReferences(); Refused(pawn, "躯干");
        });
        foreach (var name in new[] { "Pregnant", "PregnantHuman", "RJW_pregnancy", "RJW_pregnancy_beast" })
            Check("batch2 hidden pregnancy refuses surgery: " + name, () =>
            { var pawn = SurgeryPawn(); pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(name)); Refused(pawn, "妊娠"); });
        foreach (float progress in new[] { .01f, .7f, 1f }) Check("batch2 started bodies cannot reset through surgery: " + progress,
            () => { var pawn = SurgeryPawn(); Start(pawn, progress); Refused(pawn, "已经"); });
        Check("batch2 foreign target part is refused without modifying anatomy", () =>
        { var pawn = SurgeryPawn(); Require(!uterusSurgery.Worker.AvailableReport(pawn, new BodyPartRecord()).Accepted); Equal(1, pawn.health.hediffSet.hediffs.Count); });
        Check("batch2 existing independent infertility does not block startup", () =>
        {
            var pawn = SurgeryPawn(); var def = new HediffDef { defName = "ExistingInfertility", hediffClass = typeof(Hediff), stages = new() { new HediffStage { fertilityFactor = 0 } } };
            pawn.health.AddHediff(def); Require(uterusSurgery.Worker.AvailableOnNow(pawn));
        });
        Check("batch2 existing injector adds surgery to humanlike races only", () =>
        {
            // 仅给未纳入宿主的旧手术提供引用；实际执行生产注入器，检查本手术进入其他人形种族。
            var other = SurgeryPawn().def; DefDatabase<ThingDef>.Add(other);
            var animal = Bare<ThingDef>(); animal.defName = "SurgeryAnimal"; animal.race = new RaceProperties { intelligence = Intelligence.Animal }; DefDatabase<ThingDef>.Add(animal);
            SSCDefOf.SSC_InducePersonalityExcretion = SSCDefOf.SSC_Surgery_GenderChange_MtF =
                SSCDefOf.SSC_ApplyGelatinization = SSCDefOf.SSC_ApplyFullGelatinization = SSCDefOf.SSC_Surgery_BeautyEnhancement = new RecipeDef { defName = "OtherSurgery" };
            RuntimeHelpers.RunClassConstructor(typeof(RecipeInjector).TypeHandle);
            Require(uterusSurgery.recipeUsers.Contains(other)); Require(!uterusSurgery.recipeUsers.Contains(animal));
        });
    }

    /// <summary>通过实际 Harmony 后置补丁验证旧账单和医生门槛，不手工调用 Postfix 模拟挂接。</summary>
    private static void SurgeryAdmissionTests()
    {
        Check("batch2 native medical bill accepts skilled doctor and refuses medicine nine", () =>
        {
            var patient = SurgeryPawn(); var doctor = SurgeryPawn(PawnIdentity.Unset); var bill = SurgeryBill(patient);
            Require(bill.PawnAllowedToStartAnew(doctor)); doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 9;
            Require(!bill.PawnAllowedToStartAnew(doctor));
        });
        Check("batch2 native doctor restriction remains rejected by postfix", () =>
        {
            var bill = SurgeryBill(SurgeryPawn());
            AccessTools.Field(typeof(Bill), "pawnRestriction").SetValue(bill, SurgeryPawn(PawnIdentity.Unset));
            Require(!bill.PawnAllowedToStartAnew(SurgeryPawn(PawnIdentity.Unset)));
        });
        foreach (string change in new[] { "pregnant", "identity", "organ", "started", "part" })
            Check("batch2 waiting bill rechecks current patient: " + change, () =>
            {
                var patient = SurgeryPawn(); var doctor = SurgeryPawn(PawnIdentity.Unset); var bill = SurgeryBill(patient);
                Require(bill.PawnAllowedToStartAnew(doctor)); ChangeSurgeryPatient(patient, change, bill);
                Require(!bill.PawnAllowedToStartAnew(doctor));
            });
        Check("batch2 admission does not narrow unrelated medical recipes", () =>
        {
            var bill = SurgeryBill(SurgeryPawn(PawnIdentity.Unset)); bill.recipe = new RecipeDef { defName = "Unrelated" };
            Require(bill.PawnAllowedToStartAnew(SurgeryPawn(PawnIdentity.Unset)));
        });
    }

    /// <summary>变更等待中的真实身体或账单，供接单及实际完成两条路径重复验收。</summary>
    private static void ChangeSurgeryPatient(Pawn pawn, string change, Bill_Medical bill)
    {
        if (change == "pregnant") pawn.health.AddHediff(HediffDefOf.PregnantHuman);
        else if (change == "identity") pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
        else if (change == "organ") pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(vagina));
        else if (change == "started") Start(pawn, .6f);
        else if (change == "part") AccessTools.Field(typeof(Bill_Medical), "part").SetValue(bill, new BodyPartRecord());
    }

    /// <summary>稳定执行原版结果遍历，验证成功落地、失败无残留以及两次资格复查。</summary>
    private static void SurgeryResultTests()
    {
        Check("batch2 successful real outcome creates one percent Torso record with no second consumption", () =>
        {
            var patient = SurgeryPawn(); var bill = SurgeryBill(patient); var doctor = SurgeryPawn(PawnIdentity.Unset);
            var ingredients = new List<Thing> { new Thing { def = concentratedPna, stackCount = 5 }, new Thing { def = medicines[0], stackCount = 6 } };
            ResetSurgeryResult(1f);
            try
            {
                uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, doctor, ingredients, bill);
                var record = SSCUterusDevelopmentUtility.Find(patient); Require(record != null);
                Equal(bill.Part, record.Part); Near(.01f, record.Severity); Near(0, Factor(patient)); Require(!record.FertilityEnabled);
                Equal(1, surgeryRolls); Equal(1, tales); Equal(0, injuryCalls); Equal(0, consumedStacks);
                Equal(5, ingredients[0].stackCount); Equal(6, ingredients[1].stackCount);
                Require(surgeryMessages.Any(m => m.Contains("1%")));
                // 第二次调用不得再次抽取结果、增加进度或补发成功反馈。
                uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, doctor, ingredients, bill);
                Equal(1, surgeryRolls); Equal(1, tales); Near(.01f, record.Severity);
            }
            finally { forcedQuality = null; }
        });
        Check("batch2 native failed outcome requests ordinary injuries and leaves no development", () =>
        {
            var patient = SurgeryPawn(); var bill = SurgeryBill(patient); ResetSurgeryResult(0f);
            try
            {
                uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, SurgeryPawn(PawnIdentity.Unset), new(), bill);
                Equal(1, surgeryRolls); Equal(1, injuryCalls); Require(injuryDamage > 0);
                Require(SSCUterusDevelopmentUtility.Find(patient) == null); Near(1, Factor(patient));
                Equal(0, tales); Require(!surgeryMessages.Any(m => m.Contains("已开始"))); Require(!patient.Dead);
            }
            finally { forcedQuality = null; }
        });
        Check("batch2 native direct death outcome is disabled by recipe zero", () =>
        {
            var patient = SurgeryPawn(); injuryCalls = 0;
            var death = uterusSurgery.surgeryOutcomeEffect.outcomes.OfType<SurgeryOutcome_Death>().Single();
            Require(!death.Apply(0, uterusSurgery, SurgeryPawn(PawnIdentity.Unset), patient, patient.RaceProps.body.corePart));
            Require(!patient.Dead); Equal(0, injuryCalls);
        });
        foreach (string change in new[] { "pregnant", "identity", "organ", "started", "part" })
            Check("batch2 completion rechecks patient before native roll: " + change, () =>
            {
                var patient = SurgeryPawn(); var bill = SurgeryBill(patient); ChangeSurgeryPatient(patient, change, bill); ResetSurgeryResult(1);
                try { uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, SurgeryPawn(PawnIdentity.Unset), new(), bill); Equal(0, surgeryRolls); Equal(0, tales); }
                finally { forcedQuality = null; }
            });
        Check("batch2 after native success callback pregnancy blocks final landing without removing pregnancy", () =>
        {
            var patient = SurgeryPawn(); var bill = SurgeryBill(patient); ResetSurgeryResult(1);
            afterOutcome = p => p.health.AddHediff(HediffDefOf.PregnantHuman);
            try
            {
                uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, SurgeryPawn(PawnIdentity.Unset), new(), bill);
                Equal(1, surgeryRolls); Equal(0, tales); Require(SSCUterusDevelopmentUtility.Find(patient) == null);
                Require(patient.health.hediffSet.HasHediff(HediffDefOf.PregnantHuman));
            }
            finally { afterOutcome = null; forcedQuality = null; }
        });
        Check("batch2 null doctor or null target part cannot bypass surgery", () =>
        {
            var patient = SurgeryPawn(); var bill = SurgeryBill(patient); ResetSurgeryResult(1);
            try
            {
                uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, null, new(), bill);
                uterusSurgery.Worker.ApplyOnPawn(patient, null, SurgeryPawn(PawnIdentity.Unset), new(), bill);
                Equal(0, surgeryRolls); Require(SSCUterusDevelopmentUtility.Find(patient) == null);
            }
            finally { forcedQuality = null; }
        });
    }

    /// <summary>使用真实原版药物质量组件，确认同条件下药物档次与混合数量影响。</summary>
    private static void MedicineQualityTests()
    {
        Check("batch2 native medicine component improves quality herbal < industrial < ultratech", () =>
        {
            var patient = SurgeryPawn(); var doctor = SurgeryPawn(PawnIdentity.Unset); var bill = SurgeryBill(patient);
            var component = uterusSurgery.surgeryOutcomeEffect.comps.OfType<SurgeryOutcomeComp_MedicineQuality>().Single();
            var factors = new List<float>();
            foreach (var medicine in medicines)
            {
                bill.consumedMedicine = new() { [medicine] = 6 }; float quality = 1;
                component.AffectQuality(uterusSurgery, doctor, patient, new(), bill.Part, bill, ref quality); factors.Add(quality);
            }
            Require(factors[0] < factors[1] && factors[1] < factors[2]);
            Near(.88f, factors[0]); Near(1, factors[1]); Near(1.18f, factors[2]);
            bill.consumedMedicine = new() { [medicines[0]] = 3, [medicines[2]] = 3 }; float mixed = 1;
            component.AffectQuality(uterusSurgery, doctor, patient, new(), bill.Part, bill, ref mixed); Near(1.03f, mixed);
        });
        Check("batch2 recipe leaves consumption to inherited native worker", () =>
        {
            Equal(typeof(RecipeWorker), uterusSurgery.Worker.GetType().GetMethod("ConsumeIngredient").DeclaringType);
            Equal(typeof(Recipe_Surgery), uterusSurgery.Worker.GetType().GetMethod("CompletableEver").DeclaringType);
        });
    }

    /// <summary>按原版完成顺序执行材料分离、消费及医疗账单通知，覆盖实际消费后的结果落地。</summary>
    private static void MedicalConsumptionTests()
    {
        RecordDefOf.OperationsReceived = new RecordDef { defName = "OperationsReceived", type = RecordType.Int };
        RecordDefOf.OperationsPerformed = new RecordDef { defName = "OperationsPerformed", type = RecordType.Int };
        DefDatabase<RecordDef>.Add(RecordDefOf.OperationsReceived);
        DefDatabase<RecordDef>.Add(RecordDefOf.OperationsPerformed);
        foreach (float quality in new[] { 1f, 0f }) Check("batch2 native medical completion consumes five PNA and six mixed medicines once: " + quality, () =>
        {
            var patient = SurgeryPawn(); var doctor = SurgeryPawn(PawnIdentity.Unset); var bill = SurgeryBill(patient);
            patient.records = new Pawn_RecordsTracker(patient); doctor.records = new Pawn_RecordsTracker(doctor);
            var pna = new Thing { def = concentratedPna, stackCount = 5 };
            var herbal = new Medicine { def = medicines[0], stackCount = 3 };
            var glitter = new Medicine { def = medicines[2], stackCount = 3 };
            var job = new Verse.AI.Job { bill = bill, placedThings = new()
            { new ThingCountClass(pna, 5), new ThingCountClass(herbal, 3), new ThingCountClass(glitter, 3) } };
            ResetSurgeryResult(quality);
            try
            {
                // 这是原版 FinishRecipe 中的三个实际入口，不复制分堆、药物登记或销毁算法。
                var ingredients = (List<Thing>)AccessTools.Method(typeof(Verse.AI.Toils_Recipe), "CalculateIngredients")
                    .Invoke(null, new object[] { job, doctor });
                Equal(3, bill.consumedMedicine[medicines[0]]); Equal(3, bill.consumedMedicine[medicines[2]]);
                Require(!bill.consumedMedicine.ContainsKey(concentratedPna)); Require(job.placedThings == null);
                AccessTools.Method(typeof(Verse.AI.Toils_Recipe), "ConsumeIngredients")
                    .Invoke(null, new object[] { ingredients, uterusSurgery, null });
                Equal(3, consumedStacks); Require(pna.Destroyed && herbal.Destroyed && glitter.Destroyed);
                bill.Notify_IterationCompleted(doctor, ingredients);
                Equal(3, consumedStacks); Equal(1, surgeryRolls); Require(bill.iterationCompleted);
                Require(!patient.BillStack.Bills.Contains(bill));
                Equal(quality > 0, SSCUterusDevelopmentUtility.Find(patient) != null);
            }
            finally { forcedQuality = null; }
        });
    }

    /// <summary>每次结果用例清空观察值，质量只影响确定性分支覆盖。</summary>
    private static void ResetSurgeryResult(float quality)
    { forcedQuality = quality; surgeryRolls = injuryCalls = injuryDamage = tales = consumedStacks = 0; surgeryMessages.Clear(); }
    /// <summary>稳定成功／失败用例中的质量；药物效力用例直接调用实际组件且不经过此边界。</summary>
    private static bool ControlQuality(RecipeDef recipe, ref float __result)
    { if (recipe != uterusSurgery || !forcedQuality.HasValue) return true; __result = forcedQuality.Value; return false; }
    /// <summary>只观察真实原版结果选择是否被调用。</summary>
    private static void ObserveOutcome(RecipeDef recipe) { if (recipe == uterusSurgery) surgeryRolls++; }
    /// <summary>模拟其他结果补丁改变身体，检验生产成功落地前的第二次资格复查。</summary>
    private static void AfterOutcome(RecipeDef recipe, Pawn patient) { if (recipe == uterusSurgery) afterOutcome?.Invoke(patient); }
    /// <summary>记录原版失败请求的损伤；独立宿主不运行完整伤害与生命状态计算。</summary>
    private static bool InjuryBoundary(int totalDamage) { injuryCalls++; injuryDamage += totalDamage; return false; }
    /// <summary>记录玩家消息并隔离信件、声音和屏幕消息队列。</summary>
    private static bool MessageBoundary(object[] __args) { if (__args.FirstOrDefault() is string text) surgeryMessages.Add(text); return false; }
    /// <summary>记录成功事迹请求，不初始化全套故事系统。</summary>
    private static bool TaleBoundary() { tales++; return false; }
    /// <summary>为原版研究成本查询提供当前玩家阵营。</summary>
    private static bool SurgeryPlayer(ref Faction __result) { __result = surgeryPlayer; return false; }
    /// <summary>观察材料消费入口，继续执行原版行为。</summary>
    private static void ObserveConsumption() => consumedStacks++;
    /// <summary>仅将原版实际接单结果传给生产后置补丁，不复制准入逻辑。</summary>
    private static void UterusAdmission(Bill_Medical __instance, Pawn pawn, ref bool __result)
        => Harmony_UterusSurgeryAdmission.Postfix(__instance, pawn, ref __result);
}
