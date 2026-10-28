// AI 辅助生成：持续文案、观察器回调与实际替换／刷新日志边界。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class Program
{
    private static bool dirtyFaultOnce;
    private static TraitSet controlProbeTraits;
    private static Action controlProbe;

    /// <summary>补充真实回调与独立显示验证，不能以目录查询通过代替原生情境思想准入。</summary>
    private static void CompositeBatch4BoundaryTests(string repo)
    {
        var language = LanguageDatabase.activeLanguage; var originalKeys = language.keyedReplacements;
        try
        {
            foreach (string locale in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                language.keyedReplacements = XDocument.Load(Path.Combine(repo, "1.7/Languages", locale, "Keyed/SSC_RomanceMood.xml"))
                    .Root.Elements().ToDictionary(e => e.Name.LocalName, e => new LoadedLanguage.KeyedReplacement { key = e.Name.LocalName, value = e.Value });
                // 每语保留一次真实显示接线；十四分支的映射与数值由实际持续思想回归负责。
                Check("composite b4 real continuous independent text " + locale + "/C-A-S4-N", () =>
                {
                    Require(SSCRomanceMoodCatalog.TryGet("C-A-S4-N", out var branch));
                    var pawn = ControlPawn(branch.Stage, branch.Masochist, branch.Control != SSCRomanceControlState.Passive, branch.Control != SSCRomanceControlState.Active);
                    var thought = ControlThought(pawn); Require(thought.Active);
                    Equal(SSCRomanceMoodText.Name(branch).CapitalizeFirst(), thought.LabelCap);
                    Equal(SSCRomanceMoodText.Description(branch), thought.Description);
                });
            }
        }
        finally { language.keyedReplacements = originalKeys; }
        foreach (string change in new[] { "count", "value", "produces-memory" })
            Check("composite b4 malformed continuous carrier is inactive " + change, () =>
            {
                var def = SSCRomanceMoodDefOf.SSC_Romance_Control; var stages = def.stages; var produces = def.producesMemoryThought;
                try
                {
                    def.stages = stages.Select(s => new ThoughtStage { label = s.label, description = s.description, baseMoodEffect = s.baseMoodEffect }).ToList();
                    if (change == "count") def.stages.RemoveAt(13);
                    else if (change == "value") def.stages[0].baseMoodEffect = -4;
                    else def.producesMemoryThought = ThoughtDefOf.GotMarried;
                    Require(!ControlThought(ControlPawn()).Active);
                }
                finally { def.stages = stages; def.producesMemoryThought = produces; }
            });
        Check("composite b4 dirty observer failure retries and does not freeze successful signature", () =>
        {
            var pawn = ControlPawn(); var boundary = new Harmony("ssc.tests.composite.b4-dirty-fault");
            try
            {
                SSCRomanceMoodRefresh.Notify(pawn); Comp(pawn).romanceConfig.allowAcceptRomance = false; moodDiagnostics.Clear(); dirtyFaultOnce = true;
                boundary.Patch(AccessTools.Method(typeof(SituationalThoughtHandler), nameof(SituationalThoughtHandler.Notify_SituationalThoughtsDirty)), prefix: new HarmonyMethod(typeof(Program), nameof(ControlDirtyFault)));
                SSCRomanceMoodRefresh.Notify(pawn); Require(!dirtyFaultOnce); Equal(0, moodDiagnostics.Count);
                SSCRomanceMoodRefresh.Notify(pawn); Require(moodDiagnostics.Single().Contains("selected=C-AB-S4-N"));
            }
            finally { boundary.UnpatchAll(boundary.Id); dirtyFaultOnce = false; }
        });
        foreach (string change in new[] { "token", "component" })
            Check("composite b4 effective trait query cannot publish replaced context " + change, () =>
            {
                var pawn = ControlPawn(); var boundary = new Harmony("ssc.tests.composite.b4-query-context");
                try
                {
                    controlProbeTraits = pawn.story.traits;
                    controlProbe = () =>
                    {
                        if (change == "token") { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope)); scope.Dispose(); }
                        else ((List<ThingComp>)AccessTools.Field(typeof(ThingWithComps), "comps").GetValue(pawn))[0] = new CompSexSlaveTraining { parent = pawn, pawnIdentity = PawnIdentity.Slave, romanceConfig = Denied() };
                    };
                    boundary.Patch(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new[] { typeof(TraitDef) }), postfix: new HarmonyMethod(typeof(Program), nameof(ControlQueryAfter)));
                    Require(!SSCRomanceContinuousMood.TryResolve(pawn, out _)); Require(controlProbe == null);
                    Require(SSCRomanceContinuousMood.TryResolve(pawn, out _));
                }
                finally { boundary.UnpatchAll(boundary.Id); controlProbe = null; controlProbeTraits = null; }
            });
        Check("composite b4 trait scope releases after original exception", () =>
        {
            var pawn = ControlPawn(); SSCRomanceMoodRefresh.Notify(pawn);
            var boundary = new Harmony("ssc.tests.composite.b4-trait-exception");
            try
            {
                eventWorkerCallback = () => throw new InvalidOperationException("after native GainTrait");
                boundary.Patch(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.GainTrait)), postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter)));
                bool thrown = false;
                try { pawn.story.traits.GainTrait(new Trait(SSCRomanceMoodDefOf.Masochist, 0)); } catch (InvalidOperationException) { thrown = true; }
                Require(thrown); Equal("C-A-S4-M", ControlBranch(pawn));
            }
            finally { boundary.UnpatchAll(boundary.Id); eventWorkerCallback = null; }
            moodDiagnostics.Clear(); pawn.story.traits.RemoveTrait(pawn.story.traits.allTraits.Single());
            Require(moodDiagnostics.Single().Contains("selected=C-A-S4-N"));
        });
        Check("composite b4 real gene add remove coordinates final suppression", () =>
        {
            var activeField = AccessTools.Field(typeof(ModsConfig), "biotechActive"); var oldActive = activeField.GetValue(null);
            var installedField = AccessTools.Field(typeof(ModLister), "biotechInstalled"); var oldInstalled = installedField.GetValue(null);
            var boundary = new Harmony("ssc.tests.composite.b4-native-gene");
            try
            {
                activeField.SetValue(null, true); installedField.SetValue(null, true);
                boundary.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                var pawn = ControlPawn(maso: true); pawn.genes = new Pawn_GeneTracker(pawn); SSCRomanceMoodRefresh.Notify(pawn); moodDiagnostics.Clear();
                var def = new GeneDef { defName = "CompositeNativeSuppressMaso", geneClass = typeof(Gene),
                    suppressedTraits = new() { new GeneticTraitData { def = SSCRomanceMoodDefOf.Masochist, degree = 0 } } };
                var gene = pawn.genes.AddGene(def, false);
                Require(gene != null && !pawn.story.traits.HasTrait(SSCRomanceMoodDefOf.Masochist)); Equal("C-A-S4-N", ControlBranch(pawn));
                pawn.genes.RemoveGene(gene); Require(pawn.story.traits.HasTrait(SSCRomanceMoodDefOf.Masochist)); Equal("C-A-S4-M", ControlBranch(pawn));
                Equal(2, moodDiagnostics.Count(m => m.Contains("control ")));
            }
            finally { boundary.UnpatchAll(boundary.Id); activeField.SetValue(null, oldActive); installedField.SetValue(null, oldInstalled); }
        });
        foreach (bool same in new[] { false, true })
            Check("composite b4 actual short replacement refresh receipt " + same, () =>
            {
                CommandReset(1f); socialOpinionValue = 0; var (a, b) = CommandPair(SSCRomanceAction.Romance);
                var old = SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,b,raw:same?0:60); a.needs.mood.thoughts.memories.TryGainMemory(old,b); old.age = 4000;
                moodDiagnostics.Clear(); SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
                string line = moodDiagnostics.Single(m => m.Contains("event id="));
                Require(line.Contains(same ? "result=Refreshed" : "result=Replaced")); Require(line.Contains("oldRemainingTicks=176000") && line.Contains("remainingTicks=180000"));
                Equal(-3, a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().Data.Value);
            });
        Check("semantic actual long equal value replaces background and uses new nominal expiry", () =>
        {
            CommandReset(1f, 0f); socialOpinionValue = 60; var (a, b) = CommandPair(SSCRomanceAction.Proposal);
            var old=SemanticMemory(SSCRomanceCompositeEvent.CommandDivorce,b,raw:60); a.needs.mood.thoughts.memories.TryGainMemory(old,b); old.age = 60000;
            moodDiagnostics.Clear(); SubmitAndRun(a, b, SSCRomanceAction.Proposal, out _);
            string line = moodDiagnostics.Single(m => m.Contains("event id="));
            Require(line.Contains("branch=E10-S4-N-C") && line.Contains("oldBranch=E13-S4-N-C") && line.Contains("result=Replaced"));
            Require(line.Contains("nominalTicks=1500000 remainingTicks=1500000") && line.Contains("oldRemainingTicks=1740000"));
            Equal(1500000, a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().DurationTicks);
        });
        Check("composite b4 two controlled sides share one runtime correlation without merged outcomes", () =>
        {
            CommandReset(0f); socialOpinionValue = 60; var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            Comp(b).romanceConfig = new() { allowAcceptRomance = false }; moodDiagnostics.Clear();
            SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
            var entries = moodDiagnostics.Where(m => m.Contains("event id=")).ToList(); Equal(2, entries.Count);
            Equal(1, entries.Select(m => Regex.Match(m, @"id=(\d+)").Groups[1].Value).Distinct().Count());
            Require(entries.Any(m => m.Contains("branch=E07-S4-N-C")) && entries.Any(m => m.Contains("branch=E03-S4-N-C")));
            Equal(1, moodDiagnostics.Count(m => m.Contains("finish id=")));
        });
    }
    /// <summary>一次性故障验证观察器隔离；不更改生产缓存或思想计算。</summary>
    private static void ControlDirtyFault()
    { if (dirtyFaultOnce) { dirtyFaultOnce = false; throw new InvalidOperationException("situational notification observer fault"); } }
    /// <summary>只在原生 HasTrait 已返回后触发生命周期回调，生产解析器必须拒绝旧上下文。</summary>
    private static void ControlQueryAfter(TraitSet __instance)
    { if (__instance == controlProbeTraits) { var action = controlProbe; controlProbe = null; action?.Invoke(); } }
}
