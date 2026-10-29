// AI 辅助生成：真实身体视图及浮动菜单闭包，仅隔离字体度量和选中角色输入。
using System.Collections;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static int preconceptionDrawButtons, preconceptionDrawMenus;
    private static bool preconceptionDrawActive, preconceptionDrawEnabled;
    private static Color preconceptionDrawColor;

    /// <summary>复用实际ITab私有行和菜单；不替代阶段资格、保存源或提交算法。</summary>
    private static void PreconceptionUiTests(Harmony harmony)
    {
        var tabType = typeof(ITab_SexSlaveTraining);
        var calcHeight = AccessTools.Method(typeof(Text), nameof(Text.CalcHeight), new[] { typeof(string), typeof(float) });
        var lineHeight = AccessTools.PropertyGetter(typeof(Text), nameof(Text.LineHeight));
        bool installHeight = Harmony.GetPatchInfo(calcHeight)?.Owners.Contains(harmony.Id) != true;
        bool installLine = Harmony.GetPatchInfo(lineHeight)?.Owners.Contains(harmony.Id) != true;
        if (installHeight) harmony.Patch(calcHeight, prefix: new HarmonyMethod(typeof(Program), nameof(SensitivityTextHeight)));
        if (installLine) harmony.Patch(lineHeight, prefix: new HarmonyMethod(typeof(Program), nameof(SensitivityLineHeight)));
        object Build(Pawn pawn, float width) => AccessTools.Method(tabType, "BuildBodyManagementView").Invoke(null,
            new object[] { pawn, pawn.GetComp<CompSexSlaveTraining>(), width });
        object[] Rows(object view) => ((IEnumerable)AccessTools.Field(view.GetType(), "Rows").GetValue(view)).Cast<object>().ToArray();
        object[] EnumRows(object view) => Rows(view).Where(row => SensitivityField<object>(row, "Preconception") != null).ToArray();
        List<FloatMenuOption> Options(ITab_SexSlaveTraining tab, object row) =>
            (List<FloatMenuOption>)AccessTools.Method(tabType, "BuildPreconceptionOptions").Invoke(tab,
                new[] { SensitivityField<object>(row, "Preconception") });
        GameFont originalFont = Text.Font;
        bool originalWrap = Text.WordWrap;
        try
        {
            Check("preconception native body rows retain saved values and explain locked settings", () =>
            {
                var pawn = PreconceptionPawn();
                SetPreconceptionChoices(pawn, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
                int count = pawn.health.hediffSet.hediffs.Count;
                Text.Font = GameFont.Medium; Text.WordWrap = false;
                var wide = Build(pawn, 322f);
                var narrow = Build(pawn, 180f);
                Require(SensitivityField<float>(narrow, "Height") >= SensitivityField<float>(wide, "Height"));
                Equal(GameFont.Medium, Text.Font); Require(!Text.WordWrap);
                var enumRows = EnumRows(narrow); Equal(2, enumRows.Length);
                var gender = enumRows.Single(row => !SensitivityField<bool>(SensitivityField<object>(row, "Preconception"), "Species"));
                var species = enumRows.Single(row => SensitivityField<bool>(SensitivityField<object>(row, "Preconception"), "Species"));
                Equal("SSC_Preconception_GenderFemale".Translate().ToString(),
                    SensitivityField<string>(SensitivityField<object>(gender, "Preconception"), "Button"));
                Equal("SSC_Preconception_SpeciesPartner".Translate().ToString(),
                    SensitivityField<string>(SensitivityField<object>(species, "Preconception"), "Button"));
                Require(enumRows.All(row => SensitivityField<float>(row, "Height") >= 24f));
                Require(enumRows.All(row => SensitivityField<float>(row, "Indent") > 0f));
                object Toggle(object view) => Rows(view).Select(row => SensitivityField<object>(row, "Toggle"))
                    .Single(toggle => toggle != null && SensitivityField<string>(toggle, "Id") == SSCAbilityToggleUtility.OffspringImprint);
                Require(SensitivityField<bool>(Toggle(wide), "Enabled"));
                var tab = new SensitivityTab { Selected = pawn };
                Require(Options(tab, gender).All(option => option.action != null));

                // 降阶段只暂停编辑；重建视图和禁用菜单仍显示原保存选择，不执行默认值写回。
                pawn.GetComp<CompSexSlaveTraining>().specializationProgress = .1f;
                FertilitySpecializationUtility.Sync(pawn);
                var locked = Build(pawn, 322f);
                Require(SensitivityField<bool>(Toggle(locked), "Enabled"));
                Require(!SensitivityField<bool>(Toggle(locked), "CanEdit"));
                Require(!string.IsNullOrEmpty(SensitivityField<string>(Toggle(locked), "ChangeReason")));
                Require(EnumRows(locked).All(row => Options(tab, row).All(option => option.action == null && option.tooltip.HasValue)));
                var record = SSCUterusDevelopmentUtility.Find(pawn);
                Equal(SSCOffspringGender.Female, record.OffspringGender);
                Equal(SSCOffspringSpeciesSource.Partner, record.OffspringSpeciesSource);
                Require(record.OffspringImprintEnabled);
                Equal(count, pawn.health.hediffSet.hediffs.Count);
            });
            Check("preconception native enum menu applies original selection and rejects a changed selected pawn", () =>
            {
                var pawn = PreconceptionPawn();
                var tab = new SensitivityTab { Selected = pawn };
                var row = EnumRows(Build(pawn, 322f)).Single(item =>
                    !SensitivityField<bool>(SensitivityField<object>(item, "Preconception"), "Species"));
                var options = Options(tab, row);
                Equal(3, options.Count); Require(options.All(option => option.action != null));
                options[2].action();
                Equal(SSCOffspringGender.Female, SSCUterusDevelopmentUtility.Find(pawn).OffspringGender);
                // 菜单已形成后改选角色；实际旧闭包只能拒绝，不能给新身体重新拍授权凭据。
                var other = PreconceptionPawn(); tab.Selected = other;
                options[1].action();
                Equal(SSCOffspringGender.Female, SSCUterusDevelopmentUtility.Find(pawn).OffspringGender);
                Equal(SSCOffspringGender.Natural, SSCUterusDevelopmentUtility.Find(other).OffspringGender);
            });
            Check("preconception native drawing disables and dims buttons without opening menus or leaking GUI state", () =>
            {
                // 只隔离标签、按钮和悬停的引擎末端；DrawPreconceptionRow及共用按钮仍执行生产代码。
                var boundaries = new[]
                {
                    (Method: AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) }), Prefix: nameof(PreconceptionDrawSkip)),
                    (Method: AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) }), Prefix: nameof(PreconceptionDrawButton)),
                    (Method: AccessTools.Method(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion), new[] { typeof(Rect), typeof(TipSignal) }), Prefix: nameof(PreconceptionDrawSkip)),
                    (Method: AccessTools.PropertyGetter(typeof(Find), nameof(Find.WindowStack)), Prefix: nameof(PreconceptionUnexpectedMenu))
                };
                var draw = AccessTools.Method(tabType, "DrawPreconceptionRow");
                bool priorEnabled = GUI.enabled, priorWrap = Text.WordWrap;
                Color priorColor = GUI.color;
                TextAnchor priorAnchor = Text.Anchor;
                GameFont priorFont = Text.Font;
                foreach (var boundary in boundaries)
                    harmony.Patch(boundary.Method, prefix: new HarmonyMethod(typeof(Program), boundary.Prefix));
                try
                {
                    void DrawDisabled(bool outerEnabled, float progress)
                    {
                        var pawn = PreconceptionPawn(progress);
                        var tab = new SensitivityTab { Selected = pawn };
                        var row = EnumRows(Build(pawn, 322f)).Single(item =>
                            !SensitivityField<bool>(SensitivityField<object>(item, "Preconception"), "Species"));
                        preconceptionDrawButtons = preconceptionDrawMenus = 0;
                        var original = new Color(.8f, .6f, .4f, .7f);
                        GUI.enabled = outerEnabled; GUI.color = original;
                        Text.Anchor = TextAnchor.UpperRight; Text.Font = GameFont.Medium; Text.WordWrap = false;
                        draw.Invoke(tab, new[] { (object)new Rect(16f, 20f, 280f, SensitivityField<float>(row, "Height")), row });
                        Equal(1, preconceptionDrawButtons); Equal(0, preconceptionDrawMenus);
                        Require(!preconceptionDrawActive && !preconceptionDrawEnabled);
                        Near(original.r * .55f, preconceptionDrawColor.r); Near(original.g * .55f, preconceptionDrawColor.g);
                        Near(original.b * .55f, preconceptionDrawColor.b); Near(original.a, preconceptionDrawColor.a);
                        Equal(outerEnabled, GUI.enabled);
                        Near(original.r, GUI.color.r); Near(original.g, GUI.color.g); Near(original.b, GUI.color.b); Near(original.a, GUI.color.a);
                        Equal(TextAnchor.UpperRight, Text.Anchor); Equal(GameFont.Medium, Text.Font); Require(!Text.WordWrap);
                        Equal(SSCOffspringGender.Natural, SSCUterusDevelopmentUtility.Find(pawn).OffspringGender);
                    }
                    // 阶段不足与外层GUI关闭是不同拒绝来源，均不能被底层true返回绕过。
                    DrawDisabled(true, .1f);
                    DrawDisabled(false, .999f);
                }
                finally
                {
                    foreach (var boundary in boundaries) harmony.Unpatch(boundary.Method, HarmonyPatchType.All, harmony.Id);
                    GUI.enabled = priorEnabled; GUI.color = priorColor;
                    Text.Anchor = priorAnchor; Text.Font = priorFont; Text.WordWrap = priorWrap;
                }
            });
        }
        finally
        {
            Text.Font = originalFont; Text.WordWrap = originalWrap;
            if (installHeight) harmony.Unpatch(calcHeight, HarmonyPatchType.All, harmony.Id);
            if (installLine) harmony.Unpatch(lineHeight, HarmonyPatchType.All, harmony.Id);
        }
    }

    /// <summary>禁用真实图形与鼠标悬停末端，不替代行布局、可编辑资格或提交逻辑。</summary>
    private static bool PreconceptionDrawSkip() => false;

    /// <summary>记录共用按钮交给原版的active与颜色，并故意误报点击以验证最终禁用保护。</summary>
    private static bool PreconceptionDrawButton(bool __4, ref bool __result)
    {
        preconceptionDrawButtons++;
        preconceptionDrawActive = __4; preconceptionDrawEnabled = GUI.enabled; preconceptionDrawColor = GUI.color;
        __result = true;
        return false;
    }

    /// <summary>禁用行不应访问窗口栈；一旦进入菜单分支即明确失败，避免依赖完整游戏窗口根。</summary>
    private static bool PreconceptionUnexpectedMenu()
    {
        preconceptionDrawMenus++;
        throw new InvalidOperationException("Disabled preconception row attempted to open a menu.");
    }
}
