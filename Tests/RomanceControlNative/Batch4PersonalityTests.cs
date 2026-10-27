// AI 辅助生成：完整生产人格入口与真实命令层联测；仅隔离需要完整地图的引擎通知。
using HarmonyLib;
using rjw;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static Thing transferProduct;
    private static ThingDef transferGel, transferEditedGel;
    private static bool transferSpawnFails;
    private static Action transferSpawnCallback;
    /// <summary>在真实生产植入流程中联合验证配置、原人格 token 与入站/出站命令。</summary>
    private static void Batch4PersonalityTests()
    {
        SSCDefOf.BridleOfSexSlave = new HediffDef { defName = "Hediff_BridleOfSexSlave", hediffClass = typeof(Hediff_BridleOfSexSlave) };
        DefDatabase<HediffDef>.Add(SSCDefOf.BridleOfSexSlave);
        transferGel = Bare<ThingDef>(); transferGel.defName = "RomanceTransferGel"; transferGel.category = ThingCategory.Item; transferGel.thingClass = typeof(ThingWithComps);
        transferEditedGel = Bare<ThingDef>(); transferEditedGel.defName = "RomanceTransferEditedGel"; transferEditedGel.category = ThingCategory.Item; transferEditedGel.thingClass = typeof(ThingWithComps);
        transferGel.destroyable = transferEditedGel.destroyable = true;
        DefDatabase<ThingDef>.Add(transferGel); DefDatabase<ThingDef>.Add(transferEditedGel);
        SSCDefOf.SSC_PS_P_U = transferGel; SSCDefOf.SSC_PS_P_U_Edited = transferEditedGel;
        SSCDefOf.SSC_PersonalitySlime = SSCDefOf.SSC_PS_P = SSCDefOf.SSC_PS_U = transferGel;
        SSCDefOf.SSC_PersonalitySlime_Edited = SSCDefOf.SSC_PS_P_Edited = SSCDefOf.SSC_PS_U_Edited = transferEditedGel;
        SSCDefOf.SSC_Trait_PE = new TraitDef { defName = "SexSlaveCraft_PE", degreeDatas = new() { new TraitDegreeData { degree = 0, label = "Extracted" } } };
        DefDatabase<TraitDef>.Add(SSCDefOf.SSC_Trait_PE);
        if (DefDatabase<HediffDef>.GetNamedSilentFail(SSCDefOf.SSC_PersonalityExcreted_Done.defName) == null)
            DefDatabase<HediffDef>.Add(SSCDefOf.SSC_PersonalityExcreted_Done);
        var boundary = new Harmony("ssc.tests.romance.batch4.personality-engine");
        boundary.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        boundary.Patch(AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing)), prefix: new HarmonyMethod(typeof(Program), nameof(TransferMakeThing)));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(GenSpawn)).Where(m => m.Name == "Spawn" && m.GetParameters()[0].ParameterType == typeof(Thing)))
            boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(TransferSpawn)));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(GenPlace)).Where(m => m.Name == "TryPlaceThing" && m.GetParameters()[0].ParameterType == typeof(Thing)))
            boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(TransferPlace)));
        boundary.Patch(AccessTools.Method(typeof(SexUtility), nameof(SexUtility.ProcessSex)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        boundary.Patch(AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis"), prefix: new HarmonyMethod(typeof(Program), nameof(TransferReservationCleanup)));
        var mapsField = AccessTools.Field(typeof(Game), "maps"); var previousMaps = mapsField.GetValue(Current.Game);
        mapsField.SetValue(Current.Game, new List<Map> { Bare<Map>() });
        try
        {
            Check("batch4 full production insertion preserves preference and cancels old incoming/outgoing commands", () =>
            {
                CommandReset(); var source = SocialSlave(); Comp(source).romanceConfig = Denied();
                var gel = new CompPersonalityStore(); gel.StorePawnData(source);
                var receiver = SocialSlave(); receiver.jobs = new Pawn_JobTracker(receiver);
                var outsider = SocialSlave(); outsider.jobs = new Pawn_JobTracker(outsider);
                var other = SocialPawn();
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(receiver, other), out var outgoing, out _));
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(outsider, receiver), out var incoming, out _));
                var preview = SSCRomanceCommandUtility.Capture(outsider, receiver);
                Require(ExcretionUtility.InheritEverything(receiver, gel));
                Require(Comp(receiver).romanceConfig != null && !Comp(receiver).romanceConfig.allowAcceptProposal);
                Require(Comp(receiver).romanceConfig.allowInitiateToMaster && Comp(receiver).romanceConfig.alwaysAcceptMaster);
                Require(!ReferenceEquals(Comp(receiver).romanceConfig, gel.romanceConfig));
                Equal(SSCRomanceCommandStatus.Cancelled, incoming.Status); Equal(SSCRomanceCommandStatus.Cancelled, outgoing.Status);
                Equal("StaleContext", SSCRomanceCommandUtility.Failure(preview));
                Equal(SSCBondUtility.GetBoundMaster(source), SSCBondUtility.GetBoundMaster(receiver));
                Require(!SSCPersonalityRuntimeUtility.IsMutating(receiver)); Equal(0, bubbleCalls);
            });
            foreach (bool scene in new[] { false, true })
                Check("batch4 actual extraction entry captures and clears before processed gel insertion scene=" + scene, () =>
                {
                    CommandReset(); transferProduct = null; var source = SocialSlave(); PutOnTransferMap(source);
                    Comp(source).romanceConfig = Denied(); Comp(source).milkProductionEnabled = false;
                    var outsider = SocialSlave(); outsider.jobs = new Pawn_JobTracker(outsider);
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(outsider, source), out var incoming, out _));
                    ExtractForTransfer(source, scene);
                    Require(Comp(source).romanceConfig == null); Require(transferProduct != null);
                    var captured = transferProduct.TryGetComp<CompPersonalityStore>(); Require(captured.romanceConfig.alwaysAcceptMaster);
                    Equal(SSCRomanceCommandStatus.Cancelled, incoming.Status); Require(!SSCPersonalityRuntimeUtility.IsMutating(source));
                    var rawGel = transferProduct;
                    new Recipe_PSTagWorker { recipe = new RecipeDef { defName = "RomanceTransferRecipe" } }.Notify_IterationCompleted(source, new List<Thing> { rawGel });
                    Require(transferProduct != rawGel); var processed = transferProduct.TryGetComp<CompPersonalityStore>();
                    Require(!ReferenceEquals(captured.romanceConfig, processed.romanceConfig));
                    var receiver = SocialSlave(); Require(ExcretionUtility.InheritEverything(receiver, processed));
                    Require(!Comp(receiver).romanceConfig.allowInitiateRomance && Comp(receiver).romanceConfig.allowInitiateToMaster);
                    Require(!Comp(receiver).milkProductionEnabled); Equal(0, bubbleCalls);
                });
            foreach (bool scene in new[] { false, true })
            {
                Check("batch4 placement failure retains source preference but cancels old commands scene=" + scene, () =>
                {
                    CommandReset(); var (source, other) = CommandPair(SSCRomanceAction.Romance); PutOnTransferMap(source);
                    var original = Comp(source).romanceConfig = Denied();
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(source, other), out var record, out _));
                    transferSpawnFails = true;
                    try { ExtractForTransfer(source, scene); Require(false, "placement should throw"); }
                    catch (Exception ex) when (ex is InvalidOperationException || ex.InnerException is InvalidOperationException) { }
                    finally { transferSpawnFails = false; }
                    Require(ReferenceEquals(original, Comp(source).romanceConfig)); Require(!SSCPersonalityRuntimeUtility.IsMutating(source));
                    Equal(SSCRomanceCommandStatus.Cancelled, record.Status); Equal(0, bubbleCalls);
                });
                Check("batch4 placement callback cannot clear a replacement source Comp scene=" + scene, () =>
                {
                    CommandReset(); var source = SocialSlave(); PutOnTransferMap(source); Comp(source).romanceConfig = Denied();
                    var replacement = new CompSexSlaveTraining { parent = source, pawnIdentity = PawnIdentity.Slave,
                        romanceConfig = new() { allowAcceptProposal = false } };
                    transferSpawnCallback = () => AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(source, new List<ThingComp> { replacement });
                    try { ExtractForTransfer(source, scene); }
                    finally { transferSpawnCallback = null; }
                    Require(replacement.romanceConfig != null && !replacement.romanceConfig.allowAcceptProposal);
                    Require(!SSCPersonalityRuntimeUtility.IsMutating(source));
                });
                Check("batch4 unsupported extraction rejects before mutation scene=" + scene, () =>
                {
                    CommandReset(); var (source, other) = CommandPair(SSCRomanceAction.Romance); PutOnTransferMap(source);
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(source, other), out var record, out _));
                    Comp(source).romanceConfig = Denied(); Comp(source).romanceConfig.version = 99;
                    object token = SSCPersonalityRuntimeUtility.Capture(source); transferProduct = null;
                    ExtractForTransfer(source, scene);
                    Require(transferProduct == null && Comp(source).romanceConfig.version == 99);
                    Equal(SSCRomanceCommandStatus.Pending, record.Status); Require(SSCPersonalityRuntimeUtility.IsCurrent(source, token));
                    Equal(1, errors.Count); errors.Clear();
                });
            }
            foreach (string sourceState in new[] { "default", "stage1", "unbound", "mixed" })
                Check("batch4 full insertion precisely replaces host config from " + sourceState, () =>
                {
                    CommandReset(); var source = SocialSlave();
                    Comp(source).romanceConfig = sourceState == "default" ? null : sourceState == "mixed" ? new() { allowInitiateProposal = false, alwaysAcceptMaster = true } : Denied();
                    if (sourceState == "stage1") SSCBondUtility.GetChain(source).Severity = .1f;
                    if (sourceState == "unbound") SSCBondUtility.Unbind(source);
                    var gel = new CompPersonalityStore(); gel.StorePawnData(source);
                    var receiver = SocialSlave(); Comp(receiver).romanceConfig = Denied();
                    Require(ExcretionUtility.InheritEverything(receiver, gel));
                    foreach (var setting in Enum.GetValues<SSCRomanceSetting>())
                        Equal(SSCRomancePreferences.GetView(source, setting).Saved, SSCRomancePreferences.GetView(receiver, setting).Saved);
                    Require(!SSCPersonalityRuntimeUtility.IsMutating(receiver)); Equal(0, bubbleCalls);
                });
            foreach (string change in new[] { "unbindRebind", "rebindOwner", "directOwnerTick", "severitySetter", "severityUtility" })
                Check("batch4 real bond and stage lifecycle retains preference and permanently cancels request " + change, () =>
                {
                    CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); var owner = SSCBondUtility.GetBoundMaster(a);
                    Require(SSCBondUtility.Bind(owner, a, true)); var saved = Comp(a).romanceConfig = Denied();
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
                    if (change == "unbindRebind")
                    { Require(SSCBondUtility.Unbind(a)); Require(SSCBondUtility.Bind(owner, a, true)); SSCBondUtility.GetChain(a).Severity = .9f; }
                    else if (change == "rebindOwner")
                    { Require(SSCBondUtility.Bind(Pawn(PawnIdentity.Master), a, true)); Require(SSCBondUtility.Bind(owner, a, true)); }
                    else if (change == "directOwnerTick")
                    { var chain = SSCBondUtility.GetChain(a); chain.target = Pawn(PawnIdentity.Master); chain.Tick(); chain.target = owner; chain.Tick(); }
                    else if (change == "severitySetter")
                    { SSCBondUtility.GetChain(a).Severity = .5f; SSCBondUtility.GetChain(a).Severity = .9f; }
                    else
                    { Hediff_ChainOfSexSlave.IncreaseChainSeverity(a, -.2f); Hediff_ChainOfSexSlave.IncreaseChainSeverity(a, .2f); }
                    Equal(SSCRomanceCommandStatus.Cancelled, record.Status); Require(ReferenceEquals(saved, Comp(a).romanceConfig));
                    captureEnd = true; ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce(); Equal(0, bubbleCalls);
                });
            Batch4SceneSaveTests();
        }
        finally { mapsField.SetValue(Current.Game, previousMaps); boundary.UnpatchAll(boundary.Id); }
    }
    /// <summary>只提供生成实例的引擎边界，采集、清源、加工和恢复仍走完整生产方法。</summary>
    private static bool TransferMakeThing(ThingDef __0, ref Thing __result)
    {
        if (__0 != transferGel && __0 != transferEditedGel) return true;
        var item = new ThingWithComps { def = __0, thingIDNumber = ++id };
        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(item,
            new List<ThingComp> { new CompPersonalityStore { parent = item, props = new CompProperties_PersonalityStore() } });
        __result = item; return false;
    }
    /// <summary>截获真实生成调用的物品或注入放置异常；宿主不模拟地图网格。</summary>
    private static bool TransferSpawn(Thing __0, ref Thing __result)
    {
        if (__0?.def != transferGel && __0?.def != transferEditedGel) return true;
        if (transferSpawnFails) throw new InvalidOperationException("expected transfer placement failure");
        transferProduct = __result = __0;
        var callback = transferSpawnCallback; transferSpawnCallback = null; callback?.Invoke(); return false;
    }
    /// <summary>加工产物同样只隔离地图放置；保留真实配方的数据复制和标签处理。</summary>
    private static bool TransferPlace(Thing __0, ref bool __result)
    {
        if (__0?.def != transferGel && __0?.def != transferEditedGel) return true;
        transferProduct = __0; __result = true; return false;
    }
    /// <summary>隔离测试物品销毁时的全地图预约扫描；真实销毁状态与凝胶消费仍由原版处理。</summary>
    private static bool TransferReservationCleanup(Thing __instance)
        => __instance.def != transferGel && __instance.def != transferEditedGel;
    /// <summary>地图槽仅让真实入口通过位置前提；物品生成由上面的显式边界收集。</summary>
    private static void PutOnTransferMap(Pawn pawn)
        => AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
    /// <summary>直接调用两条真实完成入口，RJW 场景结算下游在本专项中隔离。</summary>
    private static void ExtractForTransfer(Pawn source, bool scene)
    {
        if (!scene) { ExcretionUtility.DoExcretion(source); return; }
        var driver = new JobDriver_PE { pawn = SocialPawn(), job = new Job { targetA = source } };
        AccessTools.Method(typeof(JobDriver_PE), "CompletePersonalityExtraction").Invoke(driver, null);
    }
}
