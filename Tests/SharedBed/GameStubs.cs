// Minimal model of the inspected RimWorld 1.6 methods. Actual Harmony installs all production patches.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;

namespace Verse
{
    public static class Log
    {
        public static readonly Dictionary<int, string> Errors = new Dictionary<int, string>();
        /// <summary>按错误编号保留首次消息，模拟原版 ErrorOnce 的去重语义。</summary>
        public static void ErrorOnce(string message, int key) { if (!Errors.ContainsKey(key)) Errors.Add(key, message); }
        /// <summary>提供兼容告警入口；模型不连接 Unity 日志，也不对告警输出作断言。</summary>
        public static void Warning(string message) { }
    }
    public class Thing { public bool Destroyed; public ThingDef def = new ThingDef(); public Map Map; }
    public class ThingDef { public float maxBodySize = 2f; }
    public class Map { public MapPawns mapPawns = new MapPawns(); }
    public class MapPawns { public List<Pawn> SlavesOfColonySpawned = new List<Pawn>(); public List<Pawn> FreeColonists = new List<Pawn>(); }
    public class Pawn : Thing
    {
        public bool Dead, Deathresting, Sleeping, MedicalRest;
        public float BodySize = 1f;
        public string LabelShortCap = "Pawn";
        public GuestStatus? GuestStatus;
        public bool IsSlave { [MethodImpl(MethodImplOptions.NoInlining)] get { return GuestStatus == RimWorld.GuestStatus.Slave; } }
        public bool IsPrisoner => GuestStatus == RimWorld.GuestStatus.Prisoner;
        public Needs needs = new Needs();
        public Ownership ownership = new Ownership();
        public Relations relations = new Relations();
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public Hediff_ChainOfSexSlave Chain;
        public Building_Bed Bed, VanillaBed;
        /// <summary>仅返回测试所需的训练组件；其他组件类型返回 null。</summary>
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class Needs
    {
        public Need_Corruption Corruption = new Need_Corruption();
        public Mood mood = new Mood();
        /// <summary>仅提供测试设置的恶堕需求，不构建完整原版需求列表。</summary>
        public T TryGetNeed<T>() where T : class => Corruption as T;
    }
    public class Ownership { public Building_Bed OwnedBed; }
    public class Relations
    {
        public HashSet<Pawn> Lovers = new HashSet<Pawn>();
        public HashSet<Pawn> FlawedLovers = new HashSet<Pawn>();
        public int Opinion;
        /// <summary>返回用例配置的固定好感值，不计算其他社交因素。</summary>
        public int OpinionOf(Pawn pawn) => Opinion;
        /// <summary>仅查询 SSC 特殊关系集合，使其与原版恋人集合保持独立。</summary>
        public bool DirectRelationExists(object def, Pawn other) => def == SSCDefOf.SSC_FlawedLovers && FlawedLovers.Contains(other);
    }
    public class Mood { public Thoughts thoughts = new Thoughts(); }
    public class Thoughts { public Memories memories = new Memories(); }
    public class Memories
    {
        public List<Thought_Memory> Items = new List<Thought_Memory>();
        /// <summary>按定义删除记忆；与原版一样先读取 IsMemory，让空定义触发错误而非被静默接受。</summary>
        public void RemoveMemoriesOfDef(ThoughtDef def) { if (def.IsMemory) Items.RemoveAll(m => m.def == def); }
        /// <summary>仅移除指定定义中符合谓词的记忆，用于验证负面房间心情的选择性清理。</summary>
        public void RemoveMemoriesOfDefIf(ThoughtDef def, Predicate<Thought_Memory> filter) => Items.RemoveAll(m => m.def == def && filter(m));
        /// <summary>记录记忆对象后直接入列；不模拟原版额外过滤或自动合并，以观察生产代码的替换行为。</summary>
        public void TryGainMemory(Thought_Memory memory, Pawn other) { memory.otherPawn = other; Items.Add(memory); }
    }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public string Reason;
        public static AcceptanceReport WasAccepted => new AcceptanceReport { Accepted = true };
        /// <summary>将拒绝原因文本转换为未通过的分配结果。</summary>
        public static implicit operator AcceptanceReport(string reason) => new AcceptanceReport { Reason = reason };
    }
    public interface IExposable
    {
        /// <summary>供生产记录类暴露其存档字段，测试只验证字段契约。</summary>
        void ExposeData();
    }
    public static class Scribe
    {
        public static bool Loading;
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        /// <summary>用内存字典保存或恢复字段，缺键时使用默认值；不模拟真实 Scribe 的引用解析。</summary>
        public static void Look<T>(ref T value, string key, T fallback)
        {
            if (Loading) value = Data.TryGetValue(key, out object stored) ? (T)stored : fallback;
            else Data[key] = value;
        }
    }
    public static class Scribe_References
    {
        /// <summary>将引用字段交给内存存档模型，旧记录缺键时恢复为 null。</summary>
        public static void Look<T>(ref T value, string key) where T : class => Scribe.Look(ref value, key, null);
    }
    public static class Scribe_Values
    {
        /// <summary>将数值字段和生产代码指定的默认值交给内存存档模型。</summary>
        public static void Look<T>(ref T value, string key, T fallback) => Scribe.Look(ref value, key, fallback);
    }
    public enum GameFont { Tiny, Small }
    public static class Text
    {
        public static GameFont Font = GameFont.Small;
        public static TextAnchor Anchor;
        /// <summary>以固定字符宽度估算尺寸，使布局断言稳定；不代表游戏字体的实际测量结果。</summary>
        public static Vector2 CalcSize(string text) => new Vector2(text.Length * 7f, 16);
    }
    public struct TaggedString
    {
        private string value;
        /// <summary>返回保存的文本，不执行富文本处理。</summary>
        public override string ToString() => value;
        /// <summary>模拟 TaggedString 到普通字符串的隐式转换。</summary>
        public static implicit operator string(TaggedString value) => value.value;
        /// <summary>包装普通文本，以便测试区分 Widgets.Label 的两个重载。</summary>
        public static implicit operator TaggedString(string value) => new TaggedString { value = value };
    }
    public static class Translation
    {
        public static Dictionary<string, string> Keys = new Dictionary<string, string>();
        /// <summary>从测试加载的翻译键取值并格式化参数；缺失时保留键名。</summary>
        public static TaggedString Translate(this string key, params object[] args) => string.Format(Keys.TryGetValue(key, out string value) ? value : key, args);
    }
    public static class Widgets
    {
        public static List<(Rect rect, string text)> Labels = new List<(Rect, string)>();
        public static List<(Rect rect, Color color)> Boxes = new List<(Rect, Color)>();
        /// <summary>记录省略标签的区域和完整输入；不实际裁剪文本，禁止内联以保留补丁入口。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public static void LabelEllipses(Rect rect, string text) => Labels.Add((rect, text));
        /// <summary>记录普通字符串标签，用于检查姓名与性奴标记的绘制调用。</summary>
        public static void Label(Rect rect, string text) => Labels.Add((rect, text));
        /// <summary>记录 TaggedString 标签，验证 Mint 的意识形态说明不被姓名补丁替换。</summary>
        public static void Label(Rect rect, TaggedString text) => Labels.Add((rect, text.ToString()));
        /// <summary>记录色块区域与颜色，检查独立身份色和状态标记；模型不执行实际图形渲染。</summary>
        public static void DrawBoxSolid(Rect rect, Color color) => Boxes.Add((rect, color));
    }
    public static class TooltipHandler
    {
        public static string LastTooltip;
        public static List<(Rect rect, string text)> Tips = new List<(Rect, string)>();
        /// <summary>保存最近注册的提示文本，供标记悬停说明断言使用。</summary>
        public static void TipRegion(Rect rect, string text) { LastTooltip = text; Tips.Add((rect, text)); }
    }
}

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        /// <summary>保存测试中使用的二维尺寸或坐标。</summary>
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public struct Rect
    {
        public float x, y, width, height;
        /// <summary>按左上角和宽高构造布局区域，不依赖 Unity。</summary>
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax => x + width;
        public float xMin { get => x; set { float right = xMax; x = value; width = right - x; } }
    }
    public struct Color
    {
        public float r, g, b, a;
        /// <summary>保存颜色分量，供绘制前后状态恢复检查使用。</summary>
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public enum TextAnchor { UpperLeft, MiddleCenter }
    public static class GUI { public static Color color = new Color(1, 1, 1, 1); }
    public static class Mathf
    {
        /// <summary>提供布局计算所需的较小值运算。</summary>
        public static float Min(float a, float b) => Math.Min(a, b);
        /// <summary>提供布局计算所需的较大值运算，用于限制可用宽度不小于零。</summary>
        public static float Max(float a, float b) => Math.Max(a, b);
    }
}

namespace DubsMintMenus
{
    using Verse;
    // 依据本机 Mint 1.6 DoRow：两个 string 姓名标签、独立的 TaggedString 意识形态提示。
    public class Dialog_AssignBuildingOwner
    {
        private CompAssignableToPawn comp;
        public string RejectedReason;
        public bool ShowIdeologyInfo;
        /// <summary>保存分配组件，并保持与 Mint 相同的字段名和类型，供反射补丁读取。</summary>
        public Dialog_AssignBuildingOwner(CompAssignableToPawn comp) { this.comp = comp; }
        /// <summary>模拟 Mint 两种角色行的姓名调用和独立说明，以执行真实 transpiler 并检查布局输出。</summary>
        /// <param name="row">整行区域，左侧 10% 对应头像，右侧 170px 对应操作区。</param>
        /// <param name="pawn">当前行角色。</param>
        /// <param name="assigned">选择已分配或未分配分支；后者可以包含拒绝原因和意识形态提示。</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DoRow(Rect row, Pawn pawn, bool assigned)
        {
            Rect name = new Rect(row.x + row.width * .1f, row.y, row.width * .9f, row.height);
            if (assigned) Widgets.Label(name, pawn.LabelShortCap);
            else
            {
                string label = pawn.LabelShortCap + (RejectedReason == null ? "" : " (" + RejectedReason + ")");
                Widgets.Label(name, label);
                if (ShowIdeologyInfo) Widgets.Label(new Rect(row.xMax - 170, row.y, 170, row.height), (TaggedString)"IdeoligionForbids");
            }
        }
    }
}

namespace RimWorld
{
    using Verse;
    public enum GuestStatus { Guest, Prisoner, Slave }
    public class Building_Bed : Thing
    {
        public bool Medical, ForPrisoners, ForSlaves;
        public bool Spawned = true, Reachable = true, Reservable = true;
        public bool Burning, Vacuum, Forbidden, IdeologyForbidden;
        public bool AnyUnoccupiedSleepingSlot = true;
        public int SleepingSlotsCount = 2;
        public List<Pawn> OwnersForReading = new List<Pawn>();
        public bool AnyUnownedSleepingSlot => OwnersForReading.Count < SleepingSlotsCount;
    }
    public class CompAssignableToPawn { public Thing parent; }
    public class CompAssignableToPawn_Bed : CompAssignableToPawn
    {
        /// <summary>模拟体型和奴隶床分类检查，保留两个 IsSlave 调用供生产分配补丁替换。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public AcceptanceReport CanAssignTo(Pawn pawn)
        {
            Building_Bed bed = (Building_Bed)parent;
            if (pawn.BodySize > bed.def.maxBodySize) return "TooLargeForBed";
            if (bed.ForSlaves && !pawn.IsSlave) return "CannotAssignBedToColonist";
            if (!bed.ForSlaves && pawn.IsSlave) return "CannotAssignBedToSlave";
            return AcceptanceReport.WasAccepted;
        }
        public IEnumerable<Pawn> AssigningCandidates
        {
            [MethodImpl(MethodImplOptions.NoInlining)] get => parent.Map.mapPawns.FreeColonists;
        }
    }
    public static class LovePartnerRelationUtility
    {
        /// <summary>仅查询原版恋人集合，供回归确认 SSC 特殊关系不会扩展全局恋爱判断。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool LovePartnerRelationExists(Pawn first, Pawn second) => first.relations.Lovers.Contains(second);
    }
    public static class RestUtility
    {
        public static int ValidationCalls;
        public static bool LastCheckSocial, LastIgnoreReservations, LastAllowMed;
        public static Pawn LastTraveler;
        /// <summary>返回用例直接设置的当前床引用，不扫描地图或任务。</summary>
        public static Building_Bed CurrentBed(this Pawn pawn) => pawn?.Bed;
        /// <summary>根据模型睡眠标志返回清醒状态。</summary>
        public static bool Awake(this Pawn pawn) => !pawn.Sleeping;
        /// <summary>模拟环境、体型、空位和床位身份检查，再进入可被生产补丁扩展的共享许可检查。</summary>
        /// <remarks>这是有限的原版行为模型，不包含完整寻路、意识形态或 DLC 规则。</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool CanUseBedNow(Thing bedThing, Pawn sleeper, bool checkSocialProperness,
            bool allowMedBedEvenIfSetToNoCare, GuestStatus? guestStatusOverride)
        {
            Building_Bed bed = bedThing as Building_Bed;
            if (bed == null || !bed.Spawned || bed.Map != sleeper.Map || bed.Burning || bed.Vacuum
                || sleeper.BodySize > bed.def.maxBodySize || bed.IdeologyForbidden || bed.Forbidden) return false;
            bool owner = bed.OwnersForReading.Contains(sleeper);
            if (!bed.AnyUnoccupiedSleepingSlot && !owner && sleeper.Bed != bed) return false;
            GuestStatus? effective = guestStatusOverride ?? sleeper.GuestStatus;
            if (bed.ForPrisoners != (effective == GuestStatus.Prisoner) || bed.ForSlaves != (effective == GuestStatus.Slave)) return false;
            return bed.Medical || owner || BedOwnerWillShare(bed, sleeper, guestStatusOverride);
        }
        /// <summary>模拟空位、访客身份及原版恋人关系对共享许可的影响，供后缀补丁补充 SSC 许可。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool BedOwnerWillShare(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus)
        {
            if (bed.OwnersForReading.Count == 0) return true;
            if (!bed.AnyUnownedSleepingSlot) return false;
            if (sleeper.IsSlave || sleeper.IsPrisoner || guestStatus == GuestStatus.Slave || guestStatus == GuestStatus.Prisoner) return true;
            return bed.OwnersForReading.Any(p => LovePartnerRelationUtility.LovePartnerRelationExists(sleeper, p));
        }
        /// <summary>记录生产代码传入的验证参数，并结合用例设置的可达、预留结果判断床位。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsValidBedFor(Thing bedThing, Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool allowMedBedEvenIfSetToNoCare, bool ignoreOtherReservations, GuestStatus? guestStatus)
        {
            ValidationCalls++;
            LastTraveler = traveler; LastCheckSocial = checkSocialProperness;
            LastIgnoreReservations = ignoreOtherReservations; LastAllowMed = allowMedBedEvenIfSetToNoCare;
            return CanUseBedNow(bedThing, sleeper, checkSocialProperness, allowMedBedEvenIfSetToNoCare, guestStatus)
                && ((Building_Bed)bedThing).Reachable && (ignoreOtherReservations || ((Building_Bed)bedThing).Reservable);
        }
        /// <summary>返回用例预置的原版找床结果，让生产后缀决定是否保留医疗/死眠结果或优先指定床。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Building_Bed FindBedFor(Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool ignoreOtherReservations, GuestStatus? guestStatus) => sleeper.VanillaBed;
    }
    public static class HealthAIUtility
    {
        /// <summary>返回用例设置的医疗休息需求，用于验证医疗优先级。</summary>
        public static bool ShouldSeekMedicalRest(Pawn pawn) => pawn.MedicalRest;
    }
    public static class ThingDefOf { public static ThingDef DeathrestCasket = new ThingDef(); }
    public class ThoughtDef { public string defName; public bool IsMemory = true; public float[] moods = { -6, -3, 2, 5, 9, 13 }; }
    public class Thought_Memory
    {
        public ThoughtDef def;
        public int CurStageIndex;
        public Pawn otherPawn;
        /// <summary>按当前阶段读取固定心情值，不模拟其他心情修正。</summary>
        public float MoodOffset() => def.moods[CurStageIndex];
    }
    public static class ThoughtMaker
    {
        /// <summary>创建指定阶段的测试记忆；定义是否合法由生产代码及 XML 检查负责验证。</summary>
        public static Thought_Memory MakeThought(ThoughtDef def, int stage) => new Thought_Memory { def = def, CurStageIndex = stage };
    }
    public static class ThoughtDefOf
    {
        public static ThoughtDef SleptInBedroom = new ThoughtDef { defName = "Bedroom" };
        public static ThoughtDef SleptInBarracks = new ThoughtDef { defName = "Barracks" };
    }
    public static class Toils_LayDown
    {
        /// <summary>提供实际睡眠记录补丁的可拦截入口，模型自身不增加休息需求。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ApplyBedRelatedEffects(Pawn p, Building_Bed bed, bool asleep, bool gainRest, int delta) { }
        /// <summary>提供房间记忆清理补丁的入口；用例自行布置原有房间记忆。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ApplyBedThoughts(Pawn actor, Building_Bed bed) { }
        /// <summary>先触发房间心情入口，再由生产后缀结算同床记忆，保留起床回调的先后顺序。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] public static void FinalizeLayingJob(Pawn pawn, Building_Bed bed, bool deathrest) { ApplyBedThoughts(pawn, bed); }
    }
    public class Dialog_AssignBuildingOwner
    {
        private CompAssignableToPawn assignable;
        /// <summary>保存原版窗口模型的分配组件，供生产 transpiler 通过字段读取。</summary>
        public Dialog_AssignBuildingOwner(CompAssignableToPawn assignable) { this.assignable = assignable; }
        /// <summary>提供已分配角色的姓名绘制入口，并推进模型行位置。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawAssignedRow(Pawn pawn, ref float y, Rect viewRect, int i) { Widgets.LabelEllipses(viewRect, pawn.LabelShortCap); y += 35f; }
        /// <summary>提供未分配角色的姓名绘制入口，保留与生产补丁一致的参数位置。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawUnassignedRow(Pawn pawn, ref float y, Rect viewRect, int i) { Widgets.LabelEllipses(viewRect, pawn.LabelShortCap); y += 35f; }
        /// <summary>在固定区域调用对应角色行，供标记、提示和绘制状态测试使用。</summary>
        public void Draw(Pawn pawn, bool assigned)
        {
            float y = 0;
            if (assigned) DrawAssignedRow(pawn, ref y, new Rect(0, 0, 200, 35), 0);
            else DrawUnassignedRow(pawn, ref y, new Rect(0, 0, 200, 35), 0);
        }
    }
}

namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public class Need_Corruption { public float CurLevelPercentage = 0.2f; }
    public class CompSexSlaveTraining { public PawnIdentity pawnIdentity; public Pawn selectedTrainer; public SSCSharedSleepRecord sharedSleep; public bool slaveTrainerEnabled, trainerIdentityInitialized = true, TrainerQualified = true; }
    public enum TrainerSpecializationFailure { None, NotQualified }
    public static class TrainerSpecializationUtility
    {
        /// <summary>同床套件只模拟资格结果；实际持续条件和 20% 计算由 TrainerIdentity 链接生产工具验证。</summary>
        public static bool HasTrainerQualification(Pawn pawn, out TrainerSpecializationFailure failure)
        {
            bool qualified = pawn?.Training?.TrainerQualified == true;
            failure = qualified ? TrainerSpecializationFailure.None : TrainerSpecializationFailure.NotQualified;
            return qualified;
        }
    }
    public class Hediff_ChainOfSexSlave { public Pawn LinkedPawn; }
    public static partial class SSCIdentityUtility
    {
        /// <summary>读取明确设置的 SSC 主人身份，不因担任调教员而自动授予主人标签。</summary>
        public static bool IsMaster(Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Master;
        /// <summary>仅根据 SSC 身份字段识别性奴，不使用原版奴隶身份或关系作为替代。</summary>
        public static bool IsSexSlave(Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Slave;
    }
    public static class SSCBondUtility
    {
        /// <summary>返回用例预置的锁链，包括可用于验证损坏引用的空主人锁链。</summary>
        public static Hediff_ChainOfSexSlave GetChain(Pawn pawn) => pawn?.Chain;
    }
    public static class TrainerAssignmentUtility
    {
        /// <summary>同床模型只接入有效指派；完整生产指派服务另由 TrainerIdentity 套件直接验证。</summary>
        public static Pawn GetActiveAssignedTrainer(Pawn slave)
        {
            Pawn trainer = slave?.Training.selectedTrainer;
            return trainer != null && trainer != slave && !trainer.Dead && !trainer.Destroyed
                && SSCIdentityUtility.IsTrainer(trainer) ? trainer : null;
        }
    }
    public static class SSCDefOf
    {
        public static ThoughtDef SSC_SharedBedWithMaster = new ThoughtDef { defName = "SSC_SharedBedWithMaster" };
        public static ThoughtDef SSC_SharedBedWithTrainer = new ThoughtDef { defName = "SSC_SharedBedWithTrainer" };
        public static object SSC_FlawedLovers = new object();
    }
}
