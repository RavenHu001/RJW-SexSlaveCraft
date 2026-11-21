// AI 辅助生成：执行真实提取、道具 Scribe、配方复制和植入，隔离地图放置及场景表现。
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn ownerlessExtractionSource;
    private static Thing ownerlessGel;
    private static Action<Hediff_SSCOwnerlessSlave> duringOwnerlessPlacement;
    private static Action<Hediff_SSCOwnerlessSlave> afterOwnerlessImplantAdd;

    private static void OwnerlessPersonalityTests()
    {
        var def = DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName);
        var mainGel = SSCDefOf.SSC_PersonalitySlime;
        var initialGel = SSCDefOf.SSC_PS_P;
        var advancedGel = SSCDefOf.SSC_PS_U;
        var editedAdvancedGel = SSCDefOf.SSC_PS_U_Edited;
        var perfectGel = SSCDefOf.SSC_PS_P_U;
        var hollowTrait = SSCDefOf.SSC_Trait_PE;
        var world = Current.Game.World;
        var harmony = new Harmony("ssc.tests.ownerless-personality");
        try
        {
            // 原版 Thing.ExposeData 在解析物品交叉引用时访问派系管理器。
            Current.Game.World = Bare<World>();
            Current.Game.World.factionManager = new FactionManager();
            SSCDefOf.SSC_PersonalitySlime = OwnerlessGelDef("SSC_PersonalitySlime");
            SSCDefOf.SSC_PS_P = OwnerlessGelDef("SSC_PersonalitySlime_PLUS");
            SSCDefOf.SSC_PS_U = OwnerlessGelDef("SSC_PersonalitySlime_ULTRA");
            SSCDefOf.SSC_PS_U_Edited = OwnerlessGelDef("SSC_PersonalitySlime_ULTRA_Edited");
            SSCDefOf.SSC_PS_P_U = OwnerlessGelDef("SSC_PersonalitySlime_PLUS_ULTRA");
            SSCDefOf.SSC_Trait_PE ??= new TraitDef { defName = "SSC_Trait_PE", degreeDatas = new() { new TraitDegreeData { degree = 0, label = "hollow" } } };
            harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)),
                prefix: new HarmonyMethod(typeof(Program), nameof(OwnerlessGelMap)));
            harmony.Patch(AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.Spawn), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(WipeMode) }),
                prefix: new HarmonyMethod(typeof(Program), nameof(OwnerlessGelSpawn)));
            harmony.Patch(AccessTools.GetDeclaredMethods(typeof(GenPlace)).Single(m => m.Name == nameof(GenPlace.TryPlaceThing) && m.GetParameters().Length == 9),
                prefix: new HarmonyMethod(typeof(Program), nameof(OwnerlessGelPlace)));
            harmony.Patch(AccessTools.Method(typeof(Hediff_SSCOwnerlessSlave), nameof(Hediff_SSCOwnerlessSlave.PostAdd)),
                postfix: new HarmonyMethod(typeof(Program), nameof(OwnerlessImplantAdded)));
            harmony.Patch(AccessTools.Method(typeof(rjw.SexUtility), nameof(rjw.SexUtility.ProcessSex)),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));

            Check("ownerless exact progress survives extraction, physical gel Scribe, recipe copy and insertion", () =>
            {
                const float severity = .8123457f;
                var source = ImprintPawn(PawnIdentity.Slave);
                var progress = source.health.AddHediff(def);
                Require(PersonalityGelUtility.GetPersonalityGelDefForPawn(source) == SSCDefOf.SSC_PersonalitySlime);
                progress.Severity = .3f;
                Require(PersonalityGelUtility.GetPersonalityGelDefForPawn(source) == SSCDefOf.SSC_PS_P);
                progress.Severity = severity;
                ownerlessExtractionSource = source;
                ownerlessGel = null;
                ExcretionUtility.DoExcretion(source);
                // 81.23457% 仍是三阶无主，使用已有高级凝胶及其加工版。
                Require(ownerlessGel?.def == SSCDefOf.SSC_PS_U && !ownerlessGel.Destroyed);
                Require(Hediff_SSCOwnerlessSlave.Find(source) == null && !SSCIdentityUtility.IsIdentityLocked(source));
                Require(source.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done));
                Require(SSCOrgasmSensitivityUtility.Find(source) == null);
                Equal(severity, ownerlessGel.TryGetComp<CompPersonalityStore>().ownerlessProgress);
                var loaded = RoundTripOwnerlessGel(ownerlessGel, "ownerless-gel");
                Equal(severity, loaded.TryGetComp<CompPersonalityStore>().ownerlessProgress);

                ownerlessGel = null;
                var recipe = new Recipe_PSTagWorker { recipe = new RecipeDef_PSTag() };
                recipe.Notify_IterationCompleted(source, new List<Thing> { loaded });
                Require(loaded.Destroyed && ownerlessGel?.def == SSCDefOf.SSC_PS_U_Edited);
                Equal(severity, ownerlessGel.TryGetComp<CompPersonalityStore>().ownerlessProgress);

                var host = ImprintPawn(PawnIdentity.Slave);
                var old = host.health.AddHediff(def);
                old.Severity = .9732119f;
                var processed = RoundTripOwnerlessGel(ownerlessGel, "ownerless-processed-gel");
                Require(ExcretionUtility.InheritEverything(host, processed.TryGetComp<CompPersonalityStore>()));
                Require(processed.Destroyed && !host.health.hediffSet.hediffs.Contains(old));
                Require(SSCBondUtility.GetChain(host) == null && SSCIdentityUtility.IsIdentityLocked(host));
                Equal(3, Hediff_SSCOwnerlessSlave.GetActiveStage(host));
                Equal(severity, Hediff_SSCOwnerlessSlave.Find(host).Severity);
                Equal(3, SSCOrgasmSensitivityUtility.GetState(host).EffectiveLevel);

                // 缺字段的旧凝胶应先清掉宿主旧锁，再恢复其无阶段身份。
                var empty = ThingMaker.MakeThing(SSCDefOf.SSC_PersonalitySlime);
                empty.TryGetComp<CompPersonalityStore>().StorePawnData(ImprintPawn());
                var legacy = RoundTripOwnerlessGel(empty, "ownerless-legacy-gel");
                Equal(CompPersonalityStore.NoOwnerlessProgress, legacy.TryGetComp<CompPersonalityStore>().ownerlessProgress);
                Require(ExcretionUtility.InheritEverything(host, legacy.TryGetComp<CompPersonalityStore>()));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(host));
                Require(Hediff_SSCOwnerlessSlave.Find(host) == null && !SSCIdentityUtility.IsIdentityLocked(host));
            });

            Check("ownerless scene withdrawal keeps a source advanced by placement and retry transfers the new severity", () =>
            {
                const float latest = .9376543f;
                var source = ImprintPawn(PawnIdentity.Slave);
                var original = source.health.AddHediff(def);
                original.Severity = .8123457f;
                ownerlessExtractionSource = source;
                duringOwnerlessPlacement = progress => progress.Severity = latest;
                var driver = Bare<JobDriver_PE>();
                driver.pawn = ImprintPawn();
                driver.job = new Verse.AI.Job { targetA = source };
                var complete = AccessTools.Method(typeof(JobDriver_PE), "CompletePersonalityExtraction");
                complete.Invoke(driver, null);
                Require(ownerlessGel?.Destroyed == true && Hediff_SSCOwnerlessSlave.Find(source) == original);
                Equal(latest, original.Severity);
                Require(!source.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done));

                duringOwnerlessPlacement = null;
                complete.Invoke(driver, null);
                Require(ownerlessGel?.def == SSCDefOf.SSC_PS_P_U && !ownerlessGel.Destroyed && Hediff_SSCOwnerlessSlave.Find(source) == null);
                Equal(latest, ownerlessGel.TryGetComp<CompPersonalityStore>().ownerlessProgress);
            });

            Check("ownerless insertion withdrawal keeps the gel and restores the host retry gate", () =>
            {
                const float severity = .8123457f;
                var source = ImprintPawn(PawnIdentity.Slave);
                source.health.AddHediff(def).Severity = severity;
                var gel = ThingMaker.MakeThing(SSCDefOf.SSC_PersonalitySlime);
                var store = gel.TryGetComp<CompPersonalityStore>();
                store.StorePawnData(source);
                var host = ImprintPawn();
                var hollow = host.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
                // 模拟健康兼容补丁在挂载期间改写了同一条记录，普通植入入口应拒绝这次提交。
                afterOwnerlessImplantAdd = progress => progress.Severity = .9f;
                try { Require(!ExcretionUtility.InheritEverything(host, store)); }
                finally { afterOwnerlessImplantAdd = null; }

                Require(!gel.Destroyed && Hediff_SSCOwnerlessSlave.Find(host) == null);
                Require(host.health.hediffSet.hediffs.Contains(hollow) && SSCOrgasmSensitivityUtility.Find(host) == null);
                Equal(severity, store.ownerlessProgress);
                Require(ExcretionUtility.InheritEverything(host, store));
                Require(gel.Destroyed && !SSCMasterProgressionUtility.IsHollow(host));
                Equal(severity, Hediff_SSCOwnerlessSlave.Find(host).Severity);
            });

            Check("bond-release countdown survives gel extraction, Scribe, processing and implantation without restarting", () =>
            {
                const float severity = .8123457f;
                int oldTick = Hediff_SSCBondReleaseAftereffect.Now;
                try
                {
                    SetBreakdownTick(1000);
                    var master = ImprintPawn(PawnIdentity.Master);
                    var source = ImprintPawn(PawnIdentity.Slave);
                    Require(SSCBondUtility.Bind(master, source));
                    SSCBondUtility.GetChain(source).Severity = severity;
                    Require(SSCBondUtility.TryReleaseToOwnerless(new SSCBondUtility.ReleaseContext(source)));
                    SetBreakdownTick(48000);
                    ownerlessExtractionSource = source;
                    ExcretionUtility.DoExcretion(source);
                    Require(!ownerlessGel.Destroyed && Hediff_SSCBondReleaseAftereffect.Find(source) == null);
                    var stored = RoundTripOwnerlessGel(ownerlessGel, "bond-release-extracted");
                    Equal(121000L, stored.TryGetComp<CompPersonalityStore>().bondReleaseExpiresAt);
                    var recipe = new Recipe_PSTagWorker { recipe = new RecipeDef_PSTag() };
                    recipe.Notify_IterationCompleted(source, new List<Thing> { stored });
                    var processed = RoundTripOwnerlessGel(ownerlessGel, "bond-release-processed");
                    var expiredCopy = RoundTripOwnerlessGel(ownerlessGel, "bond-release-expired");
                    var host = ImprintPawn();
                    SetBreakdownTick(80000);
                    Require(ExcretionUtility.InheritEverything(host, processed.TryGetComp<CompPersonalityStore>()));
                    Equal(121000L, Hediff_SSCBondReleaseAftereffect.ActiveDeadline(host));
                    Equal(severity, Hediff_SSCOwnerlessSlave.Find(host).Severity);
                    SetBreakdownTick(121000);
                    Require(ExcretionUtility.InheritEverything(host, expiredCopy.TryGetComp<CompPersonalityStore>()));
                    Require(Hediff_SSCBondReleaseAftereffect.Find(host) == null);
                    Equal(severity, Hediff_SSCOwnerlessSlave.Find(host).Severity);
                }
                finally { SetBreakdownTick(oldTick); }
            });

            Check("implantation decides saved ownership using the current master and leaves no second bond on the hollow source", () =>
            {
                int oldTick = Hediff_SSCBondReleaseAftereffect.Now;
                try
                {
                    SetBreakdownTick(23000);
                    var master = ImprintPawn(PawnIdentity.Master);
                    var source = ImprintPawn(PawnIdentity.Slave);
                    Require(SSCBondUtility.Bind(master, source));
                    SSCBondUtility.GetChain(source).Severity = .8123457f;
                    ownerlessExtractionSource = source;
                    ExcretionUtility.DoExcretion(source);
                    var original = ownerlessGel.TryGetComp<CompPersonalityStore>();
                    Require(original.chainHediffData.masterPawn == master && SSCBondUtility.GetChain(source) == null);
                    Require(Hediff_SSCOwnerlessSlave.Find(source) == null && Hediff_SSCBondReleaseAftereffect.Find(source) == null);
                    var recovered = ThingMaker.MakeThing(SSCDefOf.SSC_PS_U);
                    var recoveredData = recovered.TryGetComp<CompPersonalityStore>();
                    recoveredData.CopyFrom(original);
                    master.health.SetDead();
                    var unboundHost = ImprintPawn();
                    Require(ExcretionUtility.InheritEverything(unboundHost, original));
                    Equal(.8123457f, Hediff_SSCOwnerlessSlave.Find(unboundHost).Severity);
                    Equal(143000L, Hediff_SSCBondReleaseAftereffect.ActiveDeadline(unboundHost));
                    AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(master.health, PawnHealthState.Mobile);
                    var boundHost = ImprintPawn();
                    Require(ExcretionUtility.InheritEverything(boundHost, recoveredData));
                    Require(SSCBondUtility.IsBoundTo(boundHost, master));
                    Equal(.8123457f, SSCBondUtility.GetChain(boundHost).Severity);
                    Require(Hediff_SSCOwnerlessSlave.Find(boundHost) == null && Hediff_SSCBondReleaseAftereffect.Find(boundHost) == null);
                }
                finally { SetBreakdownTick(oldTick); }
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ownerlessExtractionSource = null;
            ownerlessGel = null;
            duringOwnerlessPlacement = null;
            afterOwnerlessImplantAdd = null;
            SSCDefOf.SSC_PersonalitySlime = mainGel;
            SSCDefOf.SSC_PS_P = initialGel;
            SSCDefOf.SSC_PS_U = advancedGel;
            SSCDefOf.SSC_PS_U_Edited = editedAdvancedGel;
            SSCDefOf.SSC_PS_P_U = perfectGel;
            SSCDefOf.SSC_Trait_PE = hollowTrait;
            Current.Game.World = world;
        }
    }

    private static ThingDef OwnerlessGelDef(string name)
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
        if (def != null) return def;
        def = Bare<ThingDef>();
        def.defName = name;
        def.category = ThingCategory.Item;
        def.thingClass = typeof(ThingWithComps);
        def.stackLimit = 1;
        def.destroyable = true;
        def.comps = new() { new CompProperties_PersonalityStore() };
        DefDatabase<ThingDef>.Add(def);
        return def;
    }

    private static Thing RoundTripOwnerlessGel(Thing gel, string name)
    {
        string path = Path.Combine(saveDirectory, name + ".xml");
        Scribe.saver.InitSaving(path, "Gel");
        Scribe_Deep.Look(ref gel, "gel");
        Scribe.saver.FinalizeSaving();
        gel = null;
        Scribe.loader.InitLoading(path);
        Scribe_Deep.Look(ref gel, "gel");
        Scribe.loader.FinalizeLoading();
        return gel;
    }

    private static bool OwnerlessGelMap(Thing __instance, ref Map __result)
    {
        if (__instance != ownerlessExtractionSource) return true;
        __result = Bare<Map>();
        return false;
    }

    private static bool OwnerlessGelSpawn(Thing __0, ref Thing __result)
    {
        if (__0.TryGetComp<CompPersonalityStore>() == null) return true;
        duringOwnerlessPlacement?.Invoke(Hediff_SSCOwnerlessSlave.Find(ownerlessExtractionSource));
        ownerlessGel = __result = __0;
        return false;
    }

    private static bool OwnerlessGelPlace(Thing __0, ref Thing __4, ref bool __result)
    {
        if (__0.TryGetComp<CompPersonalityStore>() == null) return true;
        ownerlessGel = __4 = __0;
        __result = true;
        return false;
    }

    private static void OwnerlessImplantAdded(Hediff_SSCOwnerlessSlave __instance)
        => afterOwnerlessImplantAdd?.Invoke(__instance);
}
