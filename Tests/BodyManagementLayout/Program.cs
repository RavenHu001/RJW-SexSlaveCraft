// AI 辅助生成：执行真实管理栏、身体记录与切换入口；绘制边界记录矩形和点击。
using System.Xml.Linq;
using System.Text.RegularExpressions;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static int passed, failed;
    private static HediffDef development;
    private static string repository;

    /// <summary>读取真实阶段 XML，再验证可见性、尺寸、操作、过期视图与 GUI 状态恢复。</summary>
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args[0]);
        repository = repo;
        var xml = XDocument.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_UterusDevelopment.xml")).Root.Element("HediffDef");
        development = new HediffDef { defName = SSCUterusDevelopmentUtility.DefName, hediffClass = typeof(Hediff_SSCUterusDevelopment),
            stages = xml.Element("stages").Elements("li").Select(Stage).ToList(),
            Extension = new SSCUterusDevelopmentExtension { fertileStage = Stage(xml.Element("modExtensions").Element("li").Element("fertileStage")) } };
        DefDatabase<HediffDef>.Definition = development;
        foreach (var (language, width, scale, contents) in new (string, float, float, int)[]
        {
            ("ChineseSimplified", 200f, 1.8f, 3),
            ("ChineseSimplified", 322f, 1f, 0),
            ("ChineseTraditional", 200f, 1.8f, 3),
            ("ChineseTraditional", 322f, 1f, 0),
            ("English", 200f, 1.8f, 3),
            ("English", 322f, 1f, 0),
            ("Russian", 200f, 1.8f, 3),
            ("Russian", 322f, 1f, 0),
            ("ChineseSimplified", 200f, 1f, 1),
            ("ChineseSimplified", 322f, 1.8f, 2),
            ("ChineseTraditional", 200f, 1f, 2),
            ("ChineseTraditional", 322f, 1.8f, 1),
            ("ChineseSimplified", 200f, 1.8f, 0),
            ("ChineseSimplified", 322f, 1f, 3),
            ("English", 200f, 1f, 1),
            ("English", 200f, 1f, 2),
            ("Russian", 200f, 1f, 1),
            ("Russian", 200f, 1f, 2),
            ("ChineseSimplified", 200f, 1f, 0),
            ("ChineseSimplified", 200f, 1f, 2),
            ("ChineseSimplified", 200f, 1f, 3),
            ("ChineseSimplified", 200f, 1.8f, 1),
            ("ChineseSimplified", 200f, 1.8f, 2),
            ("ChineseSimplified", 322f, 1f, 1),
        })
            Run($"{language} 内容{contents} 宽{width} 字宽{scale} 无空栏、完整换行且不重叠", () =>
            { Translator.Load(repo, language); Layout(width, scale, contents); });
        Run("长标题与开关译文按缩进宽度测量且无占位符泄漏", LongTranslations);
        foreach (float progress in new[] { .01f, .3f, .7f, .99999f, 1f })
            Run("各培养阶段只显示生育开关 " + progress, () => StageDisplay(progress));
        Run("满级开关执行生产检查并刷新健康状态", () =>
        {
            var p = Body(2, 1); var h = SSCUterusDevelopmentUtility.Find(p); int before = p.health.Notifications;
            Draw(p, "SSC_BodyManagement_FertilityToggle".Translate()); Require(h.FertilityEnabled && p.health.Notifications == before + 1);
            Draw(p, "SSC_BodyManagement_FertilityToggle".Translate()); Require(!h.FertilityEnabled);
        });
        Run("未完成的禁用控件不能被异常鼠标返回值绕过", () =>
        { var p = Body(2); Widgets.IgnoreDisabled = true; Draw(p, "SSC_BodyManagement_FertilityToggle".Translate()); Require(!SSCUterusDevelopmentUtility.Find(p).FertilityEnabled); });
        Run("妊娠时保留勾选、禁用关闭并显示原因", () =>
        {
            var p = Body(2, 1); SSCUterusDevelopmentUtility.TrySetFertilityEnabled(p, true, out _); p.Pregnant = true;
            Widgets.IgnoreDisabled = true; Draw(p, "SSC_BodyManagement_FertilityToggle".Translate()); Require(SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
            Require(Widgets.Drawn.Single(d => d.Kind == "checkbox").Disabled);
            Require(Widgets.Drawn.Any(d => d.Kind == "tip" && d.Label == "SSC_BodyManagement_PregnancyBlocked".Translate()));
        });
        Run("测量后才发生妊娠由实际提交拒绝", () =>
        {
            var p = Body(2, 1); SSCUterusDevelopmentUtility.TrySetFertilityEnabled(p, true, out _);
            var tab = new ITab_SexSlaveTraining(); var view = tab.Capture(p, 322); p.Pregnant = true;
            Widgets.Click = "SSC_BodyManagement_FertilityToggle".Translate(); view.Draw(); Require(SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
            Require(Messages.Rejections.Single().Contains("妊娠"));
        });
        foreach (string change in new[] { "移除开发", "替换开发", "移除泌乳", "替换组件", "切换角色" })
            Run("过期界面不写回：" + change, () => Stale(change));
        Run("产奶开关独立且不影响开发进度和生育选择", () =>
        {
            var p = Body(3, 1); SSCUterusDevelopmentUtility.TrySetFertilityEnabled(p, true, out _);
            Draw(p, Strings.ITab_MilkProductionToggle); Require(!p.Training.milkProductionEnabled);
            Require(SSCUterusDevelopmentUtility.Find(p).Severity == 1 && SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
        });
        Run("反复刷新不写健康状态或开关", () =>
        { var p = Body(3, 1); int n = p.health.Notifications; Draw(p); Draw(p); Require(n == p.health.Notifications && p.Training.milkProductionEnabled && !SSCUterusDevelopmentUtility.Find(p).FertilityEnabled); });
        foreach (bool throws in new[] { false, true }) Run("绘制归还全部 GUI 状态 异常=" + throws, () => GuiState(throws));
        Run("主调教页的 1.7 接入先于身份分支且无旧泌乳栏", () => MainIntegration(repo));
        RunToggleFrameworkTests();
        Console.WriteLine($"{passed}/{passed + failed} cases passed"); return failed == 0 ? 0 : 1;
    }

    /// <summary>仅加载展示所需配置；原生 XML 解析与属性运算由 UterusDevelopmentNative 验证。</summary>
    private static HediffStage Stage(XElement element) => new()
    {
        minSeverity = (float?)element.Element("minSeverity") ?? 0,
        fertilityFactor = (float?)element.Element("fertilityFactor") ?? 1,
        label = (string)element.Element("label"),
        // statFactors 是以 StatDef 名为元素名的 XML 字典；不能按 li 列表读取而丢掉全部倍率。
        statFactors = element.Element("statFactors")?.Elements().Select(x => new StatModifier
        { stat = new StatDef { defName = x.Name.LocalName, label = "已有属性译名" }, value = (float)x }).ToList()
    };

    /// <summary>隔离控件输入和静态显示状态，每个用例都从正常 GUI 环境开始。</summary>
    private static void Run(string name, Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        Widgets.Drawn.Clear(); Widgets.Click = null; Widgets.BeforeClick = null; Widgets.ThrowLabel = false; Widgets.IgnoreDisabled = false;
        GUI.enabled = true; GUI.color = Color.white; Text.Font = GameFont.Small; Text.WordWrap = true;
        Text.Anchor = TextAnchor.UpperLeft; Text.WidthScale = 1; Scribe.mode = LoadSaveMode.Inactive; Messages.Rejections.Clear();
        Translator.Load(repository, "ChineseSimplified");
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    /// <summary>创建仅含本例所需身体条目的角色，开发创建仍经过生产入口。</summary>
    private static Pawn Body(int contents, float progress = .01f)
    {
        var p = new Pawn();
        if ((contents & 1) != 0) p.health.hediffSet.hediffs.Add(new Hediff { pawn = p, def = SSCDefOf.SSC_Lactating_SubState, Comp = new HediffComp_PermanentLactating() });
        if ((contents & 2) != 0) { Require(SSCUterusDevelopmentUtility.TryCreate(p, out var h, out _)); h.Severity = progress; }
        return p;
    }
    /// <summary>绘制真实区块并允许模拟一次指定复选框点击。</summary>
    private static void Draw(Pawn pawn, string click = null)
    { Widgets.Click = click; var tab = new ITab_SexSlaveTraining(); tab.Capture(pawn, 322).Draw(); }
    /// <summary>逐行核对实际矩形和测量高度；零内容时不绘制任何框或条目。</summary>
    private static void Layout(float width, float scale, int contents)
    {
        Text.WidthScale = scale; var p = Body(contents, 1);
        // 四语完整身体组合沿用既有代表样本，检查带营养不足提示的较长从属行。
        if (contents == 3) AttachPnaProduction(p).IsWaitingForNutrition = true;
        var tab = new ITab_SexSlaveTraining(); var view = tab.Capture(p, width); view.Draw();
        if (contents == 0) { Require(view.Height == 0 && Widgets.Drawn.Count == 0); return; }
        Require(Widgets.Drawn.Count(d => d.Kind == "section") == 1);
        float bottom = 0;
        foreach (var draw in Widgets.Drawn.Where(d => d.Kind == "label" || d.Kind == "checkbox"))
        {
            Require(draw.Rect.y >= bottom); bottom = draw.Rect.yMax;
            Require(draw.Rect.height >= Text.CalcHeight(draw.Label, draw.Rect.width - (draw.Kind == "checkbox" ? 24 : 0)));
            Require(draw.Rect.yMax <= view.Height - 12 && draw.Rect.xMax <= width - 12);
        }
        Require(Widgets.Drawn.Count(d => d.Kind == "checkbox") == ((contents & 1) != 0 ? 1 : 0) + ((contents & 2) != 0 ? 1 : 0) + (contents == 3 ? 1 : 0));
        if (contents == 3) AssertPnaDetail("43%", waiting: true);
        if ((contents & 1) != 0)
        {
            var milk = Widgets.Drawn.Single(d => d.Kind == "checkbox" && d.Label == Strings.ITab_MilkProductionToggle);
            var charge = Widgets.Drawn.Single(d => d.Kind == "label" && d.Label.Contains("53%"));
            Require(charge.Rect.x == milk.Rect.x + 16 && charge.Rect.xMax == milk.Rect.xMax);
            if ((contents & 2) != 0)
            {
                var fertility = Widgets.Drawn.Single(d => d.Kind == "checkbox" && d.Label == "SSC_BodyManagement_FertilityToggle".Translate());
                Require(fertility.Rect.x == milk.Rect.x && fertility.Rect.y - charge.Rect.yMax >= 16);
            }
        }
    }

    /// <summary>模拟更长标题和开关译文；测量使用缩进后的宽度，标题在当次视图内保持一致。</summary>
    private static void LongTranslations()
    {
        string title = string.Concat(Enumerable.Repeat("身体管理的长标题", 5));
        Translator.Values["SSC_BodyManagement_Title"] = title;
        Translator.Values["SSC_ITab_MilkProductionToggle"] = string.Concat(Enumerable.Repeat("泌乳开关长译文", 4));
        Translator.Values["SSC_BodyManagement_FertilityToggle"] = string.Concat(Enumerable.Repeat("生育开关长译文", 4));
        Layout(200, 1.8f, 3);
        Require(Widgets.Drawn.All(d => !Regex.IsMatch(d.Label, @"\{\d+\}")));
        Widgets.Drawn.Clear(); var tab = new ITab_SexSlaveTraining(); var view = tab.Capture(Body(3), 200);
        Translator.Values["SSC_BodyManagement_Title"] = "稍后变化的标题";
        view.Draw(); Require(Widgets.Drawn.Any(d => d.Kind == "label" && d.Label == title));
    }

    /// <summary>五个培养进度都只绘制一个生育开关；进度、阶段、收益和生育状态不再作为页面行出现。</summary>
    private static void StageDisplay(float progress)
    {
        var p = Body(2, progress); Draw(p);
        var labels = Widgets.Drawn.Where(d => d.Kind == "label").ToList();
        Require(labels.Count == 1 && labels[0].Label == "SSC_BodyManagement_Title".Translate());
        var toggle = Widgets.Drawn.Single(d => d.Kind == "checkbox");
        Require(toggle.Label == "SSC_BodyManagement_FertilityToggle".Translate() && toggle.Disabled == (progress < 1));
        Require(Widgets.Drawn.Where(d => d.Kind != "tip").All(d => !d.Label.Contains("子宫开发") && !d.Label.Contains("%") && !d.Label.Contains("收益") && !d.Label.Contains("生育：")));
    }
    /// <summary>保存旧界面后替换身体条件，实际绘制不得操作新身体记录或旧组件。</summary>
    private static void Stale(string change)
    {
        var p = Body(3, 1); var tab = new ITab_SexSlaveTraining(); var view = tab.Capture(p, 322);
        var old = SSCUterusDevelopmentUtility.Find(p); var comp = p.Training;
        if (change == "移除开发" || change == "替换开发") p.health.RemoveHediff(old);
        if (change == "替换开发") { Require(SSCUterusDevelopmentUtility.TryCreate(p, out var h, out _)); h.Severity = 1; }
        if (change == "移除泌乳") p.health.RemoveHediff(p.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState));
        if (change == "替换组件") p.Training = new CompSexSlaveTraining();
        if (change == "切换角色") tab.SelPawn = Body(3, 1);
        Widgets.IgnoreDisabled = true; Widgets.Click = change == "移除泌乳" ? Strings.ITab_MilkProductionToggle : "SSC_BodyManagement_FertilityToggle".Translate(); view.Draw();
        Require(!old.FertilityEnabled && !(SSCUterusDevelopmentUtility.Find(p)?.FertilityEnabled ?? false) && comp.milkProductionEnabled);
    }
    /// <summary>正常及异常绘制均归还颜色、禁用、字体、对齐和换行设置。</summary>
    private static void GuiState(bool throws)
    {
        var p = Body(3); var tab = new ITab_SexSlaveTraining(); var view = tab.Capture(p, 322);
        Text.Font = GameFont.Medium; Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = false;
        GUI.enabled = false; GUI.color = new Color(.5f, .7f, .9f, .4f); var color = GUI.color;
        Widgets.ThrowLabel = throws;
        try { view.Draw(); Require(!throws); } catch (InvalidOperationException) { Require(throws); }
        Require(Text.Font == GameFont.Medium && Text.Anchor == TextAnchor.MiddleCenter && !Text.WordWrap && !GUI.enabled && GUI.color == color);
    }
    /// <summary>检查共享文件的版本接线；这是源码结构检查，不冒充完整 FillTab 运行或真实滚动输入。</summary>
    private static void MainIntegration(string repo)
    {
        string source = File.ReadAllText(Path.Combine(repo, "Sexslavecraft/ITab/ITab_SexSlaveTraining.cs"));
        var output = new List<string>(); var stack = new Stack<bool>(); bool active = true;
        foreach (string line in source.Split('\n'))
        {
            string trim = line.Trim();
            if (trim.StartsWith("#if ")) { stack.Push(active); active &= trim == "#if SSC_RW17"; }
            else if (trim == "#else") active = stack.Peek() && !active;
            else if (trim == "#endif") active = stack.Pop();
            else if (active) output.Add(line);
        }
        string current = string.Join('\n', output);
        Require(current.Split("DrawBodyManagementSection(").Length == 2 && !current.Contains("DrawMilkSection("));
        Require(current.IndexOf("DrawBodyManagementSection(") < current.IndexOf("if (comp.pawnIdentity == PawnIdentity.Master)"));
        Require(current.Contains("contentHeight += bodyView.Height + SectionSpacing") && current.Contains("curY += bodyView.Height + SectionSpacing"));
    }
    /// <summary>断言异常包含本例信息，由统一入口汇总。</summary>
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("断言失败"); }
}
