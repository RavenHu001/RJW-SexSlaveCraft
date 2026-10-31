using System.Text.Json;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal sealed class TestCard : ITab_PersonalityCard
{
    /// <summary>用指定凝胶执行完整生产绘制入口，不重新实现界面布局。</summary>
    public void Draw(Thing gel)
    {
        SelThing = gel;
        DrawLog.Clear();
        FillTab();
    }
}

internal static class Program
{
    private static int passed, failed;
    private static string repo;

    /// <summary>验证四语、多种特质数量及永久资格，并检查独立滚动、数据缺失和分配交互。</summary>
    private static int Main(string[] args)
    {
        repo = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        // 空、短、溢出列表各选代表；下方资格用例已覆盖四语长列表，不再展开语言与数量全表。
        foreach (var sample in new[] { (Language: "ChineseSimplified", Count: 0), (Language: "ChineseTraditional", Count: 3),
            (Language: "English", Count: 15), (Language: "Russian", Count: 30) })
            Run($"{sample.Language} / {sample.Count} traits: fixed information stays visible", () => Layout(sample.Language, sample.Count));
        Run("Long trait names wrap without overlapping the next row", LongNames);
        // 提供长展示结果给真实绘制入口，检查固定摘要避让；业务形态与翻译由原生印记专项验证。
        Run("Imprint status reserves wrapped height above bond summary", () =>
        {
            Thing gel = Gel(30);
            gel.Comp.imprintDisplay = "印记：淫欲之奴（永久锁定性奴，解绑后仍保留此人格印记与身份方向）";
            new TestCard().Draw(gel);
            var imprint = DrawLog.Items.Single(item => item.Kind == "label" && item.Text == gel.Comp.imprintDisplay);
            var bond = DrawLog.Items.Single(item => item.Kind == "label" && item.Text == Text.Plain(Strings.PES_BondHeader));
            Assert(imprint.Rect.yMax <= bond.Rect.y, "The imprint must not overlap the bond section.");
        });
        Run("Scroll regions are independent and the last trait is reachable", IndependentScrolling);
        Run("Switching gels resets scrolling while refresh preserves it", ScrollLifecycle);
        Run("Missing collections and invalid entries render safely", MissingData);
        Run("Drawing restores global GUI state and balances groups", GuiState);
        Run("Assignment menu targets the gel that opened it", Assignment);
        // 使用完整凝胶绘制入口验证两种永久状态，并检查共享角色展示服务的长译文和设置说明。
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            foreach (bool unlocked in new[] { false, true })
                Run($"{language} / qualification {unlocked}: saved gel record stays visible", () => Qualification(language, unlocked));
            Run($"{language}: pawn status wraps and explains manual selection separately", () => PawnQualification(language));
        }
        if (args.Length > 1 && failed == 0)
        {
            Directory.CreateDirectory(args[1]);
            Strings.Load(repo, "ChineseSimplified");
            foreach (int count in new[] { 3, 15, 30 })
            {
                ResetInput();
                new TestCard().Draw(Gel(count));
                File.WriteAllText(Path.Combine(args[1], $"layout-{count}.json"), JsonSerializer.Serialize(
                    new { Draws = DrawLog.Items, Scrolls = DrawLog.Scrolls }, new JsonSerializerOptions { IncludeFields = true }));
            }
        }
        Console.WriteLine($"RESULT: {passed}/{passed + failed} cases passed.");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>隔离测试输入并记录失败，保证某一场景异常不会隐藏后续场景的问题。</summary>
    private static void Run(string name, Action test)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        try
        {
            ResetInput();
            Strings.Load(repo, "ChineseSimplified");
            test();
            Assert(DrawLog.Depth == 0, "Every group and scroll view must be closed.");
            passed++;
            Console.WriteLine("PASS: " + name);
        }
        catch (Exception error)
        {
            failed++;
            Console.WriteLine("FAIL: " + name + "\n" + error);
        }
    }

    /// <summary>设置中性输入，避免上一场景的鼠标或菜单影响下一次绘制。</summary>
    private static void ResetInput()
    {
        DrawLog.Mouse = new Vector2(-1, -1);
        DrawLog.Wheel = 0;
        DrawLog.ClickButton = false;
        Find.CurrentMap = null;
        Find.WindowStack.LastMenu = null;
    }

    /// <summary>断言布局或交互符合预期，失败时提供具体说明。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>建立具备技能、特质、隶属信息和记忆数量的凝胶样本。</summary>
    private static Thing Gel(int traitCount, int skillCount = 12)
    {
        var comp = new CompPersonalityStore
        {
            chainHediffData = new StoredChainHediffData { masterPawn = new Pawn { LabelShort = "Daniel" }, severity = .59f },
            storedRelations = new List<object> { new object(), new object() },
            storedMemories = Enumerable.Range(0, 27).Select(_ => new object()).ToList()
        };
        string[] skills = { "智识", "社交", "医疗", "艺术", "手工", "驯兽", "种植", "烹饪", "采矿", "建造", "格斗", "射击" };
        int[] levels = { 10, 0, 10, 1, 0, 4, 1, 1, 0, 3, 1, 1 };
        for (int i = 0; i < skillCount; i++)
            comp.storedSkills.Add(new StoredSkillData
            {
                def = new SkillDef { LabelCap = skills[i % skills.Length], listOrder = i },
                level = levels[i % levels.Length], passion = i % 3 == 0 ? Passion.Minor : Passion.None
            });
        for (int i = 0; i < traitCount; i++)
            comp.storedTraits.Add(new Trait { LabelCap = $"特质 {i}", def = new TraitDef { description = $"特质 {i} 的完整描述" } });
        return new Thing { Comp = comp };
    }

    /// <summary>判断两个矩形是否有实质交叠，忽略浮点边界误差。</summary>
    private static bool Overlap(Rect a, Rect b) =>
        Math.Min(a.xMax, b.xMax) - Math.Max(a.x, b.x) > .01f && Math.Min(a.yMax, b.yMax) - Math.Max(a.y, b.y) > .01f;

    /// <summary>有锁链主人和性奴标记的凝胶仍只显示自身保存值；刷新、切换不会污染该值。</summary>
    private static void Qualification(string language, bool unlocked)
    {
        Strings.Load(repo, language);
        Thing gel = Gel(30);
        gel.Comp.masterQualificationUnlocked = unlocked;
        var card = new TestCard();
        card.Draw(gel);
        string key = unlocked ? "SSC_MasterQualification_StatusUnlocked" : "SSC_MasterQualification_StatusLocked";
        string label = Strings.TranslateKey(key);
        var row = DrawLog.Items.Single(item => item.Kind == "label" && item.Text == label);
        Assert(row.Clip == null && row.Wrap, "Qualification must be complete and outside trait scrolling.");
        Assert(row.Rect.height >= Text.CalcHeight(label, row.Rect.width), "Long status must reserve its full height.");
        Assert(DrawLog.Items.Any(item => item.Kind == "tooltip" && item.Text == Strings.TranslateKey("SSC_MasterQualification_GelStatusTip")),
            "Gel must explain personality replacement, preservation and clone exclusion.");
        Assert(gel.Comp.masterQualificationUnlocked == unlocked, "Drawing must not grant or clear qualification.");
        // 共用已有布局断言核对两种状态的固定信息、滚动区及按钮，不只检查新增行。
        Layout(language, 30, unlocked);
        card.Draw(gel);
        var other = Gel(0); other.Comp.masterQualificationUnlocked = !unlocked;
        card.Draw(other);
        Assert(DrawLog.Items.Any(item => item.Kind == "label" && item.Text == SSCMasterQualificationDisplay.GetLabel(!unlocked)),
            "Switching gels must read the new personality record.");
        Assert(gel.Comp.masterQualificationUnlocked == unlocked, "Switching must not alter the previous gel.");
    }

    /// <summary>角色状态文案不受手动放行提示影响；窄栏下共享绘制服务按真实译文分配高度。</summary>
    private static void PawnQualification(string language)
    {
        DrawLog.Clear();
        Strings.Load(repo, language);
        string locked = SSCMasterQualificationDisplay.GetLabel(false);
        string unlocked = SSCMasterQualificationDisplay.GetLabel(true);
        Assert(locked != unlocked, "Permanent states must have distinct translations.");
        foreach (bool direct in new[] { false, true })
        {
            string tip = SSCMasterQualificationDisplay.GetPawnTooltip(direct);
            Assert(tip.Contains(Strings.TranslateKey("SSC_MasterQualification_PawnStatusTip")) &&
                tip.Contains(Strings.TranslateKey(direct ? "SSC_MasterQualification_DirectSelectionOnTip" : "SSC_MasterQualification_DirectSelectionOffTip")),
                "Pawn tooltip must separately explain saved qualification and the current manual setting.");
            foreach (string label in new[] { locked, unlocked })
            {
                float height = SSCMasterQualificationDisplay.GetHeight(label, 120f, Text.LineHeight);
                Assert(height >= Text.CalcHeight(label, 120f), "Narrow status must wrap without clipping.");
                bool previousWordWrap = Text.WordWrap;
                try
                {
                    Text.WordWrap = false;
                    SSCMasterQualificationDisplay.Draw(new Rect(0f, 0f, 120f, height), label, tip);
                    Assert(!Text.WordWrap && DrawLog.Items.Last(item => item.Kind == "label").Wrap,
                        "Status must force wrapping locally and restore the previous GUI state.");
                }
                finally { Text.WordWrap = previousWordWrap; }
            }
        }
        Assert(DrawLog.Items.Count(item => item.Kind == "label" && item.Text == locked) == 2 &&
            DrawLog.Items.Count(item => item.Kind == "label" && item.Text == unlocked) == 2,
            "Manual-setting tooltips must not replace the permanent status.");
    }

    /// <summary>验证固定文字互不重叠且位于窗口内，多条目列表必须具备可滚动的内容范围。</summary>
    private static void Layout(string language, int count, bool qualification = false)
    {
        Strings.Load(repo, language);
        Thing gel = Gel(count);
        gel.Comp.masterQualificationUnlocked = qualification;
        new TestCard().Draw(gel);
        var fixedLabels = DrawLog.Items.Where(item => item.Kind == "label" && item.Clip == null).ToList();
        foreach (var label in fixedLabels)
        {
            Assert(label.Rect.x >= 17 && label.Rect.y >= 17 && label.Rect.xMax <= 483.01f && label.Rect.yMax <= 513.01f,
                "Fixed text must stay inside the card: " + label.Text);
            foreach (var other in fixedLabels.Where(other => !ReferenceEquals(other, label)))
                Assert(!Overlap(label.Rect, other.Rect), "Fixed labels overlap: " + label.Text + " / " + other.Text);
        }
        Assert(DrawLog.Scrolls.Count == 2, "Skills and traits must have separate scroll views.");
        Assert(!Overlap(DrawLog.Scrolls[0].Viewport, DrawLog.Scrolls[1].Viewport), "Scroll regions must not overlap.");
        Rect footer = DrawLog.Items.Single(item => item.Kind == "button").Rect;
        Assert(footer.width > 450 && footer.yMax <= 513.01f, "The full-width button must remain visible.");
        Assert(fixedLabels.Any(item => item.Text.Contains("Daniel")), "Master must remain outside scrolling.");
        Assert(fixedLabels.Any(item => item.Text == Text.Plain(Strings.PES_MemorySummary(27))), "Memory count must remain outside scrolling.");
        Assert(DrawLog.Items.Any(item => item.Kind == "tooltip" && item.Text.Contains("'Riley' Test")), "Complete name must be available as a tooltip.");
        var traits = DrawLog.Scrolls[1];
        if (count >= 15) Assert(traits.Content.height > traits.Viewport.height, "Many traits must scroll rather than enlarge the card.");
        if (count <= 3) Assert(Math.Abs(traits.Content.height - traits.Viewport.height) < .01f, "Short lists must shrink to their content.");
        Assert(DrawLog.Scrolls.All(view => view.Viewport.yMax < footer.y), "Scrolling content must not cover the action button.");
    }

    /// <summary>验证长中文及英文特质名称增加行高，并完整保留文本和描述提示。</summary>
    private static void LongNames()
    {
        Thing gel = Gel(15);
        string[] names = { "这是一个来自其它模组并且长度明显超过单行宽度的普通人格特质名称", "An unusually long personality trait name from another mod" };
        for (int i = 0; i < names.Length; i++) gel.Comp.storedTraits[i].LabelCap = names[i];
        new TestCard().Draw(gel);
        var rows = DrawLog.Items.Where(item => item.Kind == "label" && gel.Comp.storedTraits.Any(trait => trait.LabelCap == item.Text)).ToList();
        Assert(rows.Count == 15, "Every trait must be drawn without truncating its name.");
        Assert(rows[0].Rect.height > 24 && rows[1].Rect.height > 24 && rows.All(row => row.Wrap), "Long trait names must wrap.");
        for (int i = 1; i < rows.Count; i++) Assert(!Overlap(rows[i - 1].Rect, rows[i].Rect), "Wrapped traits must not overlap.");
    }

    /// <summary>将鼠标移入指定视口并模拟足够大的滚轮输入，以访问列表末尾。</summary>
    private static void ScrollToEnd(ScrollRecord view)
    {
        DrawLog.Mouse = new Vector2(view.Viewport.x + 5, view.Viewport.y + 5);
        DrawLog.Wheel = 10000;
    }

    /// <summary>验证滚动特质不会移动技能及摘要，并能访问最后一项；技能滚动也不会改变特质位置。</summary>
    private static void IndependentScrolling()
    {
        var card = new TestCard();
        Thing gel = Gel(30, 24);
        card.Draw(gel);
        var summary = DrawLog.Items.Single(item => item.Kind == "label" && item.Text.Contains("Daniel"));
        ScrollToEnd(DrawLog.Scrolls[1]);
        card.Draw(gel);
        Assert(DrawLog.Scrolls[0].Position.y == 0 && DrawLog.Scrolls[1].Position.y > 0, "Only the hovered trait list may move.");
        var last = DrawLog.Items.Single(item => item.Kind == "label" && item.Text == "特质 29");
        Assert(Overlap(last.Rect, last.Clip.Value), "The final trait must be reachable.");
        Assert(DrawLog.Items.Any(item => item.Kind == "label" && item.Text == summary.Text && item.Rect.Equals(summary.Rect)), "Summary must not move while scrolling.");
        float traitPosition = DrawLog.Scrolls[1].Position.y;
        ScrollToEnd(DrawLog.Scrolls[0]);
        card.Draw(gel);
        Assert(DrawLog.Scrolls[0].Position.y > 0 && DrawLog.Scrolls[1].Position.y == traitPosition, "Skill scrolling must not move traits.");
    }

    /// <summary>验证同一凝胶刷新时保留位置，内容缩短时收回位置，切换凝胶时清零。</summary>
    private static void ScrollLifecycle()
    {
        var card = new TestCard();
        Thing gel = Gel(30);
        card.Draw(gel);
        ScrollToEnd(DrawLog.Scrolls[1]);
        card.Draw(gel);
        float position = DrawLog.Scrolls[1].Position.y;
        ResetInput();
        card.Draw(gel);
        Assert(DrawLog.Scrolls[1].Position.y == position && position > 0, "Refreshing the same gel must preserve scrolling.");
        gel.Comp.storedTraits.RemoveRange(3, 27);
        card.Draw(gel);
        Assert(DrawLog.Scrolls[1].Position.y == 0, "Shorter content must not leave an empty scrolled area.");
        Thing other = Gel(30);
        card.Draw(other);
        Assert(DrawLog.Scrolls.All(view => view.Position.y == 0), "Switching gels must reset both lists.");
    }

    /// <summary>验证缺失列表、失效定义及缺失背景不会中断界面绘制。</summary>
    private static void MissingData()
    {
        Thing gel = Gel(0);
        gel.Comp.storedTraits = null;
        gel.Comp.storedSkills = null;
        gel.Comp.storedRelations = null;
        gel.Comp.storedMemories = null;
        gel.Comp.chainHediffData = null;
        gel.Comp.childhood = gel.Comp.adulthood = null;
        var card = new TestCard();
        card.Draw(gel);
        Assert(DrawLog.Items.Any(item => item.Kind == "button"), "Missing optional data must not hide the action button.");
        gel.Comp.storedTraits = new List<Trait> { null, new Trait { def = null } };
        gel.Comp.storedSkills = new List<StoredSkillData> { null, new StoredSkillData() };
        card.Draw(gel);
        Assert(DrawLog.Items.Any(item => item.Text == Text.Plain(Strings.PES_TraitsCount(0))), "Invalid traits must not inflate the displayed count.");
    }

    /// <summary>验证生产绘制入口不会把颜色、字号、对齐或换行设置泄漏到其它界面。</summary>
    private static void GuiState()
    {
        Color color = new Color(.2f, .3f, .4f);
        GUI.color = color;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        Text.WordWrap = false;
        new TestCard().Draw(Gel(15));
        Assert(GUI.color.Equals(color) && Text.Font == GameFont.Tiny && Text.Anchor == TextAnchor.MiddleRight && !Text.WordWrap,
            "All global GUI settings must be restored.");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;
    }

    /// <summary>验证目标菜单能选择和取消目标，且延迟选择不会误操作后来选中的凝胶。</summary>
    private static void Assignment()
    {
        Find.CurrentMap = new Map();
        var target = new Pawn { LabelShort = "Target" };
        target.health.hediffSet.IsHollow = true;
        Find.CurrentMap.mapPawns.AllPawnsSpawned.Add(target);
        Thing gel = Gel(15), other = Gel(3);
        var card = new TestCard();
        DrawLog.ClickButton = true;
        card.Draw(gel);
        FloatMenu menu = Find.WindowStack.LastMenu;
        Assert(menu?.Options.Count == 1 && menu.Options[0].Action != null, "The target menu must contain an actionable hollow pawn.");
        card.SelThing = other;
        menu.Options[0].Action();
        Assert(Find.CurrentMap.Assignments.GetAssignedTarget(gel) == target && Find.CurrentMap.Assignments.GetAssignedTarget(other) == null,
            "The callback must use the gel that opened the menu.");
        DrawLog.ClickButton = true;
        card.Draw(gel);
        Find.WindowStack.LastMenu.Options.Single(option => option.Label == Strings.PES_Unassign).Action();
        Assert(Find.CurrentMap.Assignments.GetAssignedTarget(gel) == null, "The menu must still support unassignment.");
    }
}
