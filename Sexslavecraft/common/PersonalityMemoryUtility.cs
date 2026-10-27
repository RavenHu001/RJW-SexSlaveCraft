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
#if SSC_RW17
        // 可选历史快照；缺节点不改变既有记忆版本与恢复语义。
        public SSCRomanceMoodData romanceMood;
#endif

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
#if SSC_RW17
            Scribe_Deep.Look(ref romanceMood, "sscRomanceMood");
#endif
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
#if SSC_RW17
            // 准入后置回调能立即提取人格；尚未提交的唯一候选不能被采集为已发生历史。
            if (memory is Thought_MemorySSCRomance pending && (pending.IsPendingNewEvent || pending.CopiedPendingEvent)) return null;
#endif

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
                sourcePrecept = memory.sourcePrecept,
#if SSC_RW17
                romanceMood = (memory as Thought_MemorySSCRomance)?.Data?.Copy()
#endif
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
                sourcePrecept = memory.sourcePrecept,
#if SSC_RW17
                romanceMood = memory.romanceMood?.Copy()
#endif
            };
        }

        /// <summary>替换目标记忆，先按保存阶段创建，再还原实例数值，并让引擎应用原有可获得性和堆叠规则。</summary>
        public static void Restore(Pawn target, List<StoredMemoryData> memories)
        {
            if (target?.needs?.mood?.thoughts?.memories == null || memories == null) return;

            var memoryHandler = target.needs.mood.thoughts.memories;
#if SSC_RW17
            // 恢复本来就在合法人格替换作用域内；只校验原 token／载体，不按 IsMutating 拒绝自身。
            object restoreToken = SSCPersonalityRuntimeUtility.Capture(target);
            var restoreMood = target.needs.mood;
            var restoreComp = target.TryGetComp<CompSexSlaveTraining>();
            Func<bool> current = () => SSCPersonalityRuntimeUtility.IsCurrent(target, restoreToken) &&
                target.TryGetComp<CompSexSlaveTraining>() == restoreComp && target.needs?.mood == restoreMood &&
                target.needs?.mood?.thoughts?.memories == memoryHandler;
            // 工厂和思想准入允许回调；先复制全部来源，不能让后续回调改写尚未恢复的保存事实。
            memories = memories.ConvertAll(Copy);
            ISSCRomanceHistoricalRestore historicalBatch = null;
#endif
            memoryHandler.Memories.Clear();
            foreach (StoredMemoryData data in memories)
            {
#if SSC_RW17
                if (!current()) return;
#endif
                if (data?.def == null) continue;

                try
                {
                    if (!ThoughtUtility.CanGetThought(target, data.def)) continue;
#if SSC_RW17
                    if (!current()) return;
#endif

                    bool hasInstanceData = data.memoryDataVersion >= CurrentMemoryDataVersion;
                    int restorableStage = hasInstanceData ? GetRestorableStage(data) : -1;
                    Thought_Memory memory = hasInstanceData
                        ? ThoughtMaker.MakeThought(data.def, restorableStage)
                        : ThoughtMaker.MakeThought(data.def) as Thought_Memory;
                    if (memory == null) continue;
#if SSC_RW17
                    if (!current()) return;
                    // 历史恢复也核对工厂实际返回载体；不能把保存快照安装到回调换出的另一种思想上。
                    if (memory.def != data.def || memory.GetType() != data.def.ThoughtClass ||
                        (hasInstanceData && memory.CurStageIndex != restorableStage)) continue;
#endif

                    memory.age = data.age;
                    memory.moodPowerFactor = data.moodPowerFactor;
                    Pawn otherPawn = data.otherPawn != null && !data.otherPawn.Destroyed ? data.otherPawn : null;
#if SSC_RW17
                    // SSC 语义键依赖原保存对象；即使对象已销毁也保留尚能解析的引用，不能把不同历史合并为空对象。
                    // 普通社交记忆继续沿原有效目标规则，不因 SSC 的保存边界改变其他模块。
                    if (memory is Thought_MemorySSCRomance) otherPawn = data.otherPawn;
#endif
                    memory.otherPawn = otherPawn;

                    if (hasInstanceData)
                    {
                        memory.moodOffset = data.moodOffset;
                        memory.durationTicksOverride = data.durationTicksOverride;
                        memory.permanent = data.permanent;
                        memory.sourcePrecept = data.sourcePrecept;
#if SSC_RW17
                        if (memory is Thought_MemorySSCRomance romance) romance.Data = data.romanceMood?.Copy();
#endif
                        if (data.hasOpinionOffset && memory is Thought_MemorySocial socialMemory)
                            socialMemory.opinionOffset = data.opinionOffset;
                    }

                    // 社交记忆必须有有效对象；普通记忆也要显式传入对象，否则引擎会将其覆盖为空引用。
                    if (memory is Thought_MemorySocial && otherPawn == null) continue;
#if SSC_RW17
                    // SSC 历史不回放原生新获得的 Renew／关联思想；其他记忆保留原恢复流程。
                    if (memory is ISSCRomanceHistoricalRestore romanceRestore)
                    {
                        historicalBatch = romanceRestore;
                        romanceRestore.RestoreHistorical(target, otherPawn, current);
                    }
                    else memoryHandler.TryGainMemory(memory, otherPawn);
#else
                    memoryHandler.TryGainMemory(memory, otherPawn);
#endif
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SSC] 人格植入跳过无效记忆：定义={data.def.defName}，角色={target.LabelShort}，原因={ex.Message}");
                }
            }
#if SSC_RW17
            historicalBatch?.CompleteHistoricalRestore(target, current);
#endif
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
