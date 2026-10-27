// AI 辅助生成：真实 tracker、worker、任务报告与 Scribe；不把占位文案当作正式文案验收。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>验证事实先于心情准入、共享原生结果、只读任务报告及终态反馈去重。</summary>
    private static void RomanceSocialBatch1Tests(Harmony owner, string repo)
    {
        foreach (int number in Enumerable.Range(1, 13))
        {
            int ev = number;
            Check("social facts real event and saved POV " + ev, () =>
            {
                var (actor, target, subject, record) = SocialFactEvent(ev);
                Equal(1, playLogCalls);
                var log = (PlayLogEntry_SSCRomance)moodLastLog;
                var fact = log.BackgroundFromPOV(subject);
                Require(fact.IsValid); Equal((SSCRomanceCompositeEvent)ev, fact.Kind);
                Require(log.BackgroundFromPOV(subject == actor ? target : actor) == null);
                Require(log.BackgroundFromPOV(SocialPawn()) == null);
                Equal(ev >= 6, fact.IsCommand);
                // 查看历史及修改当前开关不重新发放思想、不分配发生顺序，也不变更旧背景。
                var memory = subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().FirstOrDefault();
                long order = memory?.Data.OccurrenceOrder ?? 0;
                Comp(subject).romanceConfig = Denied();
                for (int i = 0; i < 4; i++) Equal(fact.Kind, log.BackgroundFromPOV(subject).Kind);
                Equal(order, memory?.Data.OccurrenceOrder ?? 0); Equal(1, playLogCalls);
                RoundTripSocialLog(log, actor, target, subject, fact.Kind);
                if (record != null) Require(SSCRomanceCommandPresentation.ResultText(record).Contains("[Text pending]"));
            });
            foreach (string mode in new[] { "no-mood", "ssc-denied" })
                Check($"social facts survive mood admission {ev}/{mode}", () =>
                {
                    var boundary = new Harmony("ssc.tests.social-facts.admission");
                    try
                    {
                        if (mode == "ssc-denied") boundary.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)),
                            prefix: new HarmonyMethod(typeof(Program), nameof(SocialDenyComposite)));
                        var (actor, target, subject, record) = SocialFactEvent(ev, (a, b, s) => { if (mode == "no-mood") s.needs.mood = null; });
                        Equal((SSCRomanceCompositeEvent)ev, ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(subject).Kind);
                        Require(subject.needs.mood == null || !subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                        Equal(1, playLogCalls);
                        if (record != null) Equal(SSCRomanceCommandStatus.Completed, record.Status);
                    }
                    finally { boundary.UnpatchAll(boundary.Id); }
                });
        }
        foreach (int ev in new[] { 3, 4, 7, 9, 10 })
            Check("social facts independent of native mood worker hook " + ev, () =>
            {
                var hook = production.GetType("SexSlaveCraft.Harmony_SSCRomanceNativeWorker");
                var transform = AccessTools.Method(hook, "Transpiler");
                var action = ev == 3 || ev == 7 ? SSCRomanceAction.Romance : SSCRomanceAction.Proposal;
                var worker = AccessTools.Method(action == SSCRomanceAction.Romance ? typeof(InteractionWorker_RomanceAttempt) : typeof(InteractionWorker_MarriageProposal), "Interacted");
                owner.Unpatch(worker, transform);
                try
                {
                    var (_, _, subject, record) = SocialFactEvent(ev);
                    Equal((SSCRomanceCompositeEvent)ev, ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(subject).Kind);
                    Require(!subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                    if (record != null) Equal(SSCRomanceCommandStatus.Completed, record.Status);
                }
                finally { owner.CreateClassProcessor(hook).Patch(); }
            });
        Check("social facts one interaction retains both controlled POVs", () =>
        {
            CommandReset(1f); var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
            Comp(b).romanceConfig = new() { alwaysAcceptSexSlave = true };
            SubmitAndRun(a, b, SSCRomanceAction.Romance, out _);
            var log = (PlayLogEntry_SSCRomance)moodLastLog;
            Equal(SSCRomanceCompositeEvent.CommandRomanceAccepted, log.BackgroundFromPOV(a).Kind);
            Equal(SSCRomanceCompositeEvent.ForcedRomanceAccepted, log.BackgroundFromPOV(b).Kind);
            Equal(1, playLogCalls); Equal(1, commandFeedback);
        });
        Check("social facts factory failure keeps real command feedback and log", () =>
        {
            var boundary = CompositeFactoryBoundary();
            compositeReviewBeforeFactory = () => throw new InvalidOperationException("social factory fault");
            try
            {
                var (_, _, subject, record) = SocialFactEvent(7);
                Equal(SSCRomanceCompositeEvent.CommandRomanceRejected, ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(subject).Kind);
                Equal(SSCRomanceCommandStatus.Completed, record.Status); Equal(1, commandFeedback);
                Require(!subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
            }
            finally { compositeReviewBeforeFactory = null; boundary.UnpatchAll(boundary.Id); }
        });
        Check("social facts reject mismatched log factory action", () =>
        {
            var boundary = new Harmony("ssc.tests.social-facts.log-action");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(SSCRomanceMoodEvents), nameof(SSCRomanceMoodEvents.Finish)),
                    postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter)));
                SocialFactEvent(7, (actor, target, _) => eventWorkerCallback = () =>
                {
                    Require(SSCRomanceMoodLog.Create(SSCRomanceDefOf.Breakup, actor, target, new()) is not PlayLogEntry_SSCRomance);
                    Require(SSCRomanceMoodLog.Create(InteractionDefOf.RomanceAttempt, actor, target, new()) is PlayLogEntry_SSCRomance);
                });
                Require(eventWorkerCallback == null); Equal(1, playLogCalls);
            }
            finally { eventWorkerCallback = null; boundary.UnpatchAll(boundary.Id); }
        });
        Check("social facts missing proposal witness does not invent terminal result", () =>
        {
            var worker = AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "Interacted");
            owner.Unpatch(worker, AccessTools.Method(typeof(Harmony_SSCRomanceProposalFacts), "Transpiler"));
            try
            {
                var (_, _, _, record) = SocialFactEvent(10);
                Require(moodLastLog is not PlayLogEntry_SSCRomance);
                Equal(SSCRomanceCommandStatus.Indeterminate, record.Status);
                Equal(SSCRomanceCommandOutcome.Unconfirmed, record.Outcome);
            }
            finally { owner.CreateClassProcessor(typeof(Harmony_SSCRomanceProposalFacts)).Patch(); }
        });
        foreach (string mode in new[] { "ordinary", "active-denial", "no-interaction", "worker-throws", "worker-skipped", "token-changed" })
            Check("social facts no false controlled history " + mode, () =>
            {
                CommandReset(1f); moodLastLog = null; var a = SocialPawn(); var b = SocialSlave();
                Comp(b).romanceConfig = new() { allowAcceptRomance = mode == "ordinary" };
                if (mode == "active-denial") { a = SocialSlave(); Comp(a).romanceConfig = new() { allowInitiateRomance = false }; }
                if (mode == "no-interaction") socialCanInteract = false;
                var boundary = new Harmony("ssc.tests.social-facts.incomplete");
                try
                {
                    if (mode == "worker-skipped") boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "Interacted"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                    if (mode is "worker-throws" or "token-changed")
                    {
                        var original = b;
                        eventWorkerCallback = () =>
                        {
                            if (mode == "worker-throws") throw new InvalidOperationException("social facts worker exception");
                            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(original, out var scope)); scope.Dispose();
                        };
                        boundary.Patch(AccessTools.Method(typeof(SSCRomanceMoodEvents), nameof(SSCRomanceMoodEvents.Observe)), postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter)));
                    }
                    try { a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt); }
                    catch (InvalidOperationException) when (mode == "worker-throws") { }
                    Require(moodLastLog is not PlayLogEntry_SSCRomance);
                }
                finally { boundary.UnpatchAll(boundary.Id); eventWorkerCallback = null; }
            });
        SocialCommandPresentationTests(repo);
    }

    /// <summary>复用现有真实命令夹具构造十三种已确认结果；只提供原生随机和外部世界输入。</summary>
    private static (Pawn actor, Pawn target, Pawn subject, SSCRomanceCommandRecord record) SocialFactEvent(int ev, Action<Pawn, Pawn, Pawn> before = null)
    {
        bool passive = ev <= 5, accepted = ev is 1 or 2 or 6 or 8;
        var action = ev is 1 or 3 or 6 or 7 ? SSCRomanceAction.Romance : ev <= 10 ? SSCRomanceAction.Proposal : SSCRomanceAction.Breakup;
        CommandReset(accepted ? 0f : 1f, ev is 5 or 10 ? 0f : 1f); moodLastLog = null; socialOpinionValue = 0;
        Pawn actor, target, subject;
        if (passive)
        {
            actor = SocialPawn(); target = SocialSlave(severity: .5f); subject = target;
            Comp(target).romanceConfig = new() { allowAcceptRomance = accepted, allowAcceptProposal = accepted, alwaysAcceptOthers = accepted };
            if (action == SSCRomanceAction.Proposal) actor.relations.AddDirectRelation(PawnRelationDefOf.Lover, target);
        }
        else
        {
            (actor, target) = CommandPair(action); subject = actor;
            if (ev >= 12) { actor.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, target); actor.relations.AddDirectRelation(ev == 12 ? PawnRelationDefOf.Fiance : PawnRelationDefOf.Spouse, target); }
        }
        before?.Invoke(actor, target, subject);
        SSCRomanceCommandRecord record = null;
        if (passive) Require(actor.interactions.TryInteractWith(target, SocialDef(action)));
        else SubmitAndRun(actor, target, action, out record);
        return (actor, target, subject, record);
    }

    /// <summary>只阻止 SSC 新记忆的原版准入，实际原生互动、心情和日志构造全部照常执行。</summary>
    private static bool SocialDenyComposite(ThoughtDef def, ref bool __result)
    {
        if (def.defName != Thought_MemorySSCCompositeRomance.ShortDefName && def.defName != Thought_MemorySSCCompositeRomance.LongDefName) return true;
        __result = false; return false;
    }

    /// <summary>原生 Scribe 保存同条日志并恢复原参与者引用；不会重放互动、生成命令或分配心情事件。</summary>
    private static void RoundTripSocialLog(PlayLogEntry_SSCRomance log, Pawn actor, Pawn target, Pawn subject, SSCRomanceCompositeEvent kind)
    {
        var scene = new RomanceMoodScene { Log = log }; string path = Path.Combine(saveDirectory, "social-log-" + (++id) + ".xml");
        Scribe.saver.InitSaving(path, "SocialLog"); Scribe_Deep.Look(ref scene, "scene"); Scribe.saver.FinalizeSaving();
        string xml = File.ReadAllText(path); Require(!xml.Contains("branchId") && !xml.Contains("sscActorEvent") && !xml.Contains("authorization"));
        RomanceMoodScene loaded = null; Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        foreach (var item in new ILoadReferenceable[] { actor, target, player }.Distinct()) directory.RegisterLoaded(item);
        Scribe_Deep.Look(ref loaded, "scene"); Scribe.loader.FinalizeLoading();
        Equal(kind, loaded.Log.BackgroundFromPOV(subject).Kind); Equal(log.LogID, loaded.Log.LogID);
        Require(loaded.Log.Concerns(actor) && loaded.Log.Concerns(target)); Equal(1, playLogCalls);
    }

    /// <summary>通过真实 GetReport 与反馈出口验证展示类别、保存边界、TargetA 解析及重复结束不重复提示。</summary>
    private static void SocialCommandPresentationTests(string repo)
    {
        foreach (int ev in new[] { 6, 8, 11, 12, 13 })
            Check("social command readonly report and terminal feedback " + ev, () =>
            {
                var (actor, target, _, record) = SocialFactEvent(ev, (a, b, _) =>
                {
                    var action = ev == 6 ? SSCRomanceAction.Romance : ev == 8 ? SSCRomanceAction.Proposal : SSCRomanceAction.Breakup;
                    Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, action), out var pending, out var reason), reason);
                    var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
                    string epoch = (string)AccessTools.Field(typeof(CompSexSlaveTraining), "romanceAuthorizationId").GetValue(Comp(a)), expected = SSCRomanceCommandPresentation.Kind(pending).ToString();
                    for (int i = 0; i < 5; i++)
                    {
                        Rand.PushState(91341); int expectedRandom = Rand.Int; Rand.PopState();
                        string report;
                        Rand.PushState(91341);
                        try { report = driver.GetReport(); Equal(expectedRandom, Rand.Int); }
                        finally { Rand.PopState(); }
                        Require(report.Contains("[Text pending]") && !report.Contains("TargetA"));
                        Require(report.Contains(ev == 6 ? "romance" : ev == 8 ? "propose marriage" : ev == 11 ? "break up" : ev == 12 ? "end the engagement" : "divorce"));
                        Equal(SSCRomanceCommandStatus.Pending, pending.Status); Equal(epoch, (string)AccessTools.Field(typeof(CompSexSlaveTraining), "romanceAuthorizationId").GetValue(Comp(a)));
                        Equal(0, bubbleCalls + playLogCalls + commandFeedback);
                    }
                    Equal(expected, SSCRomanceCommandPresentation.Kind(pending).ToString());
                    driver.job.reportStringOverride = "external report"; Equal("external report", driver.GetReport()); driver.job.reportStringOverride = null;
                    // 清除此处仅为报告准备的请求，再由共用夹具创建被测一次互动。
                    ((IEnumerable<Toil>)AccessTools.Method(driver.GetType(), "MakeNewToils").Invoke(driver, null)).ToList();
                    driver.Cleanup(JobCondition.InterruptForced); a.jobs.curJob = null; a.jobs.curDriver = null;
                    CommandReset(0f); socialOpinionValue = 0;
                });
                var display = SSCRomanceCommandPresentation.Kind(record);
                Equal(ev == 6 ? SSCRomanceCommandDisplay.Romance : ev == 8 ? SSCRomanceCommandDisplay.Proposal :
                    ev == 11 ? SSCRomanceCommandDisplay.EndLover : ev == 12 ? SSCRomanceCommandDisplay.EndFiance : SSCRomanceCommandDisplay.Divorce, display);
                string text = SSCRomanceCommandPresentation.ResultText(record);
                Require(text.Contains(("SSC_Romance_CommandAction_" + display).Translate().ToString()));
                Equal(text, commandLastMessage);
                var driver = (JobDriver_SSCRomanceCommand)actor.jobs.curDriver;
                driver.ExecuteOnce(); driver.Cleanup(JobCondition.Succeeded); Equal(1, commandFeedback); Equal(1, playLogCalls);
                string path = SaveCommand(SSCRomanceCommandGameComponent.Current, actor.CurJob);
                var loaded = LoadCommand(path, actor, target, out var store);
                Equal(display, SSCRomanceCommandPresentation.Kind(store.Records.Single()));
                Equal(text, SSCRomanceCommandPresentation.ResultText(store.Records.Single()));
                store.ReconcileAfterLoad(); Equal(1, commandFeedback); Equal(1, playLogCalls);
            });
        foreach (int ev in new[] { 7, 9, 10 })
            Check("social command rejection result distinguishes action and split " + ev, () =>
            {
                var (_, _, _, record) = SocialFactEvent(ev);
                string text = SSCRomanceCommandPresentation.ResultText(record);
                string key = ev == 10 ? "SSC_Romance_Result_RejectedAndSeparated" : "SSC_Romance_Result_Rejected";
                string action = (ev == 7 ? "SSC_Romance_CommandAction_Romance" : "SSC_Romance_CommandAction_Proposal").Translate();
                Equal(key.Translate(record.Stamp.Actor.LabelShort, record.Stamp.Target.LabelShort, action).ToString(), text);
                Equal(text, commandLastMessage); Equal(1, commandFeedback);
            });
        foreach (string mode in new[] { "not-occurred", "interrupted", "unconfirmed" })
            Check("social command actual failure message and no replay " + mode, () =>
            {
                CommandReset(1f); var (a, b) = CommandPair(SSCRomanceAction.Proposal);
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(a, b, SSCRomanceAction.Proposal), out var record, out _));
                var driver = (JobDriver_SSCRomanceCommand)a.jobs.curDriver;
                var boundary = new Harmony("ssc.tests.social-facts.failure-message");
                try
                {
                    if (mode == "not-occurred") socialCanInteract = false;
                    if (mode == "unconfirmed")
                    {
                        eventWorkerCallback = () => throw new InvalidOperationException("unconfirmed worker");
                        boundary.Patch(AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "Interacted"), postfix: new HarmonyMethod(typeof(Program), nameof(EventWorkerAfter)));
                    }
                    ((IEnumerable<Toil>)AccessTools.Method(driver.GetType(), "MakeNewToils").Invoke(driver, null)).ToList();
                    if (mode == "interrupted") { driver.ended = true; driver.Cleanup(JobCondition.InterruptForced); }
                    else { try { driver.ExecuteOnce(); } catch (InvalidOperationException) when (mode == "unconfirmed") { } }
                    // 原版 EndJobWith 在 Cleanup 前先置 ended；夹具保留这一真实顺序而不进入缺地图的预约清理。
                    driver.ended = true; driver.Cleanup(JobCondition.InterruptForced);
                    Equal(mode == "unconfirmed" ? SSCRomanceCommandOutcome.Unconfirmed : SSCRomanceCommandOutcome.NotOccurred, record.Outcome);
                    Require(commandLastMessage.StartsWith(SSCRomanceCommandPresentation.ResultText(record)));
                    Equal(1, commandFeedback); Equal(0, playLogCalls);
                    driver.ExecuteOnce(); Equal(1, commandFeedback); Equal(0, playLogCalls);
                }
                finally { boundary.UnpatchAll(boundary.Id); eventWorkerCallback = null; }
            });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("social command localized placeholders and mirrors " + language, () =>
            {
                var path = Path.Combine("Languages", language, "Keyed/SSC_Romance.xml");
                var runtime = XDocument.Load(Path.Combine(repo, "1.7", path)); var source = XDocument.Load(Path.Combine(repo, "Sexslavecraft", path));
                Equal(runtime.ToString(), source.ToString());
                var entries = runtime.Root.Elements().Where(e => e.Name.LocalName.StartsWith("SSC_Romance_Report_") ||
                    e.Name.LocalName.StartsWith("SSC_Romance_CommandAction_") || e.Name.LocalName.StartsWith("SSC_Romance_Result_")).ToList();
                Equal(17, entries.Count); Equal(17, entries.Select(e => e.Name.LocalName).Distinct().Count());
                Require(entries.All(e => !string.IsNullOrWhiteSpace(e.Value)));
                Require(entries.Where(e => e.Name.LocalName.StartsWith("SSC_Romance_Report_")).All(e => e.Value.Contains("TargetA")));
            });
    }
}
