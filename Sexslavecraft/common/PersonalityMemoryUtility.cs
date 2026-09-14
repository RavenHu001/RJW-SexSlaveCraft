using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存记忆的定义和实例状态，并通过版本号区分旧凝胶中未保存的数值。</summary>
    public class StoredMemoryData : IExposable
    {
        public ThoughtDef def;
        public int age;
        public float moodPowerFactor = 1f;
        public Pawn otherPawn;
        public int memoryDataVersion;
        public int stageIndex;
        public bool hasOpinionOffset;
        public float opinionOffset;
        public int moodOffset;
        public int durationTicksOverride = -1;
        public bool permanent;
        public Precept sourcePrecept;

        /// <summary>保存或读取记忆定义、年龄、倍率、关联角色及新版实例状态；旧数据的版本保持为零。</summary>
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref age, "age", 0);
            Scribe_Values.Look(ref moodPowerFactor, "moodPowerFactor", 1f);
            Scribe_References.Look(ref otherPawn, "otherPawn", saveDestroyedThings: true);
            Scribe_Values.Look(ref memoryDataVersion, "memoryDataVersion", 0);
            Scribe_Values.Look(ref stageIndex, "stageIndex", 0);
            Scribe_Values.Look(ref hasOpinionOffset, "hasOpinionOffset", false);
            Scribe_Values.Look(ref opinionOffset, "opinionOffset", 0f);
            Scribe_Values.Look(ref moodOffset, "moodOffset", 0);
            Scribe_Values.Look(ref durationTicksOverride, "durationTicksOverride", -1);
            Scribe_Values.Look(ref permanent, "permanent", false);
            Scribe_References.Look(ref sourcePrecept, "sourcePrecept");
        }
    }

    /// <summary>统一处理人格记忆的采集、复制和恢复，防止加工或植入时丢失实例数值。</summary>
    public static class PersonalityMemoryUtility
    {
        public const int CurrentMemoryDataVersion = 1;

        /// <summary>采集记忆的实际阶段及原生实例字段，显式记录社交偏移是否存在，保留合法的零值。</summary>
        public static StoredMemoryData Capture(Thought_Memory memory)
        {
            if (memory == null) return null;

            var socialMemory = memory as Thought_MemorySocial;
            return new StoredMemoryData
            {
                def = memory.def,
                age = memory.age,
                moodPowerFactor = memory.moodPowerFactor,
                otherPawn = memory.otherPawn,
                memoryDataVersion = CurrentMemoryDataVersion,
                stageIndex = memory.CurStageIndex,
                hasOpinionOffset = socialMemory != null,
                opinionOffset = socialMemory?.opinionOffset ?? 0f,
                moodOffset = memory.moodOffset,
                durationTicksOverride = memory.durationTicksOverride,
                permanent = memory.permanent,
                sourcePrecept = memory.sourcePrecept
            };
        }

        /// <summary>复制独立的记忆快照，同时保留定义、角色和戒律的共享引用及旧格式版本。</summary>
        public static StoredMemoryData Copy(StoredMemoryData memory)
        {
            if (memory == null) return null;

            return new StoredMemoryData
            {
                def = memory.def,
                age = memory.age,
                moodPowerFactor = memory.moodPowerFactor,
                otherPawn = memory.otherPawn,
                memoryDataVersion = memory.memoryDataVersion,
                stageIndex = memory.stageIndex,
                hasOpinionOffset = memory.hasOpinionOffset,
                opinionOffset = memory.opinionOffset,
                moodOffset = memory.moodOffset,
                durationTicksOverride = memory.durationTicksOverride,
                permanent = memory.permanent,
                sourcePrecept = memory.sourcePrecept
            };
        }

        /// <summary>替换目标记忆，先按保存阶段创建，再还原实例数值，并让引擎应用原有可获得性和堆叠规则。</summary>
        public static void Restore(Pawn target, List<StoredMemoryData> memories)
        {
            if (target?.needs?.mood?.thoughts?.memories == null || memories == null) return;

            var memoryHandler = target.needs.mood.thoughts.memories;
            memoryHandler.Memories.Clear();
            foreach (StoredMemoryData data in memories)
            {
                if (data?.def == null) continue;

                try
                {
                    if (!ThoughtUtility.CanGetThought(target, data.def)) continue;

                    bool hasInstanceData = data.memoryDataVersion >= CurrentMemoryDataVersion;
                    Thought_Memory memory = hasInstanceData
                        ? ThoughtMaker.MakeThought(data.def, GetRestorableStage(data))
                        : ThoughtMaker.MakeThought(data.def) as Thought_Memory;
                    if (memory == null) continue;

                    memory.age = data.age;
                    memory.moodPowerFactor = data.moodPowerFactor;
                    Pawn otherPawn = data.otherPawn != null && !data.otherPawn.Destroyed ? data.otherPawn : null;
                    memory.otherPawn = otherPawn;

                    if (hasInstanceData)
                    {
                        memory.moodOffset = data.moodOffset;
                        memory.durationTicksOverride = data.durationTicksOverride;
                        memory.permanent = data.permanent;
                        memory.sourcePrecept = data.sourcePrecept;
                        if (data.hasOpinionOffset && memory is Thought_MemorySocial socialMemory)
                            socialMemory.opinionOffset = data.opinionOffset;
                    }

                    // 社交记忆必须有有效对象；普通记忆也要显式传入对象，否则引擎会将其覆盖为空引用。
                    if (memory is Thought_MemorySocial && otherPawn == null) continue;
                    memoryHandler.TryGainMemory(memory, otherPawn);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SSC] 人格植入跳过无效记忆：定义={data.def.defName}，角色={target.LabelShort}，原因={ex.Message}");
                }
            }
        }

        /// <summary>验证已保存的阶段；定义更新导致阶段失效时回退到首阶段，避免一条旧记忆中断整个植入。</summary>
        private static int GetRestorableStage(StoredMemoryData data)
        {
            if (data.def.stages != null && data.stageIndex >= 0 && data.stageIndex < data.def.stages.Count)
                return data.stageIndex;

            Log.Warning($"[SSC] 人格记忆的阶段已失效，使用首阶段：定义={data.def.defName}，阶段={data.stageIndex}");
            return 0;
        }
    }
}
