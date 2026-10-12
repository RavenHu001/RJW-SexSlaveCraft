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
    public static class LongEventHandler
    {
        public static List<Action> Queue = new();
        public static bool Immediate;
        public static void ExecuteWhenFinished(Action action) { if (Immediate) action(); else Queue.Add(action); }
        public static void Finish() { var copy = Queue.ToArray(); Queue.Clear(); foreach (var a in copy) a(); }
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars }
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
            Sent.Add(signal.tag);
            foreach (var q in Find.QuestManager.QuestsListForReading.ToArray()) q.Receive(signal);
        }
    }
    public static class Log { public static List<string> Errors = new(); public static void Error(string text) => Errors.Add(text); }
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
            if (State != QuestState.NotYetAccepted) return;
            State = QuestState.Ongoing;
            Verse.Find.SignalManager.SendSignal(new Verse.Signal(InitiateSignal));
        }
        public void Receive(Verse.Signal signal)
        {
            if (!signal.tag.StartsWith("Quest" + id + ".")) return;
            foreach (var part in PartsListForReading.ToArray()) if (!Historical && State == QuestState.Ongoing) part.Notify_QuestSignalReceived(signal);
        }
        public void Tick()
        {
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
        public string inSignal, outSignalResult;
        public Planet.MapParent mapParent;
        public IEnumerable<Verse.Thing> Things;
        public bool useTradeDropSpot;
        public static bool Fail;
        public static int Calls, DeliveredUnits;
        public override void Notify_QuestSignalReceived(Verse.Signal signal)
        {
            if (signal.tag != inSignal) return;
            Calls++;
            if (Fail || mapParent?.Map == null) return;
            foreach (var t in Things.Where(x => !x.Destroyed && !x.SpawnedOrAnyParentSpawned))
            { t.SpawnedOrAnyParentSpawned = true; DeliveredUnits += t.stackCount; }
            Things = Array.Empty<Verse.Thing>();
            Verse.Find.SignalManager.SendSignal(new Verse.Signal(outSignalResult));
        }
    }
    public class QuestPart_QuestEnd : QuestPart
    {
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
