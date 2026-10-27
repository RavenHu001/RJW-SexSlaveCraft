// AI 辅助生成：真实解析与原版信件的故障窗口复核；不靠改写结果或拼接预期正文取得通过。
using HarmonyLib;
using SexSlaveCraft;
using Verse;
using Verse.Grammar;
using RimWorld;
using System.Xml.Linq;

internal static partial class Program
{
    private static bool socialReviewWarningThrows;
    private static int socialReviewWarnings, socialReviewTranslated, socialReviewUntranslated;

    /// <summary>复核翻译异常、故障诊断及双方历史；运行于已有真实语法和信件夹具的同一边界内。</summary>
    private static void SocialLogBatch2ReviewTests(string repo, Harmony boundary)
    {
        foreach (bool letter in new[] { false, true })
            Check("social grammar review translated exception still falls back to English " + letter, () =>
            {
                var resolve = SocialReviewResolveMethod();
                boundary.Patch(resolve, prefix: new HarmonyMethod(typeof(Program), nameof(SocialReviewTranslationFault)));
                socialReviewTranslated = socialReviewUntranslated = 0;
                try
                {
                    InjectSocialLogLanguage(repo, "English"); SetSocialLanguage("ChineseSimplified");
                    var (_, _, subject, _) = SocialLogEvent(8, letter);
                    var log = (PlayLogEntry_SSCRomance)moodLastLog;
                    if (letter) Require(socialLogLetters.Single().Text.ToString().Contains("SSC text pending E08"));
                    else Require(log.BackgroundTextFromPOV(subject).Contains("SSC text pending E08"));
                    Require(socialReviewTranslated > 0 && socialReviewUntranslated > 0);
                    Equal(1, playLogCalls);
                }
                finally { boundary.Unpatch(resolve, AccessTools.Method(typeof(Program), nameof(SocialReviewTranslationFault))); InjectSocialLogLanguage(repo, "English"); }
            });

        foreach (bool letter in new[] { false, true })
            Check("social grammar review warning sink failure cannot block native display " + letter, () =>
            {
                var pack = DefDatabase<RulePackDef>.GetNamed("SSC_RomanceLog_Command");
                var seen = SocialReviewWarningsSet(); var prior = seen.ToArray(); seen.Clear();
                var previous = SSCMod.settings;
                boundary.Patch(AccessTools.Method(typeof(SSCLog), nameof(SSCLog.WarningImportant)),
                    prefix: new HarmonyMethod(typeof(Program), nameof(SocialReviewWarningSink)));
                socialReviewWarningThrows = true; socialReviewWarnings = 0;
                try
                {
                    SSCMod.settings = new SSCSettings { enableSSCLogs = true, enableSSCImportantLogs = true };
                    SetSocialRulePack(pack, new XElement("rulePack", new XElement("rulesStrings")));
                    var (a, b, subject, record) = SocialLogEvent(8, letter);
                    var log = (PlayLogEntry_SSCRomance)moodLastLog;
                    var native = PlainSocialLog(log, a, b).ToGameStringFromPOV(a);
                    if (letter) Require(socialLogLetters.Single().Text.ToString().StartsWith(native + "\n\n"));
                    Equal("", log.BackgroundTextFromPOV(subject));
                    Equal(native, log.ToGameStringFromPOV(a, true));
                    Equal(SSCRomanceCommandStatus.Completed, record.Status); Equal(1, commandFeedback); Equal(1, playLogCalls);
                    Require(socialReviewWarnings > 0);
                }
                finally
                {
                    socialReviewWarningThrows = false;
                    boundary.Unpatch(AccessTools.Method(typeof(SSCLog), nameof(SSCLog.WarningImportant)), AccessTools.Method(typeof(Program), nameof(SocialReviewWarningSink)));
                    SSCMod.settings = previous; seen.Clear(); seen.UnionWith(prior); InjectSocialLogLanguage(repo, "English");
                }
            });

        foreach (bool importantGate in new[] { false, true })
            Check("social grammar review disabled warning gate does not consume diagnostic " + importantGate, () =>
            {
                var pack = DefDatabase<RulePackDef>.GetNamed("SSC_RomanceLog_Command");
                var seen = SocialReviewWarningsSet(); var prior = seen.ToArray(); seen.Clear();
                var previous = SSCMod.settings;
                boundary.Patch(AccessTools.Method(typeof(SSCLog), nameof(SSCLog.WarningImportant)),
                    prefix: new HarmonyMethod(typeof(Program), nameof(SocialReviewWarningCount)));
                try
                {
                    SSCMod.settings = new SSCSettings { enableSSCLogs = importantGate, enableSSCImportantLogs = !importantGate };
                    SetSocialRulePack(pack, new XElement("rulePack", new XElement("rulesStrings")));
                    var (_, _, subject, _) = SocialLogEvent(7); var log = (PlayLogEntry_SSCRomance)moodLastLog;
                    socialReviewWarnings = 0; Equal("", log.BackgroundTextFromPOV(subject)); Equal(0, socialReviewWarnings);
                    SSCMod.settings.enableSSCLogs = SSCMod.settings.enableSSCImportantLogs = true;
                    Equal("", log.BackgroundTextFromPOV(subject)); Equal(1, socialReviewWarnings);
                    Equal("", log.BackgroundTextFromPOV(subject)); Equal(1, socialReviewWarnings);
                }
                finally
                {
                    boundary.Unpatch(AccessTools.Method(typeof(SSCLog), nameof(SSCLog.WarningImportant)), AccessTools.Method(typeof(Program), nameof(SocialReviewWarningCount)));
                    SSCMod.settings = previous; seen.Clear(); seen.UnionWith(prior); InjectSocialLogLanguage(repo, "English");
                }
            });
        SocialLogReviewHistoryTests(repo);
    }

    /// <summary>准确选择具有 success 回执的原生重载，不拦截所有日志正文的解析入口。</summary>
    private static System.Reflection.MethodInfo SocialReviewResolveMethod()
        => AccessTools.Method(typeof(GrammarResolver), "ResolveUnsafe", new[] { typeof(string), typeof(GrammarRequest), typeof(bool).MakeByRefType(), typeof(string), typeof(bool), typeof(bool), typeof(List<string>), typeof(List<string>), typeof(bool) });

    /// <summary>只让 SSC 本地化解析异常；原文和英文解析继续执行真实原版。</summary>
    private static void SocialReviewTranslationFault(string rootKeyword, bool useUntranslatedRules)
    {
        if (!rootKeyword.StartsWith("ssc_romance_log_")) return;
        if (useUntranslatedRules) { socialReviewUntranslated++; return; }
        socialReviewTranslated++; throw new InvalidOperationException("localized grammar review fault");
    }

    /// <summary>保留跨用例的原去重集合，临时重置仅用于验证诊断窗口，不修改路由或解析规则。</summary>
    private static HashSet<string> SocialReviewWarningsSet()
        => (HashSet<string>)AccessTools.Field(production.GetType("SexSlaveCraft.SSCRomanceLogGrammar"), "warned").GetValue(null);

    /// <summary>模拟外部诊断接收器故障，必须不能外溢到日志、信件或命令结算。</summary>
    private static void SocialReviewWarningSink(string message)
    { if (!message.Contains("日志背景回退")) return; socialReviewWarnings++; if (socialReviewWarningThrows) throw new InvalidOperationException("warning sink review fault"); }

    /// <summary>观察真实 SSC 警告出口调用，不以私有去重集合数量代替诊断请求；引擎投递仍由宿主隔离。</summary>
    private static void SocialReviewWarningCount(string message)
    { if (message.Contains("日志背景回退")) socialReviewWarnings++; }

    /// <summary>历史补充验证入口；下方用真实双方事实和 Scribe，而不只检查单端字段存在。</summary>
    private static void SocialLogReviewHistoryTests(string repo)
    {
        Check("social grammar review both saved POVs stay distinct while old letter keeps text", () =>
        {
            CommandReset(1f, 0f); socialLogNotifications = true; socialLogLetters.Clear();
            var (a, b) = CommandPair(SSCRomanceAction.Proposal, true); PrepareSocialLogPawn(a); PrepareSocialLogPawn(b);
            Comp(b).romanceConfig = new() { allowAcceptProposal = false };
            SubmitAndRun(a, b, SSCRomanceAction.Proposal, out _);
            var log = (PlayLogEntry_SSCRomance)moodLastLog; ChoiceLetter letter = socialLogLetters.Single();
            int logID = log.LogID, timestamp = log.Timestamp, messages = commandFeedback;
            string oldText = letter.Text.ToString().Replace("\r\n", "\n");
            var path = Path.Combine(saveDirectory, "social-review-dual-history-" + (++id) + ".xml");
            Scribe.saver.InitSaving(path, "SocialReviewHistory");
            Scribe_Deep.Look(ref log, "log"); Scribe_Deep.Look(ref letter, "letter"); Scribe.saver.FinalizeSaving();
            foreach (var pawn in new[] { a, b })
            {
                Comp(pawn).pawnIdentity = PawnIdentity.Unset; Comp(pawn).romanceConfig = new();
                pawn.health.hediffSet.hediffs.Clear(); pawn.relations.DirectRelations.Clear();
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var mutation)); mutation.Dispose();
            }
            PlayLogEntry_SSCRomance loaded = null; ChoiceLetter loadedLetter = null;
            Scribe.loader.InitLoading(path); RegisterSocialLogReferences(a, b);
            Scribe_Deep.Look(ref loaded, "log"); Scribe_Deep.Look(ref loadedLetter, "letter"); Scribe.loader.FinalizeLoading();
            try
            {
                foreach (var language in new[] { "ChineseSimplified", "Russian", "English" })
                {
                    InjectSocialLogLanguage(repo, language);
                    string first = loaded.ToGameStringFromPOV(a, true), second = loaded.ToGameStringFromPOV(b, true);
                    if (language == "ChineseSimplified")
                    {
                        string actorText = ChineseCopyLogExpected(repo, 10, a, b), recipientText = ChineseCopyLogExpected(repo, 5, a, b);
                        string actorView = ChineseCopyPlainText(first), recipientView = ChineseCopyPlainText(second);
                        Require(actorView.Contains(actorText) && !actorView.Contains(recipientText));
                        Require(recipientView.Contains(recipientText) && !recipientView.Contains(actorText));
                    }
                    else
                    {
                        Require(first.Contains("E10") && !first.Contains("E05"));
                        Require(second.Contains("E05") && !second.Contains("E10"));
                    }
                    Equal(oldText, loadedLetter.Text.ToString().Replace("\r\n", "\n"));
                }
            }
            finally { InjectSocialLogLanguage(repo, "English"); }
            Equal(logID, loaded.LogID); Equal(timestamp, loaded.Timestamp);
            Equal(1, playLogCalls); Equal(1, socialLogLetters.Count); Equal(messages, commandFeedback);
        });
        Check("social grammar review rule order cannot change event selection", () =>
        {
            var (_, _, subject, _) = SocialLogEvent(7); var log = (PlayLogEntry_SSCRomance)moodLastLog;
            var pack = DefDatabase<RulePackDef>.GetNamed("SSC_RomanceLog_Command");
            var node = XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_RomanceLogs.xml")).Root.Elements().Single(n => n.Element("defName").Value == pack.defName).Element("rulePack");
            var rules = node.Element("rulesStrings"); var list = rules.Elements().Reverse().ToArray(); rules.ReplaceNodes(list);
            try { SetSocialRulePack(pack, node); Require(log.BackgroundTextFromPOV(subject).Contains("E07")); }
            finally { InjectSocialLogLanguage(repo, "English"); }
        });
    }
}
