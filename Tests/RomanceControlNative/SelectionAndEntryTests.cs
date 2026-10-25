// AI 辅助生成：真实随机权重、原版菜单后置适配、RJW 点击及局部状态变化。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>验证各入口共享主动策略，接收许可既不降低权重也不取消真实互动。</summary>
    private static void SelectionAndEntryTests(Harmony harmony, string repo)
    {
        AccessTools.Field(typeof(TickManager), "ticksGameInt").SetValue(Current.Game.tickManager, 6000000);
        foreach (float severity in new[] { .1f, .3f, .5f, .9f })
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check($"batch2 actual native random filter stage={severity}/action={action}", () =>
            {
                ResetSocial(); var a = SocialSlave(severity: severity); var b = SocialPawn();
                if (action != SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                // 原生求婚权重还取相恋时长，夹具使用足够早的关系起点。
                foreach (var r in a.relations.DirectRelations) r.startTicks = 0;
                float original = SocialDef(action).Worker.RandomSelectionWeight(a, b); Require(original > 0, "fixture must have positive native weight");
                Comp(a).romanceConfig = new() { allowInitiateRomance = false, allowInitiateProposal = false, allowInitiateBreakup = false };
                float filtered = SocialDef(action).Worker.RandomSelectionWeight(a, b);
                Equal(severity < .3f ? original : 0f, filtered);
            });
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
            Check("batch2 recipient rejection and master guarantee never filter autonomous candidates " + action, () =>
            {
                ResetSocial(); var a = SocialPawn(); var b = SocialSlave(a);
                if (action == SSCRomanceAction.Proposal) { a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b); a.relations.DirectRelations[0].startTicks = 0; }
                float original = SocialDef(action).Worker.RandomSelectionWeight(a, b); Require(original > 0);
                Comp(b).romanceConfig = new() { allowAcceptRomance = false, allowAcceptProposal = false };
                Equal(original, SocialDef(action).Worker.RandomSelectionWeight(a, b));
                Comp(b).romanceConfig.alwaysAcceptMaster = true; Equal(original, SocialDef(action).Worker.RandomSelectionWeight(a, b));
            });
        Check("batch2 inactive native zero weight is not increased by master guarantee", () =>
        {
            ResetSocial(); var a = SocialPawn(); var b = SocialSlave(a); Comp(b).romanceConfig = new() { alwaysAcceptMaster = true };
            a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b); Equal(0f, InteractionDefOf.RomanceAttempt.Worker.RandomSelectionWeight(a, b));
        });
        Check("batch2 only-master selection checks actual owner rather than assigned trainer", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); Comp(a).selectedTrainer = b; Comp(a).romanceConfig = new() { initiateOnlyMaster = true };
            Equal(0f, InteractionDefOf.RomanceAttempt.Worker.RandomSelectionWeight(a, b));
            Require(InteractionDefOf.RomanceAttempt.Worker.RandomSelectionWeight(a, SocialPawn(SSCBondUtility.GetChain(a).LinkedPawn)) > 0);
        });
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("batch2 denied explicit action has no tracker effects " + action, () =>
            {
                ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); if (action != SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                Comp(a).romanceConfig = new() { allowInitiateRomance = false, allowInitiateProposal = false, allowInitiateBreakup = false };
                var def = SocialDef(action); var oldThought = def.initiatorThought; var oldSkill = def.initiatorXpGainSkill;
                try
                {
                    // 主动取消必须早于这些非空副作用字段；若误进入原方法，本例会产生记忆或缺失技能的错误。
                    def.initiatorThought = ThoughtDefOf.RebuffedMyRomanceAttempt; def.initiatorXpGainSkill = new SkillDef { defName = "FixtureSocial" };
                    Require(!a.interactions.TryInteractWith(b, def)); Equal(0, a.needs.mood.thoughts.memories.Memories.Count);
                    Equal(0, bubbleCalls + taleCalls + playLogCalls); Require(a.interactions.LastInteractionDef == null);
                }
                finally { def.initiatorThought = oldThought; def.initiatorXpGainSkill = oldSkill; }
            });
        Check("batch2 master guarantee cannot bypass native inability to interact", () =>
        {
            ResetSocial(); socialCanInteract = false; var a = SocialPawn(); var b = SocialSlave(a); Comp(b).romanceConfig = new() { alwaysAcceptMaster = true };
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, bubbleCalls + taleCalls + playLogCalls);
        });
        Check("batch2 fourth stage ordinary entry never gains implicit command authorization", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); Comp(a).romanceConfig = new() { allowInitiateRomance = false };
            Require(SSCRomanceLifecycle.TryAuthorize(a, b, SSCRomanceAction.Romance, out _));
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
        });
        Check("batch2 original menu stays selectable for passive prohibition and guarantee", () =>
        {
            ResetSocial(); var a = SocialPawn(); var b = SocialSlave(a); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            foreach (bool guarantee in new[] { false, true })
            {
                Comp(b).romanceConfig.alwaysAcceptMaster = guarantee; var option = new FloatMenuOption("original", () => { }); float chance = .6f; bool eligible = true;
                Harmony_SSCRomanceMenu.Postfix(a, b, ref option, ref chance, ref eligible);
                Require(eligible && option.action != null); Equal(guarantee ? 1f : 0f, chance); Require(option.Label.Contains("acceptance"));
            }
        });
        Check("batch2 original menu shows active denial while late ordinary execution rechecks", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); Comp(a).romanceConfig = new() { allowInitiateRomance = false };
            var option = new FloatMenuOption("original", () => { }); float chance = .6f; bool eligible = true;
            Harmony_SSCRomanceMenu.Postfix(a, b, ref option, ref chance, ref eligible); Require(!eligible && option.action == null);
            Require(option.Label.Contains("prohibit")); Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
        });
        Check("batch2 original disabled menu is never reopened by passive master guarantee", () =>
        {
            var a = SocialPawn(); var b = SocialSlave(a); Comp(b).romanceConfig = new() { alwaysAcceptMaster = true };
            var option = new FloatMenuOption("native unavailable", null); float chance = 0; bool eligible = false;
            Harmony_SSCRomanceMenu.Postfix(a, b, ref option, ref chance, ref eligible); Require(!eligible && option.action == null); Equal("native unavailable", option.Label);
        });
        foreach (bool forbidden in new[] { true, false })
            Check("batch2 actual internal RJW Socialize flows through production guard " + forbidden, () =>
            {
                ResetSocial(0f); var a = SocialSlave(); var b = SocialSlave(); Comp(a).romanceConfig = new() { allowInitiateRomance = !forbidden };
                Comp(b).romanceConfig = new() { allowAcceptRomance = false };
                Harmony_SSCRomanceRjwClick.TargetMethod().Invoke(null, new object[] { a, new LocalTargetInfo(b), InteractionDefOf.RomanceAttempt });
                Equal(forbidden ? 0 : 1, playLogCalls); Equal(forbidden ? 0 : 1, a.needs.mood.thoughts.memories.Memories.Count);
            });
        Check("batch2 shared entry rechecks preference, chain stage and personality without caching", () =>
        {
            ResetSocial(0f); var a = SocialSlave(); var b = SocialPawn(); Require(SSCRomanceInteractionUtility.CanStart(a, b, SSCRomanceAction.Romance));
            Comp(a).romanceConfig = new() { allowInitiateRomance = false }; Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            SSCBondUtility.GetChain(a).Severity = .1f; Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
        });
        Check("batch2 old sexual protection switch cannot disable romance control", () =>
        {
            var old = SSCMod.settings; try { SSCMod.settings = new SSCSettings { enableSexSlaveProtectionRules = false };
                ResetSocial(); var a = SocialSlave(); Comp(a).romanceConfig = new() { allowInitiateRomance = false }; Require(!a.interactions.TryInteractWith(SocialPawn(), InteractionDefOf.RomanceAttempt)); }
            finally { SSCMod.settings = old; }
        });
        Check("batch2 unknown configuration version follows first-batch native fallback", () =>
        {
            ResetSocial(0f); var a = SocialSlave(); var b = SocialSlave(); Comp(a).romanceConfig = new() { version = 88, allowInitiateRomance = false };
            Comp(b).romanceConfig = new() { version = 88, allowAcceptRomance = false }; Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            Require(a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
        });
        Check("batch2 unrelated interaction identity and null are not classified as romance", () =>
        { Require(!SSCRomanceInteractionUtility.TryGetAction(null, out _)); Require(!SSCRomanceInteractionUtility.TryGetAction(new InteractionDef { defName = "RomanceAttempt" }, out _)); });
        Check("batch2 ordinary chat is unaffected by both sides denying every romance rule", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialSlave(); Comp(a).romanceConfig = Comp(b).romanceConfig = Denied();
            var chat = SocialInteraction("FixtureChat", typeof(InteractionWorker)); Require(a.interactions.TryInteractWith(b, chat));
            Equal(chat, a.interactions.LastInteractionDef); Equal(1, playLogCalls); Equal(0, a.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("batch2 self, missing participant and incomplete relation carrier stop before native effects", () =>
        {
            ResetSocial(); var a = SocialSlave(); Require(!a.interactions.TryInteractWith(a, InteractionDefOf.RomanceAttempt));
            Require(!a.interactions.TryInteractWith(null, InteractionDefOf.RomanceAttempt)); var b = SocialPawn(); b.relations = null;
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.MarriageProposal)); Equal(0, bubbleCalls + playLogCalls);
        });
        Check("batch2 malformed health or race carrier is rejected without querying unsafe pawn getters", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); b.health = null;
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            b = SocialPawn(); b.def = null; Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            Equal(0, bubbleCalls + playLogCalls);
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("batch2 actual translator resolves menu rejection and preview " + language, () =>
            {
                var loaded = LanguageDatabase.activeLanguage; var saved = loaded.keyedReplacements;
                try
                {
                    loaded.keyedReplacements = XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed/SSC_Romance.xml")).Root.Elements().ToDictionary(e => e.Name.LocalName,
                        e => new LoadedLanguage.KeyedReplacement { key = e.Name.LocalName, value = e.Value });
                    var a = SocialSlave(); var b = SocialPawn(); Comp(a).romanceConfig = new() { allowInitiateRomance = false };
                    string reason = SSCRomanceInteractionUtility.Rejection(a, b, SSCRomanceAction.Romance); Require(!reason.Contains("SSC_Romance"));
                    string preview = "SSC_Romance_AcceptancePreview".Translate("FixtureName", "0%"); Require(preview.Contains("FixtureName") && preview.Contains("0%") && !preview.Contains("SSC_Romance"));
                }
                finally { loaded.keyedReplacements = saved; }
            });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("romance translated keys and exact source mirror " + language, () =>
            {
                string path = Path.Combine(repo, "1.7/Languages", language, "Keyed/SSC_Romance.xml"); string source = Path.Combine(repo, "Sexslavecraft/Languages", language, "Keyed/SSC_Romance.xml");
                Require(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source))); var entries = XDocument.Load(path).Root.Elements().ToArray(); Equal(76, entries.Length);
                Require(entries.All(e => !string.IsNullOrWhiteSpace(e.Value))); Equal(76, entries.Select(e => e.Name.LocalName).Distinct().Count());
            });
    }
}
