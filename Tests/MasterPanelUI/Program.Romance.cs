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
                    Assert(Widgets.Items.Count(i => i.Kind == "romance-checkbox") == 7);
                    foreach (var item in Widgets.Items.Where(i => i.Kind is "label" or "romance-checkbox" or "romance-button"))
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
                Assert(boxes.Count(b => !b.Disabled) == (stage == 1 ? 0 : stage == 2 ? 3 : 7));
                Assert(!pawn.Training.romanceConfig.allowAcceptRomance);
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
        Widgets.Items.Clear(); Widgets.RomanceCheckboxIndex = 0; var listing = new Listing_Standard(); listing.Begin(new Rect(0,0,width,100000));
        try { SSCBehaviorControlUI.DrawPawn(listing, pawn, true); return (listing.CurHeight, Widgets.Items.Where(i => i.Kind == "romance-frame").ToArray()); }
        finally { listing.End(); }
    }
}
