// AI 辅助生成
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static int passed, failed;
    private static string repo;
    private static readonly string[] Languages = { "ChineseSimplified", "ChineseTraditional", "English", "Russian" };

    /// <summary>运行真实生产面板及开关写入，四语文本和阶段阈值均从仓库读取。</summary>
    private static int Main(string[] args)
    {
        repo = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        var def = XDocument.Load(SscTestPaths.Combine(repo, "Defs/HediffDefs/SSC_MasterProgression.xml")).Root.Element("HediffDef");
        SSCDefOf.SSC_Hediff_MasterProgression.maxSeverity = float.Parse(def.Element("maxSeverity").Value, System.Globalization.CultureInfo.InvariantCulture);
        SSCDefOf.SSC_Hediff_MasterProgression.stages = def.Element("stages").Elements("li")
            .Select(e => new HediffStage { minSeverity = float.Parse(e.Element("minSeverity").Value, System.Globalization.CultureInfo.InvariantCulture) }).ToList();
        foreach (string language in Languages)
        {
            LoadLanguage(language);
            foreach (float experience in new float[] { 0, 11, 12, 16, 39, 40, 99, 100 })
                Run($"{language}: XP {experience} 正常与窄栏区域容纳", () => Layout(experience));
            Run($"{language}: 无记录不补建", MissingRecord);
            Run($"{language}: 空壳保存阶段但停用加成", Hollow);
            Run($"{language}: 四语键/参数与镜像", () => Localization(language));
            Run($"{language}: 阶段短句与原版悬停组合", StageDescriptions);
            Run($"{language}: 升阶消息名称参数与定位", StageFeedback);
        }
        LoadLanguage("ChineseSimplified");
        RunFeedbackTests();
        Run("阶段 2 经验 16 使用 4/28 区间", () => Assert(Near(Tab().Read(Grown(16)).Fraction, 4f / 28f)));
        Run("最高阶段进度为 1且不显示下一阶段", () => { var view = Tab().Read(Grown(100)); Assert(view.Fraction == 1 && view.Progress == Key("MaxStage")); });
        Run("零经验是有效基础阶段", () => Assert(Tab().Read(Grown(0)).Experience != null));
        Run("只读不会迁移旧版本或改写成长", ReadOnly);
        Run("原版详细悬停延迟生成，记录移除后不读取旧数据", LazyTooltip);
        Run("非主人保存的成长以暂停状态查询", Inactive);
        Run("有缰绳无成长仍可修改原开关", () => Toggle(new Pawn()));
        Run("重复关闭和开启读写同一缰绳", () => Toggle(Grown(40)));
        Run("没有承伤候选仍可保存偏好", NoCandidates);
        Run("没有缰绳置灰且不创建关系", MissingBridle);
        Run("非玩家角色只读", NonPlayer);
        Run("外层 GUI 停用时不响应", DisabledGUI);
        Run("迟到点击不修改已切换角色", () => Late((tab, pawn, old) => tab.SelPawn = new Pawn()));
        Run("迟到点击不修改替换后的组件", () => Late((tab, pawn, old) => pawn.Training = new CompSexSlaveTraining()));
        Run("迟到点击不修改退出主人角色", () => Late((tab, pawn, old) => pawn.Training.pawnIdentity = PawnIdentity.Unset));
        Run("迟到点击不修改阵营变化角色", () => Late((tab, pawn, old) => pawn.Faction = new Faction()));
        Run("迟到点击不修改已解绑缰绳", () => Late((tab, pawn, old) => pawn.health.RemoveHediff(old)));
        Run("迟到点击不修改新缰绳", () => Late((tab, pawn, old) => { pawn.health.RemoveHediff(old); Bind(pawn); }));
        Run("绘制到提交之间解绑不会写旧实例", DuringDraw);
        Run("正常绘制恢复字号/颜色/对齐/换行/启用状态", () => GuiState(false));
        Run("异常绘制同样恢复全局状态", () => GuiState(true));
        Run("原伤害分担 Gizmo 移除但保留基类", Gizmos);
        Run("原缰绳字段存读档保留关闭值", BridleRoundTrip);
        Run("旧档缺开关字段仍默认开启", OldSaveDefault);
        Run("主文件接入同一面板高度且保留未选择分支", Integration);
        RunAbilityToggleTests();
        RunFrameworkTests();
        RunRomanceUiTests();
        Console.WriteLine($"{passed}/{passed + failed} passed (production Master panel/bridle; approximate game interface model).");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>每项隔离鼠标和 GUI 模型，避免前一项影响新场景。</summary>
    private static void Run(string name, Action test)
    {
        Widgets.Click = Widgets.Throw = false; Widgets.BeforeCheckbox = null;
        Widgets.TargetClick = -1; Widgets.BeforeTargetButton = null;
        Widgets.RomanceClick = -1; Widgets.RomanceButtonIndex = 0; Widgets.RomanceButtonClick = -1; Widgets.BeforeRomanceButton = null;
        SSCRomanceCommandMenu.Opened.Clear();
        GUI.enabled = true; GUI.color = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
        Scribe.mode = LoadSaveMode.Inactive; Scribe_Values.Data.Clear();
        Messages.Received.Clear();
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Assert(bool condition, string reason = "Assertion failed") { if (!condition) throw new Exception(reason); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
    private static ITab_SexSlaveTraining Tab() => new ITab_SexSlaveTraining();
    private static string Key(string suffix) => ("SSC_MasterProgression_" + suffix).Translate();
    private static Pawn Grown(float experience)
    {
        var pawn = new Pawn(); Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, experience)); return pawn;
    }
    private static Hediff_BridleOfSexSlave Bind(Pawn pawn)
    {
        // 主人面板必须使用有反向锁链归属的场景，避免用孤立缰绳条目冒充真实绑定。
        var slave = new Pawn { BoundMaster = pawn };
        return Hediff_BridleOfSexSlave.AddToPawn(pawn, slave);
    }

    /// <summary>四语标签来自真实注入文件；没有人工复制一套面板文本。</summary>
    private static void LoadLanguage(string language)
    {
        Translation.Entries.Clear();
        foreach (string name in new[] { "SSC_MasterProgression.xml", "SSC_Keys.xml", "SSC_AbilityToggles.xml" })
            foreach (var node in XDocument.Load(SscTestPaths.Combine(repo, "Languages", language, "Keyed", name)).Root.Elements()) Translation.Entries[node.Name.LocalName] = node.Value;
        var injected = XDocument.Load(SscTestPaths.Combine(repo, "Languages", language, "DefInjected/HediffDef/SSC_MasterProgression.xml")).Root;
        var definition = SSCDefOf.SSC_Hediff_MasterProgression;
        definition.label = injected.Element("SSC_Hediff_MasterProgression.label").Value;
        definition.description = injected.Element("SSC_Hediff_MasterProgression.description").Value;
        definition.extraTooltip = injected.Element("SSC_Hediff_MasterProgression.extraTooltip")?.Value;
        for (int i = 0; i < SSCDefOf.SSC_Hediff_MasterProgression.stages.Count; i++)
        {
            SSCDefOf.SSC_Hediff_MasterProgression.stages[i].label = injected.Element($"SSC_Hediff_MasterProgression.stages.{i}.label").Value;
            definition.stages[i].extraTooltip = injected.Element($"SSC_Hediff_MasterProgression.stages.{i}.extraTooltip").Value;
        }
    }

    /// <summary>执行真实面板绘制，检查文本行容纳、区块底部及重复查询不改变健康数据。</summary>
    private static void Layout(float experience)
    {
        Pawn pawn = Grown(experience); Bind(pawn); var tab = Tab();
        int adds = pawn.health.Adds, notifications = pawn.health.Notifications;
        foreach (float width in new float[] { 322, 200, 120 })
        {
            float height = tab.Draw(pawn, width);
            var rows = Widgets.Items.Where(i => i.Kind is "label" or "bar" or "checkbox").ToList();
            Assert(rows.All(i => i.Rect.y >= 12 && i.Rect.yMax <= height - 12 + .001f), "Rows exceed measured section");
            for (int i = 1; i < rows.Count; i++) Assert(rows[i].Rect.y >= rows[i - 1].Rect.yMax, "Overlapping rows");
            foreach (var row in rows.Where(i => i.Kind is "label" or "checkbox"))
                Assert(Text.CalcHeight(row.Text, row.Rect.width - (row.Kind == "checkbox" ? 24 : 0)) <= row.Rect.height, "Text exceeds allocated height");
            Assert(rows.All(i => !i.Text.StartsWith("SSC_", StringComparison.Ordinal)), "Untranslated UI key");
            Assert(TooltipHandler.Getters.Any(getter => getter() == SSCMasterProgressionUtility.GetRecord(pawn).GetTooltip(pawn, false)), "Native tooltip not reused");
        }
        Assert(pawn.health.Adds == adds && pawn.health.Notifications == notifications && SSCMasterProgressionUtility.GetExperience(pawn) == experience);
    }
    private static void MissingRecord()
    {
        Pawn pawn = new Pawn(); pawn.Training.masterProgressionVersion = 0;
        var tab = Tab(); var view = tab.Read(pawn); tab.Draw(pawn);
        Assert(view.Stage == Key("NoRecord") && view.Experience == null && view.Benefits == null && !Widgets.Items.Any(i => i.Kind == "bar"));
        Assert(pawn.health.Adds == 0 && pawn.health.Notifications == 0 && pawn.Training.masterProgressionVersion == 0);
    }
    private static void Hollow()
    {
        Pawn pawn = Grown(40); pawn.health.AddHediff(new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done });
        var view = Tab().Read(pawn); Tab().Draw(pawn);
        Assert(view.Stage == SSCMasterProgressionUtility.GetRecord(pawn).LabelInBrackets && view.Benefits == Key("BenefitsInactive"));
        Assert(!SSCMasterProgressionUtility.GetRecord(pawn).Visible);
        Assert(SSCMasterProgressionUtility.GetExperience(pawn) == 40);
    }
    private static void ReadOnly()
    {
        Pawn pawn = Grown(16); pawn.Training.masterProgressionVersion = 0;
        int adds = pawn.health.Adds, removes = pawn.health.Removes, notify = pawn.health.Notifications;
        for (int i = 0; i < 5; i++) Tab().Draw(pawn);
        Assert(pawn.health.Adds == adds && pawn.health.Removes == removes && pawn.health.Notifications == notify && pawn.Training.masterProgressionVersion == 0);
    }
    private static void Inactive()
    {
        Pawn pawn = Grown(40); pawn.Training.pawnIdentity = PawnIdentity.Slave;
        var view = Tab().Read(pawn); Assert(!view.CanEdit && view.Benefits == Key("BenefitsInactive") && view.Experience != null);
        Assert(!SSCMasterProgressionUtility.GetRecord(pawn).Visible);
    }
    /// <summary>不悬停时没有原版日志查询；悬停执行真实记录输出，移除后的旧回调为空。</summary>
    private static void LazyTooltip()
    {
        Pawn pawn = Grown(16); var record = SSCMasterProgressionUtility.GetRecord(pawn);
        Tab().Draw(pawn); Assert(record.TooltipReads == 0 && TooltipHandler.Getters.Count == 2);
        Func<string> getter = TooltipHandler.Getters[0]; Assert(getter().Contains("Native stage detail") && record.TooltipReads == 1);
        pawn.health.RemoveHediff(record); Assert(getter() == string.Empty && record.TooltipReads == 1);
    }
    private static void Toggle(Pawn pawn)
    {
        var bridle = Bind(pawn); var tab = Tab(); float experience = SSCMasterProgressionUtility.GetExperience(pawn);
        Widgets.Click = true; tab.Draw(pawn); Assert(!bridle.shareDamageEnabled);
        tab.Draw(pawn); Assert(bridle.shareDamageEnabled && ReferenceEquals(bridle, SSCBondUtility.GetBridle(pawn)));
        Assert(SSCMasterProgressionUtility.GetExperience(pawn) == experience);
    }
    private static void NoCandidates()
    {
        Pawn pawn = Grown(0); var bridle = Bind(pawn); bridle.targets[0].Dead = bridle.targets[0].Downed = true;
        Assert(Tab().Read(pawn).CanEdit); Widgets.Click = true; Tab().Draw(pawn); Assert(!bridle.shareDamageEnabled);
    }
    private static void MissingBridle()
    {
        Pawn pawn = Grown(12); int adds = pawn.health.Adds; Widgets.Click = true; Tab().Draw(pawn);
        Assert(Widgets.Items.First(i => i.Kind == "checkbox").Disabled && SSCBondUtility.GetBridle(pawn) == null && pawn.health.Adds == adds);
    }
    private static void NonPlayer()
    {
        Pawn pawn = Grown(12); var bridle = Bind(pawn); pawn.Faction = new Faction(); Widgets.Click = true; Tab().Draw(pawn);
        Assert(Widgets.Items.First(i => i.Kind == "checkbox").Disabled && bridle.shareDamageEnabled);
    }
    private static void DisabledGUI()
    {
        Pawn pawn = Grown(12); var bridle = Bind(pawn); GUI.enabled = false; Widgets.Click = true; Tab().Draw(pawn);
        Assert(!GUI.enabled && bridle.shareDamageEnabled && Widgets.Items.All(i => i.Kind != "checkbox" || i.Disabled));
    }
    private static void Late(Action<ITab_SexSlaveTraining, Pawn, Hediff_BridleOfSexSlave> change)
    {
        Pawn pawn = Grown(12); var old = Bind(pawn); var tab = Tab(); var click = tab.CaptureShareAction(pawn);
        change(tab, pawn, old); click(false);
        Assert(old.shareDamageEnabled && (SSCBondUtility.GetBridle(pawn)?.shareDamageEnabled ?? true));
    }
    private static void DuringDraw()
    {
        Pawn pawn = Grown(12); var old = Bind(pawn); Widgets.Click = true; Widgets.BeforeCheckbox = () => pawn.health.RemoveHediff(old);
        Tab().Draw(pawn); Assert(old.shareDamageEnabled && SSCBondUtility.GetBridle(pawn) == null);
    }
    private static void GuiState(bool failure)
    {
        var color = new Color(.2f, .3f, .4f); GUI.color = color; Text.Font = GameFont.Medium; Text.Anchor = TextAnchor.MiddleRight; Text.WordWrap = false;
        Widgets.Throw = failure;
        try { Tab().Draw(Grown(12)); Assert(!failure); } catch (InvalidOperationException) { Assert(failure); }
        Assert(Text.Font == GameFont.Medium && Text.Anchor == TextAnchor.MiddleRight && !Text.WordWrap && GUI.color.Equals(color) && GUI.enabled);
    }
    private static void Gizmos()
    {
        Pawn pawn = Grown(12); var bridle = Bind(pawn);
        Assert(bridle.GetGizmos().SequenceEqual(new[] { Hediff.NativeGizmo }));
    }
    private static void BridleRoundTrip()
    {
        Pawn pawn = Grown(12); var bridle = Bind(pawn); Widgets.Click = true; Tab().Draw(pawn); bridle.ExposeData();
        var restored = new Hediff_BridleOfSexSlave(); Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
        Assert(!restored.shareDamageEnabled && Scribe_Values.Data.ContainsKey("shareDamageEnabled"));
    }
    private static void OldSaveDefault()
    {
        var restored = new Hediff_BridleOfSexSlave { shareDamageEnabled = false }; Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData(); Assert(restored.shareDamageEnabled);
    }
    /// <summary>检查新增键相等、参数逐键匹配和双树逐字节一致；语义翻译仍需人工核查。</summary>
    private static void Localization(string language)
    {
        string relative = "Keyed/SSC_MasterProgression.xml";
        var source = XDocument.Load(SscTestPaths.Combine(repo, "Languages", language, relative)).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var english = XDocument.Load(SscTestPaths.Combine(repo, "Languages/English", relative)).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        Assert(source.Keys.Order().SequenceEqual(english.Keys.Order()));
        foreach (var key in english.Keys)
        {
            Assert(!string.IsNullOrWhiteSpace(source[key]));
            Assert(Regex.Matches(source[key], @"\{\d+\}").Select(m => m.Value).Order().SequenceEqual(Regex.Matches(english[key], @"\{\d+\}").Select(m => m.Value).Order()), "Placeholder mismatch: " + key);
        }
        Assert(File.ReadAllBytes(SscTestPaths.Combine(repo, "Languages", language, relative)).SequenceEqual(File.ReadAllBytes(SscTestPaths.Combine(repo, "Sexslavecraft/Languages", language, relative))));
    }
    /// <summary>宿主不执行完整旧页签；额外检查接入点和固定几何，真实外层滚动留待实机。</summary>
    private static void Integration()
    {
        string source = File.ReadAllText(SscTestPaths.Combine(repo, "Sexslavecraft/ITab/ITab_SexSlaveTraining.cs"));
        Assert(source.Contains("private const float SectionPadding = 12f;") && source.Contains("private const float SectionTitleHeight = 28f;"));
        Assert(source.Contains("contentWidth, masterView.SectionHeight") && source.Contains("return height + masterHeight + 6f;") && source.Contains("DrawMasterPanel(new Rect"));
        Assert(source.Contains("if (comp.pawnIdentity == PawnIdentity.Unset)"));
    }
}
