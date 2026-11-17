// AI 辅助生成：真实恶堕增长、印记和原版精神状态入口共用原生宿主。
using System.Reflection;
using System.Reflection.Emit;
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
    private static bool breakdownRollSucceeds;
    private static int breakdownRolls;
    private static MentalBreakIntensity? selectedBreakdownIntensity;
    private static bool tickDuringImprintLetter;
    private static Pawn skipBreakdownEffectFor;
    private static Pawn interruptBeforeBreakdownStart;

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
            HediffDef LoadHediff(string path)
            {
                xml.Load(path);
                var def = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode("HediffDef"), false);
                DefDatabase<HediffDef>.Add(def);
                return def;
            }
            var breakdownEffect = LoadHediff(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_SlaveBreakdown.xml"));
            var feelingBroken = LoadHediff(RjwResource("Defs/HediffDefs/Hediffs_FeelingBroken.xml"));
            errors.Clear();
            DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
            Require(errors.Count == 0, string.Join('\n', errors));

            // 安装生产补丁，保留原版选池、状态赋值、Scribe 和信件构造。隔离地图网格、其 UI 更新及掷骰抽样。
            foreach (Type type in new[] { typeof(Patch_SSCSlaveBreakdownMoodSelection), typeof(Patch_SSCSlaveBreakdownClear),
                         typeof(Patch_SSCSlaveBreakdownTransition), typeof(Patch_SSCSlaveBreakdownDeath) })
                boundary.CreateClassProcessor(type).Patch();
            void Prefix(MethodBase method, string name) => boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), name));
            Prefix(AccessTools.Method(typeof(AttackTargetsCache), nameof(AttackTargetsCache.UpdateTarget)), nameof(Skip));
            Prefix(AccessTools.Method(typeof(MapPawns), "DoListChangedNotifications"), nameof(Skip));
            Prefix(AccessTools.Method(typeof(Rand), nameof(Rand.MTBEventOccurs)), nameof(ControlBreakdownRoll));
            Prefix(AccessTools.Method(typeof(MentalBreakWorker_SSCSlaveBreakdown), nameof(MentalBreakWorker_SSCSlaveBreakdown.TryStart)), nameof(ObserveMoodBreakChoice));
            Prefix(AccessTools.Method(typeof(MentalBreaker), "RandomFinalStraw"), nameof(Skip));
            Prefix(AccessTools.Method(typeof(GenText), nameof(GenText.AdjustedFor),
                new[] { typeof(string), typeof(Pawn), typeof(string), typeof(bool) }), nameof(BreakdownGrammarBoundary));
            Prefix(AccessTools.Method(typeof(ColoredText), nameof(ColoredText.Resolve)), nameof(BreakdownColorBoundary));
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
                    breakdownRollSucceeds = false;
                    breakdownRolls = 0;
                    selectedBreakdownIntensity = null;
                    tickDuringImprintLetter = false;
                    skipBreakdownEffectFor = null;
                    interruptBeforeBreakdownStart = null;
                    action();
                });
            }

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

            Case("temporary role and native need effects end on interruption while completed FeelingBroken progress persists", () =>
            {
                var (pawn, need) = BreakdownPawn();
                var comp = new rjw.CompRJW { parent = pawn };
                AccessTools.Field(typeof(rjw.CompRJW), "pawnData").SetValue(comp, new rjw.PawnData(pawn) { raceSexDrive = 1f });
                ((List<ThingComp>)AccessTools.Field(typeof(ThingWithComps), "comps").GetValue(pawn)).Add(comp);
                var sexNeed = new rjw.Need_Sex(pawn) { def = new NeedDef { defName = "SSC_TestSexNeed" } };
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
                runtime.GameComponentTick();
                Require(after.MentalState == null && SSCSlaveBreakdown.IsAtRisk(after));
                afterNeed.NeedInterval();
                Equal(1, breakdownRolls);
                Require(after.MentalState is MentalState_SSCSlaveBreakdown);

                Edit(path, document => document.Root.Element("fixture").Elements().Where(e => e.Name.LocalName is
                    "enableSlaveBreakdown" or "sscSlaveBreakdownCooldownUntil" or "sscSlaveBreakdownFullTriggerUsed").Remove());
                var (legacy, _) = BreakdownPawn();
                Require(!LoadBreakdown(path, legacy).Settings.EnableSlaveBreakdown);
                Equal(0L, legacy.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownCooldownUntil);
                Require(!legacy.TryGetComp<CompSexSlaveTraining>().SlaveBreakdownFullTriggerUsed);
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
        pawn.RaceProps.lifeStageAges[0].def.canDoRandomMentalBreaks = true;
        // 原生宿主只登记躯干，不模拟脑部容量。原版 Awake 仍会检查角色的真实任务。
        pawn.RaceProps.alwaysAwake = true;
        pawn.jobs = new Pawn_JobTracker(pawn);
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

    private static SlaveBreakdownSaveFixture LoadBreakdown(string path, Pawn pawn)
    {
        SlaveBreakdownSaveFixture fixture = null;
        Scribe.loader.InitLoading(path);
        Scribe_Deep.Look(ref fixture, "fixture", pawn, new SSCSettings { EnableSlaveBreakdown = true });
        Scribe.loader.FinalizeLoading();
        return fixture;
    }
}

public sealed class SlaveBreakdownSaveFixture : IExposable
{
    private readonly Pawn pawn;
    public readonly SSCSettings Settings;

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
    }
}
