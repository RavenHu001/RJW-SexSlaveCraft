// AI 辅助生成：复核真实 worker 内 Job 切换及嵌套互动的消息归属。
using HarmonyLib;
using System.Reflection.Emit;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static Action reviewDecisionCallback;

    /// <summary>通过原生结算中的回调复现消息遗漏／重复，不把 Job 开始时状态当作实际发送证据。</summary>
    private static void FeedbackReviewTests()
    {
        var boundary = new Harmony("ssc.tests.romance.feedback-review");
        try
        {
            boundary.Patch(AccessTools.Method(typeof(SSCRomanceExecutionScope), nameof(SSCRomanceExecutionScope.ObserveDecision)),
                postfix: new HarmonyMethod(typeof(Program), nameof(ReviewDecisionCallback)));
            foreach (bool startedManual in new[] { false, true })
                Check("feedback review Job changes inside native worker still reports once " + startedManual, () =>
                {
                    CommandReset(0f); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
                    a.jobs = new Pawn_JobTracker(a);
                    if (startedManual) a.jobs.curJob = JobMaker.MakeJob(JobDefOf.TryRomance, b);
                    reviewDecisionCallback = () => a.jobs.curJob = startedManual ? null : JobMaker.MakeJob(JobDefOf.TryRomance, b);
                    Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(1, commandFeedback);
                    Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
                });
            Check("feedback review nested native workers keep separate refusal messages", () =>
            {
                CommandReset(0f, 0f, .9f); var a = SocialPawn(); var b = SocialSlave(); var c = SocialPawn(); var d = SocialSlave();
                Comp(b).romanceConfig = new() { allowAcceptRomance = false }; Comp(d).romanceConfig = new() { allowAcceptProposal = false };
                c.relations.AddDirectRelation(PawnRelationDefOf.Lover, d);
                reviewDecisionCallback = () => Require(c.interactions.TryInteractWith(d, InteractionDefOf.MarriageProposal));
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(2, commandFeedback);
                Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b)); Require(!c.relations.DirectRelationExists(PawnRelationDefOf.Fiance, d));
            });
            Check("feedback review independent later refusal is not suppressed by earlier completion", () =>
            {
                CommandReset(0f, 0f); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(2, commandFeedback);
            });
            foreach (bool duplicate in new[] { false, true })
                Check("feedback review native message anchor mismatch preserves original and disables hook " + duplicate, () =>
                {
                    var method = AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted");
                    var message = AccessTools.Method(typeof(Messages), nameof(Messages.Message), new[] { typeof(string), typeof(LookTargets), typeof(MessageTypeDef), typeof(bool) });
                    var original = PatchProcessor.GetOriginalInstructions(method).ToList();
                    var input = duplicate ? original.Concat(new[] { new CodeInstruction(OpCodes.Call, message) }).ToList() : original.Where(c => !c.Calls(message)).ToList();
                    try
                    {
                        var output = Harmony_SSCRomanceAcceptance.Transpiler(input, method).ToList();
                        Equal(input.Count, output.Count); Equal(input.Count(c => c.Calls(message)), output.Count(c => c.Calls(message)));
                        Require(!output.Any(c => c.Calls(AccessTools.Method(typeof(SSCRomanceAcceptanceUtility), nameof(SSCRomanceAcceptanceUtility.Resolve)))));
                        Require(!SSCRomanceCommandHooks.Ready);
                    }
                    finally { Harmony_SSCRomanceAcceptance.Transpiler(original, method).ToList(); }
                });
        }
        finally { reviewDecisionCallback = null; boundary.UnpatchAll(boundary.Id); }
    }

    /// <summary>只消费一次回调，模拟其他模组在接受判定后改变 Job 或触发内层真实互动。</summary>
    private static void ReviewDecisionCallback()
    { var callback = reviewDecisionCallback; reviewDecisionCallback = null; callback?.Invoke(); }
}
