// AI 辅助生成：第 3 批执行真实 RJW 部位、生产快照/成长计划、原生健康写入和 Scribe。
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using Verse.AI.Group;

internal static partial class Program
{
    private static readonly Type growthType = typeof(SSCUterusDevelopmentGrowthUtility);
    private static readonly Type partsType = typeof(SSCOrdinarySexPartsUtility);
    private static readonly Type snapshotType = typeof(SSCOrdinarySexPartsSnapshot);
    private static readonly Type guardType = growthType.Assembly.GetType("SexSlaveCraft.SSCRestrictionJobGuard");

    /// <summary>执行公开数值规则、真实接触捕获及原记录写入；流程专项另验证完成认领和三种出口。</summary>
    private static void GrowthTests(string repo, Harmony harmony)
    {
        GainCalculationTests();
        ReceivingEvidenceTests();
        GrowthWriteTests();
        GrowthFlowTests(repo, harmony);
    }

    /// <summary>对照规划的固定例子与异常输入，避免以另一份实现计算期望值。</summary>
    private static void GainCalculationTests()
    {
        foreach (var input in new[] { (25f, 1.5f, 1.3f, .024375f), (40f, 1.8f, 1.6f, .0576f),
            (50f, 2f, 2f, .10f), (.01f, 1f, 1f, .000005f), (10000f, 2f, 2f, .10f) })
            Check("batch3 fixed score/factors and cap: " + input, () =>
                Near(input.Item4, SSCUterusDevelopmentGrowthUtility.CalculateGain(input.Item1, input.Item2, input.Item3)));
        foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check("batch3 invalid score yields zero: " + value, () => Near(0, SSCUterusDevelopmentGrowthUtility.CalculateGain(value, 1, 1)));
            Check("batch3 invalid corruption factor yields zero: " + value, () => Near(0, SSCUterusDevelopmentGrowthUtility.CalculateGain(50, value, 1)));
            Check("batch3 invalid chain factor yields zero: " + value, () => Near(0, SSCUterusDevelopmentGrowthUtility.CalculateGain(50, 1, value)));
        }
    }

    /// <summary>实际 RJW 对象与原生身体：目标在正反方向都可以是接触 Item2.Owner。</summary>
    private sealed class GrowthScene
    {
        internal readonly Pawn Source = SurgeryPawn(PawnIdentity.Master), Target = SurgeryPawn();
        internal readonly Hediff_NaturalSexPart Organ;
        internal readonly SexProps Props;
        internal readonly ILewdablePart Penetrator, Vagina;
        internal readonly SSCOrdinarySexPartsSnapshot Snapshot;

        /// <summary>只提供已经解析的接触，不执行或模拟 RJW 的交互选择器。</summary>
        internal GrowthScene(bool reverse = false, bool contact = true, bool start = true)
        {
            Organ = new Hediff_NaturalSexPart { pawn = Target, Part = Target.RaceProps.body.corePart, def = vagina, loadID = ++nextId };
            Target.health.hediffSet.hediffs.Add(Organ);
            var interaction = new InteractionDef { defName = "UterusContact" + ++nextId,
                modExtensions = new() { new SexInteractionExtension { rjwSextype = "Vaginal", tags = new() } } };
            DefDatabase<InteractionDef>.Add(interaction);
            Props = new SexProps(reverse ? Target : Source, reverse ? Source : Target)
                { isRevese = reverse, dictionaryKey = interaction };
            AccessTools.Field(typeof(SexProps), "_sexType").SetValue(Props, (xxx.rjwSextype?)xxx.rjwSextype.Vaginal);
            Penetrator = new VanillaLewdablePart(Source, Source.RaceProps.body.corePart, LewdablePartFamily.Penis);
            Vagina = new RJWLewdablePart(Organ, LewdablePartFamily.Vagina);
            Props._resolved = new SexInteractionResolved { Initiator = Props.initiator, Recipient = Props.recipient,
                Interaction = new SexInteraction(interaction), InitiatorParts = new(), RecipientParts = new(), Penetrations = new() };
            (reverse ? Props._resolved.RecipientParts : Props._resolved.InitiatorParts).Add(Penetrator);
            (reverse ? Props._resolved.InitiatorParts : Props._resolved.RecipientParts).Add(Vagina);
            if (contact) Props._resolved.Penetrations.Add(Tuple.Create(Penetrator, Vagina));
            if (start) Start(Target);
            Snapshot = CaptureParts(Props);
        }
    }

    /// <summary>只用反射访问生产 internal 接口，不在测试中重复捕获或写入算法。</summary>
    private static object Hidden(Type type, object instance, string name, params object[] args)
    {
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length &&
                m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
        return method.Invoke(instance, args);
    }
    /// <summary>由生产缓存读取器捕获，不访问会触发重新解析的 resolved getter。</summary>
    private static SSCOrdinarySexPartsSnapshot CaptureParts(SexProps props)
        => (SSCOrdinarySexPartsSnapshot)Hidden(partsType, null, "Capture", props);
    /// <summary>返回真实快照条目，供场景测试检查保存字段和原器官引用。</summary>
    private static List<SSCOrdinarySexPartsSnapshot.Part> Entries(SSCOrdinarySexPartsSnapshot snapshot)
        => (List<SSCOrdinarySexPartsSnapshot.Part>)AccessTools.Field(snapshotType, "Parts").GetValue(snapshot);
    /// <summary>通过生产方向复查判断该条目是否仍能支持本项目成长。</summary>
    private static bool Receiving(SSCOrdinarySexPartsSnapshot snapshot, SexProps props, SSCOrdinarySexPartsSnapshot.Part entry)
        => (bool)Hidden(partsType, null, "IsCurrentReceivingVagina", snapshot, props, entry);
    /// <summary>取得真实成长计划，不自行选择器官或计算额度。</summary>
    private static object Plan(GrowthScene scene, float score = 50)
        => Hidden(growthType, null, "Capture", scene.Target, scene.Snapshot, scene.Props, score);
    /// <summary>在夹具提供的原场景有效性前提下执行真实计划。</summary>
    private static void Apply(object plan, Func<bool> current = null)
    {
        if (plan != null) Hidden(plan.GetType(), plan, "Apply", current ?? (() => true));
    }
    /// <summary>读取唯一当前身体记录，便于断言实际进度而不是只统计调用次数。</summary>
    private static float Progress(Pawn pawn) => SSCUterusDevelopmentUtility.Find(pawn)?.Severity ?? 0f;

    /// <summary>提供真实需求容器及其数值，不替代生产恶堕阶段倍率的读取。</summary>
    private static Need_Corruption GrowthCorruption(Pawn pawn, float level)
    {
        var need = Bare<Need_Corruption>();
        AccessTools.Field(typeof(Need), "pawn").SetValue(need, pawn);
        need.def = new NeedDef { defName = "GrowthCorruption", needClass = typeof(Need_Corruption) };
        need.CurLevel = level; pawn.needs.AllNeeds.Add(need); return need;
    }

    /// <summary>覆盖真实端点方向、参与列表合并、读档无缓存及运行中方向替换。</summary>
    private static void ReceivingEvidenceTests()
    {
        foreach (bool reverse in new[] { false, true })
            Check("batch3 Item2 owner receives growth in either initiator direction: " + reverse, () =>
            {
                var s = new GrowthScene(reverse); Equal(2, Entries(s.Snapshot).Count);
                Equal(1, Entries(s.Snapshot).Count(e => Receiving(s.Snapshot, s.Props, e)));
                Apply(Plan(s)); Near(.035f, Progress(s.Target)); Near(0, Progress(s.Source));
            });
        Check("batch3 vagina participates without penetration: no growth despite Vaginal sex type", () =>
        {
            var s = new GrowthScene(contact: false); Require(s.Snapshot != null); Require(Plan(s) == null); Near(.01f, Progress(s.Target));
        });
        Check("batch3 actual vaginal endpoint qualifies independent of sex type name", () =>
        {
            var s = new GrowthScene(); AccessTools.Field(typeof(SexProps), "_sexType").SetValue(s.Props, (xxx.rjwSextype?)xxx.rjwSextype.Anal);
            Apply(Plan(s)); Near(.035f, Progress(s.Target));
        });
        Check("batch3 direction reversal with identical participating organs rejects only new growth", () =>
        {
            var s = new GrowthScene(); var plan = Plan(s);
            s.Props._resolved.Penetrations[0] = Tuple.Create(s.Vagina, s.Penetrator);
            Require((bool)Hidden(snapshotType, s.Snapshot, "Matches", s.Props));
            Apply(plan); Near(.01f, Progress(s.Target));
        });
        foreach (string change in new[] { "organ removed", "organ replaced", "foreign owner", "new props", "new interaction", "empty contacts" })
            Check("batch3 original evidence invalidated before write: " + change, () =>
            {
                var s = new GrowthScene(); var plan = Plan(s);
                switch (change)
                {
                    case "organ removed": s.Target.health.hediffSet.hediffs.Remove(s.Organ); break;
                    case "organ replaced":
                        s.Target.health.hediffSet.hediffs.Remove(s.Organ);
                        s.Target.health.hediffSet.hediffs.Add(new Hediff_NaturalSexPart { pawn = s.Target, Part = s.Organ.Part, def = vagina }); break;
                    case "foreign owner": s.Organ.pawn = s.Source; break;
                    case "new props": Hidden(snapshotType, s.Snapshot, "Bind", new SexProps(s.Source, s.Target)); break;
                    case "new interaction": s.Props.dictionaryKey = new InteractionDef(); break;
                    case "empty contacts": s.Props._resolved.Penetrations.Clear(); break;
                }
                Apply(plan); Near(.01f, Progress(s.Target));
            });
        Check("batch3 saved original evidence works with absent cache and never resolves parts again", () =>
        {
            var s = new GrowthScene(); s.Props._resolved = null; Apply(Plan(s)); Near(.035f, Progress(s.Target)); Require(s.Props._resolved == null);
        });
        Check("batch3 missing start cache cannot be created at payout", () =>
        {
            var s = new GrowthScene(); var cached = s.Props._resolved; s.Props._resolved = null;
            Require(CaptureParts(s.Props) == null); s.Props._resolved = cached;
            Require(Hidden(growthType, null, "Capture", s.Target, null, s.Props, 50f) == null);
        });
        Check("batch3 multiple contacts and duplicate organs produce one amount for the body", () =>
        {
            var s = new GrowthScene(); s.Props._resolved.Penetrations.Add(Tuple.Create(s.Penetrator, s.Vagina));
            var snapshot = CaptureParts(s.Props); Equal(2, Entries(snapshot).Count);
            Apply(Hidden(growthType, null, "Capture", s.Target, snapshot, s.Props, 50f)); Near(.035f, Progress(s.Target));
        });
    }

    /// <summary>直接观察真实健康记录：不补建、不迁移，封顶只写原记录并保持满级默认禁育。</summary>
    private static void GrowthWriteTests()
    {
        Check("batch3 unoperated body never gains or auto creates development", () =>
        {
            var s = new GrowthScene(start: false); Require(Plan(s) == null); Near(0, Progress(s.Target));
        });
        Check("batch3 full old genital development cannot start or migrate uterus", () =>
        {
            var s = new GrowthScene(start: false);
            s.Target.health.hediffSet.hediffs.Add(new Hediff { pawn = s.Target, Part = s.Organ.Part,
                def = new HediffDef { defName = "SSC_Exp_Genitals", maxSeverity = 1 }, Severity = 1 });
            Require(Plan(s) == null); Near(0, Progress(s.Target));
        });
        Check("batch3 tiny gain is added exactly rather than reset to initial one percent", () =>
        {
            var s = new GrowthScene(); Apply(Plan(s, .01f)); Near(.010005f, Progress(s.Target));
        });
        Check("batch3 one award is capped at ten percentage points", () =>
        {
            var s = new GrowthScene(); Apply(Plan(s, 10000)); Near(.11f, Progress(s.Target));
        });
        Check("batch3 remaining progress caps completion and retains off fertility", () =>
        {
            var s = new GrowthScene(); var record = SSCUterusDevelopmentUtility.Find(s.Target); record.Severity = .99f;
            Apply(Plan(s, 10000)); Near(1, record.Severity); Require(!record.FertilityEnabled); Near(0, Factor(s.Target));
            Require(Plan(s) == null);
        });
        foreach (string change in new[] { "removed", "replaced", "full", "stale scene", "loading" })
            Check("batch3 plan cannot update invalid target after capture: " + change, () =>
            {
                var s = new GrowthScene(); var record = SSCUterusDevelopmentUtility.Find(s.Target); var plan = Plan(s);
                switch (change)
                {
                    case "removed": s.Target.health.RemoveHediff(record); break;
                    case "replaced": s.Target.health.RemoveHediff(record); Start(s.Target); break;
                    case "full": record.Severity = 1; break;
                    case "loading": Scribe.mode = LoadSaveMode.LoadingVars; break;
                }
                try { Apply(plan, () => change != "stale scene"); }
                finally { Scribe.mode = LoadSaveMode.Inactive; }
                Near(change == "removed" ? 0 : change == "full" ? 1 : .01f, Progress(s.Target));
            });
        Check("batch3 repeated independent scenes have no extra daily cap", () =>
        {
            var s = new GrowthScene();
            for (int i = 0; i < 3; i++) Apply(Plan(s, 10000));
            Near(.31f, Progress(s.Target));
        });
        Check("batch3 growth reads real corruption and chain multipliers from current body", () =>
        {
            var s = new GrowthScene(); GrowthCorruption(s.Target, .5f);
            var chain = new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff), maxSeverity = 1,
                stages = new() { new HediffStage { minSeverity = .1f }, new HediffStage { minSeverity = .3f },
                    new HediffStage { minSeverity = .5f }, new HediffStage { minSeverity = .9f } } };
            DefDatabase<HediffDef>.Add(chain);
            s.Target.health.hediffSet.hediffs.Add(new Hediff { pawn = s.Target, def = chain, Severity = .5f });
            Apply(Plan(s, 40)); Near(.0676f, Progress(s.Target));
        });
        Check("batch3 started body is not subjected to surgery identity or binding gates again", () =>
        {
            var s = new GrowthScene(); s.Target.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
            Apply(Plan(s)); Near(.035f, Progress(s.Target));
        });
    }
}
