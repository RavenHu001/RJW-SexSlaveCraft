// AI 辅助生成：提交前针对外部回调与嵌套调用的原生边界回归。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action moodReviewCallback;
    private static Action moodReviewOpinionCallback;
    private static Pawn moodReviewActor, moodReviewTarget;

    /// <summary>准入及工厂回调之后仍必须归属原人格；旧处理器也不能收到过期请求。</summary>
    private static void RomanceMoodReviewTests()
    {
        foreach (string change in new[] { "permission", "relation", "command" })
            Check("mood review snapshot opinion callback rechecks " + change, () =>
            {
                CommandReset(0f); var a = SocialSlave(); var b = SocialPawn();
                if (change == "command") a.jobs = new Verse.AI.Pawn_JobTracker(a);
                moodReviewOpinionCallback = () =>
                {
                    if (change == "permission") Comp(a).romanceConfig = new() { allowInitiateRomance = false };
                    else if (change == "relation") a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                    else { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a, out var scope)); scope.Dispose(); }
                };
                var boundary = new Harmony("ssc.tests.mood.review-snapshot");
                try
                {
                    boundary.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.OpinionOf)), postfix: new HarmonyMethod(typeof(Program), nameof(MoodReviewOpinion)));
                    if (change == "command") SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
                    else Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
                    Require(moodReviewOpinionCallback == null); Equal(0, bubbleCalls);
                    Require(!a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
                }
                finally { boundary.UnpatchAll(boundary.Id); moodReviewOpinionCallback = null; }
            });
        foreach (string when in new[] { "factory", "admission" })
        foreach (string change in new[] { "token", "comp", "mood", "handler" })
            Check($"mood review {when} callback replaces {change} before admission", () =>
            {
                CommandReset(0f); var a = SocialPawn(); var b = SocialSlave(severity: .5f);
                Comp(b).romanceConfig = new() { allowAcceptRomance = false };
                var original = b.needs.mood.thoughts.memories;
                moodReviewCallback = () =>
                {
                    if (change == "token")
                    { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(b, out var scope)); scope.Dispose(); }
                    else if (change == "comp")
                        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(b, new List<ThingComp> { new CompSexSlaveTraining { parent = b, pawnIdentity = PawnIdentity.Slave } });
                    else if (change == "mood") b.needs.mood = new Need_Mood(b) { def = new NeedDef { defName = "Mood" } };
                    else AccessTools.Field(typeof(ThoughtHandler), "memories").SetValue(b.needs.mood.thoughts, new MemoryThoughtHandler(b));
                };
                var boundary = new Harmony("ssc.tests.mood.review-admission");
                try
                {
                    if (when == "factory") boundary.Patch(AccessTools.Method(typeof(ThoughtMaker), nameof(ThoughtMaker.MakeThought), new[] { typeof(ThoughtDef), typeof(int) }),
                        prefix: new HarmonyMethod(typeof(Program), nameof(MoodReviewCallback)));
                    else boundary.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)),
                        prefix: new HarmonyMethod(typeof(Program), nameof(MoodReviewAdmission)));
                    Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
                    Require(moodReviewCallback == null, "callback must actually run");
                    Require(!original.Memories.OfType<Thought_MemorySSCRomance>().Any(), "old handler gained stale mood");
                    Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any(), "new holder gained old event");
                }
                finally { boundary.UnpatchAll(boundary.Id); moodReviewCallback = null; }
            });

        Check("mood review unrelated nested chat loss does not suppress command breakup loss", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Breakup, false);
            moodReviewActor = a; moodReviewTarget = b;
            var boundary = new Harmony("ssc.tests.mood.review-nested-chat");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(InteractionWorker_Breakup), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(MoodReviewNestedLoss)));
                SubmitAndRun(a, b, SSCRomanceAction.Breakup, out _);
                Equal(1, a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Count(m => m.Data.Kind == SSCRomanceMoodEvent.RelationshipLoss));
            }
            finally { boundary.UnpatchAll(boundary.Id); moodReviewActor = moodReviewTarget = null; }
        });
        Check("mood review lost relation carrier during worker does not throw from finalizer", () =>
        {
            CommandReset(0f); var a = SocialPawn(); var b = SocialSlave(severity: .5f);
            Comp(b).romanceConfig = new() { allowAcceptRomance = false }; moodReviewActor = a;
            var boundary = new Harmony("ssc.tests.mood.review-relations");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(MoodReviewLostRelations)));
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
                Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
            }
            finally { boundary.UnpatchAll(boundary.Id); moodReviewActor = null; }
        });
    }

    /// <summary>仅在真正构造／准入 SSC 新记忆时消费一次回调，不改变思想准入结论。</summary>
    private static void MoodReviewCallback(ThoughtDef __0)
    {
        if (__0 != SSCRomanceMoodDefOf.SSC_Romance_Experience) return;
        var action = moodReviewCallback; moodReviewCallback = null; action?.Invoke();
    }
    /// <summary>原版准入第一个参数为 Pawn，按已取证的第二个参数读取 ThoughtDef。</summary>
    private static void MoodReviewAdmission(ThoughtDef __1) => MoodReviewCallback(__1);
    /// <summary>读取互动前好感也会经过外部补丁；修改后原共同入口必须重新准入。</summary>
    private static void MoodReviewOpinion()
    { var action = moodReviewOpinionCallback; moodReviewOpinionCallback = null; action?.Invoke(); }
    /// <summary>显式嵌套 tracker 普通互动帧，模拟兼容流程在无关聊天中写入同伴侣记忆。</summary>
    private static void MoodReviewNestedLoss()
    {
        var frame = SSCRomanceExecutionScope.Enter(moodReviewActor, moodReviewTarget, new InteractionDef { defName = "ReviewOrdinaryChat" });
        try { moodReviewActor.needs.mood.thoughts.memories.TryGainMemory(moodNativeLoss, moodReviewTarget); }
        finally { SSCRomanceExecutionScope.Exit(frame); }
    }
    /// <summary>模拟外部补丁在正常 worker 后移除关系载体，结果只能视为无法确认。</summary>
    private static void MoodReviewLostRelations() => moodReviewActor.relations = null;
}
