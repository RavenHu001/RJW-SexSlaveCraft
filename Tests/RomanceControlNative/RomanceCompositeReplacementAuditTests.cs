// AI 辅助生成：实机多处替换疑问的跨事件复核，同结果的生效规则也作为受控事件。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>保留等价期限后处理的事件／跨零代表；完整终端分支在原生事件入口验证。</summary>
    private static void RomanceCompositeReplacementAuditTests()
    {
        var boundary = new Harmony("ssc.tests.composite.replacement-audit");
        durationPostprocessMode = "equivalent";
        boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
            new[] { typeof(Thought_Memory), typeof(Pawn) }), postfix: new HarmonyMethod(typeof(Program), nameof(DurationPostprocess)));
        try
        {
            // 沿用真实事件助手的关系、父社交记忆及心情断言；长期候选的一般档同时覆盖零／正好感。
            foreach (var (branch, raw) in RepresentativeBranches(RomanceRepresentativeCases.EquivalentReplacements))
                Check($"composite replacement audit equivalent {branch.Id}/{raw}", () =>
                {
                    moodDiagnostics.Clear(); RunCompositeEvent(branch, raw);
                    string line = moodDiagnostics.Single(m => m.Contains("event id="));
                    Require(line.Contains("native=committed") && line.Contains(branch.Value == 0 ? "result=Zero" : "result=Added"));
                });
            CompositeUnchangedDecisionAudit();
            // 被动五类的阶段／特质／零值代表走原判定相同路径，拒婚附带分手仍参与验证。
            foreach (var (branch, raw) in RepresentativeBranches(RomanceRepresentativeCases.SameResultBranches))
                Check($"composite replacement audit same-result branch {branch.Id}/{raw}", () =>
                {
                    moodDiagnostics.Clear(); RunCompositeEvent(branch, raw, samePassiveResult: true);
                    string receipt = moodDiagnostics.Single(m => m.Contains("event id="));
                    bool accepted = (int)branch.Event <= 2;
                    Require(receipt.Contains("native=committed") && receipt.Contains($"original={accepted} accepted={accepted}"));
                });
            CompositeSameResultHistoryAudit();
            CompositeBilateralReplacementAudit();
        }
        finally { boundary.UnpatchAll(boundary.Id); durationPostprocessMode = null; }
    }

    /// <summary>同结果拒绝仍遵守零值、组内保留与原版准入；仅撤销本次新增／续期／容量淘汰。</summary>
    private static void CompositeSameResultHistoryAudit()
    {
        foreach (string mode in new[] { "new", "renew", "capacity", "zero", "updated", "denied", "no-rule" })
            Check("composite replacement audit same-result history " + mode, () =>
            {
                CommandReset(1f); socialOpinionValue = -10;
                var actor = SocialPawn(); var target = SocialSlave(severity: mode == "zero" ? .5f : .9f);
                Comp(target).romanceConfig = new() { allowAcceptRomance = mode == "no-rule" };
                var handler = target.needs.mood.thoughts.memories;
                var native = ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood;
                int limit = native.stackLimit;
                string label = native.stages[0].label;
                var boundary = new Harmony("ssc.tests.composite.same-result-history");
                Thought_Memory oldNative = null; Thought_MemorySSCCompositeRomance oldComposite = null;
                try
                {
                    if (mode is "renew" or "capacity")
                    {
                        native.stackLimit = 1;
                        // 原版按格式化标签分组；保留对象占位，才能让另一对象走容量淘汰而非同组续期。
                        native.stages[0].label = "Had to rebuff {0}";
                        handler.TryGainMemory(native, mode == "renew" ? actor : SocialPawn());
                        oldNative = handler.Memories.Single(); oldNative.age = 54321;
                    }
                    if (mode == "updated") { oldComposite = SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceRejected,actor,stage:3,raw:60); handler.TryGainMemory(oldComposite,actor); oldComposite.age = 765; }
                    if (mode == "denied")
                    {
                        eventDeniedDef = DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
                        boundary.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)),
                            prefix: new HarmonyMethod(typeof(Program), nameof(EventDenyThought)));
                    }
                    moodDiagnostics.Clear(); Require(actor.interactions.TryInteractWith(target, InteractionDefOf.RomanceAttempt));
                    bool preserve = mode is "denied" or "no-rule";
                    Equal(oldNative != null || preserve ? 1 : 0, handler.Memories.Count(m => m.def == native));
                    if (oldNative != null) { Require(handler.Memories.Contains(oldNative)); Equal(54321, oldNative.age); }
                    var composites = handler.Memories.OfType<Thought_MemorySSCCompositeRomance>().ToList();
                    Equal(mode == "zero" || preserve ? 0 : 1, composites.Count);
                    if (oldComposite != null) { Require(!handler.Memories.Contains(oldComposite)); Equal(0, composites.Single().age); Equal(3, composites.Single().Data.Value); }
                    if (mode == "no-rule")
                    { Require(!moodDiagnostics.Any(m => m.Contains("event id="))); Require(moodDiagnostics.Any(m => m.Contains("reason=NaturalUncontrolled"))); }
                    else
                    {
                        string receipt = moodDiagnostics.Single(m => m.Contains("event id="));
                        Require(receipt.Contains("original=False accepted=False"));
                        Require(receipt.Contains(mode == "denied" ? "result=NotAdmitted" : mode == "zero" ? "result=Zero" : mode == "updated" ? "result=Replaced" : "result=Added"));
                        Require(receipt.Contains(preserve ? "native=preserved" : "native=committed"));
                        if (mode == "renew") Require(receipt.Contains("undoRenew=1"));
                        if (mode == "capacity") Require(receipt.Contains("restoreCapacity=1"));
                    }
                    Require(handler.Memories.Any(m => m.def == ThoughtDefOf.FailedRomanceAttemptOnMe && m.otherPawn == actor));
                }
                finally { native.stackLimit = limit; native.stages[0].label = label; boundary.UnpatchAll(boundary.Id); eventDeniedDef = null; }
            });
    }

    /// <summary>重现截图中的同结果拒绝，并覆盖保证下同结果接受：均结算本人综合心情。</summary>
    private static void CompositeUnchangedDecisionAudit()
    {
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
        foreach (bool accepted in new[] { false, true })
        foreach (bool masochist in new[] { false, true })
            Check($"composite replacement audit unchanged {action}/{accepted}/{masochist}", () =>
            {
                CommandReset(accepted ? 0f : 1f, 1f); socialOpinionValue = -10;
                var actor = SocialPawn(); var target = SocialSlave();
                Comp(target).romanceConfig = new()
                { allowAcceptRomance = accepted, allowAcceptProposal = accepted, alwaysAcceptOthers = accepted };
                if (masochist) target.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
                if (action == SSCRomanceAction.Proposal) actor.relations.AddDirectRelation(PawnRelationDefOf.Lover, target);
                moodDiagnostics.Clear(); Require(actor.interactions.TryInteractWith(target, SocialDef(action)));
                var memories = target.needs.mood.thoughts.memories.Memories;
                var kind = action == SSCRomanceAction.Romance ?
                    (accepted ? SSCRomanceCompositeEvent.ForcedRomanceAccepted : SSCRomanceCompositeEvent.ForcedRomanceRejected) :
                    accepted ? SSCRomanceCompositeEvent.ForcedProposalAccepted : SSCRomanceCompositeEvent.ForcedProposalRejected;
                AuditCompositeSide(target, actor, kind, masochist, -10);
                string receipt = moodDiagnostics.Single(m => m.Contains("event id="));
                Require(receipt.Contains("native=committed") && receipt.Contains($"original={accepted} accepted={accepted}"));
                Require(moodDiagnostics.Single(m => m.Contains("finish id=")).Contains("reason=Settled"));
                if (!accepted)
                {
                    string social = action == SSCRomanceAction.Romance ? "FailedRomanceAttemptOnMe" : "IRejectedTheirProposal";
                    Require(memories.Any(m => m.def.defName == social && m.otherPawn == actor));
                    Require(!memories.Any(m => m.def == ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood));
                    Require(actor.needs.mood.thoughts.memories.Memories.Any(m => m.def.defName ==
                        (action == SSCRomanceAction.Romance ? "RebuffedMyRomanceAttemptMood" : "RejectedMyProposalMood")));
                }
            });
    }

    /// <summary>一边命令、一边被控拒绝时各结算本人，零值也须替换本次匹配心情，不吞社交父记忆。</summary>
    private static void CompositeBilateralReplacementAudit()
    {
        foreach (string outcome in new[] { "romance", "proposal-kept", "proposal-split" })
        foreach (bool actorMasochist in new[] { false, true })
        foreach (bool targetMasochist in new[] { false, true })
            Check($"composite replacement audit bilateral {outcome}/{actorMasochist}/{targetMasochist}", () =>
            {
                bool romance = outcome == "romance", split = outcome == "proposal-split";
                var action = romance ? SSCRomanceAction.Romance : SSCRomanceAction.Proposal;
                int raw = romance ? -10 : 30;
                CommandReset(0f, split ? 0f : 1f); socialOpinionValue = raw;
                var (actor, target) = CommandPair(action, targetSlave: true);
                Comp(target).romanceConfig = new() { allowAcceptRomance = false, allowAcceptProposal = false };
                if (actorMasochist) actor.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
                if (targetMasochist) target.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
                moodDiagnostics.Clear(); SubmitAndRun(actor, target, action, out var command);
                Equal(SSCRomanceCommandStatus.Completed, command.Status);
                var actorEvent = romance ? SSCRomanceCompositeEvent.CommandRomanceRejected : split ?
                    SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated : SSCRomanceCompositeEvent.CommandProposalRejected;
                var targetEvent = romance ? SSCRomanceCompositeEvent.ForcedRomanceRejected : split ?
                    SSCRomanceCompositeEvent.ForcedProposalRejectedAndSeparated : SSCRomanceCompositeEvent.ForcedProposalRejected;
                AuditCompositeSide(actor, target, actorEvent, actorMasochist, raw);
                AuditCompositeSide(target, actor, targetEvent, targetMasochist, raw);
                Require(!actor.needs.mood.thoughts.memories.Memories.Any(m => m.def.defName ==
                    (romance ? "RebuffedMyRomanceAttemptMood" : "RejectedMyProposalMood")));
                Require(!target.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood));
                Require(actor.needs.mood.thoughts.memories.Memories.Any(m => m.def ==
                    (romance ? ThoughtDefOf.RebuffedMyRomanceAttempt : ThoughtDefOf.RejectedMyProposal)));
                Require(target.needs.mood.thoughts.memories.Memories.Any(m => m.def ==
                    (romance ? ThoughtDefOf.FailedRomanceAttemptOnMe : ThoughtDefOf.IRejectedTheirProposal)));
                Equal(2, moodDiagnostics.Count(m => m.Contains("event id=") && m.Contains("native=committed")));
            });
    }

    /// <summary>用目录核对实际流水线选中的本人分支、零值与期限，不在断言中再实现事件分类。</summary>
    private static void AuditCompositeSide(Pawn pawn, Pawn other, SSCRomanceCompositeEvent kind, bool masochist, int raw)
    {
        Require(SSCRomanceMoodCatalog.TryResolve(kind, 4, masochist, raw, out var expected));
        var memories = pawn.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().ToList();
        Equal(expected.HasMemory ? 1 : 0, memories.Count);
        if (!expected.HasMemory) return;
        Equal(expected.Branch.Id, memories.Single().Data.BranchId);
        Equal(expected.Branch.Value, memories.Single().Data.Value);
        Equal(expected.DurationTicks, memories.Single().DurationTicks);
        Equal(other, memories.Single().otherPawn);
    }
}
