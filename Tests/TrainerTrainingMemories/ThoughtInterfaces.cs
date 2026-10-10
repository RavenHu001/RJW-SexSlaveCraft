using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

// 想法与记忆接口模型：按已核验的本机程序集契约实现初始化、分组、合并与存读档。
namespace RimWorld
{
    public sealed class ThoughtStage
    {
        public string label;
        public string labelSocial;
        public string description;
        public float baseMoodEffect;
        public float baseOpinionOffset;
    }

    public sealed class ThoughtDef : Verse.Def
    {
        public Type thoughtClass;
        public Type workerClass;
        public int stackLimit = 3;
        public int stackLimitForSameOtherPawn = 3;
        public float durationDays = 1f;
        public bool showBubble;
        public bool stagesStack;
        public List<ThoughtStage> stages = new List<ThoughtStage>();
    }

    /// <summary>想法基类：阶段索引只由 forcedStage 提供，与原版一致。</summary>
    public abstract class Thought
    {
        public ThoughtDef def;
        public Verse.Pawn pawn;

        public virtual int CurStageIndex => 0;

        /// <summary>记忆剩余期限；基类用定义整天数，记忆子类再用覆盖值收窄。</summary>
        public virtual int DurationTicks => (int)((def?.durationDays ?? 0f) * 60000f);

        public virtual ThoughtStage CurStage
            => (def?.stages != null && CurStageIndex >= 0 && CurStageIndex < def.stages.Count)
                ? def.stages[CurStageIndex]
                : null;

        public virtual string LabelCap => CurStage?.label ?? def?.defName;

        public virtual string LabelCapSocial => LabelCap;

        public virtual string Description => CurStage?.description ?? string.Empty;

        public virtual float MoodOffset() => CurStage?.baseMoodEffect ?? 0f;

        public virtual bool GroupsWith(Thought other) => other != null && def == other.def;
    }

    public class Thought_Memory : Thought
    {
        private int forcedStage;

        public Verse.Pawn otherPawn;
        public int age;
        public float moodPowerFactor = 1f;
        public int moodOffset;
        public int durationTicksOverride = -1;
        public bool permanent;
        public Precept sourcePrecept;

        public override int CurStageIndex => forcedStage;

        /// <summary>
        /// 复现原版：存在关联角色时把其短名格式化进标签。
        /// 原版分组在人物不同时会退回标签比较，因此标签必须能区分不同人物。
        /// </summary>
        public override string LabelCap
            => otherPawn != null ? base.LabelCap + " (" + otherPawn.LabelShort + ")" : base.LabelCap;

        /// <summary>显式覆盖期限优先于定义整天数。</summary>
        public override int DurationTicks
            => durationTicksOverride >= 0 ? durationTicksOverride : base.DurationTicks;

        /// <summary>按原版顺序在初始化之前写入阶段。</summary>
        public void SetForcedStage(int stageIndex) => forcedStage = stageIndex;

        /// <summary>保留原生记忆的空初始化行为。</summary>
        public virtual void Init() { }

        public virtual void Renew() => age = 0;

        public virtual void ExposeData()
        {
            // 显式写出默认值类型，避免泛型推断失败；原版此处走引用存档入口。
            Scribe_Values.Look(ref otherPawn, "otherPawn", (Verse.Pawn)null);
            Scribe_Values.Look(ref sourcePrecept, "sourcePrecept", (Precept)null);
            Scribe_Values.Look(ref moodPowerFactor, "moodPowerFactor", 1f);
            Scribe_Values.Look(ref moodOffset, "moodOffset", 0);
            Scribe_Values.Look(ref age, "age", 0);
            // 原版以 stageIndex 为键保存 forcedStage，弱套档位因此无需额外字段。
            Scribe_Values.Look(ref forcedStage, "stageIndex", 0);
            Scribe_Values.Look(ref durationTicksOverride, "durationTicksOverride", -1);
            Scribe_Values.Look(ref permanent, "permanent", false);
        }

        /// <summary>
        /// 复现原版：社交标签把关联角色的短名格式化进去。
        /// 原版 <see cref="Thought_Memory.GroupsWith"/> 在人物不同时会退回标签比较，
        /// 覆写了 <see cref="LabelCap"/> 的记忆类必须让社交标签仍能区分不同对象。
        /// </summary>
        public override string LabelCapSocial
            => LabelCap + " (" + (otherPawn?.LabelShort ?? "???") + ")";

        /// <summary>复现原版比较：先比定义与阶段索引，再做带标签兜底的人物比较。</summary>
        public override bool GroupsWith(Thought other)
        {
            if (!(other is Thought_Memory memory)) return false;
            if (!base.GroupsWith(other)) return false;
            if (otherPawn == memory.otherPawn) return true;
            // 原版此处使用 LabelCapSocial 语义的标签兜底比较。
            return LabelCapSocial == memory.LabelCapSocial;
        }

        /// <summary>复现原版合并：达到同组上限时刷新最老一份，否则返回 false 由调用方新增。</summary>
        public virtual bool TryMergeWithExistingMemory(out bool showBubble)
        {
            var handler = pawn?.needs?.mood?.thoughts;
            if (handler != null && handler.memories.NumMemoriesInGroup(this) >= def.stackLimit)
            {
                Thought_Memory oldest = handler.memories.OldestMemoryInGroup(this);
                if (oldest != null)
                {
                    showBubble = oldest.age > oldest.DurationTicks / 2;
                    oldest.Renew();
                    return true;
                }
            }
            showBubble = true;
            return false;
        }

        public override float MoodOffset() => (CurStage?.baseMoodEffect ?? 0f) * moodPowerFactor + moodOffset;
    }

    public class Thought_MemorySocial : Thought_Memory
    {
        public float opinionOffset;

        /// <summary>按原版顺序从当前阶段的定义值初始化社交基础偏移。</summary>
        public override void Init() => opinionOffset = CurStage?.baseOpinionOffset ?? 0f;

        public virtual float OpinionOffset() => opinionOffset;

        /// <summary>原版社交记忆不参与合并，每次都是新增一份。</summary>
        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            showBubble = false;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref opinionOffset, "opinionOffset", 0f);
        }
    }

    public static class ThoughtMaker
    {
        /// <summary>复现原版创建顺序：先建立实例与定义，再指定阶段，最后执行 Init。</summary>
        public static Thought_Memory MakeThought(ThoughtDef def, int stage = 0)
        {
            var memory = (Thought_Memory)Activator.CreateInstance(def.thoughtClass);
            memory.def = def;
            memory.SetForcedStage(stage);
            memory.Init();
            return memory;
        }
    }
}

namespace Verse
{
    public sealed class Precept { }

    /// <summary>需要追踪与心情容器共享同一份实例，记忆才会落回同一处。</summary>
    public sealed class Pawn_NeedsTracker
    {
        public Need_Mood mood = new Need_Mood();
    }

    public sealed class Need_Mood
    {
        public ThoughtHandler thoughts = new ThoughtHandler();
    }

    /// <summary>想法处理器：记忆容器持有其所属角色，供原生发放契约写入 pawn。</summary>
    public sealed class ThoughtHandler
    {
        public RimWorld.MemoryThoughtHandler memories = new RimWorld.MemoryThoughtHandler();
    }
}

namespace RimWorld
{
    /// <summary>记忆容器模型：按已核验契约处理合并尝试、容量移除与新增。</summary>
    public sealed class MemoryThoughtHandler
    {
        public readonly List<Thought_Memory> Memories = new List<Thought_Memory>();

        /// <summary>所属角色；由角色建立心情追踪时注入。</summary>
        public Verse.Pawn owner;

        public int NumMemoriesInGroup(Thought_Memory group)
            => Memories.Count(m => m.GroupsWith(group));

        public int NumMemoriesOfDef(ThoughtDef def)
            => Memories.Count(m => ReferenceEquals(m.def, def));

        public Thought_Memory OldestMemoryInGroup(Thought_Memory group)
            => Memories.Where(m => m.GroupsWith(group)).OrderByDescending(m => m.age).FirstOrDefault();

        public Thought_Memory OldestMemoryOfDef(ThoughtDef def)
            => Memories.Where(m => ReferenceEquals(m.def, def)).OrderByDescending(m => m.age).FirstOrDefault();

        public void TryGainMemory(Thought_Memory newThought, Verse.Pawn otherPawn = null)
        {
            if (newThought == null) return;
            if (!ThoughtUtility.CanGetThought(owner, newThought.def)) return;

            if (newThought is Thought_MemorySocial)
            {
                // 社交记忆必须有对象，否则原版直接报错返回。
                otherPawn = otherPawn ?? newThought.otherPawn;
                if (otherPawn == null) return;
            }

            newThought.pawn = owner;
            newThought.otherPawn = otherPawn;

            if (!newThought.TryMergeWithExistingMemory(out _)) Memories.Add(newThought);

            // 按已核验的原生契约：容量由“移除最老一份”维持，先按同对象上限，再按同定义上限；
            // 社交记忆不参与合并，因此这与普通记忆的 Renew 刷新语义不同。
            if (newThought.def.stackLimitForSameOtherPawn >= 0)
            {
                while (NumMemoriesInGroup(newThought) > newThought.def.stackLimitForSameOtherPawn)
                {
                    Thought_Memory oldest = OldestMemoryInGroup(newThought);
                    if (oldest == null) break;
                    Memories.Remove(oldest);
                }
            }

            if (newThought.def.stackLimit >= 0)
            {
                while (NumMemoriesOfDef(newThought.def) > newThought.def.stackLimit)
                {
                    Thought_Memory oldest = OldestMemoryOfDef(newThought.def);
                    if (oldest == null) break;
                    Memories.Remove(oldest);
                }
            }
        }

        /// <summary>按原版重载：给出定义时创建实例、设定阶段并运行初始化。</summary>
        public void TryGainMemory(ThoughtDef def, Verse.Pawn otherPawn = null, Precept sourcePrecept = null)
        {
            if (def == null) return;
            Thought_Memory memory = ThoughtMaker.MakeThought(def);
            memory.sourcePrecept = sourcePrecept;
            TryGainMemory(memory, otherPawn);
        }

        public void RemoveMemoriesOfDef(ThoughtDef def) => Memories.RemoveAll(m => m.def == def);

        public void RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDef def, Verse.Pawn otherPawn)
            => Memories.RemoveAll(m => m.def == def && m.otherPawn == otherPawn);
    }
}
