using System;
using System.Collections.Generic;
using System.Linq;

// 宿主模拟任务登记、信号与字段保存边界；空投、XML 加载、跨引用读档均不能以此代替原版实机。
namespace UnityEngine
{
    public struct Rect { }
    public static class GUI { public static bool enabled = true; }
}

namespace Verse
{
    // 最小定义、角色和实物状态用于生产准入；不模拟完整研究、人格或运输舱物理行为。
    public class Def { public string defName; }
    public class ThingDef : Def { }
    public class HediffDef : Def { }
    public class ResearchProjectDef : Def { public float ProgressReal; public bool IsFinished => ProgressReal >= 500; }
    public class Thing
    {
        public ThingDef def;
        public int stackCount = 1;
        public bool Destroyed;
        public bool SpawnedOrAnyParentSpawned;
    }
    public static class ThingMaker
    {
        public static Thing MakeThing(ThingDef def) => new Thing { def = def };
    }
    public class Pawn
    {
        public bool Dead, Destroyed;
        public RimWorld.Faction Faction;
        public Health health = new Health();
        public SexSlaveCraft.CompSexSlaveTraining Training = new();
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class Health { public HediffSet hediffSet = new(); }
    public class HediffSet
    {
        public HashSet<HediffDef> Values = new();
        public bool HasHediff(HediffDef def) => Values.Contains(def);
    }
    // 地图集合与 Parent 分开保存，以注入“旧对象仍在、但已从当前游戏移除”的场景。
    public class Map
    {
        public bool IsPlayerHome = true;
        public RimWorld.Planet.MapParent Parent;
        public Map() { Parent = new RimWorld.Planet.MapParent { Map = this }; }
    }
    public class Game
    {
        public RimWorld.QuestManager questManager = new();
        public List<Map> Maps = new();
        public TickManager tickManager = new();
        public SexSlaveCraft.SSCMasterEntryGameComponent Entry;
        public T GetComponent<T>() where T : class => Entry as T;
    }
    public class TickManager { public int TicksGame; }
    public static class Current { public static Game Game; }
    public static class Find
    {
        // 无游戏访问保留空引用行为，使主菜单保护必须来自生产短路。
        public static RimWorld.QuestManager QuestManager => Current.Game.questManager;
        public static List<Map> Maps => Current.Game.Maps;
        public static TickManager TickManager => Current.Game.tickManager;
        public static Map CurrentMap;
        public static SignalManager SignalManager = new();
    }
    // 拒绝原因保留键名，测试生产流程选择了哪个原因，不依赖测试机器的当前语言。
    public struct AcceptanceReport
    {
        public bool Accepted;
        public string Reason;
        public static implicit operator AcceptanceReport(bool value) => new() { Accepted = value };
        public static implicit operator AcceptanceReport(string value) => new() { Reason = value };
    }
    public static class Translations { public static string Translate(this string value) => value; }
    public abstract class GameComponent
    {
        public virtual void ExposeData() { }
        public virtual void FinalizeInit() { }
        public virtual void GameComponentTick() { }
        public virtual void GameComponentUpdate() { }
    }
    // 同时支持排队与立即回调，覆盖原版 ExecuteWhenFinished 两种执行时机。
    public static class LongEventHandler
    {
        public static List<Action> Queue = new();
        public static bool Immediate;
        public static void ExecuteWhenFinished(Action action) { if (Immediate) action(); else Queue.Add(action); }
        public static void Finish() { var copy = Queue.ToArray(); Queue.Clear(); foreach (var a in copy) a(); }
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars }
    // 只验证生产保存的字段/默认值，引用在此仍是内存对象；不声称执行原版跨引用加载。
    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> Data = new();
        public static void Look<T>(ref T value, string key, T fallback = default)
        {
            if (mode == LoadSaveMode.Saving) Data[key] = value;
            if (mode == LoadSaveMode.LoadingVars) value = Data.TryGetValue(key, out var v) ? (T)v : fallback;
        }
    }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T fallback = default) => Scribe.Look(ref value, key, fallback);
    }
    public static class Scribe_References
    {
        public static void Look<T>(ref T value, string key) => Scribe.Look(ref value, key);
    }
    public class Signal { public string tag; public Signal(string value) { tag = value; } }
    public class SignalManager
    {
        public List<string> Sent = new();
        public void SendSignal(Signal signal)
        {
            // 记录发送顺序并转给任务快照，允许投放成功处理在同一调用里结束任务。
            Sent.Add(signal.tag);
            foreach (var q in Find.QuestManager.QuestsListForReading.ToArray()) q.Receive(signal);
        }
    }
    public static class Log { public static List<string> Errors = new(); public static void Error(string text) => Errors.Add(text); }
    // UI 宿主不绘制像素，只记录点击、提示和消息；禁用及 GUI 恢复由生产 partial 执行。
    public class Listing_Standard { public UnityEngine.Rect GetRect(float height) => new(); }
    public static class Widgets
    {
        public static bool Click;
        public static Action BeforeReturn;
        public static bool ButtonText(UnityEngine.Rect rect, string text) { BeforeReturn?.Invoke(); return Click; }
    }
    public static class TooltipHandler
    {
        public static string LastTip;
        public static void TipRegion(UnityEngine.Rect rect, string text) { LastTip = text; }
    }
    public static class Messages
    {
        public static List<string> Text = new();
        public static void Message(string text, RimWorld.MessageTypeDef type, bool historical = true) => Text.Add(text);
    }
}

namespace RimWorld.Planet
{
    public class MapParent { public Verse.Map Map; public bool Suitable = true; }
}

namespace RimWorld
{
    public class Faction { public static Faction OfPlayer = new(); }
    // 地图与场景准入边界仍调用生产任务根的 TestRun，而不是在宿主重复写准入规则。
    public class QuestScriptDef : Verse.Def
    {
        public QuestGen.QuestNode root;
        public bool Disabled;
        public bool IsParentSuitableForQuest(Planet.MapParent map) => map.Suitable;
        public bool CanRun(QuestGen.Slate slate, Verse.Map map) => !Disabled && root.TestRun(slate);
    }
    public enum QuestState { NotYetAccepted, Ongoing, EndedSuccess, EndedFailed, EndedOfferExpired, EndedUnknownOutcome }
    public enum QuestEndOutcome { Unknown, Success, Fail }
    public enum QuestPartState { NeverEnabled, Enabled, Disabled }
    public class Quest
    {
        public int id;
        public QuestScriptDef root;
        public bool dismissed;
        public QuestState State = QuestState.NotYetAccepted;
        public bool Historical => State != QuestState.NotYetAccepted && State != QuestState.Ongoing;
        public List<QuestPart> PartsListForReading = new();
        public string InitiateSignal => "Quest" + id + ".Initiate";
        public void AddPart(QuestPart part) { part.quest = this; PartsListForReading.Add(part); }
        public void Accept()
        {
            // 只有未接受状态发送启动信号，接受前的 Tick 不能启用生产投放协调部分。
            if (State != QuestState.NotYetAccepted) return;
            State = QuestState.Ongoing;
            Verse.Find.SignalManager.SendSignal(new Verse.Signal(InitiateSignal));
        }
        public void Receive(Verse.Signal signal)
        {
            // 采用任务 id 隔离及进行中门槛；这是本任务所用的原版信号子集，不是完整 Quest 引擎。
            if (!signal.tag.StartsWith("Quest" + id + ".")) return;
            foreach (var part in PartsListForReading.ToArray()) if (!Historical && State == QuestState.Ongoing) part.Notify_QuestSignalReceived(signal);
        }
        public void Tick()
        {
            // 历史任务不运行，启用的生产协调部分才 Tick；投放中结束任务后跳过剩余处理。
            if (Historical) return;
            foreach (var p in PartsListForReading.OfType<QuestPartActivable>().ToArray())
                if (!Historical && p.State == QuestPartState.Enabled) p.QuestPartTick();
        }
    }
    public class QuestManager
    {
        public List<Quest> QuestsListForReading = new();
        public bool ThrowBeforeAdd, ThrowAfterAdd;
        public int AddCalls;
        public bool Contains(Quest quest) => QuestsListForReading.Contains(quest);
        public void Add(Quest quest)
        {
            // 登记前/后分别注入异常，验证生产服务以 Contains 结果决定是否提交创建历史。
            AddCalls++;
            if (ThrowBeforeAdd) throw new InvalidOperationException("before registration");
            QuestsListForReading.Add(quest);
            if (ThrowAfterAdd) throw new InvalidOperationException("after registration");
        }
    }
    public class QuestPart
    {
        public Quest quest;
        public virtual void Notify_QuestSignalReceived(Verse.Signal signal) { }
        public virtual void ExposeData() { }
        public virtual void Cleanup() { }
    }
    public abstract class QuestPartActivable : QuestPart
    {
        // 最小启用/完成状态支持生产协调部分；不模拟基类的警报及额外完成信号。
        public string inSignalEnable;
        private QuestPartState state;
        public QuestPartState State => state;
        public virtual void QuestPartTick() { }
        protected void Complete() { state = QuestPartState.Disabled; }
        protected virtual void ProcessQuestSignal(Verse.Signal signal) { }
        public override void Notify_QuestSignalReceived(Verse.Signal signal)
        {
            if (signal.tag == inSignalEnable && state == QuestPartState.NeverEnabled) state = QuestPartState.Enabled;
            else if (state == QuestPartState.Enabled) ProcessQuestSignal(signal);
        }
        public override void ExposeData()
        {
            Verse.Scribe_Values.Look(ref state, "state", QuestPartState.NeverEnabled);
            Verse.Scribe_Values.Look(ref inSignalEnable, "inSignalEnable");
        }
    }
    public class QuestPart_DropPods : QuestPart
    {
        // 只模拟未投出、已派送与成功信号；实际运输舱飞行、落地、开舱属于实机验证。
        public string inSignal, outSignalResult;
        public Planet.MapParent mapParent;
        public IEnumerable<Verse.Thing> Things;
        public bool useTradeDropSpot;
        public string customLetterText, customLetterLabel;
        public bool sendStandardLetter = true;
        public static bool Fail;
        public static int Calls, DeliveredUnits;
        // 记录原版投放边界收到的自定义信件，不模拟真实运输舱飞行或 LetterStack。
        public static List<(string Label, string Text, Verse.Map Map)> DeliveryLetters = new();
        public override void Notify_QuestSignalReceived(Verse.Signal signal)
        {
            if (signal.tag != inSignal) return;
            Calls++;
            // 失败保留同一实物，成功清空本部分持有集合，供生产重试和防重复逻辑观察。
            if (Fail || mapParent?.Map == null) return;
            var pending = Things.Where(x => !x.Destroyed && !x.SpawnedOrAnyParentSpawned).ToArray();
            if (pending.Length == 0) return;
            foreach (var t in pending)
            { t.SpawnedOrAnyParentSpawned = true; DeliveredUnits += t.stackCount; }
            Things = Array.Empty<Verse.Thing>();
            if (sendStandardLetter)
                DeliveryLetters.Add((customLetterLabel, customLetterText, mapParent.Map));
            Verse.Find.SignalManager.SendSignal(new Verse.Signal(outSignalResult));
        }
    }
    public class QuestPart_QuestEnd : QuestPart
    {
        // 结束后调用各部分清理，检验 SSC 协调部分会解除历史任务的模块引用。
        public string inSignal;
        public QuestEndOutcome? outcome;
        public override void Notify_QuestSignalReceived(Verse.Signal signal)
        {
            if (signal.tag == inSignal)
            {
                quest.State = outcome == QuestEndOutcome.Success ? QuestState.EndedSuccess : QuestState.EndedFailed;
                foreach (var part in quest.PartsListForReading) part.Cleanup();
            }
        }
    }
    public class Reward_Items { public List<Verse.Thing> items; }
    public class QuestPart_Choice : QuestPart
    {
        public class Choice { public List<Reward_Items> rewards = new(); }
        public string inSignalChoiceUsed;
        public List<Choice> choices = new();
    }
    public static class QuestUtility
    {
        public static int Letters;
        public static bool ThrowLetter;
        public static void SendLetterQuestAvailable(Quest quest)
        { if (ThrowLetter) throw new InvalidOperationException("letter"); Letters++; }
    }
    public class MessageTypeDef { }
    public static class MessageTypeDefOf { public static MessageTypeDef PositiveEvent = new(), RejectInput = new(); }
}

namespace RimWorld.QuestGen
{
    // Slate 和生成器仅提供生产根节点的上下文；奖励及信号链由实际 RunInt 建立。
    public class Slate
    {
        public Dictionary<string, object> Data = new();
        public void Set<T>(string key, T value) => Data[key] = value;
        public T Get<T>(string key) => Data.TryGetValue(key, out var v) ? (T)v : default;
    }
    public abstract class QuestNode
    {
        protected abstract bool TestRunInt(Slate slate);
        protected abstract void RunInt();
        public bool TestRun(Slate slate) => TestRunInt(slate);
        public void Run() => RunInt();
    }
    public static class QuestGen
    {
        public static RimWorld.Quest quest;
        public static Slate slate;
        public static bool Fail;
        public static int Calls;
        public static RimWorld.Quest Generate(RimWorld.QuestScriptDef root, Slate initial)
        {
            // 空返回模拟原版生成失败；成功时绑定独立任务 id，再运行真实 SSC 根节点。
            Calls++;
            if (Fail) return null;
            quest = new RimWorld.Quest { id = Calls, root = root }; slate = initial;
            root.root.Run();
            return quest;
        }
    }
}

namespace SexSlaveCraft
{
    // SSC 外围状态存根只提供字段和 Pawn 快照，获取规则、组件与设置按钮均链接生产文件。
    public enum PawnIdentity { Unset, Slave, Master }
    public class CompSexSlaveTraining { public PawnIdentity pawnIdentity; public bool masterQualificationUnlocked; }
    public partial class SSCMod { }
    public static class SSCDefOf
    {
        public static Verse.ThingDef SSC_MasterQualificationItem;
        public static Verse.ResearchProjectDef SSC_BasicTraining;
        public static Verse.HediffDef SSC_PersonalityExcreted_Done;
        public static RimWorld.QuestScriptDef SSC_InitialMasterQualification;
    }
    public static class SSCRestrictionGameComponent
    {
        public static List<Verse.Pawn> Pawns = new();
        public static int Scans;
        public static List<Verse.Pawn> AllPawns() { Scans++; return new List<Verse.Pawn>(Pawns); }
    }
}
