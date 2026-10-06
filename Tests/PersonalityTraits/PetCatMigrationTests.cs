using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 链路运行真实提取、人格存储、CopyFrom、配方结算、植入和猫工具。
    // 游戏 Ability 的实例与时钟为已核验 API 的宿主模型；不验证技能施放、
    // Hediff 授予组件的 60 tick 调度、真实存档解析或其他模组增加的配方标签。
    private static void RunPetCatMigrationCases()
    {
        Run("猫普通抽取先存快照再清源当前方向、历史及孤儿技能", CatOrdinaryExtraction);
        Run("猫终极抽取先保存冷却并移除源全部重复猫状态", CatFinalExtraction);
        Run("猫狗成果抽取保留兔及组外当前方向和历史", CatExtractionKeepsOtherProgress);
        Run("兔抽取保留原有普通或终极状态及历史", CatExtractionLeavesOtherPetsAlone);
        Run("复用人格存储时猫冷却不从上次快照残留", CatStoreReuse);
        Run("人格 CopyFrom 独立复制猫绝对冷却并允许时间流逝", CatCooldownCopy);
        Run("猫终极人格经真实加工保留绝对冷却且仅扣一次账单", CatCooldownRecipe);
        Run("猫冷却经提取加工植入按剩余时间恢复且清宿主旧技能", CatCooldownInsertion);
        Run("已到期的猫冷却植入后立即可用", CatExpiredCooldownInsertion);
        Run("旧凝胶或零冷却猫终极植入后立即授予可用技能", CatZeroCooldownInsertion);
        Run("普通猫及无猫终极的人格植入不继承宿主孤儿猫技能", CatInsertionRemovesOrphanAbility);
        Run("猫临时激励不作为人格标签随提取加工植入迁移", CatEncouragementIsNotPersonality);
        Run("猫绝对冷却字段参与存档接线且缺失旧字段默认零", CatCooldownPersistenceContract);
    }

    /// <summary>通过真实方向切换积累非猫历史，再选择猫当前方向。</summary>
    private static CompSexSlaveTraining CatTraining(Pawn pawn, float progress)
    {
        var comp = Training(pawn); comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow); comp.specializationProgress = .22f;
        comp.SetSpecialization(SexSlaveSpecializationType.PetDog); comp.specializationProgress = .31f;
        comp.SetSpecialization(SexSlaveSpecializationType.PetRabbit); comp.specializationProgress = .2f;
        comp.SetSpecialization(SexSlaveSpecializationType.PetCat); comp.specializationProgress = progress;
        CompSexSlaveTraining.ReconcileSpecialization(pawn);
        return comp;
    }

    /// <summary>显式构造已完成猫成果；Hediff 授予调度不属于本迁移套件。</summary>
    private static Pawn FinalCat(string name, int remaining, out CompSexSlaveTraining comp)
    {
        var pawn = Body(name); comp = CatTraining(pawn, 1f);
        pawn.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetCat)).Severity = 1f;
        pawn.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
        if (remaining > 0) pawn.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).StartCooldown(remaining);
        return pawn;
    }

    /// <summary>核对猫身体已彻底脱离成果，重复普通对账不能重建旧状态。</summary>
    private static void AssertCatDetached(Pawn pawn, CompSexSlaveTraining comp)
    {
        for (int i = 0; i < 3; i++) CompSexSlaveTraining.ReconcileSpecialization(pawn);
        Assert(!pawn.health.hediffSet.HasHediff(PetBase(SexSlaveSpecializationType.PetCat))
            && !pawn.health.hediffSet.HasHediff(PetFinal(SexSlaveSpecializationType.PetCat)), "源身体仍保留或再生猫状态");
        Assert(!comp.ExportSpecializationProgress().ContainsKey("PetCat"), "源身体仍保留猫历史");
        Assert(pawn.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null, "源身体仍保留猫技能");
    }

    private static void CatOrdinaryExtraction()
    {
        Find.TickManager.TicksGame = 1000;
        var source = Body("OrdinaryCat"); var comp = CatTraining(source, .4f);
        source.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
        source.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).StartCooldown(5000);
        var expected = comp.ExportSpecializationProgress(); var gel = Excrete(source);
        Assert(gel.specializationType == SexSlaveSpecializationType.PetCat && gel.specializationProgress == .4f
            && gel.hediffTags[PetBase(SexSlaveSpecializationType.PetCat)] == .4f
            && expected.OrderBy(e => e.Key).SequenceEqual(gel.specializationProgressByType.OrderBy(e => e.Key)),
            "猫普通快照必须在清源之前完整采集");
        Assert(gel.petCatComfortCooldownEndTick == 0, "普通猫的孤儿技能不能成为人格冷却");
        AssertCatDetached(source, comp);
        Assert(comp.specializationType == SexSlaveSpecializationType.None && comp.specializationProgress == 0f,
            "当前猫方向未清空");
        Assert(comp.ExportSpecializationProgress().Count == 2 && comp.ExportSpecializationProgress()["Cow"] == .22f
            && comp.ExportSpecializationProgress()["PetRabbit"] == .2f,
            "猫狗抽取清理修改了其他方向历史");
        gel.specializationProgressByType["Cow"] = .9f;
        Assert(comp.ExportSpecializationProgress()["Cow"] == .22f, "源历史与凝胶共享字典");
    }

    private static void CatFinalExtraction()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalCat("FinalCat", 500, out var comp);
        source.health.AddHediff(PetBase(SexSlaveSpecializationType.PetCat)).Severity = .8f;
        source.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetCat)).Severity = 1f;
        var gel = Excrete(source);
        Assert(gel.petCatComfortCooldownEndTick == 1500 && gel.HasTag(PetFinal(SexSlaveSpecializationType.PetCat))
            && !gel.HasTag(PetBase(SexSlaveSpecializationType.PetCat))
            && gel.specializationType == SexSlaveSpecializationType.PetCat && gel.specializationProgressByType["PetCat"] == 1f,
            "必须在删除终极状态及能力前保存终极标签、当前方向与绝对冷却");
        AssertCatDetached(source, comp);
        Assert(comp.specializationType == SexSlaveSpecializationType.None && comp.specializationProgress == 0f,
            "终极抽取未清当前猫方向");
    }

    private static void CatExtractionKeepsOtherProgress()
    {
        foreach (var current in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow,
            SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.PetRabbit })
        {
            Find.TickManager.TicksGame = 2000;
            // 先设置当前方向再放入独立猫终极事实，避免用真实切换入口
            // 构造一个会被宠物终极互斥规则拒绝的测试前提。
            var source = Body("OtherCurrent"); var comp = CatTraining(source, 1f);
            comp.SetSpecialization(current); comp.specializationProgress = current == SexSlaveSpecializationType.None ? 0f : .37f;
            source.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetCat)).Severity = 1f;
            source.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
            source.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).StartCooldown(600);
            var expected = comp.ExportSpecializationProgress(); var gel = Excrete(source);
            AssertCatDetached(source, comp); expected.Remove("PetCat"); expected.Remove("PetDog");
            Assert(comp.specializationType == current && comp.specializationProgress == (current == SexSlaveSpecializationType.None ? 0f : .37f)
                && expected.OrderBy(e => e.Key).SequenceEqual(comp.ExportSpecializationProgress().OrderBy(e => e.Key)),
                "猫清理改变了其他当前方向、当前值或历史");
            Assert(gel.specializationType == current && gel.specializationProgressByType["PetCat"] == 1f
                && gel.petCatComfortCooldownEndTick == 2600, "跨方向成果快照丢失猫历史或冷却");
        }
    }

    private static void CatExtractionLeavesOtherPetsAlone()
    {
        foreach (var type in new[] { SexSlaveSpecializationType.PetRabbit })
        foreach (bool final in new[] { false, true })
        {
            var source = Body("OtherPet"); var comp = Training(source); comp.pawnIdentity = PawnIdentity.Slave;
            comp.SetSpecialization(SexSlaveSpecializationType.Cow); comp.specializationProgress = .22f;
            comp.SetSpecialization(type); comp.specializationProgress = final ? 1f : .4f;
            CompSexSlaveTraining.ReconcileSpecialization(source);
            if (final) source.health.AddHediff(PetFinal(type)).Severity = 1f;
            var expected = comp.ExportSpecializationProgress(); var gel = Excrete(source);
            Assert(source.health.hediffSet.HasHediff(final ? PetFinal(type) : PetBase(type))
                && comp.specializationType == type && comp.specializationProgress == (final ? 1f : .4f)
                && expected.OrderBy(e => e.Key).SequenceEqual(comp.ExportSpecializationProgress().OrderBy(e => e.Key)),
                "猫狗抽取接入改变了兔既有源身体行为");
            Assert(gel.petCatComfortCooldownEndTick == 0, "非猫人格携带了猫冷却");
        }
    }

    private static void CatStoreReuse()
    {
        Find.TickManager.TicksGame = 100;
        var final = FinalCat("ReusableFinal", 900, out _); var store = Gel();
        store.StorePawnData(final);
        Assert(store.petCatComfortCooldownEndTick == 1000, "首次采集未保存有效冷却");
        var noncat = Body("NonCat"); noncat.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
        noncat.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).StartCooldown(9000);
        store.StorePawnData(noncat);
        Assert(store.petCatComfortCooldownEndTick == 0, "复用组件时非猫快照残留旧冷却");
        var noAbility = FinalCat("MissingAbility", 900, out _); noAbility.abilities.RemoveAbility(SSCDefOf.SSC_PetCatComfort);
        store.petCatComfortCooldownEndTick = 777; store.StorePawnData(noAbility);
        Assert(store.petCatComfortCooldownEndTick == 0, "有终极但无实际能力时残留旧冷却");
        var tooSmall = FinalCat("InvalidFinal", 900, out _);
        tooSmall.health.hediffSet.GetFirstHediffOfDef(PetFinal(SexSlaveSpecializationType.PetCat)).Severity = .009f;
        store.petCatComfortCooldownEndTick = 777; store.StorePawnData(tooSmall);
        Assert(store.petCatComfortCooldownEndTick == 0, "低于资格严重度的标记保存了猫冷却");
        store.petCatComfortCooldownEndTick = 777;
        final.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).ResetCooldown(); store.StorePawnData(final);
        Assert(store.petCatComfortCooldownEndTick == 0, "可用技能快照残留旧冷却");
    }

    private static void CatCooldownCopy()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalCat("CopiedCat", 1000, out _); var gel = Excrete(source); var copy = Gel();
        Find.TickManager.TicksGame = 1300; copy.CopyFrom(gel);
        Assert(copy.petCatComfortCooldownEndTick == 2000, "CopyFrom 把绝对结束时间改成重新计时");
        gel.petCatComfortCooldownEndTick = 9999;
        Assert(copy.petCatComfortCooldownEndTick == 2000, "冷却标量未独立复制");
        var host = Body("CopiedHost"); Training(host); Find.TickManager.TicksGame = 1600;
        Assert(ExcretionUtility.InheritEverything(host, copy)
            && host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort)?.CooldownTicksRemaining == 400,
            "CopyFrom 副本存放期间的时间没有扣除");
    }

    /// <summary>运行真实守卫、原版消耗顺序模型与真实配方工作者，不以手动 CopyFrom 代替加工。</summary>
    private static CompPersonalityStore ProcessPetGel(CompPersonalityStore gel)
    {
        var recipe = new RecipeDef_PSTag { defName = "PetMigrationNeutralEdit", hediffToAdd = new HediffDef { defName = "NeutralEdit" } };
        var crafter = RecipeCrafter(recipe, gel.parent); var bill = crafter.CurJob.bill;
        GuardedRecipeCompletion(crafter).initAction();
        var output = GenPlace.LastPlaced?.TryGetComp<CompPersonalityStore>();
        Assert(gel.parent.Destroyed && bill.completedIterations == 1 && output != null, "真实加工未生成产物或账单扣次异常");
        return output;
    }

    private static void CatCooldownRecipe()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalCat("RecipeCat", 1000, out _); var gel = Excrete(source);
        Find.TickManager.TicksGame = 1400; var processed = ProcessPetGel(gel);
        Assert(processed.petCatComfortCooldownEndTick == 2000 && processed.HasTag(PetFinal(SexSlaveSpecializationType.PetCat))
            && processed.specializationProgressByType["PetCat"] == 1f, "加工遗漏猫成果或重启冷却");
    }

    private static void CatCooldownInsertion()
    {
        Find.TickManager.TicksGame = 1000;
        var source = FinalCat("FullChainCat", 1000, out _);
        var sourceAbility = source.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort);
        Find.TickManager.TicksGame = 1200; var gel = Excrete(source);
        Find.TickManager.TicksGame = 1400; var processed = ProcessPetGel(gel);
        var host = Body("OldHost"); var hostTraining = Training(host); hostTraining.pawnIdentity = PawnIdentity.Slave;
        hostTraining.SetSpecialization(SexSlaveSpecializationType.PetDog); hostTraining.specializationProgress = .9f;
        host.health.AddHediff(PetFinal(SexSlaveSpecializationType.PetDog)).Severity = 1f;
        host.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
        var oldHostAbility = host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort); oldHostAbility.StartCooldown(9000);
        Find.TickManager.TicksGame = 1600;
        Assert(ExcretionUtility.InheritEverything(host, processed), "猫完整链路植入失败");
        var actual = host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort);
        Assert(actual != null && actual.CooldownTicksRemaining == 400
            && !ReferenceEquals(actual, sourceAbility) && !ReferenceEquals(actual, oldHostAbility),
            "植入未立即授予独立技能、未清宿主旧能力或未恢复剩余冷却");
        Assert(hostTraining.specializationType == SexSlaveSpecializationType.PetCat
            && !host.health.hediffSet.HasHediff(PetFinal(SexSlaveSpecializationType.PetDog))
            && processed.parent.Destroyed, "宿主旧成果残留或植入未消耗凝胶");
    }

    private static void CatExpiredCooldownInsertion()
    {
        Find.TickManager.TicksGame = 100;
        var source = FinalCat("ExpiredCat", 200, out _); var gel = Excrete(source);
        Find.TickManager.TicksGame = 301; var host = Body("ExpiredHost"); Training(host);
        host.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort); host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).StartCooldown(9000);
        Assert(ExcretionUtility.InheritEverything(host, gel)
            && host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort)?.CooldownTicksRemaining == 0,
            "过期绝对时间植入时重新启动了冷却");
    }

    private static void CatZeroCooldownInsertion()
    {
        foreach (bool legacy in new[] { false, true })
        {
            Find.TickManager.TicksGame = 1000;
            var source = FinalCat("ReadyCat", 0, out _); var gel = Excrete(source);
            if (legacy)
            {
                Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData(); Scribe.Values.Remove("petCatComfortCooldownEndTick");
                gel.petCatComfortCooldownEndTick = 9999; Scribe.mode = LoadSaveMode.LoadingVars; gel.PostExposeData();
                Scribe.mode = LoadSaveMode.Inactive;
            }
            var host = Body("ReadyHost"); Training(host);
            Assert(gel.petCatComfortCooldownEndTick == 0 && ExcretionUtility.InheritEverything(host, gel)
                && host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort)?.CooldownTicksRemaining == 0,
                "零值或缺失字段没有在终极植入后立即获得可用技能");
        }
    }

    private static void CatInsertionRemovesOrphanAbility()
    {
        foreach (var type in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit })
        {
            var source = Body("NoCatFinal"); var comp = Training(source); comp.pawnIdentity = PawnIdentity.Slave;
            comp.SetSpecialization(type); comp.specializationProgress = type == SexSlaveSpecializationType.None ? 0f : .4f;
            CompSexSlaveTraining.ReconcileSpecialization(source);
            if (type == SexSlaveSpecializationType.PetDog || type == SexSlaveSpecializationType.PetRabbit)
                source.health.AddHediff(PetFinal(type)).Severity = 1f;
            var gel = Excrete(source); gel.petCatComfortCooldownEndTick = 99999;
            var host = Body("OrphanHost"); Training(host); host.abilities.GainAbility(SSCDefOf.SSC_PetCatComfort);
            host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort).StartCooldown(9000);
            Assert(ExcretionUtility.InheritEverything(host, gel)
                && host.abilities.GetAbility(SSCDefOf.SSC_PetCatComfort) == null
                && !host.health.hediffSet.HasHediff(PetFinal(SexSlaveSpecializationType.PetCat)),
                "无猫终极资格的人格沿用宿主孤儿技能或仅凭冷却字段授予技能");
        }
    }

    private static void CatEncouragementIsNotPersonality()
    {
        Find.TickManager.TicksGame = 100;
        var source = FinalCat("EncouragedCat", 500, out _);
        var encouragement = new HediffDef { defName = "SSC_Hediff_PetCatEncouragement", hediffClass = typeof(HediffWithComps) };
        source.health.AddHediff(encouragement).Severity = 1f;
        var gel = Excrete(source); var processed = ProcessPetGel(gel);
        Assert(!gel.HasTag(encouragement) && !processed.HasTag(encouragement), "临时激励被采集或加工为人格标签");
        var host = Body("EncouragementHost"); Training(host);
        Assert(ExcretionUtility.InheritEverything(host, processed) && !host.health.hediffSet.HasHediff(encouragement),
            "新身体继承了源身体临时激励");
        Assert(source.health.hediffSet.HasHediff(encouragement), "猫成果清理额外删除了原身体临时激励");
    }

    private static void CatCooldownPersistenceContract()
    {
        var gel = Gel(); gel.petCatComfortCooldownEndTick = 12345;
        Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
        Assert((int)Scribe.Values["petCatComfortCooldownEndTick"] == 12345, "冷却字段未按稳定键保存");
        var loaded = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
        Assert(loaded.petCatComfortCooldownEndTick == 12345, "冷却字段未读回");
        Scribe.Values.Remove("petCatComfortCooldownEndTick"); loaded.petCatComfortCooldownEndTick = 777;
        loaded.PostExposeData();
        Assert(loaded.petCatComfortCooldownEndTick == 0, "缺失旧字段未采用零默认值");
    }
}
