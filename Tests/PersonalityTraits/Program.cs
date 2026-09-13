using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using SexSlaveCraft;

internal static class Program
{
    private static int passed;
    private static int failed;

    /// <summary>执行所有人格特质回归用例，汇总通过数量，并以退出码报告是否存在失败。</summary>
    private static int Main()
    {
        Run("Cross-body insertion replaces ordinary traits and consumes gel", CrossBodyInsertion);
        Run("Same-body insertion restores an earlier trait snapshot", SameBodySnapshot);
        Run("Same TraitDef with a different degree is replaced", SameDefDifferentDegree);
        Run("Capture includes suppressed ordinary traits and excludes gene traits", CaptureFilters);
        Run("Storage and copying keep independent trait objects and snapshot version", StorageAndCopy);
        Run("Insertion preserves host genes and their granted traits", PreserveHostGenes);
        Run("Host gene wins same-definition trait conflicts without losing the saved personality trait", SameDefinitionHostGene);
        Run("Host gene wins conflicting definitions without losing the saved personality trait", ConflictingHostGene);
        Run("Suppressed old ordinary traits are removed during replacement", SuppressedOldTraits);
        Run("Shared abilities granted by retained genes and traits survive replacement", PreserveSharedAbilities);
        Run("Legacy version-zero snapshots still restore saved ordinary traits", LegacySnapshot);
        Run("An empty snapshot clears ordinary traits and retains host gene traits", EmptySnapshot);
        Run("Missing snapshot fails before mutating receiver or consuming gel", MissingSnapshot);
        Run("Null pawn and component inputs are safe", NullInputs);
        Run("Version metadata writes, reads, and defaults old data to zero", VersionPersistenceContract);
        Console.WriteLine($"RESULT: {passed}/{passed + failed} cases passed.");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>隔离单个用例的日志和存档模拟状态，捕获断言异常，并检查生产代码是否记录了意外错误。</summary>
    private static void Run(string name, Action test)
    {
        Log.Errors.Clear();
        Scribe.mode = LoadSaveMode.Inactive;
        Scribe.Values.Clear();
        try
        {
            test();
            Assert(Log.Errors.Count == 0, "The production methods must not hide an exception in their catch blocks.");
            passed++;
            Console.WriteLine("PASS: " + name);
        }
        catch (Exception e)
        {
            failed++;
            Console.WriteLine("FAIL: " + name + "\n" + e.Message);
        }
    }

    /// <summary>断言条件成立，否则抛出包含说明的异常，使当前用例失败。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>创建指定名称的最小特质定义，供用例构造独立数据。</summary>
    private static TraitDef Def(string name) => new TraitDef { defName = name };

    /// <summary>创建具名测试角色，并通过特质接口授予初始特质。</summary>
    private static Pawn Body(string name, params Trait[] traits)
    {
        var pawn = new Pawn { Name = new NameTriple(name, name, name) };
        foreach (Trait trait in traits) pawn.story.traits.GainTrait(trait);
        return pawn;
    }

    /// <summary>创建带有父物品和指定特质列表的人格组件，供用例验证植入与消耗行为。</summary>
    private static CompPersonalityStore Gel(params Trait[] traits) => new CompPersonalityStore
    {
        parent = new Thing(),
        nickName = "Source",
        storedTraits = traits.ToList()
    };

    /// <summary>执行生产代码中的人格提取流程，并返回刚生成的凝胶存储组件。</summary>
    private static CompPersonalityStore Excrete(Pawn pawn)
    {
        ExcretionUtility.DoExcretion(pawn);
        return GenSpawn.LastSpawned.TryGetComp<CompPersonalityStore>();
    }

    /// <summary>筛选角色当前的普通特质，排除基因授予条目以及性奴、空壳派生状态。</summary>
    private static List<Trait> OrdinaryTraits(Pawn pawn) => pawn.story.traits.allTraits
        .Where(t => t.sourceGene == null && t.def != SSCDefOf.SSC_Trait_PE && t.def.defName != "SexSlaveCraft_SexSlave").ToList();

    /// <summary>确认角色仅保有指定定义和等级的一个普通特质，防止旧特质或隐藏副本残留。</summary>
    private static void AssertOnlyOrdinary(Pawn pawn, TraitDef def, int degree)
    {
        List<Trait> actual = OrdinaryTraits(pawn);
        Assert(actual.Count == 1 && actual[0].def == def && actual[0].Degree == degree,
            "Receiver must have exactly the saved ordinary trait and degree.");
    }

    /// <summary>验证跨身体植入替换普通特质、恢复身份、移除空壳标记，并在成功后消耗凝胶。</summary>
    private static void CrossBodyInsertion()
    {
        TraitDef sourceDef = Def("SourceTrait");
        Pawn source = Body("Source", new Trait(sourceDef, 2, true));
        Pawn receiver = Body("Receiver", new Trait(Def("ReceiverTrait"), -1));
        CompPersonalityStore sourceGel = Excrete(source);
        Excrete(receiver);

        Assert(ExcretionUtility.InheritEverything(receiver, sourceGel), "Insertion must report success.");
        AssertOnlyOrdinary(receiver, sourceDef, 2);
        Trait restored = OrdinaryTraits(receiver).Single();
        Assert(restored.ScenForced, "Scenario-forced state must survive insertion.");
        Assert(!ReferenceEquals(restored, sourceGel.storedTraits[0]), "Receiver must not share the gel's mutable Trait instance.");
        Assert(receiver.LabelShort == "Source", "The existing name restore must continue working.");
        Assert(!receiver.story.traits.HasTrait(SSCDefOf.SSC_Trait_PE), "Successful insertion must remove the shell marker trait.");
        Assert(receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreted_Done) == null,
            "Successful insertion must remove the completed-shell hediff.");
        Assert(sourceGel.parent.Destroyed, "Successful insertion must consume the gel.");
    }

    /// <summary>验证同体回填恢复提取时的特质快照，覆盖提取后修改原身体特质的情况。</summary>
    private static void SameBodySnapshot()
    {
        TraitDef original = Def("Original");
        Pawn pawn = Body("SameBody", new Trait(original, 1));
        CompPersonalityStore gel = Excrete(pawn);
        Assert(pawn.story.traits.HasTrait(original), "Excretion intentionally leaves ordinary body traits in place.");
        pawn.story.traits.RemoveTrait(pawn.story.traits.GetTrait(original));
        pawn.story.traits.GainTrait(new Trait(Def("ChangedAfterExcretion")));

        Assert(ExcretionUtility.InheritEverything(pawn, gel), "Same-body insertion must succeed.");
        AssertOnlyOrdinary(pawn, original, 1);
        Assert(gel.parent.Destroyed, "Same-body insertion must consume the gel.");
    }

    /// <summary>验证同一定义但不同等级的普通特质能够按快照正确替换。</summary>
    private static void SameDefDifferentDegree()
    {
        TraitDef spectrum = Def("DegreeTrait");
        Pawn receiver = Body("Receiver", new Trait(spectrum, -1));
        CompPersonalityStore gel = Gel(new Trait(spectrum, 2));

        Assert(ExcretionUtility.InheritEverything(receiver, gel), "Different-degree replacement must succeed.");
        AssertOnlyOrdinary(receiver, spectrum, 2);
    }

    /// <summary>验证捕获时保留受抑制普通特质，并排除基因特质、派生状态和身体相关的临时标记。</summary>
    private static void CaptureFilters()
    {
        Trait ordinary = new Trait(Def("SuppressedOrdinary"), 1, true);
        Pawn pawn = Body("Source", ordinary);
        Gene gene = pawn.genes.AddGene(new GeneDef { defName = "SourceGene" });
        gene.def.suppressedTraits.Add(new GeneticTraitData { def = ordinary.def, degree = ordinary.Degree });
        Trait geneTrait = new Trait(Def("GeneTrait")) { sourceGene = gene };
        pawn.story.traits.GainTrait(geneTrait);
        pawn.story.traits.GainTrait(new Trait(SSCDefOf.SSC_Trait_PE));
        pawn.story.traits.GainTrait(new Trait(Def("SexSlaveCraft_SexSlave")));

        List<Trait> captured = PersonalityTraitUtility.CaptureTraits(pawn);
        Assert(captured.Count == 1 && captured[0].def == ordinary.def && captured[0].Degree == 1,
            "Capture must contain the suppressed ordinary trait, and omit gene-granted and SSC-derived traits.");
        Assert(captured[0].sourceGene == null && !captured[0].Suppressed,
            "Snapshots must not carry source-body gene ownership or transient suppression.");
        Assert(!ReferenceEquals(captured[0], ordinary) && captured[0].ScenForced,
            "Capture must clone ordinary trait values rather than share the source object.");
    }

    /// <summary>验证存储及复制保留特质值和格式版本，且源角色、原凝胶和复制品不共享可变特质实例。</summary>
    private static void StorageAndCopy()
    {
        TraitDef ordinaryDef = Def("Ordinary");
        Trait ordinary = new Trait(ordinaryDef, -1, true);
        Pawn pawn = Body("Source", ordinary);
        Gene gene = pawn.genes.AddGene(new GeneDef { defName = "SourceGene" });
        pawn.story.traits.GainTrait(new Trait(Def("GeneTrait")) { sourceGene = gene });
        CompPersonalityStore stored = Gel();
        stored.StorePawnData(pawn);
        Assert(stored.traitSnapshotVersion == 1, "Fresh captures must use snapshot version one.");
        Assert(stored.storedTraits.Count == 1 && stored.storedTraits[0].def == ordinaryDef,
            "StorePawnData must route through the ordinary-trait capture filter.");
        Assert(!ReferenceEquals(ordinary, stored.storedTraits[0]), "Stored traits must be detached from the pawn.");

        CompPersonalityStore copied = Gel();
        copied.CopyFrom(stored);
        Assert(copied.traitSnapshotVersion == 1, "CopyFrom must preserve snapshot version.");
        Assert(copied.storedTraits.Count == 1 && copied.storedTraits[0].def == ordinaryDef &&
               copied.storedTraits[0].Degree == -1 && copied.storedTraits[0].ScenForced,
            "CopyFrom must preserve ordinary trait identity, degree, and scenario-forced state.");
        Assert(!ReferenceEquals(copied.storedTraits, stored.storedTraits) && !ReferenceEquals(copied.storedTraits[0], stored.storedTraits[0]),
            "CopyFrom must own both its list and each mutable trait.");
        copied.storedTraits.Clear();
        Assert(stored.storedTraits.Count == 1, "Modifying a copy must not mutate the original snapshot.");
    }

    /// <summary>验证植入后宿主基因及其授予特质的实例和来源关系保持完整。</summary>
    private static void PreserveHostGenes()
    {
        Pawn host = Body("Host", new Trait(Def("HostOrdinary")));
        Gene hostGene = host.genes.AddGene(new GeneDef { defName = "HostGene" });
        Trait granted = new Trait(Def("HostGrantedTrait"), 1) { sourceGene = hostGene };
        host.story.traits.GainTrait(granted);
        TraitDef sourceOrdinary = Def("SourceOrdinary");

        Assert(ExcretionUtility.InheritEverything(host, Gel(new Trait(sourceOrdinary, 2))), "Insertion into a gene-bearing host must succeed.");
        AssertOnlyOrdinary(host, sourceOrdinary, 2);
        Assert(host.genes.GenesListForReading.Count == 1 && ReferenceEquals(host.genes.GenesListForReading[0], hostGene),
            "Personality insertion must preserve the receiving body's gene instance.");
        Assert(host.story.traits.allTraits.Contains(granted) && ReferenceEquals(granted.sourceGene, hostGene),
            "Personality insertion must preserve the receiving body's granted trait and gene ownership.");
    }

    /// <summary>验证普通特质与宿主基因特质同定义时仍被保存并受抑制，覆盖相同及不同等级。</summary>
    private static void SameDefinitionHostGene()
    {
        foreach (int savedDegree in new[] { 1, 2 })
        {
            TraitDef common = Def("CommonGeneAndPersonalityTrait");
            Pawn host = Body("Host");
            Gene gene = host.genes.AddGene(new GeneDef { defName = "HostGene" });
            Trait granted = new Trait(common, 1) { sourceGene = gene };
            host.story.traits.GainTrait(granted);
            CompPersonalityStore gel = Gel(new Trait(common, savedDegree, true));

            Assert(ExcretionUtility.InheritEverything(host, gel), "Insertion must support same-Def gene and personality traits.");
            AssertOnlyOrdinary(host, common, savedDegree);
            Trait restored = OrdinaryTraits(host).Single();
            Assert(restored.Suppressed, "Host gene must suppress the incoming ordinary trait while retaining its saved values.");
            Assert(!granted.Suppressed && host.story.traits.allTraits.Contains(granted), "The active host-granted trait must keep its original instance and suppression state.");
            Assert(host.genes.GenesListForReading.Contains(gene), "The host gene must not be removed to resolve same-Def conflicts.");
            List<Trait> nextSnapshot = PersonalityTraitUtility.CaptureTraits(host);
            Assert(nextSnapshot.Count == 1 && nextSnapshot[0].Degree == savedDegree,
                "A future capture must retain the suppressed personality trait, without capturing the host-granted trait.");
        }
    }

    /// <summary>验证不同定义的普通特质与宿主基因特质冲突时，保留人格条目且由宿主基因保持生效。</summary>
    private static void ConflictingHostGene()
    {
        TraitDef hostDef = Def("HostGeneTrait");
        TraitDef personalityDef = Def("ConflictingPersonalityTrait");
        hostDef.conflictingTraits.Add(personalityDef);
        personalityDef.conflictingTraits.Add(hostDef);
        Pawn host = Body("Host");
        Gene gene = host.genes.AddGene(new GeneDef { defName = "HostGene" });
        Trait granted = new Trait(hostDef) { sourceGene = gene };
        host.story.traits.GainTrait(granted);

        Assert(ExcretionUtility.InheritEverything(host, Gel(new Trait(personalityDef))), "Insertion must support conflicting host gene traits.");
        AssertOnlyOrdinary(host, personalityDef, 0);
        Assert(OrdinaryTraits(host).Single().Suppressed && !granted.Suppressed,
            "The receiving body's gene trait must remain active, with the conflicting personality retained but suppressed.");
        Assert(host.genes.GenesListForReading.Contains(gene), "Conflict resolution must not remove the host gene.");
    }

    /// <summary>验证原本受抑制的旧普通特质也能被移除，而负责抑制的宿主基因不会丢失。</summary>
    private static void SuppressedOldTraits()
    {
        TraitDef oldDef = Def("OldSuppressedOrdinary");
        Trait old = new Trait(oldDef, 1);
        Pawn host = Body("Host", old);
        Gene gene = host.genes.AddGene(new GeneDef { defName = "SuppressingBodyGene" });
        gene.def.suppressedTraits.Add(new GeneticTraitData { def = oldDef, degree = 1 });
        host.story.traits.RecalculateSuppression();
        Assert(old.Suppressed && !host.story.traits.HasTrait(oldDef), "Fixture must exercise the engine's suppressed-trait removal guard.");
        TraitDef replacement = Def("Replacement");

        Assert(ExcretionUtility.InheritEverything(host, Gel(new Trait(replacement))), "Replacing suppressed ordinary traits must succeed.");
        AssertOnlyOrdinary(host, replacement, 0);
        Assert(!host.story.traits.allTraits.Contains(old), "Suppressed old ordinary traits must not survive as hidden duplicates.");
        Assert(host.genes.GenesListForReading.Contains(gene), "Removing a suppressed ordinary trait must not remove its suppressing gene.");
    }

    /// <summary>验证移除普通特质后，宿主基因直接授予或通过保留特质授予的共享能力仍然存在。</summary>
    private static void PreserveSharedAbilities()
    {
        foreach (bool viaGrantedTrait in new[] { false, true })
        {
            AbilityDef shared = new AbilityDef { defName = "SharedAbility" };
            TraitDef oldDef = Def("OldAbilityTrait");
            oldDef.degreeData.abilities.Add(shared);
            Pawn host = Body("Host", new Trait(oldDef));
            var geneDef = new GeneDef { defName = "HostGene" };
            if (!viaGrantedTrait) geneDef.abilities.Add(shared);
            Gene gene = host.genes.AddGene(geneDef);
            if (viaGrantedTrait)
            {
                TraitDef grantedDef = Def("GeneAbilityTrait");
                grantedDef.degreeData.abilities.Add(shared);
                host.story.traits.GainTrait(new Trait(grantedDef) { sourceGene = gene });
            }
            Assert(host.abilities.GrantedAbilities.Contains(shared), "The fixture must start with a shared ability.");

            Assert(ExcretionUtility.InheritEverything(host, Gel()), "Empty-snapshot replacement must succeed for ability-bearing hosts.");
            Assert(host.abilities.GrantedAbilities.Contains(shared),
                "Removing the old ordinary trait must not discard an ability still granted by a host gene or its retained trait.");
            Assert(host.genes.GenesListForReading.Contains(gene), "Ability reconciliation must preserve the host gene.");
        }
    }

    /// <summary>验证旧格式凝胶及其复制品保持版本零，并按已保存条目恢复普通特质。</summary>
    private static void LegacySnapshot()
    {
        TraitDef saved = Def("LegacyStoredTrait");
        CompPersonalityStore legacy = Gel(new Trait(saved, 1));
        Assert(legacy.traitSnapshotVersion == 0, "Old/default payloads must be version zero.");
        CompPersonalityStore copiedLegacy = Gel();
        copiedLegacy.CopyFrom(legacy);
        Assert(copiedLegacy.traitSnapshotVersion == 0, "Copying legacy data must not invent a current-version marker.");
        Pawn receiver = Body("Receiver", new Trait(Def("ReceiverOrdinary")));

        Assert(ExcretionUtility.InheritEverything(receiver, copiedLegacy), "Legacy ordinary trait snapshots remain usable.");
        AssertOnlyOrdinary(receiver, saved, 1);
    }

    /// <summary>验证合法空快照清除宿主普通特质，但保留基因及其特质并正常消耗凝胶。</summary>
    private static void EmptySnapshot()
    {
        Pawn host = Body("Host", new Trait(Def("HostOrdinary")));
        Gene gene = host.genes.AddGene(new GeneDef { defName = "HostGene" });
        Trait granted = new Trait(Def("HostGrantedTrait")) { sourceGene = gene };
        host.story.traits.GainTrait(granted);
        CompPersonalityStore empty = Gel();

        Assert(ExcretionUtility.InheritEverything(host, empty), "A valid empty snapshot must succeed.");
        Assert(OrdinaryTraits(host).Count == 0, "A valid empty snapshot must clear host ordinary traits.");
        Assert(host.story.traits.allTraits.Contains(granted) && host.genes.GenesListForReading.Contains(gene),
            "An empty personality snapshot must leave body genes and granted traits intact.");
        Assert(empty.parent.Destroyed, "A valid empty snapshot is consumed after insertion.");
    }

    /// <summary>验证缺失快照经复制后仍为缺失，并在修改接收者或消耗凝胶前拒绝植入。</summary>
    private static void MissingSnapshot()
    {
        Trait original = new Trait(Def("HostOrdinary"), 1);
        Pawn host = Body("Host", original);
        Excrete(host);
        Hediff shell = host.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreted_Done);
        CompPersonalityStore missing = Gel();
        missing.storedTraits = null;
        CompPersonalityStore copy = Gel();
        copy.CopyFrom(missing);
        Assert(copy.storedTraits == null, "CopyFrom must preserve missing snapshots rather than converting them to valid empty snapshots.");

        Assert(!ExcretionUtility.InheritEverything(host, copy), "Missing ordinary-trait data must fail before restore begins.");
        Assert(!copy.parent.Destroyed, "Failure must preserve the personality item.");
        Assert(host.LabelShort == "Host" && host.story.traits.allTraits.Contains(original), "Failure must preserve the receiving body's name and ordinary traits.");
        Assert(host.story.traits.HasTrait(SSCDefOf.SSC_Trait_PE) && host.health.hediffSet.hediffs.Contains(shell),
            "Failure must preserve both empty-shell markers.");
        Assert(Log.Errors.Count == 1 && Log.Errors[0].Contains("valid trait snapshot"),
            "Rejected incomplete data must produce its expected validation error.");
        Log.Errors.Clear();
    }

    /// <summary>验证缺失角色、组件或背景追踪器时，捕获和植入入口能够安全返回。</summary>
    private static void NullInputs()
    {
        CompPersonalityStore gel = Gel();
        Assert(!ExcretionUtility.InheritEverything(null, gel), "Null receiver must return false.");
        Assert(!gel.parent.Destroyed, "A null receiver must not consume its gel.");
        Assert(!ExcretionUtility.InheritEverything(Body("Receiver"), null), "Null component must return false.");
        Assert(PersonalityTraitUtility.CaptureTraits(null).Count == 0, "Null capture input must be safe.");
        PersonalityTraitUtility.RestoreTraits(null, new List<Trait>());
        var noStory = new Pawn { story = null };
        Assert(PersonalityTraitUtility.CaptureTraits(noStory).Count == 0, "Missing story capture must be safe.");
        PersonalityTraitUtility.RestoreTraits(noStory, new List<Trait>());
    }

    /// <summary>借助标量存档测试替身验证版本字段参与读写，并在旧数据缺少字段时使用版本零。</summary>
    private static void VersionPersistenceContract()
    {
        CompPersonalityStore stored = Gel();
        stored.StorePawnData(Body("Source"));
        Scribe.mode = LoadSaveMode.Saving;
        stored.PostExposeData();
        Assert(Scribe.Values.TryGetValue("traitSnapshotVersion", out object saved) && (int)saved == 1,
            "The production save method must write the trait snapshot version field.");
        var loaded = Gel();
        Scribe.mode = LoadSaveMode.LoadingVars;
        loaded.PostExposeData();
        Assert(loaded.traitSnapshotVersion == 1, "The production load method must read snapshot version one.");
        Scribe.Values.Remove("traitSnapshotVersion");
        loaded.PostExposeData();
        Assert(loaded.traitSnapshotVersion == 0, "A save with no version key must default to legacy zero.");
    }
}
