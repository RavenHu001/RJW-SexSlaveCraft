// AI 辅助生成：真实恶堕增长、印记和原版精神状态入口共用原生宿主。
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using TrainingDecision = SexSlaveCraft.MentalState_SSCSlaveBreakdown.TrainingDecision;

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
    private static int breakdownEnslavementAttempts;
    private static Action<Hediff_ChainOfSexSlave> onBreakdownChainAdded;

    private static void NotifyBreakdownChainAdded(Hediff_ChainOfSexSlave __instance)
        => onBreakdownChainAdded?.Invoke(__instance);

    private static bool AllowBreakdownEnslavementRoll(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool RejectVanillaBreakdownEnslavement(ref bool __result)
    {
        breakdownEnslavementAttempts++;
        __result = false;
        return false;
    }
    private static bool tickDuringImprintLetter;

    private sealed class PnaLoadRemovedCallback : Hediff
    {
        public Action AfterRemove;

        public override void PostRemoved()
        {
            base.PostRemoved();
            AfterRemove?.Invoke();
        }
    }
    private static Pawn skipBreakdownEffectFor;
    private static Action beforeBreakdownCooldownAdd;
    private static Pawn interruptBeforeBreakdownStart;
    private static int invitedUseDispatches;
    private static bool rejectInvitedUseDispatch;
    private static Action<JobDriver_SSCBreakdownInvitedUse> invitedUseOnInstalled;
    private static Action beforeInvitedUseStart;
    private static int invitationMotes;
    private static Pawn incompatibleInvitedUseActor;

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
            LetterDefOf.PositiveEvent ??= DefDatabase<LetterDef>.GetNamedSilentFail("PositiveEvent") ??
                new LetterDef { defName = "PositiveEvent", letterClass = typeof(StandardLetter) };
            if (DefDatabase<LetterDef>.GetNamedSilentFail("PositiveEvent") == null)
                DefDatabase<LetterDef>.Add(LetterDefOf.PositiveEvent);

            var xml = new XmlDocument();
            xml.Load(Path.Combine(repo, "1.7/Defs/MentalStateDefs/SSC_SlaveBreakdown.xml"));
            DefDatabase<MentalStateDef>.Add(DirectXmlToObject.ObjectFromXml<MentalStateDef>(xml.DocumentElement.SelectSingleNode("MentalStateDef"), false));
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("MentalBreakDef"))
                DefDatabase<MentalBreakDef>.Add(DirectXmlToObject.ObjectFromXml<MentalBreakDef>(node, false));
            // 用实际原版候选走完整低心情抽选，宿主只补齐该状态引用的宣泄 Def。
            if (DefDatabase<ThoughtDef>.GetNamedSilentFail("Catharsis") == null)
                DefDatabase<ThoughtDef>.Add(new ThoughtDef { defName = "Catharsis" });
            string gameRoot = Path.GetDirectoryName(typeof(Pawn).Assembly.Location);
            xml.Load(Path.Combine(gameRoot, "Data/Core/Defs/MentalStateDefs/MentalStates_Mood.xml"));
            var sadWander = DirectXmlToObject.ObjectFromXml<MentalStateDef>(
                xml.DocumentElement.SelectSingleNode("MentalStateDef[defName='Wander_Sad']"), false);
            DefDatabase<MentalStateDef>.Add(sadWander);
            DefDatabase<MentalBreakDef>.Add(DirectXmlToObject.ObjectFromXml<MentalBreakDef>(
                xml.DocumentElement.SelectSingleNode("MentalBreakDef[defName='Wander_Sad']"), false));
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
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("JobDef"))
                DefDatabase<JobDef>.Add(DirectXmlToObject.ObjectFromXml<JobDef>(node, false));
            Equal(typeof(JobDriver_SSCBreakdownInvitedUse), JobDriver_SSCBreakdownInvitedUse.Def?.driverClass);
            Equal(typeof(JobDriver_Wait), SSCBreakdownInvitation.WaitDef?.driverClass);
            Require(!JobDriver_SSCBreakdownInvitedUse.Def.allowOpportunisticPrefix &&
                !SSCBreakdownInvitation.WaitDef.allowOpportunisticPrefix);
            xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_BreakdownOutcome.xml"));
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("ThoughtDef"))
                DefDatabase<ThoughtDef>.Add(DirectXmlToObject.ObjectFromXml<ThoughtDef>(node, false));
            errors.Clear();
            DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
            Require(errors.Count == 0, string.Join('\n', errors));

            // 安装生产补丁，保留原版选池、状态赋值、Scribe 和信件构造。隔离地图网格、其 UI 更新及掷骰抽样。
            foreach (Type type in new[] { typeof(Patch_SSCSlaveBreakdownClear), typeof(Patch_SSCSlaveBreakdownTransition),
                         typeof(Patch_SSCSlaveBreakdownDeath),
                         typeof(Patch_SSCSlaveBreakdownRecoveryJobs),
                         typeof(Patch_SSCSlaveBreakdownBehaviorCleanup),
                         typeof(Patch_SSCBreakdownInvitationWaitInstallation),
                         typeof(Patch_SSCBreakdownMasturbateEverywhere), typeof(Patch_SSCBreakdownVisibility),
                         typeof(Patch_SSCBreakdownActivityLocation) })
                boundary.CreateClassProcessor(type).Patch();
            void Prefix(MethodBase method, string name) => boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), name));
            Prefix(AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.UpdateTarget)), nameof(Skip));
            Prefix(AccessTools.Method(typeof(MapPawns), "DoListChangedNotifications"), nameof(Skip));
            Prefix(AccessTools.Method(typeof(Pawn), nameof(Pawn.ClearReservationsForJob)), nameof(BreakdownReservationBoundary));
            Prefix(AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new[] { typeof(IntVec3), typeof(Map) }),
                nameof(BreakdownInvitationFogBoundary));
            Prefix(AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new[] { typeof(Thing) }),
                nameof(BreakdownInvitationThingFogBoundary));
            Prefix(AccessTools.Method(typeof(SexInteractionFinder), nameof(SexInteractionFinder.FindInteractions)),
                nameof(BreakdownInvitedUsePairBoundary));
            Prefix(AccessTools.Method(typeof(rjw.PawnDesignations_Hero), nameof(rjw.PawnDesignations_Hero.IsDesignatedHero)),
                nameof(BreakdownInvitedUseHeroBoundary));
            Prefix(AccessTools.Method(typeof(rjw.xxx), nameof(rjw.xxx.can_rape)), nameof(BreakdownInvitedUseBodyBoundary));
            Prefix(AccessTools.Method(typeof(rjw.PawnExtensions), nameof(rjw.PawnExtensions.HasParts)),
                nameof(BreakdownInvitedUseBodyBoundary));
            Prefix(AccessTools.Method(typeof(rjw.CasualSex_Helper), nameof(rjw.CasualSex_Helper.CanHaveSex)),
                nameof(BreakdownInvitedUseBodyBoundary));
            Prefix(AccessTools.Method(typeof(MoteMaker), nameof(MoteMaker.ThrowText),
                new[] { typeof(UnityEngine.Vector3), typeof(Map), typeof(string), typeof(float) }), nameof(BreakdownInvitationMoteBoundary));
            Prefix(AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos)), nameof(BreakdownDrawPosBoundary));
            Prefix(AccessTools.Method(typeof(MentalState_SSCSlaveBreakdown), nameof(MentalState_SSCSlaveBreakdown.CanReachInvitedUse)),
                nameof(BreakdownInvitedUseReachBoundary));
            Prefix(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)), nameof(BreakdownInvitedUseStartBoundary));
            Prefix(AccessTools.Method(typeof(rjw.JobDriver_Sex), nameof(rjw.JobDriver_Sex.setup_ticks)), nameof(BreakdownInvitedUseTimerBoundary));
            Prefix(AccessTools.Method(typeof(Rand), nameof(Rand.MTBEventOccurs)), nameof(ControlBreakdownRoll));
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
                nameof(BreakdownHediffAddBoundary));
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
                    tickDuringImprintLetter = false;
                    skipBreakdownEffectFor = null;
                    beforeBreakdownCooldownAdd = null;
                    interruptBeforeBreakdownStart = null;
                    invitedUseDispatches = 0;
                    rejectInvitedUseDispatch = false;
                    invitedUseOnInstalled = null;
                    incompatibleInvitedUseActor = null;
                    beforeInvitedUseStart = null;
                    invitationMotes = 0;
                    action();
                });
            }

            bool CanApprove(MentalState_SSCSlaveBreakdown state, ChoiceLetter_SSCBreakdownTraining letter)
                => state.CanApprove(letter, state.TrainingChoices().First());

            Pawn RitualOfficer()
            {
                var (master, _) = BreakdownPawn(PawnIdentity.Master);
                var (officer, _) = BreakdownPawn(PawnIdentity.Slave);
                Require(SSCBondUtility.Bind(master, officer));
                SSCBondUtility.GetChain(officer).Severity = .5f;
                officer.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
                Require(SSCIdentityUtility.SetTrainerEnabled(officer, true));
                return officer;
            }

            (Pawn Pawn, Need_Corruption Need, Hediff Progress) RitualSubject(Pawn host, float severity, float corruption)
            {
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                Hediff progress = pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                progress.Severity = severity;
                pawn.TryGetComp<CompSexSlaveTraining>().ReplaceAssignedTrainers(new[] { host });
                need.RestoreCorruption(corruption, corruption);
                return (pawn, need, progress);
            }

            Case("master ritual transfers the exact ownerless severity in both scoring paths including stage four", () =>
            {
                var (master, _) = BreakdownPawn(PawnIdentity.Master);
                foreach (var (severity, stage, corruption, oldScore) in new[] { (.8f, 3, .72f, false), (.9476543f, 4, .94f, true) })
                {
                    SSCMod.settings.useOldScoring = oldScore;
                    var (pawn, need, source) = RitualSubject(master, severity, corruption);
                    string details = ConditioningUtility.ExecuteRitualOutcome(master, pawn, .25f);
                    var chain = SSCBondUtility.GetChain(pawn);
                    Require(chain?.LinkedPawn == master && SSCBondUtility.GetBridle(master).ValidTargets.Contains(pawn));
                    Require(!pawn.health.hediffSet.hediffs.Contains(source) && Hediff_SSCOwnerlessSlave.Find(pawn) == null);
                    Equal(severity, chain.Severity);
                    Require(need.CurLevel > corruption);
                    Require(details.Contains("SSC_BreakdownSuccess_CurrentProgress".Translate(stage, severity.ToStringPercent()).ToString()));
                }
            });

            Case("trainer officer ritual uses actual corruption after gains and reports ownerless progress without binding", () =>
            {
                var officer = RitualOfficer();
                var (pawn, need, source) = RitualSubject(officer, .4f, .44f);
                string details = ConditioningUtility.ExecuteRitualOutcome(officer, pawn, 1f);
                Near(.6f, need.CurLevel);
                Equal(.5f, source.Severity);
                Require(TrainingOutcomeUtility.GetSexSlaveProgress(pawn) == source && SSCBondUtility.GetBridle(officer) == null);
                Equal(3, SSCOrgasmSensitivityUtility.GetState(pawn).EffectiveLevel);
                Require(details.Contains("SSC_Ritual_OwnerlessTrainerHeader".Translate(officer.LabelShort, pawn.LabelShort).ToString()));
                Require(details.Contains("SSC_BreakdownSuccess_StageAdvanced".Translate(2, 3).ToString()));

                var (sameStage, _, sameSource) = RitualSubject(officer, .52f, .64f);
                details = ConditioningUtility.ExecuteRitualOutcome(officer, sameStage, .25f);
                Near(.68f, sameSource.Severity);
                Require(details.Contains("SSC_BreakdownSuccess_ProgressAdvanced".Translate(3, .52f.ToStringPercent(), sameSource.Severity.ToStringPercent()).ToString()));

                var (stalled, _, stalledSource) = RitualSubject(officer, .8f, .63f);
                details = ConditioningUtility.ExecuteRitualOutcome(officer, stalled, .25f);
                Equal(.8f, stalledSource.Severity);
                Require(details.Contains("SSC_BreakdownSuccess_StageUnchanged".Translate().ToString()));
            });

            Case("ritual rejects ownerless writes after record replacement, external promotion or revocation", () =>
            {
                var officer = RitualOfficer();
                var (pawn, _, source) = RitualSubject(officer, .4f, .7f);
                var result = new CorruptionProgressionUtility.RitualChainProgress(officer, pawn, () => true);
                source.Severity = .5f;
                Require(CorruptionProgressionUtility.ProcessCorruptionProgression(officer, pawn, true, 75f, result) == null);
                Equal(.5f, source.Severity);

                result = new CorruptionProgressionUtility.RitualChainProgress(officer, pawn, () => true);
                pawn.health.RemoveHediff(source);
                var replacement = pawn.health.AddHediff(source.def);
                replacement.Severity = .5f;
                Require(CorruptionProgressionUtility.ProcessCorruptionProgression(officer, pawn, true, 75f, result) == null);
                Equal(.5f, replacement.Severity);

                result = new CorruptionProgressionUtility.RitualChainProgress(officer, pawn, () => true);
                Require(SSCIdentityUtility.SetTrainerEnabled(officer, false));
                Require(CorruptionProgressionUtility.ProcessCorruptionProgression(officer, pawn, true, 75f, result) == null);
                Equal(.5f, replacement.Severity);
                Require(SSCBondUtility.GetChain(pawn) == null);
            });

            Case("master ritual rolls back when the provisional chain callback revokes the original assignment", () =>
            {
                var (master, _) = BreakdownPawn(PawnIdentity.Master);
                var (pawn, _, source) = RitualSubject(master, .8f, .96f);
                var training = pawn.TryGetComp<CompSexSlaveTraining>();
                var hook = new Harmony("ssc.tests.ownerless-ritual.binding-assignment");
                try
                {
                    hook.Patch(AccessTools.Method(typeof(Hediff_ChainOfSexSlave), nameof(Hediff_ChainOfSexSlave.PostAdd)),
                        postfix: new HarmonyMethod(typeof(Program), nameof(NotifyBreakdownChainAdded)));
                    onBreakdownChainAdded = written =>
                    {
                        if (written.pawn != pawn) return;
                        // The provisional chain already grants owner permission, but the source has not been handed over.
                        Require(SSCBondUtility.IsBoundTo(pawn, master) && Hediff_SSCOwnerlessSlave.Find(pawn) == source);
                        training.RemoveAssignedTrainer(master);
                    };
                    var result = new CorruptionProgressionUtility.RitualChainProgress(master, pawn, () => true);
                    Require(CorruptionProgressionUtility.ProcessCorruptionProgression(master, pawn, true, 75f, result) == null);
                    Require(!TrainerAssignmentUtility.IsAssignedTrainer(training, master));
                    Require(SSCBondUtility.GetChain(pawn) == null && SSCBondUtility.GetBridle(master)?.targets.Contains(pawn) != true);
                    Require(Hediff_SSCOwnerlessSlave.Find(pawn) == source);
                    Equal(.8f, source.Severity);
                }
                finally
                {
                    onBreakdownChainAdded = null;
                    hook.UnpatchAll(hook.Id);
                }
            });

            Case("ownerless ritual reuses the enslavement switch without changing the chosen SSC result", () =>
            {
                var hook = new Harmony("ssc.tests.ownerless-ritual.enslavement");
                try
                {
                    hook.Patch(AccessTools.Method(typeof(Rand), nameof(Rand.Chance), new[] { typeof(float) }),
                        prefix: new HarmonyMethod(typeof(Program), nameof(AllowBreakdownEnslavementRoll)));
                    hook.Patch(AccessTools.Method(typeof(GenGuest), nameof(GenGuest.TryEnslavePrisoner)),
                        prefix: new HarmonyMethod(typeof(Program), nameof(RejectVanillaBreakdownEnslavement)));
                    breakdownEnslavementAttempts = 0;
                    var (master, _) = BreakdownPawn(PawnIdentity.Master);
                    var officer = RitualOfficer();
                    foreach (bool enable in new[] { false, true })
                    {
                        SSCMod.settings.enableRitualEnslavement = enable;
                        var (binding, _, _) = RitualSubject(master, .8f, .96f);
                        var (growth, _, source) = RitualSubject(officer, .4f, .6f);
                        CorruptionProgressionUtility.ProcessCorruptionProgression(master, binding, true, 75f);
                        CorruptionProgressionUtility.ProcessCorruptionProgression(officer, growth, true, 75f);
                        Equal(enable ? 2 : 0, breakdownEnslavementAttempts);
                        Equal(.8f, SSCBondUtility.GetChain(binding).Severity);
                        Equal(.5f, source.Severity);
                        Require(!binding.IsSlave && !growth.IsSlave && SSCBondUtility.GetChain(growth) == null);
                    }
                }
                finally { hook.UnpatchAll(hook.Id); }
            });

            Case("breakdown request and native letter reconnect after Scribe; approval consumes exactly that request", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                Require(state.IsPendingDecision(letter) && CanApprove(state, letter));

                var fixture = new SlaveBreakdownSaveFixture(pawn, SSCMod.settings) { Request = letter };
                string path = SaveBreakdown(fixture);
                Find.LetterStack.LettersListForReading.Clear();
                var (loadedPawn, _) = BreakdownPawn();
                loadedPawn.thingIDNumber = pawn.thingIDNumber;
                var loaded = LoadBreakdown(path, loadedPawn, trainer, loadedPawn);
                var restored = (MentalState_SSCSlaveBreakdown)loadedPawn.MentalState;
                Find.LetterStack.LettersListForReading.Add(loaded.Request);
                Require(loaded.Request != letter && loaded.Request.Subject == loadedPawn && loaded.Request.Trainer == trainer);
                Require(restored.IsPendingDecision(loaded.Request) && CanApprove(restored, loaded.Request));
                Action accept = loaded.Request.Choices.First().action;
                accept();
                accept();
                letter.Choices.First().action?.Invoke();
                Require(!restored.IsPendingDecision(loaded.Request));
                Equal("Approved", BreakdownField("requestPhase").GetValue(restored).ToString());
                Equal(0, Find.LetterStack.LettersListForReading.Count);
            });

            Case("rejection revokes stale accept and expiration publishes one failure", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                SetBreakdownLowMood(pawn, MentalBreakIntensity.Minor);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                var choices = letter.Choices.ToArray();
                choices[^2].action();
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
            });

            Case("approved training extends only when started and can claim success once", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                SetBreakdownLowMood(pawn, MentalBreakIntensity.Minor);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                state.Decide(letter, TrainingDecision.FormOwnerless);
                var job = JobMaker.MakeJob(trainingDef, pawn);
                var driver = new JobDriver_Training { pawn = trainer, job = job };
                SetBreakdownPhase(state, "Dispatched");
                BreakdownField("trainingJobId").SetValue(state, job.loadID);
                SetBreakdownTick(13500);
                Require(state.OwnsTrainingJob(trainer, job));
                var effect = pawn.health.hediffSet.hediffs.OfType<Hediff_SSCSlaveBreakdown>().Single();
                Equal(2500, effect.Timer.ticksToDisappear);
                Require(!state.ClaimTrainingSuccess(driver, 5f));
                state.MarkApprovedTrainingStarted(driver);
                Equal(5000, effect.Timer.ticksToDisappear);
                Equal(5000.ToStringTicksToPeriod(shortForm: true, canUseDecimalsShortForm: true), effect.LabelInBrackets);
                state.MarkApprovedTrainingStarted(driver);
                Equal(5000, effect.Timer.ticksToDisappear);
                var wrongJob = JobMaker.MakeJob(trainingDef, pawn);
                Require(!state.OwnsTrainingJob(trainer, wrongJob));
                Require(state.ClaimTrainingSuccess(driver, 5f) && !state.ClaimTrainingSuccess(driver, 5f));
                need.SetCorruption(.97f);
                state.FinishSuccessfulTraining();
                state.FinishSuccessfulTraining();
                Near(.2f, Hediff_SSCOwnerlessSlave.Find(pawn).Severity);
                Require(!pawn.InMentalState && !pawn.health.hediffSet.hediffs.Contains(effect) &&
                    breakdownFailureLetters.Count == 0);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                var memories = pawn.needs.mood.thoughts.memories.Memories;
                Equal(0, memories.Single(thought => thought.def.defName == "SSC_BreakdownSuccess").CurStageIndex);
                Require(memories.Count(thought => thought.def.defName == "SSC_BreakdownTrainerOpinion" &&
                    thought.otherPawn == trainer) == 1);
                Equal(1, breakdownLetters.Count(item => item.Pawn == pawn && item.Type == "success"));
            });

            Case("a bound request survives temporary owner unavailability and closes when the owner changes", () =>
            {
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                var (owner, _) = BreakdownPawn(PawnIdentity.Master);
                var (other, _) = BreakdownPawn(PawnIdentity.Master);
                Hediff_ChainOfSexSlave.AddToPawn(pawn, owner).Severity = .30f;
                need.RestoreCorruption(.95f, .95f);
                Require(SSCSlaveBreakdown.IsAtRisk(pawn));
                need.NeedInterval();
                Equal(1, breakdownRolls);
                var state = StartWanderingBreakdown(pawn, need);
                Require(state.BoundEpisode && state.BoundOwner == owner && SSCSlaveBreakdown.IsEligiblePawn(pawn));

                // An available trainer other than the linked owner has no authority to accept this request.
                reachableBreakdownTrainer = other;
                var wrong = InstallBreakdownRequest(state, pawn, other, requestDef);
                Require(!CanApprove(state, wrong));
                Find.LetterStack.RemoveLetter(wrong);

                reachableBreakdownTrainer = owner;
                var letter = InstallBreakdownRequest(state, pawn, owner, requestDef);
                Require(CanApprove(state, letter));
                Action openDialogAccept = letter.Choices.First().action;
                var busy = JobMaker.MakeJob(JobDefOf.Wait);
                busy.playerForced = true;
                owner.jobs.curJob = busy;
                state.MentalStateTick(1);
                Require(state.IsPendingDecision(letter) && !CanApprove(state, letter));
                openDialogAccept();
                Require(state.IsPendingDecision(letter) && !state.HasApprovedTraining);
                owner.jobs.curJob = null;
                Require(CanApprove(state, letter));

                Hediff_ChainOfSexSlave.AddToPawn(pawn, other);
                openDialogAccept();
                state.MentalStateTick(1);
                Require(!pawn.InMentalState && !Find.LetterStack.LettersListForReading.Contains(letter) &&
                    !state.HasApprovedTraining && state.BoundOwner == null);
                Require(SSCBondUtility.IsBoundTo(pawn, other) && SSCIdentityUtility.IsSexSlave(pawn));
            });

            Case("a restored bound episode advances only its original stage on successful training", () =>
            {
                foreach (var (initial, external, expected) in new[]
                {
                    (.50f, .50f, .80f),
                    (.70f, .70f, .90f),
                    (.70f, .90f, .90f)
                })
                {
                    var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                    var (owner, _) = BreakdownPawn(PawnIdentity.Master);
                    Hediff_ChainOfSexSlave.AddToPawn(pawn, owner).Severity = initial;
                    reachableBreakdownTrainer = owner;
                    var state = StartWanderingBreakdown(pawn, need);
                    var request = InstallBreakdownRequest(state, pawn, owner, requestDef);
                    string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings) { Request = request });
                    Find.LetterStack.LettersListForReading.Clear();
                    var (loaded, loadedNeed) = BreakdownPawn();
                    loaded.thingIDNumber = pawn.thingIDNumber;
                    var fixture = LoadBreakdown(path, loaded, loaded, owner);
                    var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                    var chain = SSCBondUtility.GetChain(loaded);
                    Require(chain != null && restored.BoundEpisode && restored.BoundOwner == owner);
                    chain.Severity = external;
                    loadedNeed.RestoreCorruption(.95f, 1f);
                    SetBreakdownLowMood(loaded, MentalBreakIntensity.Minor);
                    var memories = loaded.needs.mood.thoughts.memories;
                    memories.TryGainMemoryFast(DefDatabase<ThoughtDef>.GetNamed("SSC_BreakdownSuccess"), 0);
                    Find.LetterStack.LettersListForReading.Add(fixture.Request);
                    Require(CanApprove(restored, fixture.Request));
                    fixture.Request.Choices.First().action();

                    var job = JobMaker.MakeJob(trainingDef, loaded);
                    var driver = new JobDriver_Training { pawn = owner, job = job };
                    SetBreakdownPhase(restored, "Dispatched");
                    BreakdownField("trainingJobId").SetValue(restored, job.loadID);
                    restored.MarkApprovedTrainingStarted(driver);
                    Require(restored.ClaimTrainingSuccess(driver, 5f) && !restored.ClaimTrainingSuccess(driver, 5f));
                    restored.FinishSuccessfulTraining();
                    restored.FinishSuccessfulTraining();

                    Require(Math.Abs(chain.Severity - expected) < .0001f);
                    Require(!loaded.InMentalState && SSCBondUtility.GetChain(loaded) == chain &&
                        SSCBondUtility.IsBoundTo(loaded, owner));
                    Equal(expected < .90f, SSCSlaveBreakdown.IsEligiblePawn(loaded));
                    Equal(1, memories.Memories.Single(thought => thought.def.defName == "SSC_BreakdownSuccess").CurStageIndex);
                    Equal(1, breakdownLetters.Count(item => item.Pawn == loaded && item.Type == "success"));
                    Require(!breakdownFailureLetters.Contains(loaded));
                }
            });

            Case("ownerless approval survives Scribe and preserves exact progress through binding", () =>
            {
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                trainer.Name = new NameSingle("Binding Trainer");
                var def = DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName);
                pawn.health.AddHediff(def).Severity = .8f;
                reachableBreakdownTrainer = trainer;
                var state = StartWanderingBreakdown(pawn, need);
                var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                Require(state.TrainingChoices().SequenceEqual(new[] { TrainingDecision.BindOwnerless, TrainingDecision.AdvanceOwnerless }));
                var bindingLabel = (string)AccessTools.Field(typeof(DiaOption), "text").GetValue(letter.Choices.First());
                Require(bindingLabel.Contains(trainer.LabelShort));
                state.Decide(letter, TrainingDecision.BindOwnerless);
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, loadedNeed) = BreakdownPawn(PawnIdentity.Slave);
                loaded.thingIDNumber = pawn.thingIDNumber;
                LoadBreakdown(path, loaded, loaded, trainer);
                loadedNeed.RestoreCorruption(1f, 1f);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                var job = JobMaker.MakeJob(trainingDef, loaded);
                var driver = new JobDriver_Training { pawn = trainer, job = job };
                SetBreakdownPhase(restored, "Dispatched");
                BreakdownField("trainingJobId").SetValue(restored, job.loadID);
                restored.MarkApprovedTrainingStarted(driver);
                Require(restored.ClaimTrainingSuccess(driver, 5f));
                restored.FinishSuccessfulTraining();
                Require(SSCBondUtility.IsBoundTo(loaded, trainer) && Hediff_SSCOwnerlessSlave.Find(loaded) == null);
                Near(.8f, SSCBondUtility.GetChain(loaded).Severity);
                Equal(1, breakdownLetters.Count(item => item.Pawn == loaded && item.Type == "success"));
            });

            Case("release cooldown blocks breakdown binding and both ritual paths until the aftereffect expires", () =>
            {
                var (master, _) = BreakdownPawn(PawnIdentity.Master);
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                var (zero, zeroNeed) = BreakdownPawn(PawnIdentity.Slave);
                foreach (var (subject, severity) in new[] { (pawn, .4f), (zero, .03f) })
                {
                    Require(SSCBondUtility.Bind(master, subject));
                    SSCBondUtility.GetChain(subject).Severity = severity;
                    Require(SSCBondUtility.TryReleaseToOwnerless(new SSCBondUtility.ReleaseContext(subject)));
                    Hediff_SSCBondReleaseAftereffect.Find(subject).GetComp<HediffComp_Disappears>().ticksToDisappear = 1;
                    subject.TryGetComp<CompSexSlaveTraining>().ReplaceAssignedTrainers(new[] { master });
                }
                zeroNeed.RestoreCorruption(.8f, .8f);
                reachableBreakdownTrainer = master;
                var state = StartWanderingBreakdown(pawn, need);
                var letter = InstallBreakdownRequest(state, pawn, master, requestDef);
                Require(!state.CanApprove(letter, TrainingDecision.BindOwnerless));
                Require(state.CanApprove(letter, TrainingDecision.AdvanceOwnerless));
                foreach (var subject in new[] { pawn, zero })
                {
                    var request = SSCRestrictionTrainingUtility.CreateRequest(master, subject, true);
                    Equal(SSCTrainingFailure.BindingCooldown, SSCRestrictionTrainingUtility.Evaluate(request, false).Failure);
                    Require(CorruptionProgressionUtility.ProcessCorruptionProgression(master, subject, true, 75f) == null);
                    Require(SSCBondUtility.GetChain(subject) == null);
                }
                pawn.health.HealthTick();
                zero.health.HealthTick();
                Require(state.CanApprove(letter, TrainingDecision.BindOwnerless));
                Require(SSCRestrictionTrainingUtility.Evaluate(SSCRestrictionTrainingUtility.CreateRequest(master, pawn, true), false).Allowed);
                Require(CorruptionProgressionUtility.ProcessCorruptionProgression(master, zero, true, 75f) != null);
                Require(SSCBondUtility.IsBoundTo(zero, master) && Hediff_SSCOwnerlessSlave.Find(zero) == null);
            });

            Case("binding rollback keeps the original ownerless record when a chain callback advances it or throws", () =>
            {
                const string callbackError = "Injected chain attachment callback failure";
                var hook = new Harmony("ssc.tests.slave-breakdown.binding-rollback");
                hook.Patch(AccessTools.Method(typeof(Hediff_ChainOfSexSlave), nameof(Hediff_ChainOfSexSlave.PostAdd)),
                    postfix: new HarmonyMethod(typeof(Program), nameof(NotifyBreakdownChainAdded)));
                try
                {
                    foreach (bool throwOnAdd in new[] { false, true })
                    {
                        var (pawn, _) = BreakdownPawn(PawnIdentity.Slave);
                        var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                        var def = DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName);
                        var source = (Hediff_SSCOwnerlessSlave)pawn.health.AddHediff(def);
                        source.Severity = .8f;
                        onBreakdownChainAdded = written =>
                        {
                            // The real AddHediff has already inserted the chain, but the binding has not retired its source.
                            Require(pawn.health.hediffSet.hediffs.Contains(written) && written.LinkedPawn == trainer);
                            Near(.8f, written.Severity);
                            if (throwOnAdd) throw new InvalidOperationException(callbackError);
                            source.Severity = .9f;
                        };

                        bool threw = false;
                        try { Require(!SSCBondUtility.BindNewWithProgress(trainer, pawn, source, .8f, () => true, out _)); }
                        catch (InvalidOperationException error) when (error.Message == callbackError) { threw = true; }

                        Equal(throwOnAdd, threw);
                        Require(SSCBondUtility.GetChain(pawn) == null && SSCBondUtility.GetBridle(trainer)?.targets.Contains(pawn) != true);
                        Require(Hediff_SSCOwnerlessSlave.Find(pawn) == source);
                        Near(throwOnAdd ? .8f : .9f, source.Severity);
                    }
                }
                finally
                {
                    onBreakdownChainAdded = null;
                    hook.UnpatchAll(hook.Id);
                }
            });

            Case("first bound formation starts at twenty percent and ownerless growth uses the shared cap", () =>
            {
                var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = trainer;
                foreach (var decision in new[] { TrainingDecision.FormBound, TrainingDecision.AdvanceOwnerless })
                {
                    var (pawn, need) = BreakdownPawn();
                    var def = DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName);
                    if (decision == TrainingDecision.AdvanceOwnerless)
                    {
                        Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave));
                        pawn.health.AddHediff(def).Severity = .5f;
                    }
                    var state = StartWanderingBreakdown(pawn, need);
                    var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                    state.Decide(letter, decision);
                    var job = JobMaker.MakeJob(trainingDef, pawn);
                    var driver = new JobDriver_Training { pawn = trainer, job = job };
                    SetBreakdownPhase(state, "Dispatched");
                    BreakdownField("trainingJobId").SetValue(state, job.loadID);
                    state.MarkApprovedTrainingStarted(driver);
                    Require(state.ClaimTrainingSuccess(driver, 5f));
                    state.FinishSuccessfulTraining();
                    if (decision == TrainingDecision.FormBound)
                    {
                        Require(SSCBondUtility.IsBoundTo(pawn, trainer));
                        Near(.2f, SSCBondUtility.GetChain(pawn).Severity);
                    }
                    else
                    {
                        Require(!Utils.HasChainHediff(pawn));
                        Near(.8f, Hediff_SSCOwnerlessSlave.Find(pawn).Severity);
                    }
                }
            });

            Case("breakdown enslavement shares the setting and a failed vanilla conversion keeps formation", () =>
            {
                var hook = new Harmony("ssc.tests.slave-breakdown.enslavement");
                var ideology = AccessTools.Field(typeof(ModsConfig), "ideologyActive");
                object previousIdeology = ideology.GetValue(null);
                ideology.SetValue(null, true);
                breakdownEnslavementAttempts = 0;
                try
                {
                    hook.Patch(AccessTools.Method(typeof(Rand), nameof(Rand.Chance), new[] { typeof(float) }),
                        prefix: new HarmonyMethod(typeof(Program), nameof(AllowBreakdownEnslavementRoll)));
                    hook.Patch(AccessTools.Method(typeof(GenGuest), nameof(GenGuest.TryEnslavePrisoner)),
                        prefix: new HarmonyMethod(typeof(Program), nameof(RejectVanillaBreakdownEnslavement)));
                    var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                    reachableBreakdownTrainer = trainer;
                    Complete(TrainingDecision.FormOwnerless, enabled: false);
                    Equal(0, breakdownEnslavementAttempts);
                    Complete(TrainingDecision.FormOwnerless, enabled: true);
                    Equal(1, breakdownEnslavementAttempts);
                    Complete(TrainingDecision.AdvanceOwnerless, enabled: true);
                    Equal(1, breakdownEnslavementAttempts);

                    void Complete(TrainingDecision decision, bool enabled)
                    {
                        SSCMod.settings.enableRitualEnslavement = enabled;
                        var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                        pawn.guest = new Pawn_GuestTracker(pawn);
                        pawn.apparel = new Pawn_ApparelTracker(pawn);
                        if (decision == TrainingDecision.AdvanceOwnerless)
                            pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                        var state = StartWanderingBreakdown(pawn, need);
                        var letter = InstallBreakdownRequest(state, pawn, trainer, requestDef);
                        state.Decide(letter, decision);
                        var job = JobMaker.MakeJob(trainingDef, pawn);
                        var driver = new JobDriver_Training { pawn = trainer, job = job };
                        SetBreakdownPhase(state, "Dispatched");
                        BreakdownField("trainingJobId").SetValue(state, job.loadID);
                        state.MarkApprovedTrainingStarted(driver);
                        Require(state.ClaimTrainingSuccess(driver, 5f));
                        state.FinishSuccessfulTraining();
                        state.FinishSuccessfulTraining();
                        Require(!pawn.IsSlave && Hediff_SSCOwnerlessSlave.Find(pawn) != null);
                        Equal(1, breakdownLetters.Count(item => item.Pawn == pawn && item.Type == "success"));
                    }
                }
                finally
                {
                    hook.UnpatchAll(hook.Id);
                    ideology.SetValue(null, previousIdeology);
                }
            });

            Case("extended training deadline survives Scribe and rolls back if the restored job is lost", () =>
            {
                const int regularDeadline = 16000;
                const int extendedDeadline = 18500;
                foreach (int? interruptedAt in new int?[] { 15000, 16001, null })
                {
                    breakdownFailureLetters.Clear();
                    SetBreakdownTick(1000);
                    var (pawn, need) = BreakdownPawn();
                    var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                    reachableBreakdownTrainer = trainer;
                    var state = StartWanderingBreakdown(pawn, need);
                    InstallBreakdownRequest(state, pawn, trainer, requestDef).Choices.First().action();
                    var job = JobMaker.MakeJob(trainingDef, pawn);
                    SetBreakdownPhase(state, "Dispatched");
                    BreakdownField("trainingJobId").SetValue(state, job.loadID);
                    SetBreakdownTick(13500);
                    trainer.jobs.curJob = job;
                    trainer.jobs.curDriver = new JobDriver_Training { pawn = trainer, job = job };
                    state.MarkApprovedTrainingStarted((JobDriver_Training)trainer.jobs.curDriver);
                    SetBreakdownTick(14500);
                    string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                    if (interruptedAt == 16001)
                    {
                        // 原期限后中断的这一支从旧版延时记录恢复。
                        Edit(path, document =>
                        {
                            var record = document.Descendants("li").Single(e => (string)e.Element("def") == breakdownEffect.defName);
                            record.Elements().Where(e => e.Name.LocalName is "ticksToDisappear" or "disappearsAfterTicks" or "seed").Remove();
                            record.SetElementValue("sscBreakdownEffectExpiresAt", extendedDeadline);
                            var extended = document.Descendants("sscBreakdownTrainingExtended").Single();
                            extended.Parent.SetElementValue("sscBreakdownOriginalDeadline", regularDeadline);
                            extended.Parent.SetElementValue("sscSlaveBreakdownNextBrokenAt", 3500);
                            extended.Remove();
                        });
                    }

                    var (loadedPawn, _) = BreakdownPawn();
                    LoadBreakdown(path, loadedPawn, trainer);
                    var restored = (MentalState_SSCSlaveBreakdown)loadedPawn.MentalState;
                    var effect = (Hediff_SSCSlaveBreakdown)loadedPawn.health.hediffSet.GetFirstHediffOfDef(breakdownEffect);
                    // The isolated fixture restores the pawn and state. Reattach the trainer's original job as vanilla would.
                    job.targetA = loadedPawn;
                    Equal(4000, effect.Timer.ticksToDisappear);
                    Equal(4000.ToStringTicksToPeriod(shortForm: true, canUseDecimalsShortForm: true), effect.LabelInBrackets);
                    restored.MentalStateTick(1);
                    Require(restored.OwnsTrainingJob(trainer, job) && loadedPawn.MentalState == restored);
                    Equal(4000, effect.Timer.ticksToDisappear);

                    if (interruptedAt is int interruptionTick)
                    {
                        SetBreakdownTick(interruptionTick);
                        trainer.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                        restored.MentalStateTick(1);
                        Equal(regularDeadline - interruptionTick, effect.Timer.ticksToDisappear);
                        if (interruptionTick < regularDeadline)
                        {
                            Require(loadedPawn.MentalState == restored && !restored.HasApprovedTraining);
                            Equal(1000.ToStringSecondsFromTicks("F0"), effect.LabelInBrackets);
                            SetBreakdownTick(regularDeadline);
                            restored.MentalStateTick(1);
                        }
                    }
                    else
                    {
                        SetBreakdownTick(regularDeadline);
                        restored.MentalStateTick(1);
                        Require(restored.OwnsTrainingJob(trainer, job));
                        // Expiry should release the approved training without stopping unrelated work.
                        Job otherWork = JobMaker.MakeJob(JobDefOf.Wait, 200);
                        Job queuedWork = JobMaker.MakeJob(JobDefOf.Wait, 300);
                        loadedPawn.jobs.curJob = otherWork;
                        loadedPawn.jobs.curDriver = new JobDriver_Wait { pawn = loadedPawn, job = otherWork };
                        loadedPawn.jobs.jobQueue.EnqueueLast(queuedWork);
                        SetBreakdownTick(extendedDeadline);
                        restored.MentalStateTick(1);
                        Require(trainer.CurJob == null);
                        Equal(otherWork, loadedPawn.CurJob);
                        Equal(queuedWork, loadedPawn.jobs.jobQueue.Single().job);
                    }
                    Require(loadedPawn.MentalState == null && !loadedPawn.health.hediffSet.HasHediff(breakdownEffect));
                    Require(breakdownFailureLetters.SequenceEqual(new[] { loadedPawn }));
                    Near(interruptedAt.HasValue ? .18f : .21f, loadedPawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
                }
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

            Case("ordinary training scores in both modes are independent of binding", () =>
            {
                SkillDef previousSocial = SkillDefOf.Social;
                try
                {
                    SkillDefOf.Social = DefDatabase<SkillDef>.GetNamedSilentFail("Social") ?? new SkillDef { defName = "Social" };
                    if (DefDatabase<SkillDef>.GetNamedSilentFail("Social") == null) DefDatabase<SkillDef>.Add(SkillDefOf.Social);
                    foreach (bool legacy in new[] { false, true })
                    {
                        SSCMod.settings.useOldScoring = legacy;
                        var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                        trainer.skills = new Pawn_SkillTracker(trainer);
                        trainer.skills.GetSkill(SkillDefOf.Social).Level = 15;
                        var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                        need.SetCorruption(.30f);
                        var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                        comp.lastTrainingScore = 12f;
                        ConditioningUtility.ExecuteOutcome(trainer, pawn, 5f);
                        Equal(legacy ? 12f : 5f, comp.lastTrainingScore);
                        Require(!Utils.HasChainHediff(pawn) && SSCBondUtility.GetBoundMaster(pawn) == null);

                        var (bound, boundNeed) = BreakdownPawn(PawnIdentity.Slave);
                        var chain = Hediff_ChainOfSexSlave.AddToPawn(bound, trainer);
                        chain.Severity = .30f;
                        boundNeed.SetCorruption(.30f);
                        ConditioningUtility.ExecuteOutcome(trainer, bound, 5f);
                        var (daily, dailyNeed) = BreakdownPawn(PawnIdentity.Slave);
                        Hediff_ChainOfSexSlave.AddToPawn(daily, trainer).Severity = .30f;
                        dailyNeed.SetCorruption(.30f);
                        ConditioningUtility.ExecuteOutcome(trainer, daily, 5f);
                        Equal(dailyNeed.CurLevel, boundNeed.CurLevel);
                        Equal(daily.TryGetComp<CompSexSlaveTraining>().lastTrainingScore,
                            bound.TryGetComp<CompSexSlaveTraining>().lastTrainingScore);
                        Near(boundNeed.CurLevel, need.CurLevel);
                        Require(SSCBondUtility.GetChain(bound) == chain);
                        Equal(.30f, chain.Severity);
                    }
                }
                finally { SkillDefOf.Social = previousSocial; }
            });

            Case("daily training uses assignment and pays normally without binding zero-stage or ownerless targets", () =>
            {
                SkillDef previousSocial = SkillDefOf.Social;
                try
                {
                    SkillDefOf.Social = DefDatabase<SkillDef>.GetNamedSilentFail("Social") ?? new SkillDef { defName = "Social" };
                    if (DefDatabase<SkillDef>.GetNamedSilentFail("Social") == null) DefDatabase<SkillDef>.Add(SkillDefOf.Social);
                    var (trainer, _) = BreakdownPawn(PawnIdentity.Master);
                    trainer.skills.GetSkill(SkillDefOf.Social).Level = 15;

                    foreach (bool legacy in new[] { false, true })
                    foreach (bool ownerless in new[] { false, true })
                    {
                        SSCMod.settings.useOldScoring = legacy;
                        var (target, need) = BreakdownPawn(PawnIdentity.Slave);
                        Hediff record = null;
                        if (ownerless)
                        {
                            record = target.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                            record.Severity = .8f;
                            need.SetCorruption(.1f);
                            need.NeedInterval();
                            Equal(.5f, need.CurLevel);
                        }
                        need.SetCorruption(.5f);
                        var request = SSCRestrictionTrainingUtility.CreateRequest(trainer, target, false);
                        Equal(SSCInteractionKind.DailyTraining, request.Kind);
                        Require(!SSCRestrictionTrainingUtility.Evaluate(request, true).Allowed);
                        target.TryGetComp<CompSexSlaveTraining>().ReplaceAssignedTrainers(new[] { trainer });
                        Require(SSCRestrictionTrainingUtility.Evaluate(request, true).Allowed);
                        Equal(SSCInteractionKind.BindingPreparation,
                            SSCRestrictionTrainingUtility.CreateRequest(trainer, target, true).Kind);

                        ConditioningUtility.ExecuteOutcome(trainer, target, 50f);
                        Require(need.CurLevel > .5f);
                        Require(SSCBondUtility.GetChain(target) == null, "daily outcome created a binding");

                        // 旧调用者可直达公开进度入口。此入口同样只处理仪式的进度请求。
                        ConditioningUtility.ProcessCorruptionProgression(trainer, target, false, 50f);
                        Equal(record, TrainingOutcomeUtility.GetSexSlaveProgress(target));
                        Equal(ownerless ? 3 : 0, SSCIdentityUtility.GetSexSlaveStage(target));
                        Require(SSCBondUtility.GetChain(target) == null && SSCBondUtility.GetBridle(trainer) == null);
                        if (record != null) Equal(.8f, record.Severity);
                    }
                }
                finally { SkillDefOf.Social = previousSocial; }
            });

            Case("ownerless stage factors flow through native stats and the installed RJW getter", () =>
            {
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                var record = pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                // 关闭档通常可提交。跨过一阶阈值后，原上下文和重新捕获的上下文都不能手动提交。
                record.Severity = MathF.BitDecrement(.1f);
                var sensitivity = SSCOrgasmSensitivityUtility.Capture(pawn);
                Require(SSCOrgasmSensitivityUtility.CanSet(sensitivity, 0, out _));
                record.Severity = .1f;
                Require(!SSCOrgasmSensitivityUtility.TrySet(sensitivity, 0, out _));
                Require(!SSCOrgasmSensitivityUtility.TrySet(SSCOrgasmSensitivityUtility.Capture(pawn), 0, out _));
                record.Severity = .5f;
                Near(3f, rjw.xxx.get_vulnerability(pawn));
                Require(SSCSlaveBreakdown.IsEligiblePawn(pawn));

                record.Severity = .9f;
                var stat = rjw.RJWStatDefOf.Vulnerability;
                Near(18f, stat.Worker.GetValue(StatRequest.For(pawn), applyPostProcess: false));
                var panel = pawn.GetStatValue(stat);
                Near(3f, panel);
                Near(panel, rjw.xxx.get_vulnerability(pawn));
                Require(!SSCSlaveBreakdown.IsEligiblePawn(pawn));

                record.Severity = .5f;
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                Require(stat.Worker.GetValue(StatRequest.For(pawn), applyPostProcess: false) > 3f);
                Near(3f, rjw.xxx.get_vulnerability(pawn));
            });

            Case("ownerless PNA immunity clears dependency while preserving ordinary full load", () =>
            {
                var (pawn, _) = BreakdownPawn(PawnIdentity.Slave);
                var previousLoad = SSCDefOf.SSC_PnaLoad;
                var previousAddiction = SSCDefOf.SSC_PnaAddiction;
                try
                {
                    SSCDefOf.SSC_PnaLoad = new HediffDef
                    {
                        defName = SSCPnaExposureUtility.LoadDefName, hediffClass = typeof(Hediff),
                        maxSeverity = SSCPnaMath.MaximumSeverity
                    };
                    SSCDefOf.SSC_PnaAddiction = ImprintBoundaryDef(SSCPnaExposureUtility.AddictionDefName, typeof(Hediff));
                    pawn.health.AddHediff(SSCDefOf.SSC_PnaAddiction);
                    Require(SSCPnaExposureUtility.GetAddiction(pawn) != null);
                    pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                    Require(SSCPnaExposureUtility.GetAddiction(pawn) == null);
                    var result = SSCPnaExposureUtility.ApplyExposure(pawn, 2f, SSCPnaExposureSource.Debug);
                    Require(result.Accepted && result.IsFull && !result.Rooted && !result.AddictionCreated,
                        $"accepted={result.Accepted}, full={result.IsFull}, rooted={result.Rooted}, addiction={result.AddictionCreated}");
                    Equal(SSCDefOf.SSC_PnaLoad, SSCPnaExposureUtility.GetBodyRecord(pawn).def);
                    Require(SSCPnaExposureUtility.GetAddiction(pawn) == null);

                    // 清掉低于有效阈值的负荷时，健康回调可能将零阶推过免瘾阈值。
                    // 回调中的 PNA 刷新是重入，由外层完成清理，此时已经没有负荷可供下一个周期刷新。
                    var (callbackPawn, _) = BreakdownPawn(PawnIdentity.Slave);
                    var ownerless = HediffMaker.MakeHediff(
                        DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName), callbackPawn);
                    ownerless.Severity = MathF.BitDecrement(.1f);
                    callbackPawn.health.AddHediff(ownerless);
                    callbackPawn.health.AddHediff(SSCDefOf.SSC_PnaAddiction);
                    var load = new PnaLoadRemovedCallback
                    {
                        def = SSCDefOf.SSC_PnaLoad,
                        pawn = callbackPawn,
                        Severity = SSCPnaMath.RemovalSeverity,
                        AfterRemove = () => ownerless.Severity = .1f
                    };
                    // 布置已衰减的负荷，避免添加补丁在调用外层刷新前先清掉它。
                    callbackPawn.health.hediffSet.hediffs.Add(load);
                    SSCPnaExposureUtility.RefreshBodyState(callbackPawn);
                    Require(SSCPnaExposureUtility.GetBodyRecord(callbackPawn) == null);
                    Require(SSCPnaExposureUtility.HasAddictionImmunity(callbackPawn));
                    Require(SSCPnaExposureUtility.GetAddiction(callbackPawn) == null,
                        "PNA refresh must complete immunity cleanup after the load removal callback.");
                }
                finally
                {
                    SSCDefOf.SSC_PnaLoad = previousLoad;
                    SSCDefOf.SSC_PnaAddiction = previousAddiction;
                }
            });

            Case("ownerless targets can appoint a trainer officer and loss of binding disables the officer's record", () =>
            {
                var (master, _) = BreakdownPawn(PawnIdentity.Master);
                var (officer, _) = BreakdownPawn(PawnIdentity.Slave);
                Require(SSCBondUtility.Bind(master, officer));
                SSCBondUtility.GetChain(officer).Severity = .5f;
                officer.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
                Require(SSCIdentityUtility.SetTrainerEnabled(officer, true));

                var (target, _) = BreakdownPawn(PawnIdentity.Slave);
                var def = DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName);
                target.health.AddHediff(def);
                var comp = target.TryGetComp<CompSexSlaveTraining>();
                var saved = new SSCRestrictionConfig();
                comp.restrictionConfig = saved;
                Require(SSCRestrictionTrainerAssignment.CanAdd(target, officer, out bool opensNonOwnerTraining));
                Require(!opensNonOwnerTraining);
                Require(SSCRestrictionTrainerAssignment.TryAdd(target, officer, () => true));
                var request = SSCRestrictionTrainingUtility.CreateRequest(officer, target, false);
                Require(SSCRestrictionTrainingUtility.Evaluate(request, true).Allowed);
                Equal(saved, comp.restrictionConfig);

                officer.health.AddHediff(def).Severity = .5f;
                Require(SSCBondUtility.Unbind(officer));
                Require(!SSCIdentityUtility.IsTrainer(officer));
                Require(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(officer, out _));
                Require(officer.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled));
                Require(!SSCRestrictionTrainingUtility.Evaluate(request, true).Allowed);
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
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown { causedByMood: false });
                Require(!runtime.HasFullRequest(pawn) && !SSCSlaveBreakdown.IsAtRisk(pawn));
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
                Require(!opening.TryIssueJobPackage(pawn, default).IsValid);
                SetBreakdownTick(1001);
                Equal(wanderJob, branch.TryIssueJobPackage(pawn, default).Job);
                Require(!state.HasBehaviorJob);
                Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, state.Phase);
                Equal(1, solo.Calls);
            });

            Case("an opening with no solo candidate is consumed immediately", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var state = (MentalState_SSCSlaveBreakdown)pawn.MentalState;
                var solo = new BreakdownCandidateNode(null);
                var opening = new ThinkNode_SSCSlaveBreakdownOpening();
                AccessTools.Field(typeof(ThinkNode), "subNodes").SetValue(opening, new List<ThinkNode> { solo });
                Require(!opening.TryIssueJobPackage(pawn, default).IsValid);
                Equal(MentalState_SSCSlaveBreakdown.OpeningPhase.Wander, state.Phase);
                Require(!opening.TryIssueJobPackage(pawn, default).IsValid);
                Equal(1, solo.Calls);
            });

            Case("the dev bind gizmo transfers precise ownerless progress and honors the release cooldown", () =>
            {
                var master = ImprintPawn(PawnIdentity.Master);
                var pawn = ImprintPawn(PawnIdentity.Slave);
                var faction = AccessTools.Field(typeof(Thing), "factionInt");
                faction.SetValue(master, breakdownFaction);
                faction.SetValue(pawn, breakdownFaction);
                master.Name = new NameSingle("Debug Master");
                pawn.Name = new NameSingle("Debug Subject");
                var source = pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                const float progress = .8123457f;
                source.Severity = progress;
                var prefs = (PrefsData)AccessTools.Field(typeof(Prefs), "data").GetValue(null);
                bool previousDevMode = prefs.devMode;
                var selection = AccessTools.Field(typeof(CompSexSlaveTraining), "debugSelectedMaster");
                object previousMaster = selection.GetValue(null);
                prefs.devMode = true;
                SSCMod.settings.enableDebugGizmos = true;
                try
                {
                    selection.SetValue(null, master);
                    var bind = pawn.TryGetComp<CompSexSlaveTraining>().CompGetGizmosExtra()
                        .OfType<Command_Action>().First(command => command.defaultLabel == "DEV: Bind To Master");
                    bind.action();
                    var chain = SSCBondUtility.GetChain(pawn);
                    Require(chain?.LinkedPawn == master && Hediff_SSCOwnerlessSlave.Find(pawn) == null);
                    Equal(progress, chain.Severity);

                    Require(SSCBondUtility.TryReleaseToOwnerless(new SSCBondUtility.ReleaseContext(pawn)));
                    Equal(progress, Hediff_SSCOwnerlessSlave.Find(pawn).Severity);
                    bind.action();
                    Require(SSCBondUtility.GetChain(pawn) == null);
                }
                finally
                {
                    prefs.devMode = previousDevMode;
                    selection.SetValue(null, previousMaster);
                }
            });

            Case("debug start enters the real opening without changing corruption and keeps public activity scoped", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var (partner, _) = BreakdownPawn();
                var (bystander, _) = BreakdownPawn();
                var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                comp.RecordSlaveBreakdownCooldown();
                ((Hediff_SSCSlaveBreakdownCooldown)pawn.health.hediffSet.GetFirstHediffOfDef(cooldownDef))
                    .Timer.ticksToDisappear = 4000;
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

            Case("invitation deadlines distinguish the shared wait from a refusal or an accepted target", () =>
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

                position.SetValue(second, new IntVec3(1, 0, 0));
                SetBreakdownTick(2399);
                Require(!state.TryBeginInvitationScan());
                SetBreakdownTick(2400);
                Require(state.TryBeginInvitationScan() && !state.CanInvite(first) && state.CanInvite(second));
                SetBreakdownTick(3649);
                Require(!state.CanInvite(first));
                SetBreakdownTick(3650);
                Require(state.CanInvite(first));

                pawn.jobs.StartJob(RecordInvitedUse(state, first));
                Require(first.jobs.curDriver is JobDriver_SSCBreakdownInvitedUse);
                first.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Require(!state.HasBehaviorJob && !state.CanInvite(first));
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                var (loaded, _) = BreakdownPawn();
                position.SetValue(loaded, IntVec3.Zero);
                LoadBreakdown(path, loaded, first);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;

                SetBreakdownTick(4899);
                Require(!restored.TryBeginInvitationScan());
                SetBreakdownTick(4900);
                Require(restored.TryBeginInvitationScan() && !restored.CanInvite(first) && restored.CanInvite(second));
                SetBreakdownTick(6149);
                Require(!restored.CanInvite(first));
                SetBreakdownTick(6150);
                Require(restored.CanInvite(first));
            });

            Case("training approval cancels preparation but waits for an invited use that actually started", () =>
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
                    pawn.jobs.StartJob(RecordInvitedUse(state, partner));
                    var driver = (JobDriver_SSCBreakdownInvitedUse)partner.jobs.curDriver;
                    Job job = driver.job;
                    var invitation = state.InvitationFor(job);
                    Job receive = JobMaker.MakeJob(rjw.xxx.gettin_raped, partner);
                    JobCondition? receiverOutcome = null;
                    RunBreakdownInvitationPreparation(driver, () =>
                    {
                        pawn.jobs.curJob = receive;
                        var receiving = new rjw.JobDriver_SexBaseRecieverRaped { pawn = pawn, job = receive };
                        receiving.AddFinishAction(condition => receiverOutcome = condition);
                        pawn.jobs.curDriver = receiving;
                    });
                    Require(invitation.HasReceiver(pawn));
                    if (started) Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));

                    state.Decide(letter, TrainingDecision.FormOwnerless);
                    Require(state.HasApprovedTraining && !state.TryBeginInvitationScan());
                    if (started)
                    {
                        Require(state.InvitationFor(partner.CurJob) == invitation && pawn.CurJob == receive);
                        Require(!state.CanReceiveBreakdownTraining);
                        partner.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
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
                    pawn.jobs.StartJob(RecordInvitedUse(state, partner));
                    var driver = (JobDriver_SSCBreakdownInvitedUse)partner.jobs.curDriver;

                    // The native receiver installation can fail while the partner's original work is still current.
                    if (directStart)
                        Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool allowed) && !allowed);
                    else
                    {
                        bool ranNativeScene = false;
                        RunBreakdownInvitationPreparation(driver, () => ranNativeScene = true, ToilCompleteMode.Never);
                        Require(!ranNativeScene);
                    }
                    Require(!state.HasBehaviorJob && pawn.CurJob == null && partner.CurJob == null);
                }
            });

            Case("same-version loading cannot reauthorize an invitation after the actor's personality changes", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                subject.jobs.StartJob(RecordInvitedUse(state, actor));
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(actor, out var mutation));
                mutation.Dispose();

                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(subject, SSCMod.settings));
                LoadBreakdown(path, subject, actor, subject);
                var restored = (MentalState_SSCSlaveBreakdown)subject.MentalState;
                SetBreakdownTick(1001);
                restored.MentalStateTick(1);
                Require(subject.CurJob == null && actor.CurJob == null && !restored.HasBehaviorJob);
            });

            Case("invited use offers a compatible same-sex pairing independently of hookup attraction", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                subject.gender = Gender.Female;
                actor.gender = Gender.Female;
                subject.relations = new Pawn_RelationsTracker(subject);
                actor.relations = new Pawn_RelationsTracker(actor);
                var state = StartWanderingBreakdown(subject, need);
                incompatibleInvitedUseActor = actor;

                Require(state.CanInvite(actor));
                object personality = SSCPersonalityRuntimeUtility.Capture(actor);
                Require(!state.TryRecordInvitation(actor, personality, true, out Job incompatibleAcceptance));
                Require(!state.TryRecordInvitation(actor, personality, false, out Job incompatibleRefusal));
                Require(incompatibleAcceptance == null && incompatibleRefusal == null && !state.HasBehaviorJob &&
                    invitedUseDispatches == 0);

                incompatibleInvitedUseActor = null;
                MapPawns oldMapPawns = breakdownMap.mapPawns;
                bool oldEnabled = rjw.RJWSettings.rape_enabled;
                bool oldHippieMode = rjw.RJWSettings.HippieMode;
                int oldOpinion = rjw.RJWHookupSettings.MinimumRelationshipToHookup;
                float oldRomance = rjw.RJWHookupSettings.MinimumAttractivenessToHookup;
                DutyDef oldTravelDuty = DutyDefOf.TravelOrLeave;
                ThinkResult offer;
                try
                {
                    // 候选节点会查询地图名单，隔离其他用例留下的角色。
                    breakdownMap.mapPawns = new MapPawns(breakdownMap);
                    breakdownMap.mapPawns.RegisterPawn(subject);
                    breakdownMap.mapPawns.RegisterPawn(actor);
                    rjw.RJWSettings.rape_enabled = true;
                    rjw.RJWSettings.HippieMode = true;
                    rjw.RJWHookupSettings.MinimumRelationshipToHookup = -100;
                    rjw.RJWHookupSettings.MinimumAttractivenessToHookup = float.MaxValue;
                    DutyDefOf.TravelOrLeave ??= new DutyDef { defName = "TravelOrLeave" };
                    offer = new ThinkNode_SSCSlaveBreakdownInvitation().TryIssueJobPackage(subject, default);
                }
                finally
                {
                    breakdownMap.mapPawns = oldMapPawns;
                    rjw.RJWSettings.rape_enabled = oldEnabled;
                    rjw.RJWSettings.HippieMode = oldHippieMode;
                    rjw.RJWHookupSettings.MinimumRelationshipToHookup = oldOpinion;
                    rjw.RJWHookupSettings.MinimumAttractivenessToHookup = oldRomance;
                    DutyDefOf.TravelOrLeave = oldTravelDuty;
                }
                Require(SSCBreakdownInvitation.IsWait(offer.Job));
                Equal(0, invitationMotes);
                subject.jobs.StartJob(offer.Job);
                Equal(1, invitedUseDispatches);
                Equal(1, invitationMotes);
                var driver = (JobDriver_SSCBreakdownInvitedUse)actor.jobs.curDriver;
                Require(driver.Partner == subject && SSCBreakdownInvitation.IsWait(subject.CurJob));
                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, actor);
                RunBreakdownInvitationPreparation(driver, () =>
                {
                    subject.jobs.curJob = receiver;
                    subject.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = subject, job = receiver };
                });
                Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool ready) && ready);
                incompatibleInvitedUseActor = actor;
                Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool allowed) && !allowed);
                Require(!state.HasBehaviorJob && subject.CurJob == null && actor.CurJob == null);
            });

            Case("accepted invited use dispatches on wait installation and resumes the same pair after load", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn(PawnIdentity.Master);
                reachableBreakdownTrainer = actor;
                var state = StartWanderingBreakdown(subject, need);
                var requestLetter = InstallBreakdownRequest(state, subject, actor, requestDef);
                Job wait = RecordInvitedUse(state, actor);
                subject.jobs.StartJob(wait);
                Equal(1, invitedUseDispatches);
                var behavior = new ThinkNode_SSCSlaveBreakdownBehavior();
                Equal(wait, behavior.TryIssueJobPackage(subject, default).Job);
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(subject, SSCMod.settings) { Request = requestLetter });
                Find.LetterStack.LettersListForReading.Clear();
                Pawn loaded = subject;
                var fixture = LoadBreakdown(path, loaded, actor, loaded);
                var restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                Find.LetterStack.LettersListForReading.Add(fixture.Request);

                restored.MentalStateTick(1);
                restored.MentalStateTick(1);
                Equal(1, invitedUseDispatches);
                Require(actor.jobs.curDriver is JobDriver_SSCBreakdownInvitedUse && loaded.CurJob == wait);
                Equal(wait, behavior.TryIssueJobPackage(loaded, default).Job);
                var driver = (JobDriver_SSCBreakdownInvitedUse)actor.jobs.curDriver;
                Require(SSCRestrictionJobContext.TryCreate(driver, out var request) &&
                    request.DirectionKnown && request.Kind == SSCInteractionKind.Forced &&
                    request.Initiator == actor && request.Receiver == loaded);
                Require(SSCBreakdownPublicActivity.Permits(actor, loaded) && !SSCBreakdownPublicActivity.Permits(loaded, actor));

                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, actor);
                RunBreakdownInvitationPreparation(driver, () =>
                {
                    loaded.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                    loaded.jobs.curJob = receiver;
                    loaded.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = loaded, job = receiver };
                });
                Require(SSCRestrictionJobGuard.TryCheck(driver, "Start", out bool allowed) && allowed);
                Require(SSCRestrictionJobGuard.TryMarkStarted(driver, true));
                Equal(receiver, new ThinkNode_SSCSlaveBreakdownBehavior().TryIssueJobPackage(loaded, default).Job);
                Require(CanApprove(restored, fixture.Request));

                path = SaveBreakdown(fixture);
                Find.LetterStack.LettersListForReading.Clear();
                fixture = LoadBreakdown(path, loaded, actor, loaded);
                Find.LetterStack.LettersListForReading.Add(fixture.Request);
                restored = (MentalState_SSCSlaveBreakdown)loaded.MentalState;
                restored.MentalStateTick(1);
                Equal(1, invitedUseDispatches);
                Require(restored.InvitedUseReceiverFor(receiver) != null && actor.jobs.curDriver == driver);
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
                Require(!restored.HasBehaviorJob && loaded.CurJob == null && actor.CurJob == null);
            });

            Case("failed second dispatch releases the first wait without replacing unrelated work", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                Job work = JobMaker.MakeJob(JobDefOf.Wait, 300);
                actor.jobs.curJob = work;
                actor.jobs.curDriver = new JobDriver_Wait { pawn = actor, job = work };
                var state = StartWanderingBreakdown(subject, need);
                rejectInvitedUseDispatch = true;
                subject.jobs.StartJob(RecordInvitedUse(state, actor));
                Equal(1, invitedUseDispatches);
                Equal(0, invitationMotes);
                Require(subject.CurJob == null && subject.jobs.jobQueue.Count == 0 &&
                    actor.CurJob == work && !state.HasBehaviorJob);

                // An integration StartJob hook can throw after the wait has already been installed.
                SetBreakdownTick(2250);
                var error = new InvalidOperationException("fixture StartJob failure");
                beforeInvitedUseStart = () => throw error;
                bool propagated = false;
                try { subject.jobs.StartJob(RecordInvitedUse(state, actor)); }
                catch (InvalidOperationException caught) when (caught == error) { propagated = true; }
                Require(propagated && subject.CurJob == null && subject.jobs.jobQueue.Count == 0 &&
                    actor.CurJob == work && !state.HasBehaviorJob);
                Equal(1, invitedUseDispatches);
                Equal(0, invitationMotes);
            });

            Case("immediate dispatch replaces forced work and can install the receiver synchronously without clearing queues", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                Job actorQueued = JobMaker.MakeJob(JobDefOf.Wait, 200);
                Job subjectQueued = JobMaker.MakeJob(JobDefOf.Wait, 300);
                actor.jobs.jobQueue.EnqueueLast(actorQueued);
                subject.jobs.jobQueue.EnqueueLast(subjectQueued);
                Job firstJob = JobMaker.MakeJob(JobDefOf.Wait, 50);
                firstJob.playerForced = true;
                actor.jobs.curJob = firstJob;
                actor.jobs.curDriver = new JobDriver_Wait { pawn = actor, job = firstJob };
                Job wait = RecordInvitedUse(state, actor);
                actor.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Job otherJob = JobMaker.MakeJob(JobDefOf.Wait, 100);
                otherJob.playerForced = true;
                actor.jobs.curJob = otherJob;
                actor.jobs.curDriver = new JobDriver_Wait { pawn = actor, job = otherJob };
                Job receiver = JobMaker.MakeJob(rjw.xxx.gettin_raped, actor);
                invitedUseOnInstalled = driver => RunBreakdownInvitationPreparation(driver, () =>
                {
                    Equal(wait, subject.CurJob);
                    Require(actor.CurJob == driver.job && actor.CurJob != otherJob);
                    subject.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                    subject.jobs.curJob = receiver;
                    subject.jobs.curDriver = new rjw.JobDriver_SexBaseRecieverRaped { pawn = subject, job = receiver };
                });

                subject.jobs.StartJob(wait);
                Equal(1, invitedUseDispatches);
                Equal(1, invitationMotes);
                Require(state.InvitedUseReceiverFor(receiver) != null && actor.jobs.curDriver is JobDriver_SSCBreakdownInvitedUse);
                subject.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Require(!state.HasBehaviorJob && subject.CurJob == null && actor.CurJob == null);
                Equal(actorQueued, actor.jobs.jobQueue.Single().job);
                Equal(subjectQueued, subject.jobs.jobQueue.Single().job);
            });

            Case("a started invited use cannot settle after its exact receiver is lost", () =>
            {
                var (subject, need) = BreakdownPawn();
                var (actor, _) = BreakdownPawn();
                var state = StartWanderingBreakdown(subject, need);
                subject.jobs.StartJob(RecordInvitedUse(state, actor));
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
                subject.jobs.StartJob(RecordInvitedUse(state, actor));
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

                state.Decide(letter, TrainingDecision.FormOwnerless);
                Require(state.HasApprovedTraining && !state.HasBehaviorJob && state.CanReceiveBreakdownTraining);
                Require(actor.CurJob == null && actor.jobs.jobQueue.Count == 0 && subject.CurJob == null);
                Equal(0, invitedUseDispatches);
                SetBreakdownTick(2250);
                Require(!state.TryBeginInvitationScan());
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

            Case("early removal of the owned hediff stops dispatch and settles through the interruption tick once", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var state = StartWanderingBreakdown(pawn, need);
                var effect = pawn.health.hediffSet.GetFirstHediffOfDef(breakdownEffect);
                SetBreakdownTick(3500);
                state.MentalStateTick(1);
                SetBreakdownTick(5999);
                pawn.health.RemoveHediff(effect);
                Require(!state.CanIssueBehavior && !state.TryBeginInvitationScan());
                // Save in the interval after the health list loses the Hediff but before the state processes the interruption.
                string path = SaveBreakdown(new SlaveBreakdownSaveFixture(pawn, SSCMod.settings));
                Equal("null", XDocument.Load(path).Descendants("sscSlaveBreakdownEffect").Single().Value);
                state.MentalStateTick(1);
                pawn.mindState.mentalStateHandler.Reset();
                Require(pawn.MentalState == null && !pawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(breakdownFailureLetters.SequenceEqual(new[] { pawn }));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Equal(60000, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
                Near(.03f, pawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);

                var (loaded, _) = BreakdownPawn();
                LoadBreakdown(path, loaded);
                ((MentalState_SSCSlaveBreakdown)loaded.MentalState).MentalStateTick(1);
                loaded.mindState.mentalStateHandler.Reset();
                Require(loaded.MentalState == null && !loaded.health.hediffSet.HasHediff(breakdownEffect));
                Require(breakdownFailureLetters.SequenceEqual(new[] { pawn, loaded }));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(loaded));
                Equal(60000, loaded.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
                Near(.03f, loaded.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
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
                Job running = JobMaker.MakeJob(JobDefOf.Wait, 100);
                pawn.jobs.curJob = running;
                pawn.jobs.curDriver = new JobDriver_Wait { pawn = pawn, job = running };
                pawn.jobs.jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Wait, 200));
                handler.CurState.RecoverFromState();
                Require(pawn.CurJob == null && pawn.jobs.jobQueue.Count == 0);
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
                Equal(0, comp.SlaveBreakdownCooldownRemainingTicks);
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
                Equal(0, comp.SlaveBreakdownCooldownRemainingTicks);
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
                Equal(60000, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
            });

            Case("silent replacement settles the old episode and preserves the slave role present at entry", () =>
            {
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                var handler = pawn.mindState.mentalStateHandler;
                var state = (MentalState_SSCSlaveBreakdown)handler.CurState;
                bool reentered = false;
                beforeBreakdownCooldownAdd = () =>
                {
                    reentered = true;
                    state.FinishEnding();
                    Require(breakdownFailureLetters.Count == 0, "reentry settled the episode before cooldown installation finished");
                };
                SetBreakdownTick(3500);
                var external = new MentalStateDef { defName = "SSC_TestSilentReplacement", category = MentalStateCategory.Misc };
                Require(handler.TryStartMentalState(external, transitionSilently: true));
                Require(reentered && breakdownFailureLetters.SequenceEqual(new[] { pawn }));
                Equal(external, handler.CurStateDef);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Equal(60000, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
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

            Case("random checks raw 95 percent before decay while restored full corruption starts without a roll", () =>
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
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown { causedByMood: false } && need.CurLevel < .95f);

                SSCMod.settings.enableCorruptionDecay = false;
                var (restored, restoredNeed) = BreakdownPawn();
                restoredNeed.RestoreCorruption(1f, 1f);
                restoredNeed.NeedInterval();
                runtime.GameComponentTick();
                Equal(1, breakdownRolls);
                Require(restored.MentalState is MentalState_SSCSlaveBreakdown);
            });

            Case("low mood still starts a vanilla break and cannot select SSC slave breakdown", () =>
            {
                Require(!DefDatabase<MentalBreakDef>.AllDefsListForReading.Any(def => def.mentalState == SSCSlaveBreakdown.StateDef));
                var (pawn, need) = BreakdownPawn();
                need.RestoreCorruption(1f, 1f);
                var breaker = SetBreakdownLowMood(pawn, MentalBreakIntensity.Minor);
                Require(breaker.TryDoRandomMoodCausedMentalBreak());
                Require(pawn.MentalState?.def == sadWander && pawn.MentalState.causedByMood);
                Require(!pawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(breakdownLetters.Count == 1 && breakdownLetters[0].Pawn == pawn && !breakdownLetters[0].InBreakdown);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Require(!pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownFullTriggerUsed);
            });

            Case("risk report follows random eligibility and pending full crossings, independently of mood", () =>
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
                using (SSCFullCorruptionImprint.BeginRitual(free))
                    Require(!alert.GetReport().culpritsPawns.Contains(free));
                freeNeed.RestoreCorruption(1f, 1f);
                Require(alert.GetReport().culpritsPawns.Contains(free));
                SetBreakdownLowMood(free, MentalBreakIntensity.Minor);
                Require(alert.GetReport().culpritsPawns.Contains(free));
                freeNeed.SetCorruption(.97f);
                freeNeed.SetCorruption(1f);
                Require(runtime.HasFullRequest(free));
                Require(alert.GetReport().culpritsPawns.Contains(free));
                Equal(0, breakdownRolls);
                SSCMod.settings.EnableSlaveBreakdown = false;
                Require(!runtime.HasFullRequest(free) && !alert.GetReport().active);
            });

            Case("ownerless continuous full corruption starts once after cooldown through the same alert qualification", () =>
            {
                SSCMod.settings.enableCorruptionDecay = false;
                var (pawn, need) = BreakdownPawn(PawnIdentity.Slave);
                var progress = pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed(Hediff_SSCOwnerlessSlave.DefName));
                need.SetCorruption(1f);
                need.NeedInterval();
                Require(pawn.MentalState == null && runtime.HasFullRequest(pawn));
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                pawn.mindState.mentalStateHandler.Reset();
                var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                Require(comp.SlaveBreakdownFullTriggerUsed);
                int deadline = GenTicks.TicksGame + comp.SlaveBreakdownCooldownRemainingTicks;
                SetBreakdownTick(deadline - 1);
                need.NeedInterval();
                Require(pawn.MentalState == null && !SSCSlaveBreakdown.IsAtRisk(pawn));
                SetBreakdownTick(deadline);
                progress.Severity = .9f;
                Require(!SSCSlaveBreakdown.IsAtRisk(pawn));
                need.NeedInterval();
                Require(pawn.MentalState == null);
                progress.Severity = .2f;
                Require(SSCSlaveBreakdown.IsAtRisk(pawn) && !runtime.HasFullRequest(pawn));
                need.NeedInterval();
                var started = pawn.MentalState;
                Require(started is MentalState_SSCSlaveBreakdown);
                need.NeedInterval();
                runtime.GameComponentTick();
                Require(pawn.MentalState == started && !SSCSlaveBreakdown.IsAtRisk(pawn));
                Equal(2, breakdownLetters.Count(letter => letter.Pawn == pawn && letter.InBreakdown));
                Equal(0, breakdownRolls);
            });

            Case("cooldown follows a hard reset, does not block imprint and rearms the spent full trigger only below 94 percent", () =>
            {
                var (pawn, need) = BreakdownPawn();
                need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(pawn.MentalState is MentalState_SSCSlaveBreakdown);
                SetBreakdownTick(2000);
                pawn.mindState.mentalStateHandler.Reset();
                Equal(60000, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
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
                Require(!SSCSlaveBreakdown.IsAtRisk(other));
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
                Equal(60000, pawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
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
                readyNeed.SetCorruption(.97f);
                foreach (Pawn pawn in new[] { ready, expired, changed })
                    Require(pawn.mindState.mentalStateHandler.TryStartMentalState(external));
                // 其他精神状态阻止当下掷骰。真实满值跨越则可登记，风险提示跟随这份短期请求。
                Require(!SSCSlaveBreakdown.IsAtRisk(ready));
                readyNeed.NeedInterval();
                Equal(0, breakdownRolls);
                foreach (Need_Corruption need in new[] { readyNeed, expiredNeed, changedNeed }) need.SetCorruption(1f);
                runtime.GameComponentTick();
                Require(runtime.HasFullRequest(ready) && runtime.HasFullRequest(expired) && runtime.HasFullRequest(changed));
                Require(SSCSlaveBreakdown.IsAtRisk(ready));
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
                // 原待办到期，仍满值者下一次定期检查可以获得新的通用启动资格。
                Require(expired.MentalState == null && !runtime.HasFullRequest(expired) && SSCSlaveBreakdown.IsAtRisk(expired));
                expiredNeed.NeedInterval();
                Require(expired.MentalState is MentalState_SSCSlaveBreakdown);
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
                Equal("2500", XDocument.Load(path).Descendants("ticksToDisappear").Single().Value);
                var (loadedPawn, loadedNeed) = BreakdownPawn();
                loadedNeed.RestoreCorruption(1f, 1f);
                var restored = LoadBreakdown(path, loadedPawn);
                Require(restored.Settings.EnableSlaveBreakdown && loadedPawn.MentalState is MentalState_SSCSlaveBreakdown);
                Require(loadedPawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(SSCIdentityUtility.IsManualIdentityChangeBlocked(loadedPawn));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(loadedPawn));
                Near(10f, loadedPawn.GetStatValue(rjw.RJWStatDefOf.SexFrequency));
                Require(loadedPawn.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownFullTriggerUsed);
                // A paused re-save before the first state refresh still writes the attached Hediff reference.
                var resaved = XDocument.Load(SaveBreakdown(restored));
                Require(resaved.Descendants("sscSlaveBreakdownEffect").Single().Value != "null");
                Equal("2500", resaved.Descendants("ticksToDisappear").Single().Value);

                // 开发版已保存的运行场次使用绝对期限和下次整小时的 tick。
                Edit(path, document =>
                {
                    var record = document.Descendants("li").Single(e => (string)e.Element("def") == breakdownEffect.defName);
                    record.Elements().Where(e => e.Name.LocalName is "ticksToDisappear" or "disappearsAfterTicks" or "seed").Remove();
                    record.SetElementValue("sscBreakdownEffectExpiresAt", 16000);
                    var hours = document.Descendants("sscBreakdownBrokenHoursApplied").Single();
                    hours.Parent.SetElementValue("sscSlaveBreakdownNextBrokenAt", 16000);
                    hours.Remove();
                });
                var (legacyRunning, _) = BreakdownPawn();
                var migrated = LoadBreakdown(path, legacyRunning);
                var migratedSave = XDocument.Load(SaveBreakdown(migrated));
                Equal("2500", migratedSave.Descendants("ticksToDisappear").Single().Value);
                Equal("5", migratedSave.Descendants("sscBreakdownBrokenHoursApplied").Single().Value);

                SetBreakdownTick(15999);
                var loadedEffect = (Hediff_SSCSlaveBreakdown)loadedPawn.health.hediffSet.GetFirstHediffOfDef(breakdownEffect);
                var loadedState = (MentalState_SSCSlaveBreakdown)loadedPawn.MentalState;
                Equal(1, loadedEffect.Timer.ticksToDisappear);
                Equal(loadedEffect.Timer.CompLabelInBracketsExtra, loadedEffect.LabelInBrackets);
                Require(loadedEffect.Label.EndsWith($" ({loadedEffect.LabelInBrackets})"));
                Require(loadedState.CanIssueBehavior);
                SetBreakdownTick(16000, advanceHealth: false);
                Require(loadedState.CanIssueBehavior);
                // 原版健康先移除到期 Hediff，精神状态仍要从原实例结算第六个整小时。
                loadedPawn.health.HealthTick();
                Require(loadedEffect.ShouldRemove && !loadedPawn.health.hediffSet.HasHediff(breakdownEffect));
                Require(!loadedState.CanIssueBehavior);
                Near(1f, loadedPawn.GetStatValue(rjw.RJWStatDefOf.SexFrequency));
                beforeBreakdownCooldownAdd = () =>
                {
                    loadedState.MentalStateTick(1);
                    Require(loadedPawn.MentalState == loadedState && breakdownFailureLetters.Count == 0);
                };
                loadedPawn.mindState.mentalStateHandler.MentalStateHandlerTickInterval(1);
                beforeBreakdownCooldownAdd = null;
                loadedState.FinishEnding();
                Require(loadedPawn.MentalState == null);
                Require(breakdownFailureLetters.SequenceEqual(new[] { loadedPawn }));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(loadedPawn));
                Require(!loadedPawn.health.hediffSet.HasHediff(breakdownEffect));
                Near(.18f, loadedPawn.health.hediffSet.GetFirstHediffOfDef(feelingBroken).Severity);
                restored.Settings.EnableSlaveBreakdown = false;
                path = SaveBreakdown(restored);
                var (after, afterNeed) = BreakdownPawn();
                SSCMod.settings = LoadBreakdown(path, after).Settings;
                Require(!SSCMod.settings.EnableSlaveBreakdown);
                Equal(60000, after.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);
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
                afterNeed.NeedInterval();
                Equal(0, breakdownRolls);
                Require(!SSCSlaveBreakdown.IsAtRisk(after));
                Require(after.MentalState == null);
                SetBreakdownTick(76000, advanceHealth: false);
                Require(!cooldown.ShouldRemove && !SSCSlaveBreakdown.IsAtRisk(after));
                after.health.HealthTick();
                Require(cooldown.ShouldRemove && !after.health.hediffSet.HasHediff(cooldownDef));
                runtime.GameComponentTick();
                Require(after.MentalState == null && SSCSlaveBreakdown.IsAtRisk(after));
                afterNeed.NeedInterval();
                Equal(1, breakdownRolls);
                Require(after.MentalState is MentalState_SSCSlaveBreakdown);

                // 旧隐藏 Hediff 与更早的训练组件字段都换算成原版剩余时间。
                Edit(path, document =>
                {
                    var record = document.Descendants("li").Single(e => (string)e.Element("def") == cooldownDef.defName);
                    record.Elements().Where(e => e.Name.LocalName is "ticksToDisappear" or "disappearsAfterTicks" or "seed").Remove();
                    record.SetElementValue("sscBreakdownCooldownExpiresAt", 76000);
                });
                SetBreakdownTick(75999);
                var (legacyMarker, _) = BreakdownPawn();
                LoadBreakdown(path, legacyMarker);
                Equal(1, legacyMarker.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownRemainingTicks);

                Edit(path, document =>
                {
                    document.Descendants("li").Where(e => (string)e.Element("def") == cooldownDef.defName).Remove();
                    var fixture = document.Root.Element("fixture");
                    fixture.SetElementValue("sscSlaveBreakdownCooldownUntil", 76000);
                    fixture.Elements().Where(e => e.Name.LocalName is "enableSlaveBreakdown" or "sscSlaveBreakdownFullTriggerUsed").Remove();
                });
                var (legacy, _) = BreakdownPawn();
                Require(new SSCSettings().EnableSlaveBreakdown && LoadBreakdown(path, legacy).Settings.EnableSlaveBreakdown);
                var legacyComp = legacy.TryGetComp<CompSexSlaveTraining>();
                Equal(1, legacyComp.SlaveBreakdownCooldownRemainingTicks);
                Require(!legacy.health.hediffSet.HasHediff(cooldownDef));
                legacyComp.MigrateSlaveBreakdownCooldown();
                Require(legacy.health.hediffSet.HasHediff(cooldownDef));
                Equal(1, legacyComp.SlaveBreakdownCooldownRemainingTicks);
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

    private static void SetBreakdownTick(int tick, bool advanceHealth = true)
    {
        int elapsed = tick - GenTicks.TicksGame;
        // 隔离宿主批量跳过地图时间。只运行这两个原版组件的健康 tick，边界用例另走完整健康更新。
        if (advanceHealth && elapsed > 0)
        {
            foreach (Pawn pawn in breakdownPawns.Where(pawn => !pawn.Dead))
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                var timer = hediff switch
                {
                    Hediff_SSCSlaveBreakdown effect => effect.Timer,
                    Hediff_SSCSlaveBreakdownCooldown cooldown => cooldown.Timer,
                    _ => null
                };
                int steps = Math.Min(elapsed, timer?.ticksToDisappear ?? 0);
                for (int i = 0; i < steps; i++) hediff.PostTick();
            }
        }
        AccessTools.Field(typeof(TickManager), "ticksGameInt").SetValue(Current.Game.tickManager, tick);
    }

    private static bool ControlBreakdownRoll(float mtb, float mtbUnit, float ticksSinceLastCheck, ref bool __result)
    {
        if (mtb != 1.5f || mtbUnit != 60000f || ticksSinceLastCheck != 150f) return true;
        breakdownRolls++;
        __result = breakdownRollSucceeds;
        return false;
    }

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

    private static bool BreakdownHediffAddBoundary(Hediff __0)
    {
        if (__0 is Hediff_SSCSlaveBreakdownCooldown) beforeBreakdownCooldownAdd?.Invoke();
        return skipBreakdownEffectFor == null || __0 is not Hediff_SSCSlaveBreakdown || __0.pawn != skipBreakdownEffectFor;
    }

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
        string type = label.RawText == "SSC_BreakdownSuccess_Label".Translate().RawText ? "success" :
            label.RawText == "SSC_Imprint_FullCorruptionLetterLabel".Translate().RawText ? "imprint" :
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

    private static bool BreakdownInvitationThingFogBoundary(Thing t, ref bool __result)
        => BreakdownInvitationFogBoundary(t.Map, ref __result);

    private static bool BreakdownDrawPosBoundary(Pawn __instance, ref UnityEngine.Vector3 __result)
    {
        if (!breakdownPawns.Contains(__instance)) return true;
        __result = default;
        return false;
    }

    // 本宿主没有加载玩家的互动 Def。捕获正式 RJW 查询的方向，并提供兼容与不兼容的两种结果。
    private static bool BreakdownInvitedUsePairBoundary(rjw.SexProps props, ref List<SexInteractionResolved> __result)
    {
        if (!breakdownPawns.Contains(props.pawn) || !breakdownPawns.Contains(props.partner)) return true;
        Require(props.isRape && props.pawn.MentalState is not MentalState_SSCSlaveBreakdown &&
            props.partner.MentalState is MentalState_SSCSlaveBreakdown);
        __result = props.pawn == incompatibleInvitedUseActor ? new List<SexInteractionResolved>() :
            new List<SexInteractionResolved> { null };
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
        __result = true;
        return false;
    }

    private static bool BreakdownInvitationMoteBoundary(Map map)
    {
        if (map != breakdownMap) return true;
        invitationMotes++;
        return false;
    }

    private static bool BreakdownInvitedUseHeroBoundary(Pawn __0, ref bool __result)
    {
        if (!breakdownPawns.Contains(__0)) return true;
        __result = false;
        return false;
    }

    // 地图宿主只隔离预约和行走的安装边界。生产代码仍负责何时以及向哪个追踪器派发，终止流程走原版。
    private static bool BreakdownInvitedUseStartBoundary(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob,
        JobCondition lastJobEndCondition)
    {
        if (!breakdownPawns.Contains(___pawn)) return true;
        if (SSCBreakdownInvitation.IsWait(newJob))
        {
            if (__instance.curJob != null)
                __instance.EndCurrentJob(JobCondition.InterruptOptional, startNewJob: false);
            __instance.curJob = newJob;
            __instance.curDriver = new JobDriver_Wait { pawn = ___pawn, job = newJob };
            return false;
        }
        if (!JobDriver_SSCBreakdownInvitedUse.IsInvitedJob(newJob)) return true;
        Equal(JobCondition.InterruptForced, lastJobEndCondition);
        beforeInvitedUseStart?.Invoke();
        invitedUseDispatches++;
        if (rejectInvitedUseDispatch) return false;
        if (__instance.curJob != null)
            __instance.EndCurrentJob(lastJobEndCondition, startNewJob: false);
        __instance.curJob = newJob;
        var driver = new JobDriver_SSCBreakdownInvitedUse { pawn = ___pawn, job = newJob };
        __instance.curDriver = driver;
        invitedUseOnInstalled?.Invoke(driver);
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

    private static Job RecordInvitedUse(MentalState_SSCSlaveBreakdown state, Pawn actor)
    {
        Require(state.TryRecordInvitation(actor, SSCPersonalityRuntimeUtility.Capture(actor), true,
            out Job candidate));
        Require(SSCBreakdownInvitation.IsWait(candidate) && state.HasBehaviorJob);
        return candidate;
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
