using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed;
    private static int failed;

    /// <summary>执行真实生产记忆工具的独立回归用例，并通过退出码报告失败。</summary>
    private static int Main()
    {
        Run("所有心情阶段与非零偏移、年龄、倍率完整恢复", MoodStages);
        Run("社交默认值及正负零小数自定义值完整恢复", SocialValues);
        Run("普通记忆的关联人物通过引擎显式参数保留", OrdinaryOtherPawn);
        Run("复制快照保留全部字段且实例相互独立", CopySnapshot);
        Run("新版完整存档字段往返保持一致", PersistenceRoundTrip);
        Run("新版零好感在缺省省略的存档中仍保持为零", PersistedZeroOpinion);
        Run("旧格式只恢复原有字段并保留定义默认好感", LegacySnapshot);
        Run("旧格式经过复制和再次存档仍保持旧版本", LegacyCopyAndSave);
        Run("缺少好感标记时保留所选阶段的定义默认值", MissingOpinionMarker);
        Run("有效阶段与好感在引擎分组前完成恢复", DynamicGroups);
        Run("已有多个普通记忆在合法堆叠范围内保持年龄和阶段", MoodStacking);
        Run("恢复会替换宿主记忆且空快照可清空旧记忆", ReplaceHost);
        Run("空输入和没有心情需求的身体不会抛异常", NullInputs);
        Run("失效人物的社交记忆跳过而普通记忆仍可恢复", InvalidOtherPawn);
        Run("空条目、筛选失败及创建异常不会中断后续记忆", InvalidEntries);
        Run("定义更新导致阶段越界时回退首阶段且保留实际值", InvalidStages);
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>为每项用例重置模拟存档和日志，记录失败而继续其余检查。</summary>
    private static void Run(string name, Action test)
    {
        Scribe.mode = LoadSaveMode.Inactive;
        Scribe.Values.Clear();
        Log.Warnings.Clear();
        try
        {
            test();
            passed++;
            Console.WriteLine("通过：" + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("失败：" + name + "\n" + ex);
        }
    }

    /// <summary>断言关键结果，不满足时给出中文失败说明。</summary>
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    /// <summary>创建包含四个不同心情与好感阶段的定义。</summary>
    private static ThoughtDef Definition(bool social = false)
    {
        return new ThoughtDef
        {
            defName = social ? "社交定义" : "心情定义",
            factory = social ? () => new Thought_MemorySocial() : () => new Thought_Memory(),
            stages = new List<ThoughtStage>
            {
                new ThoughtStage { baseMoodEffect = -10f, baseOpinionOffset = 1f },
                new ThoughtStage { baseMoodEffect = 2f, baseOpinionOffset = 3f },
                new ThoughtStage { baseMoodEffect = 8f, baseOpinionOffset = 8f },
                new ThoughtStage { baseMoodEffect = 15f, baseOpinionOffset = 15f }
            }
        };
    }

    /// <summary>向全新身体恢复一条快照，返回唯一恢复结果。</summary>
    private static Thought_Memory RestoreOne(StoredMemoryData data)
    {
        var pawn = new Pawn();
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData> { data });
        Assert(pawn.needs.mood.thoughts.memories.Memories.Count == 1, "应恢复一条有效记忆。");
        return pawn.needs.mood.thoughts.memories.Memories[0];
    }

    /// <summary>使用生产存档入口完成写入与读取，模拟默认值省略后的加载。</summary>
    private static StoredMemoryData SaveAndLoad(StoredMemoryData data)
    {
        Scribe.Values.Clear();
        Scribe.mode = LoadSaveMode.Saving;
        data.ExposeData();
        Scribe.mode = LoadSaveMode.LoadingVars;
        var restored = new StoredMemoryData();
        restored.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        return restored;
    }

    /// <summary>逐阶段验证采集及恢复，覆盖非默认持续时间、永久标志与戒律引用。</summary>
    private static void MoodStages()
    {
        ThoughtDef def = Definition();
        for (int stage = 0; stage < def.stages.Count; stage++)
        {
            Thought_Memory source = ThoughtMaker.MakeThought(def, stage);
            source.age = 1234 + stage;
            source.moodPowerFactor = 1.75f;
            source.moodOffset = stage % 2 == 0 ? -7 : 9;
            source.durationTicksOverride = 90000;
            source.permanent = true;
            source.sourcePrecept = new Precept();
            var restored = RestoreOne(PersonalityMemoryUtility.Capture(source));
            Assert(restored.CurStageIndex == stage, "阶段必须保持。");
            Assert(restored.CurStage.baseMoodEffect == def.stages[stage].baseMoodEffect, "基础心情必须保持。");
            Assert(restored.moodOffset == source.moodOffset && restored.age == source.age
                && restored.moodPowerFactor == source.moodPowerFactor, "心情实例字段必须保持。");
            Assert(restored.durationTicksOverride == 90000 && restored.permanent
                && restored.sourcePrecept == source.sourcePrecept, "原生持续时间与来源必须保持。");
        }
    }

    /// <summary>遍历各阶段与正负零小数数值，覆盖手动调整及日常动态调教的实际偏移。</summary>
    private static void SocialValues()
    {
        ThoughtDef def = Definition(true);
        var other = new Pawn();
        for (int stage = 0; stage < def.stages.Count; stage++)
        {
            foreach (float value in new[] { def.stages[stage].baseOpinionOffset, -23.5f, 0f, 42.25f })
            {
                var source = (Thought_MemorySocial)ThoughtMaker.MakeThought(def, stage);
                source.otherPawn = other;
                source.opinionOffset = value;
                source.age = 900;
                var snapshot = PersonalityMemoryUtility.Capture(source);
                Assert(snapshot.hasOpinionOffset && snapshot.memoryDataVersion == 1, "新版社交数据必须带存在标记。");
                var restored = (Thought_MemorySocial)RestoreOne(snapshot);
                Assert(restored.opinionOffset == value, "实际好感不能回退到阶段默认数值。");
                Assert(restored.CurStageIndex == stage && restored.otherPawn == other && restored.age == 900,
                    "社交阶段、关联人物与年龄必须保持。");
            }
        }
    }

    /// <summary>验证普通心情记忆的人物不会被原生入口的默认空参数覆盖。</summary>
    private static void OrdinaryOtherPawn()
    {
        var source = ThoughtMaker.MakeThought(Definition(), 2);
        source.otherPawn = new Pawn();
        Assert(RestoreOne(PersonalityMemoryUtility.Capture(source)).otherPawn == source.otherPawn,
            "普通记忆也必须显式传递关联人物。");
    }

    /// <summary>构造包含所有非默认字段的快照，供复制和序列化测试复用。</summary>
    private static StoredMemoryData FullSnapshot()
    {
        var source = (Thought_MemorySocial)ThoughtMaker.MakeThought(Definition(true), 3);
        source.otherPawn = new Pawn();
        source.sourcePrecept = new Precept();
        source.age = 3210;
        source.moodPowerFactor = 0.6f;
        source.moodOffset = -4;
        source.opinionOffset = -32.75f;
        source.durationTicksOverride = 88000;
        source.permanent = true;
        return PersonalityMemoryUtility.Capture(source);
    }

    /// <summary>逐字段比较快照，防止加工复制或存档遗漏新增实例状态。</summary>
    private static void AssertSnapshotEqual(StoredMemoryData expected, StoredMemoryData actual)
    {
        Assert(expected.def == actual.def && expected.otherPawn == actual.otherPawn
            && expected.sourcePrecept == actual.sourcePrecept, "引用字段必须保持。");
        Assert(expected.age == actual.age && expected.moodPowerFactor == actual.moodPowerFactor
            && expected.memoryDataVersion == actual.memoryDataVersion && expected.stageIndex == actual.stageIndex
            && expected.hasOpinionOffset == actual.hasOpinionOffset && expected.opinionOffset == actual.opinionOffset
            && expected.moodOffset == actual.moodOffset && expected.durationTicksOverride == actual.durationTicksOverride
            && expected.permanent == actual.permanent, "全部实例数值必须保持。");
    }

    /// <summary>验证加工复制既不丢字段，也不会共享可修改的快照对象。</summary>
    private static void CopySnapshot()
    {
        StoredMemoryData source = FullSnapshot();
        StoredMemoryData copy = PersonalityMemoryUtility.Copy(source);
        Assert(!ReferenceEquals(source, copy), "复制必须产生独立对象。");
        AssertSnapshotEqual(source, copy);
        copy.opinionOffset = 123f;
        copy.stageIndex = 0;
        Assert(source.opinionOffset == -32.75f && source.stageIndex == 3, "修改复制结果不能影响原件。");
    }

    /// <summary>验证生产 ExposeData 写入预期字段，并将存档结果再恢复到新身体。</summary>
    private static void PersistenceRoundTrip()
    {
        StoredMemoryData source = FullSnapshot();
        StoredMemoryData loaded = SaveAndLoad(PersonalityMemoryUtility.Copy(source));
        AssertSnapshotEqual(source, loaded);
        var expectedKeys = new[] { "def", "age", "moodPowerFactor", "otherPawn", "memoryDataVersion", "stageIndex",
            "hasOpinionOffset", "opinionOffset", "moodOffset", "durationTicksOverride", "permanent", "sourcePrecept" };
        Assert(expectedKeys.All(Scribe.Values.ContainsKey) && Scribe.Values.Count == expectedKeys.Length,
            "完整快照必须保留全部新旧存档键。");
        Assert(((Thought_MemorySocial)RestoreOne(loaded)).opinionOffset == source.opinionOffset,
            "存档后仍应恢复自定义好感。");
    }

    /// <summary>验证零好感字段即使被存档默认省略，也能依靠存在标记正确恢复。</summary>
    private static void PersistedZeroOpinion()
    {
        StoredMemoryData source = FullSnapshot();
        source.opinionOffset = 0f;
        StoredMemoryData loaded = SaveAndLoad(source);
        Assert(!Scribe.Values.ContainsKey("opinionOffset") && Scribe.Values.ContainsKey("hasOpinionOffset"),
            "模拟存档应省略默认零值但保留好感存在标记。");
        Assert(((Thought_MemorySocial)RestoreOne(loaded)).opinionOffset == 0f,
            "显式零好感不能误当旧格式缺失字段。");
    }

    /// <summary>以旧版四个键加载，检验版本缺失及新增字段的缺省兼容。</summary>
    private static void LegacySnapshot()
    {
        ThoughtDef def = Definition(true);
        var other = new Pawn();
        Scribe.Values["def"] = def;
        Scribe.Values["age"] = 123;
        Scribe.Values["moodPowerFactor"] = 1.2f;
        Scribe.Values["otherPawn"] = other;
        Scribe.mode = LoadSaveMode.LoadingVars;
        var legacy = new StoredMemoryData();
        legacy.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        Assert(legacy.memoryDataVersion == 0 && !legacy.hasOpinionOffset && legacy.durationTicksOverride == -1,
            "旧存档应缺省为旧版本与原生持续时间。");
        var restored = (Thought_MemorySocial)RestoreOne(legacy);
        Assert(restored.opinionOffset == 1f && restored.CurStageIndex == 0,
            "旧快照的未知偏移必须沿用定义默认值。");
        Assert(restored.age == 123 && restored.moodPowerFactor == 1.2f && restored.otherPawn == other,
            "旧版已保存字段仍必须保持。");
    }

    /// <summary>验证旧凝胶加工和再次保存不会伪装成拥有完整数据的新版本。</summary>
    private static void LegacyCopyAndSave()
    {
        var legacy = new StoredMemoryData { def = Definition(true), otherPawn = new Pawn(), age = 50 };
        StoredMemoryData loaded = SaveAndLoad(PersonalityMemoryUtility.Copy(legacy));
        Assert(loaded.memoryDataVersion == 0 && !Scribe.Values.ContainsKey("memoryDataVersion"),
            "复制和存档不能升级未采集的旧快照。");
        Assert(((Thought_MemorySocial)RestoreOne(loaded)).opinionOffset == 1f,
            "旧快照再次保存后仍须使用定义默认值。");
    }

    /// <summary>验证新版非社交来源后来变为社交定义时，不用未采集的零覆盖定义值。</summary>
    private static void MissingOpinionMarker()
    {
        var snapshot = new StoredMemoryData
        {
            def = Definition(true), otherPawn = new Pawn(), memoryDataVersion = 1, stageIndex = 2,
            hasOpinionOffset = false, opinionOffset = 0f
        };
        Assert(((Thought_MemorySocial)RestoreOne(snapshot)).opinionOffset == 8f,
            "没有实例好感标记时应保留指定阶段的默认值。");
    }

    /// <summary>用每组仅一条的动态好感验证恢复值在分组前生效，不把不同区间误合并。</summary>
    private static void DynamicGroups()
    {
        ThoughtDef def = Definition(true);
        def.factory = () => new DynamicTrainingMemory();
        def.stackLimitForSameOtherPawn = 1;
        var other = new Pawn();
        float[] offsets = { -4f, 3f, 8f, 14f };
        var snapshots = offsets.Select(value =>
        {
            var memory = (Thought_MemorySocial)ThoughtMaker.MakeThought(def, 0);
            memory.otherPawn = other;
            memory.opinionOffset = value;
            return PersonalityMemoryUtility.Capture(memory);
        }).ToList();
        var pawn = new Pawn();
        PersonalityMemoryUtility.Restore(pawn, snapshots);
        var actual = pawn.needs.mood.thoughts.memories.Memories.Cast<Thought_MemorySocial>().ToList();
        Assert(actual.Count == 4 && actual.Select(m => m.opinionOffset).SequenceEqual(offsets),
            "四个区间必须各自保留，不能因先使用默认好感而触发数量上限。");
    }

    /// <summary>验证合法的同阶段多条心情记忆与不同阶段记忆不会在恢复中重置年龄。</summary>
    private static void MoodStacking()
    {
        ThoughtDef def = Definition();
        def.stackLimit = 4;
        var snapshots = new List<StoredMemoryData>();
        for (int index = 0; index < 4; index++)
        {
            Thought_Memory memory = ThoughtMaker.MakeThought(def, index / 2);
            memory.age = 100 + index;
            snapshots.Add(PersonalityMemoryUtility.Capture(memory));
        }
        var pawn = new Pawn();
        PersonalityMemoryUtility.Restore(pawn, snapshots);
        var restored = pawn.needs.mood.thoughts.memories.Memories;
        Assert(restored.Count == 4 && restored.Select(m => m.age).SequenceEqual(new[] { 100, 101, 102, 103 }),
            "合法堆叠中的记忆数量和年龄必须保持。");
        Assert(restored.Select(m => m.CurStageIndex).SequenceEqual(new[] { 0, 0, 1, 1 }), "阶段分组必须保持。");
    }

    /// <summary>验证替换和空快照语义，避免接收身体残留原有记忆。</summary>
    private static void ReplaceHost()
    {
        var pawn = new Pawn();
        var old = ThoughtMaker.MakeThought(Definition());
        pawn.needs.mood.thoughts.memories.Memories.Add(old);
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData> { FullSnapshot() });
        Assert(!pawn.needs.mood.thoughts.memories.Memories.Contains(old), "宿主旧记忆必须移除。");
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData>());
        Assert(pawn.needs.mood.thoughts.memories.Memories.Count == 0, "空快照必须清空记忆。");
    }

    /// <summary>覆盖没有角色、列表或心情需求时的无操作入口。</summary>
    private static void NullInputs()
    {
        Assert(PersonalityMemoryUtility.Capture(null) == null && PersonalityMemoryUtility.Copy(null) == null,
            "空记忆应保持为空。");
        PersonalityMemoryUtility.Restore(null, new List<StoredMemoryData>());
        var pawn = new Pawn();
        pawn.needs.mood.thoughts.memories.Memories.Add(ThoughtMaker.MakeThought(Definition()));
        PersonalityMemoryUtility.Restore(pawn, null);
        Assert(pawn.needs.mood.thoughts.memories.Memories.Count == 1, "缺失列表不能清空宿主。");
        pawn.needs.mood = null;
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData>());
        pawn.needs = null;
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData>());
    }

    /// <summary>验证被销毁的人物不会进入社交记忆，普通记忆仍保留其余效果。</summary>
    private static void InvalidOtherPawn()
    {
        var destroyed = new Pawn { Destroyed = true };
        var social = new StoredMemoryData { def = Definition(true), otherPawn = destroyed };
        var mood = new StoredMemoryData { def = Definition(), otherPawn = destroyed };
        var pawn = new Pawn();
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData> { social, mood });
        var memories = pawn.needs.mood.thoughts.memories.Memories;
        Assert(memories.Count == 1 && memories[0].def == mood.def && memories[0].otherPawn == null,
            "无有效社交对象应跳过；普通记忆应清空无效引用后保留。");
    }

    /// <summary>验证缺失定义、条件过滤及各类单条异常均不会中断后续恢复。</summary>
    private static void InvalidEntries()
    {
        ThoughtDef disallowed = Definition();
        disallowed.allowed = false;
        ThoughtDef checkFailure = Definition();
        checkFailure.throwOnCheck = true;
        ThoughtDef constructionFailure = Definition();
        constructionFailure.factory = () => throw new InvalidOperationException("模拟构造异常");
        var valid = FullSnapshot();
        var pawn = new Pawn();
        PersonalityMemoryUtility.Restore(pawn, new List<StoredMemoryData>
        {
            null, new StoredMemoryData(), new StoredMemoryData { def = disallowed },
            new StoredMemoryData { def = checkFailure }, new StoredMemoryData { def = constructionFailure }, valid
        });
        Assert(pawn.needs.mood.thoughts.memories.Memories.Count == 1 && Log.Warnings.Count == 2,
            "只应恢复有效条目，并分别记录检查和构造异常。");
    }

    /// <summary>模拟定义删去阶段以及损坏的负阶段，确认可恢复字段仍然保留。</summary>
    private static void InvalidStages()
    {
        foreach (int stage in new[] { -1, 99 })
        {
            StoredMemoryData data = FullSnapshot();
            data.stageIndex = stage;
            var restored = (Thought_MemorySocial)RestoreOne(data);
            Assert(restored.CurStageIndex == 0 && restored.opinionOffset == data.opinionOffset,
                "失效阶段应回退，但已保存的实际好感仍须保持。");
        }
        Assert(Log.Warnings.Count == 2, "失效阶段应产生明确日志。");
    }
}
