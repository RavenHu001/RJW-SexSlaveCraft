// AI 辅助生成：概览原概率、真实必拒和左上角提示，不通过降低概率模拟拒绝。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>真实 worker／tracker 覆盖消息时机与去重，概率输入仍由既有宿主边界提供。</summary>
    private static void PassiveFeedbackTests(Harmony harmony, string repo)
    {
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
        foreach (float chance in new[] { 0f, .6f, 1f })
            Check($"passive refusal native chance {chance} still rejects and emits one message {action}", () =>
            {
                CommandReset(0f, .9f); socialChance = chance;
                var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false, allowAcceptProposal = false };
                if (action == SSCRomanceAction.Proposal) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                Require(a.interactions.TryInteractWith(b, SocialDef(action))); Equal(1, commandFeedback);
                Require(!a.relations.DirectRelationExists(action == SSCRomanceAction.Romance ? PawnRelationDefOf.Lover : PawnRelationDefOf.Fiance, b));
                Require(a.needs.mood.thoughts.memories.Memories.Any(m => m.def == (action == SSCRomanceAction.Romance ? ThoughtDefOf.RebuffedMyRomanceAttempt : ThoughtDefOf.RejectedMyProposal)));
                Equal(chance, action == SSCRomanceAction.Romance ? InteractionWorker_RomanceAttempt.SuccessChance(a, b) : InteractionWorker_MarriageProposal.AcceptanceChance(a, b));
            });
        foreach (bool guarantee in new[] { false, true })
            Check("passive refusal query has no message and menu keeps native label " + guarantee, () =>
            {
                CommandReset(); var a = SocialPawn(); var b = SocialSlave(a);
                Comp(b).romanceConfig = new() { allowAcceptRomance = false, alwaysAcceptMaster = guarantee };
                var original = new FloatMenuOption("native 60 percent", () => { }); var option = original; float chance = .6f; bool eligible = true;
                Harmony_SSCRomanceMenu.Postfix(a, b, ref option, ref chance, ref eligible);
                Require(eligible && option.action != null && option == original); Equal(guarantee ? 1f : .6f, chance);
                if (!guarantee) Equal("native 60 percent", option.Label);
                SSCRomanceAcceptanceUtility.Resolve(true, a, b, SSCRomanceAction.Romance); Equal(0, commandFeedback);
            });
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
            Check("passive refusal command emits only its existing result " + action, () =>
            {
                CommandReset(0f, .9f); var (a, b) = CommandPair(action, true);
                Comp(b).romanceConfig = new() { allowAcceptRomance = false, allowAcceptProposal = false };
                SubmitAndRun(a, b, action, out var record); Equal(SSCRomanceCommandOutcome.Rejected, record.Outcome);
                Equal(1, commandFeedback); ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).Cleanup(JobCondition.Succeeded);
                Equal(1, commandFeedback);
            });
        Check("passive refusal native manual romance retains exactly one native failed message", () =>
        {
            CommandReset(0f); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            a.jobs = new Pawn_JobTracker(a); a.jobs.curJob = JobMaker.MakeJob(JobDefOf.TryRomance, b);
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(1, commandFeedback);
        });
        Check("passive refusal RJW click retains native chance and adds one refusal message", () =>
        {
            CommandReset(0f); socialChance = 1f; var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            Harmony_SSCRomanceRjwClick.TargetMethod().Invoke(null, new object[] { a, new LocalTargetInfo(b), InteractionDefOf.RomanceAttempt });
            Equal(1, commandFeedback); Equal(1, playLogCalls); Equal(1f, InteractionWorker_RomanceAttempt.SuccessChance(a, b));
        });
        Check("passive refusal active denial and inability do not invent rejection messages", () =>
        {
            CommandReset(); var a = SocialSlave(); var b = SocialSlave(); Comp(a).romanceConfig = new() { allowInitiateRomance = false };
            Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, commandFeedback);
            Comp(a).romanceConfig.allowInitiateRomance = true; socialCanInteract = false;
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, commandFeedback);
        });
        foreach (bool guarantee in new[] { false, true })
            Check("passive refusal normal random rejection and master success add no SSC refusal " + guarantee, () =>
            {
                CommandReset(guarantee ? 0f : 1f); var a = SocialPawn(); var b = SocialSlave(a);
                Comp(b).romanceConfig = new() { allowAcceptRomance = guarantee ? false : true, alwaysAcceptMaster = guarantee };
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, commandFeedback);
                Equal(guarantee, a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            });
        Check("passive refusal exception does not report completed refusal and releases observation", () =>
        {
            CommandReset(); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            var boundary = new Harmony("ssc.tests.romance.passive-feedback-exception");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(SSCRomanceExecutionScope), nameof(SSCRomanceExecutionScope.ObserveDecision)), postfix: new HarmonyMethod(typeof(Program), nameof(FeedbackThrow)));
                try { a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt); throw new Exception("expected native worker failure"); }
                catch (InvalidOperationException) { }
                Equal(0, commandFeedback);
            }
            finally { boundary.UnpatchAll(boundary.Id); }
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(1, commandFeedback);
        });
        Check("passive refusal skipped original worker does not emit completion message", () =>
        {
            CommandReset(); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            var boundary = new Harmony("ssc.tests.romance.passive-feedback-skip");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, commandFeedback);
            }
            finally { boundary.UnpatchAll(boundary.Id); }
        });
    }

    /// <summary>在实际接受比较之后注入异常，验证反馈不能先于真实拒绝结算完成。</summary>
    private static void FeedbackThrow() => throw new InvalidOperationException("passive feedback fixture");
}
