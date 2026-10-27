// AI 辅助生成：独立文案入口，沿实际语言注入、思想显示及语法；不执行浪漫互动机制测试。
using HarmonyLib;
using SexSlaveCraft;
using RimWorld;
using Verse;
using System.Xml.Linq;

internal static partial class Program
{
    /// <summary>只加载当前资源和显示所需最小原生夹具，保留原模式的整套机制测试入口。</summary>
    private static void RomanceLocalizationOnlyTests(Harmony boundary, string repo, string game)
    {
        Console.WriteLine("SCOPE: romance text only; no interaction/policy/personality regression");
        SetupInteractionDefs(repo);
        AccessTools.Field(typeof(TickManager), "gameStartAbsTick").SetValue(Current.Game.tickManager, 3600000);
        SetupCompositeCarriers(repo);
        SetupMoodDefs();
        AccessTools.Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map> { Bare<Map>() });
        SetupSocialLogGrammar(repo, game);
        boundary.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.FactionManager)),
            prefix: new HarmonyMethod(typeof(Program), nameof(SocialLogFactionManager)));
        boundary.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MaxHitPoints)),
            prefix: new HarmonyMethod(typeof(Program), nameof(SocialLogHitPoints)));
        FactionDefOf.PlayerColony ??= new FactionDef { defName = "CopyPlayerColony", pawnSingular = "colonist", pawnsPlural = "colonists" };
        ColoredText.ResetStaticData();
        var actor = SocialPawn(); var recipient = SocialPawn();
        PrepareSocialLogPawn(actor); PrepareSocialLogPawn(recipient);
        foreach (string locale in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            InjectSocialLogLanguage(repo, locale);
            var language = LanguageDatabase.activeLanguage;
            // 每语独立字典，避免覆盖 English/defaultLanguage 对象时污染下一语回退。
            language.keyedReplacements = new();
            foreach (string file in new[] { "SSC_RomanceMood.xml", "SSC_Romance.xml" })
                foreach (var entry in XDocument.Load(Path.Combine(repo, "1.7/Languages", locale, "Keyed", file)).Root.Elements())
                    language.keyedReplacements[entry.Name.LocalName] = new LoadedLanguage.KeyedReplacement { key = entry.Name.LocalName, value = entry.Value };
            foreach (var branch in SSCRomanceMoodCatalog.EventBranches)
                Check("copy event text " + locale + "/" + branch.Id, () =>
                {
                    Equal(language.keyedReplacements[branch.NameKey].value, SSCRomanceMoodText.Name(branch));
                    Equal(language.keyedReplacements[branch.DescriptionKey].value, SSCRomanceMoodText.Description(branch));
                    int opinion = branch.Opinion == SSCRomanceOpinionBand.Disliked ? -10 : branch.Opinion == SSCRomanceOpinionBand.Close ? 60 : 30;
                    Require(SSCRomanceMoodCatalog.TryResolve(branch.Event, branch.Stage, branch.Masochist, opinion, out var selection));
                    if (!selection.HasMemory) return;
                    Require(Thought_MemorySSCCompositeRomance.TryCreate(selection, out var memory)); memory.pawn = actor;
                    Equal(SSCRomanceMoodText.Name(branch).CapitalizeFirst(), memory.LabelCap);
                    Equal(SSCRomanceMoodText.Description(branch), memory.Description);
                });
            foreach (var branch in SSCRomanceMoodCatalog.ControlBranches)
                Check("copy continuous text " + locale + "/" + branch.Id, () =>
                {
                    var pawn = ControlPawn(branch.Stage, branch.Masochist, branch.Control != SSCRomanceControlState.Passive, branch.Control != SSCRomanceControlState.Active);
                    var thought = ControlThought(pawn); Require(thought.Active);
                    Equal(SSCRomanceMoodText.Name(branch).CapitalizeFirst(), thought.LabelCap);
                    Equal(SSCRomanceMoodText.Description(branch), thought.Description);
                });
            Check("copy fallback text " + locale, () =>
            {
                Equal(language.keyedReplacements["SSC_RomanceMood_Unknown_Name"].value, SSCRomanceMoodText.Name(null));
                Equal(language.keyedReplacements["SSC_RomanceMood_Unknown_Description"].value, SSCRomanceMoodText.Description(null));
            });
            for (int ev = 1; ev <= 13; ev++)
            {
                int number = ev;
                Check("copy native grammar " + locale + "/E" + ev.ToString("00"), () =>
                {
                    var before = number is 1 or 3 or 6 or 7 ? SSCRomanceOldRelation.None : number == 12 ? SSCRomanceOldRelation.Fiance : number == 13 ? SSCRomanceOldRelation.Spouse : SSCRomanceOldRelation.Lover;
                    var fact = (SSCRomanceInteractionFact)AccessTools.Constructor(typeof(SSCRomanceInteractionFact), new[] { typeof(SSCRomanceCompositeEvent), typeof(SSCRomanceOldRelation) })
                        .Invoke(new object[] { (SSCRomanceCompositeEvent)number, before });
                    var format = AccessTools.Method(production.GetType("SexSlaveCraft.SSCRomanceLogGrammar"), "Format");
                    string text = (string)format.Invoke(null, new object[] { fact, actor, recipient, 90000 + number, false });
                    Equal(CopyLogExpected(repo, locale, number, actor, recipient), CopyPlainText(text));
                    var interaction = number is 1 or 3 or 6 or 7 ? InteractionDefOf.RomanceAttempt : number >= 11 ? SSCRomanceDefOf.Breakup : InteractionDefOf.MarriageProposal;
                    var log = new PlayLogEntry_SSCRomance(interaction, actor, recipient, new(), number >= 6 ? fact : null, number <= 5 ? fact : null);
                    Equal(text, log.BackgroundTextFromPOV(number >= 6 ? actor : recipient));
                    Equal("", log.BackgroundTextFromPOV(number >= 6 ? recipient : actor));
                });
            }
            foreach (var entry in XDocument.Load(Path.Combine(repo, "1.7/Languages", locale, "Keyed/SSC_Romance.xml")).Root.Elements()
                .Where(e => e.Name.LocalName.StartsWith("SSC_Romance_Report_") || e.Name.LocalName.StartsWith("SSC_Romance_Result_")))
                Check("copy keyed format " + locale + "/" + entry.Name.LocalName, () =>
                {
                    string result = entry.Name.LocalName.StartsWith("SSC_Romance_Result_")
                        ? entry.Name.LocalName.Translate(actor.LabelShort, recipient.LabelShort, "ACTION").ToString()
                        : entry.Name.LocalName.Translate().ToString();
                    Require(!string.IsNullOrWhiteSpace(result) && !result.Contains("{0}") && !result.Contains("{1}") && !result.Contains("{2}"));
                    if (entry.Name.LocalName.StartsWith("SSC_Romance_Report_")) Require(result.Contains("TargetA"));
                });
        }
    }
}
