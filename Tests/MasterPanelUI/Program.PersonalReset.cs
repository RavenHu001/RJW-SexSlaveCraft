// AI 辅助生成：实际标题绘制与浪漫提交；字体和性行为后台是明确的 UI 边界。
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;
using UnityEngine;

internal static partial class Program
{
    /// <summary>与面板同批读取四语恢复按钮，保留旧资源键数断言。</summary>
    private static void LoadResetLanguage(string language)
    {
        foreach (var node in XDocument.Load(SscTestPaths.Combine(repo, "Languages", language, "Keyed/SSC_BehaviorReset.xml")).Root.Elements())
            Translation.Entries[node.Name.LocalName] = node.Value;
    }

    /// <summary>检验标题同行、窄栏换行、输入灰显及个人提交，不假定真实引擎像素尺寸。</summary>
    private static void RunPersonalResetUiTests()
    {
        foreach (var language in Languages)
        foreach (float width in new[] { 260f, 352f, 560f })
            Run($"个人恢复标题同行 {language}/{width}", () =>
            {
                LoadLanguage(language); LoadResetLanguage(language);
                foreach (var node in XDocument.Load(SscTestPaths.Combine(repo, "Languages", language, "Keyed/SSC_Romance.xml")).Root.Elements()) Translation.Entries[node.Name.LocalName] = node.Value;
                var (_, frames) = DrawRomanceUi(RomanceUiPawn(), width);
                var buttons = Widgets.Items.Where(i => i.Kind == "reset-button").ToArray(); Assert(buttons.Length == 2);
                for (int i = 0; i < 2; i++)
                {
                    var title = Widgets.Items.Single(item => item.Kind == "label" && item.Text == Translation.Translate(i == 0 ? "SSC_Romance_SexSection" : "SSC_Romance_RomanceSection"));
                    Assert(Math.Abs(title.Rect.y - buttons[i].Rect.y) < .01f);
                    Assert(title.Rect.xMax + 7f <= buttons[i].Rect.x && buttons[i].Rect.xMax <= frames[i].Rect.xMax);
                    Assert(buttons[i].Rect.height >= Text.CalcHeight(buttons[i].Text, buttons[i].Rect.width - 12f));
                    Assert(buttons[i].Disabled);
                }
            });
        Run("个人恢复浪漫整份恢复且独立于性行为按钮", () =>
        {
            LoadResetLanguage("English");
            var p = RomanceUiPawn(); p.Training.romanceConfig = new() { allowInitiateRomance = false, alwaysAcceptOthers = true };
            Widgets.ResetButtonClick = 1; DrawRomanceUi(p, 260);
            Assert(p.Training.romanceConfig.IsDefault && SSCRestrictionEditor.Resets == 0);
        });
        Run("个人恢复性行为只向对应后台分发", () =>
        {
            var p = RomanceUiPawn(); var config = p.Training.romanceConfig = new() { allowInitiateRomance = false };
            SSCRestrictionEditor.CanReset = true; Widgets.ResetButtonClick = 0; DrawRomanceUi(p, 260);
            Assert(SSCRestrictionEditor.Resets == 1 && p.Training.romanceConfig == config && !config.IsDefault);
        });
        Run("个人恢复外层禁用不重新开放按钮", () =>
        {
            var p = RomanceUiPawn(); var c = p.Training.romanceConfig = new() { allowInitiateRomance = false };
            GUI.enabled = false; Widgets.ResetButtonClick = 1; DrawRomanceUi(p, 260);
            Assert(p.Training.romanceConfig == c && !GUI.enabled);
        });
        Run("个人恢复绘制后换人格拒绝旧按钮", () =>
        {
            var p = RomanceUiPawn(); var c = p.Training.romanceConfig = new() { allowInitiateRomance = false };
            Widgets.ResetButtonClick = 1;
            Widgets.BeforeResetButton = () => { SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope); scope.Dispose(); };
            DrawRomanceUi(p, 260);
            Assert(p.Training.romanceConfig == c && GUI.enabled);
        });
        Run("个人恢复长按钮译文仍与标题分区且保持外层颜色", () =>
        {
            LoadResetLanguage("English"); const string key = "SSC_BehaviorReset_Button"; var old = Translation.Entries[key];
            try
            {
                Translation.Entries[key] = new string('W', 70); GUI.color = new Color(.7f, .6f, .5f);
                Text.WordWrap = false; Text.Anchor = TextAnchor.MiddleRight;
                var (_, frames) = DrawRomanceUi(RomanceUiPawn(), 260);
                var buttons = Widgets.Items.Where(i => i.Kind == "reset-button").ToArray();
                Assert(buttons.Length == 2 && buttons[0].Rect.yMax < frames[0].Rect.yMax && frames[0].Rect.yMax < frames[1].Rect.y);
                Assert(Near(GUI.color.r, .7f) && Near(GUI.color.g, .6f) && Near(GUI.color.b, .5f));
                Assert(!Text.WordWrap && Text.Anchor == TextAnchor.MiddleRight);
            }
            finally { Translation.Entries[key] = old; }
        });
    }
}
