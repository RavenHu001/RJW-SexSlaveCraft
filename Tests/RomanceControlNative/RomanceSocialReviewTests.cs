// AI 辅助生成：提交前复核真实关系结果与新增保存事实的完整性，不迁移未发布旧格式。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using System.Reflection.Emit;
using System.Xml.Linq;
using Verse;

internal static partial class Program
{
    private static bool socialReviewConfirming, socialReviewCapturing;
    private static Pawn socialReviewPairActor, socialReviewPairTarget;
    private static Action socialReviewPairMutation;
    /// <summary>启动时安装查询回调；实际破坏仍由用例的采集／确认窗口及唯一配对限定。</summary>
    private static void SetupSocialReviewQueryBoundaries(Harmony owner)
    {
        owner.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.OpinionOf)),
            postfix: new HarmonyMethod(typeof(Program), nameof(SocialReviewOpinionChanged)));
        owner.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.DirectRelationExists)),
            postfix: new HarmonyMethod(typeof(Program), nameof(SocialReviewRelationChanged)));
    }
    /// <summary>第三方回调与原生 Scribe 破坏输入：未知结果不冒充拒绝，历史事实不成为下一次执行依据。</summary>
    private static void RomanceSocialReviewTests()
    {
        foreach (bool capture in new[] { false, true })
            Check("social review relation callback cannot publish mixed pair evidence " + capture, () =>
            {
                var boundary = new Harmony("ssc.tests.social-review.pair-evidence");
                try
                {
                    if (capture)
                    {
                        boundary.Patch(AccessTools.Method(typeof(SSCRomanceMoodEvents), nameof(SSCRomanceMoodEvents.Capture)),
                            prefix: new HarmonyMethod(typeof(Program), nameof(SocialReviewCaptureStart)), finalizer: new HarmonyMethod(typeof(Program), nameof(SocialReviewCaptureEnd)));
                    }
                    else
                    {
                        boundary.Patch(AccessTools.Method(production.GetType("SexSlaveCraft.SSCRomanceInteractionFacts"), "TryConfirm"),
                            prefix: new HarmonyMethod(typeof(Program), nameof(SocialReviewConfirmStart)), finalizer: new HarmonyMethod(typeof(Program), nameof(SocialReviewConfirmEnd)));
                    }
                    var (_, _, subject, record) = SocialFactEvent(capture ? 11 : 7, (a, b, _) =>
                    {
                        socialReviewPairActor = a; socialReviewPairTarget = b;
                        socialReviewPairMutation = () =>
                        {
                            if (capture) a.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, b);
                            a.relations.AddDirectRelation(PawnRelationDefOf.Spouse, b);
                        };
                    });
                    Require(socialReviewPairMutation == null); Equal(SSCRomanceCommandStatus.Indeterminate, record.Status);
                    Require(moodLastLog is not PlayLogEntry_SSCRomance);
                    Require(!subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                }
                finally
                {
                    boundary.UnpatchAll(boundary.Id); socialReviewConfirming = socialReviewCapturing = false;
                    socialReviewPairActor = socialReviewPairTarget = null; socialReviewPairMutation = null;
                }
            });
        foreach (var relation in new[] { PawnRelationDefOf.Fiance, PawnRelationDefOf.Spouse })
            Check("social review rejected romance with unexpected final relationship " + relation.defName, () =>
            {
                var boundary = new Harmony("ssc.tests.social-review.final-relation");
                try
                {
                    boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"),
                        postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter)));
                    var (_, _, owner, record) = SocialFactEvent(7, (a, b, _) => eventWorkerCallback = () => a.relations.AddDirectRelation(relation, b));
                    Equal(SSCRomanceCommandStatus.Indeterminate, record.Status);
                    Equal(SSCRomanceCommandOutcome.Unconfirmed, record.Outcome);
                    Require(moodLastLog is not PlayLogEntry_SSCRomance);
                    Require(!owner.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                }
                finally { eventWorkerCallback = null; boundary.UnpatchAll(boundary.Id); }
            });

        foreach (string mode in new[] { "missing", "unknown", "invalid-before", "passive", "wrong-action", "wrong-outcome" })
            Check("social review saved completed fact rejected " + mode, () =>
            {
                var (a, b, _, _) = SocialFactEvent(7);
                string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
                Edit(path, doc =>
                {
                    var row = doc.Descendants("sscRomanceCommands").Elements("li").Single();
                    var fact = row.Element("interactionFact");
                    row.Element("feedbackSent").Value = "False";
                    if (mode == "missing") fact.Remove();
                    else if (mode == "unknown") fact.Element("kind").Value = "999";
                    else if (mode == "invalid-before") fact.Add(new XElement("before", "Spouse"));
                    else if (mode == "passive") fact.Element("kind").Value = "ForcedRomanceRejected";
                    else if (mode == "wrong-action")
                    { fact.Element("kind").Value = "CommandProposalRejectedAndSeparated"; fact.Add(new XElement("before", "Lover")); }
                    else row.Element("outcome").Value = "RelationshipChanged";
                });
                var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
                var saved = store.Records.Single(); Equal(SSCRomanceCommandStatus.Indeterminate, saved.Status);
                Equal(SSCRomanceCommandOutcome.Unconfirmed, saved.Outcome);
                Require(!SSCRomanceCommandPresentation.ResultText(saved).Contains("proposed marriage"));
                Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null);
            });
        Check("social review pending saved fact cannot be resumed as a new event", () =>
        {
            var (oldA, _, _, _) = SocialFactEvent(7);
            string history = SaveCommand(SSCRomanceCommandGameComponent.Current, oldA.CurJob);
            var fact = new XElement(XDocument.Load(history).Descendants("interactionFact").Single());
            CommandReset(1f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out _, out _));
            string path = SaveCommand(SSCRomanceCommandGameComponent.Current, a.CurJob);
            Edit(path, doc =>
            {
                var row = doc.Descendants("sscRomanceCommands").Elements("li").Single();
                row.Element("interactionFact")?.Remove(); row.Add(fact);
            });
            var loaded = LoadCommand(path, a, b, out var store); BindCommand(a, loaded.Job); store.ReconcileAfterLoad();
            Equal(SSCRomanceCommandStatus.Indeterminate, store.Records.Single().Status);
            Require(store.Claim((JobDriver_SSCRomanceCommand)a.jobs.curDriver) == null); Equal(0, playLogCalls);
        });
        Check("social review new attempt cannot reuse a runtime historical fact", () =>
        {
            var (_, _, oldOwner, _) = SocialFactEvent(7);
            var oldFact = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(oldOwner);
            CommandReset(1f); var (a, b) = CommandPair(SSCRomanceAction.Romance);
            Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b), out var record, out _));
            AccessTools.Field(typeof(SSCRomanceCommandRecord), "interactionFact").SetValue(record, oldFact);
            var boundary = new Harmony("ssc.tests.social-review.stale-runtime-fact");
            try
            {
                eventWorkerCallback = () => a.relations.AddDirectRelation(PawnRelationDefOf.Spouse, b);
                boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter)));
                ((JobDriver_SSCRomanceCommand)a.jobs.curDriver).ExecuteOnce();
                Equal(SSCRomanceCommandStatus.Indeterminate, record.Status);
                Require(AccessTools.Field(typeof(SSCRomanceCommandRecord), "interactionFact").GetValue(record) == null);
            }
            finally { eventWorkerCallback = null; boundary.UnpatchAll(boundary.Id); }
        });
        Check("social review result formatter never attributes another action to a saved fact", () =>
        {
            var (_, _, _, wrong) = SocialFactEvent(10);
            var wrongFact = AccessTools.Field(typeof(SSCRomanceCommandRecord), "interactionFact").GetValue(wrong);
            var (_, _, _, record) = SocialFactEvent(7);
            AccessTools.Field(typeof(SSCRomanceCommandRecord), "interactionFact").SetValue(record, wrongFact);
            Require(!SSCRomanceCommandPresentation.ResultText(record).Contains("proposed marriage"));
        });
        foreach (bool wrongRole in new[] { false, true })
            Check("social review log POV rejects mismatched role or action " + wrongRole, () =>
            {
                var (_, _, passiveOwner, _) = SocialFactEvent(3);
                var passive = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(passiveOwner);
                var (a, b, actorOwner, _) = SocialFactEvent(wrongRole ? 7 : 9);
                var active = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(actorOwner);
                var log = new PlayLogEntry_SSCRomance(InteractionDefOf.RomanceAttempt, a, b, new(), wrongRole ? passive : active, wrongRole ? active : null);
                Require(log.BackgroundFromPOV(a) == null && log.BackgroundFromPOV(b) == null);
            });
        foreach (bool duplicate in new[] { false, true })
            Check("social review proposal witness IL mismatch leaves native sequence " + duplicate, () =>
            {
                var original = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "Interacted")).ToList();
                var add = AccessTools.Method(typeof(List<RulePackDef>), "Add");
                var input = duplicate ? original.Concat(new[] { new CodeInstruction(OpCodes.Call, add) }).ToList() : original.Where(c => !c.Calls(add)).ToList();
                try
                {
                    var output = Harmony_SSCRomanceProposalFacts.Transpiler(input).ToList();
                    Equal(input.Count, output.Count); Equal(input.Count(c => c.Calls(add)), output.Count(c => c.Calls(add)));
                    Require(!output.Any(c => c.Calls(AccessTools.Method(typeof(SSCRomanceMoodEvents), "ObserveProposalPack"))));
                }
                finally { Harmony_SSCRomanceProposalFacts.Transpiler(original).ToList(); }
            });
    }

    /// <summary>限定破坏输入只发生在共用确认器的真实关系查询窗口，不改写原 worker 的接受 bool。</summary>
    private static void SocialReviewConfirmStart() => socialReviewConfirming = true;
    private static void SocialReviewConfirmEnd() => socialReviewConfirming = false;
    /// <summary>限定破坏输入只发生在原入口快照采集，不干预菜单或策略查询。</summary>
    private static void SocialReviewCaptureStart() => socialReviewCapturing = true;
    private static void SocialReviewCaptureEnd() => socialReviewCapturing = false;
    /// <summary>第二方向的最后一次旧查询返回后改变真实关系，复现查询结果与实际列表错开。</summary>
    private static void SocialReviewRelationChanged(Pawn_RelationsTracker __instance, PawnRelationDef __0, Pawn __1)
    {
        if (!socialReviewConfirming || __instance != socialReviewPairTarget?.relations || __1 != socialReviewPairActor || __0 != PawnRelationDefOf.Lover) return;
        var mutation = socialReviewPairMutation; socialReviewPairMutation = null; mutation?.Invoke();
    }
    /// <summary>采集第二人的好感时切换原关系，让两端原关系快照不一致。</summary>
    private static void SocialReviewOpinionChanged(Pawn_RelationsTracker __instance)
    {
        if (!socialReviewCapturing || __instance != socialReviewPairTarget?.relations) return;
        var mutation = socialReviewPairMutation; socialReviewPairMutation = null; mutation?.Invoke();
    }
}
