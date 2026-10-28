// AI 辅助生成：直接运行真实注册表、载体后端、身体工具和 UI，不复制生育或泌乳规则。
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>第二批仅验证原字段适配及即时提交，不把跨人格／克隆整体验收提前计为第三批完成。</summary>
    private static void RunToggleFrameworkTests()
    {
        const string milk = SSCAbilityToggleUtility.MilkProduction, fertility = SSCAbilityToggleUtility.UterusFertility;
        Run("身体条目由注册表生成且默认与原保存源一致", () =>
        {
            Require(SSCAbilityToggleUtility.Definitions.Count(d => d.Group == SSCAbilityToggleGroup.Body) == 3);
            var p = Body(3, 1);
            Require(SSCAbilityToggleUtility.GetEnabled(p, milk) && !SSCAbilityToggleUtility.GetEnabled(p, fertility));
            Require(!p.Training.abilityToggles.HasEntries);
            Require(SSCAbilityToggleUtility.Definitions.Where(d => d.Group == SSCAbilityToggleGroup.Body).All(d => d.MarkerDefName == null));
        });
        Run("PNA共用身体行位于泌乳和生育之间且只改变自身生产选择", () =>
        {
            var p = Body(3, 1); var production = AttachPnaProduction(p);
            Draw(p, "SSC_PnaProduction_Label".Translate());
            Require(!production.ProductionEnabled && p.Training.milkProductionEnabled);
            Require(production.parent.Severity == .43f && !p.Training.abilityToggles.HasEntries);
            Require(!SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
            var labels = Widgets.Drawn.Where(d => d.Kind == "checkbox").Select(d => d.Label).ToArray();
            Require(labels.SequenceEqual(new[] { Strings.ITab_MilkProductionToggle, "SSC_PnaProduction_Label".Translate(), "SSC_BodyManagement_FertilityToggle".Translate() }));
            // 点击帧绘制的是开启状态快照；下一帧才显示刚提交的关闭状态。
            AssertPnaDetail("43%");
            // 暂停后的新视图仍只显示一条原进度；显示查询不能补建载体或写健康／人格状态。
            int notifications = p.health.Notifications;
            Widgets.Drawn.Clear(); Draw(p); AssertPnaDetail("43%", disabled: true);
            production.parent.Severity = .61f;
            Widgets.Drawn.Clear(); Draw(p); AssertPnaDetail("61%", disabled: true);
            Require(production.parent.Severity == .61f && !production.ProductionEnabled &&
                p.health.Notifications == notifications && !p.Training.abilityToggles.HasEntries);
        });
        Run("替换PNA生产记录后旧身体视图不能改变新记录", () =>
        {
            var p = Body(0); var previous = AttachPnaProduction(p);
            var tab = new ITab_SexSlaveTraining(); var snapshot = tab.Capture(p, 322);
            p.health.RemoveHediff(previous.parent); var current = AttachPnaProduction(p);
            Widgets.IgnoreDisabled = true; Widgets.Click = "SSC_PnaProduction_Label".Translate(); snapshot.Draw();
            Require(previous.ProductionEnabled && current.ProductionEnabled);
            Require(Widgets.Drawn.Single(d => d.Kind == "checkbox").Disabled);
        });
        // 故意放入冲突的同名人格键，检出后端误读字典或产生双保存源的问题。
        Run("泌乳偏好和原字段双向一致，字典同名键不能覆盖", () =>
        {
            var p = Body(1); p.Training.milkProductionEnabled = false; p.Training.abilityToggles.Set(milk, true);
            Require(!SSCAbilityToggleUtility.GetEnabled(p, milk) && !SSCAbilityToggleUtility.GetMilkProductionEnabled(p));
            var view = SSCAbilityToggleUtility.GetView(p, milk);
            Require(SSCAbilityToggleUtility.TrySet(p, milk, true, view.Context, out _));
            Require(p.Training.milkProductionEnabled);
        });
        Run("缺训练组件的原生产读取仍默认放行，UI 不补建组件", () =>
        {
            var p = Body(1); p.Training = null;
            Require(SSCAbilityToggleUtility.GetMilkProductionEnabled(p));
            Require(new ITab_SexSlaveTraining().Capture(p, 322).Height == 0 && p.Training == null);
        });
        foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master, PawnIdentity.Slave })
            Run("身体开关不新增身份／研究／绑定门槛 " + identity, () =>
            {
                var p = Body(3, 1); p.Training.pawnIdentity = identity;
                p.health.hediffSet.hediffs.Add(new Hediff { pawn = p, def = SSCDefOf.SSC_PersonalityExcreted_Done });
                Require(SSCAbilityToggleUtility.CanSet(p, milk, false, out _));
                Require(SSCAbilityToggleUtility.CanSet(p, fertility, true, out _));
                Draw(p, Strings.ITab_MilkProductionToggle); Draw(p, "SSC_BodyManagement_FertilityToggle".Translate());
                Require(!p.Training.milkProductionEnabled && SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
            });
        Run("同组件换人格后旧泌乳点击失效", () =>
        {
            var p = Body(1); var tab = new ITab_SexSlaveTraining(); var snapshot = tab.Capture(p, 322);
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); using (scope) { }
            Widgets.IgnoreDisabled = true; Widgets.Click = Strings.ITab_MilkProductionToggle; snapshot.Draw();
            Require(p.Training.milkProductionEnabled);
            Require(Widgets.Drawn.Any(d => d.Kind == "checkbox" && d.Disabled));
        });
        Run("替换泌乳实例后旧视图不能写新身体的同一偏好字段", () =>
        {
            var p = Body(1); var tab = new ITab_SexSlaveTraining(); var snapshot = tab.Capture(p, 322);
            p.health.hediffSet.hediffs.Clear(); p.health.hediffSet.hediffs.Add(new Hediff { pawn = p, def = SSCDefOf.SSC_Lactating_SubState });
            Widgets.IgnoreDisabled = true; Widgets.Click = Strings.ITab_MilkProductionToggle; snapshot.Draw();
            Require(p.Training.milkProductionEnabled);
        });
        Run("生育仍委托身体记录，同名人格键不改变保存归属", () =>
        {
            var p = Body(2, 1); p.Training.abilityToggles.Set(fertility, true);
            Require(!SSCAbilityToggleUtility.GetEnabled(p, fertility));
            var view = SSCAbilityToggleUtility.GetView(p, fertility);
            Require(SSCAbilityToggleUtility.TrySet(p, fertility, true, view.Context, out _));
            Require(SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
            SSCAbilityToggleUtility.ClearPersonalityState(p);
            Require(SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
        });
        Run("未完成和缺配置的开启拒绝保留原原因", () =>
        {
            var p = Body(2); Require(!SSCAbilityToggleUtility.CanSet(p, fertility, true, out string reason));
            Require(reason == "SSC_BodyManagement_NotComplete".Translate());
            SSCUterusDevelopmentUtility.Find(p).Severity = 1;
            var previous = development.Extension.fertileStage; development.Extension.fertileStage = null;
            try { Require(!SSCAbilityToggleUtility.CanSet(p, fertility, true, out reason) && reason == "SSC_BodyManagement_FertilityConfigUnavailable".Translate()); }
            finally { development.Extension.fertileStage = previous; }
        });
        Run("原 setter 在公共预检后拒绝时保留原因与原状态", () =>
        {
            var p = Body(2, 1); Require(SSCUterusDevelopmentUtility.TrySetFertilityEnabled(p, true, out _));
            var view = SSCAbilityToggleUtility.GetView(p, fertility); int checks = 0, before = p.health.Notifications;
            // 第一次公共 CanSet 放行，第二次原 setter 检出妊娠；只替代引擎查询，不复制生育判断。
            rjw.PawnExtensions.PregnancyProbe = _ => ++checks >= 2;
            try
            {
                Require(!SSCAbilityToggleUtility.TrySet(p, fertility, false, view.Context, out string reason));
                Require(reason == "SSC_BodyManagement_PregnancyBlocked".Translate());
                Require(SSCUterusDevelopmentUtility.Find(p).FertilityEnabled && p.health.Notifications == before);
            }
            finally { rjw.PawnExtensions.PregnancyProbe = null; }
        });
        Run("身体同值提交不改进度／健康通知且不写人格字典", () =>
        {
            var p = Body(3, 1); int before = p.health.Notifications;
            foreach (var pair in new[] { (milk, true), (fertility, false) })
            {
                var view = SSCAbilityToggleUtility.GetView(p, pair.Item1);
                Require(SSCAbilityToggleUtility.TrySet(p, pair.Item1, pair.Item2, view.Context, out _));
            }
            Require(p.health.Notifications == before && !p.Training.abilityToggles.HasEntries && SSCUterusDevelopmentUtility.Find(p).Severity == 1);
        });
        Run("加载期拒绝身体两种后端且不改原字段", () =>
        {
            var p = Body(3, 1); var a = SSCAbilityToggleUtility.GetView(p, milk); var b = SSCAbilityToggleUtility.GetView(p, fertility);
            Scribe.mode = LoadSaveMode.LoadingVars;
            Require(!SSCAbilityToggleUtility.TrySet(p, milk, false, a.Context, out _));
            Require(!SSCAbilityToggleUtility.TrySet(p, fertility, true, b.Context, out _));
            Require(p.Training.milkProductionEnabled && !SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
        });
    }

    /// <summary>只构造 GUI 所需的身体 owner 与存储边界；实际生产和保存另由原生专项验证。</summary>
    private static HediffComp_SeverityProduct AttachPnaProduction(Pawn pawn)
    {
        var record = new Hediff { pawn = pawn, def = new HediffDef { defName = SSCPnaProductionUtility.ProducerDefName }, Severity = .43f };
        var production = new HediffComp_SeverityProduct { parent = record };
        record.Comp = production; pawn.health.hediffSet.hediffs.Add(record);
        return production;
    }

    /// <summary>同一生产项只有一条缩进积累信息，悬停共用解释；不固定任何语言的正文。</summary>
    private static void AssertPnaDetail(string progress, bool disabled = false)
    {
        var toggle = Widgets.Drawn.Single(d => d.Kind == "checkbox" && d.Label == "SSC_PnaProduction_Label".Translate());
        var detail = Widgets.Drawn.Single(d => d.Kind == "label" && d.Label == (disabled ? "SSC_PnaProduction_StorageDisabled" : "SSC_PnaProduction_Storage").Translate(progress));
        Require(detail.Rect.x == toggle.Rect.x + 16 && detail.Rect.xMax == toggle.Rect.xMax && detail.Rect.y >= toggle.Rect.yMax);
        Require(Widgets.Drawn.Any(d => d.Kind == "tip" && d.Rect.x == detail.Rect.x && d.Rect.y == detail.Rect.y &&
            d.Label == "SSC_PnaProduction_Tip".Translate()));
    }
}
