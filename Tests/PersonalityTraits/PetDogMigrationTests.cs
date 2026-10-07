using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 所有冷却计算、状态删除和历史替换均链接生产代码；原版驯服及授予调度不在此执行。
    private static void RunPetDogMigrationCases()
    {
        Run("狗普通抽取先保存快照再清当前方向、狗历史和孤儿技能", DogOrdinaryExtraction);
        Run("狗终极抽取保存绝对冷却并清全部重复狗状态", DogFinalExtraction);
        Run("狗工具独立清理仅移除狗成果而保留猫兔及组外历史", DogDetachKeepsOtherProgress);
        Run("狗终极跨方向抽取保留当前非宠物方向及兔历史", DogFinalExtractionKeepsOtherProgress);
        Run("复用人格组件不残留上一次狗冷却", DogStoreReuse);
        Run("狗冷却 CopyFrom 独立保存截止时间且存放期间继续流逝", DogCooldownCopy);
        Run("狗终极人格经真实加工保存截止时间且仅扣一次账单", DogCooldownRecipe);
        Run("狗提取加工植入恢复剩余冷却并清宿主旧狗猫技能", DogCooldownInsertion);
        Run("已到期狗冷却植入后立即可用", DogExpiredCooldownInsertion);
        Run("旧凝胶缺狗冷却字段仍立即授予可用终极技能", DogLegacyCooldownInsertion);
        Run("非狗终极人格不借用宿主技能或伪造冷却字段", DogInsertionRemovesOrphanAbility);
        Run("猫狗冷却字段分别读写且缺失旧字段默认零", DogCooldownPersistenceContract);
    }

    private static CompSexSlaveTraining DogTraining(Pawn pawn, float progress)
    {
        var comp = Training(pawn); comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow); comp.specializationProgress = .22f;
        comp.SetSpecialization(SexSlaveSpecializationType.PetCat); comp.specializationProgress = .31f;
        comp.SetSpecialization(SexSlaveSpecializationType.PetRabbit); comp.specializationProgress = .2f;
        comp.SetSpecialization(SexSlaveSpecializationType.PetDog); comp.specializationProgress = progress;
        CompSexSlaveTraining.ReconcileSpecialization(pawn);
        return comp;
    }

    // 用普通 Hediff 实例表示旧版本可实际存在的终极狗；不主动转换实例类型。
    private static Pawn FinalDog(string name, int remaining, out CompSexSlaveTraining comp)
    {
        var pawn = Body(name); comp = DogTraining(pawn, 1f);
        pawn.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = 1f;
        pawn.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
        if (remaining > 0) pawn.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).StartCooldown(remaining);
        return pawn;
    }

    private static void AssertDogDetached(Pawn pawn, CompSexSlaveTraining comp)
    {
        for (int i = 0; i < 3; i++) CompSexSlaveTraining.ReconcileSpecialization(pawn);
        Assert(!pawn.health.hediffSet.HasHediff(PetBase(SexSlaveSpecializationType.PetDog))
            && !pawn.health.hediffSet.HasHediff(PetFinal(SexSlaveSpecializationType.PetDog)), "旧身体保留或再生狗状态");
        Assert(!comp.ExportSpecializationProgress().ContainsKey("PetDog"), "旧身体保留狗历史");
        Assert(pawn.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) == null, "旧身体保留狗技能");
    }

    private static void DogOrdinaryExtraction()
    {
        Find.TickManager.TicksGame = 1000;
        var source = Body("OrdinaryDog"); var comp = DogTraining(source, .4f);
        source.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
        source.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).StartCooldown(5000);
        var expected = comp.ExportSpecializationProgress(); var gel = Excrete(source);
        Assert(gel.specializationType == SexSlaveSpecializationType.PetDog && gel.specializationProgress == .4f
            && gel.hediffTags[PetBase(SexSlaveSpecializationType.PetDog)] == .4f
            && expected.OrderBy(e => e.Key).SequenceEqual(gel.specializationProgressByType.OrderBy(e => e.Key)),
            "普通狗快照在清理源身体前未完整保存");
        Assert(gel.petDogTameCooldownEndTick == 0, "普通狗的孤儿技能成为人格冷却");
        AssertDogDetached(source, comp);
        Assert(comp.specializationType == SexSlaveSpecializationType.None && comp.specializationProgress == 0f,
            "普通狗当前方向未清空");
        Assert(comp.ExportSpecializationProgress().Count == 2 && comp.ExportSpecializationProgress()["Cow"] == .22f
            && comp.ExportSpecializationProgress()["PetRabbit"] == .2f, "猫狗完整抽取清理损坏组外或兔历史");
    }

    private static void DogFinalExtraction()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalDog("FinalDog", 500, out var comp);
        source.health.AddHediff(PetBase(SexSlaveSpecializationType.PetDog)).Severity = .8f;
        source.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = 1f;
        var gel = Excrete(source);
        Assert(gel.petDogTameCooldownEndTick == 1500 && gel.HasTag(PetFinal(SexSlaveSpecializationType.PetDog))
            && !gel.HasTag(PetBase(SexSlaveSpecializationType.PetDog))
            && gel.specializationType == SexSlaveSpecializationType.PetDog && gel.specializationProgressByType["PetDog"] == 1f,
            "终极狗提取未先保存标签、方向、历史及绝对冷却");
        AssertDogDetached(source, comp);
    }

    private static void DogDetachKeepsOtherProgress()
    {
        foreach (var current in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow,
            SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.PetCat, SexSlaveSpecializationType.PetRabbit })
        {
            var source = Body("DogOnlyDetach"); var comp = DogTraining(source, 1f);
            comp.SetSpecialization(current); comp.specializationProgress = current == SexSlaveSpecializationType.None ? 0f : .37f;
            source.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = 1f;
            source.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
            var kept = new HediffDef { defName = "UnaffectedDogMigrationState" };
            source.health.AddHediff(kept).Severity = .5f;
            var expectedStates = source.health.hediffSet.hediffs
                .Where(h => h.def != PetBase(SexSlaveSpecializationType.PetDog)
                    && h.def != PetFinal(SexSlaveSpecializationType.PetDog)).ToArray();
            var expected = comp.ExportSpecializationProgress(); expected.Remove("PetDog");
            // 独立调用狗工具核验其删除范围，不能用同时清猫的完整抽取替代此断言。
            PetDogAbilityUtility.DetachAfterExtraction(source);
            AssertDogDetached(source, comp);
            Assert(comp.specializationType == current && comp.specializationProgress == (current == SexSlaveSpecializationType.None ? 0f : .37f)
                && expected.OrderBy(e => e.Key).SequenceEqual(comp.ExportSpecializationProgress().OrderBy(e => e.Key))
                && expectedStates.All(source.health.hediffSet.hediffs.Contains),
                "狗工具删除其他当前方向、健康状态或历史");
        }
    }

    private static void DogFinalExtractionKeepsOtherProgress()
    {
        foreach (var current in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow,
            SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.PetRabbit })
        {
            Find.TickManager.TicksGame = 2000;
            var source = Body("OtherCurrentDog"); var comp = DogTraining(source, 1f);
            comp.SetSpecialization(current); comp.specializationProgress = current == SexSlaveSpecializationType.None ? 0f : .37f;
            source.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = 1f;
            source.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
            source.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).StartCooldown(600);
            var expected = comp.ExportSpecializationProgress(); var gel = Excrete(source);
            AssertDogDetached(source, comp); expected.Remove("PetDog"); expected.Remove("PetCat");
            Assert(comp.specializationType == current && comp.specializationProgress == (current == SexSlaveSpecializationType.None ? 0f : .37f)
                && expected.OrderBy(e => e.Key).SequenceEqual(comp.ExportSpecializationProgress().OrderBy(e => e.Key)),
                "狗抽取修改非猫狗方向或兔历史");
            Assert(gel.specializationType == current && gel.specializationProgressByType["PetDog"] == 1f
                && gel.petDogTameCooldownEndTick == 2600, "跨方向成果快照丢失狗历史或冷却");
        }
    }

    private static void DogStoreReuse()
    {
        Find.TickManager.TicksGame = 100;
        var final = FinalDog("ReusableDog", 900, out _); var store = Gel(); store.StorePawnData(final);
        Assert(store.petDogTameCooldownEndTick == 1000, "首次采集未保存狗冷却");
        var nondog = Body("NoDogFinal"); nondog.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
        nondog.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).StartCooldown(9000); store.StorePawnData(nondog);
        Assert(store.petDogTameCooldownEndTick == 0, "无狗终极资格快照残留旧冷却");
        var noAbility = FinalDog("MissingDogAbility", 900, out _); noAbility.abilities.RemoveAbility(SSCDefOf.SSC_PetDogTame);
        store.petDogTameCooldownEndTick = 777; store.StorePawnData(noAbility);
        Assert(store.petDogTameCooldownEndTick == 0, "无实际狗技能时残留旧冷却");
        final.health.hediffSet.GetFirstHediffOfDef(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = .009f;
        store.petDogTameCooldownEndTick = 777; store.StorePawnData(final);
        Assert(store.petDogTameCooldownEndTick == 0, "低严重度狗终极错误保存冷却");
        final.health.hediffSet.GetFirstHediffOfDef(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = 1f;
        final.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).ResetCooldown(); store.petDogTameCooldownEndTick = 777;
        store.StorePawnData(final);
        Assert(store.petDogTameCooldownEndTick == 0, "可用狗技能快照仍残留旧冷却");
    }

    private static void DogCooldownCopy()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalDog("CopiedDog", 1000, out _); var gel = Excrete(source); var copy = Gel();
        Find.TickManager.TicksGame = 1300; copy.CopyFrom(gel);
        Assert(copy.petDogTameCooldownEndTick == 2000, "CopyFrom 重新启动狗冷却");
        gel.petDogTameCooldownEndTick = 9999;
        Assert(copy.petDogTameCooldownEndTick == 2000, "狗冷却标量未独立复制");
        var host = Body("CopiedDogHost"); Training(host); Find.TickManager.TicksGame = 1600;
        Assert(ExcretionUtility.InheritEverything(host, copy)
            && host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame)?.CooldownTicksRemaining == 400,
            "加工复制或存放冻结狗冷却");
    }

    private static void DogCooldownRecipe()
    {
        Find.TickManager.TicksGame = 1000;
        var gel = Excrete(FinalDog("RecipeDog", 1000, out _));
        Find.TickManager.TicksGame = 1400; var processed = ProcessPetGel(gel);
        Assert(processed.petDogTameCooldownEndTick == 2000 && processed.HasTag(PetFinal(SexSlaveSpecializationType.PetDog))
            && processed.specializationProgressByType["PetDog"] == 1f, "真实人格加工遗漏狗成果或重启冷却");
    }

    private static void DogCooldownInsertion()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalDog("FullChainDog", 1000, out _); var sourceAbility = source.abilities.GetAbility(SSCDefOf.SSC_PetDogTame);
        Find.TickManager.TicksGame = 1200; var gel = Excrete(source);
        Find.TickManager.TicksGame = 1400; var processed = ProcessPetGel(gel);
        var host = Body("OldDogHost"); var hostTraining = Training(host); hostTraining.pawnIdentity = PawnIdentity.Slave;
        hostTraining.SetSpecialization(SexSlaveSpecializationType.PetCat); hostTraining.specializationProgress = .9f;
        host.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetCat)).Severity = 1f;
        host.abilities.GainAbility(SSCDefOf.SSC_PetDogTame); host.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
        var oldHostAbility = host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame); oldHostAbility.StartCooldown(9000);
        Find.TickManager.TicksGame = 1600;
        Assert(ExcretionUtility.InheritEverything(host, processed), "狗完整链植入失败");
        var actual = host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame);
        Assert(actual != null && actual.CooldownTicksRemaining == 400
            && !ReferenceEquals(actual, sourceAbility) && !ReferenceEquals(actual, oldHostAbility)
            && host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null,
            "狗植入未恢复独立技能/剩余时间，或宿主猫狗能力残留");
        Assert(hostTraining.specializationType == SexSlaveSpecializationType.PetDog
            && !host.health.hediffSet.HasHediff(PetFinal(SexSlaveSpecializationType.PetCat)) && processed.parent.Destroyed,
            "狗植入未替换宿主成果或消耗凝胶");
    }

    private static void DogExpiredCooldownInsertion()
    {
        Find.TickManager.TicksGame = 100;
        var gel = Excrete(FinalDog("ExpiredDog", 200, out _));
        Find.TickManager.TicksGame = 301; var host = Body("ExpiredDogHost"); Training(host);
        host.abilities.GainAbility(SSCDefOf.SSC_PetDogTame); host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).StartCooldown(9000);
        Assert(ExcretionUtility.InheritEverything(host, gel)
            && host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame)?.CooldownTicksRemaining == 0,
            "到期狗冷却植入后重新启动");
    }

    private static void DogLegacyCooldownInsertion()
    {
        foreach (bool legacy in new[] { false, true })
        {
            Find.TickManager.TicksGame = 1000;
            var gel = Excrete(FinalDog("ReadyDog", 0, out _));
            if (legacy)
            {
                Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData(); Scribe.Values.Remove("petDogTameCooldownEndTick");
                gel.petDogTameCooldownEndTick = 9999; Scribe.mode = LoadSaveMode.LoadingVars; gel.PostExposeData();
                Scribe.mode = LoadSaveMode.Inactive;
            }
            var host = Body("ReadyDogHost"); Training(host);
            Assert(gel.petDogTameCooldownEndTick == 0 && ExcretionUtility.InheritEverything(host, gel)
                && host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame)?.CooldownTicksRemaining == 0,
                "旧凝胶或零冷却终极狗未立即得到可用技能");
        }
    }

    private static void DogInsertionRemovesOrphanAbility()
    {
        foreach (var type in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit })
        {
            var source = Body("NoDogFinal"); var comp = Training(source); comp.pawnIdentity = PawnIdentity.Slave;
            comp.SetSpecialization(type); comp.specializationProgress = type == SexSlaveSpecializationType.None ? 0f : .4f;
            CompSexSlaveTraining.ReconcileSpecialization(source);
            if (type == SexSlaveSpecializationType.PetCat || type == SexSlaveSpecializationType.PetRabbit)
                source.health.AddHediff(PetFinal(type)).Severity = 1f;
            var gel = Excrete(source); gel.petDogTameCooldownEndTick = 99999;
            var host = Body("OrphanDogHost"); Training(host); host.abilities.GainAbility(SSCDefOf.SSC_PetDogTame);
            host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame).StartCooldown(9000);
            Assert(ExcretionUtility.InheritEverything(host, gel)
                && host.abilities.GetAbility(SSCDefOf.SSC_PetDogTame) == null
                && !host.health.hediffSet.HasHediff(PetFinal(SexSlaveSpecializationType.PetDog)),
                "无终极狗资格人格继承宿主孤儿技能或伪造冷却字段授予技能");
        }
    }

    private static void DogCooldownPersistenceContract()
    {
        var gel = Gel(); gel.petDogTameCooldownEndTick = 12345; gel.petCatComfortCooldownEndTick = 54321;
        Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
        Assert((int)Scribe.Values["petDogTameCooldownEndTick"] == 12345
            && (int)Scribe.Values["petCatComfortCooldownEndTick"] == 54321, "猫狗冷却键混用或漏保存");
        var loaded = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
        Assert(loaded.petDogTameCooldownEndTick == 12345 && loaded.petCatComfortCooldownEndTick == 54321,
            "猫狗冷却字段未独立读回");
        Scribe.Values.Remove("petDogTameCooldownEndTick"); loaded.petDogTameCooldownEndTick = 777;
        loaded.PostExposeData();
        Assert(loaded.petDogTameCooldownEndTick == 0 && loaded.petCatComfortCooldownEndTick == 54321,
            "缺失狗旧字段未归零或改变猫冷却");
    }
}
