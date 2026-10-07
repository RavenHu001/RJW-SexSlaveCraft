using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static int passed, failed;
    private static string root;
    private const SexSlaveSpecializationType Dog = SexSlaveSpecializationType.PetDog;
    private static HediffDef DogFinal => PetSpecializationUtility.GetFinalHediffDef(Dog);

    /// <summary>资格、接近、效果和冷却迁移直接执行生产源码，原版边界只记录输入及副作用。</summary>
    private static int Main(string[] args)
    {
        root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        Run("狗技能XML为单一驯服效果、两秒预热和一天冷却", Definitions);
        Run("技能、任务和拒绝原因四语及镜像完整", Translations);
        Run("普通狗和其他终极不授予，狗终极严重度达标才授予", FinalQualification);
        Run("旧普通Hediff终极狗授予技能，重复维护保留能力及冷却", LegacyGrant);
        Run("终极狗跨方向留空及SSC身份可用，不依赖绑定研究与征召", CrossDirectionQualification);
        Run("失格施放者在全部入口拒绝，不进入原版激活", InvalidCasters);
        Run("目标基础资格及玩家无待训项目拒绝", TargetQualifications);
        Run("原版CanTame拒绝动物在选择预热生效各入口均拒绝", NativeTamingQualification);
        Run("远处与隔墙可达目标可选，实际预热和生效要求同格", RangeBoundaries);
        Run("Valid与CanApplyOn保留原版限制并报告非法目标", ComponentValidation);
        Run("生效只调用一次原版DoRecruit，立即成为玩家阵营", NativeRecruitBoundary);
        Run("原版驯服无实际阵营转换时清本次冷却，重试不沿用上次成功", NativeRecruitFailure);
        Run("零个或重复驯服效果在预热前拒绝损坏配置", MissingEffectConfiguration);
        Run("直接效果复查资格，重复施放或两名施放者不重复驯服", DirectAndCompetingCasts);
        Run("生效前资格变化不进入PreActivate也不扣冷却", ExecutionRecheck);
        Run("真实授予组件与兼容维护保留同一能力和冷却", GrantLifecycle);
        Run("无终极维护撤销孤儿狗技能但保留其他技能", RemoveFinalLifecycle);
        Run("授予维护只在Inactive或PostLoadInit运行，失去有效成果可撤销", MaintenanceModes);
        Run("绝对截止tick捕获与恢复，凝胶存放时间继续流逝", CooldownMigration);
        Run("无终极植入撤销孤儿技能，过期与旧字段恢复就绪", RestoreWithoutFinal);
        Run("提取清狗标签方向历史能力，保留其他方向成果与技能", DetachAfterExtraction);
        Run("缺角色能力容器或定义的维护迁移入口安全", MissingBoundaries);
        RunMovementCases();
        RunVisualCases();
        RunOwnedTrainingCases();
        Console.WriteLine($"RESULT: {passed}/{passed + failed} cases passed.");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>每组重建最小定义与边界记录，不让冷却、阵营或渲染调用泄漏。</summary>
    private static void Run(string name, Action test)
    {
        Reset();
        try { test(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL: " + name + "\n" + error); }
    }
    private static void Reset()
    {
        Find.TickManager.TicksGame = 100000; Scribe.mode = LoadSaveMode.Inactive; Messages.Requests.Clear();
        Scribe_Values.Values.Clear(); GenDraw.Highlights.Clear(); GenUI.Attachments.Clear(); Widgets.AttachedLabels.Clear();
        InteractionWorker_RecruitAttempt.Requests.Clear();
        TrainableUtility.TrainableDefsInListOrder.Clear();
        DefDatabase<HediffDef>.Definitions.Clear(); DefDatabase<ResearchProjectDef>.Definitions.Clear();
        foreach (string pet in new[] { "PetCat", "PetDog", "PetRabbit" })
        {
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + pet });
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + pet + "_Final", initialSeverity = 1 });
            DefDatabase<ResearchProjectDef>.Add(new ResearchProjectDef { defName = "SSC_RES_" + pet, IsFinished = false });
        }
        SSCDefOf.SSC_BasicTraining.IsFinished = false;
        SSCDefOf.SSC_PetDogTame.jobDef = SSCDefOf.SSC_Job_PetDogTame;
        SSCDefOf.SSC_PetDogTame.EffectFactory = ability => new CompAbilityEffect_PetDogTame
        { parent = ability, props = new CompProperties_AbilityPetDogTame() };
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal(float expected, float actual) => Check(Math.Abs(expected - actual) < .00001f, $"expected {expected}, actual {actual}");
    private static Pawn Pawn(SexSlaveSpecializationType type = SexSlaveSpecializationType.None, float progress = 0)
    {
        var pawn = new Pawn { Training = new CompSexSlaveTraining { pawnIdentity = PawnIdentity.Slave } };
        pawn.Training.parent = pawn; pawn.Training.SetSpecialization(type); pawn.Training.specializationProgress = progress;
        return pawn;
    }
    private static (Pawn caster, Pawn target, Ability_PetDogTame ability) Pair()
    {
        var caster = Pawn(Dog, .34f); caster.health.AddHediff(DogFinal); PetDogAbilityUtility.Maintain(caster);
        var target = Pawn(); target.Map = caster.Map; target.Position = caster.Position;
        target.Faction = null; target.RaceProps.Humanlike = false; target.RaceProps.Animal = true;
        return (caster, target, (Ability_PetDogTame)caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame));
    }
    private static CompAbilityEffect_PetDogTame Effect(Ability ability) => (CompAbilityEffect_PetDogTame)ability.EffectComps.Single();
    private static Verb_PetDogTame Verb(Ability ability) => (Verb_PetDogTame)ability.verb;
    private static void TickGrant(Pawn pawn)
    {
        float adjustment = 0;
        foreach (Hediff h in pawn.health.hediffSet.hediffs.ToArray()) h.Grant?.CompPostTick(ref adjustment);
    }
    private static XElement Definition(string path, string name) => XDocument.Load(Path.Combine(root, path)).Root.Elements().Single(e => (string)e.Element("defName") == name);
    private static void NoSettlement((Pawn caster, Pawn target, Ability_PetDogTame ability) p)
    {
        Check(p.ability.ActivationCalls == 0 && p.ability.CooldownStartCalls == 0 && InteractionWorker_RecruitAttempt.Requests.Count == 0 && (p.target.training == null || p.target.training.Requests.Count == 0),
            "invalid/approaching cast entered native activation, cooldown or recruit");
    }

    /// <summary>核对资源接线与原版读条参数，不在宿主重写Effecter或游戏驯服算法。</summary>
    private static void Definitions()
    {
        var def = Definition("Defs/AbilityDefs/SSC_PetDogTame.xml", "SSC_PetDogTame");
        Check((string)def.Element("abilityClass") == typeof(Ability_PetDogTame).FullName, "wrong ability class");
        Check((string)def.Element("cooldownTicksRange") == "60000", "wrong cooldown");
        foreach (string field in new[] { "targetRequired", "showWhenDrafted", "displayGizmoWhileUndrafted" }) Check((string)def.Element(field) == "true", "missing " + field);
        Check((string)def.Element("disableGizmoWhileUndrafted") == "false", "undrafted disabled");
        var verb = def.Element("verbProperties");
        Equal(0, float.Parse((string)verb.Element("range"))); Equal(2, float.Parse((string)verb.Element("warmupTime")));
        Check((string)verb.Element("verbClass") == typeof(Verb_PetDogTame).FullName, "custom verb missing");
        Check((string)verb.Element("requireLineOfSight") == "false" && (string)def.Element("stunTargetWhileCasting") == "true", "native same-cell warmup wiring wrong");
        Check(def.Descendants("showCastingProgressBar").Any(e => (string)e == "true"), "native progress missing");
        Check((string)def.Element("jobDef") == "SSC_Job_PetDogTame", "approach job missing");
        Check((string)verb.Element("targetParams").Element("canTargetAnimals") == "true" && (string)verb.Element("targetParams").Element("canTargetSelf") == "false", "animal targeting wiring wrong");
        foreach (string field in new[] { "canTargetHumans", "canTargetMechs", "neverTargetIncapacitated", "canTargetBuildings", "canTargetLocations" })
            Check((string)verb.Element("targetParams").Element(field) == "false", "wrong target XML " + field);
        Check((string)def.Element("warmupMoteSocialSymbol") == "Things/Mote/SpeechSymbols/TrainAttempt" && (string)def.Element("iconPath") == "UI/Icons/Trainables/Tameness", "native train icon/social symbol missing");
        Check((string)def.Element("comps").Elements().Single().Attribute("Class") == typeof(CompProperties_AbilityPetDogTame).FullName, "single recruit effect missing");
        var job = Directory.EnumerateFiles(Path.Combine(root, "Defs/JobDefs"), "*.xml").SelectMany(path => XDocument.Load(path).Root.Elements()).Single(e => (string)e.Element("defName") == "SSC_Job_PetDogTame");
        Check((string)job.Element("driverClass") == typeof(JobDriver_PetDogTame).FullName && (string)job.Element("abilityCasting") == "false" && (string)job.Element("collideWithPawns") == "false", "approach driver casts early or collides");
        var effects = Directory.EnumerateFiles(Path.Combine(root, "Defs/EffecterDefs"), "*.xml").SelectMany(path => XDocument.Load(path).Root.Elements());
        Check(def.Element("warmupEffecter") != null && effects.Any(e => (string)e.Element("defName") == (string)def.Element("warmupEffecter")), "warmup effect not wired");
        var speech = effects.Single(e => (string)e.Element("defName") == (string)def.Element("warmupEffecter")).Element("children").Elements().Single();
        Check((string)speech.Element("subEffecterClass") == "SubEffecter_SprayerContinuous" && (string)speech.Element("fleckDef") == "SpeechLines" && (string)speech.Element("spawnLocType") == "OnTarget", "native continuous speech warmup missing");
        Check((string)speech.Element("ticksBetweenMotes") == "15" && (string)speech.Element("initialDelayTicks") == "0" && (string)speech.Element("makeMoteOnSubtrigger") == "false" && (string)speech.Element("maxMoteCount") == "10" && (string)speech.Element("burstCount") == "1", "native speech cadence/bound wrong");
        Check((string)speech.Element("positionOffset") == "(0.35, 0, 0.6)", "speech obscured by same-cell bodies"); Equal(1.7f, float.Parse((string)speech.Element("scale"))); Equal(0, float.Parse((string)speech.Element("speed")));
    }

    private static void Translations()
    {
        string[] languages = { "ChineseSimplified", "ChineseTraditional", "English", "Russian" };
        foreach (string lang in languages)
        {
            foreach (string area in new[] { "DefInjected/AbilityDef", "DefInjected/JobDef", "Keyed" })
            {
                string directory = Path.Combine(root, "Languages", lang, area);
                var files = Directory.EnumerateFiles(directory, "*.xml").Where(path => XDocument.Load(path).Descendants().Any(e => e.Name.LocalName.StartsWith("SSC_PetDogTame") || e.Name.LocalName.StartsWith("SSC_Job_PetDogTame"))).ToArray();
                Check(files.Length > 0, "dog text missing " + lang + "/" + area);
                foreach (string file in files)
                {
                    string mirror = Path.Combine(root, "Sexslavecraft", Path.GetRelativePath(root, file));
                    Check(File.Exists(mirror) && File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(mirror)), "language mirror differs " + file);
                }
                if (area == "Keyed")
                {
                    var keys = files.SelectMany(path => XDocument.Load(path).Root.Elements()).ToArray();
                    foreach (string suffix in new[] { "RequiresFinal", "InvalidCaster", "InvalidTarget", "UnconsciousTarget", "UntamableTarget", "UnreachableTarget", "MissingEffect", "TargetHint" })
                        Check(!string.IsNullOrWhiteSpace(keys.Single(e => e.Name.LocalName == "SSC_PetDogTame" + suffix).Value), "missing/empty reject/hint " + suffix);
                    Check(keys.Single(e => e.Name.LocalName == "SSC_PetDogTameTargetHint").Value.Contains("{0}"), "target name placeholder missing");
                }
                else if (area == "DefInjected/AbilityDef")
                {
                    var keys = files.SelectMany(path => XDocument.Load(path).Root.Elements()).ToArray();
                    foreach (string suffix in new[] { "label", "description" })
                        Check(!string.IsNullOrWhiteSpace(keys.Single(e => e.Name.LocalName == "SSC_PetDogTame." + suffix).Value), "missing ability " + suffix);
                }
                else Check(files.SelectMany(path => XDocument.Load(path).Root.Elements()).Any(e => e.Name.LocalName == "SSC_Job_PetDogTame.reportString" && !string.IsNullOrWhiteSpace(e.Value)), "job report missing");
            }
        }
    }

    private static void FinalQualification()
    {
        foreach (float severity in new[] { 0f, .009f, .01f, 1f })
        {
            Reset(); var caster = Pawn(Dog, 1); caster.health.AddHediff(DogFinal).Severity = severity;
            PetDogAbilityUtility.Maintain(caster);
            Check(PetDogAbilityUtility.CanUse(caster, out _) == (severity >= .01f), "wrong severity gate");
            Check((caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) != null) == (severity >= .01f), "wrong severity grant");
        }
        foreach (var type in new[] { Dog, SexSlaveSpecializationType.PetCat, SexSlaveSpecializationType.PetRabbit })
        {
            Reset(); var caster = Pawn(type, 1);
            caster.health.AddHediff(type == Dog ? PetSpecializationUtility.GetBaseHediffDef(type) : PetSpecializationUtility.GetFinalHediffDef(type));
            PetDogAbilityUtility.Maintain(caster);
            Check(!PetDogAbilityUtility.CanUse(caster, out _) && caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) == null, "ordinary/other pet granted dog skill");
        }
    }
    private static void LegacyGrant()
    {
        var caster = Pawn(); var legacy = new Hediff { pawn = caster, def = DogFinal, Severity = 1 };
        caster.health.hediffSet.hediffs.Add(legacy); Check(legacy.Grant == null, "legacy fixture unexpectedly has grant comp");
        PetDogAbilityUtility.Maintain(caster);
        var ability = caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame); Check(ability is Ability_PetDogTame, "legacy final missed immediate grant");
        ability.StartCooldown(900); var verb = ability.verb;
        for (int i = 0; i < 3; i++) PetDogAbilityUtility.Maintain(caster);
        Check(ReferenceEquals(ability, caster.abilities.GetAbility(ability.def)) && ReferenceEquals(verb, ability.verb) && ability.CooldownTicksRemaining == 900 && ability.CooldownStartCalls == 1, "maintenance replaced ability/cooldown");
        Check(ReferenceEquals(legacy, caster.health.hediffSet.hediffs.Single(h => h.def == DogFinal)), "legacy final replaced or converted");
    }
    private static void CrossDirectionQualification()
    {
        foreach (SexSlaveSpecializationType direction in Enum.GetValues<SexSlaveSpecializationType>())
        foreach (PawnIdentity identity in Enum.GetValues<PawnIdentity>())
        foreach (bool drafted in new[] { false, true })
        {
            Reset(); var caster = Pawn(direction, .37f); caster.Training.pawnIdentity = identity; caster.Drafted = drafted;
            caster.health.AddHediff(DogFinal); PetDogAbilityUtility.Maintain(caster);
            Check(PetDogAbilityUtility.CanUse(caster, out _) && caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).CanCast, "final tied to direction/identity/draft " + direction);
        }
        var bare = new Pawn(); bare.health.AddHediff(DogFinal); PetDogAbilityUtility.Maintain(bare);
        Check(PetDogAbilityUtility.CanUse(bare, out _), "final incorrectly requires training component");
    }
    private static void InvalidCasters()
    {
        for (int scenario = 0; scenario < 9; scenario++)
        {
            Reset(); var p = Pair();
            if (scenario == 0) p.caster.Dead = true;
            if (scenario == 1) p.caster.Destroyed = true;
            if (scenario == 2) p.caster.Downed = true;
            if (scenario == 3) p.caster.Spawned = false;
            if (scenario == 4) p.caster.MentalState = new MentalState();
            if (scenario == 5) p.caster.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            if (scenario == 6) p.caster.health.hediffSet.hediffs.Clear();
            if (scenario == 7) p.caster.Faction = null;
            if (scenario == 8) p.caster.Faction = new Faction();
            Check(!PetDogAbilityUtility.CanUse(p.caster, out _) && !PetDogAbilityUtility.CanTarget(p.caster, p.target, out _) && !p.ability.CanCast, "invalid caster accepted " + scenario);
            Check(!Verb(p.ability).TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default), "invalid caster executed " + scenario); NoSettlement(p);
        }
    }
    private static void TargetQualifications()
    {
        for (int scenario = 0; scenario < 12; scenario++)
        {
            Reset(); var p = Pair();
            if (scenario == 0) p.target.Faction = Faction.OfPlayer;
            if (scenario == 1) p.target.Faction = new Faction();
            if (scenario == 2) p.target.Map = new Map();
            if (scenario == 3) p.target.Dead = true;
            if (scenario == 4) p.target.Destroyed = true;
            if (scenario == 5) p.target.Spawned = false;
            if (scenario == 6) p.target.RaceProps.Animal = false;
            if (scenario == 7) { p.target.RaceProps.Humanlike = true; p.target.RaceProps.Animal = false; }
            if (scenario == 8) p.target.Conscious = false;
            if (scenario == 9) p.target.MentalState = new MentalState();
            if (scenario == 10) p.target.health.capacities = null;
            if (scenario == 11) p.target.RaceProps = null;
            Check(!PetDogAbilityUtility.CanTarget(p.caster, p.target, out _) && !Effect(p.ability).Valid(p.target) && !p.ability.Activate(p.target, default), "invalid target accepted " + scenario); NoSettlement(p);
        }
        Reset(); var missing = Pair(); Check(!PetDogAbilityUtility.CanTarget(missing.caster, null, out _) && !PetDogAbilityUtility.CanTarget(missing.caster, missing.caster, out _), "null/self accepted");
        missing.target.RaceProps = new RaceProperties { Humanlike = false, Animal = false, IsMechanoid = true };
        Check(!PetDogAbilityUtility.CanTarget(missing.caster, missing.target, out _), "mechanoid accepted");
    }
    private static void NativeTamingQualification()
    {
        var p = Pair(); p.target.NativeTamingAllowed = false;
        Check(!PetDogAbilityUtility.CanTarget(p.caster, p.target, out string reason) && reason == "SSC_PetDogTameUntamableTarget", "native CanTame gate ignored");
        Check(!PetDogAbilityUtility.CanSelectTarget(p.caster, p.target, out _) && !PetDogAbilityUtility.CanApply(p.caster, p.target, out _) && !Effect(p.ability).Valid(p.target), "native qualification missing at selection/application");
        Check(!Verb(p.ability).TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) && !PetDogAbilityUtility.Apply(p.caster, p.target), "untamable entered native cast/recruit"); NoSettlement(p);
    }
    private static void RangeBoundaries()
    {
        var p = Pair(); p.target.Position = new IntVec3(200, 75); p.caster.Map.LineOfSight = false;
        Check(PetDogAbilityUtility.CanSelectTarget(p.caster, p.target, out _) && p.caster.LastReachMode == PathEndMode.OnCell && Verb(p.ability).ValidateTarget(p.target), "map selection blocked by range/LOS");
        Check(!PetDogAbilityUtility.CanApply(p.caster, p.target, out _) && !Verb(p.ability).CanHitTarget(p.target) && !Verb(p.ability).CanHitTargetFrom(p.caster.Position, p.target), "remote execution accepted");
        Check(!Verb(p.ability).TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default), "remote execution entered native"); NoSettlement(p);
        p.caster.Reachable = false; Check(!PetDogAbilityUtility.CanSelectTarget(p.caster, p.target, out _) && !Verb(p.ability).ValidateTarget(p.target), "unreachable selectable");
        p.caster.Reachable = true; p.caster.Position = p.target.Position;
        Check(PetDogAbilityUtility.CanApply(p.caster, p.target, out _) && Verb(p.ability).CanHitTargetFrom(p.caster.Position, p.target), "same-cell rejected");
        Check(!Verb(p.ability).CanHitTargetFrom(new IntVec3(201, 75), p.target), "adjacent From accepted");
    }
    private static void ComponentValidation()
    {
        var p = Pair(); var comp = Effect(p.ability); p.target.Position = new IntVec3(50, 50);
        Check(comp.Valid(p.target) && comp.CanApplyOn(p.target, default) && p.ability.CanApplyOn(p.target), "selection uses execution range");
        comp.BaseAllowed = false; Check(!comp.Valid(p.target) && !comp.CanApplyOn(p.target, default), "base validity dropped");
        comp.BaseAllowed = true; p.target.Conscious = false;
        Check(!comp.Valid(p.target, true) && Messages.Requests.Count == 1, "invalid target report missing"); NoSettlement(p);
    }
    private static void NativeRecruitBoundary()
    {
        var p = Pair(); var training = p.caster.Training; var before = training.ExportSpecializationProgress();
        Check(p.ability.Activate(p.target, default), "valid native recruit rejected");
        Check(p.target.Faction == Faction.OfPlayer && InteractionWorker_RecruitAttempt.Requests.Count == 1, "native recruit not called once");
        var request = InteractionWorker_RecruitAttempt.Requests.Single(); Check(request.recruiter == p.caster && request.recruitee == p.target, "native recruit reversed pawn arguments");
        Check(p.ability.ActivationCalls == 1 && p.ability.CooldownStartCalls == 1 && p.ability.CooldownTicksRemaining == 60000 && Effect(p.ability).BaseApplyCalls == 1, "native effect/cooldown duplicated");
        Check(training.ExportSpecializationProgress().OrderBy(e => e.Key).SequenceEqual(before.OrderBy(e => e.Key)) && training.lastPetAffectionTick == -999999 && p.target.Training.specializationProgress == 0, "skill granted SSC progress or affection");
        Check(!p.ability.Activate(p.target, default) && InteractionWorker_RecruitAttempt.Requests.Count == 1 && p.ability.CooldownStartCalls == 1, "repeat cast recruited twice");
        Check(p.target.health.hediffSet.hediffs.Count == 0 && p.caster.health.hediffSet.hediffs.Count == 1, "direct tame added unrelated health effects");
    }
    private static void DirectAndCompetingCasts()
    {
        var p = Pair(); var rival = Pawn(); rival.Map = p.caster.Map; rival.Position = p.target.Position; rival.health.AddHediff(DogFinal); PetDogAbilityUtility.Maintain(rival);
        var rivalAbility = (Ability_PetDogTame)rival.abilities.GetAbility(SSCDefOf.SSC_PetDogTame);
        Check(PetDogAbilityUtility.Apply(p.caster, p.target), "valid direct effect rejected");
        Check(!PetDogAbilityUtility.Apply(p.caster, p.target) && !rivalAbility.Activate(p.target, default), "already recruited target accepted");
        Check(InteractionWorker_RecruitAttempt.Requests.Count == 1 && rivalAbility.CooldownStartCalls == 0 && rivalAbility.ActivationCalls == 0, "competing caster consumed cooldown");
        Reset(); p = Pair(); p.target.Position = new IntVec3(1, 0); Effect(p.ability).Apply(p.target, default);
        Check(Effect(p.ability).BaseApplyCalls == 0, "invalid direct component forwarded base"); NoSettlement(p);

        // 两名施放者同时预热同一目标，只允许先完成者结算；另一人按最终实际阵营取消。
        Reset(); p = Pair(); rival = Pawn(); rival.Map = p.caster.Map; rival.Position = p.target.Position; rival.health.AddHediff(DogFinal); PetDogAbilityUtility.Maintain(rival);
        rivalAbility = (Ability_PetDogTame)rival.abilities.GetAbility(SSCDefOf.SSC_PetDogTame);
        var first = new CastBoundaryRunner(p.caster, p.target, p.ability); var second = new CastBoundaryRunner(rival, p.target, rivalAbility);
        first.NotifyArrival(); second.NotifyArrival();
        for (int i = 0; i < 119; i++) { first.Tick(); second.Tick(); }
        first.Tick(); second.Tick();
        Check(p.target.Faction == Faction.OfPlayer && InteractionWorker_RecruitAttempt.Requests.Count == 1 && p.ability.CooldownStartCalls == 1 && rivalAbility.ActivationCalls == 0 && rivalAbility.CooldownStartCalls == 0 && second.Ended, "simultaneous warmups duplicated native effect/cooldown");
    }
    /// <summary>第三方不转换阵营作为明确的原版边界输入，生产代码自己恢复本次冷却。</summary>
    private static void NativeRecruitFailure()
    {
        var p = Pair(); p.target.NativeRecruitChangesFaction = false;
        Check(!p.ability.Activate(p.target, default) && p.ability.CooldownTicksRemaining == 0 && p.target.Faction == null && !Effect(p.ability).Succeeded && Effect(p.ability).BaseApplyCalls == 0, "no-effect recruit spent cooldown or reported success");
        Check(p.ability.ActivationCalls == 1 && p.ability.CooldownStartCalls == 1 && InteractionWorker_RecruitAttempt.Requests.Count == 1, "failure did not enter exactly one native attempt");
        p.target.NativeRecruitChangesFaction = true;
        Check(p.ability.Activate(p.target, default) && Effect(p.ability).Succeeded && p.ability.CooldownTicksRemaining == 60000 && InteractionWorker_RecruitAttempt.Requests.Count == 2, "failed recruit cannot retry immediately");
        p.ability.ResetCooldown(); p.target.Faction = null; p.target.NativeRecruitChangesFaction = false;
        Check(!p.ability.Activate(p.target, default) && !Effect(p.ability).Succeeded && p.ability.CooldownTicksRemaining == 0 && Effect(p.ability).BaseApplyCalls == 1 && InteractionWorker_RecruitAttempt.Requests.Count == 3, "last successful result leaked into failed retry");
    }
    private static void MissingEffectConfiguration()
    {
        foreach (bool duplicate in new[] { false, true })
        {
            Reset(); var p = Pair();
            if (duplicate) p.ability.EffectComps.Add(new CompAbilityEffect_PetDogTame { parent = p.ability, props = new CompProperties_AbilityPetDogTame() });
            else p.ability.EffectComps.Clear();
            Check(!p.ability.CanCast && !Verb(p.ability).TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default), "broken sole effect contract allowed cast");
            Check(p.target.stances.stunner.StunCalls == 0 && Verb(p.ability).BaseStartCalls == 0, "broken effects entered native warmup"); NoSettlement(p);
        }
    }
    private static void ExecutionRecheck()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            Reset(); var p = Pair(); Check(Effect(p.ability).CanApplyOn(p.target, default), "fixture invalid before queue");
            if (scenario == 0) p.target.Faction = Faction.OfPlayer;
            if (scenario == 1) p.target.MentalState = new MentalState();
            if (scenario == 2) p.target.Conscious = false;
            if (scenario == 3) p.target.Position = new IntVec3(1, 0);
            if (scenario == 4) p.caster.health.hediffSet.hediffs.Clear();
            if (scenario == 5) { p.caster.abilities.RemoveAbility(p.ability.def); p.caster.abilities.GainAbility(p.ability.def); }
            if (scenario == 6) Effect(p.ability).BaseAllowed = false;
            Check(!p.ability.Activate(p.target, default) && !Verb(p.ability).TryStartCastOn(p.target, default), "late invalid executed " + scenario); NoSettlement(p);
        }
    }
    private static void GrantLifecycle()
    {
        DogFinal.grantedAbility = SSCDefOf.SSC_PetDogTame;
        var caster = Pawn(Dog, .43f); caster.health.AddHediff(DogFinal); TickGrant(caster);
        var ability = caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame); Check(ability != null, "real grant component failed"); ability.StartCooldown(4321);
        PetDogAbilityUtility.Maintain(caster); TickGrant(caster); caster.Training.SetSpecialization(SexSlaveSpecializationType.None); PetDogAbilityUtility.Maintain(caster);
        Check(ReferenceEquals(ability, caster.abilities.GetAbility(ability.def)) && ability.CooldownTicksRemaining == 4321, "grant/maintenance/reset direction discarded cooldown");
        var duplicate = caster.health.AddHediff(DogFinal); caster.health.RemoveHediff(duplicate); PetDogAbilityUtility.Maintain(caster);
        Check(ReferenceEquals(ability, caster.abilities.GetAbility(ability.def)) && ability.CooldownStartCalls == 1, "removing duplicate discarded ability");
    }
    private static void RemoveFinalLifecycle()
    {
        var p = Pair(); var otherDef = new AbilityDef { defName = "other" }; p.caster.abilities.GainAbility(otherDef); var other = p.caster.abilities.GetAbility(otherDef);
        p.caster.health.hediffSet.hediffs.Clear(); PetDogAbilityUtility.Maintain(p.caster);
        Check(p.caster.abilities.GetAbility(p.ability.def) == null && ReferenceEquals(other, p.caster.abilities.GetAbility(otherDef)), "maintenance removed unrelated ability or kept orphan");
    }
    private static void CooldownMigration()
    {
        var p = Pair(); Check(PetDogAbilityUtility.CaptureCooldownDeadline(p.caster) == 0, "ready deadline not zero");
        p.ability.StartCooldown(900); int deadline = PetDogAbilityUtility.CaptureCooldownDeadline(p.caster); Check(deadline == 100900, "deadline is duration");
        Find.TickManager.TicksGame += 300; var receiver = Pawn(); receiver.health.AddHediff(DogFinal); PetDogAbilityUtility.RestoreCooldown(receiver, deadline);
        var restored = receiver.abilities.GetAbility(SSCDefOf.SSC_PetDogTame); Check(restored != null && restored.CooldownTicksRemaining == 600, "stored time did not elapse");
        var instance = restored; Find.TickManager.TicksGame += 100; PetDogAbilityUtility.Maintain(receiver);
        Check(ReferenceEquals(instance, receiver.abilities.GetAbility(restored.def)) && restored.CooldownTicksRemaining == 500 && restored.CooldownStartCalls == 1, "maintenance restarted transferred cooldown");
        receiver.health.hediffSet.hediffs.Clear(); Check(PetDogAbilityUtility.CaptureCooldownDeadline(receiver) == 0, "orphan deadline captured");
    }
    private static void MaintenanceModes()
    {
        foreach (LoadSaveMode mode in Enum.GetValues<LoadSaveMode>())
        {
            Reset(); var caster = Pawn(); caster.health.AddHediff(DogFinal); Scribe.mode = mode; PetDogAbilityUtility.Maintain(caster);
            bool allowed = mode == LoadSaveMode.Inactive || mode == LoadSaveMode.PostLoadInit;
            Check((caster.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) != null) == allowed, "grant at wrong Scribe phase " + mode);
        }
        for (int scenario = 0; scenario < 4; scenario++)
        {
            Reset(); var p = Pair();
            if (scenario == 0) p.caster.Dead = true;
            if (scenario == 1) p.caster.Destroyed = true;
            if (scenario == 2) p.caster.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            if (scenario == 3) p.caster.health.hediffSet.GetFirstHediffOfDef(DogFinal).Severity = .009f;
            Scribe.mode = LoadSaveMode.LoadingVars; PetDogAbilityUtility.Maintain(p.caster);
            Check(ReferenceEquals(p.ability, p.caster.abilities.GetAbility(p.ability.def)), "loading prematurely removed saved skill");
            Scribe.mode = LoadSaveMode.PostLoadInit; PetDogAbilityUtility.Maintain(p.caster);
            Check(p.caster.abilities.GetAbility(p.ability.def) == null, "post-load left dead/hollow/invalid ability " + scenario);
        }
        Reset(); var hollow = Pair(); hollow.caster.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done); PetDogAbilityUtility.RestoreCooldown(hollow.caster, 100900);
        Check(hollow.caster.abilities.GetAbility(hollow.ability.def) == null, "hollow restore granted ability");
    }
    private static void RestoreWithoutFinal()
    {
        foreach (int deadline in new[] { 0, -5, 99999, 100000 })
        {
            Reset(); var p = Pair(); p.ability.StartCooldown(99); PetDogAbilityUtility.RestoreCooldown(p.caster, deadline);
            Check(p.ability.CooldownTicksRemaining == 0 && ReferenceEquals(p.ability, p.caster.abilities.GetAbility(p.ability.def)), "old/expired deadline not ready");
        }
        var bare = Pawn(); bare.abilities.GainAbility(SSCDefOf.SSC_PetDogTame); var otherDef = new AbilityDef { defName = "other" }; bare.abilities.GainAbility(otherDef);
        PetDogAbilityUtility.RestoreCooldown(bare, 100900);
        Check(bare.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) == null && bare.abilities.GetAbility(otherDef) != null, "without-final restore kept orphan/removed unrelated");
    }
    private static void DetachAfterExtraction()
    {
        foreach (bool currentDog in new[] { true, false })
        {
            Reset(); var p = Pair(); var comp = p.caster.Training;
            comp.RestoreSpecializationProgress(currentDog ? Dog : SexSlaveSpecializationType.Cow, currentDog ? .42f : .73f,
                new() { [Dog.ToString()] = .42f, [SexSlaveSpecializationType.Cow.ToString()] = .73f });
            p.caster.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Dog));
            var otherHediff = p.caster.health.AddHediff(new HediffDef { defName = "other" }); var otherDef = new AbilityDef { defName = "other" }; p.caster.abilities.GainAbility(otherDef);
            PetDogAbilityUtility.DetachAfterExtraction(p.caster); PetDogAbilityUtility.Maintain(p.caster);
            Check(!p.caster.health.hediffSet.HasHediff(DogFinal) && !p.caster.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(Dog)) && p.caster.abilities.GetAbility(p.ability.def) == null, "extracted body retained dog state/ability");
            Check(!comp.ExportSpecializationProgress().ContainsKey(Dog.ToString()) && Math.Abs(comp.ExportSpecializationProgress()[SexSlaveSpecializationType.Cow.ToString()] - .73f) < .00001f, "extraction altered wrong history");
            Check(comp.specializationType == (currentDog ? SexSlaveSpecializationType.None : SexSlaveSpecializationType.Cow) && p.caster.health.hediffSet.hediffs.Contains(otherHediff) && p.caster.abilities.GetAbility(otherDef) != null, "extraction altered other direction/state/ability");
        }
    }
    private static void MissingBoundaries()
    {
        Check(!PetDogAbilityUtility.CanUse(null, out _) && PetDogAbilityUtility.CaptureCooldownDeadline(null) == 0, "null accepted");
        PetDogAbilityUtility.Maintain(null); PetDogAbilityUtility.RemoveAbility(null); PetDogAbilityUtility.DetachAfterExtraction(null); PetDogAbilityUtility.RestoreCooldown(null, 100900);
        var pawn = Pawn(); pawn.health.AddHediff(DogFinal); pawn.abilities = null;
        PetDogAbilityUtility.Maintain(pawn); PetDogAbilityUtility.RestoreCooldown(pawn, 100900); PetDogAbilityUtility.RemoveAbility(pawn); Check(PetDogAbilityUtility.CaptureCooldownDeadline(pawn) == 0, "missing tracker captured");
        DefDatabase<HediffDef>.Definitions.Clear(); Check(!PetDogAbilityUtility.CanUse(pawn, out _), "missing final definition manufactured qualification");
    }
}
