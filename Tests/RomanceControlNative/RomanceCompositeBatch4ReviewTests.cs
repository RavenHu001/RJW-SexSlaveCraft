// AI 辅助生成：提交前复现持续查询／缓存重入与诊断归属边界。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action review4DirtyAction, review4TraitAction;
    private static TraitSet review4TraitSet;
    private static Pawn review4RepeatingPawn;
    private static int review4Repeats;

    /// <summary>先在初次候选复现，再由修复候选执行；所有补丁限定一次原生回调。</summary>
    private static void RomanceCompositeBatch4ReviewTests()
    {
        foreach (bool raw in new[] { false, true })
            Check("composite b4 review query rejects changed configuration " + raw, () =>
            {
                var pawn = ControlPawn(); var boundary = new Harmony("ssc.tests.composite.b4-review-config");
                try
                {
                    review4TraitSet = pawn.story.traits;
                    review4TraitAction = () =>
                    {
                        if (raw) Comp(pawn).romanceConfig.allowAcceptRomance = false;
                        else SetReview4Preference(pawn, SSCRomanceSetting.AcceptRomance, false);
                    };
                    boundary.Patch(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new[] { typeof(TraitDef) }), postfix: new HarmonyMethod(typeof(Program), nameof(Review4TraitAfter)));
                    Require(!SSCRomanceContinuousMood.TryResolve(pawn, out _), "must not return the pre-callback A branch");
                    Equal("C-AB-S4-N", ControlBranch(pawn)); Require(review4TraitAction == null);
                }
                finally { boundary.UnpatchAll(boundary.Id); review4TraitSet = null; review4TraitAction = null; }
            });
        Check("composite b4 review query rejects effective trait changed in callback", () =>
        {
            var pawn = ControlPawn(maso: true); var boundary = new Harmony("ssc.tests.composite.b4-review-trait");
            try
            {
                SSCRomanceMoodRefresh.Notify(pawn); review4TraitSet = pawn.story.traits;
                review4TraitAction = () => pawn.story.traits.RemoveTrait(pawn.story.traits.allTraits.Single());
                boundary.Patch(AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new[] { typeof(TraitDef) }), postfix: new HarmonyMethod(typeof(Program), nameof(Review4TraitAfter)));
                Require(!SSCRomanceContinuousMood.TryResolve(pawn, out _)); Equal("C-A-S4-N", ControlBranch(pawn));
            }
            finally { boundary.UnpatchAll(boundary.Id); review4TraitSet = null; review4TraitAction = null; }
        });
        foreach (bool trait in new[] { false, true })
            Check("composite b4 review dirty callback reconciles final branch " + trait, () =>
            {
                var pawn = ControlPawn(); SSCRomanceMoodRefresh.Notify(pawn); var boundary = new Harmony("ssc.tests.composite.b4-review-dirty");
                try
                {
                    review4DirtyAction = () =>
                    {
                        if (trait) pawn.story.traits.GainTrait(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
                        else SetReview4Preference(pawn, SSCRomanceSetting.AcceptRomance, true);
                    };
                    boundary.Patch(AccessTools.Method(typeof(SituationalThoughtHandler), nameof(SituationalThoughtHandler.Notify_SituationalThoughtsDirty)), postfix: new HarmonyMethod(typeof(Program), nameof(Review4DirtyAfter)));
                    moodDiagnostics.Clear(); SetReview4Preference(pawn, SSCRomanceSetting.AcceptRomance, false);
                    Require(review4DirtyAction == null);
                    if (trait)
                    {
                        Equal("C-AB-S4-M", ControlBranch(pawn));
                        Require(moodDiagnostics.Single(m => m.Contains("control ")).Contains("selected=C-AB-S4-M"));
                    }
                    else { Equal("C-A-S4-N", ControlBranch(pawn)); Equal(0, moodDiagnostics.Count); }
                    moodDiagnostics.Clear(); SSCRomanceMoodRefresh.Notify(pawn); Equal(0, moodDiagnostics.Count);
                }
                finally { boundary.UnpatchAll(boundary.Id); review4DirtyAction = null; }
            });
        Check("composite b4 review null stage fails inactive without display exception", () =>
        {
            var def = SSCRomanceMoodDefOf.SSC_Romance_Control; var old = def.stages[0];
            try { def.stages[0] = null; Require(!ControlThought(ControlPawn()).Active); }
            finally { def.stages[0] = old; }
        });
        Check("composite b4 review endless dirty reentry is bounded and later notification recovers", () =>
        {
            var pawn = ControlPawn(); SSCRomanceMoodRefresh.Notify(pawn); var boundary = new Harmony("ssc.tests.composite.b4-review-repeat");
            try
            {
                review4RepeatingPawn = pawn; review4Repeats = 0;
                boundary.Patch(AccessTools.Method(typeof(SituationalThoughtHandler), nameof(SituationalThoughtHandler.Notify_SituationalThoughtsDirty)), postfix: new HarmonyMethod(typeof(Program), nameof(Review4RepeatedDirty)));
                moodDiagnostics.Clear(); SetReview4Preference(pawn, SSCRomanceSetting.AcceptRomance, false);
                Equal(3, review4Repeats); Equal(0, moodDiagnostics.Count);
            }
            finally { boundary.UnpatchAll(boundary.Id); review4RepeatingPawn = null; }
            SSCRomanceMoodRefresh.Notify(pawn); Require(moodDiagnostics.Single().Contains("selected=C-AB-S4-N"));
            Equal(0, pawn.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("composite b4 review diagnostics distinguish own rule from shared decision rule", () =>
        {
            CommandReset(0f); socialOpinionValue = 60; var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            Comp(b).romanceConfig = new() { allowAcceptRomance = false }; moodDiagnostics.Clear();
            SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
            string actor = moodDiagnostics.Single(m => m.Contains("branch=E07-S4-N-C"));
            string target = moodDiagnostics.Single(m => m.Contains("branch=E03-S4-N-C"));
            Require(actor.Contains(" rule=none ") && actor.Contains(" decisionRule=AcceptRomance "));
            Require(target.Contains(" rule=AcceptRomance ") && target.Contains(" decisionRule=AcceptRomance "));
        });
        foreach (int number in new[] { 11, 12, 13 })
            Check("composite b4 review breakup does not invent acceptance decision " + number, () =>
            {
                var branch = SSCRomanceMoodCatalog.EventBranches.Single(b => (int)b.Event == number && !b.Masochist && b.Opinion == SSCRomanceOpinionBand.Close);
                moodDiagnostics.Clear(); RunCompositeEvent(branch, 60);
                string line = moodDiagnostics.Single(m => m.Contains("event id="));
                Require(line.Contains("decisionKnown=False original=unknown accepted=unknown"));
            });
    }
    /// <summary>使用真实上下文提交，确保重入来自已授权的新选择，而不是伪造配置写入。</summary>
    private static void SetReview4Preference(Pawn pawn, SSCRomanceSetting setting, bool value)
    { Require(SSCRomancePreferences.TrySet(pawn, setting, value, SSCRomancePreferences.GetView(pawn, setting).Context, out _)); }
    /// <summary>原生有效特质返回后执行一次真实操作，外层仍持有旧返回值。</summary>
    private static void Review4TraitAfter(TraitSet __instance)
    { if (__instance == review4TraitSet) { var action = review4TraitAction; review4TraitAction = null; action?.Invoke(); } }
    /// <summary>缓存失效后的业务回调只执行一次，不在测试助手内重做分支解析。</summary>
    private static void Review4DirtyAfter()
    { var action = review4DirtyAction; review4DirtyAction = null; action?.Invoke(); }
    /// <summary>持续重入只登记新的状态通知，生产协调器必须限制本次调用工作量。</summary>
    private static void Review4RepeatedDirty()
    { review4Repeats++; SSCRomanceMoodRefresh.Notify(review4RepeatingPawn); }
}
