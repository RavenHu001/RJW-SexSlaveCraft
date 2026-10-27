using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 使用已有互动的结果；此处不得再次发起SocialFactEvent／SocialLogEvent。
    private static void AssertSocialFactAndWrappedSave(int ev, Pawn actor, Pawn target, Pawn subject)
    {
        Equal(1, playLogCalls);
        var log = (PlayLogEntry_SSCRomance)moodLastLog;
        var fact = log.BackgroundFromPOV(subject);
        Require(fact.IsValid); Equal((SSCRomanceCompositeEvent)ev, fact.Kind);
        Require(log.BackgroundFromPOV(subject == actor ? target : actor) == null);
        Require(log.BackgroundFromPOV(SocialPawn()) == null);
        Equal(ev >= 6, fact.IsCommand);
        RoundTripSocialLog(log, actor, target, subject, fact.Kind);
    }

    private static void AssertSocialCommandResult(int ev, Pawn actor, Pawn target, SSCRomanceCommandRecord record)
    {
        if (record != null)
        {
            // 正式文案已替换占位；按本例真实结果及行为核对当前语言，不要求旧占位字面。
            string outcome = ev is 7 or 9 ? "Rejected" : ev == 10 ? "RejectedAndSeparated" : "RelationshipChanged";
            string action = ev is 6 or 7 ? "Romance" : ev <= 10 ? "Proposal" : ev == 11 ? "EndLover" : ev == 12 ? "EndFiance" : "Divorce";
            Equal(("SSC_Romance_Result_" + outcome).Translate(actor.LabelShort, target.LabelShort,
                ("SSC_Romance_CommandAction_" + action).Translate()).ToString(), SSCRomanceCommandPresentation.ResultText(record));
        }
    }

    private static void AssertSocialHistorySave(string repo, int ev, Pawn a, Pawn b, Pawn subject, SSCRomanceCommandRecord record)
    {
        var log = (PlayLogEntry_SSCRomance)moodLastLog;
        string actorText = log.ToGameStringFromPOV(a), targetText = log.ToGameStringFromPOV(b);
        var oldExtras = ((List<RulePackDef>)AccessTools.Field(typeof(PlayLogEntry_Interaction), "extraSentencePacks").GetValue(log)).ToArray();
        var path = Path.Combine(saveDirectory, "social-grammar-history-" + (++id) + ".xml");
        Scribe.saver.InitSaving(path, "SocialGrammar"); Scribe_Deep.Look(ref log, "log"); Scribe.saver.FinalizeSaving();
        string xml = File.ReadAllText(path);
        Require(!xml.Contains("ssc_romance_log_") && !xml.Contains("SSC_RomanceLog_") && !CopyAnyEventTextPresent(repo, xml));
        // 两种根节点都已保存，之后才更改当前状态，核对冻结历史及只读副作用。
        var fact = log.BackgroundFromPOV(subject);
        var memory = subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().FirstOrDefault();
        long order = memory?.Data.OccurrenceOrder ?? 0;
        Comp(subject).romanceConfig = Denied();
        for (int i = 0; i < 4; i++) Equal(fact.Kind, log.BackgroundFromPOV(subject).Kind);
        Equal(order, memory?.Data.OccurrenceOrder ?? 0); Equal(1, playLogCalls);
        AssertSocialCommandResult(ev, a, b, record);
        Comp(subject).pawnIdentity = PawnIdentity.Unset;
        foreach (var p in new[] { a, b }) p.relations.DirectRelations.Clear();
        SSCPersonalityRuntimeUtility.TryBeginMutation(subject, out var mutation); mutation?.Dispose();
        PlayLogEntry_SSCRomance loaded = null;
        Scribe.loader.InitLoading(path); RegisterSocialLogReferences(a, b);
        Scribe_Deep.Look(ref loaded, "log"); Scribe.loader.FinalizeLoading();
        Equal(log.LogID, loaded.LogID); Equal(log.Timestamp, loaded.Timestamp);
        Equal(actorText, loaded.ToGameStringFromPOV(a)); Equal(targetText, loaded.ToGameStringFromPOV(b));
        Require(oldExtras.SequenceEqual((List<RulePackDef>)AccessTools.Field(typeof(PlayLogEntry_Interaction), "extraSentencePacks").GetValue(loaded)));
        Require(loaded.Concerns(a) && loaded.Concerns(b)); Equal(1, playLogCalls);
    }
}
