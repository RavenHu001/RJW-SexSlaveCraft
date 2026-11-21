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
        var editedGel = SSCDefOf.SSC_PersonalitySlime_Edited;
        var hollowTrait = SSCDefOf.SSC_Trait_PE;
        var world = Current.Game.World;
        var harmony = new Harmony("ssc.tests.ownerless-personality");
        try
        {
            // 原版 Thing.ExposeData 在解析物品交叉引用时访问派系管理器。
            Current.Game.World = Bare<World>();
            Current.Game.World.factionManager = new FactionManager();
            SSCDefOf.SSC_PersonalitySlime = OwnerlessGelDef("SSC_PersonalitySlime");
            SSCDefOf.SSC_PersonalitySlime_Edited = OwnerlessGelDef("SSC_PersonalitySlime_Edited");
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
                source.health.AddHediff(def).Severity = severity;
                ownerlessExtractionSource = source;
                ownerlessGel = null;
                ExcretionUtility.DoExcretion(source);
                Require(ownerlessGel != null && !ownerlessGel.Destroyed);
                Require(Hediff_SSCOwnerlessSlave.Find(source) == null && !SSCIdentityUtility.IsIdentityLocked(source));
                Require(source.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done));
                Require(SSCOrgasmSensitivityUtility.Find(source) == null);
                Equal(severity, ownerlessGel.TryGetComp<CompPersonalityStore>().ownerlessProgress);
                var loaded = RoundTripOwnerlessGel(ownerlessGel, "ownerless-gel");
                Equal(severity, loaded.TryGetComp<CompPersonalityStore>().ownerlessProgress);

                ownerlessGel = null;
                var recipe = new Recipe_PSTagWorker { recipe = new RecipeDef_PSTag() };
                recipe.Notify_IterationCompleted(source, new List<Thing> { loaded });
                Require(loaded.Destroyed && ownerlessGel?.def == SSCDefOf.SSC_PersonalitySlime_Edited);
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
                Require(ownerlessGel != null && !ownerlessGel.Destroyed && Hediff_SSCOwnerlessSlave.Find(source) == null);
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
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ownerlessExtractionSource = null;
            ownerlessGel = null;
            duringOwnerlessPlacement = null;
            afterOwnerlessImplantAdd = null;
            SSCDefOf.SSC_PersonalitySlime = mainGel;
            SSCDefOf.SSC_PersonalitySlime_Edited = editedGel;
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
