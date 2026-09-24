using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using SexSlaveCraft;

internal static partial class Program
{
    /// <summary>给角色附加训练组件，让实际生产方向切换与历史快照代码参与集成测试。</summary>
    private static CompSexSlaveTraining Training(Pawn pawn)
    {
        var comp = new CompSexSlaveTraining { parent = pawn };
        pawn.Comps[typeof(CompSexSlaveTraining)] = comp;
        return comp;
    }

    /// <summary>通过真实方向切换入口创建公交历史及带有未归档增长的当前奶牛进度。</summary>
    private static CompSexSlaveTraining TrainedSource(Pawn pawn)
    {
        CompSexSlaveTraining comp = Training(pawn);
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        comp.specializationProgress = 0.1f;
        comp.SetSpecialization(SexSlaveSpecializationType.Bus);
        comp.specializationProgress = 0.5f;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        comp.specializationProgress = 0.2f;
        return comp;
    }

    /// <summary>创建具有不同历史与当前方向的接收身体，以检测人格植入后是否混入宿主训练记录。</summary>
    private static CompSexSlaveTraining TrainedHost(Pawn pawn)
    {
        CompSexSlaveTraining comp = Training(pawn);
        comp.SetSpecialization(SexSlaveSpecializationType.PetDog);
        comp.specializationProgress = 0.9f;
        comp.SetSpecialization(SexSlaveSpecializationType.Bus);
        comp.specializationProgress = 0.8f;
        return comp;
    }

    /// <summary>验证完整提取、加工复制与跨体植入链路保留源历史及当前最新值，并清除宿主记录。</summary>
    private static void SpecializationTransfer()
    {
        Pawn source = Body("Source");
        TrainedSource(source);
        CompPersonalityStore stored = Excrete(source);
        Assert(stored.specializationProgressByType["Bus"] == 0.5f && stored.specializationProgressByType["Cow"] == 0.2f,
            "采集必须包含公交历史，以及尚未归档的最新奶牛进度。");
        CompPersonalityStore processed = Gel();
        processed.CopyFrom(stored);
        Pawn receiver = Body("Receiver");
        CompSexSlaveTraining host = TrainedHost(receiver);
        Excrete(receiver);

        Assert(ExcretionUtility.InheritEverything(receiver, processed), "加工凝胶的跨体植入必须成功。");
        Assert(host.specializationType == SexSlaveSpecializationType.Cow && host.specializationProgress == 0.2f,
            "植入必须恢复源人格的当前方向与最新进度。");
        Dictionary<string, float> restored = host.ExportSpecializationProgress();
        Assert(restored.Count == 2 && restored["Bus"] == 0.5f && !restored.ContainsKey("PetDog"),
            "恢复后的历史必须完全属于源人格，不能保留宿主公交或宠物进度。");
        host.SetSpecialization(SexSlaveSpecializationType.Bus);
        Assert(host.specializationProgress == 0.5f, "之后切回公交时必须取得源人格的历史进度。");
        Assert(processed.parent.Destroyed, "成功植入后必须消耗加工凝胶。");
    }

    /// <summary>验证源角色、原凝胶、加工副本与新身体分别拥有独立的可变进度快照。</summary>
    private static void SpecializationIsolation()
    {
        Pawn source = Body("Source");
        CompSexSlaveTraining sourceTraining = TrainedSource(source);
        CompPersonalityStore stored = Excrete(source);
        CompPersonalityStore copied = Gel();
        copied.CopyFrom(stored);
        sourceTraining.SetSpecialization(SexSlaveSpecializationType.Bus);
        sourceTraining.specializationProgress = 0.95f;
        Assert(stored.specializationProgressByType["Bus"] == 0.5f, "源身体后续训练不得改写已提取快照。");
        stored.specializationProgressByType["Bus"] = 0.7f;
        Assert(copied.specializationProgressByType["Bus"] == 0.5f, "加工副本不得共享原凝胶的进度字典。");
        Pawn receiver = Body("Receiver");
        CompSexSlaveTraining receiverTraining = Training(receiver);
        Assert(ExcretionUtility.InheritEverything(receiver, copied), "独立副本必须能够植入。");
        copied.specializationProgressByType["Bus"] = 0.99f;
        receiverTraining.SetSpecialization(SexSlaveSpecializationType.Bus);
        Assert(receiverTraining.specializationProgress == 0.5f, "新身体不能引用凝胶的可变字典。");
    }

    /// <summary>验证旧凝胶缺少历史时只恢复已知当前进度，且无当前方向的旧凝胶不会沿用宿主历史。</summary>
    private static void LegacySpecializationTransfer()
    {
        foreach (SexSlaveSpecializationType type in new[] { SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.None })
        {
            CompPersonalityStore legacy = Gel();
            legacy.specializationType = type;
            legacy.specializationProgress = 0.3f;
            Assert(legacy.specializationProgressByType == null, "默认凝胶必须表示没有保存过历史的旧格式。");
            CompPersonalityStore copy = Gel();
            copy.CopyFrom(legacy);
            Assert(copy.specializationProgressByType == null, "复制不得把旧格式伪装成完整空历史。");
            Pawn receiver = Body("Receiver");
            CompSexSlaveTraining host = TrainedHost(receiver);
            Assert(ExcretionUtility.InheritEverything(receiver, copy), "旧凝胶仍须能够植入。");
            Dictionary<string, float> restored = host.ExportSpecializationProgress();
            Assert(!restored.ContainsKey("Bus") && !restored.ContainsKey("PetDog"), "旧凝胶也不能保留接收身体的历史。");
            Assert(type == SexSlaveSpecializationType.None
                ? restored.Count == 0 && host.specializationProgress == 0f
                : restored.Count == 1 && restored["Cow"] == 0.3f,
                "旧数据只能恢复已知当前方向，不能猜测未保存的源历史。");
        }
    }

    /// <summary>经真实快照与植入流程迁移普通训导官标签，验证宿主旧严重度不会抬高源进度。</summary>
    private static void TrainerOrdinaryTagTransfer()
    {
        // 源人格同时拥有当前进度和同系普通标签；两者须按原值进入凝胶快照。
        Pawn source = Body("TrainerSource");
        CompSexSlaveTraining sourceTraining = Training(source);
        sourceTraining.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
        sourceTraining.specializationProgress = 0.35f;
        source.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer).Severity = 0.35f;
        CompPersonalityStore stored = Gel();
        stored.StorePawnData(source);
        Assert(stored.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer)
            && stored.GetTagSeverity(SSCDefOf.SSC_Hediff_TrainerOfficer) == 0.35f,
            "普通训导官标签及其严重度必须显式进入凝胶。 ");

        // 加工副本接收同一个人格；宿主较高的旧标签必须先被清掉。
        CompPersonalityStore processed = Gel();
        processed.CopyFrom(stored);
        Pawn receiver = Body("OldHost");
        CompSexSlaveTraining hostTraining = Training(receiver);
        hostTraining.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
        hostTraining.specializationProgress = 0.8f;
        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer).Severity = 0.8f;
        Assert(ExcretionUtility.InheritEverything(receiver, processed), "普通训导官人格植入必须成功。");

        // 源方向、进度及标签都应保持 0.35，不允许取宿主的 0.8 最大值。
        Hediff restored = receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_TrainerOfficer);
        Assert(hostTraining.specializationType == SexSlaveSpecializationType.TrainerOfficer
            && hostTraining.specializationProgress == 0.35f && restored?.Severity == 0.35f,
            "宿主旧训导官标签或进度不得混入源人格。");
    }

    /// <summary>分别测试有效与禁用终极记录经提取、复制、植入后的互斥恢复。</summary>
    private static void TrainerFinalTagTransfer()
    {
        foreach (HediffDef sourceTag in new[]
        {
            SSCDefOf.SSC_Hediff_TrainerOfficer_Final,
            SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled
        })
        {
            HediffDef hostTag = sourceTag == SSCDefOf.SSC_Hediff_TrainerOfficer_Final
                ? SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled
                : SSCDefOf.SSC_Hediff_TrainerOfficer_Final;

            // 终极记录独立于当前培养方向；禁用标签也必须进入源快照。
            Pawn source = Body("FinalSource");
            CompSexSlaveTraining sourceTraining = Training(source);
            sourceTraining.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            sourceTraining.specializationProgress = 1f;
            sourceTraining.SetSpecialization(SexSlaveSpecializationType.Cow);
            source.health.AddHediff(sourceTag).Severity = 1f;
            CompPersonalityStore stored = Gel();
            stored.StorePawnData(source);
            Assert(stored.HasTag(sourceTag), "两种终极标签都必须被显式保存。");

            // 新身体预先持有相反的终极记录，验证植入先清宿主、后应用源记录。
            CompPersonalityStore processed = Gel();
            processed.CopyFrom(stored);
            Pawn receiver = Body("FinalHost");
            CompSexSlaveTraining hostTraining = Training(receiver);
            hostTraining.SetSpecialization(SexSlaveSpecializationType.Bus);
            receiver.health.AddHediff(hostTag).Severity = 1f;
            Assert(ExcretionUtility.InheritEverything(receiver, processed), "终极训导官人格植入必须成功。");
            Assert(receiver.health.hediffSet.GetFirstHediffOfDef(sourceTag) != null
                && receiver.health.hediffSet.GetFirstHediffOfDef(hostTag) == null,
                "植入后只能保留源人格的终极状态，不得混入宿主完成记录。");
            Assert(hostTraining.specializationType == SexSlaveSpecializationType.Cow,
                "终极记录不得自动改写当前培养方向。");
        }
    }

    /// <summary>源人格没有训导官标签时也要清除宿主三个同系标记。</summary>
    private static void TrainerMissingTagClearsHost()
    {
        // 构造不带训导官完成记录的源凝胶；普通与终极状态属于宿主旧身体。
        CompPersonalityStore clean = Gel();
        clean.StorePawnData(Body("CleanSource"));
        Pawn receiver = Body("MarkedHost");
        Training(receiver);
        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer);
        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);

        // 即使凝胶标签字典为空，清理仍须发生；历史或孤儿标签不能复活旧资格。
        Assert(ExcretionUtility.InheritEverything(receiver, clean), "无标签人格植入必须成功。");
        Assert(receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_TrainerOfficer) == null
            && receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_TrainerOfficer_Final) == null
            && receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) == null,
            "源人格没有训导官记录时，宿主全部同系标签都必须消失。");
    }

    /// <summary>创建带有四个不同基础数值阶段的测试记忆定义，不依赖游戏定义加载。</summary>
    private static ThoughtDef MemoryDef(string name, bool social)
    {
        return new ThoughtDef
        {
            defName = name,
            thoughtClass = social ? typeof(Thought_MemorySocial) : typeof(Thought_Memory),
            stages = new List<ThoughtStage>
            {
                new ThoughtStage { baseMoodEffect = -10f, baseOpinionOffset = 1 },
                new ThoughtStage { baseMoodEffect = 2f, baseOpinionOffset = 3 },
                new ThoughtStage { baseMoodEffect = 8f, baseOpinionOffset = 7 },
                new ThoughtStage { baseMoodEffect = 15f, baseOpinionOffset = 13 }
            }
        };
    }

    /// <summary>安装最小心情与记忆追踪器，以便真实采集和植入入口访问记忆列表。</summary>
    private static MemoryHandler Memories(Pawn pawn)
    {
        var handler = new MemoryHandler();
        pawn.needs = new NeedsTracker
        {
            mood = new Mood { thoughts = new Thoughts { memories = handler, situational = new SituationalThoughts() } }
        };
        return handler;
    }

    /// <summary>构造带有不同于定义默认值的完整记忆实例，覆盖阶段、人物和原生可变字段。</summary>
    private static Thought_Memory CustomizedMemory(ThoughtDef def, Pawn otherPawn, int stage, float opinion)
    {
        Thought_Memory memory = ThoughtMaker.MakeThought(def, stage);
        memory.age = 1234;
        memory.moodPowerFactor = 0.75f;
        memory.otherPawn = otherPawn;
        memory.moodOffset = -4;
        memory.durationTicksOverride = 98765;
        memory.permanent = true;
        memory.sourcePrecept = new Precept();
        if (memory is Thought_MemorySocial social) social.opinionOffset = opinion;
        return memory;
    }

    /// <summary>比较恢复的原生记忆字段，确保条目存在之外的阶段、时间、人物和数值也相同。</summary>
    private static void AssertMemory(Thought_Memory expected, Thought_Memory actual)
    {
        Assert(actual.def == expected.def && actual.CurStageIndex == expected.CurStageIndex,
            "记忆定义与阶段必须保持一致。");
        Assert(actual.age == expected.age && actual.moodPowerFactor == expected.moodPowerFactor && actual.otherPawn == expected.otherPawn,
            "记忆年龄、心情倍率与关联人物必须保持一致。");
        Assert(actual.moodOffset == expected.moodOffset && actual.durationTicksOverride == expected.durationTicksOverride &&
            actual.permanent == expected.permanent && actual.sourcePrecept == expected.sourcePrecept,
            "记忆心情偏移、时长、永久标记与戒律引用必须保持一致。");
        if (expected is Thought_MemorySocial expectedSocial)
            Assert(actual is Thought_MemorySocial actualSocial && actualSocial.opinionOffset == expectedSocial.opinionOffset,
                "实际社交好感必须原样保留，包括合法的零值与负值。");
    }

    /// <summary>验证真实提取、复制与植入链路保留各阶段和正负零自定义好感，同时替换宿主记忆。</summary>
    private static void MemoryTransfer()
    {
        int stage = 1;
        foreach (float opinion in new[] { 31.25f, -12.5f, 0f })
        {
            Pawn source = Body("Source");
            Pawn partner = Body("Partner");
            MemoryHandler sourceMemories = Memories(source);
            Thought_Memory social = CustomizedMemory(MemoryDef("Social", true), partner, stage, opinion);
            Thought_Memory mood = CustomizedMemory(MemoryDef("Mood", false), partner, stage, 0f);
            sourceMemories.Memories.AddRange(new[] { social, mood });
            CompPersonalityStore stored = Excrete(source);
            CompPersonalityStore processed = Gel();
            processed.CopyFrom(stored);
            Assert(processed.storedMemories.All(memory => memory.memoryDataVersion == 1), "新采集并加工的记忆必须携带实例字段格式版本。");
            Pawn receiver = Body("Receiver");
            MemoryHandler restored = Memories(receiver);
            restored.Memories.Add(ThoughtMaker.MakeThought(MemoryDef("HostMemory", false)));
            Excrete(receiver);

            Assert(ExcretionUtility.InheritEverything(receiver, processed), "带有记忆的加工凝胶必须能够植入。");
            Assert(restored.Memories.Count == 2, "植入必须替换宿主记忆，不能残留宿主条目。");
            AssertMemory(social, restored.Memories.Single(memory => memory.def == social.def));
            AssertMemory(mood, restored.Memories.Single(memory => memory.def == mood.def));
            stage++;
        }
    }

    /// <summary>验证加工复制保留全部记忆字段，并隔离源记忆及各凝胶快照的可变实例。</summary>
    private static void MemoryCopyIsolation()
    {
        Pawn source = Body("Source");
        Thought_MemorySocial memory = (Thought_MemorySocial)CustomizedMemory(MemoryDef("Social", true), Body("Partner"), 2, 18.5f);
        Memories(source).Memories.Add(memory);
        CompPersonalityStore stored = Excrete(source);
        CompPersonalityStore copy = Gel();
        copy.CopyFrom(stored);
        Assert(!ReferenceEquals(copy.storedMemories, stored.storedMemories) && !ReferenceEquals(copy.storedMemories[0], stored.storedMemories[0]),
            "加工副本必须拥有独立列表和独立记忆快照。");
        memory.opinionOffset = -90f;
        stored.storedMemories[0].opinionOffset = 70f;
        stored.storedMemories[0].stageIndex = 0;
        Pawn receiver = Body("Receiver");
        MemoryHandler restored = Memories(receiver);
        Assert(ExcretionUtility.InheritEverything(receiver, copy), "独立记忆副本必须能够植入。");
        Thought_MemorySocial actual = (Thought_MemorySocial)restored.Memories.Single();
        Assert(actual.opinionOffset == 18.5f && actual.CurStageIndex == 2 && actual.age == 1234 && actual.moodOffset == -4 &&
            actual.durationTicksOverride == 98765 && actual.permanent && actual.sourcePrecept == memory.sourcePrecept,
            "加工后修改源记忆或原凝胶不得改变加工副本保留的完整实例字段。");
    }

    /// <summary>验证无实例版本的旧记忆继续使用引擎定义默认值，而不是把缺失字段解释为自定义零值。</summary>
    private static void LegacyMemoryTransfer()
    {
        Pawn partner = Body("Partner");
        ThoughtDef def = MemoryDef("LegacySocial", true);
        def.stages[0].baseOpinionOffset = 37;
        CompPersonalityStore legacy = Gel();
        legacy.storedMemories.Add(new StoredMemoryData { def = def, age = 456, moodPowerFactor = 0.6f, otherPawn = partner });
        CompPersonalityStore processed = Gel();
        processed.CopyFrom(legacy);
        Assert(processed.storedMemories[0].memoryDataVersion == 0 && !processed.storedMemories[0].hasOpinionOffset,
            "加工复制必须保留旧记忆缺少实际数值的状态。");
        Pawn receiver = Body("Receiver");
        MemoryHandler restored = Memories(receiver);
        Assert(ExcretionUtility.InheritEverything(receiver, processed), "旧记忆格式仍须能够植入。");
        Thought_MemorySocial memory = (Thought_MemorySocial)restored.Memories.Single();
        Assert(memory.opinionOffset == 37f && memory.CurStageIndex == 0 && memory.moodOffset == 0 &&
            memory.durationTicksOverride == -1 && !memory.permanent && memory.sourcePrecept == null,
            "缺少实例数据的旧凝胶必须维持定义和引擎的默认初始化行为。");
        Assert(memory.age == 456 && memory.moodPowerFactor == 0.6f && memory.otherPawn == partner,
            "旧格式已经保存过的字段仍须原样恢复。");
    }

    /// <summary>验证组件实际存档入口写入并读取独立历史字典，缺少新字段时保留旧格式标记。</summary>
    private static void SpecializationPersistenceContract()
    {
        Pawn source = Body("Source");
        TrainedSource(source);
        CompPersonalityStore stored = Excrete(source);
        Scribe.mode = LoadSaveMode.Saving;
        stored.PostExposeData();
        Assert(Scribe.Values.ContainsKey("specializationProgressByType"), "组件存档入口必须包含完整方向历史字段。");
        CompPersonalityStore loaded = Gel();
        Scribe.mode = LoadSaveMode.LoadingVars;
        loaded.PostExposeData();
        Assert(loaded.specializationProgressByType.Count == 2 && loaded.specializationProgressByType["Bus"] == 0.5f &&
            loaded.specializationProgressByType["Cow"] == 0.2f, "加载入口必须恢复历史和最新当前值。");
        Assert(!ReferenceEquals(stored.specializationProgressByType, loaded.specializationProgressByType), "内存存档契约不得依赖共享字典。");
        Scribe.Values.Remove("specializationProgressByType");
        loaded.PostExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit;
        loaded.PostExposeData();
        Assert(loaded.specializationProgressByType == null, "缺少新字段的存档在读档初始化后仍须可识别为旧格式。");
    }

    /// <summary>验证记忆实例的新增存档字段全部参与读写，并检查缺少新增字段的旧数据默认值。</summary>
    private static void MemoryPersistenceContract()
    {
        Thought_Memory source = CustomizedMemory(MemoryDef("Social", true), Body("Partner"), 3, -22.5f);
        StoredMemoryData saved = PersonalityMemoryUtility.Capture(source);
        Scribe.mode = LoadSaveMode.Saving;
        saved.ExposeData();
        string[] newKeys = { "memoryDataVersion", "stageIndex", "hasOpinionOffset", "opinionOffset", "moodOffset", "durationTicksOverride", "permanent", "sourcePrecept" };
        Assert(newKeys.All(Scribe.Values.ContainsKey), "记忆存档入口必须写入全部新增实例字段。");
        var loaded = new StoredMemoryData();
        Scribe.mode = LoadSaveMode.LoadingVars;
        loaded.ExposeData();
        Assert(loaded.memoryDataVersion == 1 && loaded.stageIndex == 3 && loaded.hasOpinionOffset && loaded.opinionOffset == -22.5f &&
            loaded.moodOffset == -4 && loaded.durationTicksOverride == 98765 && loaded.permanent && loaded.sourcePrecept == source.sourcePrecept,
            "加载入口必须读回全部新增实例字段。");
        Assert(loaded.def == source.def && loaded.age == source.age && loaded.moodPowerFactor == source.moodPowerFactor && loaded.otherPawn == source.otherPawn,
            "新增存档字段不得破坏旧字段的读写契约。");
        foreach (string key in newKeys) Scribe.Values.Remove(key);
        loaded.ExposeData();
        Assert(loaded.memoryDataVersion == 0 && loaded.stageIndex == 0 && !loaded.hasOpinionOffset && loaded.opinionOffset == 0f &&
            loaded.moodOffset == 0 && loaded.durationTicksOverride == -1 && !loaded.permanent && loaded.sourcePrecept == null,
            "没有新增字段的旧存档必须读取为旧格式和明确默认值。");
    }
}
