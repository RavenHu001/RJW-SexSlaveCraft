// AI 辅助生成：实际双板块绘制及生产设置提交，字体与 Listing 为近似接口模型。
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    /// <summary>复用既有四语 UI 宿主，验证两个板块的真实几何、门槛和原上下文提交。</summary>
    private static void RunRomanceUiTests()
    {
        foreach (string language in Languages)
        {
            LoadLanguage(language);
            foreach (var node in XDocument.Load(SscTestPaths.Combine(repo, "Languages", language, "Keyed/SSC_Romance.xml")).Root.Elements()) Translation.Entries[node.Name.LocalName] = node.Value;
            foreach (float width in new[] { 260f, 352f, 560f })
                Run($"浪漫双板块 {language}/{width} 边界/长姓名/长译文不重叠", () =>
                {
                    var pawn = RomanceUiPawn(); pawn.LabelShort = new string('长', 40); pawn.BoundMaster.LabelShort = new string('M', 60);
                    var (height, frames) = DrawRomanceUi(pawn, width);
                    Assert(frames.Length == 2 && frames[0].Rect.yMax + 14 <= frames[1].Rect.y && frames[1].Rect.yMax <= height);
                    Assert(Widgets.Items.Count(i => i.Kind == "romance-checkbox") == 5);
                    var buttons = Widgets.Items.Where(i => i.Kind == "romance-button").ToArray();
                    Assert(buttons.Length == 3 && buttons[0].Rect.y > Widgets.Items.Where(i => i.Kind == "romance-checkbox").Max(i => i.Rect.yMax));
                    Assert(buttons[0].Rect.yMax <= buttons[1].Rect.y && buttons[1].Rect.yMax <= buttons[2].Rect.y);
                    foreach (var item in Widgets.Items.Where(i => i.Kind is "label" or "romance-checkbox" or "romance-button" or "romance-target" or "romance-surface"))
                        Assert(frames.Any(f => item.Rect.x >= f.Rect.x && item.Rect.xMax <= f.Rect.xMax + .01f && item.Rect.y >= f.Rect.y && item.Rect.yMax <= f.Rect.yMax + .01f), item.Kind + ": outside section");
                    Assert(RomanceUiOrigins.Stack.Count == 0);
                });
        }
        LoadLanguage("English");
        foreach (var node in XDocument.Load(SscTestPaths.Combine(repo, "Languages/English/Keyed/SSC_Romance.xml")).Root.Elements()) Translation.Entries[node.Name.LocalName] = node.Value;
        foreach (int stage in new[] { 1, 2, 3, 4 })
            Run("浪漫 UI 解锁行/保存值独立于灰显 " + stage, () =>
            {
                var pawn = RomanceUiPawn(stage); pawn.Training.romanceConfig = new() { allowAcceptRomance = false };
                DrawRomanceUi(pawn, 352);
                var boxes = Widgets.Items.Where(i => i.Kind == "romance-checkbox").ToArray();
                Assert(Widgets.Items.Count(i => i.Kind == "romance-target" && !i.Disabled) == (stage < 2 ? 0 : stage == 2 ? 3 : 6));
                Assert(boxes.Count(b => !b.Disabled) == (stage == 1 ? 0 : stage == 2 ? 3 : 5));
                Assert(!pawn.Training.romanceConfig.allowAcceptRomance);
                Assert(Widgets.Items.Where(i => i.Kind == "romance-button").All(i => i.Disabled == (stage < 4)));
            });
        Run("浪漫 UI 点击通过生产 TrySet 写原字段且不改其他项", () =>
        {
            var pawn = RomanceUiPawn(); Widgets.RomanceClick = 0; DrawRomanceUi(pawn, 352);
            Assert(pawn.Training.romanceConfig?.allowInitiateRomance == false && pawn.Training.romanceConfig.allowAcceptRomance);
        });
        Run("浪漫 UI 外层禁用不重新开放输入", () =>
        {
            var pawn = RomanceUiPawn(); GUI.enabled = false; Widgets.RomanceClick = 0; DrawRomanceUi(pawn, 352);
            Assert(!GUI.enabled && pawn.Training.romanceConfig == null);
        });
        Run("浪漫 UI 绘制中换人格拒绝原视图提交", () =>
        {
            var pawn = RomanceUiPawn(); Widgets.RomanceClick = 0;
            Widgets.BeforeCheckbox = () => { SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope); scope?.Dispose(); };
            DrawRomanceUi(pawn, 352); Assert(pawn.Training.romanceConfig == null);
        });
        Run("浪漫 UI 异常后列表分组和 GUI 状态恢复", () =>
        {
            var pawn = RomanceUiPawn(); Widgets.Throw = true; bool threw = false;
            try { DrawRomanceUi(pawn, 352); } catch (InvalidOperationException) { threw = true; }
            Assert(threw && RomanceUiOrigins.Stack.Count == 0 && GUI.enabled);
        });
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Run("浪漫 UI 底部按钮直接打开对应目标菜单 " + action, () =>
            {
                var pawn = RomanceUiPawn(4); Widgets.RomanceButtonClick = (int)action; DrawRomanceUi(pawn, 260);
                Assert(SSCRomanceCommandMenu.Opened.Count == 1);
                var context = SSCRomanceCommandMenu.Opened[0]; Assert(context.Actor == pawn && context.Action == action);
                Assert(SSCPersonalityRuntimeUtility.IsCurrent(pawn, context.Token));
            });
        Run("浪漫 UI 未解锁三个按钮都不能打开菜单", () =>
        {
            var pawn = RomanceUiPawn(3); Widgets.RomanceButtonClick = 0; DrawRomanceUi(pawn, 260);
            Assert(SSCRomanceCommandMenu.Opened.Count == 0);
        });
        Run("浪漫 UI 外层禁用三个命令都不能重新启用", () =>
        {
            var pawn = RomanceUiPawn(4); GUI.enabled = false; Widgets.RomanceButtonClick = 1; DrawRomanceUi(pawn, 260);
            Assert(!GUI.enabled && SSCRomanceCommandMenu.Opened.Count == 0);
        });
        Run("浪漫 UI 按钮事件换人格不重拍执行者上下文", () =>
        {
            var pawn = RomanceUiPawn(4); Widgets.RomanceButtonClick = 0;
            Widgets.BeforeRomanceButton = () => { SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope); scope?.Dispose(); };
            DrawRomanceUi(pawn, 260); Assert(SSCRomanceCommandMenu.Opened.Count == 1);
            Assert(!SSCPersonalityRuntimeUtility.IsCurrent(pawn, SSCRomanceCommandMenu.Opened[0].Token));
        });
        foreach (int targetIndex in new[] { 0, 1, 2 })
            Run("浪漫对象多选按钮只切换自身且可全部关闭 " + targetIndex, () =>
            {
                var pawn = RomanceUiPawn(2); Widgets.TargetClick = targetIndex;
                DrawRomanceUi(pawn, 260); Widgets.TargetClick = -1;
                var cfg = pawn.Training.romanceConfig;
                Assert(cfg.allowInitiateToMaster == (targetIndex != 0));
                Assert(cfg.allowInitiateToSexSlave == (targetIndex != 1));
                Assert(cfg.allowInitiateToOthers == (targetIndex != 2));
            });
        Run("浪漫对象按钮使用原人格上下文且恢复 GUI 状态", () =>
        {
            var pawn = RomanceUiPawn(2); Widgets.TargetClick = 0;
            Widgets.BeforeTargetButton = () => { SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope); scope?.Dispose(); };
            DrawRomanceUi(pawn, 260); Widgets.TargetClick = -1;
            Assert(pawn.Training.romanceConfig == null && GUI.enabled && Text.Anchor == TextAnchor.UpperLeft);
        });
        Run("浪漫对象按钮外层禁用不可提交", () =>
        {
            var pawn = RomanceUiPawn(2); Widgets.TargetClick = 0; GUI.enabled = false;
            DrawRomanceUi(pawn, 260); Widgets.TargetClick = -1;
            Assert(pawn.Training.romanceConfig == null && !GUI.enabled);
        });
        Run("浪漫对象支持累计关闭及再次开启且不改行为许可", () =>
        {
            var pawn = RomanceUiPawn(2);
            for (int i = 0; i < 3; i++) { Widgets.TargetClick = i; DrawRomanceUi(pawn, 260); }
            Widgets.TargetClick = -1;
            Assert(!pawn.Training.romanceConfig.allowInitiateToMaster && !pawn.Training.romanceConfig.allowInitiateToSexSlave && !pawn.Training.romanceConfig.allowInitiateToOthers);
            Assert(pawn.Training.romanceConfig.allowInitiateRomance && pawn.Training.romanceConfig.allowInitiateProposal);
            Widgets.TargetClick = 1; DrawRomanceUi(pawn, 260); Widgets.TargetClick = -1;
            Assert(pawn.Training.romanceConfig.allowInitiateToSexSlave && !pawn.Training.romanceConfig.allowInitiateToOthers);
        });
        Run("浪漫对象长译文自动改为纵向且按钮不重叠", () =>
        {
            const string key = "SSC_Romance_Setting_InitiateToSexSlave";
            string original = Translation.Entries[key];
            try {
                Translation.Entries[key] = new string('W', 80); DrawRomanceUi(RomanceUiPawn(2), 260);
                var targets = Widgets.Items.Where(i => i.Kind == "romance-target").Take(3).ToArray();
                Assert(targets.Length == 3 && targets[0].Rect.yMax <= targets[1].Rect.y && targets[1].Rect.yMax <= targets[2].Rect.y);
            } finally { Translation.Entries[key] = original; }
        });
        Run("其他人接受保证位于被动多选且只修改本人保存项", () =>
        {
            var pawn = RomanceUiPawn(3); Widgets.TargetClick = 5; DrawRomanceUi(pawn, 260);
            Assert(pawn.Training.romanceConfig.alwaysAcceptOthers && !pawn.Training.romanceConfig.alwaysAcceptMaster && !pawn.Training.romanceConfig.alwaysAcceptSexSlave);
            Assert(pawn.Training.romanceConfig.allowAcceptRomance && pawn.Training.romanceConfig.allowInitiateToOthers);
            Widgets.TargetClick = -1; DrawRomanceUi(pawn, 260);
            Assert(Widgets.Items.Any(i => i.Kind == "label" && i.Text == Translation.Entries["SSC_Romance_AcceptTargetTitle"]));
            Assert(Widgets.Items.Count(i => i.Kind == "romance-target") == 6);
        });
        Run("其他人接受保证低阶灰显保留显式选择且不能点击", () =>
        {
            var pawn = RomanceUiPawn(2); pawn.Training.romanceConfig = new() { alwaysAcceptOthers = true };
            Widgets.TargetClick = 5; DrawRomanceUi(pawn, 260);
            Assert(pawn.Training.romanceConfig.alwaysAcceptOthers);
            Assert(Widgets.Items.Where(i => i.Kind == "romance-target").Skip(3).All(i => i.Disabled));
        });
        Run("其他人接受保证末行使用原组上下文防止点击时换人格", () =>
        {
            var pawn = RomanceUiPawn(3); Widgets.TargetClick = 5; int seen = 0;
            Widgets.BeforeCheckbox = () => { if (++seen == 5) Widgets.BeforeTargetButton = () =>
                { SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope); scope.Dispose(); }; };
            DrawRomanceUi(pawn, 260); Assert(pawn.Training.romanceConfig == null);
        });
        foreach (int index in new[] { 0, 1, 2 })
            Run("被动多选独立切换对应保证且不改主动对象 " + index, () =>
            {
                var pawn = RomanceUiPawn(3); Widgets.TargetClick = index + 3; DrawRomanceUi(pawn, 260);
                var cfg = pawn.Training.romanceConfig;
                Assert(cfg.alwaysAcceptMaster == (index == 0) && cfg.alwaysAcceptSexSlave == (index == 1) && cfg.alwaysAcceptOthers == (index == 2));
                Assert(cfg.allowInitiateToMaster && cfg.allowInitiateToSexSlave && cfg.allowInitiateToOthers && cfg.allowAcceptRomance && cfg.allowAcceptProposal);
            });
        Run("被动多选允许全选及全不选", () =>
        {
            var pawn = RomanceUiPawn(3);
            foreach (int i in new[] { 3, 4, 5, 3, 4, 5 }) { Widgets.TargetClick = i; DrawRomanceUi(pawn, 260); }
            Assert(!pawn.Training.romanceConfig.alwaysAcceptMaster && !pawn.Training.romanceConfig.alwaysAcceptSexSlave && !pawn.Training.romanceConfig.alwaysAcceptOthers);
        });
        Run("被动多选外层禁用不提交", () =>
        {
            var pawn = RomanceUiPawn(3); Widgets.TargetClick = 5; GUI.enabled = false; DrawRomanceUi(pawn, 260);
            Assert(pawn.Training.romanceConfig == null && !GUI.enabled);
        });
        Run("被动多选长译文自动换行", () =>
        {
            const string key = "SSC_Romance_Setting_InitiateToOthers"; string old = Translation.Entries[key];
            try {
                Translation.Entries[key] = new string('W', 80); DrawRomanceUi(RomanceUiPawn(3), 260);
                var targets = Widgets.Items.Where(i => i.Kind == "romance-target").Skip(3).ToArray();
                Assert(targets.Length == 3 && targets[0].Rect.yMax <= targets[1].Rect.y && targets[1].Rect.yMax <= targets[2].Rect.y);
            } finally { Translation.Entries[key] = old; }
        });
        Widgets.RomanceClick = -1;
    }
    /// <summary>仅准备已有健康模型的链引用；生产 Resolver/Preferences 与原人格 token 实际参与绘制/提交。</summary>
    private static Pawn RomanceUiPawn(int stage = 3)
    {
        var pawn = new Pawn { BoundMaster = new Pawn() }; pawn.Training.pawnIdentity = PawnIdentity.Slave;
        pawn.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = pawn, Master = pawn.BoundMaster, Stage = stage }); return pawn;
    }
    /// <summary>只提供外层列表，行高、板块高度和边框均由生产绘制器计算。</summary>
    private static (float height, DrawItem[] frames) DrawRomanceUi(Pawn pawn, float width)
    {
        Widgets.Items.Clear(); Widgets.TargetIndex = 0; Widgets.RomanceCheckboxIndex = 0; var listing = new Listing_Standard(); listing.Begin(new Rect(0,0,width,100000));
        try { SSCBehaviorControlUI.DrawPawn(listing, pawn, true); return (listing.CurHeight, Widgets.Items.Where(i => i.Kind == "romance-frame").ToArray()); }
        finally { listing.End(); }
    }
}
