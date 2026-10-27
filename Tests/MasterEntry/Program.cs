using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using RimWorld;
using RimWorld.QuestGen;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed, failed;
    private static string rootPath;
    private static SSCMasterEntryGameComponent Entry => Current.Game.Entry;
    private static Quest LastQuest => Find.QuestManager.QuestsListForReading.Last();
    private static void Check(bool condition, string reason = "assertion failed")
    { if (!condition) throw new InvalidOperationException(reason); }

    /// <summary>每组独立创建游戏、任务、Pawn 与故障边界，避免静态宿主污染多存档结论。</summary>
    private static void Reset()
    {
        // 重建游戏和基地，组件按真实构造签名接收所属游戏，便于验证旧回调的存档隔离。
        Faction.OfPlayer = new Faction();
        Current.Game = new Game();
        Current.Game.Entry = new SSCMasterEntryGameComponent(Current.Game);
        Find.CurrentMap = new Map(); Current.Game.Maps.Add(Find.CurrentMap);
        Find.SignalManager = new SignalManager();
        // Def 指向本套件链接的生产任务根；没有另写一套获取/投放业务来代替生产逻辑。
        SSCDefOf.SSC_MasterQualificationItem = new ThingDef { defName = "SSC_MasterQualificationItem" };
        SSCDefOf.SSC_BasicTraining = new ResearchProjectDef();
        SSCDefOf.SSC_PersonalityExcreted_Done = new HediffDef();
        SSCDefOf.SSC_InitialMasterQualification = new QuestScriptDef
        { defName = SSCMasterEntryUtility.QuestDefName, root = new QuestNode_Root_MasterQualification() };
        // 重置加载与保存宿主，记录 Pawn 扫描次数，区分首次自动条件查询和周期重试。
        SSCRestrictionGameComponent.Pawns.Clear(); SSCRestrictionGameComponent.Scans = 0;
        LongEventHandler.Queue.Clear();
        LongEventHandler.Immediate = false;
        Scribe.mode = LoadSaveMode.Inactive; Scribe.Data.Clear();
        // 每组先清空生成、登记、信件和投放的故障注入及计数，避免上一组影响下一组结果。
        QuestGen.Calls = 0; QuestGen.Fail = false;
        QuestPart_DropPods.Calls = 0; QuestPart_DropPods.DeliveredUnits = 0; QuestPart_DropPods.Fail = false;
        QuestPart_DropPods.DeliveryLetters.Clear();
        QuestUtility.Letters = 0; QuestUtility.ThrowLetter = false;
        Log.Errors.Clear(); Messages.Text.Clear();
        // GUI 点击宿主可在绘制中注入状态变化，专门验证按钮点击后的生产复查。
        UnityEngine.GUI.enabled = true; Widgets.Click = true; Widgets.BeforeReturn = null; TooltipHandler.LastTip = null;
    }
    /// <summary>隔离每组状态，失败仍继续运行后续用例，最终统一报告实际通过数量。</summary>
    private static void Run(string name, Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        Reset();
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    // 先执行长事件结束，再模拟一次运行 Update；初始化不要求游戏推进 Tick。
    private static void Init() { Entry.FinalizeInit(); LongEventHandler.Finish(); Entry.GameComponentUpdate(); }
    /// <summary>保存生产组件输出的字段，返回独立字典；原版跨引用解析不由这里模拟。</summary>
    private static Dictionary<string, object> Save(GameComponent component)
    {
        Scribe.Data = new(); Scribe.mode = LoadSaveMode.Saving; component.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        return new Dictionary<string, object>(Scribe.Data);
    }
    // 只模拟字段保存/恢复：原版任务与物品跨引用的真实往返另列实机步骤。
    private static void Reload(Dictionary<string, object> data = null)
    {
        // 新建组件以清空 ready 等运行期字段，再把保存值交回实际 ExposeData 加载。
        data ??= Save(Entry);
        Current.Game.Entry = new SSCMasterEntryGameComponent(Current.Game);
        Scribe.Data = data; Scribe.mode = LoadSaveMode.LoadingVars; Entry.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        Init();
    }
    /// <summary>只设置当前身份，不预置永久资格，验证手动主人也属于自动检测范围。</summary>
    private static Pawn Master()
    {
        var pawn = new Pawn { Faction = Faction.OfPlayer };
        pawn.Training.pawnIdentity = PawnIdentity.Master;
        return pawn;
    }
    private static void Request()
    { Check(Entry.TryRequestManually(out string reason), reason); }
    private static QuestPart_DropPods Drop(Quest quest) => quest.PartsListForReading.OfType<QuestPart_DropPods>().Single();
    private static QuestPart_MasterQualificationDelivery Delivery(Quest quest) => quest.PartsListForReading.OfType<QuestPart_MasterQualificationDelivery>().Single();
    private static void Draw()
    {
        // 反射调用真实私有绘制入口，不为测试扩大生产 API，也不重复实现按钮逻辑。
        typeof(SSCMod).GetMethod("DrawMasterEntryDebugEntry", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { new Listing_Standard() });
    }
    private static int Main(string[] args)
    {
        rootPath = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        // 自动触发用当前 SSC 身份及研究完成状态；永久资格不替代身份，部分研究仍可触发。
        Run("零进度新游戏生成一个免费任务和通知，不直接发物品", () => {
            Init(); Check(QuestGen.Calls == 1 && QuestUtility.Letters == 1);
            Check(LastQuest.State == QuestState.NotYetAccepted && QuestPart_DropPods.DeliveredUnits == 0);
            Check((bool)Save(Entry)["sscInitialMasterQuestCreated"]);
        });
        Run("已有部分研究仍触发，不改变研究进度", () => {
            SSCDefOf.SSC_BasicTraining.ProgressReal = 250; Init();
            Check(QuestGen.Calls == 1 && SSCDefOf.SSC_BasicTraining.ProgressReal == 250);
        });
        Run("完成研究阻止自动任务，手动入口仍允许", () => {
            SSCDefOf.SSC_BasicTraining.ProgressReal = 500; Init(); Check(QuestGen.Calls == 0); Request(); Check(QuestGen.Calls == 1);
        });
        Run("己方手动主人无永久资格也阻止自动任务", () => {
            SSCRestrictionGameComponent.Pawns.Add(Master()); Init(); Check(QuestGen.Calls == 0); Request();
            Check(!SSCRestrictionGameComponent.Pawns[0].Training.masterQualificationUnlocked);
        });
        Run("有永久资格但未选择主人不阻止自动任务", () => {
            var pawn = Master(); pawn.Training.pawnIdentity = PawnIdentity.Unset; pawn.Training.masterQualificationUnlocked = true;
            SSCRestrictionGameComponent.Pawns.Add(pawn); Init(); Check(QuestGen.Calls == 1);
        });
        Run("己方主人查询与地图无关，商队和运输角色快照有效", () => {
            var pawn = Master(); Check(SSCMasterEntryUtility.HasExistingMaster(new[] { pawn }));
            SSCRestrictionGameComponent.Pawns.Add(pawn); Init(); Check(QuestGen.Calls == 0 && SSCRestrictionGameComponent.Scans == 1);
        });
        Run("死亡、销毁、敌方、访客、空壳和非主人不阻止任务", () => {
            foreach (Action<Pawn> mutation in new Action<Pawn>[] {
                p => p.Dead = true, p => p.Destroyed = true, p => p.Faction = new Faction(), p => p.Faction = null,
                p => p.health.hediffSet.Values.Add(SSCDefOf.SSC_PersonalityExcreted_Done),
                p => p.Training.pawnIdentity = PawnIdentity.Slave, p => p.Training = null
            }) { var p = Master(); mutation(p); Check(!SSCMasterEntryUtility.HasExistingMaster(new[] { p })); SSCRestrictionGameComponent.Pawns.Add(p); }
            Init(); Check(QuestGen.Calls == 1);
            Check(!SSCMasterEntryUtility.HasExistingMaster(null));
        });
        Run("自动条件等加载结束回调，不依赖组件 FinalizeInit 顺序", () => {
            Entry.FinalizeInit(); Check(QuestGen.Calls == 0);
            SSCRestrictionGameComponent.Pawns.Add(Master()); LongEventHandler.Finish(); Entry.GameComponentUpdate(); Check(QuestGen.Calls == 0);
        });
        Run("重复 FinalizeInit 每轮只检查一次", () => {
            Entry.FinalizeInit(); Entry.FinalizeInit(); LongEventHandler.Finish(); Entry.GameComponentUpdate();
            Check(QuestGen.Calls == 1 && SSCRestrictionGameComponent.Scans == 1);
        });
        Run("旧游戏加载回调不作用到新存档", () => {
            Entry.FinalizeInit(); var next = new Game(); Current.Game = next; LongEventHandler.Finish();
            Check(QuestGen.Calls == 0 && next.questManager.QuestsListForReading.Count == 0);
        });
        Run("长事件回调同步执行时仍等待其余组件，暂停 Update 可初始化", () => {
            LongEventHandler.Immediate = true; Entry.FinalizeInit(); Check(QuestGen.Calls == 0);
            SSCRestrictionGameComponent.Pawns.Add(Master()); Entry.GameComponentUpdate();
            Check(QuestGen.Calls == 0 && Entry.CanRequestManually().Accepted);
        });
        Run("旧档缺所有新字段按初次规则创建", () => { Reload(new()); Check(QuestGen.Calls == 1); });

        // 去重既看存档历史，又看原版任务管理器；缺引用和隐藏提示不能导致补发。
        Run("未接受任务读档不重复且手动拒绝", () => {
            Init(); Reload(); Check(QuestGen.Calls == 1 && QuestUtility.Letters == 1);
            Check(!Entry.TryRequestManually(out string reason) && reason == "SSC_MasterEntry_AlreadyPending");
            Check(!SSCMasterEntryUtility.TryCreateQuest(Find.CurrentMap, out Quest duplicate) && duplicate == null && QuestGen.Calls == 1);
        });
        Run("进行中任务读档不重复且手动拒绝", () => {
            Init(); LastQuest.Accept(); Reload(); Check(QuestGen.Calls == 1 && !Entry.CanRequestManually().Accepted);
        });
        Run("隐藏任务提示仍视为待处理", () => {
            Init(); LastQuest.dismissed = true; Reload(); Check(QuestGen.Calls == 1 && !Entry.CanRequestManually().Accepted);
        });
        Run("已完成任务读档不补发，但手动可以再创建", () => {
            Init(); LastQuest.Accept(); LastQuest.Tick(); Check(LastQuest.State == QuestState.EndedSuccess);
            Check(Delivery(LastQuest).module == null, "historical quest retained the consumable module reference");
            Reload(); Check(QuestGen.Calls == 1); Request(); Check(QuestGen.Calls == 2);
        });
        Run("已失败或过期任务不自动补发，手动可重新创建", () => {
            Init(); LastQuest.State = QuestState.EndedFailed; Reload(); Check(QuestGen.Calls == 1); Request();
            LastQuest.State = QuestState.EndedOfferExpired; Reload(); Check(QuestGen.Calls == 2); Request(); Check(QuestGen.Calls == 3);
        });
        Run("历史任务清理后仍以存档标记阻止自动补发", () => {
            Init(); var saved = Save(Entry); Find.QuestManager.QuestsListForReading.Clear(); Reload(saved); Check(QuestGen.Calls == 1);
        });
        Run("任务引用和标记缺失时从原版任务列表恢复", () => {
            Init(); Reload(new()); Check(QuestGen.Calls == 1 && (bool)Save(Entry)["sscInitialMasterQuestCreated"]);
        });
        Run("其他模组任务不阻止初次任务", () => {
            Find.QuestManager.Add(new Quest { root = new QuestScriptDef { defName = "OtherQuest" } }); Init(); Check(QuestGen.Calls == 1);
        });
        Run("不同存档历史互不影响", () => {
            Init(); var previous = Current.Game; var map = new Map(); Current.Game = new Game(); Find.CurrentMap = map; Current.Game.Maps.Add(map);
            Current.Game.Entry = new SSCMasterEntryGameComponent(Current.Game); Init();
            Check(previous.questManager.QuestsListForReading.Count == 1 && Find.QuestManager.QuestsListForReading.Count == 1);
        });

        // 保留请求只轻量重试；登记前后和通知故障分别检验历史标记的提交时机。
        Run("无地图请求保存读档，基地恢复后创建，不周期扫描 Pawn", () => {
            Find.Maps.Clear(); Init(); var data = Save(Entry);
            Check(!(bool)data["sscInitialMasterQuestCreated"] && (bool)data["sscInitialMasterQuestPending"]);
            Reload(data); Check(SSCRestrictionGameComponent.Scans == 1);
            var map = new Map(); Find.Maps.Add(map); Find.CurrentMap = map; Find.TickManager.TicksGame = 1200; Entry.GameComponentTick();
            Check(QuestGen.Calls == 1 && SSCRestrictionGameComponent.Scans == 1);
            for (int i = 0; i < 20; i++) Entry.GameComponentTick(); Check(QuestGen.Calls == 1);
        });
        Run("地图不属于玩家基地时等待", () => {
            Find.CurrentMap.IsPlayerHome = false; Init(); Check(QuestGen.Calls == 0 && !Entry.CanRequestManually().Accepted);
            Find.CurrentMap.IsPlayerHome = true; Find.TickManager.TicksGame = 1200; Entry.GameComponentTick(); Check(QuestGen.Calls == 1);
        });
        Run("原版脚本不允许的地图不能作为投放目标", () => {
            Find.CurrentMap.Parent.Suitable = false; Init(); Check(QuestGen.Calls == 0);
            Check(Entry.CanRequestManually().Reason == "SSC_MasterEntry_NoMap");
        });
        Run("地图已从当前游戏移除时不能沿用旧对象", () => {
            var old = Find.CurrentMap; Find.Maps.Clear(); Check(SSCMasterEntryUtility.FindDeliveryMap(old, SSCDefOf.SSC_InitialMasterQualification) == null);
        });
        Run("生成返回空时不提交标记，限制重试频率", () => {
            QuestGen.Fail = true; Init(); Check(!(bool)Save(Entry)["sscInitialMasterQuestCreated"]);
            Entry.GameComponentTick(); Check(QuestGen.Calls == 1);
            QuestGen.Fail = false; Find.TickManager.TicksGame = 1200; Entry.GameComponentTick(); Check(QuestGen.Calls == 2);
        });
        Run("登记前异常保留请求，后续可以恢复", () => {
            Find.QuestManager.ThrowBeforeAdd = true; Init(); Check(!(bool)Save(Entry)["sscInitialMasterQuestCreated"]);
            Find.QuestManager.ThrowBeforeAdd = false; Find.TickManager.TicksGame = 1200; Entry.GameComponentTick(); Check(Find.QuestManager.QuestsListForReading.Count == 1);
        });
        Run("登记后通知异常仍提交历史，不再补发", () => {
            Find.QuestManager.ThrowAfterAdd = true; Init(); Reload(); Check(QuestGen.Calls == 1 && (bool)Save(Entry)["sscInitialMasterQuestCreated"]);
        });
        Run("任务信件异常不丢弃已登记任务", () => {
            QuestUtility.ThrowLetter = true; Init(); Reload(); Check(QuestGen.Calls == 1 && Find.QuestManager.QuestsListForReading.Count == 1);
        });
        Run("场景禁止任务时保留请求，不绕过原版 CanRun", () => {
            SSCDefOf.SSC_InitialMasterQualification.Disabled = true; Init(); Check(QuestGen.Calls == 0);
        });
        Run("已有历史发放后的手动失败请求也随读档保留", () => {
            Init(); LastQuest.State = QuestState.EndedSuccess; QuestGen.Fail = true;
            Check(!Entry.TryRequestManually(out string reason) && reason == "SSC_MasterEntry_CreateFailed");
            var data = Save(Entry); Check((bool)data["sscInitialMasterQuestManualPending"]);
            QuestGen.Fail = false; Reload(data); Check(Find.QuestManager.QuestsListForReading.Count == 2);
        });
        Run("未创建任务时不在日常 tick 重新判定研究主人", () => {
            SSCRestrictionGameComponent.Pawns.Add(Master()); Init(); SSCRestrictionGameComponent.Pawns.Clear();
            Find.TickManager.TicksGame = 12000; Entry.GameComponentTick(); Check(QuestGen.Calls == 0 && SSCRestrictionGameComponent.Scans == 1);
            Reload(); Check(QuestGen.Calls == 1);
        });

        // 实际生产任务节点连接原版边界模型；交付只发生在接受之后，且奖励与投放引用同一物品。
        Run("奖励展示与空投共用一个模块，无费用战斗或直接资格收益", () => {
            var pawn = Master(); pawn.Training.pawnIdentity = PawnIdentity.Unset; SSCRestrictionGameComponent.Pawns.Add(pawn); Init();
            var quest = LastQuest; var reward = quest.PartsListForReading.OfType<QuestPart_Choice>().Single().choices.Single().rewards.Single();
            Check(reward.items.Single() == Drop(quest).Things.Single() && reward.items.Single() == Delivery(quest).module);
            Check(reward.items.Single().stackCount == 1); quest.Tick(); Check(QuestPart_DropPods.Calls == 0);
            quest.Accept(); quest.Tick(); Check(QuestPart_DropPods.DeliveredUnits == 1 && quest.State == QuestState.EndedSuccess);
            Check(!pawn.Training.masterQualificationUnlocked && pawn.Training.pawnIdentity == PawnIdentity.Unset);
        });
        Run("任务完成及重复信号不会重复发放", () => {
            Init(); var quest = LastQuest; quest.Accept(); quest.Tick();
            Find.SignalManager.SendSignal(new Signal(Delivery(quest).deliverSignal)); quest.Tick(); Delivery(quest).QuestPartTick();
            Check(QuestPart_DropPods.DeliveredUnits == 1 && QuestPart_DropPods.Calls == 1);
        });
        // 实际投放复用原版信件字段；接受前、无基地、失败重试和读档不得提前或重复通知。
        Run("接受前不通知，投放时替换原版标题正文且只通知一次", () => {
            Init(); var quest = LastQuest; quest.Tick(); Check(QuestPart_DropPods.DeliveryLetters.Count == 0);
            quest.Accept(); quest.Tick();
            var letter = QuestPart_DropPods.DeliveryLetters.Single();
            Check(letter.Label == "SSC_MasterEntry_DeliveryLetterLabel" && letter.Text == "SSC_MasterEntry_DeliveryLetterText");
            Check(letter.Map == Drop(quest).mapParent.Map);
            Reload(); Find.SignalManager.SendSignal(new Signal(Delivery(quest).deliverSignal)); Delivery(quest).QuestPartTick();
            Check(QuestPart_DropPods.DeliveryLetters.Count == 1 && QuestPart_DropPods.DeliveredUnits == 1);
        });
        Run("旧待交付任务恢复后使用当前通知并跟随新基地", () => {
            Init(); var quest = LastQuest; quest.Accept(); Find.Maps.Clear(); quest.Tick();
            Check(QuestPart_DropPods.DeliveryLetters.Count == 0);
            var part = Delivery(quest); Scribe.Data = new(); Scribe.mode = LoadSaveMode.Saving; part.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive;
            var copy = new QuestPart_MasterQualificationDelivery(); Scribe.mode = LoadSaveMode.LoadingVars; copy.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive; quest.PartsListForReading.Remove(part); quest.AddPart(copy);
            Drop(quest).customLetterLabel = null; Drop(quest).customLetterText = null;
            var map = new Map(); Find.Maps.Add(map); Find.TickManager.TicksGame = 1200; quest.Tick();
            var letter = QuestPart_DropPods.DeliveryLetters.Single();
            Check(letter.Map == map && letter.Label == "SSC_MasterEntry_DeliveryLetterLabel" && letter.Text == "SSC_MasterEntry_DeliveryLetterText");
        });
        Run("投放失败不通知，重试成功仅发一封", () => {
            Init(); var quest = LastQuest; QuestPart_DropPods.Fail = true; quest.Accept(); quest.Tick();
            Check(QuestPart_DropPods.DeliveryLetters.Count == 0 && quest.State == QuestState.Ongoing);
            QuestPart_DropPods.Fail = false; Find.TickManager.TicksGame = 1200; quest.Tick();
            Check(QuestPart_DropPods.DeliveryLetters.Count == 1 && QuestPart_DropPods.DeliveredUnits == 1);
            Reload(); quest.Tick(); Check(QuestPart_DropPods.DeliveryLetters.Count == 1);
        });
        Run("接受后原地图丢失转到另一基地", () => {
            Init(); var quest = LastQuest; Find.Maps.Clear(); var map = new Map(); Find.Maps.Add(map); Find.CurrentMap = map;
            quest.Accept(); quest.Tick(); Check(Drop(quest).mapParent == map.Parent && QuestPart_DropPods.DeliveredUnits == 1);
        });
        Run("接受后没有基地不会提前成功，基地恢复后再投放", () => {
            Init(); var quest = LastQuest; quest.Accept(); Find.Maps.Clear(); quest.Tick();
            Check(quest.State == QuestState.Ongoing && QuestPart_DropPods.Calls == 0);
            Find.Maps.Add(new Map()); Find.TickManager.TicksGame = 1200; quest.Tick(); Check(quest.State == QuestState.EndedSuccess);
        });
        Run("投放等待状态字段恢复后继续原任务", () => {
            Init(); var quest = LastQuest; quest.Accept(); Find.Maps.Clear(); quest.Tick(); var part = Delivery(quest);
            Scribe.Data = new(); Scribe.mode = LoadSaveMode.Saving; part.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            var copy = new QuestPart_MasterQualificationDelivery(); Scribe.mode = LoadSaveMode.LoadingVars; copy.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            quest.PartsListForReading.Remove(part); quest.AddPart(copy); Find.Maps.Add(new Map()); Find.TickManager.TicksGame = 1200;
            quest.Tick(); Check(quest.State == QuestState.EndedSuccess && QuestPart_DropPods.DeliveredUnits == 1);
        });
        Run("原版投放未成功不结束，未投出的同一实物允许重试", () => {
            Init(); var quest = LastQuest; QuestPart_DropPods.Fail = true; quest.Accept(); quest.Tick();
            Check(quest.State == QuestState.Ongoing && QuestPart_DropPods.DeliveredUnits == 0);
            QuestPart_DropPods.Fail = false; Find.TickManager.TicksGame = 1200; quest.Tick(); Check(QuestPart_DropPods.DeliveredUnits == 1);
        });
        Run("失败历史任务即便直接调用旧投放控制也不发物品", () => {
            Init(); var quest = LastQuest; quest.Accept(); quest.State = QuestState.EndedFailed; Delivery(quest).QuestPartTick();
            Check(QuestPart_DropPods.DeliveredUnits == 0);
        });

        // 实际设置 partial 验证禁用、GUI 状态恢复和点击后的最终复查。
        Run("主菜单设置按钮禁用，无空引用", () => {
            Current.Game = null; Draw(); Check(Messages.Text.Count == 0 && UnityEngine.GUI.enabled);
            Check(TooltipHandler.LastTip == "SSC_MasterEntry_Unavailable");
        });
        Run("初始化未结束以及缺任务管理器时手动不可用", () => {
            Check(!Entry.CanRequestManually().Accepted); Init(); Current.Game.questManager = null; Check(!Entry.CanRequestManually().Accepted);
        });
        Run("缺任务或道具定义时不创建，按钮禁用", () => {
            SSCDefOf.SSC_InitialMasterQualification = null; Init(); Draw(); Check(QuestGen.Calls == 0 && Messages.Text.Count == 0);
            SSCDefOf.SSC_InitialMasterQualification = new QuestScriptDef(); SSCDefOf.SSC_MasterQualificationItem = null;
            Check(!Entry.CanRequestManually().Accepted);
        });
        Run("无基地时禁用按钮并显示原因", () => {
            SSCDefOf.SSC_BasicTraining.ProgressReal = 500; Init(); Find.Maps.Clear(); Draw();
            Check(TooltipHandler.LastTip == "SSC_MasterEntry_NoMap" && Messages.Text.Count == 0);
        });
        Run("按钮成功创建任务，保留原有道具生成按钮的独立性", () => {
            SSCDefOf.SSC_BasicTraining.ProgressReal = 500; Init(); Draw();
            Check(QuestGen.Calls == 1 && Messages.Text.Single() == "SSC_MasterEntry_Created" && UnityEngine.GUI.enabled);
        });
        Run("外层 GUI 禁用时按钮不能重新启用父窗口", () => {
            SSCDefOf.SSC_BasicTraining.ProgressReal = 500; Init(); UnityEngine.GUI.enabled = false; Draw();
            Check(!UnityEngine.GUI.enabled && QuestGen.Calls == 0);
        });
        Run("绘制后出现同类任务时最终复查拒绝重复创建", () => {
            SSCDefOf.SSC_BasicTraining.ProgressReal = 500; Init(); Widgets.BeforeReturn = () => {
                Check(SSCMasterEntryUtility.TryCreateQuest(Find.CurrentMap, out _));
            }; Draw(); Check(QuestGen.Calls == 1 && Messages.Text.Single() == "SSC_MasterEntry_AlreadyPending");
        });
        // 汇总格式由统一门禁读取；任何单组失败都让进程返回非零，不以日志存在代替通过。
        Console.WriteLine($"RESULT: {passed}/{passed + failed} cases passed");
        return failed == 0 ? 0 : 1;
    }
}
