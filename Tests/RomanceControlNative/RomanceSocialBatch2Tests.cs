// AI 辅助生成：真实事件、原生语法与语言注入；地图通知仅在最终接收端截取，不能替代正文生成。
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.IO;
using SexSlaveCraft;
using Verse;
using Verse.Grammar;

internal static partial class Program
{
    private static bool socialLogNotifications;
    private static FactionManager socialLogFactions;
    private static LoadedLanguage socialLogEnglish;
    private static readonly List<ChoiceLetter> socialLogLetters = new();

    /// <summary>第二批放在既有专项之后，装载真实语法；第一批夹具不再代表实际日志文字已经被解析。</summary>
    private static void RomanceSocialBatch2Tests(Harmony owner, string repo, string game)
    {
        SetupSocialLogGrammar(repo, game);
        var boundary = new Harmony("ssc.tests.romance-log-grammar");
        boundary.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.FactionManager)),
            prefix: new HarmonyMethod(typeof(Program), nameof(SocialLogFactionManager)));
        boundary.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MaxHitPoints)),
            prefix: new HarmonyMethod(typeof(Program), nameof(SocialLogHitPoints)));
        boundary.Patch(AccessTools.Method(typeof(SSCRomanceMoodEvents), nameof(SSCRomanceMoodEvents.Finish)),
            postfix: new HarmonyMethod(typeof(Program), nameof(SocialLogWorkerFinished)));
        foreach (var warning in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == "Warning" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
            boundary.Patch(warning, prefix: new HarmonyMethod(typeof(Program), nameof(SocialGrammarWarning)));
        var notification = AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.ShouldSendNotificationAbout));
        owner.Unpatch(notification, AccessTools.Method(typeof(Program), nameof(NoCooldown)));
        boundary.Patch(notification, prefix: new HarmonyMethod(typeof(Program), nameof(SocialLogNotify)));
        boundary.Patch(AccessTools.GetDeclaredMethods(typeof(LetterStack)).Single(m => m.Name == "ReceiveLetter" && m.GetParameters()[0].ParameterType == typeof(Letter)),
            prefix: new HarmonyMethod(typeof(Program), nameof(SocialLogReceive)));
        try
        {
            foreach (int ev in Enumerable.Range(1, 13))
                Check("social grammar actual event POV and native results " + ev, () =>
                {
                    var (a, b, subject, record) = SocialLogEvent(ev);
                    var log = (PlayLogEntry_SSCRomance)moodLastLog;
                    var background = log.BackgroundTextFromPOV(subject);
                    Require(CopyEventTextPresent(repo, background, ev), background);
                    Require(background.Contains(a.Name.ToStringShort) && background.Contains(b.Name.ToStringShort), background);
                    Equal("", log.BackgroundTextFromPOV(subject == a ? b : a));
                    Equal("", log.BackgroundTextFromPOV(SocialPawn()));
                    var plain = PlainSocialLog(log, a, b);
                    foreach (var pov in new[] { a, b })
                    {
                        var original = plain.ToGameStringFromPOV(pov);
                        var actual = log.ToGameStringFromPOV(pov);
                        Equal(pov == subject ? original + " " + background : original, actual);
                    }
                    Equal(1, playLogCalls); Equal(0, socialLogLetters.Count);
                    AssertSocialFactAndWrappedSave(ev, a, b, subject);
                    AssertSocialHistorySave(repo, ev, a, b, subject, record);
                });
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "Russian", "English" })
                Check("social grammar actual DefInjected and thirteen roots " + language, () =>
                {
                    InjectSocialLogLanguage(repo, language);
                    foreach (int ev in Enumerable.Range(1, 13))
                    {
                        var (a, b, subject, _) = SocialLogEvent(ev);
                        var text = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundTextFromPOV(subject);
                        Equal(CopyLogExpected(repo, language, ev, a, b), CopyPlainText(text));
                    }
                    var rel = $"Languages/{language}/DefInjected/RulePackDef/SSC_RomanceLogs.xml";
                    Require(File.ReadAllBytes(Path.Combine(repo, "1.7", rel)).SequenceEqual(File.ReadAllBytes(Path.Combine(repo, "Sexslavecraft", rel))));
                });
            SocialLogHistoryAndFeedbackTests(repo, boundary);
            SocialLogBatch2ReviewTests(repo, boundary);
        }
        finally
        {
            socialLogNotifications = false;
            boundary.UnpatchAll(boundary.Id);
            owner.Patch(notification, prefix: new HarmonyMethod(typeof(Program), nameof(NoCooldown)));
        }
    }

    /// <summary>原生 XML 反序列化生产三包及 Core 正文／结果；通用词汇只提供六个无机制含义的词汇叶子。</summary>
    private static void SetupSocialLogGrammar(string repo, string game)
    {
        socialLogEnglish = LanguageDatabase.defaultLanguage;
        foreach (var node in XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_RomanceLogs.xml")).Root.Elements())
            DefDatabase<RulePackDef>.Add(SocialLogXml<RulePackDef>(node));
        RulePackDefOf.GlobalUtility = new RulePackDef { defName = "SocialLogFixtureGlobalUtility" };
        SetSocialRulePack(RulePackDefOf.GlobalUtility, new XElement("rulePack", new XElement("rulesStrings",
            new[] { "PersonalCharacteristic", "TalkTopicAny", "AdjectiveBadass", "TerrainFeature", "NaturalObject", "TreeType" }.Select(k => new XElement("li", k + "->fixture vocabulary")))));
        var core = XDocument.Load(Path.Combine(game, "Data/Core/Defs/InteractionDefs/Interactions_Romance.xml"));
        foreach (var node in core.Root.Elements())
        {
            var name = node.Element("defName").Value;
            if (node.Name == "InteractionDef")
                DefDatabase<InteractionDef>.GetNamed(name).logRulesInitiator = SocialLogXml<RulePack>(node.Element("logRulesInitiator"));
            else SetSocialRulePack(DefDatabase<RulePackDef>.GetNamed(name), node.Element("rulePack"));
        }
        // 补齐语法人物信息和原信件所需的原生记录；不替换 RulesForPawn 或 GrammarResolver。
        player.Name = "SocialLogFixtureColony";
        // 完整游戏由 ModContentPack 填载入目录；宿主的裸包在此指向真实 Core 英语词形资料。
        foreach (var mod in LoadedModManager.RunningMods)
            AccessTools.Field(typeof(ModContentPack), "foldersToLoadDescendingOrder").SetValue(mod, new List<string> { Path.Combine(game, "Data/Core") });
        human.useHitPoints = false; human.label = "human";
        socialLogFactions = new FactionManager(); socialLogFactions.AllFactionsListForReading.Add(player);
        foreach (var field in typeof(RecordDefOf).GetFields().Where(f => f.FieldType == typeof(RecordDef)))
        { var d = new RecordDef { defName = field.Name }; DefDatabase<RecordDef>.Add(d); field.SetValue(null, d); }
        AccessTools.Field(typeof(Game), "letterStack").SetValue(Current.Game, new LetterStack());
        foreach (var name in new[] { "PositiveEvent", "NegativeEvent" })
        {
            var def = new LetterDef { defName = name, letterClass = typeof(StandardLetter) };
            DefDatabase<LetterDef>.Add(def); AccessTools.Field(typeof(LetterDefOf), name).SetValue(null, def);
        }
        foreach (var file in Directory.EnumerateFiles(Path.Combine(game, "Data/Core/Languages/English/Keyed"), "*.xml"))
            foreach (var node in XDocument.Load(file).Root.Elements())
                LanguageDatabase.activeLanguage.keyedReplacements.TryAdd(node.Name.LocalName,
                    new LoadedLanguage.KeyedReplacement { key = node.Name.LocalName, value = node.Value });
    }

    /// <summary>只提供原生人物的展示资料，使用同一真实事件夹具；可选择打开原版信件资格。</summary>
    private static (Pawn actor, Pawn target, Pawn subject, SSCRomanceCommandRecord record) SocialLogEvent(int ev, bool notify = false, Action<Pawn, Pawn, Pawn> before = null)
    {
        socialLogNotifications = notify; socialLogLetters.Clear();
        var result = SocialFactEvent(ev, (a, b, subject) =>
        {
            PrepareSocialLogPawn(a); PrepareSocialLogPawn(b);
            before?.Invoke(a, b, subject);
        });
        // 行为输入只供该次 worker；未消费的拒婚随机不可被后续显示语法误用。
        socialRolls.Clear(); return result;
    }

    /// <summary>语法所需最小真实人物资料，不替换人物规则或最终文案生成。</summary>
    private static void PrepareSocialLogPawn(Pawn p)
    {
        p.kindDef = new PawnKindDef { defName = "SocialLogFixtureKind", label = "colonist", race = human };
        p.records = new Pawn_RecordsTracker(p);
    }

    /// <summary>历史与信件使用真实保存及 tracker 输出；显示查询不能产生任何互动或结算副作用。</summary>
    private static void SocialLogHistoryAndFeedbackTests(string repo, Harmony boundary)
    {
        foreach (int ev in Enumerable.Range(1, 13))
        {
            Check("social grammar actual native letter and saved text " + ev, () =>
            {
                var (a, b, _, record) = SocialLogEvent(ev, true);
                if (ev is 3 or 7) { Equal(0, socialLogLetters.Count); return; }
                Equal(1, socialLogLetters.Count);
                var log = (PlayLogEntry_SSCRomance)moodLastLog; var letter = socialLogLetters.Single();
                string text = letter.Text.ToString();
                Require(text.StartsWith(log.ToGameStringFromPOV(a) + "\n\n"), text);
                Equal(ev >= 6, CopyAnyEventTextPresent(repo, text));
                Equal(ev is 1 or 2 or 6 or 8 ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, letter.def);
                Require(letter.lookTargets.targets.Any(t => t.Thing == a));
                if (ev is not 1 and not 6) Require(letter.lookTargets.targets.Any(t => t.Thing == b));
                Require(!string.IsNullOrEmpty(letter.Label.ToString()));
                var path = Path.Combine(saveDirectory, "social-grammar-letter-" + (++id) + ".xml");
                Scribe.saver.InitSaving(path, "SocialLetter"); Scribe_Deep.Look(ref letter, "letter"); Scribe.saver.FinalizeSaving();
                ChoiceLetter loaded = null; Scribe.loader.InitLoading(path); RegisterSocialLogReferences(a, b);
                Scribe_Deep.Look(ref loaded, "letter"); Scribe.loader.FinalizeLoading();
                // 原生 XML 把 Windows 换行标准化为 LF；按正文比较，不把原版换行规则误判为文字变化。
                Equal(text.Replace("\r\n", "\n"), loaded.Text.ToString().Replace("\r\n", "\n"));
                Equal(letter.Label.ToString(), loaded.Label.ToString()); Equal(letter.def, loaded.def);
                if (record != null) Require(record.Status == SSCRomanceCommandStatus.Completed);
                Equal(1, socialLogLetters.Count); Equal(1, playLogCalls);
            });
        }
        foreach (int ev in new[] { 6, 7, 8, 9, 10 })
            Check("social grammar both controlled POVs and initiator letter " + ev, () =>
            {
                CommandReset(ev is 6 or 8 ? 0f : 1f, ev == 10 ? 0f : 1f);
                socialLogNotifications = true; socialLogLetters.Clear();
                var action = ev <= 7 ? SSCRomanceAction.Romance : SSCRomanceAction.Proposal;
                var (a, b) = CommandPair(action, true); PrepareSocialLogPawn(a); PrepareSocialLogPawn(b);
                Comp(b).romanceConfig = new() { allowAcceptRomance = false, allowAcceptProposal = false, alwaysAcceptSexSlave = ev is 6 or 8 };
                SubmitAndRun(a, b, action, out _); socialRolls.Clear();
                var log = (PlayLogEntry_SSCRomance)moodLastLog;
                int passive = ev == 6 ? 1 : ev == 7 ? 3 : ev == 8 ? 2 : ev == 9 ? 4 : 5;
                var first = log.ToGameStringFromPOV(a); var second = log.ToGameStringFromPOV(b);
                Require(CopyEventTextPresent(repo, first, ev) && !CopyEventTextPresent(repo, first, passive));
                Require(CopyEventTextPresent(repo, second, passive) && !CopyEventTextPresent(repo, second, ev));
                Equal(first, log.ToGameStringFromPOV(a)); Equal(second, log.ToGameStringFromPOV(b));
                Equal(1, playLogCalls); Equal(1, commandFeedback);
                if (ev == 7) Equal(0, socialLogLetters.Count);
                else { Require(CopyEventTextPresent(repo, socialLogLetters.Single().Text.ToString(), ev)); Require(!CopyEventTextPresent(repo, socialLogLetters.Single().Text.ToString(), passive)); }
            });
        foreach (int ev in Enumerable.Range(1, 13))
        foreach (bool denied in new[] { false, true })
            Check($"social grammar render survives mood admission {ev}/{denied}", () =>
            {
                if (denied) boundary.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)), prefix: new HarmonyMethod(typeof(Program), nameof(SocialDenyComposite)));
                try
                {
                    var (_, _, subject, record) = SocialLogEvent(ev, false, (a, b, s) => { if (!denied) s.needs.mood = null; });
                    var fact = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(subject);
                    Equal((SSCRomanceCompositeEvent)ev, fact.Kind);
                    Require(subject.needs.mood == null || !subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                    Equal(1, playLogCalls);
                    if (record != null) Equal(SSCRomanceCommandStatus.Completed, record.Status);
                    Require(CopyEventTextPresent(repo, ((PlayLogEntry_SSCRomance)moodLastLog).ToGameStringFromPOV(subject), ev));
                }
                finally { if (denied) boundary.Unpatch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)), AccessTools.Method(typeof(Program), nameof(SocialDenyComposite))); }
            });
        Check("social grammar extra packs and display are side effect free", () =>
        {
            var (a, b, subject, record) = SocialLogEvent(7);
            var log = (PlayLogEntry_SSCRomance)moodLastLog;
            var extras = (List<RulePackDef>)AccessTools.Field(typeof(PlayLogEntry_Interaction), "extraSentencePacks").GetValue(log);
            var third = new RulePackDef { defName = "SocialLogThirdParty" };
            SetSocialRulePack(third, new XElement("rulePack", new XElement("rulesStrings", new XElement("li", "sent->Third party preserved."))));
            extras.Add(third); int count = extras.Count;
            var before = new[] { a, b }.SelectMany(p => p.needs.mood.thoughts.memories.Memories).ToArray();
            var relations = a.relations.DirectRelations.ToArray(); int messages = commandFeedback;
            Rand.PushState();
            try
            {
                Rand.Seed = 98765; float expected = Rand.Value; Rand.Seed = 98765;
                string original = log.ToGameStringFromPOV(subject);
                for (int i = 0; i < 4; i++)
                {
                    Equal(original, log.ToGameStringFromPOV(subject, true));
                    Require(CopyEventTextPresent(repo, log.BackgroundTextFromPOV(subject), 7));
                    Require(log.ToGameStringFromPOV(b, true).Contains("Third party preserved."));
                }
                Equal(expected, Rand.Value); Require(original.Contains("Third party preserved."));
            }
            finally { Rand.PopState(); }
            Equal(count, extras.Count); Require(before.SequenceEqual(new[] { a, b }.SelectMany(p => p.needs.mood.thoughts.memories.Memories)));
            Require(relations.SequenceEqual(a.relations.DirectRelations)); Equal(messages, commandFeedback); Equal(1, playLogCalls);
            Equal(SSCRomanceCommandStatus.Completed, record.Status);
        });
        SocialLogFallbackTests(repo, boundary);
    }

    /// <summary>存档仍引用原两个人和原派系，不把人物日志复制到人格载体。</summary>
    private static void RegisterSocialLogReferences(Pawn a, Pawn b)
    {
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        foreach (var item in new ILoadReferenceable[] { a, b, player }.Distinct()) directory.RegisterLoaded(item);
    }

    /// <summary>资源故障和损坏事实只影响补句；失败后可恢复显示，原版正文及世界随机不变。</summary>
    private static void SocialLogFallbackTests(string repo, Harmony boundary)
    {
        foreach (string mode in new[] { "missing-pack", "missing-root", "broken-root", "exception" })
            Check("social grammar resource failure retains native entry " + mode, () =>
            {
                var (a, b, subject, _) = SocialLogEvent(7); var log = (PlayLogEntry_SSCRomance)moodLastLog;
                string native = PlainSocialLog(log, a, b).ToGameStringFromPOV(subject);
                var pack = DefDatabase<RulePackDef>.GetNamed("SSC_RomanceLog_Command");
                var packXml = XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_RomanceLogs.xml")).Root.Elements().Single(n => n.Element("defName").Value == pack.defName).Element("rulePack");
                try
                {
                    if (mode == "missing-pack") AccessTools.Method(typeof(DefDatabase<RulePackDef>), "Remove").Invoke(null, new object[] { pack });
                    if (mode is "missing-root" or "broken-root")
                        SetSocialRulePack(pack, new XElement("rulePack", new XElement("rulesStrings", new XElement("li", mode == "missing-root" ? "ssc_other_root->unrelated" : "ssc_romance_log_command_romance_rejected->[ssc_missing_rule]"))));
                    if (mode == "exception") boundary.Patch(AccessTools.Method(typeof(GrammarResolver), "ResolveUnsafe", new[] { typeof(string), typeof(GrammarRequest), typeof(bool).MakeByRefType(), typeof(string), typeof(bool), typeof(bool), typeof(List<string>), typeof(List<string>), typeof(bool) }), prefix: new HarmonyMethod(typeof(Program), nameof(SocialGrammarThrow)));
                    Rand.PushState();
                    try
                    {
                        Rand.Seed = 99887; float expected = Rand.Value; Rand.Seed = 99887;
                        Equal("", log.BackgroundTextFromPOV(subject)); Equal(expected, Rand.Value);
                    }
                    finally { Rand.PopState(); }
                    // 原生解析器对坏规则可发 ErrorOnce；验证实际错误来源后清理这一预期诊断。
                    if (mode == "broken-root") { Require(errors.Any(e => e.Contains("ssc_missing_rule"))); errors.Clear(); }
                    Equal(native, log.ToGameStringFromPOV(subject, true));
                }
                finally
                {
                    if (mode == "missing-pack") DefDatabase<RulePackDef>.Add(pack);
                    SetSocialRulePack(pack, packXml);
                    if (mode == "exception") boundary.Unpatch(AccessTools.Method(typeof(GrammarResolver), "ResolveUnsafe", new[] { typeof(string), typeof(GrammarRequest), typeof(bool).MakeByRefType(), typeof(string), typeof(bool), typeof(bool), typeof(List<string>), typeof(List<string>), typeof(bool) }), AccessTools.Method(typeof(Program), nameof(SocialGrammarThrow)));
                }
                Require(CopyEventTextPresent(repo, log.ToGameStringFromPOV(subject, true), 7)); Equal(1, playLogCalls);
            });
        Check("social grammar missing translated root uses native untranslated snapshot", () =>
        {
            var (_, _, subject, _) = SocialLogEvent(7); var log = (PlayLogEntry_SSCRomance)moodLastLog;
            var pack = DefDatabase<RulePackDef>.GetNamed("SSC_RomanceLog_Command");
            var old = LanguageDatabase.activeLanguage;
            try
            {
                // 模拟译包漏掉当前入口，英文副本由生产 XML 的原生 PostLoad 保留。
                SetSocialLanguage("ChineseSimplified");
                pack.RulesImmediate.RemoveAll(r => r.keyword == "ssc_romance_log_command_romance_rejected");
                AccessTools.Field(typeof(RulePackDef), "cachedRules").SetValue(pack, null);
                Require(CopyEventTextPresent(repo, log.BackgroundTextFromPOV(subject), 7, "English"));
            }
            finally { LanguageDatabase.activeLanguage = old; InjectSocialLogLanguage(repo, "English"); }
        });
        Check("social grammar plain native history never gains control background", () =>
        {
            var (a, b, _, _) = SocialLogEvent(7); var log = (PlayLogEntry_SSCRomance)moodLastLog;
            var plain = PlainSocialLog(log, a, b);
            foreach (var pov in new[] { a, b }) Require(!CopyAnyEventTextPresent(repo, plain.ToGameStringFromPOV(pov)));
        });
        foreach (bool role in new[] { false, true })
            Check("social grammar damaged saved role or action cannot disclose background " + role, () =>
            {
                var (_, _, subject, _) = SocialLogEvent(3); var passive = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(subject);
                var (a, b, actor, _) = SocialLogEvent(role ? 7 : 9); var active = ((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(actor);
                var invalid = new PlayLogEntry_SSCRomance(InteractionDefOf.RomanceAttempt, a, b, new(), role ? passive : active, role ? active : null);
                Equal("", invalid.BackgroundTextFromPOV(a)); Equal("", invalid.BackgroundTextFromPOV(b));
                Require(!CopyAnyEventTextPresent(repo, invalid.ToGameStringFromPOV(a)));
            });
    }

    /// <summary>只破坏 SSC 背景的解析，不能把原版正文也改成异常后声称保留成功。</summary>
    private static void SocialGrammarThrow(string rootKeyword)
    { if (rootKeyword.StartsWith("ssc_romance_log_")) throw new InvalidOperationException("social grammar fixture fault"); }

    /// <summary>原版对照条目保持同一日志编号和 extras；确保结果是原正文的补句而非重写或吞掉原结果。</summary>
    private static PlayLogEntry_Interaction PlainSocialLog(PlayLogEntry_SSCRomance log, Pawn a, Pawn b)
    {
        var plain = new PlayLogEntry_Interaction((InteractionDef)AccessTools.Field(typeof(PlayLogEntry_Interaction), "intDef").GetValue(log), a, b,
            (List<RulePackDef>)AccessTools.Field(typeof(PlayLogEntry_Interaction), "extraSentencePacks").GetValue(log));
        AccessTools.Field(typeof(LogEntry), "logID").SetValue(plain, log.LogID);
        return plain;
    }

    /// <summary>原生反序列化 XML 片段，不在宿主重建生产规则目录。</summary>
    private static T SocialLogXml<T>(XElement node)
    {
        var doc = new XmlDocument(); doc.LoadXml(node.ToString()); var value = DirectXmlToObject.ObjectFromXml<T>(doc.DocumentElement, false);
        // 完整 Def 装载另有 PostLoad 阶段；片段夹具显式执行以保存原版英文规则副本。
        if (value is RulePack pack) pack.PostLoad();
        if (value is RulePackDef def) ((RulePack)AccessTools.Field(typeof(RulePackDef), "rulePack").GetValue(def)).PostLoad();
        return value;
    }

    /// <summary>替换测试语言输入并清原生解析缓存；等同重新装载语言后的规则，保留原版未翻译副本。</summary>
    private static void SetSocialRulePack(RulePackDef def, XElement node)
    {
        AccessTools.Field(typeof(RulePackDef), "rulePack").SetValue(def, SocialLogXml<RulePack>(node));
        AccessTools.Field(typeof(RulePackDef), "cachedRules").SetValue(def, null);
        AccessTools.Field(typeof(RulePackDef), "cachedUntranslatedRules").SetValue(def, null);
    }

    /// <summary>去除原生人物颜色标签，只比较文字；不重新解析或替代受测语法入口。</summary>
    private static string CopyPlainText(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

    /// <summary>按稳定事件／索引读取指定语言资源的预期正文，不依赖开发占位或 E 编号。</summary>
    private static string CopyLogExpected(string repo, string locale, int ev, Pawn actor, Pawn recipient)
    {
        string[] paths = { "Accept.0", "Accept.1", "Reject.0", "Reject.1", "Reject.2",
            "Command.0", "Command.1", "Command.2", "Command.3", "Command.4", "Command.5", "Command.6", "Command.7" };
        var parts = paths[ev - 1].Split('.');
        string key = "SSC_RomanceLog_" + parts[0] + ".rulePack.rulesStrings." + parts[1];
        var row = XDocument.Load(Path.Combine(repo, "1.7/Languages", locale, "DefInjected/RulePackDef/SSC_RomanceLogs.xml"))
            .Root.Elements().Single(node => node.Name.LocalName == key).Value;
        return row.Substring(row.IndexOf("->", StringComparison.Ordinal) + 2)
            .Replace("[INITIATOR_nameDef]", actor.Name.ToStringShort)
            .Replace("[RECIPIENT_nameDef]", recipient.Name.ToStringShort);
    }

    /// <summary>历史／信件断言按当前资源的固定片段识别事件，人物部分由原生语法另行核对。</summary>
    private static bool CopyEventTextPresent(string repo, string actual, int ev, string locale = null)
    {
        locale ??= LanguageDatabase.activeLanguage.folderName;
        string[] paths = { "Accept.0", "Accept.1", "Reject.0", "Reject.1", "Reject.2", "Command.0", "Command.1", "Command.2", "Command.3", "Command.4", "Command.5", "Command.6", "Command.7" };
        var parts = paths[ev - 1].Split('.');
        string key = "SSC_RomanceLog_" + parts[0] + ".rulePack.rulesStrings." + parts[1];
        string rule = XDocument.Load(Path.Combine(repo, "1.7/Languages", locale, "DefInjected/RulePackDef/SSC_RomanceLogs.xml"))
            .Root.Elements().Single(node => node.Name.LocalName == key).Value;
        string body = rule.Substring(rule.IndexOf("->", StringComparison.Ordinal) + 2);
        var fragments = System.Text.RegularExpressions.Regex.Split(body, @"\[(?:INITIATOR|RECIPIENT)_[^\]]+\]")
            .Where(part => !string.IsNullOrWhiteSpace(part));
        actual = CopyPlainText(actual);
        return fragments.All(part => actual.Contains(part));
    }

    /// <summary>验证未注入 SSC 背景的普通正文／持久记录，不再把开发占位当作唯一识别标志。</summary>
    private static bool CopyAnyEventTextPresent(string repo, string actual)
        => Enumerable.Range(1, 13).Any(ev => CopyEventTextPresent(repo, actual, ev));

    /// <summary>通过原版语言注入器写入真实生产 Def；固定索引和路径必须全部成功，不能只匹配 XML 文本。</summary>
    private static void InjectSocialLogLanguage(string repo, string language)
    {
        SetSocialLanguage(language);
        foreach (var node in XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_RomanceLogs.xml")).Root.Elements())
            SetSocialRulePack(DefDatabase<RulePackDef>.GetNamed(node.Element("defName").Value), node.Element("rulePack"));
        var path = Path.Combine(repo, "1.7/Languages", language, "DefInjected/RulePackDef/SSC_RomanceLogs.xml");
        var file = new SocialLogTranslationFile(path); var package = new DefInjectionPackage(typeof(RulePackDef));
        package.AddDataFromFile(file, out var parseError, file.ReadAllText());
        Require(!parseError && package.loadErrors.Count == 0);
        package.InjectIntoDefs(true);
        Require(package.loadErrors.Count == 0, string.Join(";", package.loadErrors));
        Equal(13, package.injections.Count); Require(package.injections.Values.All(i => i.injected));
    }

    /// <summary>使用原生英／俄／中文通用语言处理器；宿主词形输入回退 Core 英语，不冒充已装完整译包。</summary>
    private static void SetSocialLanguage(string name)
    {
        if (name == "English") { LanguageDatabase.activeLanguage = socialLogEnglish; return; }
        var language = Bare<LoadedLanguage>(); language.folderName = name;
        language.info = new LanguageInfo { languageWorkerClass = name == "Russian" ? typeof(LanguageWorker_Russian) : typeof(LanguageWorker_Default) };
        language.keyedReplacements = socialLogEnglish.keyedReplacements; language.stringFiles = new(); language.loadErrors = new();
        AccessTools.Field(typeof(LoadedLanguage), "legacyFolderName").SetValue(language, "English");
        AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
        AccessTools.Field(typeof(LoadedLanguage), "wordInfo").SetValue(language, new LanguageWordInfo());
        LanguageDatabase.activeLanguage = language;
    }

    /// <summary>仅提供本地文件边界；语言路径和字段赋值由原版注入器负责。</summary>
    private sealed class SocialLogTranslationFile(string path) : VirtualFile
    {
        public override string Name => Path.GetFileName(path);
        public override string FullPath => path;
        public override bool Exists => File.Exists(path);
        public override long Length => new FileInfo(path).Length;
        public override Stream CreateReadStream() => File.OpenRead(path);
        public override byte[] ReadAllBytes() => File.ReadAllBytes(path);
        public override string[] ReadAllLines() => File.ReadAllLines(path);
        public override string ReadAllText() => File.ReadAllText(path);
    }

    /// <summary>只控制通知资格，正文仍由原 worker 和 tracker 真实生成。</summary>
    private static bool SocialLogNotify(ref bool __result) { __result = socialLogNotifications; return false; }
    /// <summary>原 worker 结束即停止测试行为随机输入；信件会在 tracker 返回前解析，不能等辅助函数返回才清队列。</summary>
    private static void SocialLogWorkerFinished() => socialRolls.Clear();
    /// <summary>人物颜色解析需要世界派系列表；仅提供宿主自己的派系，保留原版人物语法及标签解析。</summary>
    private static bool SocialLogFactionManager(ref FactionManager __result) { __result = socialLogFactions; return false; }
    /// <summary>原生名称格式化会查询耐久；宿主无全量 StatDef，仅隔离与日志无关的显示属性。</summary>
    private static bool SocialLogHitPoints(ref int __result) { __result = 100; return false; }
    /// <summary>截取原 LetterMaker 已创建的完整信件，避免地图声音和界面；不是直接构造预期正文。</summary>
    private static bool SocialLogReceive(Letter let) { socialLogLetters.Add((ChoiceLetter)let); return false; }
    /// <summary>保留受测语法故障诊断；原宿主仍隔离引擎日志投递。</summary>
    private static void SocialGrammarWarning(object[] __args)
    { if (__args[0] is string text && text.Contains("日志背景回退")) Console.WriteLine("GRAMMAR DIAGNOSTIC " + text); }
}
