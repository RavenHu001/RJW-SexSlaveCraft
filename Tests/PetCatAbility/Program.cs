using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static class Program
{
    private static int passed, failed;
    private static string root;
    private static readonly SexSlaveSpecializationType Cat = SexSlaveSpecializationType.PetCat;
    private static HediffDef CatFinal => PetSpecializationUtility.GetFinalHediffDef(Cat);
    private static HediffDef Buff => SSCDefOf.SSC_Hediff_PetCatEncouragement;

    /// <summary>运行真实资格、能力类、效果组件与授予组件；宿主只记录原版边界的进入和副作用。</summary>
    private static int Main(string[] args)
    {
        root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        Run("猫技能XML只有单一效果与一天冷却，6格视线和征召/非征召均可用", Definitions);
        Run("猫激励使用定时状态与工作倍率，猫终极挂真实能力授予组件", EffectDefinitions);
        Run("猫鼓舞心情为Hediff即时思绪，状态存续时提供8点且不另发长期记忆", MoodDefinition);
        Run("技能、鼓舞、思绪与拒绝原因的四语和源码镜像完整", Translations);
        Run("普通完成与狗兔终极不授予猫技能，猫终极达到有效严重度才可用", FinalQualification);
        Run("猫终极跨全部方向、身份和征召状态可用，不依赖绑定与研究", CrossDirectionQualification);
        Run("施放者死亡、销毁、倒地、离图、精神状态或空壳均拒绝", InvalidCasters);
        Run("目标必须为同图玩家阵营其他人形角色，范围与视线在运行时复查", TargetQualifications);
        Run("6格及自定义范围边界正确，损坏范围安全拒绝", RangeBoundaries);
        Run("正常目标需要心情，精神状态目标无心情或倒地仍可恢复", MentalMoodBoundary);
        Run("所有当前精神状态及来源直接恢复一次，不附带激励或删除疾病", MentalRecovery);
        Run("预热后正常目标进入精神状态时只恢复，原精神状态消失时只给激励", LateBranchChanges);
        Run("预热中精神状态被替换时只恢复当前实例，恢复后的新状态不二次处理", ReplacedMentalState);
        Run("效果组件Valid和CanApplyOn共用资格并保留原版Valid限制", ComponentValidation);
        Run("排队后资格、目标、距离、视线失效不进入PreActivate或消耗冷却", ExecutionRecheck);
        Run("两种效果共用完整一天冷却，生效组件不因PreActivate已扣冷却而失效", SharedCooldown);
        Run("激励刷新原实例与计时，多猫及重复标签不叠加状态", EncouragementRefresh);
        Run("配置损坏在激活前拒绝并给出拒绝原因", InvalidEffectConfiguration);
        Run("实际创建缺倒计时组件时清掉新状态，不留下永久激励", MissingRuntimeTimer);
        Run("直接调用效果入口同样复查资格，技能不改变当前培养或历史", DirectEffectAndExperienceIsolation);
        Run("真实授予组件仅授予一份，跨方向同步保持能力实例与冷却", GrantLifecycle);
        Run("移除重复终极保留能力及冷却，移除最后终极只撤销猫技能", RemoveFinalLifecycle);
        Run("冷却捕获保存绝对截止tick，旧空载荷与无终极角色返回就绪", CaptureDeadline);
        Run("恢复立即授予技能并扣除凝胶存放时间，过期或旧截止值就绪", RestoreDeadline);
        Run("无猫终极植入不保留孤儿猫能力，其他能力不受影响", RestoreWithoutFinal);
        Run("抽取后清源猫状态、技能与猫历史，保留其他方向及成果", DetachAfterExtraction);
        Run("缺失角色、能力容器与定义的迁移入口安全，不制造错误资格", MissingBoundaries);
        Console.WriteLine($"RESULT: {passed}/{passed + failed} cases passed.");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>每组重建最小定义与游戏tick，不让前一组冷却、标签或配置泄漏。</summary>
    private static void Run(string name, Action test)
    {
        Reset();
        try { test(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL: " + name + "\n" + error); }
    }
    private static void Reset()
    {
        Find.TickManager.TicksGame = 100000; Scribe.mode = LoadSaveMode.Inactive; Messages.Requests.Clear();
        DefDatabase<HediffDef>.Definitions.Clear(); DefDatabase<ResearchProjectDef>.Definitions.Clear();
        foreach (string pet in new[] { "PetCat", "PetDog", "PetRabbit" })
        {
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + pet });
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + pet + "_Final", initialSeverity = 1 });
            DefDatabase<ResearchProjectDef>.Add(new ResearchProjectDef { defName = "SSC_RES_" + pet, IsFinished = false });
        }
        SSCDefOf.SSC_BasicTraining.IsFinished = false;
        CatFinal.grantedAbility = SSCDefOf.SSC_PetCatComfort;
        Buff.hediffClass = typeof(HediffWithComps); Buff.disappearsAfterTicks = 30000;
        Buff.comps = new() { new HediffCompProperties_Disappears { disappearsAfterTicks = 30000 } };
        SSCDefOf.SSC_PetCatComfort.EffectFactory = ability => new CompAbilityEffect_PetCatComfort
        {
            parent = ability, props = new CompProperties_AbilityPetCatComfort { encouragementHediff = Buff, durationTicks = 30000 }
        };
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal(float expected, float actual) => Check(Math.Abs(expected - actual) < .00001f, $"expected {expected}, actual {actual}");
    private static Pawn Pawn(SexSlaveSpecializationType type = SexSlaveSpecializationType.None, float progress = 0)
    {
        var pawn = new Pawn { Training = new CompSexSlaveTraining { pawnIdentity = PawnIdentity.Slave } };
        pawn.Training.parent = pawn; pawn.Training.SetSpecialization(type); pawn.Training.specializationProgress = progress;
        return pawn;
    }
    private static (Pawn caster, Pawn target, Ability_PetCatComfort ability) Pair()
    {
        var caster = Pawn(Cat, .34f); caster.health.AddHediff(CatFinal); TickGrant(caster);
        var target = Pawn(SexSlaveSpecializationType.Cow, .27f); target.Map = caster.Map; target.Position = new IntVec3(2, 0);
        return (caster, target, (Ability_PetCatComfort)caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort));
    }
    private static CompAbilityEffect_PetCatComfort Effect(Ability ability) => (CompAbilityEffect_PetCatComfort)ability.EffectComps.Single();
    private static MentalState Mental(Pawn pawn)
    {
        var state = new MentalState { pawn = pawn }; pawn.MentalState = state; return state;
    }
    private static void TickGrant(Pawn pawn)
    {
        float adjustment = 0;
        foreach (Hediff h in pawn.health.hediffSet.hediffs.ToArray()) h.Grant?.CompPostTick(ref adjustment);
    }
    private static int BuffCount(Pawn target) => target.health.hediffSet.hediffs.Count(h => h.def == Buff);
    private static void Cast((Pawn caster, Pawn target, Ability_PetCatComfort ability) p)
    {
        Check(p.ability.Activate(p.target, default), "valid cast rejected");
        Check(p.ability.ActivationCalls == 1 && p.ability.CooldownTicksRemaining == 60000, "valid cast did not enter original cooldown once");
    }
    private static XElement Definition(string path, string name) => XDocument.Load(Path.Combine(root, path)).Root.Elements().Single(e => (string)e.Element("defName") == name);

    /// <summary>验证XML实际连接与单一组件，防止定义误挂两个分支或遗漏未征召入口。</summary>
    private static void Definitions()
    {
        var def = Definition("Defs/AbilityDefs/SSC_PetCatComfort.xml", "SSC_PetCatComfort");
        Check((string)def.Element("abilityClass") == typeof(Ability_PetCatComfort).FullName, "wrong ability class");
        Check((string)def.Element("cooldownTicksRange") == "60000", "wrong shared cooldown");
        foreach (string field in new[] { "targetRequired", "showWhenDrafted", "displayGizmoWhileUndrafted" }) Check((string)def.Element(field) == "true", "missing " + field);
        Check((string)def.Element("disableGizmoWhileUndrafted") == "false", "undrafted disabled");
        var verb = def.Element("verbProperties");
        Equal(6, float.Parse((string)verb.Element("range"))); Equal(2, float.Parse((string)verb.Element("warmupTime")));
        Check((string)verb.Element("requireLineOfSight") == "true", "no line of sight");
        Check((string)verb.Element("targetParams").Element("canTargetSelf") == "false", "self target enabled");
        var comp = def.Element("comps").Elements().Single();
        Check((string)comp.Attribute("Class") == typeof(CompProperties_AbilityPetCatComfort).FullName, "wrong sole effect component");
        Check((string)comp.Element("encouragementHediff") == Buff.defName && (string)comp.Element("durationTicks") == "30000", "effect configuration mismatch");
    }

    /// <summary>终极授予与临时状态计时分别连接，不把鼓舞注册为永久人格标签。</summary>
    private static void EffectDefinitions()
    {
        var final = Definition("Defs/HediffDefs/SSC_HediffDefs_PetSpecializations.xml", CatFinal.defName);
        Check((string)final.Element("hediffClass") == "HediffWithComps", "final cannot run grant component");
        var grant = final.Element("comps").Elements().Single();
        Check((string)grant.Attribute("Class") == typeof(HediffCompProperties_GiveAbility).FullName && (string)grant.Element("abilityDef") == SSCDefOf.SSC_PetCatComfort.defName, "grant mismatch");
        var buff = Definition("Defs/HediffDefs/SSC_HediffDefs_PetCatComfort.xml", Buff.defName);
        var timer = buff.Element("comps").Elements().Single();
        Check((string)timer.Attribute("Class") == "HediffCompProperties_Disappears" && (string)timer.Element("disappearsAfterTicks") == "30000", "wrong buff timer");
        Equal(1.10f, float.Parse((string)buff.Element("stages").Elements().Single().Element("statFactors").Element("WorkSpeedGlobal")));
    }

    /// <summary>心情效应绑定Hediff存在，而非独立长记忆，保证与工作倍率共用一个状态生命周期。</summary>
    private static void MoodDefinition()
    {
        var thought = Definition("Defs/ThoughtDefs/SSC_PetCatComfortThoughts.xml", "SSC_PetCatEncouragement_Mood");
        Check((string)thought.Element("workerClass") == "ThoughtWorker_Hediff" && (string)thought.Element("hediff") == Buff.defName, "mood not tied to encouragement state");
        Equal(8, float.Parse((string)thought.Element("stages").Elements().Single().Element("baseMoodEffect")));
        Check(thought.Element("thoughtClass") == null && thought.Element("durationDays") == null, "encouragement emits independent memory");
    }

    /// <summary>逐条核对资源键与镜像，不把译文存在检查当成原版心情或UI渲染测试。</summary>
    private static void Translations()
    {
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            var keyed = XDocument.Load(Path.Combine(root, "Languages", language, "Keyed/SSC_PetCatComfort.xml"));
            foreach (string key in new[] { "RequiresFinal", "InvalidCaster", "InvalidTarget", "MissingEffect" })
                Check(!string.IsNullOrWhiteSpace((string)keyed.Root.Element("SSC_PetCatComfort" + key)), "missing reason translation " + language + "/" + key);
            foreach (var resource in new[] {
                ("DefInjected/AbilityDef/SSC_PetCatComfort.xml", "SSC_PetCatComfort.label", "SSC_PetCatComfort.description"),
                ("DefInjected/HediffDef/SSC_PetCatComfort.xml", "SSC_Hediff_PetCatEncouragement.label", "SSC_Hediff_PetCatEncouragement.description"),
                ("DefInjected/ThoughtDef/SSC_PetCatComfortThoughts.xml", "SSC_PetCatEncouragement_Mood.stages.0.label", "SSC_PetCatEncouragement_Mood.stages.0.description") })
            {
                string path = Path.Combine("Languages", language, resource.Item1);
                var definition = XDocument.Load(Path.Combine(root, path));
                Check(!string.IsNullOrWhiteSpace((string)definition.Root.Element(resource.Item2)) && !string.IsNullOrWhiteSpace((string)definition.Root.Element(resource.Item3)), "missing definition translation " + path);
                var mirror = XDocument.Load(Path.Combine(root, "Sexslavecraft", path));
                Check(XNode.DeepEquals(definition.Root, mirror.Root), "source language mirror mismatch " + path);
            }
        }
    }

    /// <summary>普通完成和其他种终极都不能冒充猫成果；授予组件的严重度门槛与使用资格一致。</summary>
    private static void FinalQualification()
    {
        foreach (float progress in new[] { 0, .999f, 1 })
        {
            var ordinary = Pawn(Cat, progress); PetSpecializationUtility.SyncPetStates(ordinary); TickGrant(ordinary);
            Check(!PetCatAbilityUtility.CanUse(ordinary, out _) && ordinary.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null, "ordinary cat has ability");
        }
        foreach (var type in new[] { SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit })
        {
            var other = Pawn(); other.health.AddHediff(PetSpecializationUtility.GetFinalHediffDef(type));
            Check(!PetCatAbilityUtility.CanUse(other, out _), "other pet grants cat ability");
        }
        var caster = Pawn(); var final = caster.health.AddHediff(CatFinal);
        foreach (float severity in new[] { .001f, .01f, 1 })
        {
            final.Severity = severity; TickGrant(caster);
            Check(PetCatAbilityUtility.CanUse(caster, out _) == (severity >= .01f), "severity gate disagrees");
            Check((caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) != null) == (severity >= .01f), "grant threshold disagrees");
        }
    }

    /// <summary>终极能力读独立成果，不把当前训练方向、身份或主人绑定重新作为资格。</summary>
    private static void CrossDirectionQualification()
    {
        foreach (var direction in Enum.GetValues<SexSlaveSpecializationType>())
        foreach (var identity in Enum.GetValues<PawnIdentity>())
        foreach (bool drafted in new[] { false, true })
        {
            var caster = Pawn(direction, .13f); caster.health.AddHediff(CatFinal); caster.Training.pawnIdentity = identity; caster.Drafted = drafted;
            Check(PetCatAbilityUtility.CanUse(caster, out _) && caster.BoundMaster == null, "terminal qualification depends on training state");
            TickGrant(caster); Check(caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).CanCast, "ability imposed drafted gate");
        }
        var withoutComp = Pawn(); withoutComp.Training = null; withoutComp.health.AddHediff(CatFinal);
        Check(PetCatAbilityUtility.CanUse(withoutComp, out _), "final needs training comp");
    }

    /// <summary>无效施放者既不能选择目标，也不能绕过能力类直接启动原版冷却。</summary>
    private static void InvalidCasters()
    {
        for (int scenario = 0; scenario < 8; scenario++)
        {
            var p = Pair();
            if (scenario == 0) p.caster.Dead = true;
            if (scenario == 1) p.caster.Destroyed = true;
            if (scenario == 2) p.caster.Downed = true;
            if (scenario == 3) p.caster.Spawned = false;
            if (scenario == 4) Mental(p.caster);
            if (scenario == 5) p.caster.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            if (scenario == 6) p.caster.health = null;
            if (scenario == 7) p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
            Check(!PetCatAbilityUtility.CanUse(p.caster, out string reason) && !string.IsNullOrEmpty(reason), "invalid caster accepted " + scenario);
            Check(!p.ability.Activate(p.target, default) && p.ability.ActivationCalls == 0 && p.ability.CooldownTicksRemaining == 0, "invalid caster consumed cooldown");
        }
        Check(!PetCatAbilityUtility.CanUse(null, out _), "null caster accepted");
    }

    /// <summary>校验实际运行时目标，不依赖XML目标选择器即可拒绝越界对象。</summary>
    private static void TargetQualifications()
    {
        for (int scenario = 0; scenario < 11; scenario++)
        {
            var p = Pair(); Pawn target = p.target;
            if (scenario == 0) target = null;
            if (scenario == 1) target = p.caster;
            if (scenario == 2) target.Destroyed = true;
            if (scenario == 3) target.Dead = true;
            if (scenario == 4) target.Spawned = false;
            if (scenario == 5) target.health = null;
            if (scenario == 6) target.RaceProps.Humanlike = false;
            if (scenario == 7) target.Faction = new Faction();
            if (scenario == 8) target.Map = new Map();
            if (scenario == 9) target.Position = new IntVec3(7, 0);
            if (scenario == 10) p.caster.Map.LineOfSight = false;
            Check(!PetCatAbilityUtility.CanApply(p.caster, target, out _) && !Effect(p.ability).Valid(target), "invalid target accepted " + scenario);
            Check(!p.ability.Activate(target, default) && p.ability.ActivationCalls == 0 && p.ability.CooldownTicksRemaining == 0, "invalid target consumed cooldown");
        }
    }

    /// <summary>范围取当前技能verb值；NaN、无穷与负值不能造成无限范围或旁路。</summary>
    private static void RangeBoundaries()
    {
        var p = Pair(); p.target.Position = new IntVec3(6, 0);
        Check(PetCatAbilityUtility.CanApply(p.caster, p.target, out _), "six tiles rejected");
        p.target.Position = new IntVec3(6, 1); Check(!PetCatAbilityUtility.CanApply(p.caster, p.target, out _), "outside radius accepted");
        p.target.Position = new IntVec3(2, 0); p.ability.verb.verbProps.range = 1;
        Check(!Effect(p.ability).Valid(p.target), "component ignores custom range");
        foreach (float range in new[] { -1, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            Check(!PetCatAbilityUtility.CanApply(p.caster, p.target, out _, range), "bad range accepted");
    }

    /// <summary>有实际精神状态时不检查心情需求或目标倒地；无状态时心情是激励目标条件。</summary>
    private static void MentalMoodBoundary()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            var p = Pair();
            if (scenario == 0) p.target.needs = null;
            if (scenario == 1) p.target.needs.mood = null;
            if (scenario == 2) p.target.Downed = true;
            Check(PetCatAbilityUtility.CanApply(p.caster, p.target, out _) == (scenario == 2), "normal mood gate incorrect");
            var state = Mental(p.target); Cast(p);
            Check(state.RecoverCalls == 1 && BuffCount(p.target) == 0, "mental target needs mood or upright posture");
        }
    }

    private sealed class ThirdPartyMentalState : MentalState { }

    /// <summary>不模拟恢复副作用的具体游戏逻辑；记录调用当前实例，疾病标签始终原样保留。</summary>
    private static void MentalRecovery()
    {
        for (int source = 0; source < 4; source++)
        {
            var p = Pair();
            var state = new ThirdPartyMentalState { pawn = p.target, causedByMood = source == 0, causedByPsycast = source == 1, causedByDamage = source == 2 };
            p.target.MentalState = state;
            var disease = p.target.health.AddHediff(new HediffDef { defName = "CatatonicBreakdown" });
            Cast(p);
            Check(state.RecoverCalls == 1 && p.target.MentalState == null && BuffCount(p.target) == 0, "mental branch did not recover only once");
            Check(p.target.health.hediffSet.hediffs.Contains(disease), "ability deleted catatonic disease");
        }
        var normal = Pair(); var normalDisease = normal.target.health.AddHediff(new HediffDef { defName = "CatatonicBreakdown" });
        Cast(normal); Check(BuffCount(normal.target) == 1 && normal.target.health.hediffSet.hediffs.Contains(normalDisease), "normal branch interprets disease as mental state");
    }

    /// <summary>展示与预热时的Valid不缓存分支，实际生效才决定唯一结果。</summary>
    private static void LateBranchChanges()
    {
        var normal = Pair(); Check(Effect(normal.ability).Valid(normal.target), "normal initially invalid");
        var started = Mental(normal.target); Cast(normal); Check(started.RecoverCalls == 1 && BuffCount(normal.target) == 0, "cached normal branch used");
        var mental = Pair(); var ended = Mental(mental.target); Check(Effect(mental.ability).Valid(mental.target), "mental initially invalid");
        mental.target.MentalState = null; Cast(mental); Check(ended.RecoverCalls == 0 && BuffCount(mental.target) == 1, "cached mental branch used");
    }

    /// <summary>精神状态实例在等待中被替换时只操作最新实例；Recover后的变化不串入第二个分支。</summary>
    private static void ReplacedMentalState()
    {
        var p = Pair(); var previous = Mental(p.target); Check(Effect(p.ability).Valid(p.target), "initial mental target invalid");
        var current = Mental(p.target); var next = new MentalState { pawn = p.target };
        current.OnRecover = () => p.target.MentalState = next;
        Cast(p);
        Check(previous.RecoverCalls == 0 && current.RecoverCalls == 1 && next.RecoverCalls == 0 && p.target.MentalState == next,
            "effect recovered wrong or repeated mental state");
        Check(BuffCount(p.target) == 0, "recovery fell through to encouragement");
    }

    /// <summary>生产效果组件共用真实CanApply并保留原版Valid返回值，拒绝文本仅按调用选项输出。</summary>
    private static void ComponentValidation()
    {
        var p = Pair(); var effect = Effect(p.ability);
        Check(effect.Valid(p.target) && effect.CanApplyOn(p.target, default), "valid target refused");
        effect.BaseAllowed = false; Check(!effect.Valid(p.target) && !effect.CanApplyOn(p.target, default), "original Valid bypassed");
        effect.BaseAllowed = true; p.target.Faction = null;
        Check(!effect.Valid(p.target) && Messages.Requests.Count == 0, "silent Valid emitted message");
        Check(!effect.Valid(p.target, true) && Messages.Requests.Count == 1, "rejection message missing");
    }

    /// <summary>选择目标后再改变条件，断言拒绝发生在原版预扣冷却之前，不只是效果Apply返回。</summary>
    private static void ExecutionRecheck()
    {
        for (int scenario = 0; scenario < 10; scenario++)
        {
            var p = Pair(); Check(p.ability.CanCast && Effect(p.ability).Valid(p.target), "initial cast invalid");
            if (scenario == 0) p.ability.BaseAllowed = false;
            if (scenario == 1) p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
            if (scenario == 2) p.caster.Downed = true;
            if (scenario == 3) p.caster.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            if (scenario == 4) p.target.Dead = true;
            if (scenario == 5) p.target.Position = new IntVec3(7, 0);
            if (scenario == 6) p.caster.Map.LineOfSight = false;
            if (scenario == 7) p.target.Faction = new Faction();
            if (scenario == 8) p.target.needs.mood = null;
            if (scenario == 9) p.target.Map = new Map();
            Check(!p.ability.Activate(p.target, default), "late invalid cast accepted " + scenario);
            Check(p.ability.ActivationCalls == 0 && p.ability.CooldownStartCalls == 0 && BuffCount(p.target) == 0, "late invalid cast consumed cooldown or applied effect");
        }
    }

    /// <summary>先发激励后切换精神状态，同一实例冷却阻止第二个分支提前使用，期满只恢复。</summary>
    private static void SharedCooldown()
    {
        var p = Pair(); Cast(p); Check(BuffCount(p.target) == 1, "normal effect rejected after cooldown was claimed");
        var mental = Mental(p.target); Find.TickManager.TicksGame += 59999;
        Check(!p.ability.Activate(p.target, default) && mental.RecoverCalls == 0, "mental branch bypassed normal branch cooldown");
        Find.TickManager.TicksGame++; Check(p.ability.Activate(p.target, default) && mental.RecoverCalls == 1, "cooldown boundary did not recover");
        Check(p.ability.ActivationCalls == 2 && p.ability.CooldownTicksRemaining == 60000 && BuffCount(p.target) == 1, "branches have inconsistent cooldown or duplicate buff");
    }

    /// <summary>第二只猫只刷新目标同一份激励；外部重复状态清理不删除保留实例。</summary>
    private static void EncouragementRefresh()
    {
        var first = Pair(); Cast(first); var original = first.target.health.hediffSet.GetFirstHediffOfDef(Buff);
        original.Timer.ticksToDisappear = 123;
        var duplicate = first.target.health.AddHediff(Buff);
        var second = Pair(); second.caster.Map = first.target.Map;
        Check(second.ability.Activate(first.target, default), "second cat cannot refresh");
        Check(BuffCount(first.target) == 1 && ReferenceEquals(original, first.target.health.hediffSet.GetFirstHediffOfDef(Buff)) && original.Timer.ticksToDisappear == 30000,
            "refresh stacked state or replaced original");
        Check(!first.target.health.hediffSet.hediffs.Contains(duplicate), "duplicate encouragement remains");
    }

    /// <summary>配置缺HediffWithComps、消失组件或合法时长时，实际技能不能空耗冷却。</summary>
    private static void InvalidEffectConfiguration()
    {
        for (int scenario = 0; scenario < 6; scenario++)
        {
            Reset(); var p = Pair(); var props = Effect(p.ability).Props;
            if (scenario == 0) props.encouragementHediff = null;
            if (scenario == 1) props.encouragementHediff.hediffClass = null;
            if (scenario == 2) props.encouragementHediff.hediffClass = typeof(Hediff);
            if (scenario == 3) props.encouragementHediff.comps = null;
            if (scenario == 4) props.encouragementHediff.comps.Clear();
            if (scenario == 5) props.durationTicks = 0;
            Check(!Effect(p.ability).Valid(p.target, true) && Messages.Requests.Count == 1, "invalid effect configuration not rejected");
            Check(!p.ability.Activate(p.target, default) && p.ability.ActivationCalls == 0, "invalid configuration consumed cooldown");
        }
    }

    /// <summary>模拟配置通过但实际Hediff实例被外部宿主损坏的边界，不声称覆盖游戏实例化系统。</summary>
    private static void MissingRuntimeTimer()
    {
        var p = Pair(); Buff.disappearsAfterTicks = 0;
        Check(PetCatAbilityUtility.IsEffectConfigured(Buff, 30000), "test must keep valid metadata");
        Check(!PetCatAbilityUtility.Apply(p.caster, p.target, Buff, 30000) && BuffCount(p.target) == 0, "failed new state left permanent encouragement");
    }

    /// <summary>外部直接调用Apply也受资格保护；成功技能没有任何训练XP或亲昵冷却副作用。</summary>
    private static void DirectEffectAndExperienceIsolation()
    {
        var p = Pair(); p.caster.Training.SetSpecialization(SexSlaveSpecializationType.Cow); p.caster.Training.specializationProgress = .17f;
        var history = p.caster.Training.ExportSpecializationProgress(); var targetHistory = p.target.Training.ExportSpecializationProgress();
        Effect(p.ability).Apply(p.target, default);
        Check(BuffCount(p.target) == 1 && Effect(p.ability).BaseApplyCalls == 1, "direct effect failed");
        Check(history.OrderBy(e => e.Key).SequenceEqual(p.caster.Training.ExportSpecializationProgress().OrderBy(e => e.Key)) &&
            targetHistory.OrderBy(e => e.Key).SequenceEqual(p.target.Training.ExportSpecializationProgress().OrderBy(e => e.Key)), "ability changed specialization data");
        Check(p.caster.Training.lastPetAffectionTick == -999999 && p.ability.CooldownTicksRemaining == 0, "effect owns training or ability scheduling cooldown");
        p.target.Faction = new Faction(); Effect(p.ability).Apply(p.target, default);
        Check(Effect(p.ability).BaseApplyCalls == 1 && BuffCount(p.target) == 1, "direct invalid Apply had effects");
    }

    /// <summary>授予维护始终运行真实Hediff组件；方向切换与反复对账不得删除或重建已有能力。</summary>
    private static void GrantLifecycle()
    {
        var p = Pair(); p.ability.StartCooldown(7654);
        foreach (var direction in new[] { Cat, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.Combatant, SexSlaveSpecializationType.None })
        {
            p.caster.Training.SetSpecialization(direction);
            for (int repeat = 0; repeat < 3; repeat++) { CompSexSlaveTraining.ReconcileSpecialization(p.caster); TickGrant(p.caster); }
            Check(ReferenceEquals(p.ability, p.caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort)) && p.ability.CooldownTicksRemaining == 7654, "grant maintenance reset ability");
        }
    }

    /// <summary>共用授予组件识别其他有效同种成果；最后授予源移除后只撤销指定能力。</summary>
    private static void RemoveFinalLifecycle()
    {
        var p = Pair(); p.ability.StartCooldown(4321); var duplicate = p.caster.health.AddHediff(CatFinal);
        var otherDef = new AbilityDef(); p.caster.abilities.GainAbility(otherDef);
        p.caster.health.RemoveHediff(duplicate); TickGrant(p.caster);
        Check(ReferenceEquals(p.ability, p.caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort)) && p.ability.CooldownTicksRemaining == 4321, "duplicate removal reset ability");
        p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
        Check(p.caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null && p.caster.abilities.GetAbility(otherDef) != null, "last final removal scope incorrect");
    }

    /// <summary>读取真实Ability剩余冷却转换为绝对tick，只有猫终极拥有有效人格冷却载荷。</summary>
    private static void CaptureDeadline()
    {
        var p = Pair(); Check(PetCatAbilityUtility.CaptureCooldownDeadline(p.caster) == 0, "ready caster has deadline");
        p.ability.StartCooldown(7000); int deadline = PetCatAbilityUtility.CaptureCooldownDeadline(p.caster);
        Check(deadline == 107000, "capture did not use absolute tick");
        Find.TickManager.TicksGame += 321; Check(PetCatAbilityUtility.CaptureCooldownDeadline(p.caster) == deadline, "capture froze remaining duration");
        Check(PetCatAbilityUtility.CaptureCooldownDeadline(null) == 0 && PetCatAbilityUtility.CaptureCooldownDeadline(Pawn()) == 0, "empty source not ready");
        p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
        Check(PetCatAbilityUtility.CaptureCooldownDeadline(p.caster) == 0, "orphan cooldown captured without final");
    }

    /// <summary>恢复函数立即授予新实例并沿用绝对截止时间，重复恢复不会冻结冷却。</summary>
    private static void RestoreDeadline()
    {
        foreach (int deadline in new[] { 0, -1, 99000, 100000, 107000 })
        {
            var host = Pawn(SexSlaveSpecializationType.None); host.health.AddHediff(CatFinal);
            PetCatAbilityUtility.RestoreCooldown(host, deadline);
            var ability = host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort);
            Check(ability != null && ability.CooldownTicksRemaining == Math.Max(0, deadline - 100000), "restore missing ability or wrong remaining time");
            if (deadline > 100000)
            {
                Find.TickManager.TicksGame += 234; PetCatAbilityUtility.RestoreCooldown(host, deadline);
                Check(ReferenceEquals(ability, host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort)) && ability.CooldownTicksRemaining == deadline - Find.TickManager.TicksGame, "repeat restore rebuilt or extended cooldown");
                Find.TickManager.TicksGame = 100000;
            }
        }
        var expired = Pair(); expired.ability.StartCooldown(1000); PetCatAbilityUtility.RestoreCooldown(expired.caster, 0);
        Check(expired.ability.CooldownTicksRemaining == 0, "old gel kept host cooldown");
    }

    /// <summary>无终极的人格不能从宿主继承旧猫技能，即使孤儿能力仍在Tracker中。</summary>
    private static void RestoreWithoutFinal()
    {
        var host = Pawn(); host.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort); var other = new AbilityDef(); host.abilities.GainAbility(other);
        PetCatAbilityUtility.RestoreCooldown(host, 120000);
        Check(host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null && host.abilities.GetAbility(other) != null, "empty restore retained orphan or removed other ability");
    }

    /// <summary>生产Detach在快照之后调用；这里只验证其数据清理，不模拟人格生成与存档。</summary>
    private static void DetachAfterExtraction()
    {
        foreach (var current in new[] { Cat, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.None })
        {
            var caster = Pawn(Cat, .65f); caster.Training.SetSpecialization(SexSlaveSpecializationType.PetDog); caster.Training.specializationProgress = .29f;
            caster.Training.SetSpecialization(current); caster.Training.specializationProgress = current == Cat ? .65f : .13f;
            caster.health.AddHediff(CatFinal); caster.health.AddHediff(CatFinal); caster.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Cat));
            var unrelated = caster.health.AddHediff(SSCDefOf.SSC_Hediff_Cow_Final); TickGrant(caster);
            var other = new AbilityDef(); caster.abilities.GainAbility(other);
            PetCatAbilityUtility.DetachAfterExtraction(caster); TickGrant(caster); CompSexSlaveTraining.ReconcileSpecialization(caster);
            Check(!PetSpecializationUtility.HasAnyPetState(caster, Cat) && caster.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null, "source cat state or ability retained");
            Check(!caster.Training.ExportSpecializationProgress().ContainsKey(Cat.ToString()) && caster.Training.ExportSpecializationProgress().ContainsKey(SexSlaveSpecializationType.PetDog.ToString()), "cat history retained or unrelated history erased");
            Check(caster.health.hediffSet.hediffs.Contains(unrelated) && caster.abilities.GetAbility(other) != null, "detach erased other effects");
            Check(caster.Training.specializationType == (current == Cat ? SexSlaveSpecializationType.None : current), "detach changed unrelated direction");
        }
    }

    /// <summary>迁移辅助缺失边界不触发授予；没有定义时标签事实不能变成合法技能。</summary>
    private static void MissingBoundaries()
    {
        PetCatAbilityUtility.DetachAfterExtraction(null); PetCatAbilityUtility.RemoveAbility(null); PetCatAbilityUtility.RestoreCooldown(null, 107000);
        Check(!PetCatAbilityUtility.Apply(null, null, Buff, 30000), "null direct effect accepted");
        var p = Pair(); p.caster.abilities = null;
        Check(PetCatAbilityUtility.CaptureCooldownDeadline(p.caster) == 0, "missing tracker captured cooldown");
        PetCatAbilityUtility.RestoreCooldown(p.caster, 107000);
        DefDatabase<HediffDef>.Definitions.Remove(CatFinal.defName);
        Check(!PetCatAbilityUtility.CanUse(p.caster, out _), "missing final definition grants skill");
    }
}
