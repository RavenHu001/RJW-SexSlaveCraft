using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    // SSC 规则直接链接生产文件；下面只准备原版边界状态并断言结果，不复制授予逻辑。
    private static int passed, failed;
    private static string root;
    private static ResearchManager Research => Find.ResearchManager;
    private static ResearchProjectDef Project => SSCDefOf.SSC_BasicTraining;
    /// <summary>每项用例重建游戏、定义和边界记录，避免前一项授予或 GUI 状态污染下一项。</summary>
    private static void Reset()
    {
        Current.Game = new Game(); Find.CurrentMap = new Map(); Find.Selector = new Selector();
        ModLister.RoyaltyInstalled = true;
        SSCDefOf.SSC_MasterQualificationItem = new ThingDef { defName = "SSC_MasterQualificationItem" };
        SSCDefOf.SSC_BasicTraining = new ResearchProjectDef { defName = "SSC_RES_BasicTraining" };
        SSCDefOf.SSC_PersonalityExcreted_Done = new HediffDef();
        SSCMod.settings = new SSCSettings();
        Thing.DestroyedUnits = 0; Messages.Records.Clear(); ThingMaker.Calls = 0; ThingMaker.LastCreated = null;
        GenPlace.Calls = 0; GenPlace.Fail = false; GenPlace.MergeInto = null;
        GUI.enabled = true; Widgets.Click = false; TooltipHandler.LastTip = null;
    }
    /// <summary>默认创建当前版本、未解锁的有效角色，可指定 SSC 身份以验证道具准入。</summary>
    private static Pawn Pawn(PawnIdentity identity = PawnIdentity.Unset)
    {
        var pawn = new Pawn(); pawn.Training.pawnIdentity = identity; return pawn;
    }
    private static ThingWithComps Item(int count = 2) => new ThingWithComps { def = SSCDefOf.SSC_MasterQualificationItem, stackCount = count };
    private static bool Use(Pawn pawn, Thing item) => SSCMasterQualificationUtility.TryUseQualificationItem(pawn, item, out _);
    private static void Check(bool condition, string message = "assertion failed") { if (!condition) throw new Exception(message); }
    /// <summary>逐项隔离并记录失败；单项失败后继续检查其他行为，汇总供统一门禁解析。</summary>
    private static void Run(string name, Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        Reset();
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    /// <summary>同时检查菜单判定与最终服务拒绝，并验证资格、版本、物品和研究没有副作用。</summary>
    private static void Reject(Pawn pawn, Thing item, string reason)
    {
        int count = item?.stackCount ?? 0;
        int version = pawn?.Training?.masterQualificationVersion ?? 0;
        bool unlocked = pawn?.Training?.masterQualificationUnlocked == true;
        var report = SSCMasterQualificationUtility.CanUseQualificationItem(pawn, item);
        Check(!report.Accepted && report.Reason == reason, "wrong rejection");
        Check(!SSCMasterQualificationUtility.TryUseQualificationItem(pawn, item, out string finalReason) && finalReason == reason);
        Check((item?.stackCount ?? 0) == count && Thing.DestroyedUnits == 0);
        if (pawn?.Training != null) Check(pawn.Training.masterQualificationVersion == version && pawn.Training.masterQualificationUnlocked == unlocked);
        if (Current.Game?.researchManager != null) Check(Research.AddCalls == 0);
    }
    // 反射调用实际设置按钮方法；无需为测试把生产私有绘制入口改为公开 API。
    private static void DrawButton() => typeof(SSCMod).GetMethod("DrawMasterQualificationDebugEntry", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new object[] { new Listing_Standard() });
    /// <summary>验证道具、研究边界、最终复查、调试按钮及定义/翻译；原版引擎专项留待实机。</summary>
    private static int Main(string[] args)
    {
        root = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        // 基础授予与准入：身份设置不能代替永久资格，重复及无效使用不能消耗物品。
        Run("首次使用授资格补原版蓝图只消耗一个，身份研究进度保持", () => {
            var pawn = Pawn(); var item = Item(4);
            Check(Use(pawn, item));
            Check(pawn.Training.masterQualificationUnlocked && pawn.Training.masterQualificationVersion == 1 && pawn.Training.pawnIdentity == PawnIdentity.Unset);
            Check(item.stackCount == 3 && !item.Destroyed && Thing.DestroyedUnits == 1);
            Check(Research.AddCalls == 1 && Project.TechprintsApplied == 1 && Project.ProgressReal == 0 && !Project.IsFinished);
            SSCMod.settings.allowDirectMasterSelection = false; Check(SSCMasterQualificationUtility.CanSelectMaster(pawn));
        });
        Run("最后一个模块整体消耗，无重复销毁", () => {
            var pawn = Pawn(); var item = Item(1); Check(Use(pawn, item));
            Check(item.Destroyed && Thing.DestroyedUnits == 1 && pawn.Training.masterQualificationUnlocked);
        });
        Run("SSC 性奴拒绝且不写版本或资格", () => Reject(Pawn(PawnIdentity.Slave), Item(), "SSC_MasterQualification_Slave"));
        Run("原版奴隶不是 SSC 性奴时可以使用", () => { var pawn = Pawn(); pawn.IsSlave = true; Check(Use(pawn, Item())); });
        Run("重复使用拒绝，保留资格与物品", () => {
            var pawn = Pawn(); pawn.Training.ReplaceMasterQualification(true);
            Reject(pawn, Item(), "SSC_MasterQualification_AlreadyUnlocked");
        });
        Run("手动已选主人但无资格仍能使用，不自动切换身份", () => {
            foreach (bool setting in new[] { false, true }) {
                SSCMod.settings.allowDirectMasterSelection = setting;
                var pawn = Pawn(PawnIdentity.Master); Check(Use(pawn, Item()));
                Check(pawn.Training.masterQualificationUnlocked && pawn.Training.pawnIdentity == PawnIdentity.Master);
            }
        });
        Run("无 SSC 组件角色拒绝", () => { var pawn = Pawn(); pawn.Training = null; Reject(pawn, Item(), "SSC_MasterQualification_InvalidPawn"); });
        Run("无效角色、死亡、销毁、非人形与空壳拒绝", () => {
            Reject(null, Item(), "SSC_MasterQualification_InvalidPawn");
            foreach (Action<Pawn> mutate in new Action<Pawn>[] {
                p => p.Dead = true, p => p.Destroyed = true, p => p.RaceProps.Humanlike = false,
                p => p.RaceProps = null, p => p.health.hediffSet.Values.Add(SSCDefOf.SSC_PersonalityExcreted_Done)
            }) { var pawn = Pawn(); mutate(pawn); Reject(pawn, Item(), "SSC_MasterQualification_InvalidPawn"); }
        });
        Run("损坏身份不绕过性奴条件", () => Reject(Pawn((PawnIdentity)99), Item(), "SSC_MasterQualification_InvalidPawn"));
        Run("空、销毁、空堆叠与错误种类道具拒绝", () => {
            Reject(Pawn(), null, "SSC_MasterQualification_InvalidItem");
            var destroyed = Item(); destroyed.Destroyed = true; Reject(Pawn(), destroyed, "SSC_MasterQualification_InvalidItem");
            Reject(Pawn(), Item(0), "SSC_MasterQualification_InvalidItem");
            Reject(Pawn(), new ThingWithComps { def = new ThingDef() }, "SSC_MasterQualification_InvalidItem");
        });
        Run("缺必要 Def 时没有研究、资格或消耗副作用", () => {
            var item = Item(); SSCDefOf.SSC_MasterQualificationItem = null;
            Reject(Pawn(), item, "SSC_MasterQualification_Unavailable");
            SSCDefOf.SSC_MasterQualificationItem = item.def; SSCDefOf.SSC_BasicTraining = null;
            Reject(Pawn(), item, "SSC_MasterQualification_Unavailable");
        });
        Run("主菜单与缺研究管理器安全拒绝", () => {
            var item = Item(); Current.Game = null; Reject(Pawn(), item, "SSC_MasterQualification_Unavailable");
            Current.Game = new Game { researchManager = null }; Reject(Pawn(), item, "SSC_MasterQualification_Unavailable");
        });
        // 研究边界：只补计数缺口，研究完成度不变，DLC 缺失时仍可独立授予个人资格。
        Run("无 Royalty 仅授资格，不创建蓝图或推进研究", () => {
            ModLister.RoyaltyInstalled = false;
            var pawn = Pawn(); Check(Use(pawn, Item()));
            Check(pawn.Training.masterQualificationUnlocked && Research.AddCalls == 0 && Research.Techprints.Count == 0 && Project.ProgressReal == 0);
        });
        Run("只补缺少蓝图，不越过需求", () => {
            Project.techprintCount = 3; Research.Techprints[Project] = 1;
            Check(Use(Pawn(), Item())); Check(Research.LastAmount == 2 && Project.TechprintsApplied == 3);
        });
        Run("原版蓝图已够时仍授个人资格，不重复加蓝图", () => {
            Research.Techprints[Project] = 1; Check(Use(Pawn(), Item())); Check(Research.AddCalls == 0 && Project.TechprintsApplied == 1);
        });
        Run("两名角色分别授资格，第二次不影响已有研究", () => {
            var first = Pawn(); var second = Pawn(); var item = Item(3);
            Check(Use(first, item)); Research.Progress[Project] = 200;
            Check(Use(second, item)); Check(first.Training.masterQualificationUnlocked && second.Training.masterQualificationUnlocked);
            Check(Research.AddCalls == 1 && Project.ProgressReal == 200 && item.stackCount == 1 && Thing.DestroyedUnits == 2);
        });
        Run("零进度、部分研究与已完成研究保持原进度", () => {
            foreach (float progress in new[] { 0f, 250f, 500f }) {
                Reset(); Research.Progress[Project] = progress;
                Check(Use(Pawn(), Item())); Check(Project.ProgressReal == progress && Project.IsFinished == (progress == 500f));
            }
        });
        Run("研究边界失败发生在个人授予和消耗之前", () => {
            var pawn = Pawn(); var item = Item(); Research.ThrowOnAdd = true;
            bool threw = false; try { Use(pawn, item); } catch (InvalidOperationException) { threw = true; }
            Check(threw && !pawn.Training.masterQualificationUnlocked && item.stackCount == 2 && Thing.DestroyedUnits == 0);
        });
        // 最终复查与提示：菜单通过后改变条件，再直接执行生产效果，检查拒绝和实际消耗。
        Run("菜单通过后变为 SSC 性奴，真实效果最终复查拒绝", () => {
            var pawn = Pawn(); var item = Item(); var effect = new CompUseEffect_MasterQualification { parent = item };
            Check(effect.CanBeUsedBy(pawn).Accepted); pawn.Training.pawnIdentity = PawnIdentity.Slave; effect.DoEffect(pawn);
            Check(!pawn.Training.masterQualificationUnlocked && item.stackCount == 2 && Research.AddCalls == 0 && Thing.DestroyedUnits == 0);
            Check(Messages.Records.Single().type == MessageTypeDefOf.RejectInput);
        });
        Run("菜单通过后其他入口取得资格或物品销毁，不再消耗", () => {
            var pawn = Pawn(); var item = Item(); var effect = new CompUseEffect_MasterQualification { parent = item };
            Check(effect.CanBeUsedBy(pawn).Accepted); pawn.Training.ReplaceMasterQualification(true); effect.DoEffect(pawn);
            Check(item.stackCount == 2 && Research.AddCalls == 0 && Thing.DestroyedUnits == 0);
            pawn.Training.ReplaceMasterQualification(false); item.Destroyed = true; effect.DoEffect(pawn);
            Check(!pawn.Training.masterQualificationUnlocked && Thing.DestroyedUnits == 0);
        });
        Run("成功提示只在实际授予和消耗后发出", () => {
            var pawn = Pawn(); var item = Item(); var effect = new CompUseEffect_MasterQualification { parent = item };
            effect.DoEffect(pawn); Check(item.stackCount == 1 && pawn.Training.masterQualificationUnlocked);
            Check(Messages.Records.Single().type == MessageTypeDefOf.PositiveEvent && Messages.Records[0].target == pawn);
            effect.DoEffect(pawn); Check(Messages.Records.Count == 2 && Messages.Records[1].type == MessageTypeDefOf.RejectInput && item.stackCount == 1);
        });
        Run("读取使用条件不改版本、资格或研究", () => {
            var pawn = Pawn(PawnIdentity.Master); pawn.Training.masterQualificationVersion = 0;
            Check(SSCMasterQualificationUtility.CanUseQualificationItem(pawn, Item()).Accepted);
            Check(pawn.Training.masterQualificationVersion == 0 && !pawn.Training.masterQualificationUnlocked && Research.AddCalls == 0);
        });
        // 设置与放置：直接绘制真实按钮，验证禁用状态、GUI 恢复、位置和失败清理。
        Run("调试按钮无游戏、地图或 Def 时禁用且不生成", () => {
            Widgets.Click = true; Current.Game = null; DrawButton(); Check(ThingMaker.Calls == 0 && GUI.enabled);
            Current.Game = new Game(); Find.CurrentMap = null; DrawButton(); Check(ThingMaker.Calls == 0 && GUI.enabled);
            Find.CurrentMap = new Map(); SSCDefOf.SSC_MasterQualificationItem = null; DrawButton(); Check(ThingMaker.Calls == 0 && GUI.enabled);
            Check(TooltipHandler.LastTip == "SSC_MasterQualification_DebugUnavailable");
        });
        Run("调试按钮尊重外层禁用状态并恢复 GUI", () => {
            GUI.enabled = false; Widgets.Click = true; DrawButton(); Check(!GUI.enabled && ThingMaker.Calls == 0);
        });
        Run("调试生成恰好一个，选中同图对象附近放置不授资格", () => {
            var pawn = Pawn(); pawn.Map = Find.CurrentMap; pawn.Position = new IntVec3(11, 20);
            Find.Selector.SingleSelectedThing = pawn; Widgets.Click = true; DrawButton();
            Check(ThingMaker.Calls == 1 && ThingMaker.LastCreated.stackCount == 1 && GenPlace.LastCenter.x == 11 && GenPlace.LastCenter.z == 20);
            Check(!pawn.Training.masterQualificationUnlocked && Research.AddCalls == 0 && Messages.Records.Single().target == ThingMaker.LastCreated);
        });
        Run("未选中、离图或异图对象使用当前地图中心", () => {
            foreach (Thing selected in new Thing[] { null, new Thing(), new Thing { Map = new Map(), Position = new IntVec3(9, 9) } }) {
                Check(SSCMasterQualificationDebugUtility.TrySpawn(Find.CurrentMap, selected, out var spawned));
                Check(spawned.stackCount == 1 && GenPlace.LastCenter.x == 50 && GenPlace.LastCenter.z == 50);
            }
        });
        Run("调试放置失败清理未放置物品，显示失败而非成功", () => {
            GenPlace.Fail = true; Widgets.Click = true; DrawButton();
            Check(ThingMaker.LastCreated.Destroyed && Messages.Records.Single().type == MessageTypeDefOf.RejectInput);
            Check(!SSCMasterQualificationDebugUtility.TrySpawn(null, null, out var spawned) && spawned == null);
        });
        Run("调试模块并入现有堆叠时报告实际放置对象", () => {
            GenPlace.MergeInto = Item(3); Widgets.Click = true; DrawButton();
            Check(GenPlace.MergeInto.stackCount == 4 && Messages.Records.Single().target == GenPlace.MergeInto);
        });
        // 配套资源：XML 只负责连接原版任务与 SSC 效果，四语覆盖玩家能看到的全部入口。
        Run("道具始终加载、原版 UseItem 与唯一效果控制消耗", ItemDefinition);
        Run("研究只对 Royalty 加蓝图条件，不新增全局迁移或替换原版蓝图", ResearchDefinition);
        Run("四语物品、按钮、拒绝和提示完整且镜像一致", Translations);
        RunAcquisitionDefinitions();
        Console.WriteLine($"{passed}/{passed + failed} passed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>检查实际道具定义的任务、唯一结算效果、原版分类及注册/设置接入。</summary>
    private static void ItemDefinition()
    {
        var def = XDocument.Load(SscTestPaths.Combine(root, "Defs/ThingsDefs/SSC_MasterQualificationItem.xml")).Root.Element("ThingDef");
        Check(def.Attribute("MayRequire") == null && (string)def.Element("thingClass") == "ThingWithComps");
        var comps = def.Element("comps").Elements("li").ToArray();
        Check(comps.Length == 3 && comps[0].Attribute("Class").Value == "CompProperties_Usable");
        Check((string)comps[0].Element("useJob") == "UseItem" && (int)comps[0].Element("useDuration") == 600);
        Check(comps[1].Attribute("Class").Value == "CompProperties_UseEffect" && (string)comps[1].Element("compClass") == "SexSlaveCraft.CompUseEffect_MasterQualification");
        Check(comps[2].Attribute("Class").Value == "CompProperties_Forbiddable");
        Check(!def.ToString().Contains("DestroySelf") && !def.ToString().Contains("CompProperties_Techprint"));
        Check((int)def.Element("stackLimit") > 1 && (float)def.Element("statBases").Element("MarketValue") > 0);
        Check((string)def.Element("thingCategories").Element("li") == "ItemsMisc", "invalid core item category");
        var source = File.ReadAllText(SscTestPaths.Combine(root, "Sexslavecraft/common/StringAndDefOf.cs"));
        Check(source.Contains("SSC_MasterQualificationItem = GetDef<ThingDef>(\"SSC_MasterQualificationItem\")"));
        Check(File.ReadAllText(SscTestPaths.Combine(root, "Sexslavecraft/common/Settings.cs")).Contains("DrawMasterQualificationDebugEntry(listingStandard)"));
    }
    /// <summary>检查 Royalty 条件字段，确保 SSC 没有定向替换暂时保留的原版自动蓝图。</summary>
    private static void ResearchDefinition()
    {
        var def = XDocument.Load(SscTestPaths.Combine(root, "Defs/ResearchDefs/ResearchProjectDef.xml")).Root.Elements("ResearchProjectDef")
            .Single(x => (string)x.Element("defName") == "SSC_RES_BasicTraining");
        var count = def.Element("techprintCount");
        Check((int)count == 1 && (string)count.Attribute("MayRequire") == "Ludeon.RimWorld.Royalty");
        Check((float)def.Element("baseCost") == 500);
        Check(def.Element("heldByFactionCategoryTags").Elements("li").Select(x => x.Value).SequenceEqual(new[] { "Empire", "Outlander" }));
        foreach (string file in Directory.GetFiles(SscTestPaths.Combine(root, "Sexslavecraft"), "*.cs", SearchOption.AllDirectories))
            if (!file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                Check(!File.ReadAllText(file).Contains("Techprint_SSC_RES_BasicTraining"), "generated vanilla blueprint was adapted");
    }
    /// <summary>核对四语必需键、姓名占位符、物品注入路径及源码镜像。</summary>
    private static void Translations()
    {
        string[] keys = { "InvalidPawn", "Slave", "AlreadyUnlocked", "Unavailable", "InvalidItem", "Granted", "DebugSpawn", "DebugSpawnTip", "DebugUnavailable", "DebugSpawned", "DebugSpawnFailed" };
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" }) {
            string keyed = $"Languages/{language}/Keyed/SSC_MasterQualification.xml";
            string injected = $"Languages/{language}/DefInjected/ThingDef/SSC_MasterQualificationItem.xml";
            var keyData = XDocument.Load(SscTestPaths.Combine(root, keyed)).Root;
            foreach (string key in keys) Check(!string.IsNullOrWhiteSpace(keyData.Element("SSC_MasterQualification_" + key)?.Value));
            Check(keyData.Element("SSC_MasterQualification_Granted").Value.Contains("{0}"));
            var itemData = XDocument.Load(SscTestPaths.Combine(root, injected)).Root;
            foreach (string key in new[] { "label", "description", "comps.0.useLabel" })
                Check(!string.IsNullOrWhiteSpace(itemData.Element("SSC_MasterQualificationItem." + key)?.Value));
            foreach (string relative in new[] { keyed, injected })
                Check(File.ReadAllText(SscTestPaths.Combine(root, relative)) == File.ReadAllText(SscTestPaths.Combine(root, "Sexslavecraft", relative)));
        }
    }
}
