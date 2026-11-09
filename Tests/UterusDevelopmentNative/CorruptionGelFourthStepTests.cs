// AI 辅助生成：实际Orgasm、摄入、需求写入和RJW Scribe；只隔离整场声效及世界服务。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static Action gelAfterBoost, gelAfterCorruptionWrite;
    private static Action<JobDriver_Sex> gelLoadSetup;

    private static void GelFourthStepTests(string repo, Harmony harmony)
    {
        var previous = SSCMod.settings; SSCMod.settings = new SSCSettings();
        SSCDefOf.SSC_PersonalityExcreted_Done ??= DefDatabase<HediffDef>.GetNamed("Hediff_SSC_PersonalityExcreted_Done");
        var sideEffects = new[] { AccessTools.Method(typeof(JobDriver_Sex), "PlayCumSound"),
            AccessTools.Method(typeof(JobDriver_Sex), "PlayOrgasmVoice"), AccessTools.Method(typeof(JobDriver_Sex), "CalculateSatisfactionPerTick"),
            AccessTools.Method(typeof(SexUtility), "SatisfyPersonal") };
        foreach (var method in sideEffects) harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(sideEffects[0], prefix: new HarmonyMethod(typeof(Program), nameof(GelOrgasmSideEffect)));
        var boost = AccessTools.Method(typeof(HediffComp_SSCCorruptionGelProduction), "ApplySceneBoost");
        var write = AccessTools.Method(typeof(Need_Corruption), nameof(Need_Corruption.SetCorruption));
        var baseExpose = AccessTools.Method(typeof(JobDriver), nameof(JobDriver.ExposeData));
        bool skipped = Harmony.GetPatchInfo(baseExpose)?.Prefixes.Any(p => p.PatchMethod == AccessTools.Method(typeof(Program), nameof(Skip))) == true;
        if (!skipped) harmony.Patch(baseExpose, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        var lookup = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly));
        // 同一映射已由早期真实存档分区安装时只借用，不在结束后撤掉原边界。
        bool hadRjwTypeBoundary = Harmony.GetPatchInfo(lookup)?.Prefixes.Any(p => p.PatchMethod == AccessTools.Method(typeof(Program), nameof(RjwScribeTypeBoundary))) == true;
        if (!hadRjwTypeBoundary) harmony.Patch(lookup, prefix: new HarmonyMethod(typeof(Program), nameof(RjwScribeTypeBoundary)));
        harmony.Patch(boost, postfix: new HarmonyMethod(typeof(Program), nameof(GelAfterBoostCallback)));
        harmony.Patch(write, postfix: new HarmonyMethod(typeof(Program), nameof(GelAfterCorruptionCallback)));
        try
        {
            Check("gel ordinary consumer gains only from its own successful real orgasm", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                var partner = GelConsumer(); var other = GelUseNeed(partner); GelDose(partner);
                d.PartnerPawn = partner; d.Sexprops.partner = partner; d.job.targetA = partner;
                d.Orgasm(); Near(0, need.CurLevel); GelFire(d); Near(.02f, need.CurLevel); Near(0, other.CurLevel);
                Require(p.TryGetComp<CompSexSlaveTraining>() == null && GelComp(p) == null);
                Near(.02f, need.HighestCorruptionLevel); Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(p));
            });
            Check("gel producer boost and repeated consumption have independent eligibility and quotas", () =>
            {
                var d = GelDriver(); var need = GelUseNeed(d.pawn); GelDose(d.pawn);
                GelFire(d); Near(.1f, (float)GelComp(d.pawn).BatchProgress); Near(.02f, need.CurLevel);
                GelFire(d); Near(.1f, (float)GelComp(d.pawn).BatchProgress); Near(.04f, need.CurLevel);
                GelToggle(d.pawn, false); GelFire(d); Near(.06f, need.CurLevel);
            });
            Check("gel scene cap survives lower corruption, redose and replacement medicine", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                for (int i = 0; i < 5; i++) GelFire(d);
                Near(.1f, need.CurLevel); double delivered = need.CurLevel;
                Require(delivered >= .10f && delivered <= .10f, "five events must reach the native 10% milestone without exceeding it");
                GelFire(d); Near(.1f, need.CurLevel);
                need.SetCorruption(0); GelDose(p); GelFire(d); Require(need.CurLevel < .000001f);
                p.health.RemoveHediff(SSCCorruptionGelEffectUtility.Find(p)); GelDose(p); GelFire(d); Require(need.CurLevel < .000001f);
                var next = GelUseDriver(p); GelFire(next); Near(.02f, need.CurLevel); Near(.1f, need.HighestCorruptionLevel);
            });
            Check("gel stage cap clips only actual gain and never lowers an already over-cap need", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); need.SetCorruption(.115f); GelDose(p); var d = GelUseDriver(p);
                GelFire(d); Near(.12f, need.CurLevel);
                need.SetCorruption(0); GelFire(d); Near(.02f, need.CurLevel);
                need.SetCorruption(.4f); GelFire(d); Near(.4f, need.CurLevel);
                var chain = p.health.AddHediff(SSCDefOf.ChainOfSexSlave); chain.Severity = .1f;
                need.SetCorruption(.295f); GelFire(d); Near(.3f, need.CurLevel);
                SSCMod.settings.useOldScoring = true; need.SetCorruption(.4f); GelFire(d); Near(.42f, need.CurLevel);
                SSCMod.settings.useOldScoring = false;
            });
            Check("gel prior unrelated corruption gains do not consume the drug scene quota", () =>
            {
                SSCMod.settings.useOldScoring = true;
                try
                {
                    var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                    CorruptionUtility.AddCorruption(p, .2f); GelFire(d); CorruptionUtility.AddCorruption(p, .1f);
                    for (int i = 0; i < 5; i++) GelFire(d);
                    Near(.4f, need.CurLevel);
                }
                finally { SSCMod.settings.useOldScoring = false; }
            });
            Check("gel mid-scene dose and expiry affect only later successful events", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); var d = GelUseDriver(p); GelFire(d); Near(0, need.CurLevel);
                gelOrgasmCallback = () => GelDose(p); GelFire(d); Near(0, need.CurLevel);
                GelFire(d); Near(.02f, need.CurLevel);
                SSCCorruptionGelEffectUtility.Find(p).Timer.SetDuration(0); GelFire(d); Near(.02f, need.CurLevel);
                GelDose(p); GelFire(d); Near(.04f, need.CurLevel);
                SSCCorruptionGelSceneRuntime.Finish(d); GelFire(d); Near(.04f, need.CurLevel);
            });
            Check("gel absent need is never created and an in-flight replacement cannot receive an old event", () =>
            {
                var p = GelConsumer(); p.needs.AllNeeds.Clear(); p.needs.mood = null;
                GelDose(p); var d = GelUseDriver(p); GelFire(d); Equal(0, p.needs.AllNeeds.Count);
                var old = GelUseNeed(p); Need_Corruption replacement = null;
                gelOrgasmCallback = () => { p.needs.AllNeeds.Remove(old); replacement = GelUseNeed(p); };
                GelFire(d); Near(0, old.CurLevel); Near(0, replacement.CurLevel);
                GelFire(d); Near(.02f, replacement.CurLevel);
            });
            Check("gel in-flight medicine and personality replacements reject old rewards without resetting quota", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p); GelFire(d);
                gelOrgasmCallback = () => { p.health.RemoveHediff(SSCCorruptionGelEffectUtility.Find(p)); GelDose(p); };
                GelFire(d); Near(.02f, need.CurLevel);
                gelOrgasmCallback = () => { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); scope.Dispose(); };
                GelFire(d); Near(.02f, need.CurLevel); GelFire(d); Near(.04f, need.CurLevel);
            });
            Check("gel producer callback cannot grant consumption to a replacement health tracker", () =>
            {
                var d = GelDriver(); var p = d.pawn; var need = GelUseNeed(p); GelDose(p);
                gelAfterBoost = () => { p.health = new Pawn_HealthTracker(p); GelDose(p); };
                GelFire(d); Near(0, need.CurLevel); GelFire(d); Near(.02f, need.CurLevel);
            });
            Check("gel master transitions and hollow suspension stop gains without restoring a removed dose", () =>
            {
                var p = GelConsumer(); var training = AttachToggleTraining(p); training.pawnIdentity = PawnIdentity.Unset;
                var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p); GelFire(d);
                training.pawnIdentity = PawnIdentity.Master; SSCAbilityToggleUtility.SyncMarkers(p); GelFire(d); Near(.02f, need.CurLevel);
                training.pawnIdentity = PawnIdentity.Unset; SSCAbilityToggleUtility.SyncMarkers(p); GelFire(d); Near(.02f, need.CurLevel);
                GelDose(p); var hollow = p.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
                GelFire(d); Near(.02f, need.CurLevel); p.health.RemoveHediff(hollow); GelFire(d); Near(.04f, need.CurLevel);
            });
            Check("gel nested real events count once each and exceptional events are not replayed", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                gelOrgasmCallback = () => GelFire(d); GelFire(d); Equal(2, d.orgasms); Near(.04f, need.CurLevel);
                gelOrgasmCallback = () => throw new InvalidOperationException("gel event failure");
                bool threw = false; try { GelFire(d); } catch (InvalidOperationException) { threw = true; }
                Require(threw); Near(.04f, need.CurLevel); GelFire(d); Near(.06f, need.CurLevel);
            });
            Check("gel settlement reentry and a post-write exception cannot duplicate or refund a paid gain", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                gelAfterCorruptionWrite = () => GelFire(d); GelFire(d); Equal(2, d.orgasms); Near(.02f, need.CurLevel);
                gelAfterCorruptionWrite = () => throw new InvalidOperationException("gel write callback failure");
                bool threw = false; try { GelFire(d); } catch (InvalidOperationException) { threw = true; }
                Require(threw); Near(.04f, need.CurLevel);
                need.SetCorruption(0); for (int i = 0; i < 5; i++) GelFire(d); Near(.06f, need.CurLevel);
            });
            Check("gel driver replacement on the same Job preserves quota before its first event or save", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                for (int i = 0; i < 5; i++) GelFire(d); need.SetCorruption(0);
                var replacement = GelUseDriver(p, d.job); var restored = GelLoad(GelSave(replacement), p);
                GelFire(restored); Require(need.CurLevel < .000001f);
                replacement = GelUseDriver(p, restored.job); GelFire(replacement); Require(need.CurLevel < .000001f);
                GelFire(GelUseDriver(p)); Near(.02f, need.CurLevel);
            });
            Check("gel native Scribe and immediate re-save preserve the remaining consumer allowance", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p);
                GelFire(d); GelFire(d); var restored = GelLoad(GelSave(d), p);
                restored = GelLoad(GelSave(restored), p); need.SetCorruption(0);
                for (int i = 0; i < 5; i++) GelFire(restored); Near(.06f, need.CurLevel);
            });
            Check("gel loaded task retains both ledgers when its driver is replaced before any event or save", () =>
            {
                var d = GelDriver(); var p = d.pawn; var need = GelUseNeed(p); GelDose(p);
                for (int i = 0; i < 5; i++) GelFire(d);
                var restored = GelLoad(GelSave(d), p); need.SetCorruption(0);
                var replacement = GelUseDriver(p, restored.job);
                restored = GelLoad(GelSave(replacement), p); GelFire(restored);
                Require(need.CurLevel < .000001f && Math.Abs(GelComp(p).BatchProgress - .1d) < .000001d,
                    $"loaded task must retain paid consumption and production claim: need={need.CurLevel}, batch={GelComp(p).BatchProgress}");
            });
            Check("gel native PostLoadInit binds the ledger before SetupToils can replace its driver", () =>
            {
                var d = GelDriver(); var p = d.pawn; var need = GelUseNeed(p); GelDose(p);
                for (int i = 0; i < 5; i++) GelFire(d);
                var setup = AccessTools.Method(typeof(JobDriver), "SetupToils");
                // 这例保留原版基类ExposeData及其PostLoadInit调用，只隔离地图Toil构建体。
                harmony.Unpatch(baseExpose, AccessTools.Method(typeof(Program), nameof(Skip)));
                harmony.Patch(setup, prefix: new HarmonyMethod(typeof(Program), nameof(GelLoadSetupBoundary)));
                try
                {
                    string path = GelSave(d);
                    gelLoadSetup = loaded => GelUseDriver(p, loaded.job);
                    var restored = GelLoad(path, p);
                    Require(!ReferenceEquals(restored, p.jobs.curDriver), "native SetupToils boundary must actually replace the driver");
                    var replacement = (JobDriver_Sex)p.jobs.curDriver;
                    restored = GelLoad(GelSave(replacement), p); need.SetCorruption(0); GelFire(restored);
                    Require(need.CurLevel < .000001f); Near(.1f, (float)GelComp(p).BatchProgress);
                }
                finally
                {
                    gelLoadSetup = null;
                    harmony.Unpatch(setup, AccessTools.Method(typeof(Program), nameof(GelLoadSetupBoundary)));
                    harmony.Patch(baseExpose, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                }
            });
            Check("gel v1 and missing scene nodes migrate without replaying old orgasms or production claims", () =>
            {
                var d = GelDriver(); GelFire(d); var p = d.pawn; var need = GelUseNeed(p); GelDose(p);
                string path = GelSave(d); Edit(path, xml => {
                    var node = xml.Descendants("sscCorruptionGelScene").Single(); node.Element("version").Value = "1";
                    node.Element("granted").Remove(); node.Element("consumptionClosed").Remove(); });
                var restored = GelLoad(path, p); GelFire(restored); Near(.02f, need.CurLevel); Near(.1f, (float)GelComp(p).BatchProgress);
                path = GelSave(restored); Edit(path, xml => xml.Descendants("sscCorruptionGelScene").Remove());
                restored = GelLoad(path, p); Near(.02f, need.CurLevel); GelFire(restored); Near(.04f, need.CurLevel);
            });
            Check("gel invalid current-format allowance closes consumption but retains independent production", () =>
            {
                var d = GelDriver(); var p = d.pawn;
                // 用真实但未启用生产的事件建立有效账本，不依赖原版立即返回的空询问创建状态。
                GelToggle(p, false); GelFire(d); GelToggle(p, true);
                var need = GelUseNeed(p); GelDose(p);
                string path = GelSave(d); Edit(path, xml => xml.Descendants("granted").Single().Remove());
                var restored = GelLoad(path, p); GelFire(restored); Near(0, need.CurLevel); Near(.1f, (float)GelComp(p).BatchProgress);
                path = GelSave(restored); Edit(path, xml => xml.Descendants("version").Single().Value = "99");
                restored = GelLoad(path, p); GelFire(restored); Near(0, need.CurLevel); Near(.1f, (float)GelComp(p).BatchProgress);
            });
            Check("gel snapshots during an event or reward settlement cannot restore a second claim", () =>
            {
                var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p); string path = null;
                gelOrgasmCallback = () => path = GelSave(d); GelFire(d); Near(.02f, need.CurLevel);
                var restored = GelLoad(path, p); GelFire(restored); Near(.02f, need.CurLevel);
                var next = GelUseDriver(p); gelAfterCorruptionWrite = () => path = GelSave(next); GelFire(next); Near(.04f, need.CurLevel);
                restored = GelLoad(path, p); GelFire(restored); Near(.04f, need.CurLevel);
            });
            GelFourthLifecycleTests(harmony);
        }
        finally
        {
            gelOrgasmCallback = gelAfterBoost = gelAfterCorruptionWrite = null; gelLoadSetup = null; SSCMod.settings = previous;
            foreach (var method in sideEffects) harmony.Unpatch(method, AccessTools.Method(typeof(Program), nameof(Skip)));
            harmony.Unpatch(sideEffects[0], AccessTools.Method(typeof(Program), nameof(GelOrgasmSideEffect)));
            harmony.Unpatch(boost, AccessTools.Method(typeof(Program), nameof(GelAfterBoostCallback)));
            harmony.Unpatch(write, AccessTools.Method(typeof(Program), nameof(GelAfterCorruptionCallback)));
            if (!skipped) harmony.Unpatch(baseExpose, AccessTools.Method(typeof(Program), nameof(Skip)));
            if (!hadRjwTypeBoundary) harmony.Unpatch(lookup, AccessTools.Method(typeof(Program), nameof(RjwScribeTypeBoundary)));
        }
    }

    private static void GelDose(Pawn p)
    {
        // 生产者夹具此前只需身体；真实摄入还会访问这些原生角色服务。
        p.story ??= new Pawn_StoryTracker(p); p.mindState ??= new Pawn_MindState(p); p.carryTracker ??= new Pawn_CarryTracker(p);
        var item = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SSC_CorruptionGel")); item.stackCount = 2;
        item.Ingested(p, 1f); Equal(1, item.stackCount);
    }
    private static Need_Corruption GelUseNeed(Pawn p)
    {
        var def = DefDatabase<NeedDef>.GetNamedSilentFail("GelCorruption");
        if (def == null) { def = new NeedDef { defName = "GelCorruption", needClass = typeof(Need_Corruption) }; DefDatabase<NeedDef>.Add(def); }
        // 注册NeedDef后新建的原生Tracker会自行生成需求，不可往列表再塞一份同类型需求。
        var need = p.needs.TryGetNeed<Need_Corruption>();
        if (need == null) { need = new Need_Corruption(p) { def = def }; p.needs.AllNeeds.Add(need); }
        need.SetCorruption(0, true); return need;
    }
    private static SSCGelTestDriver GelUseDriver(Pawn p, Job job = null)
    {
        p.jobs ??= new Pawn_JobTracker(p);
        var d = new SSCGelTestDriver { pawn = p, job = job ?? new Job { def = gelJob, loadID = ++nextId },
            Sexprops = new SexProps(p, p) { usedCondom = true }, orgasmstick = 1, sex_ticks = 100 };
        p.jobs.curDriver = d; p.jobs.curJob = d.job; return d;
    }
    private static void GelAfterBoostCallback() { var callback = gelAfterBoost; gelAfterBoost = null; callback?.Invoke(); }
    private static void GelAfterCorruptionCallback() { var callback = gelAfterCorruptionWrite; gelAfterCorruptionWrite = null; callback?.Invoke(); }
    private static bool GelLoadSetupBoundary(JobDriver __instance)
    {
        if (__instance is not SSCGelTestDriver driver || Scribe.mode != LoadSaveMode.PostLoadInit) return true;
        var callback = gelLoadSetup; gelLoadSetup = null; callback?.Invoke(driver); return false;
    }

    private static void GelFourthLifecycleTests(Harmony harmony)
    {
        // 人格通用采集会遍历其他特化标签；只提供未携带的中性定义，不替代采集／恢复算法。
        foreach (string name in new[] { "SSC_Hediff_Bus", "SSC_Hediff_Bus_Final", "SSC_Hediff_Cow", "SSC_Hediff_Cow_Final" })
        {
            var field = AccessTools.Field(typeof(SSCDefOf), name);
            if (field.GetValue(null) == null) field.SetValue(null, AddGelDef(new HediffDef { defName = name, hediffClass = typeof(Hediff) }));
        }
        foreach (string pet in new[] { "PetCat", "PetDog", "PetRabbit", "PetFox" })
        foreach (string suffix in new[] { "", "_Final" })
            if (DefDatabase<HediffDef>.GetNamedSilentFail("SSC_Hediff_" + pet + suffix) == null)
                AddGelDef(new HediffDef { defName = "SSC_Hediff_" + pet + suffix, hediffClass = typeof(Hediff) });
        DefDatabase<RecipeDef>.Add(new RecipeDef_PSTag { defName = "GelBodyMedicineCaptureFixture", hediffToAdd = gelEffect });
        var engine = new[] { AccessTools.Method(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)),
            AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_DisabledWorkTypesChanged)) }
            .Concat(AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == nameof(Messages.Message) &&
                m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))).ToArray();
        foreach (var method in engine) harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        try
        {
        Check("gel actual personality capture and processing omit body medicine and scene allowance", () =>
        {
            var p = GelConsumer(); var need = GelUseNeed(p); GelDose(p); var d = GelUseDriver(p); GelFire(d);
            var original = SSCCorruptionGelEffectUtility.Find(p); original.Timer.ticksToDisappear = 4321;
            var raw = new CompPersonalityStore(); raw.StorePawnData(p); var processed = new CompPersonalityStore(); processed.CopyFrom(raw);
            Require(!raw.hediffTags.ContainsKey(gelEffect) && !processed.hediffTags.ContainsKey(gelEffect));
            Near(.02f, processed.corruptionLevel); Near(.02f, processed.highestCorruptionLevel);
            Require(ReferenceEquals(original, SSCCorruptionGelEffectUtility.Find(p))); Equal(4321, original.Timer.ticksToDisappear);
            need.SetCorruption(0); for (int i = 0; i < 5; i++) GelFire(d); Near(.08f, need.CurLevel);
        });
        Check("gel actual clone birth cleanup removes copied medicine and inventory while maturation keeps its own", () =>
        {
            var source = GelPawn(); var clone = GelPawn(); GelToggle(source, true); GelToggle(clone, true);
            // 原生克隆仅保留SSC状态；夹具器官也用该前缀，避免新生清理把测试器官当作普通疾病删除。
            var organ = new HediffDef_SexPart { defName = "SSC_GelCloneAnus", hediffClass = typeof(Hediff), genitalFamily = GenitalFamily.Anus };
            AddGelDef(organ); source.health.AddHediff(organ, human.race.body.corePart); clone.health.AddHediff(organ, human.race.body.corePart);
            GelTick(GelComp(source), 60000); GelTick(GelComp(clone), 60000);
            GelDose(source); GelDose(clone);
            AccessTools.Method(typeof(RabbitCloneUtility), "PrepareFreshRabbitCloneBody").Invoke(null, new object[] { source, clone });
            Require(SSCCorruptionGelEffectUtility.Find(clone) == null);
            SSCCorruptionGelProductionUtility.EnsureProducer(clone); Require(!GelComp(clone).ProductionEnabled); Near(0, (float)GelComp(clone).BatchProgress);
            GelDose(clone); var own = SSCCorruptionGelEffectUtility.Find(clone); own.Timer.ticksToDisappear = 4321;
            var link = new HediffComp_RabbitCloneLink(); RabbitCloneUtility.MatureRabbitClone(clone, source, link);
            Require(link.mature && ReferenceEquals(own, SSCCorruptionGelEffectUtility.Find(clone))); Equal(4321, own.Timer.ticksToDisappear);
            Require(!GelComp(clone).ProductionEnabled); Near(0, (float)GelComp(clone).BatchProgress);
        });
        Check("gel full personality insertion keeps host medicine and does not reset its current task allowance", () =>
        {
            var source = GelConsumer(); var sourceNeed = GelUseNeed(source); GelDose(source); GelFire(GelUseDriver(source));
            var raw = new CompPersonalityStore(); raw.StorePawnData(source);
            var processed = new CompPersonalityStore(); processed.CopyFrom(raw);
            // 旧档／外部载荷即使强塞药效标签，真实恢复入口也须忽略它。
            processed.hediffTags[gelEffect] = 1;
            var host = GelConsumer(); var need = GelUseNeed(host); GelDose(host); var d = GelUseDriver(host);
            for (int i = 0; i < 4; i++) GelFire(d);
            var own = SSCCorruptionGelEffectUtility.Find(host); own.Timer.ticksToDisappear = 4100;
            Require(ExcretionUtility.InheritEverything(host, processed), string.Join('\n', errors));
            Require(ReferenceEquals(own, SSCCorruptionGelEffectUtility.Find(host))); Equal(4100, own.Timer.ticksToDisappear);
            Near(.02f, need.CurLevel); Near(.02f, need.HighestCorruptionLevel);
            need.SetCorruption(0); GelFire(d); GelFire(d); Near(.02f, need.CurLevel);
            Require(!SSCPersonalityRuntimeUtility.IsMutating(host));
        });
        }
        finally { foreach (var method in engine) harmony.Unpatch(method, AccessTools.Method(typeof(Program), nameof(Skip))); }
    }
}
