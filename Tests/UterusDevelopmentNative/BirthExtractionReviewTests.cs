// AI 辅助生成：执行两条真实提取入口，在快照／放置边界模拟新受孕，确认不清源且撤回新产物。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn birthExtractionVictim;
    private static bool birthPregnancyAtStore;
    private static bool birthExtractionPlacementFails;
    private static bool birthExtractionNormal;
    private static readonly List<Thing> birthExtractionProducts = new();

    /// <summary>只提供空间和外部回调边界；提取准入、末端撤回及源数据保护由生产代码决定。</summary>
    private static void BirthExtractionReviewTests(Harmony harmony)
    {
        // 绑定夹具可按锁链档位选不同凝胶；补齐真实定义别名，不替换生产筛选算法。
        SSCDefOf.SSC_PS_P = DefDatabase<ThingDef>.GetNamed("SSC_PersonalitySlime_PLUS");
        SSCDefOf.SSC_PS_U = DefDatabase<ThingDef>.GetNamed("SSC_PersonalitySlime_ULTRA");
        SSCDefOf.SSC_PS_P_U = DefDatabase<ThingDef>.GetNamed("SSC_PersonalitySlime_PLUS_ULTRA");
        harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)),
            prefix: new HarmonyMethod(typeof(Program), nameof(BirthExtractionMap)) { priority = Priority.First });
        harmony.Patch(AccessTools.Method(typeof(CompPersonalityStore), nameof(CompPersonalityStore.StorePawnData)),
            prefix: new HarmonyMethod(typeof(Program), nameof(BirthExtractionStore)) { priority = Priority.First });
        harmony.Patch(AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.Spawn), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(WipeMode) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(BirthExtractionSpawn)) { priority = Priority.First });
        harmony.Patch(AccessTools.Method(typeof(rjw.SexUtility), nameof(rjw.SexUtility.ProcessSex)),
            prefix: new HarmonyMethod(typeof(Program), nameof(BirthExtractionSex)) { priority = Priority.First });

        Check("birth direct extraction aborts after snapshot callback becomes pregnant and retains source", () =>
        {
            var mother = BirthMother(.3f); var master = ImprintPawn(PawnIdentity.Master);
            Require(SSCBondUtility.Bind(master, mother));
            var preparation = AddBirthExtractionPreparation(mother);
            birthExtractionVictim = mother; birthPregnancyAtStore = true; birthExtractionProducts.Clear();
            try
            {
                ExcretionUtility.DoExcretion(mother);
                AssertBirthExtractionRejected(mother, master, preparation);
            }
            finally { birthExtractionVictim = null; }
        });
        Check("birth scene extraction aborts after placement callback becomes pregnant before stripping source", () =>
        {
            var mother = BirthMother(.3f); var master = ImprintPawn(PawnIdentity.Master);
            Require(SSCBondUtility.Bind(master, mother));
            var preparation = AddBirthExtractionPreparation(mother);
            birthExtractionVictim = mother; birthPregnancyAtStore = false; birthExtractionProducts.Clear();
            try
            {
                var driver = Bare<JobDriver_PE>(); driver.pawn = master; driver.job = new Verse.AI.Job { targetA = mother };
                AccessTools.Method(typeof(JobDriver_PE), "CompletePersonalityExtraction").Invoke(driver, null);
                AssertBirthExtractionRejected(mother, master, preparation);
            }
            finally { birthExtractionVictim = null; }
        });
        Check("birth scene extraction retains source when native placement returns no product", () =>
        {
            var mother = BirthMother(.3f); var master = ImprintPawn(PawnIdentity.Master);
            Require(SSCBondUtility.Bind(master, mother));
            var preparation = AddBirthExtractionPreparation(mother);
            birthExtractionVictim = mother; birthPregnancyAtStore = false; birthExtractionPlacementFails = true; birthExtractionProducts.Clear();
            try
            {
                var driver = Bare<JobDriver_PE>(); driver.pawn = master; driver.job = new Verse.AI.Job { targetA = mother };
                AccessTools.Method(typeof(JobDriver_PE), "CompletePersonalityExtraction").Invoke(driver, null);
                Require(!SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(mother));
                Require(mother.health.hediffSet.hediffs.Contains(preparation));
                Require(!mother.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done));
                Near(.3f, mother.TryGetComp<CompSexSlaveTraining>().specializationProgress);
                Require(SSCBondUtility.IsBoundTo(mother, master));
                Equal(1, birthExtractionProducts.Count); Require(birthExtractionProducts[0].Destroyed);
            }
            finally { birthExtractionVictim = null; birthExtractionPlacementFails = false; }
        });
        Check("birth scene extraction still commits a valid source after successful placement", () =>
        {
            var mother = BirthMother(.3f); var owner = SSCUterusDevelopmentUtility.Find(mother);
            Mark(mother, SSCImprintUtility.SlaveDefName);
            AddBirthExtractionPreparation(mother);
            SSCDefOf.SSC_Trait_PE ??= new TraitDef { defName = "SSC_Trait_PE", degreeDatas = new() { new TraitDegreeData { degree = 0, label = "hollow" } } };
            birthExtractionVictim = mother; birthExtractionNormal = true; birthExtractionProducts.Clear();
            try
            {
                var driver = Bare<JobDriver_PE>(); driver.pawn = ImprintPawn(); driver.job = new Verse.AI.Job { targetA = mother };
                AccessTools.Method(typeof(JobDriver_PE), "CompletePersonalityExtraction").Invoke(driver, null);
                Require(mother.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done));
                Require(!mother.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreting));
                Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(mother));
                Equal(SexSlaveSpecializationType.None, mother.TryGetComp<CompSexSlaveTraining>().specializationType);
                Require(ReferenceEquals(owner, SSCUterusDevelopmentUtility.Find(mother)) && owner.OffspringImprintEnabled);
                Equal(1, birthExtractionProducts.Count); Require(!birthExtractionProducts[0].Destroyed);
                Require(birthExtractionProducts[0].TryGetComp<CompPersonalityStore>().hediffTags.Keys.Any(SSCImprintUtility.IsImprintDef));
            }
            finally { birthExtractionVictim = null; birthExtractionNormal = false; }
        });
    }

    /// <summary>使用真实准备Hediff，不以属性替身复制其孕期保留规则。</summary>
    private static Hediff AddBirthExtractionPreparation(Pawn pawn)
    {
        var preparation = new Hediff_SSC_PersonalityExcreting { def = SSCDefOf.SSC_PersonalityExcreting, pawn = pawn };
        pawn.health.AddHediff(preparation); preparation.Severity = 1;
        return preparation;
    }

    /// <summary>验证失败仍保有原人格、准备、培养和绑定；本次新快照不得成为重复人格产物。</summary>
    private static void AssertBirthExtractionRejected(Pawn mother, Pawn master, Hediff preparation)
    {
        Require(SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(mother));
        Require(mother.health.hediffSet.hediffs.Contains(preparation)); Near(1, preparation.Severity);
        Require(!mother.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done));
        Near(.3f, mother.TryGetComp<CompSexSlaveTraining>().specializationProgress);
        Require(SSCBondUtility.IsBoundTo(mother, master));
        Equal(1, birthExtractionProducts.Count); Require(birthExtractionProducts[0].Destroyed);
    }

    /// <summary>只为当前提取目标提供地图入口，其余实例继续原地图查询。</summary>
    private static bool BirthExtractionMap(Thing __instance, ref Map __result)
    {
        if (__instance != birthExtractionVictim) return true;
        __result = Bare<Map>(); return false;
    }

    /// <summary>快照的外部采集回调模拟受孕；不替代之后的生产准入或清源算法。</summary>
    private static bool BirthExtractionStore(CompPersonalityStore __instance, Pawn __0)
    {
        if (__0 != birthExtractionVictim) return true;
        birthExtractionProducts.Add(__instance.parent);
        if (birthExtractionNormal) return true;
        if (birthPregnancyAtStore) NewPregnancy(__0, "PregnantHuman");
        return false;
    }

    /// <summary>不执行地图放置，模拟其同步回调新增妊娠；之后生产代码必须撤回本次产物。</summary>
    private static bool BirthExtractionSpawn(Thing __0, ref Thing __result)
    {
        if (birthExtractionVictim == null) return true;
        if (birthExtractionPlacementFails) { __result = null; return false; }
        if (!birthPregnancyAtStore && !birthExtractionNormal) NewPregnancy(birthExtractionVictim, "PregnantHuman");
        __result = __0; return false;
    }

    /// <summary>不启动完整RJW结算，本项只验证结算后创建／放置产物阶段的末端保护。</summary>
    private static bool BirthExtractionSex() => birthExtractionVictim == null;
}
