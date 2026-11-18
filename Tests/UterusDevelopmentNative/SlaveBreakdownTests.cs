// AI 辅助生成：真实恶堕增长、印记和原版精神状态入口共用原生宿主。
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static Map breakdownMap;
    private static Faction breakdownFaction;
    private static readonly HashSet<Pawn> breakdownPawns = new();
    private static readonly List<(Pawn Pawn, string Type, SSCImprintKind Imprint, bool InBreakdown)> breakdownLetters = new();
    private static readonly List<Pawn> breakdownFailureLetters = new();
    private static Pawn reachableBreakdownTrainer;
    private static bool breakdownRollSucceeds;
    private static int breakdownRolls;
    private static MentalBreakIntensity? selectedBreakdownIntensity;
    private static bool tickDuringImprintLetter;
    private static Pawn skipBreakdownEffectFor;
    private static Pawn interruptBeforeBreakdownStart;
    private static int invitedUseDispatches;
    private static Action invitedUseReachCallback;

    private static void SlaveBreakdownTests(string repo, MethodBase receiveLetter)
    {
        var boundary = new Harmony("ssc.tests.slave-breakdown");
        var game = Current.Game;
        var previousSettings = SSCMod.settings;
        var previousState = Current.ProgramState;
        var previousWorld = game.World;
        var previousQuests = game.questManager;
        var previousThreshold = StatDefOf.MentalBreakThreshold;
        var previousRecord = RecordDefOf.TimesInMentalState;
        JobDef previousWait = JobDefOf.Wait;
        ref StatDef frequency = ref AccessTools.StaticFieldRefAccess<StatDef>(typeof(rjw.RJWStatDefOf), "SexFrequency");
        ref StatDef satisfaction = ref AccessTools.StaticFieldRefAccess<StatDef>(typeof(rjw.RJWStatDefOf), "SexSatisfaction");
        ref StatDef vulnerability = ref AccessTools.StaticFieldRefAccess<StatDef>(typeof(rjw.RJWStatDefOf), "Vulnerability");
        var previousFrequency = frequency;
        var previousSatisfaction = satisfaction;
        var previousVulnerability = vulnerability;
        float previousDecay = rjw.RJWSettings.sexneed_decay_rate;
        bool previousRandomStates = DebugSettings.enableRandomMentalStates;
        var mapsField = AccessTools.Field(typeof(Game), "maps");
        object previousMaps = mapsField.GetValue(game);
        var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(game);
        var runtime = new SSCSlaveBreakdownGameComponent(game);
        var oldTick = GenTicks.TicksGame;
        try
        {
            components.Add(runtime);
            Current.ProgramState = ProgramState.Playing;
            game.World = Bare<World>();
            game.World.worldPawns = new WorldPawns();
            game.World.factionManager = new FactionManager();
            breakdownFaction = new Faction { def = new FactionDef { defName = "SSC_TestColony", isPlayer = true } };
            game.World.factionManager.AllFactionsListForReading.Add(breakdownFaction);
            AccessTools.Field(typeof(FactionManager), "ofPlayer").SetValue(game.World.factionManager, breakdownFaction);
            game.questManager = new QuestManager();
            breakdownMap = new Map();
            breakdownMap.mapPawns = new MapPawns(breakdownMap);
            breakdownMap.gatherSpotLister = new GatherSpotLister();
            JobDefOf.Wait ??= new JobDef { defName = "Wait", driverClass = typeof(JobDriver_Wait) };
            // 精神状态调用更新入口，独立宿主将网格更新隔离在下方边界。
            breakdownMap.attackTargetsCache = Bare<AttackTargetsCache>();
            mapsField.SetValue(game, new List<Map> { breakdownMap });
            DebugSettings.enableRandomMentalStates = true;
            StatDefOf.MentalBreakThreshold = new StatDef { defName = "SSC_TestMentalBreakThreshold", defaultBaseValue = .35f };
            RecordDefOf.TimesInMentalState ??= DefDatabase<RecordDef>.GetNamedSilentFail("TimesInMentalState") ??
                new RecordDef { defName = "TimesInMentalState", type = RecordType.Int };
            if (DefDatabase<RecordDef>.GetNamedSilentFail("TimesInMentalState") == null)
                DefDatabase<RecordDef>.Add(RecordDefOf.TimesInMentalState);
            LetterDefOf.NegativeEvent ??= DefDatabase<LetterDef>.GetNamedSilentFail("NegativeEvent") ??
                new LetterDef { defName = "NegativeEvent", letterClass = typeof(StandardLetter) };
            if (DefDatabase<LetterDef>.GetNamedSilentFail("NegativeEvent") == null)
                DefDatabase<LetterDef>.Add(LetterDefOf.NegativeEvent);

            var xml = new XmlDocument();
            xml.Load(Path.Combine(repo, "1.7/Defs/MentalStateDefs/SSC_SlaveBreakdown.xml"));
            DefDatabase<MentalStateDef>.Add(DirectXmlToObject.ObjectFromXml<MentalStateDef>(xml.DocumentElement.SelectSingleNode("MentalStateDef"), false));
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("MentalBreakDef"))
                DefDatabase<MentalBreakDef>.Add(DirectXmlToObject.ObjectFromXml<MentalBreakDef>(node, false));
            // 宿主不模拟技能和身体能力。边界从实际 RJW 定义读取，数值仍经过原版 StatWorker 和 RJW 需求计算。
            var rjwStats = XDocument.Load(RjwResource("Defs/StatDefs/Stats.xml"));
            StatDef LoadNeutralStat(string name)
            {
                var source = rjwStats.Root.Elements("StatDef").Single(node => (string)node.Element("defName") == name);
                var stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
                if (stat == null)
                {
                    stat = new StatDef { defName = name, defaultBaseValue = (float)source.Element("defaultBaseValue") };
                    DefDatabase<StatDef>.Add(stat);
                }
                stat.minValue = (float?)source.Element("minValue") ?? stat.minValue;
                stat.maxValue = (float?)source.Element("maxValue") ?? stat.maxValue;
                return stat;
            }
            frequency = LoadNeutralStat("SexFrequency");
            satisfaction = LoadNeutralStat("SexSatisfaction");
            vulnerability = LoadNeutralStat("Vulnerability");
            rjw.RJWSettings.sexneed_decay_rate = 1f;
            if (DefDatabase<PawnCapacityDef>.GetNamedSilentFail("Consciousness") == null)
                DefDatabase<PawnCapacityDef>.Add(new PawnCapacityDef { defName = "Consciousness" });
            boundary.Patch(AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly)),
                prefix: new HarmonyMethod(typeof(Program), nameof(BreakdownRjwTypeBoundary)));
            HediffDef LoadHediff(string path, string name = null)
            {
                xml.Load(path);
                string selector = name == null ? "HediffDef" : $"HediffDef[defName='{name}']";
                var def = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode(selector), false);
                DefDatabase<HediffDef>.Add(def);
                return def;
            }
            var breakdownEffect = LoadHediff(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_SlaveBreakdown.xml"));
            var cooldownDef = LoadHediff(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_SlaveBreakdown.xml"),
                Hediff_SSCSlaveBreakdownCooldown.DefName);
            var feelingBroken = LoadHediff(RjwResource("Defs/HediffDefs/Hediffs_FeelingBroken.xml"));
            // The isolated suite loads only the RJW Defs it exercises. Prewarm the shared cache before case error capture.
            bool previousBootstrap = bootstrap;
            try
            {
                bootstrap = true;
                RuntimeHelpers.RunClassConstructor(typeof(rjw.xxx).TypeHandle);
            }
            finally { bootstrap = previousBootstrap; }
            xml.Load(Path.Combine(repo, "1.7/Defs/LetterDefs/SSC_BreakdownTrainingRequest.xml"));
            var requestDef = DirectXmlToObject.ObjectFromXml<LetterDef>(xml.DocumentElement.SelectSingleNode("LetterDef"), false);
            DefDatabase<LetterDef>.Add(requestDef);
            xml.Load(Path.Combine(repo, "1.7/Defs/JobDef/SSC_BreakdownTraining.xml"));
            var trainingDef = DirectXmlToObject.ObjectFromXml<JobDef>(xml.DocumentElement.SelectSingleNode("JobDef"), false);
            DefDatabase<JobDef>.Add(trainingDef);
            xml.Load(Path.Combine(repo, "1.7/Defs/JobDef/SSC_BreakdownInvitedUse.xml"));
            DefDatabase<JobDef>.Add(DirectXmlToObject.ObjectFromXml<JobDef>(xml.DocumentElement.SelectSingleNode("JobDef"), false));
            Equal(typeof(JobDriver_SSCBreakdownInvitedUse), JobDriver_SSCBreakdownInvitedUse.Def?.driverClass);
            xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_BreakdownOutcome.xml"));
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("ThoughtDef"))
                DefDatabase<ThoughtDef>.Add(DirectXmlToObject.ObjectFromXml<ThoughtDef>(node, false));
            errors.Clear();
            DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
            Require(errors.Count == 0, string.Join('\n', errors));

            // 安装生产补丁，保留原版选池、状态赋值、Scribe 和信件构造。隔离地图网格、其 UI 更新及掷骰抽样。
            foreach (Type type in new[] { typeof(Patch_SSCSlaveBreakdownMoodSelection), typeof(Patch_SSCSlaveBreakdownClear),
                         typeof(Patch_SSCSlaveBreakdownTransition), typeof(Patch_SSCSlaveBreakdownDeath),
                         typeof(Patch_SSCSlaveBreakdownBehaviorCleanup),
                         typeof(Patch_SSCBreakdownMasturbateEverywhere), typeof(Patch_SSCBreakdownVisibility),
                         typeof(Patch_SSCBreakdownActivityLocation) })
                boundary.CreateClassProcessor(type).Patch();
            void Prefix(MethodBase method, string name) => boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), name));
            Prefix(AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.UpdateTarget)), nameof(Skip));
            Prefix(AccessTools.Method(typeof(MapPawns), "DoListChangedNotifications"), nameof(Skip));
            Prefix(AccessTools.Method(typeof(Pawn), nameof(Pawn.ClearReservationsForJob)), nameof(BreakdownReservationBoundary));
            Prefix(AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new[] { typeof(IntVec3), typeof(Map) }),
                nameof(BreakdownInvitationFogBoundary));
            Prefix(AccessTools.Method(typeof(rjw.CasualSex_Helper), nameof(rjw.CasualSex_Helper.CanTargetHookup)),
                nameof(BreakdownInvitationAppraisalBoundary));
            Prefix(AccessTools.Method(typeof(rjw.PawnDesignations_Hero), nameof(rjw.PawnDesignations_Hero.IsDesignatedHero)),
                nameof(BreakdownInvitedUseHeroBoundary));
            Prefix(AccessTools.Method(typeof(rjw.xxx), nameof(rjw.xxx.can_rape)), nameof(BreakdownInvitedUseBodyBoundary));
            Prefix(AccessTools.Method(typeof(rjw.CasualSex_Helper), nameof(rjw.CasualSex_Helper.CanHaveSex)),
                nameof(BreakdownInvitedUseBodyBoundary));
            Prefix(AccessTools.Method(typeof(MentalState_SSCSlaveBreakdown), nameof(MentalState_SSCSlaveBreakdown.CanReachInvitedUse)),
                nameof(BreakdownInvitedUseReachBoundary));
            Prefix(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)), nameof(BreakdownInvitedUseStartBoundary));
            Prefix(AccessTools.Method(typeof(rjw.JobDriver_Sex), nameof(rjw.JobDriver_Sex.setup_ticks)), nameof(BreakdownInvitedUseTimerBoundary));
            Prefix(AccessTools.Method(typeof(Rand), nameof(Rand.MTBEventOccurs)), nameof(ControlBreakdownRoll));
            Prefix(AccessTools.Method(typeof(MentalBreakWorker_SSCSlaveBreakdown), nameof(MentalBreakWorker_SSCSlaveBreakdown.TryStart)), nameof(ObserveMoodBreakChoice));
            Prefix(AccessTools.Method(typeof(MentalBreaker), "RandomFinalStraw"), nameof(Skip));
            Prefix(AccessTools.Method(typeof(GenText), nameof(GenText.AdjustedFor),
                new[] { typeof(string), typeof(Pawn), typeof(string), typeof(bool) }), nameof(BreakdownGrammarBoundary));
            Prefix(AccessTools.Method(typeof(ColoredText), nameof(ColoredText.Resolve)), nameof(BreakdownColorBoundary));
            Prefix(AccessTools.Method(typeof(MentalState_SSCSlaveBreakdown), "CanApproach"), nameof(BreakdownTrainerBoundary));
            Prefix(AccessTools.Method(typeof(MentalState_SSCSlaveBreakdown), "TrainerEligible"), nameof(BreakdownTrainerBoundary));
            Prefix(AccessTools.Method(typeof(Messages), nameof(Messages.Message),
                new[] { typeof(string), typeof(LookTargets), typeof(MessageTypeDef), typeof(bool) }), nameof(Skip));
            Prefix(AccessTools.Method(typeof(TrainingOutcomeUtility), nameof(TrainingOutcomeUtility.CalculateOpinionChange)), nameof(BreakdownOpinionBoundary));
            // 只替代与本步无关的初始准入、年龄和身体因素，保留 NeedInterval 和实际驱动力属性。
            Prefix(AccessTools.Method(typeof(rjw.xxx), nameof(rjw.xxx.is_asexual), new[] { typeof(Pawn) }), nameof(BreakdownAsexualBoundary));
            Prefix(AccessTools.Method(typeof(rjw.xxx), nameof(rjw.xxx.can_do_loving), new[] { typeof(Pawn) }), nameof(BreakdownLovingBoundary));
            Prefix(AccessTools.Method(typeof(rjw.Need_Sex), nameof(rjw.Need_Sex.GetFallFactorFor)), nameof(BreakdownFallFactorBoundary));
            Prefix(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
                new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) }),
                nameof(SkipBreakdownEffect));
            boundary.Patch(AccessTools.Method(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState)),
                transpiler: new HarmonyMethod(typeof(Program), nameof(InsertBreakdownStartInterruption)));
            boundary.Patch(receiveLetter, prefix: new HarmonyMethod(typeof(Program), nameof(CaptureBreakdownLetter)) { priority = Priority.First });

            void Case(string name, Action action)
            {
                Check(name, () =>
                {
                    runtime.CancelAllFullRequests();
                    SSCMod.settings = new SSCSettings { EnableSlaveBreakdown = true, enableCorruptionDecay = false, enableRitualEnslavement = false };
                    SetBreakdownTick(1000);
                    breakdownLetters.Clear();
                    breakdownFailureLetters.Clear();
                    Find.LetterStack.LettersListForReading.Clear();
                    reachableBreakdownTrainer = null;
                    breakdownRollSucceeds = false;
                    breakdownRolls = 0;
                    selectedBreakdownIntensity = null;
                    tickDuringImprintLetter = false;
                    skipBreakdownEffectFor = null;
                    interruptBeforeBreakdownStart = null;
                    invitedUseDispatches = 0;
                    invitedUseReachCallback = null;
                    action();
                });
            }

            Case("breakdown request and native letter reconnect after Scribe; approval consumes exactly that request", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                Require(state.IsPendingDecision(letter) && state.CanApprove(letter));

                var fixture = new SlaveBreakdownSaveFixture(pawn, SSCMod.settings) { Request = letter };
                string path = SaveBreakdown(fixture);
                Find.LetterStack.LettersListForReading.Clear();
                var (loadedPawn, _) = BreakdownPawn();
                loadedPawn.thingIDNumber = pawn.thingIDNumber;
                var loaded = LoadBreakdown(path, loadedPawn, trainer, loadedPawn);
                var restored = (MentalState_SSCSlaveBreakdown)loadedPawn.MentalState;
                Find.LetterStack.LettersListForReading.Add(loaded.Request);
                Require(loaded.Request != letter && loaded.Request.Subject == loadedPawn && loaded.Request.Trainer == trainer);
                Require(restored.IsPendingDecision(loaded.Request) && restored.CanApprove(loaded.Request));
                Action accept = loaded.Request.Choices.First().action;
                accept();
                accept();
                letter.Choices.First().action?.Invoke();
                Require(!restored.IsPendingDecision(loaded.Request));
                Equal("Approved", BreakdownField("requestPhase").GetValue(restored).ToString());
                Equal(0, Find.LetterStack.LettersListForReading.Count);
            });

            Case("rejection revokes stale accept and expiration or interruption publishes one failure", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                SetBreakdownLowMood(pawn, MentalBreakIntensity.Minor);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                var choices = letter.Choices.ToArray();
                choices[1].action();
                choices[0].action();
                Equal("None", BreakdownField("requestPhase").GetValue(state).ToString());
                Equal(4750L, (long)BreakdownField("nextRequestAt").GetValue(state));
                Require(!Find.LetterStack.LettersListForReading.Contains(letter) && pawn.InMentalState);

                var late = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                Action acceptLate = late.Choices.First().action;
                SetBreakdownTick(16000);
                acceptLate();
                state.MentalStateTick(1);
                pawn.mindState.mentalStateHandler.Reset();
                Equal(1, breakdownFailureLetters.Count);
                Require(breakdownFailureLetters[0] == pawn && !pawn.InMentalState &&
                    !Find.LetterStack.LettersListForReading.Contains(late));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Require(pawn.needs.mood.thoughts.memories.Memories.Count(thought =>
                    thought.def.defName == "SSC_BreakdownFailure") == 1);

                SetBreakdownTick(1000);
                var (interrupted, interruptedNeed) = BreakdownPawn();
                StartWanderingBreakdown(interrupted, interruptedNeed);
                interrupted.mindState.mentalStateHandler.Reset();
                interrupted.mindState.mentalStateHandler.Reset();
                Equal(2, breakdownFailureLetters.Count);
                Require(breakdownFailureLetters[1] == interrupted);
            });

            Case("approved training extends only when started and can claim success once", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                SetBreakdownLowMood(pawn, MentalBreakIntensity.Minor);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                letter.Choices.First().action();
                var job = JobMaker.MakeJob(trainingDef, pawn);
                var driver = new JobDriver_Training { pawn = trainer, job = job };
                SetBreakdownPhase(state, "Dispatched");
                BreakdownField("trainingJobId").SetValue(state, job.loadID);
                SetBreakdownTick(13500);
                Require(state.OwnsTrainingJob(trainer, job));
                var effect = pawn.health.hediffSet.hediffs.OfType<Hediff_SSCSlaveBreakdown>().Single();
                Equal(16000L, effect.ExpiresAt);
                Require(!state.ClaimTrainingSuccess(driver));
                state.MarkApprovedTrainingStarted(driver);
                Equal(18500L, effect.ExpiresAt);
                var wrongJob = JobMaker.MakeJob(trainingDef, pawn);
                Require(!state.OwnsTrainingJob(trainer, wrongJob));
                Require(state.ClaimTrainingSuccess(driver) && !state.ClaimTrainingSuccess(driver));
                state.FinishSuccessfulTraining();
                state.FinishSuccessfulTraining();
                Require(!pawn.InMentalState && !pawn.health.hediffSet.hediffs.Contains(effect) &&
                    breakdownFailureLetters.Count == 0);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                var memories = pawn.needs.mood.thoughts.memories.Memories;
                Require(memories.Count(thought => thought.def.defName == "SSC_BreakdownSuccess") == 1);
                Require(memories.Count(thought => thought.def.defName == "SSC_BreakdownTrainerOpinion" &&
                    thought.otherPawn == trainer) == 1);
            });

            Case("breakdown training guard requires the actual direction and rechecks the live approval after Start", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                letter.Choices.First().action();

                var job = JobMaker.MakeJob(trainingDef, pawn);
                var driver = new JobDriver_Training { pawn = trainer, job = job };
                SetBreakdownPhase(state, "Dispatched");
                BreakdownField("trainingJobId").SetValue(state, job.loadID);
                trainer.jobs.curJob = job;
                trainer.jobs.curDriver = driver;
                var receiver = new JobDriver_TrainingReceiver { pawn = pawn, job = JobMaker.MakeJob(trainingDef, trainer) };
                pawn.jobs.curJob = receiver.job;
                pawn.jobs.curDriver = receiver;
                try
                {
                    // 即使关闭互动保护，任务实际持有的方向也必须与批准相符。
                    SSCMod.settings.enableSexSlaveProtectionRules = false;
                    driver.Sexprops = new rjw.SexProps(pawn, trainer);
                    Require(SSCRestrictionJobGuard.TryReserve(driver, out bool allowed) && !allowed);
                    driver.Sexprops = new rjw.SexProps(trainer, pawn);
                    Require(SSCRestrictionJobGuard.TryReserve(driver, out allowed) && allowed);
                    Require(SSCRestrictionJobGuard.TryReserve(receiver, out allowed) && allowed);
                    Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));
                    Require(SSCRestrictionJobGuard.HasStartedScene(receiver, SSCInteractionKind.DailyTraining));

                    // 同一身体换人格会撤销旧批准。已有的 Start 记录不能让接收端跳过特批复查。
                    Require(SSCPersonalityRuntimeUtility.TryBeginMutation(trainer, out var mutation));
                    using (mutation)
                    {
                        Require(SSCRestrictionJobGuard.TryReserve(receiver, out allowed) && !allowed);
                        Require(SSCRestrictionJobGuard.TryReserve(driver, out allowed) && !allowed);
                    }
                }
                finally
                {
                    trainer.jobs.curDriver = pawn.jobs.curDriver = null;
                    trainer.jobs.curJob = pawn.jobs.curJob = null;
                }
            });

            Case("the scene bonus feeds both scoring modes without writing a bond or an inflated history score", () =>
            {
                SkillDef previousSocial = SkillDefOf.Social;
                try
                {
                    SkillDefOf.Social = DefDatabase<SkillDef>.GetNamedSilentFail("Social") ?? new SkillDef { defName = "Social" };
                    if (DefDatabase<SkillDef>.GetNamedSilentFail("Social") == null) DefDatabase<SkillDef>.Add(SkillDefOf.Social);
                    var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                    trainer.skills = new Pawn_SkillTracker(trainer);
                    trainer.skills.GetSkill(SkillDefOf.Social).Level = 15;
                    foreach (bool legacy in new[] { false, true })
                    {
                        SSCMod.settings.useOldScoring = legacy;
                        var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                        need.SetCorruption(.30f);
                        var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                        comp.lastTrainingScore = 12f;
                        ConditioningUtility.ExecuteBreakdownOutcome(trainer, pawn, 5f);
                        Require(Math.Abs(need.CurLevel - (legacy ? .38f : .42f)) < .0001f);
                        Equal(legacy ? 12f : 5f, comp.lastTrainingScore);
                        Require(!Utils.HasChainHediff(pawn) && SSCBondUtility.GetBoundMaster(pawn) == null);
                    }
                }
                finally { SkillDefOf.Social = previousSocial; }
            });

            Case("one actual full crossing differentiates first and sends exactly one letter for each outcome", () =>
            {
                var (pawn, need) = BreakdownPawn();
                Mark(pawn, SSCImprintUtility.InitialDefName);
                need.SetCorruption(.9999f);
                Require(!runtime.HasFullRequest(pawn));
                tickDuringImprintLetter = true;
                need.SetCorruption(1f);
                Require(!tickDuringImprintLetter);
                Require(runtime.HasFullRequest(pawn) && pawn.MentalState == null);
                Require(breakdownLetters.SequenceEqual(new[] { (pawn, "imprint", SSCImprintKind.Slave, false) }));
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                need.SetCorruption(1f);
                need.NeedInterval();
                runtime.GameComponentTick();
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Require(breakdownLetters.SequenceEqual(new[] { (pawn, "imprint", SSCImprintKind.Slave, false),
                    (pawn, "breakdown", SSCImprintKind.Slave, true) }));
                pawn.mindState.mentalStateHandler.Reset();
                Require(!pawn.health.hediffSet.HasHediff(breakdownEffect));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(pawn));
                Equal(2, breakdownLetters.Count);
            });

            Case("solo opening resumes the exact saved job without querying a second candidate", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
                var job = new Job { loadID = 381, def = new JobDef { defName = "SSC_TestOpeningSolo" } };
                var solo = new BreakdownCandidateNode(job);
                var opening = new ThinkNode_SSCSlaveBreakdownOpening();
                AccessTools.Field(typeof(ThinkNode), "subNodes").SetValue(opening, new List<ThinkNode> { solo });
                Equal(job, opening.TryIssueJobPackage(pawn, default).Job);
                Require(!opening.TryIssueJobPackage(pawn, default).IsValid);
                Equal(1, solo.Calls);

                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                LoadBreakdown(path, loaded);
                loaded.jobs.curJob = new Job { loadID = job.loadID, def = job.def };
                var after = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                var branch = new ThinkNode_SSCSlaveBreakdownBehavior();
                Equal(loaded.CurJob, branch.TryIssueJobPackage(loaded, default).Job);
                loaded.jobs.curDriver = new JobDriver_Wait { pawn = loaded, job = loaded.CurJob };
                loaded.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, after.Phase);
                Require(!opening.TryIssueJobPackage(loaded, default).IsValid);
                Equal(1, solo.Calls);
            });

            Case("an opening candidate rejected during dispatch retires to wandering without retrying", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
                var job = new Job { loadID = 386, def = new JobDef { defName = "SSC_TestRejectedOpening" } };
                var solo = new BreakdownCandidateNode(job);
                var opening = new ThinkNode_SSCSlaveBreakdownOpening();
                var branch = new ThinkNode_SSCSlaveBreakdownBehavior();
                var children = AccessTools.Field(typeof(ThinkNode), "subNodes");
                children.SetValue(opening, new List<ThinkNode> { solo });
                var wanderJob = new Job { loadID = 387, def = new JobDef { defName = "SSC_TestWander" } };
                children.SetValue(branch, new List<ThinkNode> { opening, new BreakdownCandidateNode(wanderJob) });

                Equal(job, branch.TryIssueJobPackage(pawn, default).Job);
                // 原版可能在准备阶段拒绝，既没安装候选也没发 CleanupCurrentJob。
                Require(pawn.CurJob == null && state.OwnsBehaviorJob(job));
                // 读档后即使当前仍是派发当轮，已记录的编号也会阻止再次选取候选。
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                LoadBreakdown(path, loaded);
                var after = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                Require(!opening.TryIssueJobPackage(loaded, default).IsValid);
                SetBreakdownTick(1001);
                Equal(wanderJob, branch.TryIssueJobPackage(loaded, default).Job);
                Require(!after.HasBehaviorJob);
                Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, after.Phase);
                Equal(1, solo.Calls);
            });

            Case("an opening with no solo candidate enters wandering and remains consumed after loading", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
                var solo = new BreakdownCandidateNode(null);
                var opening = new ThinkNode_SSCSlaveBreakdownOpening();
                var branch = new ThinkNode_SSCSlaveBreakdownBehavior();
                var children = AccessTools.Field(typeof(ThinkNode), "subNodes");
                children.SetValue(opening, new List<ThinkNode> { solo });
                var wanderJob = new Job { loadID = 391, def = new JobDef { defName = "SSC_TestWander" } };
                children.SetValue(branch, new List<ThinkNode> { opening, new BreakdownCandidateNode(wanderJob) });
                Equal(wanderJob, branch.TryIssueJobPackage(pawn, default).Job);
                Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, state.Phase);

                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                LoadBreakdown(path, loaded);
                Equal(wanderJob, branch.TryIssueJobPackage(loaded, default).Job);
                Equal(1, solo.Calls);
            });

            Case("debug start enters the real opening without changing corruption and keeps public activity scoped", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (partner, _) = BreakdownPawn();
                var (bystander, _) = BreakdownPawn();
                var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                comp.RecordSlaveBreakdownCooldown(5000);
                need.SetCorruption(.42f);
                SSCMod.settings.EnableSlaveBreakdown = false;
                SSCMod.settings.enableDebugGizmos = true;
                var prefs = (PrefsData)AccessTools.Field(typeof(Prefs), "data").GetValue(null);
                bool previousDevMode = prefs.devMode;
                prefs.devMode = true;
                try
                {
                    Require(runtime.TryStartForDebug(pawn) && !runtime.TryStartForDebug(pawn));
                    var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
                    Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Solo, state.Phase);
                    Near(.42f, need.CurLevel);
                    Require(!comp.SlaveBreakdownFullTriggerUsed && !SSCBreakdownPublicActivity.Permits(pawn, partner));

                    Job opener = JobMaker.MakeJob(new JobDef { defName = "SSC_TestPublicSolo" }, pawn);
                    state.SetBehaviorJob(opener);
                    pawn.jobs.curJob = opener;
                    Require(SSCBreakdownPublicActivity.Permits(pawn));
                    Require(!SSCBreakdownPublicActivity.Permits(pawn, partner));
                    Require(!SSCBreakdownPublicActivity.Permits(pawn, bystander));
                    Require(!rjw.CasualSex_Helper.MightBeSeen(new List<Pawn> { bystander }, pawn.Position, pawn, null));
                    pawn.jobs.curJob = null;
                    state.CompleteBehaviorJob();
                    Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, state.Phase);

                    var willingness = AccessTools.Method(typeof(rjw.JobGiver_Masturbate), "WillingToMasturbateEverywhere");
                    bool oldPublicSetting = rjw.RJWPreferenceSettings.FapEverywhere;
                    rjw.RJWPreferenceSettings.FapEverywhere = false;
                    try
                    {
                        var jobGiver = new JobGiver_SSCSlaveBreakdownMasturbate();
                        Require(!(bool)willingness.Invoke(jobGiver, new object[] { pawn }));
                        JobGiver_SSCSlaveBreakdownMasturbate.CandidatePawn = pawn;
                        Require((bool)willingness.Invoke(jobGiver, new object[] { pawn }));
                        Require(!(bool)willingness.Invoke(jobGiver, new object[] { bystander }));
                    }
                    finally
                    {
                        JobGiver_SSCSlaveBreakdownMasturbate.CandidatePawn = null;
                        rjw.RJWPreferenceSettings.FapEverywhere = oldPublicSetting;
                    }
                }
                finally { prefs.devMode = previousDevMode; }
            });

            Case("native suspension removes only the original opening job from the queue before wandering", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
                var openingJob = new Job { loadID = 388, def = new JobDef { defName = "SSC_TestSuspendedOpening", suspendable = true } };
                var unrelated = new Job { loadID = 390, def = new JobDef { defName = "SSC_TestUnrelatedJob" }, playerForced = true };
                pawn.jobs.jobQueue.EnqueueLast(unrelated);
                pawn.jobs.curJob = openingJob;
                pawn.jobs.curDriver = new JobDriver_Wait { pawn = pawn, job = openingJob };
                state.SetBehaviorJob(openingJob);
                pawn.jobs.SuspendCurrentJob(JobCondition.InterruptForced);
                Require(pawn.CurJob == null && !state.HasBehaviorJob);
                Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, state.Phase);
                Equal(unrelated, pawn.jobs.jobQueue.Single().job);
            });

            Case("wander invitations preserve both cooldowns across Scribe and consume only an actual proposal", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (first, _) = BreakdownPawn();
                var (second, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(pawn, need);
                var position = AccessTools.Field(typeof(Thing), "positionInt");
                position.SetValue(pawn, IntVec3.Zero);
                position.SetValue(first, new IntVec3(15, 0, 0));
                position.SetValue(second, new IntVec3(16, 0, 0));
                Require(state.TryBeginInvitationScan());
                Require(state.CanInvite(first) && !state.CanInvite(second));
                // An empty scan is throttled, but leaves the 1,250-tick invitation cooldown untouched.
                SetBreakdownTick(1150);
                Require(state.TryBeginInvitationScan());
                Require(state.TryRecordInvitation(first, SSCPersonalityRuntimeUtility.Capture(first), false, out Job rejected));
                Require(rejected == null && !state.HasBehaviorJob);

                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                position.SetValue(loaded, IntVec3.Zero);
                position.SetValue(second, new IntVec3(1, 0, 0));
                LoadBreakdown(path, loaded, first);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                SetBreakdownTick(2399);
                Require(!restored.TryBeginInvitationScan());
                SetBreakdownTick(2400);
                Require(restored.TryBeginInvitationScan() && !restored.CanInvite(first) && restored.CanInvite(second));
                SetBreakdownTick(3649);
                Require(!restored.CanInvite(first));
                SetBreakdownTick(3650);
                Require(restored.CanInvite(first));
            });

            Case("training approval cancels invitation preparation but waits for a Quickie that actually started", () =>
            {
                foreach (bool started in new[] { false, true })
                {
                    var (pawn, need) = BreakdownPawn();
                    var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                    var partner = started ? trainer : BreakdownPawn().Pawn;
                    SetBreakdownTick(1000);
                    reachableBreakdownTrainer = trainer;
                    var state = StartWanderingBreakdown(pawn, need);
                    var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                    // A pending letter leaves the invitation slot free.
                    Require(state.TryBeginInvitationScan());
                    var (job, driver) = InstallBreakdownInvitation(pawn, state, partner);
                    var invitation = state.InvitationFor(job);
                    Job receive = JobMaker.MakeJob(rjw.xxx.getting_quickie, pawn, partner);
                    JobCondition? receiverOutcome = null;
                    RunBreakdownInvitationPreparation(driver, () =>
                    {
                        partner.jobs.curJob = receive;
                        var receiving = new rjw.JobDriver_SexBaseRecieverQuickie { pawn = partner, job = receive };
                        receiving.AddFinishAction(condition => receiverOutcome = condition);
                        partner.jobs.curDriver = receiving;
                    });
                    Require(invitation.HasReceiver(pawn));
                    if (started) Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));

                    state.Decide(letter, true);
                    Require(state.HasApprovedTraining && !state.TryBeginInvitationScan());
                    if (started)
                    {
                        Require(state.InvitationFor(pawn.CurJob) == invitation && partner.CurJob == receive);
                        Require(!state.CanReceiveBreakdownTraining);
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
                    }
                    Require(!state.HasBehaviorJob && pawn.CurJob == null && partner.CurJob == null);
                    Require(state.CanReceiveBreakdownTraining && state.HasApprovedTraining);
                    Equal(started ? JobCondition.Succeeded : JobCondition.Incompletable, receiverOutcome.GetValueOrDefault());
                    SetBreakdownTick(2250);
                    Require(!state.TryBeginInvitationScan());
                }
            });

            Case("an invitation cannot enter RJW Start without its own installed receiver", () =>
            {
                foreach (bool directStart in new[] { false, true })
                {
                    var (pawn, need) = BreakdownPawn();
                    var (partner, _) = BreakdownPawn();
                    var ownWork = JobMaker.MakeJob(JobDefOf.Wait, 300);
                    partner.jobs.curJob = ownWork;
                    partner.jobs.curDriver = new JobDriver_Wait { pawn = partner, job = ownWork };
                    var state = StartWanderingBreakdown(pawn, need);
                    var (_, driver) = InstallBreakdownInvitation(pawn, state, partner);

                    // The native receiver installation can fail while the partner's original work is still current.
                    if (directStart)
                        Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool allowed) && !allowed);
                    else
                    {
                        bool ranNativeScene = false;
                        RunBreakdownInvitationPreparation(driver, () => ranNativeScene = true, ToilCompleteMode.Never);
                        Require(!ranNativeScene);
                    }
                    Require(!state.HasBehaviorJob && pawn.CurJob == null && partner.CurJob == ownWork);
                }
            });

            Case("a loaded invitation resumes its original job and interruption removes only its saved preparation", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (partner, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(pawn, need);
                var (job, driver) = InstallBreakdownInvitation(pawn, state, partner);
                Job prepare = JobMaker.MakeJob(JobDefOf.Wait, 100);
                RunBreakdownInvitationPreparation(driver, () => partner.jobs.jobQueue.EnqueueLast(prepare));
                Equal(prepare, partner.jobs.jobQueue.Single().job);
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));

                var (loaded, _) = BreakdownPawn();
                var resumedJob = new Job { def = job.def, targetA = partner, loadID = job.loadID };
                loaded.jobs.curJob = resumedJob;
                loaded.jobs.curDriver = new rjw.JobDriver_SexQuick { pawn = loaded, job = resumedJob };
                LoadBreakdown(path, loaded, partner);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                SetBreakdownTick(2300); // The cooldown elapsed, but the exact saved invitation still owns the slot.
                Equal(resumedJob, new ThinkNode_SSCSlaveBreakdownBehavior().TryIssueJobPackage(loaded, default).Job);
                Require(restored.InvitationFor(resumedJob)?.Partner == partner && !restored.TryBeginInvitationScan());

                Job unrelated = JobMaker.MakeJob(JobDefOf.Wait, 200);
                partner.jobs.jobQueue.EnqueueLast(unrelated);
                loaded.mindState.mentalStateHandler.Reset();
                Require(loaded.CurJob == null && !restored.HasBehaviorJob);
                Equal(unrelated, partner.jobs.jobQueue.Single().job);
            });

            Case("saving just after invitee personality replacement cannot reauthorize the old invitation", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (partner, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(pawn, need);
                var (job, driver) = InstallBreakdownInvitation(pawn, state, partner);
                Job prepare = JobMaker.MakeJob(JobDefOf.Wait, 100);
                RunBreakdownInvitationPreparation(driver, () => partner.jobs.jobQueue.EnqueueLast(prepare));
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(partner, out var mutation));
                mutation.Dispose();

                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                var resumedJob = new Job { def = job.def, targetA = partner, loadID = job.loadID };
                loaded.jobs.curJob = resumedJob;
                loaded.jobs.curDriver = new rjw.JobDriver_SexQuick { pawn = loaded, job = resumedJob };
                LoadBreakdown(path, loaded, partner);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                SetBreakdownTick(1001);
                restored.MentalStateTick(1);
                Require(loaded.MentalState == restored && loaded.CurJob == null && !restored.HasBehaviorJob &&
                    partner.jobs.jobQueue.Count == 0 && !restored.TryBeginInvitationScan());
            });

            Case("invited use and ordinary invitations share cooldowns and refusal never dispatches", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var (other, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                Require(state.TryRecordInvitation(actor, SSCPersonalityRuntimeUtility.Capture(actor), false,
                    out Job rejected, invitedUse: true));
                Require(rejected == null && !state.HasBehaviorJob && !state.TryBeginInvitationScan());
                state.MentalStateTick(1);
                Equal(0, invitedUseDispatches);

                SetBreakdownTick(2250);
                Require(state.TryBeginInvitationScan() && !state.CanInvite(actor) && !state.CanInvite(actor, invitedUse: true));
                Require(state.TryRecordInvitation(other, SSCPersonalityRuntimeUtility.Capture(other), false, out _));
                SetBreakdownTick(3500);
                Require(state.TryBeginInvitationScan() && state.CanInvite(actor, invitedUse: true) && !state.CanInvite(other));
            });

            Case("an accepted invited use dispatches once across load and uses the actual RJW forced direction", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                RecordInvitedUse(state, actor);
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(subject, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                LoadBreakdown(path, loaded, actor);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;

                restored.MentalStateTick(1);
                restored.MentalStateTick(1);
                Equal(1, invitedUseDispatches);
                Require(actor.jobs.curDriver is JobDriver_SSCBreakdownInvitedUse && loaded.CurJob == null);
                var driver = (JobDriver_SSCBreakdownInvitedUse)actor.jobs.curDriver;
                Require(SSCRestrictionJobContext.TryCreate(driver, out var request) &&
                    request.DirectionKnown && request.Kind == SSCInteractionKind.Forced &&
                    request.Initiator == actor && request.Receiver == loaded);
                Require(SSCBreakdownPublicActivity.Permits(actor, loaded) && !SSCBreakdownPublicActivity.Permits(loaded, actor));

                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, actor);
                RunBreakdownInvitationPreparation(driver, () =>
                {
                    loaded.jobs.curJob = receiver;
                    loaded.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = loaded, job = receiver };
                });
                Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool allowed) && allowed);
                Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));
                Equal(receiver, new ThinkNode_SSCSlaveBreakdownBehavior().TryIssueJobPackage(loaded, default).Job);

                path = SaveBreakdown(new SlaveBreakdownSaveFixture(loaded, SSCMod.settings));
                LoadBreakdown(path, loaded, actor);
                restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                restored.MentalStateTick(1);
                Equal(1, invitedUseDispatches);
                Require(restored.InvitedUseReceiverFor(receiver) != null && actor.jobs.curDriver == driver);
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
                Require(!restored.HasBehaviorJob && loaded.CurJob == null && actor.CurJob == null);
            });

            Case("invited use rechecks replaced work after path callbacks and cancels its actor when the receiver is interrupted", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                RecordInvitedUse(state, actor);
                Job otherJob = JobMaker.MakeJob(JobDefOf.Wait, 100);
                // Let the original availability check pass. An integration callback swaps the work during path validation.
                invitedUseReachCallback = () =>
                {
                    actor.jobs.curJob = otherJob;
                    actor.jobs.curDriver = new JobDriver_Wait { pawn = actor, job = otherJob };
                };
                state.MentalStateTick(1);
                Require(!state.HasBehaviorJob && actor.CurJob == otherJob);
                Equal(0, invitedUseDispatches);
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);

                SetBreakdownTick(2250);
                RecordInvitedUse(state, actor);
                state.MentalStateTick(1);
                var driver = (JobDriver_SSCBreakdownInvitedUse)actor.jobs.curDriver;
                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, actor);
                RunBreakdownInvitationPreparation(driver, () =>
                {
                    subject.jobs.curJob = receiver;
                    subject.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = subject, job = receiver };
                });
                Require(state.InvitedUseReceiverFor(receiver) != null);
                Job unrelated = JobMaker.MakeJob(JobDefOf.Wait, 200);
                actor.jobs.jobQueue.EnqueueLast(unrelated);
                subject.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Require(!state.HasBehaviorJob && subject.CurJob == null && actor.CurJob == null);
                Equal(unrelated, actor.jobs.jobQueue.Single().job);
            });

            Case("a started invited use cannot settle after its exact receiver is lost", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                RecordInvitedUse(state, actor);
                state.MentalStateTick(1);
                var driver = (JobDriver_SSCBreakdownInvitedUse)actor.jobs.curDriver;
                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, actor);
                RunBreakdownInvitationPreparation(driver, () =>
                {
                    subject.jobs.curJob = receiver;
                    subject.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = subject, job = receiver };
                });
                Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool allowed) && allowed);
                Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));

                // Model a receiver changed before the actor ticks, without relying on the normal cleanup notification.
                Job unrelated = JobMaker.MakeJob(JobDefOf.Wait, 200);
                subject.jobs.curJob = unrelated;
                subject.jobs.curDriver = new JobDriver_Wait { pawn = subject, job = unrelated };
                bool resolvedNativeOutcome = false;
                RunBreakdownInvitationPreparation(driver, () => resolvedNativeOutcome = true);
                Require(!resolvedNativeOutcome && !state.HasBehaviorJob && actor.CurJob == null);
                Equal(unrelated, subject.CurJob);
            });

            Case("invited use preserves saved RJW timers when native toils are rebuilt on load", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                RecordInvitedUse(state, actor);
                state.MentalStateTick(1);
                var driver = (JobDriver_SSCBreakdownInvitedUse)actor.jobs.curDriver;
                var saved = (300, 1200, 50, 180, 730, 90, 92, 120);
                (driver.ticks_left, driver.duration, driver.sex_ticks, driver.orgasmstick, driver.orgasmStartTick,
                    driver.ticks_between_hearts, driver.ticks_between_hits, driver.ticks_between_thrusts) = saved;
                var makeToils = AccessTools.DeclaredMethod(typeof(JobDriver_SSCBreakdownInvitedUse), "MakeNewToils");

                Scribe.mode = LoadSaveMode.PostLoadInit;
                try
                {
                    var toils = ((IEnumerable<Toil>)makeToils.Invoke(driver, null)).ToList();
                    Equal(1200, toils.Single(toil => toil.defaultCompleteMode == ToilCompleteMode.Never).defaultDuration);
                    Equal(saved, (driver.ticks_left, driver.duration, driver.sex_ticks, driver.orgasmstick, driver.orgasmStartTick,
                        driver.ticks_between_hearts, driver.ticks_between_hits, driver.ticks_between_thrusts));
                }
                finally { Scribe.mode = LoadSaveMode.Inactive; }
            });

            Case("approval withdraws accepted invited use before it can take the other pawn's job", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = actor;
                var state = StartWanderingBreakdown(subject, need);
                var letter = InstallBreakdownRequest(state, subject, actor, requestDef);
                RecordInvitedUse(state, actor);

                state.Decide(letter, true);
                Require(state.HasApprovedTraining && !state.HasBehaviorJob && state.CanReceiveBreakdownTraining);
                Require(actor.CurJob == null && actor.jobs.jobQueue.Count == 0 && subject.CurJob == null);
                Equal(0, invitedUseDispatches);
                SetBreakdownTick(2250);
                Require(!state.TryBeginInvitationScan());
            });

            Case("one breakdown carries its single opening, both invitation cooldowns, and training handoff through a save", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (ordinaryPartner, _) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var state = (MentalState_SSCSlaveBreakdown)subject.MentalState;

                Job openingJob = JobMaker.MakeJob(JobDefOf.Wait, 100);
                var solo = new BreakdownCandidateNode(openingJob);
                var opening = new ThinkNode_SSCSlaveBreakdownOpening();
                AccessTools.Field(typeof(ThinkNode), "subNodes").SetValue(opening, new List<ThinkNode> { solo });
                Equal(openingJob, opening.TryIssueJobPackage(subject, default).Job);
                subject.jobs.curJob = openingJob;
                subject.jobs.curDriver = new JobDriver_Wait { pawn = subject, job = openingJob };
                Require(!state.TryBeginInvitationScan());
                subject.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
                Require(!opening.TryIssueJobPackage(subject, default).IsValid);
                Equal(1, solo.Calls);

                var request = InstallBreakdownRequest(state, subject, trainer, requestDef);
                Require(state.TryBeginInvitationScan());
                InstallBreakdownInvitation(subject, state, ordinaryPartner);
                subject.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                SetBreakdownTick(2249);
                Require(!state.TryBeginInvitationScan());
                SetBreakdownTick(2250);
                Require(state.TryBeginInvitationScan());
                Require(state.TryRecordInvitation(ordinaryPartner, SSCPersonalityRuntimeUtility.Capture(ordinaryPartner),
                    false, out Job rejected, invitedUse: true) && rejected == null);
                SetBreakdownTick(3500);
                Require(state.TryBeginInvitationScan() && !state.CanInvite(ordinaryPartner));
                RecordInvitedUse(state, trainer);

                // Save while the training choice and the accepted reverse invitation are both outstanding.
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(subject, SSCMod.settings) { Request = request });
                Find.LetterStack.LettersListForReading.Clear();
                var (loaded, _) = BreakdownPawn();
                loaded.thingIDNumber = subject.thingIDNumber;
                var fixture = LoadBreakdown(path, loaded, ordinaryPartner, trainer, loaded);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                Find.LetterStack.LettersListForReading.Add(fixture.Request);
                restored.MentalStateTick(1);
                Equal(1, invitedUseDispatches);
                var driver = (JobDriver_SSCBreakdownInvitedUse)trainer.jobs.curDriver;
                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, trainer);
                RunBreakdownInvitationPreparation(driver, () =>
                {
                    loaded.jobs.curJob = receiver;
                    loaded.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = loaded, job = receiver };
                });
                Require(restored.InvitedUseReceiverFor(receiver) != null && restored.CanApprove(fixture.Request));

                restored.Decide(fixture.Request, true);
                Require(restored.HasApprovedTraining && restored.CanReceiveBreakdownTraining &&
                    restored.Phase == MentalState_SSCSlaveBreakdown.OpeningPhase.Wander &&
                    trainer.CurJob == null && loaded.CurJob == null);
                SetBreakdownTick(4750);
                Require(!restored.TryBeginInvitationScan() && !opening.TryIssueJobPackage(loaded, default).IsValid);
                Equal(1, invitedUseDispatches);
                Equal(1, solo.Calls);
            });

            Case("disabling a native gather spot switches available anchors immediately", () =>
            {
                var (pawn, _) = BreakdownPawn();
                var (first, _) = BreakdownPawn();
                var (second, _) = BreakdownPawn();
                var removed = new CompGatherSpot { parent = first };
                var remaining = new CompGatherSpot { parent = second };
                breakdownMap.gatherSpotLister.RegisterActivated(removed);
                breakdownMap.gatherSpotLister.RegisterActivated(remaining);
                try
                {
                    Equal(2, JobGiver_SSCSlaveBreakdownWander.ActiveGatherSpots(pawn).Count());
                    removed.Active = false;
                    Equal(remaining, JobGiver_SSCSlaveBreakdownWander.ActiveGatherSpots(pawn).Single());
                }
                finally
                {
                    breakdownMap.gatherSpotLister.RegisterDeactivated(removed);
                    breakdownMap.gatherSpotLister.RegisterDeactivated(remaining);
                }
            });

            Case("temporary role and native need effects end on interruption while completed FeelingBroken progress persists", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var comp = new rjw.CompRJW { parent = pawn };
                AccessTools.Field(typeof(rjw.CompRJW), "pawnData").SetValue(comp, new rjw.PawnData(pawn) { raceSexDrive = 1f });
                ((List<ThingComp>)AccessTools.Field(typeof(ThingWithComps), "comps").GetValue(pawn)).Add(comp);
                var sexNeed = new rjw.Need_Sex(pawn) { def = new NeedDef {
                    defName = "SSC_TestSexNeed", needClass = typeof(rjw.Need_Sex) } };
                pawn.needs.AllNeeds.Add(sexNeed);
                float DecayOneInterval()
                {
                    sexNeed.CurLevel = 1f;
                    sexNeed.NeedInterval();
                    return 1f - sexNeed.CurLevel;
                }
                float baseline = DecayOneInterval();
                Require(baseline > 0f && baseline < .02f);
                Near(1f, rjw.xxx.get_vulnerability(pawn));
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                Near(0f, sexNeed.CurLevel);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Require(SSCIdentityUtility.IsManualIdentityChangeBlocked(pawn));
                Require(!SSCIdentityUtility.TrySetManualIdentity(pawn, PawnIdentity.Unset));
                Require(!SSCPersonalityExcretionAdmission.CanExtract(pawn));
                Near(baseline * 10f, DecayOneInterval());
                Near(.25f, pawn.GetStatValue(rjw.RJWStatDefOf.SexSatisfaction));
                Near(3f, rjw.xxx.get_vulnerability(pawn));

                SetBreakdownTick(3499);
                pawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Require(!pawn.health.hediffSet.HasHediff(feelingBroken));
                SetBreakdownTick(3500);
                pawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                var broken = pawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken);
                Near(.03f, broken.Severity);
                SetBreakdownTick(5999);
                pawn.mindState.mentalStateHandler.Reset();
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Require(!pawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(!SSCIdentityUtility.IsManualIdentityChangeBlocked(pawn));
                Require(SSCPersonalityExcretionAdmission.CanExtract(pawn));
                Near(baseline, DecayOneInterval());
                Near(1f, rjw.xxx.get_vulnerability(pawn));
                SetBreakdownTick(16000);
                pawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Near(.03f, broken.Severity);
            });

            Case("personality excretion pauses during a native non-SSC state and keeps the preparation", () =>
            {
                var (pawn, _) = BreakdownPawn();
                var prep = new Hediff_SSC_PersonalityExcreting
                    { def = SSCDefOf.SSC_PersonalityExcreting, pawn = pawn, Severity = .75f };
                pawn.health.AddHediff(prep);
                var external = new MentalStateDef { defName = "SSC_TestExcretionState", category = MentalStateCategory.Misc };
                var handler = pawn.mindState.mentalStateHandler;
                Require(handler.TryStartMentalState(external));
                Require(!SSCPersonalityExcretionAdmission.CanExtract(pawn));
                Require(!SSCPersonalityExcretionAdmission.TryContinueExtraction(pawn, null));
                Require(pawn.health.hediffSet.hediffs.Contains(prep) && !prep.ShouldRemove);
                handler.Reset();
                Require(SSCPersonalityExcretionAdmission.CanExtract(pawn));
            });

            Case("a missing start effect retracts the role and does not consume the crossing or send a letter", () =>
            {
                var (pawn, need) = BreakdownPawn();
                skipBreakdownEffectFor = pawn;
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState == null && !pawn.health.hediffSet.HasHediff(breakdownEffect));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                Equal(0L, comp.SlaveBreakdownCooldownUntil);
                Require(!comp.SlaveBreakdownFullTriggerUsed && breakdownLetters.Count == 0);
                skipBreakdownEffectFor = null;
                need.SetCorruption(.99f);
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown && comp.SlaveBreakdownFullTriggerUsed);
                Equal(1, breakdownLetters.Count);
            });

            Case("native interruption before PostStart leaves no cooldown or begin letter", () =>
            {
                var (pawn, need) = BreakdownPawn();
                interruptBeforeBreakdownStart = pawn;
                need.SetCorruption(1f);
                runtime.GameComponentTick();

                Require(interruptBeforeBreakdownStart == null && pawn.MentalState == null);
                Require(!pawn.health.hediffSet.HasHediff(breakdownEffect));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                Equal(0L, comp.SlaveBreakdownCooldownUntil);
                Require(!comp.SlaveBreakdownFullTriggerUsed && breakdownLetters.Count == 0);
            });

            Case("native death teardown clears the temporary role before discarding the mind tracker", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var handler = pawn.mindState.mentalStateHandler;
                Require(handler.CurState is MentalState_SSCSlaveBreakdown);
                SetBreakdownTick(3500);
                handler.MentalStateHandlerTickInterval(1);
                // 调用 Pawn.Kill 的原版死亡及卸载入口。省去尸体生成和地图上的死亡表现。
                pawn.health.SetDead();
                PawnComponentsUtility.RemoveComponentsOnKilled(pawn);

                Require(pawn.mindState == null && handler.CurState == null);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Require(!pawn.health.hediffSet.HasHediff(breakdownEffect));
                Near(.03f, pawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
                Equal(63500L, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownUntil);
            });

            Case("silent replacement settles the old episode and preserves the slave role present at entry", () =>
            {
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var handler = pawn.mindState.mentalStateHandler;
                Require(handler.CurState is MentalState_SSCSlaveBreakdown);
                SetBreakdownTick(3500);
                var external = new MentalStateDef { defName = "SSC_TestSilentReplacement", category = MentalStateCategory.Misc };
                Require(handler.TryStartMentalState(external, transitionSilently: true));
                Equal(external, handler.CurStateDef);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Equal(63500L, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownUntil);
                Require(!pawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(!SSCIdentityUtility.IsManualIdentityChangeBlocked(pawn));
                Near(.03f, pawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
            });

            Case("replacement before the first loaded tick withdraws the old effect without rolling back the incoming role", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                LoadBreakdown(path, loaded);
                Require(loaded.MentalState is MentalState_SSCSlaveBreakdown);
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(loaded, out var mutation));
                using (mutation)
                    Require(SSCIdentityUtility.TryRestorePersonalityIdentity(loaded, PawnIdentity.Slave, false));

                var effect = loaded.health.hediffSet.GetFirstHediffOfDef(breakdownEffect);
                Require(effect.ShouldRemove);
                SetBreakdownTick(3500);
                loaded.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Require(loaded.MentalState == null && !loaded.health.hediffSet.HasHediff(breakdownEffect));
                Require(!loaded.health.hediffSet.HasHediff(feelingBroken));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(loaded));
            });

            Case("full breakdown waits for imprint retries and continues from either final outcome", () =>
            {
                foreach (bool succeeds in new[] { true, false })
                {
                    var (pawn, need) = BreakdownPawn();
                    Mark(pawn, SSCImprintUtility.InitialDefName);
                    FailNextFullImprint(pawn);
                    need.SetCorruption(1f);
                    runtime.GameComponentTick();
                    Require(pawn.MentalState == null && runtime.HasFullRequest(pawn));
                    FailNextFullImprint(pawn);
                    need.NeedInterval();
                    runtime.GameComponentTick();
                    Require(pawn.MentalState == null && runtime.HasFullRequest(pawn));
                    if (!succeeds) FailNextFullImprint(pawn);
                    need.NeedInterval();
                    runtime.GameComponentTick();
                    Require(pawn.MentalState is MentalState_SSCSlaveBreakdown && !runtime.HasFullRequest(pawn));
                    var types = breakdownLetters.Where(letter => letter.Pawn == pawn).Select(letter => letter.Type);
                    Require(types.SequenceEqual(succeeds ? new[] { "imprint", "breakdown" } : new[] { "breakdown" }));
                }
            });

            Case("first binding during the same crossing sends the imprint letter and excludes the newly chained pawn", () =>
            {
                foreach (bool retry in new[] { false, true })
                {
                    breakdownLetters.Clear();
                    SSCMod.settings.useOldScoring = retry;
                    Pawn master = ImprintPawn(PawnIdentity.Master);
                    var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                    Mark(pawn, SSCImprintUtility.InitialDefName);
                    need.RestoreCorruption(.96f, .96f);
                    if (retry) FailNextFullImprint(pawn);
                    ConditioningUtility.ExecuteRitualOutcome(master, pawn, 1f);
                    Require(SSCBondUtility.IsBoundTo(pawn, master) && need.CurLevel == 1f);
                    runtime.GameComponentTick();
                    if (retry)
                    {
                        Equal(0, breakdownLetters.Count);
                        need.NeedInterval();
                        runtime.GameComponentTick();
                    }
                    Require(pawn.MentalState == null && !runtime.HasFullRequest(pawn));
                    Require(breakdownLetters.SequenceEqual(new[] { (pawn, "imprint", SSCImprintKind.Slave, false) }));
                }
            });

            Case("random checks raw 95 percent before decay while a restored full value has no random or direct trigger", () =>
            {
                breakdownRollSucceeds = true;
                SSCMod.settings.enableCorruptionDecay = true;
                SSCMod.settings.corruptionDecayPerDay = .20f;
                var (pawn, need) = BreakdownPawn();
                need.RestoreCorruption(MathF.BitDecrement(.95f), .95f);
                need.NeedInterval();
                Equal(0, breakdownRolls);
                need.SetCorruption(.95f);
                need.NeedInterval();
                Equal(1, breakdownRolls);
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown && need.CurLevel < .95f);

                SSCMod.settings.enableCorruptionDecay = false;
                var (restored, restoredNeed) = BreakdownPawn();
                restoredNeed.RestoreCorruption(1f, 1f);
                restoredNeed.NeedInterval();
                runtime.GameComponentTick();
                Equal(1, breakdownRolls);
                Require(restored.MentalState == null);
            });

            Case("only native mood selection can choose the three breakdown intensities", () =>
            {
                foreach (var intensity in new[] { MentalBreakIntensity.Minor, MentalBreakIntensity.Major, MentalBreakIntensity.Extreme })
                {
                    var (pawn, need) = BreakdownPawn();
                    need.RestoreCorruption(1f, 1f);
                    var breaker = SetBreakdownLowMood(pawn, intensity);
                    Require(!breaker.TryGetRandomMentalBreak(intensity, out _));
                    Require(breaker.TryDoRandomMoodCausedMentalBreak());
                    Equal<MentalBreakIntensity?>(intensity, selectedBreakdownIntensity);
                    Require(pawn.MentalState is MentalState_SSCSlaveBreakdown state && state.causedByMood);
                    Require(!pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownFullTriggerUsed);
                }
            });

            Case("risk report locates colonists and colony slaves, excludes masters and even a zero-stage chain", () =>
            {
                var (free, freeNeed) = BreakdownPawn();
                var (slave, slaveNeed) = BreakdownPawn(PawnIdentity.Slave);
                slave.guest = new Pawn_GuestTracker(slave);
                AccessTools.Field(typeof(Pawn_GuestTracker), "guestStatusInt").SetValue(slave.guest, GuestStatus.Slave);
                AccessTools.Field(typeof(Pawn_GuestTracker), "hostFactionInt").SetValue(slave.guest, breakdownFaction);
                Require(slave.IsSlaveOfColony);
                var (master, masterNeed) = BreakdownPawn(PawnIdentity.Master);
                var (chained, chainedNeed) = BreakdownPawn(PawnIdentity.Slave);
                chained.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave
                    { pawn = chained, def = SSCDefOf.ChainOfSexSlave });
                Equal(0, SSCIdentityUtility.GetSexSlaveStage(chained));
                foreach (Need_Corruption need in new[] { freeNeed, slaveNeed, masterNeed, chainedNeed })
                    need.RestoreCorruption(.9999f, 1f);
                var alert = new Alert_SSCSlaveBreakdownRisk();
                AlertReport report = alert.GetReport();
                Require(report.active && report.culpritsPawns.Contains(free) && report.culpritsPawns.Contains(slave));
                Require(!report.culpritsPawns.Contains(master) && !report.culpritsPawns.Contains(chained));
                freeNeed.RestoreCorruption(1f, 1f);
                Require(!alert.GetReport().culpritsPawns.Contains(free));
                SetBreakdownLowMood(free, MentalBreakIntensity.Minor);
                Require(alert.GetReport().culpritsPawns.Contains(free));
                Equal(0, breakdownRolls);
                SSCMod.settings.EnableSlaveBreakdown = false;
                Require(!alert.GetReport().active);
            });

            Case("cooldown follows a hard reset, does not block imprint and rearms the spent full trigger only below 94 percent", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                SetBreakdownTick(2000);
                pawn.mindState.mentalStateHandler.Reset();
                long until = pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownUntil;
                Equal(62000L, until);
                need.SetCorruption(.93f);
                Mark(pawn, SSCImprintUtility.InitialDefName);
                need.SetCorruption(1f);
                Require(!runtime.HasFullRequest(pawn) && !SSCSlaveBreakdown.IsAtRisk(pawn));
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(pawn));

                SetBreakdownTick(62000);
                runtime.GameComponentTick();
                Require(pawn.MentalState == null);
                need.SetCorruption(.97f);
                Require(SSCSlaveBreakdown.IsAtRisk(pawn));
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                pawn.mindState.mentalStateHandler.Reset();
                SetBreakdownTick(122000);
                need.SetCorruption(.94f);
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState == null);
                need.SetCorruption(MathF.BitDecrement(.94f));
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
            });

            Case("switch clears pending triggers even during pause and lets an active episode keep its timeout", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                Require(runtime.HasFullRequest(pawn));
                SSCMod.settings.EnableSlaveBreakdown = false;
                breakdownRollSucceeds = true;
                var (other, otherNeed) = BreakdownPawn();
                otherNeed.SetCorruption(.97f);
                otherNeed.NeedInterval();
                Equal(0, breakdownRolls);
                Require(!SetBreakdownLowMood(other, MentalBreakIntensity.Minor).TryDoRandomMoodCausedMentalBreak());
                otherNeed.SetCorruption(1f);
                SSCMod.settings.EnableSlaveBreakdown = true;
                runtime.GameComponentTick();
                Require(pawn.MentalState == null && other.MentalState == null);
                need.SetCorruption(.99f);
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                SSCMod.settings.EnableSlaveBreakdown = false;
                SetBreakdownTick(15999);
                pawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                SetBreakdownTick(16000);
                pawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Require(pawn.MentalState == null);
                Equal(76000L, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownUntil);
            });

            Case("switch revokes a full crossing captured before the need write completes", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var captured = runtime.CaptureFullCrossing(pawn, need);
                Require(captured != null);
                SSCMod.settings.EnableSlaveBreakdown = false;
                SSCMod.settings.EnableSlaveBreakdown = true;
                need.CurLevel = 1f;
                runtime.QueueFullCrossing(captured);
                runtime.GameComponentTick();
                Require(pawn.MentalState == null && breakdownLetters.Count == 0);
            });

            Case("short full request resumes after a native blocker but expires or cancels when the original personality is replaced", () =>
            {
                var external = new MentalStateDef { defName = "SSC_TestExternalState", category = MentalStateCategory.Misc };
                var (ready, readyNeed) = BreakdownPawn();
                var (expired, expiredNeed) = BreakdownPawn();
                var (changed, changedNeed) = BreakdownPawn();
                foreach (Pawn pawn in new[] { ready, expired, changed })
                    Require(pawn.mindState.mentalStateHandler.TryStartMentalState(external));
                foreach (Need_Corruption need in new[] { readyNeed, expiredNeed, changedNeed }) need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(runtime.HasFullRequest(ready) && runtime.HasFullRequest(expired) && runtime.HasFullRequest(changed));
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(changed, out var mutation));
                mutation.Dispose();
                SetBreakdownTick(3499);
                ready.mindState.mentalStateHandler.Reset();
                changed.mindState.mentalStateHandler.Reset();
                readyNeed.SetCorruption(.95f);
                runtime.GameComponentTick();
                Require(ready.MentalState is MentalState_SSCSlaveBreakdown && changed.MentalState == null);
                SetBreakdownTick(3500);
                expired.mindState.mentalStateHandler.Reset();
                runtime.GameComponentTick();
                Require(expired.MentalState == null && !runtime.HasFullRequest(expired));
            });

            Case("native Scribe preserves a running breakdown, its settings and cooldown and gives missing fields safe defaults", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                SetBreakdownTick(13500);
                pawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Near(.15f, pawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loadedPawn, loadedNeed) = BreakdownPawn();
                loadedNeed.RestoreCorruption(1f, 1f);
                var restored = LoadBreakdown(path, loadedPawn);
                Require(restored.Settings.EnableSlaveBreakdown && loadedPawn.MentalState is MentalState_SSCSlaveBreakdown);
                Require(loadedPawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(SSCIdentityUtility.IsManualIdentityChangeBlocked(loadedPawn));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(loadedPawn));
                Near(10f, loadedPawn.GetStatValue(rjw.RJWStatDefOf.SexFrequency));
                Require(loadedPawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownFullTriggerUsed);
                SetBreakdownTick(16000);
                var loadedEffect = loadedPawn.health.hediffSet.GetFirstHediffOfDef(breakdownEffect);
                Require(loadedEffect.ShouldRemove);
                // At the deadline health may tick before the mental state. The sixth pulse still belongs to the episode.
                loadedPawn.health.RemoveHediff(loadedEffect);
                loadedPawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                Require(loadedPawn.MentalState == null);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(loadedPawn));
                Require(!loadedPawn.health.hediffSet.HasHediff(breakdownEffect));
                Near(.18f, loadedPawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
                restored.Settings.EnableSlaveBreakdown = false;
                path = SaveBreakdown(restored);
                var (after, afterNeed) = BreakdownPawn();
                SSCMod.settings = LoadBreakdown(path, after).Settings;
                Require(!SSCMod.settings.EnableSlaveBreakdown);
                Equal(76000L, after.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownUntil);
                var cooldown = after.health.hediffSet.GetFirstHediffOfDef(cooldownDef);
                Require(cooldown is Hediff_SSCSlaveBreakdownCooldown && !cooldown.Visible);
                Require(!cooldownDef.duplicationAllowed && !(bool)AccessTools.Method(typeof(RabbitCloneUtility),
                    "ShouldKeepSscHediff").Invoke(null, new object[] { cooldownDef }));
                Equal(1, breakdownLetters.Count);

                SSCMod.settings.EnableSlaveBreakdown = true;
                SSCMod.settings.enableCorruptionDecay = false;
                breakdownRollSucceeds = true;
                SetBreakdownTick(75999);
                afterNeed.SetCorruption(1f);
                runtime.GameComponentTick();
                afterNeed.SetCorruption(.97f);
                var breaker = SetBreakdownLowMood(after, MentalBreakIntensity.Minor);
                afterNeed.NeedInterval();
                Equal(0, breakdownRolls);
                Require(!breaker.TryDoRandomMoodCausedMentalBreak() && !SSCSlaveBreakdown.IsAtRisk(after));
                Require(after.MentalState == null);
                SetBreakdownTick(76000);
                Require(cooldown.ShouldRemove);
                runtime.GameComponentTick();
                Require(after.MentalState == null && SSCSlaveBreakdown.IsAtRisk(after));
                afterNeed.NeedInterval();
                Equal(1, breakdownRolls);
                Require(after.MentalState is MentalState_SSCSlaveBreakdown);

                Edit(path, document =>
                {
                    document.Descendants("li").Where(e => (string)e.Element("def") == cooldownDef.defName).Remove();
                    var fixture = document.Root.Element("fixture");
                    fixture.SetElementValue("sscSlaveBreakdownCooldownUntil", 76000);
                    fixture.Elements().Where(e => e.Name.LocalName is "enableSlaveBreakdown" or "sscSlaveBreakdownFullTriggerUsed").Remove();
                });
                SetBreakdownTick(75999);
                var (legacy, _) = BreakdownPawn();
                Require(!LoadBreakdown(path, legacy).Settings.EnableSlaveBreakdown);
                var legacyComp = legacy.TryGetComp<CompSexSlaveTraining>();
                Equal(76000L, legacyComp.SlaveBreakdownCooldownUntil);
                Require(!legacy.health.hediffSet.HasHediff(cooldownDef));
                legacyComp.MigrateSlaveBreakdownCooldown();
                Require(legacy.health.hediffSet.HasHediff(cooldownDef));
                Equal(76000L, legacyComp.SlaveBreakdownCooldownUntil);
                Require(!legacyComp.SlaveBreakdownFullTriggerUsed);
            });
        }
        finally
        {
            boundary.UnpatchAll(boundary.Id);
            breakdownPawns.Clear();
            breakdownLetters.Clear();
            breakdownMap = null;
            breakdownFaction = null;
            components.Remove(runtime);
            mapsField.SetValue(game, previousMaps);
            game.World = previousWorld;
            game.questManager = previousQuests;
            Current.ProgramState = previousState;
            StatDefOf.MentalBreakThreshold = previousThreshold;
            RecordDefOf.TimesInMentalState = previousRecord;
            JobDefOf.Wait = previousWait;
            frequency = previousFrequency;
            satisfaction = previousSatisfaction;
            vulnerability = previousVulnerability;
            rjw.RJWSettings.sexneed_decay_rate = previousDecay;
            DebugSettings.enableRandomMentalStates = previousRandomStates;
            SSCMod.settings = previousSettings;
            SetBreakdownTick(oldTick);
        }
    }

    private static (Pawn Pawn, Need_Corruption Need) BreakdownPawn(PawnIdentity identity = PawnIdentity.Unset)
    {
        Pawn pawn = ImprintPawn(identity);
        // 原版信件 LookTargets 由 ThingID 标识角色，定义名后不可附带临时数字。
        pawn.def.defName = "SSC_TestBreakdownRace";
        pawn.RaceProps.lifeStageAges[0].def.canDoRandomMentalBreaks = true;
        // 原生宿主只登记躯干，不模拟脑部容量。原版 Awake 仍会检查角色的真实任务。
        pawn.RaceProps.alwaysAwake = true;
        pawn.jobs = new Pawn_JobTracker(pawn);
        pawn.stances = new Pawn_StanceTracker(pawn);
        pawn.records = new Pawn_RecordsTracker(pawn);
        AccessTools.Field(typeof(Thing), "factionInt").SetValue(pawn, breakdownFaction);
        AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
        breakdownMap.mapPawns.RegisterPawn(pawn);
        breakdownPawns.Add(pawn);
        return (pawn, AddFullImprintNeed(pawn));
    }

    private static MentalBreaker SetBreakdownLowMood(Pawn pawn, MentalBreakIntensity intensity)
    {
        pawn.needs.mood = new Need_Mood(pawn) { def = new NeedDef { defName = "SSC_TestMood" }, CurLevel = 0f };
        MentalBreaker breaker = pawn.mindState.mentalBreaker;
        AccessTools.Field(typeof(MentalBreaker), "ticksBelow" + intensity).SetValue(breaker, 2150);
        return breaker;
    }

    private static void SetBreakdownTick(int tick)
        => AccessTools.Field(typeof(TickManager), "ticksGameInt").SetValue(Current.Game.tickManager, tick);

    private static bool ControlBreakdownRoll(float mtb, float mtbUnit, float ticksSinceLastCheck, ref bool __result)
    {
        if (mtb != 1.5f || mtbUnit != 60000f || ticksSinceLastCheck != 150f) return true;
        breakdownRolls++;
        __result = breakdownRollSucceeds;
        return false;
    }

    private static void ObserveMoodBreakChoice(MentalBreakWorker_SSCSlaveBreakdown __instance)
        => selectedBreakdownIntensity = __instance.def.intensity;

    private static bool BreakdownAsexualBoundary(Pawn __0, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0)) return true;
        __result = false;
        return false;
    }

    private static bool BreakdownLovingBoundary(Pawn __0, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0)) return true;
        __result = true;
        return false;
    }

    private static bool BreakdownFallFactorBoundary(Pawn __0, ref float __result)
    {
        if (!breakdownPawns.Contains(__0)) return true;
        __result = 1f;
        return false;
    }

    // 独立宿主的原版模组搜索列表未注册 RJW。走同一实际 DLL 解析 XML 中的类名。
    private static bool BreakdownRjwTypeBoundary(string typeName, ref Type __result)
    {
        if (!typeName.StartsWith("rjw.", StringComparison.Ordinal)) return true;
        __result = typeof(rjw.xxx).Assembly.GetType(typeName);
        return __result == null;
    }

    private static bool SkipBreakdownEffect(Hediff __0)
        => skipBreakdownEffectFor == null || __0 is not Hediff_SSCSlaveBreakdown || __0.pawn != skipBreakdownEffectFor;

    // 原版在写入状态后、调用 PostStart 前进入 Lord 等通知。无图形宿主在同一窗口注入一次 Reset。
    private static IEnumerable<CodeInstruction> InsertBreakdownStartInterruption(IEnumerable<CodeInstruction> instructions)
    {
        var stateSlot = AccessTools.Field(typeof(MentalStateHandler), "curStateInt");
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.StoresField(stateSlot)) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return CodeInstruction.Call(typeof(Program), nameof(InterruptBeforeBreakdownStart));
        }
    }

    private static void InterruptBeforeBreakdownStart(MentalStateHandler handler)
    {
        if (interruptBeforeBreakdownStart == null ||
            handler != interruptBeforeBreakdownStart.mindState.mentalStateHandler ||
            handler.CurState is not MentalState_SSCSlaveBreakdown) return;
        interruptBeforeBreakdownStart = null;
        handler.Reset();
    }

    // 省去与信件顺序无关的原版语法规则表；原生状态仍构造信件并实际调用 ReceiveLetter。
    private static bool BreakdownGrammarBoundary(string __0, Pawn __1, ref string __result)
    {
        if (!breakdownPawns.Contains(__1)) return true;
        __result = __0;
        return false;
    }

    private static bool BreakdownColorBoundary(TaggedString taggedStr, ref string __result)
    {
        __result = taggedStr.RawText;
        return false;
    }

    private static bool CaptureBreakdownLetter(TaggedString label, LookTargets lookTargets)
    {
        if (lookTargets.targets?.FirstOrDefault().Thing is not Pawn pawn || !breakdownPawns.Contains(pawn)) return true;
        if (label.RawText == "SSC_BreakdownFailure_Label".Translate().RawText)
        {
            breakdownFailureLetters.Add(pawn);
            return false;
        }
        string type = label.RawText == "SSC_Imprint_FullCorruptionLetterLabel".Translate().RawText ? "imprint" :
            label.RawText.StartsWith(SSCSlaveBreakdown.StateDef.LabelCap.RawText + ": ", StringComparison.Ordinal) ? "breakdown" : label.RawText;
        breakdownLetters.Add((pawn, type, SSCImprintUtility.GetKind(pawn), pawn.MentalState is MentalState_SSCSlaveBreakdown));
        if (type == "imprint" && tickDuringImprintLetter)
        {
            tickDuringImprintLetter = false;
            SSCSlaveBreakdownGameComponent.Current.GameComponentTick();
        }
        return false;
    }

    private static string SaveBreakdown(SlaveBreakdownSaveFixture fixture)
    {
        string path = Path.Combine(saveDirectory, "slave-breakdown.xml");
        Scribe.saver.InitSaving(path, "SlaveBreakdownTest");
        Scribe_Deep.Look(ref fixture, "fixture");
        Scribe.saver.FinalizeSaving();
        return path;
    }

    // 宿主没有地图预约管理器，原版任务卸载、挂起和 Job 队列仍真实运行。
    private static bool BreakdownReservationBoundary(Pawn __instance) => !breakdownPawns.Contains(__instance);

    private static bool BreakdownInvitationFogBoundary(Map map, ref bool __result)
    {
        if (map != breakdownMap) return true;
        __result = false;
        return false;
    }

    // The isolated map has no RJW appraisal services. The production request and native job tracker still run.
    private static bool BreakdownInvitationAppraisalBoundary(Pawn __0, Pawn __1, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0) || !breakdownPawns.Contains(__1)) return true;
        __result = true;
        return false;
    }

    private static bool BreakdownInvitedUseBodyBoundary(Pawn __0, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0)) return true;
        __result = true;
        return false;
    }

    private static bool BreakdownInvitedUseReachBoundary(Pawn __0, Pawn __1, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0) || !breakdownPawns.Contains(__1)) return true;
        Action callback = invitedUseReachCallback;
        invitedUseReachCallback = null;
        callback?.Invoke();
        __result = true;
        return false;
    }

    private static bool BreakdownInvitedUseHeroBoundary(Pawn __0, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0)) return true;
        __result = false;
        return false;
    }

    // 地图宿主只隔离预约和行走的安装边界。生产代码仍负责何时以及向哪个追踪器派发，终止流程走原版。
    private static bool BreakdownInvitedUseStartBoundary(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob)
    {
        if (!breakdownPawns.Contains(___pawn) || !JobDriver_SSCBreakdownInvitedUse.IsInvitedJob(newJob)) return true;
        invitedUseDispatches++;
        __instance.curJob = newJob;
        __instance.curDriver = new JobDriver_SSCBreakdownInvitedUse { pawn = ___pawn, job = newJob };
        return false;
    }

    private static bool BreakdownInvitedUseTimerBoundary(rjw.JobDriver_Sex __instance)
    {
        if (__instance is not JobDriver_SSCBreakdownInvitedUse || !breakdownPawns.Contains(__instance.pawn)) return true;
        // Model a fresh roll from setup_ticks without invoking graphics or loading unrelated RJW Defs in this host.
        (__instance.ticks_left, __instance.duration, __instance.sex_ticks, __instance.orgasmstick, __instance.orgasmStartTick,
            __instance.ticks_between_hearts, __instance.ticks_between_hits, __instance.ticks_between_thrusts) =
            (1500, 1500, 1000, 180, 1000, 70, 80, 120);
        return false;
    }

    private static void RecordInvitedUse(MentalState_SSCSlaveBreakdown state, Pawn actor)
    {
        Require(state.TryRecordInvitation(actor, SSCPersonalityRuntimeUtility.Capture(actor), true,
            out Job candidate, invitedUse: true));
        Require(candidate == null && state.HasBehaviorJob);
    }

    private static (Job Job, rjw.JobDriver_SexQuick Driver) InstallBreakdownInvitation(
        Pawn pawn, MentalState_SSCSlaveBreakdown state, Pawn partner)
    {
        Require(state.TryRecordInvitation(partner, SSCPersonalityRuntimeUtility.Capture(partner), true, out Job job));
        var driver = new rjw.JobDriver_SexQuick { pawn = pawn, job = job };
        pawn.jobs.curJob = job;
        pawn.jobs.curDriver = driver;
        return (job, driver);
    }

    private static void RunBreakdownInvitationPreparation(rjw.JobDriver_SexBaseInitiator driver, Action install,
        ToilCompleteMode mode = ToilCompleteMode.Instant)
        => SSCRestrictionJobGuard.TrackPreparation(driver,
            new[] { new Toil { initAction = install, defaultCompleteMode = mode } }).Single().initAction();

    private sealed class BreakdownCandidateNode(Job job) : ThinkNode
    {
        internal int Calls;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            Calls++;
            return job == null ? ThinkResult.NoJob : new ThinkResult(job, this, null, false);
        }
    }

    private static MentalState_SSCSlaveBreakdown StartWanderingBreakdown(Pawn pawn, Need_Corruption need)
    {
        need.SetCorruption(1f);
        SSCSlaveBreakdownGameComponent.Current.GameComponentTick();
        var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
        AccessTools.Field(typeof(MentalState_SSCSlaveBreakdown), "openingPhase").SetValue(state,
            MentalState_SSCSlaveBreakdown.OpeningPhase.Wander);
        return state;
    }

    // 宿主没有寻路网格。使用原版 LetterMaker、信堆和点击，从已找到调教员的边界驱动申请。
    private static bool BreakdownTrainerBoundary(Pawn __0, ref bool __result)
    {
        if (__0 != reachableBreakdownTrainer || __0 == null) return true;
        __result = true;
        return false;
    }

    // 这一用例比较实际恶堕入账及训练历史；无图形宿主不装入身体与社交记忆资源。
    private static bool BreakdownOpinionBoundary(Pawn sexSlave, ref float __result)
    {
        if (!breakdownPawns.Contains(sexSlave)) return true;
        __result = 0f;
        return false;
    }

    private static FieldInfo BreakdownField(string name) => AccessTools.Field(typeof(MentalState_SSCSlaveBreakdown), name);

    private static void SetBreakdownPhase(MentalState_SSCSlaveBreakdown state, string name)
    {
        var field = BreakdownField("requestPhase");
        field.SetValue(state, Enum.Parse(field.FieldType, name));
    }

    private static ChoiceLetter_SSCBreakdownTraining InstallBreakdownRequest(MentalState_SSCSlaveBreakdown state,
        Pawn pawn, Pawn trainer, LetterDef def)
    {
        var letter = (ChoiceLetter_SSCBreakdownTraining)LetterMaker.MakeLetter("test request", "test text", def,
            new LookTargets(pawn, trainer));
        letter.Subject = pawn;
        letter.Trainer = trainer;
        Find.LetterStack.LettersListForReading.Add(letter);
        BreakdownField("requestTrainer").SetValue(state, trainer);
        BreakdownField("trainerPersonality").SetValue(state, SSCPersonalityRuntimeUtility.Capture(trainer));
        BreakdownField("requestId").SetValue(state, letter.ID);
        SetBreakdownPhase(state, "Waiting");
        return letter;
    }

    private static SlaveBreakdownSaveFixture LoadBreakdown(string path, Pawn pawn, params Pawn[] references)
    {
        SlaveBreakdownSaveFixture fixture = null;
        Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        foreach (Pawn reference in references) directory.RegisterLoaded(reference);
        Scribe_Deep.Look(ref fixture, "fixture", pawn, new SSCSettings { EnableSlaveBreakdown = true });
        Scribe.loader.FinalizeLoading();
        return fixture;
    }
}

public sealed class SlaveBreakdownSaveFixture : IExposable
{
    private readonly Pawn pawn;
    public readonly SSCSettings Settings;
    public ChoiceLetter_SSCBreakdownTraining Request;

    public SlaveBreakdownSaveFixture(Pawn pawn, SSCSettings settings)
    {
        this.pawn = pawn;
        Settings = settings;
    }

    public void ExposeData()
    {
        Settings.ExposeData();
        pawn.TryGetComp<CompSexSlaveTraining>().PostExposeData();
        pawn.health.hediffSet.ExposeData();
        pawn.mindState.mentalStateHandler.ExposeData();
        Scribe_Deep.Look(ref Request, "request");
    }
}
