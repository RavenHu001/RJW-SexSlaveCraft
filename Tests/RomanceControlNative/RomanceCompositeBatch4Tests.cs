// AI 辅助生成：十四持续分支、原生特质抑制、生命周期及实际诊断回执专项。
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static bool captureMoodDiagnostics, throwMoodDiagnostic;
    private static readonly List<string> moodDiagnostics = new();
    private static readonly List<string> moodDiagnosticEvidence = new();
    private static Pawn traitRefreshPawn;
    private static int traitRefreshCount;

    /// <summary>只在相关原生套件末尾添加验证，不改变前三批用例的夹具或生产业务。</summary>
    private static void RomanceCompositeBatch4Tests(Harmony harmony, string repo)
    {
        var mapsField = AccessTools.Field(typeof(Game), "maps"); var previousMaps = mapsField.GetValue(Current.Game);
        mapsField.SetValue(Current.Game, new List<Map> { Bare<Map>() });
        var previous = SSCMod.settings;
        var boundary = new Harmony("ssc.tests.composite.batch4");
        SSCMod.settings = new SSCSettings { enableSSCLogs = true, enableSSCVerboseLogs = true };
        captureMoodDiagnostics = true;
        // 特质测试执行原 Gain/Remove/RecalculateSuppression；这里只隔离工作和冥想等无关缓存。
        boundary.Patch(AccessTools.Method(typeof(Pawn), nameof(Verse.Pawn.Notify_DisabledWorkTypesChanged)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        boundary.Patch(AccessTools.Method(typeof(MeditationFocusTypeAvailabilityCache), nameof(MeditationFocusTypeAvailabilityCache.ClearFor)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        boundary.Patch(AccessTools.Method(typeof(SituationalThoughtHandler), nameof(SituationalThoughtHandler.Notify_SituationalThoughtsDirty)), postfix: new HarmonyMethod(typeof(Program), nameof(CountControlRefresh)));
        try
        {
            CompositeContinuousTests(repo);
            CompositeEffectiveTraitTests(boundary);
            CompositeDiagnosticTests(repo);
            CompositeBatch4BoundaryTests(repo);
            RomanceCompositeBatch4ReviewTests();
            RomanceCompositeDurationTests();
            RomanceCompositeReplacementAuditTests();
        }
        finally
        {
            string diagnosticPath = Environment.GetEnvironmentVariable("SSC_ROMANCE_DIAGNOSTIC_OUTPUT") ?? Path.Combine(repo, ".builds/romance-composite-batch4/DiagnosticSamples.log");
            Directory.CreateDirectory(Path.GetDirectoryName(diagnosticPath));
            File.WriteAllLines(diagnosticPath, moodDiagnosticEvidence);
            boundary.UnpatchAll(boundary.Id); SSCMod.settings = previous; mapsField.SetValue(Current.Game, previousMaps);
            captureMoodDiagnostics = throwMoodDiagnostic = false; traitRefreshPawn = null; moodDiagnostics.Clear();
        }
    }

    /// <summary>构造当前持续状态；所有阶段／特质分支均交给生产解析器和原生思想重算。</summary>
    private static Pawn ControlPawn(int stage = 4, bool maso = false, bool active = true, bool passive = false)
    {
        var pawn = SocialSlave(severity: new[] { .1f, .3f, .5f, .9f }[stage - 1]);
        Comp(pawn).romanceConfig = new() { allowInitiateRomance = !active, allowAcceptRomance = !passive };
        if (maso) pawn.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
        return pawn;
    }
    /// <summary>直接使用 ThoughtMaker 和 RecalculateState，验证实际实例类型及缓存阶段。</summary>
    private static Thought_SituationalSSCRomanceControl ControlThought(Pawn pawn)
    {
        var thought = (Thought_SituationalSSCRomanceControl)ThoughtMaker.MakeThought(SSCRomanceMoodDefOf.SSC_Romance_Control);
        // 情境思想保留原版离图准入；只为显示宿主提供 Spawned 槽，不改生产 Def 的准入字段。
        AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
        thought.pawn = pawn; thought.RecalculateState(); return thought;
    }

    /// <summary>数值／映射／独立文本与真实 A/B 条件逐项核对，零持续状态不当成零事件。</summary>
    private static void CompositeContinuousTests(string repo)
    {
        Check("composite b4 fourteen XML stages match explicit branch mapping", () =>
        {
            var def = XDocument.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_RomanceMood.xml")).Root.Element("ThoughtDef");
            Equal(typeof(Thought_SituationalSSCRomanceControl).FullName, def.Element("thoughtClass").Value);
            var stages = def.Element("stages").Elements("li").ToList(); Equal(14, stages.Count);
            for (int i = 0; i < 14; i++) Equal(SSCRomanceContinuousMood.BranchAt(i).Value, (int)stages[i].Element("baseMoodEffect"));
            Require(SSCRomanceContinuousMood.BranchAt(-1) == null && SSCRomanceContinuousMood.BranchAt(14) == null);
        });
        foreach (var branch in SSCRomanceMoodCatalog.ControlBranches)
            Check("composite b4 actual continuous " + branch.Id, () =>
            {
                var pawn = ControlPawn(branch.Stage, branch.Masochist, branch.Control != SSCRomanceControlState.Passive, branch.Control != SSCRomanceControlState.Active);
                Require(SSCRomanceContinuousMood.TryResolve(pawn, out var selected)); Equal(branch.Id, selected.Id);
                var thought = ControlThought(pawn); Require(thought.Active, "native continuous admission must succeed"); Equal(SSCRomanceContinuousMood.IndexOf(branch), thought.CurStageIndex);
                AssertDocumentedControl(branch, selected, thought);
                Equal(0, pawn.needs.mood.thoughts.memories.Memories.Count);
            });
        foreach (var (stage, mask) in RomanceRepresentativeCases.ActiveFilters)
            Check($"composite b4 active filter {stage}/{mask}", () =>
            {
                var pawn = ControlPawn(stage, active: false); var c = Comp(pawn).romanceConfig;
                c.allowInitiateRomance = (mask & 1) != 0; c.allowInitiateProposal = (mask & 2) != 0; c.allowInitiateBreakup = (mask & 4) != 0;
                c.allowInitiateToMaster = (mask & 8) != 0; c.allowInitiateToSexSlave = (mask & 16) != 0; c.allowInitiateToOthers = (mask & 32) != 0;
                bool expected = stage >= 2 && mask != 63;
                Equal(expected, SSCRomanceContinuousMood.TryResolve(pawn, out var branch));
                if (expected) Equal(SSCRomanceControlState.Active, branch.Control);
            });
        foreach (var (stage, permissions, guarantees) in RomanceRepresentativeCases.PassiveFilters)
            Check($"composite b4 passive OR {stage}/{permissions}/{guarantees}", () =>
            {
                var pawn = ControlPawn(stage, active: false); var c = Comp(pawn).romanceConfig;
                c.allowAcceptRomance = (permissions & 1) != 0; c.allowAcceptProposal = (permissions & 2) != 0;
                c.alwaysAcceptMaster = (guarantees & 1) != 0; c.alwaysAcceptSexSlave = (guarantees & 2) != 0; c.alwaysAcceptOthers = (guarantees & 4) != 0;
                bool expected = stage >= 3 && (permissions != 3 || guarantees != 0);
                Equal(expected, SSCRomanceContinuousMood.TryResolve(pawn, out var branch));
                if (expected) Equal(SSCRomanceControlState.Passive, branch.Control);
            });
        Check("composite b4 same value changes branch and independent cached text", () =>
        {
            var pawn = ControlPawn(3, true); SSCRomanceMoodRefresh.Notify(pawn); var thought = ControlThought(pawn);
            var language = LanguageDatabase.activeLanguage; var keys = language.keyedReplacements;
            try
            {
                language.keyedReplacements = new(keys);
                foreach (string id in new[] { "C-A-S3-M", "C-A-S4-N" })
                {
                    SSCRomanceMoodCatalog.TryGet(id, out var b);
                    language.keyedReplacements[b.NameKey] = new() { key = b.NameKey, value = "Name " + id };
                    language.keyedReplacements[b.DescriptionKey] = new() { key = b.DescriptionKey, value = "Description " + id };
                }
                Equal("Name C-A-S3-M", thought.LabelCap); Equal("Description C-A-S3-M", thought.Description);
                moodDiagnostics.Clear(); pawn.story.traits.allTraits.Clear(); SSCBondUtility.GetChain(pawn).Severity = .9f;
                SSCRomanceMoodRefresh.Notify(pawn); Require(moodDiagnostics.Single().Contains("old=C-A-S3-M selected=C-A-S4-N value=1"));
                // 旧实例在原版重算前的数值、名字和正文保持同一个缓存阶段。
                Equal("Name C-A-S3-M", thought.LabelCap); thought.RecalculateState(); Equal("Name C-A-S4-N", thought.LabelCap);
                Equal("Description C-A-S4-N", thought.Description);
            }
            finally { language.keyedReplacements = keys; }
        });
        Check("composite b4 same branch new handler invalidates without false branch event", () =>
        {
            var pawn = ControlPawn(); SSCRomanceMoodRefresh.Notify(pawn); traitRefreshPawn = pawn; traitRefreshCount = 0; moodDiagnostics.Clear();
            pawn.needs.mood = new Need_Mood(pawn) { def = new NeedDef { defName = "Mood", baseLevel = .5f } };
            SSCRomanceMoodRefresh.Notify(pawn); Equal(1, traitRefreshCount); Equal(0, moodDiagnostics.Count);
            for (int i = 0; i < 10; i++) { SSCRomanceMoodRefresh.Notify(pawn); _ = ControlThought(pawn).Description; }
            Equal(1, traitRefreshCount); Equal(0, moodDiagnostics.Count); traitRefreshPawn = null;
        });
        foreach (string mode in new[] { "loading", "mutation", "hollow", "unbound", "identity", "dead", "default-clone" })
            Check("composite b4 continuous lifecycle " + mode, () =>
            {
                var pawn = ControlPawn(); var memory = CompositeMemory(-3, SSCRomanceMoodGroup.Short); pawn.needs.mood.thoughts.memories.TryGainMemory(memory); memory.age = 5432;
                IDisposable mutation = null;
                try
                {
                    SSCRomanceMoodRefresh.Notify(pawn);
                    switch (mode)
                    {
                        case "loading": Scribe.mode = LoadSaveMode.LoadingVars; break;
                        case "mutation": Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope)); mutation = scope; break;
                        case "hollow": pawn.health.hediffSet.hediffs.Add(new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done, pawn = pawn }); break;
                        case "unbound": pawn.health.hediffSet.hediffs.Remove(SSCBondUtility.GetChain(pawn)); break;
                        case "identity": Comp(pawn).pawnIdentity = PawnIdentity.Unset; break;
                        case "dead": AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Dead); break;
                        case "default-clone": SSCRomanceLifecycle.ResetNewClone(pawn); break;
                    }
                    Require(!SSCRomanceContinuousMood.TryResolve(pawn, out _)); SSCRomanceMoodRefresh.Notify(pawn);
                    Equal(5432, memory.age); Require(pawn.needs.mood.thoughts.memories.Memories.Contains(memory));
                }
                finally { Scribe.mode = LoadSaveMode.Inactive; mutation?.Dispose(); }
                if (mode is "loading" or "mutation") Require(SSCRomanceContinuousMood.TryResolve(pawn, out _));
            });
        Check("composite b4 no old corruption rule or patch remains", () =>
        {
            Require(production.GetType("SexSlaveCraft.SSCRomanceMoodRules") == null);
            Require(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodCorruption") == null);
            foreach (var method in Harmony.GetAllPatchedMethods().Where(m => m.DeclaringType == typeof(Need_Corruption)))
                Require(!Harmony.GetPatchInfo(method).Postfixes.Any(p => p.PatchMethod.DeclaringType?.Name == "Harmony_SSCRomanceMoodCorruption"));
        });
    }

    /// <summary>实际特质及抑制方法执行，受虐狂是否有效仍由原版 HasTrait 决定。</summary>
    private static void CompositeEffectiveTraitTests(Harmony boundary)
    {
        Check("composite b4 real native gain and remove trait refresh", () =>
        {
            var pawn = ControlPawn(); SSCRomanceMoodRefresh.Notify(pawn); moodDiagnostics.Clear();
            var trait = new Trait(SSCRomanceMoodDefOf.Masochist, 0);
            pawn.story.traits.GainTrait(trait); Require(SSCRomanceContinuousMood.TryResolve(pawn, out var branch)); Equal("C-A-S4-M", branch.Id);
            pawn.story.traits.RemoveTrait(trait); Require(SSCRomanceContinuousMood.TryResolve(pawn, out branch)); Equal("C-A-S4-N", branch.Id);
            Equal(2, moodDiagnostics.Count(m => m.Contains("control "))); Require(moodDiagnostics.All(m => m.Contains("reason=EffectiveTrait")));
        });
        // 原版小 getter 可被 JIT 内联，直接设置并恢复真实 DLC 输入字段，不能靠补丁 getter 冒充启用。
        var biotechField = AccessTools.Field(typeof(ModsConfig), "biotechActive"); var oldBiotech = biotechField.GetValue(null);
        biotechField.SetValue(null, true);
        try
        {
            Check("composite b4 real gene suppression recalculation and recovery refresh", () =>
            {
                var pawn = ControlPawn(maso: true); pawn.genes = new Pawn_GeneTracker(pawn);
                var gene = new Gene { pawn = pawn, def = new GeneDef { defName = "CompositeSuppressMaso", suppressedTraits = new() { new GeneticTraitData { def = SSCRomanceMoodDefOf.Masochist, degree = 0 } } } };
                var list = (List<Gene>)AccessTools.Field(typeof(Pawn_GeneTracker), "endogenes").GetValue(pawn.genes);
                SSCRomanceMoodRefresh.Notify(pawn); moodDiagnostics.Clear(); list.Add(gene); AccessTools.Field(typeof(Pawn_GeneTracker), "cachedGenes").SetValue(pawn.genes, null);
                pawn.story.traits.RecalculateSuppression(); Require(!pawn.story.traits.HasTrait(SSCRomanceMoodDefOf.Masochist)); Equal("C-A-S4-N", ControlBranch(pawn));
                list.Clear(); AccessTools.Field(typeof(Pawn_GeneTracker), "cachedGenes").SetValue(pawn.genes, null);
                pawn.story.traits.RecalculateSuppression(); Require(pawn.story.traits.HasTrait(SSCRomanceMoodDefOf.Masochist)); Equal("C-A-S4-M", ControlBranch(pawn));
                Equal(2, moodDiagnostics.Count(m => m.Contains("control ")));
            });
            Check("composite b4 direct source gene override and recovery refresh", () =>
            {
                var pawn = ControlPawn(maso: true);
                var source = new Gene { pawn = pawn, def = new GeneDef { defName = "CompositeMasoSource" } };
                var other = new Gene { pawn = pawn, def = new GeneDef { defName = "CompositeMasoOverride" } };
                pawn.story.traits.allTraits.Single().sourceGene = source; SSCRomanceMoodRefresh.Notify(pawn); moodDiagnostics.Clear();
                source.OverrideBy(other); Equal("C-A-S4-N", ControlBranch(pawn)); source.OverrideBy(null); Equal("C-A-S4-M", ControlBranch(pawn));
                Equal(2, moodDiagnostics.Count(m => m.Contains("control ")));
            });
            Check("composite b4 trait suppression follows native effective trait", () =>
            {
                var pawn = ControlPawn(maso: true); pawn.story.traits.allTraits.Single().suppressedByTrait = true;
                pawn.story.traits.RecalculateSuppression(); Equal("C-A-S4-N", ControlBranch(pawn));
                pawn.story.traits.allTraits.Single().suppressedByTrait = false; pawn.story.traits.RecalculateSuppression(); Equal("C-A-S4-M", ControlBranch(pawn));
            });
        }
        finally { biotechField.SetValue(null, oldBiotech); }
        Check("composite b4 nested trait notifications emit only final branch", () =>
        {
            var pawn = ControlPawn(); SSCRomanceMoodRefresh.Notify(pawn); moodDiagnostics.Clear();
            Harmony_SSCRomanceMoodTraits.Prefix(pawn.story.traits, out var outer);
            try { pawn.story.traits.GainTrait(new Trait(SSCRomanceMoodDefOf.Masochist, 0)); pawn.story.traits.RemoveTrait(pawn.story.traits.allTraits.Single()); }
            finally { Harmony_SSCRomanceMoodTraits.Finalizer(outer, null); }
            Equal(0, moodDiagnostics.Count); Equal("C-A-S4-N", ControlBranch(pawn));
        });
    }
    /// <summary>读真实解析结果，不在测试助手中复制 A/B 或特质业务。</summary>
    private static string ControlBranch(Pawn pawn) => SSCRomanceContinuousMood.TryResolve(pawn, out var branch) ? branch.Id : null;
    /// <summary>仅统计指定角色的真实缓存失效调用，其他原生刷新不计入本例。</summary>
    private static void CountControlRefresh(SituationalThoughtHandler __instance)
    { if (traitRefreshPawn?.needs?.mood?.thoughts?.situational == __instance) traitRefreshCount++; }

    /// <summary>控制台日志与真实提交的对应关系，不将字符串存在作为业务正确性的唯一断言。</summary>
    private static void CompositeDiagnosticTests(string repo)
    {
        // 所有分支的日志字段已并入原生真实事件，不为同一数据重跑108次事件。
        foreach (string mode in new[] { "renew", "capacity", "updated", "denied", "same-result-controlled", "interrupted", "exception", "sink-fault" })
            Check("composite b4 diagnostic actual boundary " + mode, () =>
            {
                CommandReset(1f); socialOpinionValue = 60; var (a, b) = CommandPair(SSCRomanceAction.Romance); var h = a.needs.mood.thoughts.memories;
                var native = DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood"); var boundary = new Harmony("ssc.tests.composite.b4-diagnostic");
                var limit = native.stackLimit;
                try
                {
                    Thought_Memory oldNative = null; Thought_MemorySSCCompositeRomance old = null;
                    if (mode is "renew" or "capacity")
                    { native.stackLimit = 1; oldNative = (Thought_Memory)ThoughtMaker.MakeThought(native); h.TryGainMemory(oldNative, mode == "renew" ? b : SocialPawn()); oldNative.age = 7654; }
                    if (mode == "updated") { old = SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,b,raw:0); h.TryGainMemory(old,b); old.age = 6543; }
                    if (mode == "denied") { eventDeniedDef = DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName); boundary.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)), prefix: new HarmonyMethod(typeof(Program), nameof(EventDenyThought))); }
                    if (mode == "exception") { eventWorkerCallback = () => throw new InvalidOperationException("worker diagnostic test"); boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter))); }
                    moodDiagnostics.Clear();
                    if (mode == "same-result-controlled")
                    {
                        ResetSocial(0f); a = SocialPawn(); b = SocialSlave(); Comp(b).romanceConfig = new() { alwaysAcceptOthers = true };
                        moodDiagnostics.Clear(); Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
                        Require(moodDiagnostics.Any(m => m.Contains("event=ForcedRomanceAccepted") && m.Contains("original=True accepted=True") && m.Contains("native=committed")));
                        Require(moodDiagnostics.Any(m => m.Contains("finish id=") && m.Contains("reason=Settled"))); return;
                    }
                    if (mode == "interrupted")
                    {
                        Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var r, out _));
                        a.stances = new Pawn_StanceTracker(a); a.carryTracker = new Pawn_CarryTracker(a);
                        PrepareReviewDriver((JobDriver_SSCRomanceCommand)a.jobs.curDriver);
                        boundary.Patch(AccessTools.Method(typeof(Verse.Pawn), nameof(Verse.Pawn.ClearReservationsForJob)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                        a.jobs.StopAll(canReturnToPool: false); Require(r.Terminal); Require(moodDiagnostics.Any(m => m.Contains("Command:Cancelled:NotOccurred"))); Equal(0, h.Memories.Count); return;
                    }
                    throwMoodDiagnostic = mode == "sink-fault";
                    try { SubmitAndRun(a, b, SSCRomanceAction.Romance, out _); } catch (InvalidOperationException) when (mode == "exception") { }
                    if (mode == "exception") { Require(moodDiagnostics.Any(m => m.Contains("reason=WorkerException"))); Require(h.Memories.Any(m => m.def == native)); return; }
                    if (mode == "sink-fault") { Require(h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any()); Require(!h.Memories.Any(m => m.def == native)); return; }
                    string line = moodDiagnostics.Single(m => m.Contains("event id="));
                    if (mode == "denied") { Require(line.Contains("result=NotAdmitted") && line.Contains("native=preserved")); Require(h.Memories.Any(m => m.def == native)); }
                    else if (mode == "updated") { Require(line.Contains("result=Replaced") && line.Contains("oldRemainingTicks=173457")); Require(!h.Memories.Contains(old)); Equal(-7,h.Memories.OfType<Thought_MemorySSCRomance>().Single().Data.Value); }
                    else { Require(line.Contains(mode == "renew" ? "undoRenew=1" : "restoreCapacity=1")); Require(h.Memories.Contains(oldNative)); Equal(7654, oldNative.age); }
                }
                finally { native.stackLimit = limit; boundary.UnpatchAll(boundary.Id); eventDeniedDef = null; eventWorkerCallback = null; throwMoodDiagnostic = false; }
            });
        float? randomReference = null;
        foreach (bool total in new[] { false, true }) foreach (bool verbose in new[] { false, true })
            Check($"composite b4 log gates preserve actual settlement {total}/{verbose}", () =>
            {
                SSCMod.settings.enableSSCLogs = total; SSCMod.settings.enableSSCVerboseLogs = verbose;
                Rand.PushState(171710);
                try
                {
                    CommandReset(1f); socialOpinionValue = 60; var (a, b) = CommandPair(SSCRomanceAction.Romance); moodDiagnostics.Clear();
                    SubmitAndRun(a, b, SSCRomanceAction.Romance, out var r); Equal(SSCRomanceCommandStatus.Completed, r.Status);
                    var memory = a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single();
                    Equal("E07-S4-N-C", memory.Data.BranchId); Equal(-7, memory.Data.Value); Equal(180000, memory.DurationTicks); Equal(0, socialRolls.Count);
                    Equal(total && verbose, moodDiagnostics.Any(m => m.Contains("event id=")));
                    // 确定性输入耗尽后的原生随机序列也须一致，日志不能额外消耗游戏随机数。
                    float after = Rand.Value; if (randomReference.HasValue) Equal(randomReference.Value, after); else randomReference = after;
                }
                finally { Rand.PopState(); SSCMod.settings.enableSSCLogs = SSCMod.settings.enableSSCVerboseLogs = true; }
            });
    }
}
