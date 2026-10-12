using System;
using System.Collections.Generic;

namespace UnityEngine
{
    // GUI 边界只记录启用状态；不模拟实际布局或绘制，按钮方法仍来自生产 partial。
    public struct Rect { }
    public static class GUI { public static bool enabled = true; }
}

namespace Verse
{
    // 最小游戏与定义宿主。ResearchManager 访问故意保留无游戏时的空引用行为，检验生产短路。
    public class Def { public string defName; }
    public class ThingDef : Def { }
    public class HediffDef : Def { }
    public enum DestroyMode { Vanish }
    public enum ThingPlaceMode { Near }
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
    }
    public class Map { public IntVec3 Center = new IntVec3(50, 50); }
    public class Game { public RimWorld.ResearchManager researchManager = new RimWorld.ResearchManager(); }
    public static class Current { public static Game Game; }
    public static class Find
    {
        public static RimWorld.ResearchManager ResearchManager => Current.Game.researchManager;
        public static Map CurrentMap;
        public static Selector Selector = new Selector();
    }
    public class Selector { public Thing SingleSelectedThing; }
    // 物品边界记录数量、拆分及销毁；测试可据此区分最后一个整体消耗与从堆叠拆出一个。
    public class Thing
    {
        public ThingDef def;
        public int stackCount = 1;
        public bool Destroyed;
        public Map Map;
        public IntVec3 Position;
        public bool Spawned => Map != null && !Destroyed;
        public virtual T TryGetComp<T>() where T : class { return null; }
        public Thing SplitOff(int count)
        {
            if (Destroyed || count < 1 || count > stackCount) throw new InvalidOperationException("bad split");
            if (count == stackCount) { Map = null; return this; }
            stackCount -= count;
            return new Thing { def = def, stackCount = count };
        }
        public void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Destroyed) throw new InvalidOperationException("double destroy");
            Destroyed = true; Map = null; DestroyedUnits += stackCount;
        }
        public static int DestroyedUnits;
    }
    public class ThingWithComps : Thing { }
    public class ThingComp { public ThingWithComps parent; }
    // 角色只提供道具规则所需事实；原版奴隶状态与 SSC 当前身份分别保存。
    public class Pawn : ThingWithComps
    {
        public bool Dead, IsSlave;
        public RaceProperties RaceProps = new RaceProperties();
        public PawnHealthTracker health = new PawnHealthTracker();
        public SexSlaveCraft.CompSexSlaveTraining Training = new SexSlaveCraft.CompSexSlaveTraining();
        public string LabelShort = "Test pawn";
        public override T TryGetComp<T>() { return Training as T; }
    }
    public class RaceProperties { public bool Humanlike = true; }
    public class PawnHealthTracker { public HediffSet hediffSet = new HediffSet(); }
    public class HediffSet
    {
        public HashSet<HediffDef> Values = new HashSet<HediffDef>();
        public bool HasHediff(HediffDef def) => Values.Contains(def);
    }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public string Reason;
        public static implicit operator AcceptanceReport(bool value) => new AcceptanceReport { Accepted = value };
        public static implicit operator AcceptanceReport(string value) => new AcceptanceReport { Accepted = false, Reason = value };
    }
    // 翻译返回键名以稳定检查拒绝原因；真实翻译内容由 XML 用例单独核对。
    public static class Translation
    {
        public static string Translate(this string key, params object[] arguments) => key;
    }
    public static class Scribe_Values { public static void Look<T>(ref T value, string label, T defaultValue) { } }
    // 原版蓝图数量/进度属性的测试宿主，不执行旧档兼容、研究任务或智识经验系统。
    public class ResearchProjectDef : Def
    {
        public int techprintCount = 1;
        public float baseCost = 500;
        public int TechprintCount => RimWorld.ModLister.RoyaltyInstalled ? techprintCount : 0;
        public int TechprintsApplied => Find.ResearchManager.GetTechprints(this);
        public float ProgressReal => Find.ResearchManager.GetProgress(this);
        public bool IsFinished => ProgressReal >= baseCost;
    }
    // 提示目标和类型是可观察结果，尤其验证合堆目标和失败时不发成功消息。
    public static class Messages
    {
        public static readonly List<(string text, Thing target, RimWorld.MessageTypeDef type)> Records = new();
        public static void Message(string text, Thing target, RimWorld.MessageTypeDef type, bool historical = true)
            => Records.Add((text, target, type));
        public static void Message(string text, RimWorld.MessageTypeDef type, bool historical = true)
            => Message(text, null, type, historical);
    }
    // 生成与放置边界可切换成功、失败或合堆，SSC 如何处理这些结果仍执行生产代码。
    public static class ThingMaker
    {
        public static int Calls;
        public static Thing LastCreated;
        public static Thing MakeThing(ThingDef def)
        {
            Calls++; return LastCreated = new ThingWithComps { def = def };
        }
    }
    public static class GenPlace
    {
        public static int Calls;
        public static bool Fail;
        public static Thing MergeInto;
        public static IntVec3 LastCenter;
        public static bool TryPlaceThing(Thing item, IntVec3 center, Map map, ThingPlaceMode mode, out Thing spawned)
        {
            Calls++; LastCenter = center; spawned = null;
            if (Fail) return false;
            if (MergeInto != null)
            {
                MergeInto.stackCount += item.stackCount;
                item.Destroy(); spawned = MergeInto;
            }
            else { item.Map = map; item.Position = center; spawned = item; }
            return true;
        }
    }
    // UI 宿主只提供点击和提示记录；禁用按钮是否阻止操作由实际设置方法负责。
    public class Listing_Standard { public UnityEngine.Rect GetRect(float height) => new UnityEngine.Rect(); }
    public static class Widgets
    {
        public static bool Click;
        public static bool ButtonText(UnityEngine.Rect rect, string text) => Click;
    }
    public static class TooltipHandler
    {
        public static string LastTip;
        public static void TipRegion(UnityEngine.Rect rect, string tip) => LastTip = tip;
    }
}

namespace RimWorld
{
    // 研究宿主记录 AddTechprints 调用和数值，并提供异常边界；不把该模型当作原版引擎验证。
    public static class ModLister { public static bool RoyaltyInstalled = true; }
    public class ResearchManager
    {
        public readonly Dictionary<Verse.ResearchProjectDef, int> Techprints = new();
        public readonly Dictionary<Verse.ResearchProjectDef, float> Progress = new();
        public int AddCalls;
        public int LastAmount;
        public bool ThrowOnAdd;
        public int GetTechprints(Verse.ResearchProjectDef def) => Techprints.TryGetValue(def, out int value) ? value : 0;
        public float GetProgress(Verse.ResearchProjectDef def) => Progress.TryGetValue(def, out float value) ? value : 0;
        public void AddTechprints(Verse.ResearchProjectDef def, int amount)
        {
            if (ThrowOnAdd) throw new InvalidOperationException("research boundary failed");
            AddCalls++; LastAmount = amount;
            Techprints[def] = Math.Min(def.TechprintCount, GetTechprints(def) + amount);
        }
    }
    public abstract class CompUseEffect : Verse.ThingComp
    {
        public virtual Verse.AcceptanceReport CanBeUsedBy(Verse.Pawn pawn) => true;
        public virtual void DoEffect(Verse.Pawn pawn) { }
    }
    public class MessageTypeDef { }
    public static class MessageTypeDefOf
    {
        public static MessageTypeDef RejectInput = new MessageTypeDef();
        public static MessageTypeDef PositiveEvent = new MessageTypeDef();
    }
}

namespace SexSlaveCraft
{
    // SSC 外围契约存根；资格字段、道具使用服务及效果由项目链接的生产文件提供。
    public enum PawnIdentity { Unset, Slave, Master }
    public partial class CompSexSlaveTraining { public PawnIdentity pawnIdentity; }
    public class SSCSettings { public bool allowDirectMasterSelection = true; }
    public partial class SSCMod { public static SSCSettings settings = new SSCSettings(); }
    public static class SSCDefOf
    {
        public static Verse.ThingDef SSC_MasterQualificationItem;
        public static Verse.ResearchProjectDef SSC_BasicTraining;
        public static Verse.HediffDef SSC_PersonalityExcreted_Done;
    }
}
