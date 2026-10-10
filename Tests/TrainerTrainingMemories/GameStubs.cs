using System;
using System.Collections.Generic;

// 本套件只复现记忆发放所需的引擎接口，业务判定全部链接生产源码。
namespace UnityEngine
{
    /// <summary>只提供生产代码使用的数学函数；舍入采用“五入”而不是银行家舍入。</summary>
    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;

        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        public static int CeilToInt(float value) => (int)Math.Ceiling(value);

        public static int FloorToInt(float value) => (int)Math.Floor(value);

        public static float Abs(float value) => Math.Abs(value);

        public static float Max(float a, float b) => Math.Max(a, b);

        public static float Min(float a, float b) => Math.Min(a, b);

        public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);

        public static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
    }
}

namespace Verse
{
    public class Def
    {
        public string defName;
    }

    public interface IExposable
    {
        void ExposeData();
    }

    /// <summary>原版启动构造标记；本套件由类型初始化走同一入口，属性本身不参与行为。</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute
    {
    }

    /// <summary>仅记录日志调用；本套件不校验告警文本。</summary>
    public static class Log
    {
        public static readonly List<string> Warnings = new List<string>();

        public static void Warning(string message) => Warnings.Add(message);

        public static void Error(string message) => Warnings.Add(message);
    }

    /// <summary>存档接口模型：加载阶段读字典，保存阶段写字典。</summary>
    public static class Scribe_Values
    {
        public static bool Loading;
        public static readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public static void Reset()
        {
            Loading = false;
            Data.Clear();
            Scribe_Defs.Data.Clear();
            Scribe_References.Data.Clear();
        }

        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Loading) value = Data.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
            else Data[key] = value;
        }
    }

    /// <summary>定义引用独立成表，使存读档往返可以验证定义未被替换。</summary>
    public static class Scribe_Defs
    {
        public static readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public static void Look<T>(ref T value, string key) where T : class
        {
            if (Scribe_Values.Loading)
                value = Data.TryGetValue(key, out object saved) ? (T)saved : null;
            else
                Data[key] = value;
        }
    }

    /// <summary>角色引用独立成表，保持对象身份可比较。</summary>
    public static class Scribe_References
    {
        public static readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public static void Look<T>(ref T value, string key, bool saveDestroyedThings = false, bool parallel = false)
            where T : class
        {
            if (Scribe_Values.Loading)
                value = Data.TryGetValue(key, out object saved) ? (T)saved : null;
            else
                Data[key] = value;
        }

        public static void Look<T>(ref T value, string key) where T : class => Look(ref value, key, false);
    }

    /// <summary>译文扩展：缺键时保留键名，便于断言键清单本身。</summary>
    public static class Extensions
    {
        public static readonly Dictionary<string, string> Translations = new Dictionary<string, string>();

        public static string Translate(this string key, params object[] args)
            => string.Format(Translations.TryGetValue(key, out string text) ? text : key, args);

        public static string CapitalizeFirst(this string text)
            => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>按原版惯例输出带符号整数偏移，供记忆描述格式化。</summary>
        public static string ToStringWithSign(this float value)
        {
            int rounded = (int)value;
            return rounded > 0 ? "+" + rounded : rounded.ToString();
        }

        public static string ToStringWithSign(this int value)
            => value > 0 ? "+" + value : value.ToString();
    }

    /// <summary>模型角色：只需组件容器、心情追踪、技能与存活标记。</summary>
    public class Pawn : IExposable
    {
        public bool Dead;
        public bool Destroyed;
        public bool Discarded;

        /// <summary>显示名沿用实际引用的当前名字，改名不改变记忆定义与阶段。</summary>
        public string LabelShort = "测试角色";

        public RimWorld.Pawn_NeedsTracker needs;
        public RimWorld.SkillsTracker skills = new RimWorld.SkillsTracker();
        public RimWorld.Pawn_RelationsTracker relations = new RimWorld.Pawn_RelationsTracker();
        public RimWorld.GuestTracker guest;
        public RimWorld.HealthTracker health = new RimWorld.HealthTracker();

        private readonly Dictionary<Type, object> comps = new Dictionary<Type, object>();

        public Pawn()
        {
            needs = new RimWorld.Pawn_NeedsTracker();
            // 记忆容器需要知道自己是哪个角色的，才能按原生契约写入 pawn。
            needs.mood.thoughts.memories.owner = this;
        }

        public void AddComp<T>(T comp) => comps[typeof(T)] = comp;

        public T TryGetComp<T>() where T : class
            => comps.TryGetValue(typeof(T), out object comp) ? (T)comp : null;

        public void ExposeData() { }
    }
}

namespace RimWorld
{
    public sealed class MentalStateDef : Verse.Def { }

    /// <summary>心情追踪与技能追踪的最小容器；记忆最终落在这份心情容器上。</summary>
    public sealed class Need_Mood { public Verse.ThoughtHandler thoughts = new Verse.ThoughtHandler(); }

    public sealed class Pawn_NeedsTracker { public Need_Mood mood = new Need_Mood(); }

    public sealed class SkillDef : Verse.Def { }

    public sealed class SkillRecord
    {
        public SkillDef def;
        public int Level;
    }

    /// <summary>技能查询的最小模型；只提供旧模式评分与等级判定所需的社交技能等级。</summary>
    public sealed class SkillsTracker
    {
        public readonly List<SkillRecord> skills = new List<SkillRecord>();

        public SkillRecord GetSkill(SkillDef def) => skills.Find(s => s.def == def);
    }

    public sealed class MessageTypeDefOf
    {
        public static readonly object NeutralEvent = new object();
        public static readonly object PositiveEvent = new object();
        public static readonly object NegativeEvent = new object();
        public static readonly object RejectInput = new object();
    }

    public static class Messages
    {
        public static int MessageCount;

        public static void Message(string text, Verse.Pawn target, object type, bool historical = true)
            => MessageCount++;
    }

    public static class ThoughtUtility
    {
        /// <summary>模型角色均视为可获得想法，隔离意识形态前置条件。</summary>
        public static bool CanGetThought(Verse.Pawn pawn, ThoughtDef def) => true;
    }
}
