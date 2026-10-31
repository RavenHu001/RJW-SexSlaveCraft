// AI 辅助生成：印记专项执行真实候选 DLL、XML、身份/绑定/道具入口及 Scribe。
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 按初始／主人／性奴顺序定位生产定义；预期倍率由效果用例给出，不从生产阶段反推。
    private static readonly string[] imprintNames = { SSCImprintUtility.InitialDefName, SSCImprintUtility.MasterDefName, SSCImprintUtility.SlaveDefName };
    private static ThingDef imprintSeed;
    // 只控制本专项的Biotech资源准入边界，不改变生产模块的DLC判断算法。
    private static bool imprintBiotech = true;

    /// <summary>准备最小原生上下文、加载生产印记XML并安装必要边界，再执行五组真实入口用例。</summary>
    private static void ImprintTests(string repo, Harmony harmony, bool fertilityOnly = false, bool preconceptionOnly = false, int birthSelection = 0)
    {
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
        // 只预热游戏 DefOf 的非本批引用；生产 XML 解析和用例期间错误仍计为失败。
        bootstrap = true;
        try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SSCDefOf).TypeHandle); }
        finally { bootstrap = false; }
        errors.Clear();
        // 人格采集会查询其他特化定义，补齐无效果的依赖占位；本批印记定义仍从生产XML原生解析。
        foreach (FieldInfo field in typeof(SSCDefOf).GetFields(BindingFlags.Static | BindingFlags.Public))
            if (field.FieldType == typeof(HediffDef) && field.GetValue(null) == null && field.Name != "SSC_Hediff_MasterProgression")
                field.SetValue(null, ImprintBoundaryDef(field.Name, typeof(Hediff)));
        foreach (string name in new[] { "SSC_Hediff_PetCat", "SSC_Hediff_PetDog", "SSC_Hediff_PetRabbit", "SSC_Hediff_PetCat_Final", "SSC_Hediff_PetDog_Final", "SSC_Hediff_PetRabbit_Final" })
            ImprintBoundaryDef(name, typeof(Hediff));
        AccessTools.Field(typeof(TickManager), "gameStartAbsTick").SetValue(Current.Game.tickManager, 1);
        harmony.Patch(AccessTools.Method(typeof(ModLister), "AllModsActiveNoSuffix", new[] { typeof(IEnumerable<string>) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(ImprintBiotechActive)));
        // 属性夹具提供中性基础值，实际Hediff偏移与乘算继续经过游戏StatWorker。
        foreach (string name in new[] { "LifespanFactor", "PsychicSensitivity", "GlobalLearningFactor", "LearningRateFactor", "Vulnerability", "SSC_OrgasmFrequency" })
            if (DefDatabase<StatDef>.GetNamedSilentFail(name) == null)
                DefDatabase<StatDef>.Add(new StatDef { defName = name, defaultBaseValue = 1f });
        foreach (string name in new[] { "WoundInfection", "GutWorms", "MuscleParasites" })
            if (DefDatabase<HediffDef>.GetNamedSilentFail(name) == null) PregnancyDef(name);
        SSCDefOf.ChainOfSexSlave = ImprintBoundaryDef("Hediff_ChainOfSexSlave", typeof(Hediff_ChainOfSexSlave));
        SSCDefOf.BridleOfSexSlave = ImprintBoundaryDef("Hediff_BridleOfSexSlave", typeof(Hediff_BridleOfSexSlave));
        SSCDefOf.SSC_PersonalityExcreted_Done = ImprintBoundaryDef("SSC_PersonalityExcreted_Done", typeof(Hediff));
        SSCDefOf.SSC_BasicTraining = new ResearchProjectDef { defName = "SSC_BasicTraining" };
        DefDatabase<ResearchProjectDef>.Add(SSCDefOf.SSC_BasicTraining);
        Current.Game.researchManager = new ResearchManager();
        SSCMod.settings = new SSCSettings { allowDirectMasterSelection = false };
        // 种子夹具支持原版拆堆和销毁；具体收益与单枚消耗执行真实生产结算入口。
        imprintSeed = Bare<ThingDef>(); imprintSeed.defName = "SSC_MasterQualificationItem";
        imprintSeed.category = ThingCategory.Item; imprintSeed.thingClass = typeof(ThingWithComps); imprintSeed.stackLimit = 10;
        imprintSeed.comps = new List<CompProperties>();
        imprintSeed.destroyable = true;
        SSCDefOf.SSC_MasterQualificationItem = imprintSeed;
        DefDatabase<ThingDef>.Add(imprintSeed);
        // 保留MayRequire交叉引用处理，分别验证DLC启用和未启用时同一生产配置的装载结果。
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_Imprint.xml"));
        foreach (XmlNode node in xml.DocumentElement.SelectNodes("HediffDef"))
            DefDatabase<HediffDef>.Add(DirectXmlToObject.ObjectFromXml<HediffDef>(node, false));
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Require(errors.Count == 0, string.Join('\n', errors));
        harmony.Patch(AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == "Message"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        // 不运行训导官/共振场的整套地图维护，绑定本身和双向健康记录继续执行。
        harmony.Patch(AccessTools.Method(typeof(SSCMasterBondUtility), nameof(SSCMasterBondUtility.RefreshMasterEmpowerment)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_AgeTracker), "TickBiologicalAge"),
            prefix: new HarmonyMethod(typeof(Harmony_ImprintAge), nameof(Harmony_ImprintAge.Prefix)),
            postfix: new HarmonyMethod(typeof(Harmony_ImprintAge), nameof(Harmony_ImprintAge.Postfix)));
        // 正常年龄速率固定为 1；仍执行生产停龄补丁及原版进位/年龄字段更新。
        harmony.Patch(AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), "BiologicalTicksPerTick"), prefix: new HarmonyMethod(typeof(Program), nameof(ImprintAgeRate)));

        // 独立孕前分区只复用资源和身体初始化，不先执行印记或特化旧用例。
        if (fertilityOnly || preconceptionOnly || birthSelection != 0) { FertilityTests(repo, harmony, preconceptionOnly, birthSelection); return; }

        Check("imprint native XML resolves all references and forbids item cures", () =>
        {
            Require(SSCImprintUtility.DefinitionsAvailable);
            foreach (string name in imprintNames) Require(!DefDatabase<HediffDef>.GetNamed(name).everCurableByItem);
        });
        Check("imprint native XML omits Biotech-only stat when DLC inactive", () =>
        {
            imprintBiotech = false;
            try
            {
                var withoutDlc = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode("HediffDef"), false);
                DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                Require(withoutDlc.stages[0].statFactors.All(s => s.stat.defName != "LearningRateFactor"));
            }
            finally { imprintBiotech = true; }
        });
        // 从实际属性开始，再覆盖获取、真实绑定、年龄推进和人格存读档，避免仅检查配置文本。
        Check("master cannot extract and preparation is removed without touching bonds", () =>
        {
            var master = ImprintPawn(PawnIdentity.Master); var slave = ImprintPawn(PawnIdentity.Slave);
            Require(SSCBondUtility.Bind(master, slave));
            Require(!SSCPersonalityExcretionAdmission.CanExtract(master));
            Require(SSCPersonalityExcretionAdmission.CanExtract(slave));
            SSCDefOf.SSC_InducePersonalityExcretion = new RecipeDef { defName = "SSC_InducePersonalityExcretion" };
            var surgery = new Recipe_AddHediff { recipe = SSCDefOf.SSC_InducePersonalityExcretion };
            bool available = true;
            Patch_RabbitClone_BlocksPersonalityExcretionSurgery.Postfix(surgery, master, ref available);
            Require(!available);
            Require(!Harmony_MasterBlocksExcretionOperation.Prefix(surgery, master));
            Require(Harmony_MasterBlocksExcretionOperation.Prefix(surgery, slave));
            var preparation = new Hediff_SSC_PersonalityExcreting { def = SSCDefOf.SSC_PersonalityExcreting, pawn = master };
            master.health.AddHediff(preparation);
            Require(!master.health.hediffSet.hediffs.Contains(preparation));
            var stale = new Hediff_SSC_PersonalityExcreting { def = SSCDefOf.SSC_PersonalityExcreting, pawn = master };
            master.health.hediffSet.hediffs.Add(stale);
            Require(stale.ShouldRemove);
            ExcretionUtility.DoExcretion(master);
            Require(!master.health.hediffSet.hediffs.Contains(stale));
            Equal(PawnIdentity.Master, SSCIdentityUtility.GetIdentity(master));
            Require(SSCBondUtility.IsBoundTo(slave, master));
            var candidate = ImprintPawn();
            candidate.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
            candidate.health.AddHediff(new Hediff_SSC_PersonalityExcreting { def = SSCDefOf.SSC_PersonalityExcreting, pawn = candidate });
            Require(SSCIdentityUtility.TrySetIdentity(candidate, PawnIdentity.Master));
            Require(!candidate.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreting));
        });
        ImprintEffectTests();
        ImprintSeedTests();
        ImprintBindingTests();
        ImprintAgeTests();
        ImprintPersonalityTests();
    }

    /// <summary>为非印记依赖登记最小健康定义；已有真实引用时沿用原实例，不替换被测印记资源。</summary>
    private static HediffDef ImprintBoundaryDef(string name, Type type)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
        if (def != null) return def;
        def = new HediffDef { defName = name, label = name, hediffClass = type, initialSeverity = 1, maxSeverity = 1 };
        DefDatabase<HediffDef>.Add(def); return def;
    }

    /// <summary>创建具有真实健康、年龄、故事与训练组件的独立身体，默认没有资格或印记。</summary>
    private static Pawn ImprintPawn(PawnIdentity identity = PawnIdentity.Unset, int age = 30)
    {
        Pawn pawn = NewPawn();
        var race = new RaceProperties { intelligence = Intelligence.Humanlike, body = human.race.body };
        // 健康属性说明会读取肉体类型，提供真实普通有机身体，避免缺省null被误认成机械体。
        var corpseCategory = Bare<ThingCategoryDef>(); corpseCategory.defName = "CorpsesHumanlike";
        AccessTools.Field(typeof(RaceProperties), "fleshType").SetValue(race,new FleshTypeDef { defName="Normal",isOrganic=true,corpseCategory=corpseCategory });
        race.lifeStageAges.Add(new LifeStageAge { minAge = 0, def = new LifeStageDef { defName = "ImprintAdult", developmentalStage = DevelopmentalStage.Adult } });
        pawn.def = Bare<ThingDef>(); pawn.def.defName = "ImprintRace" + pawn.thingIDNumber;
        pawn.def.category = ThingCategory.Pawn; pawn.def.race = race;
        pawn.kindDef = new PawnKindDef { defName = "ImprintKind", race = pawn.def };
        // 不启动整局生成器，显式提供成年生命阶段与成长缓存，年龄字段仍由原版追踪器维护。
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        AccessTools.Field(typeof(Pawn_AgeTracker), "growth").SetValue(pawn.ageTracker, 1f);
        pawn.ageTracker.AgeBiologicalTicks = age * (long)GenDate.TicksPerYear;
        pawn.story = new Pawn_StoryTracker(pawn); pawn.skills = new Pawn_SkillTracker(pawn);
        pawn.mindState = new Verse.AI.Pawn_MindState(pawn);
        // 主人资格与成长版本使用生产写入接口初始化，不让旧档迁移替代本例待验证的获取过程。
        var comp = new CompSexSlaveTraining { parent = pawn, pawnIdentity = identity };
        comp.ReplaceMasterQualification(false); comp.masterProgressionVersion = SSCMasterProgressionUtility.CurrentVersion;
        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(pawn, new List<ThingComp> { comp });
        return pawn;
    }

    /// <summary>为属性和存档用例直接走原版健康添加入口；获取规则另由种子与绑定用例验证。</summary>
    private static Hediff Mark(Pawn pawn, string name)
    { pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(name)); return SSCImprintUtility.Find(pawn); }

    /// <summary>生成可实际拆堆和销毁的种子夹具，便于检查成功单枚消耗与拒绝保料。</summary>
    private static Thing Seed(int count = 3) => new ThingWithComps { def = imprintSeed, stackCount = count };

    /// <summary>调用生产种子结算并要求成功，将业务拒绝原因直接交给当前用例报告。</summary>
    private static void UseSeed(Pawn pawn, Thing seed)
        => Require(SSCMasterQualificationUtility.TryUseQualificationItem(pawn, seed, out string reason), reason);

    /// <summary>调用已挂生产补丁的原版共同年龄推进点，覆盖自然增长、越界和停龄。</summary>
    private static void AgeStep(Pawn pawn, int ticks)
        => AccessTools.Method(typeof(Pawn_AgeTracker), "TickBiologicalAge").Invoke(pawn.ageTracker, new object[] { ticks });

    /// <summary>将环境年龄速率固定为1，不替换原版进位过程或生产印记停龄判断。</summary>
    private static bool ImprintAgeRate(ref float __result) { __result = 1; return false; }

    /// <summary>仅接管Biotech这一个Mod依赖查询；其他依赖仍走原版，不复制XML条件过滤算法。</summary>
    private static bool ImprintBiotechActive(object[] __args, ref bool __result)
    {
        var mods = ((IEnumerable<string>)__args[0]).ToArray();
        if (mods.Length != 1 || !string.Equals(mods[0], "Ludeon.RimWorld.Biotech", StringComparison.OrdinalIgnoreCase)) return true;
        __result = imprintBiotech; return false;
    }

    /// <summary>验证三态真实属性运算、互斥去重及原版完全免疫查询，保留已有疾病。</summary>
    private static void ImprintEffectTests()
    {
        foreach (var sample in new[] {
            (imprintNames[0], new[] { 1.5f, 1.25f, 1.2f, 1.5f, 1.25f, 1f, 1f, 1f, 1f, 1f }),
            (imprintNames[1], new[] { 3f, 1.5f, 1.5f, 1f, 1.5f, .25f, 1.25f, 1.25f, 1f, 1f }),
            (imprintNames[2], new[] { 3f, 1.5f, 1.5f, 1f, 1.5f, 1.5f, 1.5f, 1.5f, 1.25f, 2f }) })
            Check("imprint actual native stat calculation " + sample.Item1, () =>
            {
                var pawn = ImprintPawn(); Mark(pawn, sample.Item1);
                string[] names = { "LifespanFactor", "PsychicSensitivity", "GlobalLearningFactor", "LearningRateFactor", "ImmunityGainSpeed", "Vulnerability", "SexFrequency", "SexSatisfaction", "SSC_OrgasmFrequency", "PawnBeauty" };
                for (int i = 0; i < names.Length; i++)
                    Require(Math.Abs(sample.Item2[i] - StageStat(pawn, names[i])) < .0001f, names[i] + " expected " + sample.Item2[i] + ", got " + StageStat(pawn, names[i]));
                Mark(pawn, sample.Item1); Mark(pawn, imprintNames[(Array.IndexOf(imprintNames, sample.Item1) + 1) % 3]);
                Equal(1, pawn.health.hediffSet.hediffs.Count(h => SSCImprintUtility.IsImprintDef(h.def)));
                Near(sample.Item2[1], StageStat(pawn, "PsychicSensitivity"));
                Near(sample.Item2[9], StageStat(pawn, "PawnBeauty"));
            });
        Check("imprint immunity uses native immunity handler without deleting illness", () =>
        {
            var pawn = ImprintPawn(); var infection = DefDatabase<HediffDef>.GetNamed("WoundInfection");
            pawn.health.AddHediff(infection); Mark(pawn, imprintNames[0]);
            var immune = AccessTools.Method(typeof(ImmunityHandler), "AnyHediffMakesFullyImmuneTo");
            Require(!(bool)immune.Invoke(pawn.health.immunity, new object[] { infection, null }));
            SSCImprintUtility.Clear(pawn); Mark(pawn, imprintNames[1]);
            Require((bool)immune.Invoke(pawn.health.immunity, new object[] { infection, null }));
            Require(pawn.health.hediffSet.HasHediff(infection));
        });
    }

    /// <summary>覆盖首次资格、已有资格、性奴优先、初始禁用和空壳／人格替换期间的消耗边界。</summary>
    private static void ImprintSeedTests()
    {
        Check("imprint first seed grants qualification only; second matures and locks", () =>
        {
            var pawn = ImprintPawn(); Thing seed = Seed(); UseSeed(pawn, seed);
            Require(SSCMasterQualificationUtility.HasQualification(pawn)); Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
            Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(pawn)); Equal(30, pawn.ageTracker.AgeBiologicalYears); Equal(2, seed.stackCount);
            UseSeed(pawn, seed); Equal(1, seed.stackCount);
            Equal(SSCImprintKind.Master, SSCImprintUtility.GetKind(pawn)); Equal(PawnIdentity.Master, SSCIdentityUtility.GetIdentity(pawn));
            Equal(25, pawn.ageTracker.AgeBiologicalYears);
            Require(!SSCMasterQualificationUtility.TryUseQualificationItem(pawn, seed, out _)); Equal(1, seed.stackCount);
            Require(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset)); Require(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave));
            Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
        });
        Check("imprint old qualification needs one seed and current slave branch wins", () =>
        {
            var pawn = ImprintPawn(); pawn.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
            Thing seed = Seed(); UseSeed(pawn, seed); Equal(SSCImprintKind.Master, SSCImprintUtility.GetKind(pawn)); Equal(2, seed.stackCount);
            var slave = ImprintPawn(PawnIdentity.Slave); slave.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
            UseSeed(slave, seed); Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(slave)); Equal(1, seed.stackCount);
            Require(SSCMasterQualificationUtility.HasQualification(slave));
        });
        Check("imprint slave seed does not grant master qualification or alter binding", () =>
        {
            var master = ImprintPawn(PawnIdentity.Master); var slave = ImprintPawn(PawnIdentity.Slave);
            Require(SSCBondUtility.Bind(master, slave)); UseSeed(slave, Seed());
            Require(SSCBondUtility.IsBoundTo(slave, master)); Require(!SSCMasterQualificationUtility.HasQualification(slave));
            Require(SSCBondUtility.Unbind(slave)); Equal(PawnIdentity.Slave, SSCImprintUtility.GetImprintLockedIdentity(slave));
            var other = ImprintPawn(PawnIdentity.Master); Require(SSCBondUtility.Bind(other, slave));
            Require(!SSCIdentityUtility.TrySetIdentity(slave, PawnIdentity.Unset));
        });
        Check("imprint born adult can select master but seed and selection cannot differentiate", () =>
        {
            var pawn = ImprintPawn(); Require(SSCImprintUtility.TryGrantInitial(pawn));
            Require(SSCMasterQualificationUtility.CanSelectMaster(pawn)); Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(pawn));
            Thing seed = Seed(); Require(!SSCMasterQualificationUtility.CanUseQualificationItem(pawn, seed).Accepted);
            Require(!SSCMasterQualificationUtility.TryUseQualificationItem(pawn, seed, out _)); Equal(3, seed.stackCount);
            pawn.def.race.lifeStageAges[0].def.developmentalStage = DevelopmentalStage.Child;
            Require(!SSCMasterQualificationUtility.CanSelectMaster(pawn));
        });
        Check("imprint seed refuses hollow and personality mutation without consumption", () =>
        {
            var pawn = ImprintPawn(); Thing seed = Seed();
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope));
            using (scope) Require(!SSCMasterQualificationUtility.TryUseQualificationItem(pawn, seed, out _));
            pawn.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            Require(!SSCMasterQualificationUtility.TryUseQualificationItem(pawn, seed, out _)); Equal(3, seed.stackCount);
        });
    }

    /// <summary>执行真实双向绑定，区分成功新绑定与失败、旧链恢复、同关系重放及反向索引修复。</summary>
    private static void ImprintBindingTests()
    {
        Check("imprint only successful new two-way binding differentiates both personalities", () =>
        {
            var master = ImprintPawn(); var slave = ImprintPawn(); SSCImprintUtility.TryGrantInitial(master); SSCImprintUtility.TryGrantInitial(slave);
            Require(!SSCBondUtility.Bind(master, slave)); Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(slave));
            Require(SSCIdentityUtility.TrySetIdentity(master, PawnIdentity.Master)); Require(SSCBondUtility.Bind(master, slave));
            Equal(SSCImprintKind.Master, SSCImprintUtility.GetKind(master)); Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(slave));
            Require(SSCMasterQualificationUtility.HasQualification(master)); Require(SSCBondUtility.Unbind(slave));
            Require(SSCIdentityUtility.IsIdentityLocked(master)); Require(SSCIdentityUtility.IsIdentityLocked(slave));
        });
        Check("imprint restored old links and same-pair repair do not differentiate", () =>
        {
            var master = ImprintPawn(PawnIdentity.Master); var slave = ImprintPawn(PawnIdentity.Slave);
            SSCImprintUtility.TryGrantInitial(master); SSCImprintUtility.TryGrantInitial(slave);
            Require(SSCBondUtility.RestorePersonalityBinding(master, slave)); SSCBondUtility.RepairReciprocalLink(slave);
            Require(SSCBondUtility.Bind(master, slave));
            Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(master)); Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(slave));
        });
    }

    /// <summary>验证即时回春、25岁截断、每日超龄纠正、移出后恢复增长及初始态不干预年龄。</summary>
    private static void ImprintAgeTests()
    {
        Check("imprint mature age clamps immediately and stops native biological advancement", () =>
        {
            var pawn = ImprintPawn(PawnIdentity.Master, 70); long chronological = pawn.ageTracker.AgeChronologicalTicks;
            Mark(pawn, imprintNames[1]); Equal(SSCImprintUtility.YoungAgeTicks, pawn.ageTracker.AgeBiologicalTicks);
            AgeStep(pawn, 60000); Equal(SSCImprintUtility.YoungAgeTicks, pawn.ageTracker.AgeBiologicalTicks);
            Equal(chronological, pawn.ageTracker.AgeChronologicalTicks);
            pawn.ageTracker.AgeBiologicalTicks = 40L * GenDate.TicksPerYear;
            AgeStep(pawn, 10); Equal(40L * GenDate.TicksPerYear, pawn.ageTracker.AgeBiologicalTicks);
            SSCImprintUtility.Find(pawn).TickInterval(GenDate.TicksPerDay);
            Equal(SSCImprintUtility.YoungAgeTicks, pawn.ageTracker.AgeBiologicalTicks);
            SSCImprintUtility.Clear(pawn); AgeStep(pawn, 10); Equal(SSCImprintUtility.YoungAgeTicks + 10, pawn.ageTracker.AgeBiologicalTicks);
        });
        Check("imprint mature younger body grows to boundary; initial ages normally", () =>
        {
            var pawn = ImprintPawn(PawnIdentity.Slave, 20); Mark(pawn, imprintNames[2]); AgeStep(pawn, 10);
            Equal(20L * GenDate.TicksPerYear + 10, pawn.ageTracker.AgeBiologicalTicks);
            pawn.ageTracker.AgeBiologicalTicks = SSCImprintUtility.YoungAgeTicks - 2; AgeStep(pawn, 10);
            Equal(SSCImprintUtility.YoungAgeTicks, pawn.ageTracker.AgeBiologicalTicks); Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(pawn));
            var initial = ImprintPawn(age: 70); Mark(initial, imprintNames[0]); AgeStep(initial, 10);
            Equal(70L * GenDate.TicksPerYear + 10, initial.ageTracker.AgeBiologicalTicks);
        });
    }

    /// <summary>执行健康与凝胶原生Scribe、载荷复制、完整植入和坏快照预检，不模拟迁移算法。</summary>
    private static void ImprintPersonalityTests()
    {
        Check("imprint native Scribe roundtrip preserves all three forms without age conversion", () =>
        {
            foreach (string name in imprintNames)
            { var pawn = ImprintPawn(); Mark(pawn, name); var loaded = Load(Save(pawn)); Equal(name, SSCImprintUtility.Find(loaded)?.def.defName); }
        });
        Check("imprint snapshot copy and gel status preserve exact form; old payload stays empty", () =>
        {
            var pawn = ImprintPawn(PawnIdentity.Slave); Mark(pawn, imprintNames[2]);
            var data = new CompPersonalityStore(); data.StorePawnData(pawn);
            Require(data.hediffTags.ContainsKey(DefDatabase<HediffDef>.GetNamed(imprintNames[2])));
            var copy = new CompPersonalityStore(); copy.CopyFrom(data);
            Require(!ReferenceEquals(copy.hediffTags, data.hediffTags));
            // 先让加工副本经过实际Comp存读档，再检查形态与锁，避免只验证内存字典恰好相同。
            var fixture = new ImprintGelFixture { Data = copy };
            string path = Path.Combine(saveDirectory, "imprint-gel.xml");
            Scribe.saver.InitSaving(path, "ImprintGelFixture"); Scribe_Deep.Look(ref fixture, "gel"); Scribe.saver.FinalizeSaving();
            fixture = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture, "gel"); Scribe.loader.FinalizeLoading();
            copy = fixture.Data;
            Require(SSCImprintUtility.GelLabel(copy).Contains(DefDatabase<HediffDef>.GetNamed(imprintNames[2]).LabelCap));
            var host = ImprintPawn(PawnIdentity.Slave, 70); Require(SSCImprintUtility.CanRestore(host, copy));
            Require(SSCImprintUtility.Restore(host, copy)); Equal(25, host.ageTracker.AgeBiologicalYears);
            SSCImprintUtility.Clear(host); Require(SSCImprintUtility.Restore(host, new CompPersonalityStore())); Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(host));
        });
        Check("imprint full personality replacement clears host lock and precisely restores source", () =>
        {
            var source = ImprintPawn(PawnIdentity.Slave); Mark(source, imprintNames[2]);
            var data = new CompPersonalityStore(); data.StorePawnData(source);
            var host = ImprintPawn(PawnIdentity.Master, 70); Mark(host, imprintNames[1]);
            Require(ExcretionUtility.InheritEverything(host, data));
            Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(host)); Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(host));
            var empty = new CompPersonalityStore(); empty.StorePawnData(ImprintPawn());
            Require(ExcretionUtility.InheritEverything(host, empty)); Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(host));
            Require(SSCIdentityUtility.TrySetIdentity(host, PawnIdentity.Slave));
        });
        Check("imprint personality cleanup preserves the body master-side bonds", () =>
        {
            var host = ImprintPawn(PawnIdentity.Master); Mark(host, imprintNames[1]);
            var previousSlave = ImprintPawn(PawnIdentity.Slave);
            Require(SSCBondUtility.Bind(host, previousSlave));
            var chain = SSCBondUtility.GetChain(previousSlave);
            var bridle = SSCBondUtility.GetBridle(host);
            ExcretionUtility.CleanConsumerData(host);
            Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(host));
            Require(SSCBondUtility.GetBoundMaster(previousSlave) == host);
            Require(ReferenceEquals(chain, SSCBondUtility.GetChain(previousSlave)));
            Require(ReferenceEquals(bridle, SSCBondUtility.GetBridle(host)));
            Require(bridle.ValidTargets.Contains(previousSlave));
        });
        Check("imprint malformed migration refuses before clearing host", () =>
        {
            var host = ImprintPawn(PawnIdentity.Master); Mark(host, imprintNames[1]);
            var data = new CompPersonalityStore { sscIdentity = PawnIdentity.Slave };
            data.hediffTags[DefDatabase<HediffDef>.GetNamed(imprintNames[1])] = 1;
            Require(!SSCImprintUtility.CanRestore(host, data)); Equal(SSCImprintKind.Master, SSCImprintUtility.GetKind(host));
        });
    }
}

/// <summary>使用生产 Comp 的原始 Scribe 入口；不模拟健康标签字典的序列化。</summary>
public sealed class ImprintGelFixture : IExposable
{
    public CompPersonalityStore Data = new();
    /// <summary>将凝胶载荷完整委托给生产Comp保存与恢复，保持原有Def引用和格式版本语义。</summary>
    public void ExposeData() => Data.PostExposeData();
}
