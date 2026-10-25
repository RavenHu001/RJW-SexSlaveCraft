// AI 辅助生成：真实游戏 Harmony 3 安装生产补丁；实际 tracker／worker 结算，显式隔离图形、地图和通知发送。
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly Queue<float> socialRolls = new();
    private static float socialChance = .6f;
    private static bool socialCanInteract = true, socialWilling = true;
    private static Action socialDuringCanInteract;
    private static int bubbleCalls, taleCalls, shareBedCalls, playLogCalls, unclaimCalls, socialOpinionValue = 30;

    /// <summary>在已有规则／存档回归之后装载互动夹具，避免新增原版 Def 干扰第一批固定环境。</summary>
    private static void InteractionTests(Harmony harmony, string repo, string game)
    {
        bootstrap = true;
        SetupInteractionDefs(repo);
        SetupMoodDefs();
        AccessTools.Field(typeof(TickManager), "gameStartAbsTick").SetValue(Current.Game.tickManager, 3600000);
        AccessTools.Field(typeof(Game), "playLog").SetValue(Current.Game, new PlayLog());
        // 边界签名缺失必须带明确入口报错，不能静默漏装后以假环境取得通过。
        void Prefix(Type type, string method, string boundary) => harmony.Patch(AccessTools.Method(type, method) ?? throw new Exception(type.FullName + "." + method), prefix: new HarmonyMethod(typeof(Program), boundary));
        Prefix(typeof(Pawn_InteractionsTracker), "CanInteractNowWith", nameof(SocialCanInteract));
        Prefix(typeof(Pawn_InteractionsTracker), "InteractedTooRecentlyToInteract", nameof(NoCooldown));
        Prefix(typeof(InteractionWorker_RomanceAttempt), "SuccessChance", nameof(SocialProbability));
        Prefix(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance", nameof(SocialProbability));
        Prefix(typeof(InteractionWorker_RomanceAttempt), "CanCreatePsychicBondBetween", nameof(NoCooldown));
        Prefix(typeof(Pawn_RelationsTracker), "OpinionOf", nameof(SocialOpinion));
        Prefix(typeof(Pawn_RelationsTracker), "SecondaryRomanceChanceFactor", nameof(SocialAttraction));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(IdeoUtility)).Where(m => m.Name == "DoerWillingToDo"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(SocialWilling)));
        Prefix(typeof(PawnUtility), "ShouldSendNotificationAbout", nameof(NoCooldown));
        Prefix(typeof(MoteMaker), "MakeInteractionBubble", nameof(SocialBubble));
        Prefix(typeof(TaleRecorder), "RecordTale", nameof(SocialTale));
        Prefix(typeof(LovePartnerRelationUtility), "TryToShareBed", nameof(SocialShareBed));
        Prefix(typeof(PlayLog), "Add", nameof(SocialPlayLog));
        Prefix(typeof(Pawn_Ownership), "UnclaimBed", nameof(SocialUnclaim));
        harmony.Patch(AccessTools.Method(typeof(Messages), "Message", new[] { typeof(Message), typeof(bool) }), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Rand), "Value"), prefix: new HarmonyMethod(typeof(Program), nameof(SocialRoll)));
        // 使用生产特性与同一 Harmony 3 安装，不链接副本、不在宿主重写策略或指令匹配。
        foreach (var type in new[] { typeof(Harmony_SSCRomanceRandomSelection), typeof(Harmony_SSCRomanceInteraction),
            typeof(Harmony_SSCRomanceAcceptance), typeof(Harmony_SSCRomanceMoodWorker), typeof(Harmony_SSCRomanceMoodCorruption), typeof(Harmony_SSCRomanceMoodLoss), typeof(Harmony_SSCRomanceMoodLog), typeof(Harmony_SSCRomanceMenu), typeof(Harmony_SSCRomanceRjwClick),
            typeof(Patch_DogSpecialization_AnimalHandling) }) harmony.CreateClassProcessor(type).Patch();
        bootstrap = false;
        Check("batch2 actual game Harmony 3 and production annotated patches installed", () =>
        {
            Require(typeof(Harmony).Assembly.GetName().Version.Major >= 3);
            foreach (var method in Harmony_SSCRomanceAcceptance.TargetMethods())
                Require(Harmony.GetPatchInfo(method).Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceAcceptance)));
            Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(Pawn_InteractionsTracker), "TryInteractWith")).Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceInteraction)));
        });
        Check("batch2 active denial precedes all tracker side effects", () =>
        {
            ResetSocial(); var actor = SocialSlave(); var target = SocialPawn(); Comp(actor).romanceConfig = new() { allowInitiateRomance = false };
            Require(!actor.interactions.TryInteractWith(target, InteractionDefOf.RomanceAttempt));
            Equal(0, bubbleCalls + taleCalls + shareBedCalls + playLogCalls);
            Equal(0, actor.needs.mood.thoughts.memories.Memories.Count + target.needs.mood.thoughts.memories.Memories.Count);
            Require(actor.interactions.LastInteractionDef == null); Equal(0, actor.relations.DirectRelations.Count);
        });
        Check("batch2 passive denial executes real rejection branch and tracker feedback", () =>
        {
            ResetSocial(0f); var actor = SocialPawn(); var target = SocialSlave(); Comp(target).romanceConfig = new() { allowAcceptRomance = false };
            Require(actor.interactions.TryInteractWith(target, InteractionDefOf.RomanceAttempt));
            Require(actor.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.RebuffedMyRomanceAttempt && m.otherPawn == target));
            Require(target.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.FailedRomanceAttemptOnMe && m.otherPawn == actor));
            Require(!LovePartnerRelationUtility.LovePartnerRelationExists(actor, target));
            Equal(1, bubbleCalls); Equal(1, playLogCalls); Equal(0, taleCalls); Equal(InteractionDefOf.RomanceAttempt, actor.interactions.LastInteractionDef);
            Equal(.6f, InteractionWorker_RomanceAttempt.SuccessChance(actor, target));
        });
        Check("batch2 master guarantee overrides zero chance and inclusive random endpoint", () =>
        {
            ResetSocial(1f); socialChance = 0; var actor = SocialPawn(); var target = SocialSlave(actor);
            Comp(target).romanceConfig = new() { allowAcceptRomance = false, alwaysAcceptMaster = true };
            Require(actor.interactions.TryInteractWith(target, InteractionDefOf.RomanceAttempt));
            Require(actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, target)); Equal(1, taleCalls); Equal(1, shareBedCalls);
            Equal(0, socialRolls.Count); Equal(0f, InteractionWorker_RomanceAttempt.SuccessChance(actor, target));
        });
        foreach (float breakupRoll in new[] { .3999f, .4f })
            Check("batch2 real rejected proposal keeps native 40 percent split " + breakupRoll, () =>
            {
                ResetSocial(0f, breakupRoll); var actor = SocialSlave(); var target = SocialSlave();
                Comp(actor).romanceConfig = new() { allowInitiateBreakup = false }; Comp(target).romanceConfig = new() { allowAcceptProposal = false };
                actor.relations.AddDirectRelation(PawnRelationDefOf.Lover, target);
                Require(actor.interactions.TryInteractWith(target, InteractionDefOf.MarriageProposal));
                Equal(breakupRoll < .4f, actor.relations.DirectRelationExists(PawnRelationDefOf.ExLover, target));
                Equal(breakupRoll >= .4f, actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, target));
                Require(actor.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.RejectedMyProposal));
                Equal(0, taleCalls + unclaimCalls); Equal(0, socialRolls.Count);
            });
        SettlementTests(); SelectionAndEntryTests(harmony, repo); AcceptancePatchTests(harmony, repo); Batch2ReviewTests(harmony);
    }

    /// <summary>构造真实社交组件与成年载体；不改变被测策略，原关系和记忆写入仍走游戏实现。</summary>
    private static Pawn SocialPawn(Pawn pawn = null)
    {
        pawn ??= Pawn(); pawn.relations = new Pawn_RelationsTracker(pawn); pawn.story = new Pawn_StoryTracker(pawn);
        pawn.ownership = new Pawn_Ownership(pawn); pawn.interactions = new Pawn_InteractionsTracker(pawn);
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        AccessTools.Field(typeof(Pawn_AgeTracker), "growth").SetValue(pawn.ageTracker, 1f);
        pawn.ageTracker.AgeBiologicalTicks = 30L * GenDate.TicksPerYear;
        pawn.needs.mood = new Need_Mood(pawn) { def = new NeedDef { defName = "Mood", baseLevel = .5f } };
        return pawn;
    }
    /// <summary>在真实链与身份夹具上补足社交组件，主人引用由调用者显式传入。</summary>
    private static Pawn SocialSlave(Pawn master = null, float severity = .9f) => SocialPawn(Slave(severity, master));
    /// <summary>重置本例确定性随机序列及边界计数；空序列回到原生随机，不在业务助手中增加抽样。</summary>
    private static void ResetSocial(params float[] rolls)
    {
        socialRolls.Clear(); foreach (float roll in rolls) socialRolls.Enqueue(roll);
        socialChance = .6f; socialCanInteract = socialWilling = true;
        socialDuringCanInteract = null;
        socialOpinionValue = 30;
        bubbleCalls = taleCalls = shareBedCalls = playLogCalls = unclaimCalls = 0;
    }
    /// <summary>装载原版具名 Def 所需的最小字段与本模块真实语言；不启动整场游戏或读取玩家配置。</summary>
    private static void SetupInteractionDefs(string repo)
    {
        JobDefOf.TryRomance = new JobDef { defName = "TryRomance" };
        AccessTools.Field(typeof(RaceProperties), "fleshType").SetValue(human.race, new FleshTypeDef { defName = "FixtureFlesh", isOrganic = true });
        MessageTypeDefOf.RejectInput = new MessageTypeDef { defName = "RejectInput" };
        MessageTypeDefOf.NeutralEvent = new MessageTypeDef { defName = "NeutralEvent" };
        human.race.lifeStageAges.Add(new LifeStageAge { minAge = 0, def = new LifeStageDef { defName = "FixtureAdult", developmentalStage = DevelopmentalStage.Adult } });
        foreach (string name in new[] { "Lover", "Fiance", "Spouse", "ExLover", "ExSpouse" })
        { var def = new PawnRelationDef { defName = name, reflexive = true, workerClass = typeof(PawnRelationWorker) }; DefDatabase<PawnRelationDef>.Add(def); AccessTools.Field(typeof(PawnRelationDefOf), name).SetValue(null, def); }
        foreach (string name in new[] { "RebuffedMyRomanceAttempt", "FailedRomanceAttemptOnMe", "FailedRomanceAttemptOnMeLowOpinionMood", "RejectedMyProposal", "RejectedMyProposalMood", "IRejectedTheirProposal", "BrokeUpWithMe", "GotMarried", "DivorcedMe", "HoneymoonPhase", "CheatedOnMe" })
        {
            var def = new ThoughtDef { defName = name, thoughtClass = typeof(Thought_Memory), durationDays = 5, stages = new() { new ThoughtStage { label = name } } };
            DefDatabase<ThoughtDef>.Add(def); AccessTools.Field(typeof(ThoughtDefOf), name)?.SetValue(null, def);
        }
        foreach (var field in typeof(RulePackDefOf).GetFields().Where(f => f.Name.StartsWith("Sentence_RomanceAttempt") || f.Name.StartsWith("Sentence_MarriageProposal")))
            field.SetValue(null, new RulePackDef { defName = field.Name });
        InteractionDefOf.RomanceAttempt = SocialInteraction("RomanceAttempt", typeof(InteractionWorker_RomanceAttempt));
        InteractionDefOf.MarriageProposal = SocialInteraction("MarriageProposal", typeof(InteractionWorker_MarriageProposal));
        SSCRomanceDefOf.Breakup = SocialInteraction("Breakup", typeof(InteractionWorker_Breakup));
        foreach (var def in new[] { InteractionDefOf.RomanceAttempt, InteractionDefOf.MarriageProposal, SSCRomanceDefOf.Breakup }) DefDatabase<InteractionDef>.Add(def);
        var language = Bare<LoadedLanguage>(); language.folderName = "English";
        language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
        AccessTools.Field(typeof(LoadedLanguage), "legacyFolderName").SetValue(language, "English");
        language.stringFiles = new(); language.keyedReplacements = new(); language.loadErrors = new();
        foreach (var entry in XDocument.Load(Path.Combine(repo, "1.7/Languages/English/Keyed/SSC_Romance.xml")).Root.Elements())
            language.keyedReplacements.Add(entry.Name.LocalName, new LoadedLanguage.KeyedReplacement { key = entry.Name.LocalName, value = entry.Value });
        AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
        AccessTools.Field(typeof(LoadedLanguage), "wordInfo").SetValue(language, new LanguageWordInfo());
        LanguageDatabase.activeLanguage = LanguageDatabase.defaultLanguage = language;
    }
    /// <summary>按已取证的私有 XML 字段建立真实 InteractionDef，让其原生 Worker 属性负责创建 worker。</summary>
    private static InteractionDef SocialInteraction(string name, Type worker)
    { var def = new InteractionDef { defName = name }; AccessTools.Field(typeof(InteractionDef), "workerClass").SetValue(def, worker); return def; }
    /// <summary>隔离地图可交互性，仍让 TryInteractWith 自己处理返回及全部社交副作用。</summary>
    private static bool SocialCanInteract(ref bool __result)
    {
        // 复核夹具模拟其他补丁在原版可交互性检查中回调修改规则；回调只消费一次，避免重入循环。
        var callback = socialDuringCanInteract; socialDuringCanInteract = null; callback?.Invoke();
        __result = socialCanInteract; return false;
    }
    /// <summary>提供非冷却、无灵能纽带及不发送地图通知的边界，不覆盖浪漫规则。</summary>
    private static bool NoCooldown(ref bool __result) { __result = false; return false; }
    /// <summary>提供原生接受概率输入；验证生产覆盖不污染公共查询且保留默认比较。</summary>
    private static bool SocialProbability(ref float __result) { __result = socialChance; return false; }
    /// <summary>提供稳定普通好感，避免 SSC 奖励的好感掩盖本次接受覆盖。</summary>
    private static bool SocialOpinion(ref int __result) { __result = socialOpinionValue; return false; }
    /// <summary>给自主权重提供稳定吸引力输入，保留权重本身的原生公式与 SSC 主动过滤。</summary>
    private static bool SocialAttraction(ref float __result) { __result = 1f; return false; }
    /// <summary>控制原版意识形态意愿输入，以验证原生清旧关系循环。</summary>
    private static bool SocialWilling(ref bool __result) { __result = commandWilling.Count > 0 ? commandWilling.Dequeue() : socialWilling; return false; }
    /// <summary>按序提供包含 0／1 端点的原随机输入；序列外继续真实 Rand 实现。</summary>
    private static bool SocialRoll(ref float __result) { if (socialRolls.Count == 0) return true; __result = socialRolls.Dequeue(); return false; }
    /// <summary>记录原 tracker 的气泡请求，独立宿主不创建图形对象。</summary>
    private static bool SocialBubble() { bubbleCalls++; return false; }
    /// <summary>记录原 worker 的传说请求，不要求完整世界传说系统。</summary>
    private static bool SocialTale() { taleCalls++; return false; }
    /// <summary>记录求爱成功后的共床协调入口，地图床位选择留游戏验证。</summary>
    private static bool SocialShareBed() { shareBedCalls++; return false; }
    /// <summary>记录原 tracker 的互动日志提交，不展开依赖完整语法包的文字生成。</summary>
    private static bool SocialPlayLog(LogEntry entry) { moodLastLog = entry; playLogCalls++; return false; }
    /// <summary>隔离实际地图床位 Comp，只记录原分手是否调用解除并清掉夹具的拥有引用。</summary>
    private static bool SocialUnclaim(Pawn_Ownership __instance)
    { unclaimCalls++; AccessTools.Field(typeof(Pawn_Ownership), "intOwnedBed").SetValue(__instance, null); return false; }
}
