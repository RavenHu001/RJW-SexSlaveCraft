using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static int passed, failed;
    private static string root;
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
        Run("旧枚举编号不变", EnumCompatibility);
        Run("选择要求 SSC 性奴及战斗员研究，兼容三种原版身份", SelectionMatrix);
        Run("缺失研究、组件或不支持的对象不会开放入口", InvalidSelection);
        Run("其他方向保留原身份与基础研究门槛", LegacySelection);
        Run("零进度反复对账不增长或重建状态", ZeroDoesNotGrow);
        Run("特化区仅向 SSC 性奴显示", SpecializationVisibility);
        Run("重复标记归一，组件覆盖陈旧严重度", DuplicateAndStaleStates);
        Run("普通培养完成不添加终极标记或能力", OrdinaryCompletion);
        Run("切换历史独立，当前状态立即更新", Switching);
        Run("明确留空不从残留状态认领", ExplicitNone);
        Run("损坏进度不会进入健康状态或历史", InvalidProgress);
        Run("保存字段重建后可恢复当前方向与历史", SaveRoundTrip);
        Run("明确留空字段重建后保留历史和其他终极成果", EmptySaveRoundTrip);
        Run("读档中不提前添加状态，完成后按组件恢复", LoadingBoundary);
        Run("显示区分普通完成和终极，未完成不舍入至百分百", CompletionDisplay);
        Run("阶段边界读取真实 XML，熟练收益不叠加", StageDefinitions);
        Run("研究成本、前置与四语资源完整", Resources);
        RunExperienceCases();
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }
    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception e) { failed++; Console.WriteLine("失败：" + name + "\n" + e); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Equal(float expected, float actual)
    {
        Check(float.IsFinite(actual) && Math.Abs(expected - actual) < 0.000001f,
            $"期望 {expected}，实际 {actual}");
    }
    private static Pawn Pawn(PawnIdentity identity = PawnIdentity.Slave)
    {
        var p = new Pawn();
        p.Training = new CompSexSlaveTraining { parent = p, pawnIdentity = identity };
        return p;
    }
    private static Hediff Ordinary(Pawn p) => p.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant);
    private static void Train(Pawn p, float progress)
    {
        p.Training.SetSpecialization(SexSlaveSpecializationType.Combatant);
        p.Training.specializationProgress = progress;
        CompSexSlaveTraining.ReconcileSpecialization(p);
    }
    private static void EnumCompatibility()
    {
        Check((int)SexSlaveSpecializationType.None == 0 && (int)SexSlaveSpecializationType.Bus == 1 &&
            (int)SexSlaveSpecializationType.Cow == 2 && (int)SexSlaveSpecializationType.PetCat == 3 &&
            (int)SexSlaveSpecializationType.PetDog == 4 && (int)SexSlaveSpecializationType.PetRabbit == 5 &&
            (int)SexSlaveSpecializationType.TrainerOfficer == 6 && (int)SexSlaveSpecializationType.Combatant == 7,
            "旧枚举值变化");
    }
    private static void SelectionMatrix()
    {
        foreach (var identity in Enum.GetValues<PawnIdentity>())
        foreach (int status in new[] { 0, 1, 2 })
        {
            var p = Pawn(identity);
            p.IsColonist = status == 0; p.IsPrisonerOfColony = status == 1; p.IsSlave = status == 2;
            SSCDefOf.SSC_BasicTraining.IsFinished = false;
            SSCDefOf.SSC_RES_Combatant.IsFinished = false;
            Check(!CombatantSpecializationUtility.CanSelect(p, out var reason) && !string.IsNullOrEmpty(reason), "研究前须锁定");
            SSCDefOf.SSC_RES_Combatant.IsFinished = true;
            bool eligible = identity == PawnIdentity.Slave;
            Check(CombatantSpecializationUtility.CanSelect(p, out reason) == eligible, "性奴身份门槛错误");
            Check(reason == (eligible ? null : Strings.ITab_SpecializationCombatantDisabledIdentity), "身份拒绝原因错误");
        }
    }
    private static void InvalidSelection()
    {
        Check(!CombatantSpecializationUtility.CanSelect(null, out _), "空对象");
        Check(!CombatantSpecializationUtility.CanSelect(new Pawn(), out _), "缺少组件");
        var p = Pawn(); p.IsColonist = false;
        Check(!CombatantSpecializationUtility.CanSelect(p, out _), "不支持对象");
        p.IsColonist = true;
        var def = SSCDefOf.SSC_RES_Combatant;
        try { SSCDefOf.SSC_RES_Combatant = null; Check(!CombatantSpecializationUtility.CanSelect(p, out _), "缺失研究"); }
        finally { SSCDefOf.SSC_RES_Combatant = def; }
    }
    private static void LegacySelection()
    {
        foreach (var identity in Enum.GetValues<PawnIdentity>())
        foreach (bool research in new[] { false, true })
        {
            SSCDefOf.SSC_BasicTraining.IsFinished = research;
            Check(ITab_SexSlaveTraining.LegacyOptions(Pawn(identity)) == (identity == PawnIdentity.Slave && research), "旧方向被放开");
        }
    }
    private static void ZeroDoesNotGrow()
    {
        var p = Pawn(); Train(p, 0);
        var original = Ordinary(p);
        for (int i = 0; i < 100; i++) CompSexSlaveTraining.ReconcileSpecialization(p);
        Equal(0, p.Training.specializationProgress); Equal(0.01f, Ordinary(p).Severity);
        Check(ReferenceEquals(original, Ordinary(p)) && p.health.hediffSet.hediffs.Count == 1, "重复重建状态");
    }
    private static void SpecializationVisibility()
    {
        Check(!ITab_SexSlaveTraining.SpecializationVisible(null), "空对象显示特化区");
        foreach (var identity in Enum.GetValues<PawnIdentity>())
        foreach (bool research in new[] { false, true })
        {
            SSCDefOf.SSC_RES_Combatant.IsFinished = research;
            Check(ITab_SexSlaveTraining.SpecializationVisible(Pawn(identity)) == (identity == PawnIdentity.Slave),
                "主人或未设定身份不应显示特化区，性奴研究前仍应显示锁定选项");
        }
    }
    private static void DuplicateAndStaleStates()
    {
        var p = Pawn(PawnIdentity.Slave); Train(p, 0.2f); var original = Ordinary(p);
        original.Severity = 0.9f;
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant).Severity = 1;
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Bus);
        CompSexSlaveTraining.ReconcileSpecialization(p);
        Equal(0.2f, original.Severity); Equal(0.2f, p.Training.specializationProgress);
        Check(p.health.hediffSet.hediffs.Count == 1 && ReferenceEquals(original, Ordinary(p)), "重复状态未修复");
    }
    private static void OrdinaryCompletion()
    {
        var p = Pawn(); Train(p, 1);
        Check(p.health.hediffSet.hediffs.Count == 1 && Ordinary(p) != null, "普通完成生成额外状态");
        var xml = XDocument.Load(Path.Combine(root, "Defs/HediffDefs/SSC_HediffDefs_CombatantSpecialization.xml"));
        Check(xml.Root.Elements().Count() == 1 && !xml.Descendants("comps").Any(), "普通定义包含额外状态或授予能力组件");
    }
    private static void Switching()
    {
        var p = Pawn(PawnIdentity.Slave); Train(p, 0.62f);
        var final = p.health.AddHediff(SSCDefOf.SSC_Hediff_Bus_Final);
        p.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
        Check(Ordinary(p) == null, "切走没有立即清理"); Equal(0, p.Training.specializationProgress);
        p.Training.specializationProgress = 0.3f;
        p.Training.SetSpecialization(SexSlaveSpecializationType.Combatant);
        Equal(0.62f, Ordinary(p).Severity); Equal(0.62f, p.Training.specializationProgress);
        Equal(0.3f, p.Training.ExportSpecializationProgress()["Cow"]);
        Check(p.health.hediffSet.hediffs.Contains(final), "丢失其他终极记录");
    }
    private static void ExplicitNone()
    {
        var p = Pawn(); Train(p, 0.7f);
        p.Training.SetSpecialization(SexSlaveSpecializationType.None);
        Check(Ordinary(p) == null, "留空未即时清理");
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant);
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Bus_Final);
        CompSexSlaveTraining.ReconcileSpecialization(p);
        Check(p.Training.specializationType == SexSlaveSpecializationType.None && Ordinary(p) == null, "留空被认领");
        Equal(0.7f, p.Training.ExportSpecializationProgress()["Combatant"]);
    }
    private static void InvalidProgress()
    {
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 2f })
        {
            var p = Pawn(); Train(p, value);
            float expected = value == 2 ? 1 : 0;
            Equal(expected, p.Training.specializationProgress); Equal(Math.Max(0.01f, expected), Ordinary(p).Severity);
            p.Training.specializationProgress = value;
            p.Training.SetSpecialization(SexSlaveSpecializationType.None);
            p.Training.SetSpecialization(SexSlaveSpecializationType.Combatant);
            Equal(expected, p.Training.specializationProgress);
        }
    }
    // 模拟序列化边界，保留生产字段；历史规范化、切换与健康对账均调用真实代码。
    private sealed class Saved
    {
        public SexSlaveSpecializationType Type { get; set; }
        public float Progress { get; set; }
        public bool Unset { get; set; }
        public Dictionary<string, float> History { get; set; }
    }
    private static Pawn Reload(Pawn p)
    {
        var json = JsonSerializer.Serialize(new Saved { Type = p.Training.specializationType,
            Progress = p.Training.specializationProgress, Unset = p.Training.specializationExplicitlyUnset,
            History = p.Training.ExportSpecializationProgress() });
        var data = JsonSerializer.Deserialize<Saved>(json);
        var loaded = Pawn(p.Training.pawnIdentity);
        loaded.Training.specializationType = data.Type;
        loaded.Training.specializationProgress = data.Progress;
        loaded.Training.specializationExplicitlyUnset = data.Unset;
        typeof(CompSexSlaveTraining).GetField("perTypeProgress", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(loaded.Training, data.History);
        loaded.Training.NormalizeSpecializationData();
        CompSexSlaveTraining.ReconcileSpecialization(loaded);
        return loaded;
    }
    private static void SaveRoundTrip()
    {
        var p = Pawn(PawnIdentity.Slave);
        p.Training.SetSpecialization(SexSlaveSpecializationType.Cow); p.Training.specializationProgress = 0.3f;
        Train(p, 0.62f); var loaded = Reload(p);
        Equal(0.62f, Ordinary(loaded).Severity);
        loaded.Training.SetSpecialization(SexSlaveSpecializationType.Cow); Equal(0.3f, loaded.Training.specializationProgress);
        loaded.Training.SetSpecialization(SexSlaveSpecializationType.Combatant); Equal(0.62f, Ordinary(loaded).Severity);
    }
    private static void EmptySaveRoundTrip()
    {
        var p = Pawn(); Train(p, 0.62f); p.Training.SetSpecialization(SexSlaveSpecializationType.None);
        var loaded = Reload(p);
        loaded.health.AddHediff(SSCDefOf.SSC_Hediff_Bus_Final);
        CompSexSlaveTraining.ReconcileSpecialization(loaded);
        Check(loaded.Training.specializationType == SexSlaveSpecializationType.None && Ordinary(loaded) == null, "留空读档后被覆盖");
        loaded.Training.SetSpecialization(SexSlaveSpecializationType.Combatant); Equal(0.62f, Ordinary(loaded).Severity);
    }
    private static void LoadingBoundary()
    {
        var p = Pawn(); Scribe.mode = LoadSaveMode.LoadingVars;
        p.Training.SetSpecialization(SexSlaveSpecializationType.Combatant);
        Check(Ordinary(p) == null, "读取字段时提前生成状态");
        p.Training.specializationProgress = 0.5f;
        Scribe.mode = LoadSaveMode.PostLoadInit;
        p.Training.NormalizeSpecializationData(); CombatantSpecializationUtility.Sync(p);
        Equal(0.5f, Ordinary(p).Severity);
    }
    private static void CompletionDisplay()
    {
        var p = Pawn(); Train(p, 0.99899f);
        Check(!ITab_SexSlaveTraining.Progress(p).Contains("100") && !ITab_SexSlaveTraining.Progress(p).Contains("complete"), "提前显示完成");
        foreach (float v in new[] { 0.999f, 1f })
        {
            Train(p, v); Check(ITab_SexSlaveTraining.Progress(p) == Strings.ITab_SpecializationOrdinaryComplete, "完成容差不一致");
            Check(ITab_SexSlaveTraining.Label(p) == Strings.ITab_SpecializationCombatant, "冒充终极");
        }
        p.Training.SetSpecialization(SexSlaveSpecializationType.None);
        Check(ITab_SexSlaveTraining.Label(p) == Strings.ITab_SpecializationNone, "留空名称错误");
    }
    private static float Value(XElement e, string path) => float.Parse(e.Element(path).Value, CultureInfo.InvariantCulture);
    private static void StageDefinitions()
    {
        var stages = XDocument.Load(Path.Combine(root, "Defs/HediffDefs/SSC_HediffDefs_CombatantSpecialization.xml"))
            .Descendants("stages").Single().Elements("li").ToArray();
        Check(stages.Length == 3, "普通阶段数量错误");
        foreach (var entry in new[] { (0f, 0), (0.19999f, 0), (0.2f, 1), (0.49999f, 1), (0.5f, 2), (0.999f, 2), (1f, 2) })
        {
            var p = Pawn(); Train(p, entry.Item1);
            var stage = stages.Last(s => Value(s, "minSeverity") <= Ordinary(p).Severity);
            Check(stage == stages[entry.Item2], "边界选择错误");
            if (entry.Item2 == 0) { Check(stage.Element("statOffsets") == null && stage.Element("statFactors") == null, "提前获得属性"); continue; }
            bool basic = entry.Item2 == 1;
            var offsets = stage.Element("statOffsets"); var factors = stage.Element("statFactors");
            Equal(basic ? 2 : 4, Value(offsets, "ShootingAccuracyPawn"));
            Equal(basic ? 2 : 4, Value(offsets, "MeleeHitChance"));
            Equal(basic ? -0.02f : -0.04f, Value(offsets, "MentalBreakThreshold"));
            Equal(basic ? 0.95f : 0.9f, Value(factors, "AimingDelayFactor"));
            Equal(basic ? 0.95f : 0.9f, Value(factors, "MeleeCooldownFactor"));
            Equal(basic ? 1f : 0.9f, Value(factors, "IncomingDamageFactor"));
            Check(offsets.Elements().Count() == 3 && factors.Elements().Count() == 3, "多余属性");
        }
    }
    private static void Resources()
    {
        var research = XDocument.Load(Path.Combine(root, "Defs/ResearchDefs/ResearchProjectDef.xml"))
            .Root.Elements().Single(e => (string)e.Element("defName") == "SSC_RES_Combatant");
        Equal(1200, Value(research, "baseCost"));
        Check(research.Element("prerequisites").Elements().Single().Value == "SSC_RES_SlaveSpecialization", "研究前置改变");
        foreach (var lang in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            var keyed = XDocument.Load(Path.Combine(root, "Languages", lang, "Keyed/SSC_CombatantSpecialization.xml"));
            Check(keyed.Root.Elements().Count() == 5 && keyed.Root.Elements().All(e => !string.IsNullOrWhiteSpace(e.Value)), "缺少界面翻译");
            if (lang == "ChineseSimplified") continue;
            var defs = XDocument.Load(Path.Combine(root, "Languages", lang, "DefInjected/HediffDef/SSC_HediffDefs_CombatantSpecialization.xml"));
            Check(defs.Root.Elements().Count() == 5, "缺少阶段翻译");
        }
    }
}
