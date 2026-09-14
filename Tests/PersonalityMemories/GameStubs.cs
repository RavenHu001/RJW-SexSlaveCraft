using System;
using System.Collections.Generic;
using System.Linq;

namespace Verse
{
    public interface IExposable
    {
        /// <summary>提供与游戏相同的存档入口，供生产快照调用。</summary>
        void ExposeData();
    }

    public enum LoadSaveMode { Inactive, Saving, LoadingVars }

    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> Values = new Dictionary<string, object>();
    }

    public static class Scribe_Values
    {
        /// <summary>模拟原生值存档的缺省省略与缺失回退，区分旧字段缺失和显式零值。</summary>
        public static void Look<T>(ref T value, string label, T defaultValue = default(T), bool forceSave = false)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (forceSave || !EqualityComparer<T>.Default.Equals(value, defaultValue))
                    Scribe.Values[label] = value;
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.Values.TryGetValue(label, out object saved) ? (T)saved : defaultValue;
        }
    }

    public static class Scribe_Defs
    {
        /// <summary>保存或读取定义引用，保留生产代码使用的键名。</summary>
        public static void Look<T>(ref T value, string label) where T : class
        {
            Scribe_Values.Look(ref value, label);
        }
    }

    public static class Scribe_References
    {
        /// <summary>保存或读取对象引用；测试只验证引用传递，不模拟游戏引用解析器。</summary>
        public static void Look<T>(ref T value, string label, bool saveDestroyedThings = false) where T : class
        {
            Scribe_Values.Look(ref value, label);
        }
    }

    public static class Log
    {
        public static readonly List<string> Warnings = new List<string>();

        /// <summary>收集生产代码的容错日志，以便断言损坏条目不会中断后续恢复。</summary>
        public static void Warning(string message) => Warnings.Add(message);
    }

    public sealed class Pawn
    {
        public bool Destroyed;
        public string LabelShort = "测试角色";
        public RimWorld.Pawn_NeedsTracker needs;

        /// <summary>建立具有独立记忆列表的角色，供跨身体恢复测试使用。</summary>
        public Pawn()
        {
            needs = new RimWorld.Pawn_NeedsTracker
            {
                mood = new RimWorld.Need_Mood
                {
                    thoughts = new RimWorld.ThoughtHandler
                    {
                        memories = new RimWorld.MemoryThoughtHandler(this)
                    }
                }
            };
        }
    }
}

namespace RimWorld
{
    public sealed class Precept { }
    public sealed class Pawn_NeedsTracker { public Need_Mood mood; }
    public sealed class Need_Mood { public ThoughtHandler thoughts; }
    public sealed class ThoughtHandler { public MemoryThoughtHandler memories; }
    public sealed class ThoughtStage { public float baseOpinionOffset; public float baseMoodEffect; }

    public sealed class ThoughtDef
    {
        public string defName = "测试记忆";
        public List<ThoughtStage> stages = new List<ThoughtStage>();
        public Func<Thought_Memory> factory;
        public bool allowed = true;
        public bool throwOnCheck;
        public int stackLimit = 10;
        public int stackLimitForSameOtherPawn = 10;
    }

    public class Thought_Memory
    {
        public ThoughtDef def;
        public Verse.Pawn pawn;
        public Verse.Pawn otherPawn;
        public float moodPowerFactor = 1f;
        public int moodOffset;
        public int age;
        public bool permanent;
        public int durationTicksOverride = -1;
        public Precept sourcePrecept;
        private int forcedStage;
        public int CurStageIndex => forcedStage;
        public ThoughtStage CurStage => def.stages[forcedStage];

        /// <summary>按引擎契约在初始化前设置阶段。</summary>
        public void SetForcedStage(int stageIndex) => forcedStage = stageIndex;

        /// <summary>保留原生普通记忆的空初始化行为。</summary>
        public virtual void Init() { }

        /// <summary>模拟本组测试涉及的阶段与关联人物分组。</summary>
        public virtual bool GroupsWith(Thought_Memory other)
            => def == other.def && CurStageIndex == other.CurStageIndex && otherPawn == other.otherPawn;
    }

    public class Thought_MemorySocial : Thought_Memory
    {
        public float opinionOffset;

        /// <summary>按照本机引擎的真实初始化顺序，使用当前阶段的定义数值。</summary>
        public override void Init() => opinionOffset = CurStage.baseOpinionOffset;
    }

    public sealed class DynamicTrainingMemory : Thought_MemorySocial
    {
        /// <summary>复现动态训练记忆按实际好感区间分组的关键规则。</summary>
        public override bool GroupsWith(Thought_Memory other)
            => other is DynamicTrainingMemory memory && base.GroupsWith(other)
                && DisplayStage(opinionOffset) == DisplayStage(memory.opinionOffset);

        /// <summary>使用生产动态训练记忆的显示区间，检验数值在交给引擎之前已恢复。</summary>
        private static int DisplayStage(float value)
            => value < 0f ? 0 : value <= 5f ? 1 : value <= 10f ? 2 : 3;
    }

    public static class ThoughtMaker
    {
        /// <summary>按原生默认重载创建第零阶段记忆，并运行初始化。</summary>
        public static Thought_Memory MakeThought(ThoughtDef def)
        {
            var memory = def.factory();
            memory.def = def;
            memory.Init();
            return memory;
        }

        /// <summary>按本机引擎顺序先指定阶段，再初始化对应的默认数值。</summary>
        public static Thought_Memory MakeThought(ThoughtDef def, int stageIndex)
        {
            var memory = def.factory();
            memory.def = def;
            memory.SetForcedStage(stageIndex);
            memory.Init();
            return memory;
        }
    }

    public static class ThoughtUtility
    {
        /// <summary>模拟正常筛选和外部定义检查异常，检验生产恢复循环的容错边界。</summary>
        public static bool CanGetThought(Verse.Pawn pawn, ThoughtDef def)
        {
            if (def.throwOnCheck) throw new InvalidOperationException("模拟记忆检查异常");
            return def.allowed;
        }
    }

    public sealed class MemoryThoughtHandler
    {
        private readonly Verse.Pawn pawn;
        public List<Thought_Memory> Memories = new List<Thought_Memory>();
        public readonly List<Thought_Memory> ReceivedMemories = new List<Thought_Memory>();

        /// <summary>记录记忆归属，模拟原生处理器构造。</summary>
        public MemoryThoughtHandler(Verse.Pawn pawn) => this.pawn = pawn;

        /// <summary>复现原生人物传递、普通记忆合并和数量上限；不模拟全套游戏附加事件。</summary>
        public void TryGainMemory(Thought_Memory memory, Verse.Pawn otherPawn = null)
        {
            if (!ThoughtUtility.CanGetThought(pawn, memory.def)) return;
            if (memory is Thought_MemorySocial)
            {
                otherPawn = otherPawn ?? memory.otherPawn;
                if (otherPawn == null) return;
            }
            memory.pawn = pawn;
            memory.otherPawn = otherPawn;
            ReceivedMemories.Add(memory);
            var group = Memories.Where(memory.GroupsWith).ToList();
            if (!(memory is Thought_MemorySocial) && group.Count >= memory.def.stackLimit && group.Count > 0)
                group.OrderByDescending(m => m.age).First().age = 0;
            else
                Memories.Add(memory);

            if (memory.def.stackLimitForSameOtherPawn >= 0)
                while (Memories.Count(memory.GroupsWith) > memory.def.stackLimitForSameOtherPawn)
                    Memories.Remove(Memories.Where(memory.GroupsWith).OrderByDescending(m => m.age).First());
            if (memory.def.stackLimit >= 0)
                while (Memories.Count(m => m.def == memory.def) > memory.def.stackLimit)
                    Memories.Remove(Memories.Where(m => m.def == memory.def).OrderByDescending(m => m.age).First());
        }
    }
}
