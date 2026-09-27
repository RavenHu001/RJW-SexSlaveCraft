using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RunCombatantCases()
    {
        Run("战斗员普通人格抽取和植入运行真实健康对账，宿主高进度不污染", CombatantOrdinaryTransfer);
        Run("战斗员终极随当前、其他及留空方向迁移，普通终极互斥", CombatantFinalTransfer);
        Run("不带战斗员记录的人格清空宿主全部战斗员成果", CombatantEmptyTransfer);
        Run("抽取清理非当前战斗员历史但保留其他方向", CombatantInactiveExtraction);
        Run("标签保存服从组件当前方向且终极优先，重复保存无旧标签", CombatantTagCapture);
        Run("战斗员凝胶标签与完整历史参与存档字段往返且副本隔离", CombatantGelPersistence);
        Run("战斗员筛料与资格判定共用完成容差、标签和有限值检查", CombatantRecipeEligibility);
        Run("八种凝胶通过真实结算和加工映射，保留其他终极及历史", CombatantRecipeAllBases);
        Run("战斗员制作中失效在消耗和计数前终止", CombatantRecipeChangedDuringWork);
        Run("战斗员异常原料集合与未知产物映射不会吞料", CombatantRecipeInvalidSources);
        Run("战斗员半成品优先检查内装人格，失败保留容器", CombatantRecipeUnfinished);
        Run("直接调用配方工作者同样拒绝不合格战斗员凝胶", CombatantWorkerRecheck);
    }

    private static CompSexSlaveTraining CombatantTraining(Pawn pawn, float progress)
    {
        var comp = Training(pawn);
        comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow); comp.specializationProgress = 0.22f;
        comp.SetSpecialization(SexSlaveSpecializationType.Combatant); comp.specializationProgress = progress;
        CompSexSlaveTraining.ReconcileSpecialization(pawn);
        return comp;
    }

    private static bool CombatantTag(Pawn pawn, HediffDef def) => pawn.health.hediffSet.HasHediff(def);

    private static void CombatantOrdinaryTransfer()
    {
        var source = Body("CombatantSource"); var sourceComp = CombatantTraining(source, 0.35f);
        // 普通 Hediff 的陈旧严重度不能代替组件值进入快照。
        source.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant).Severity = 0.9f;
        var gel = Excrete(source);
        Assert(gel.GetTagSeverity(SSCDefOf.SSC_Hediff_Combatant) == 0.35f, "采集使用了陈旧严重度");
        CompSexSlaveTraining.ReconcileSpecialization(source);
        Assert(sourceComp.specializationType == SexSlaveSpecializationType.None &&
            !sourceComp.ExportSpecializationProgress().ContainsKey("Combatant") &&
            !CombatantTag(source, SSCDefOf.SSC_Hediff_Combatant), "抽取后重生普通状态或残留培养历史");
        Assert(gel.specializationProgressByType["Combatant"] == 0.35f && gel.specializationProgressByType["Cow"] == 0.22f,
            "先清理再保存导致人格数据丢失");
        var receiver = Body("Host"); var host = CombatantTraining(receiver, 0.95f);
        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        Assert(ExcretionUtility.InheritEverything(receiver, gel), "植入失败");
        var ordinary = receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant);
        Assert(host.specializationProgress == 0.35f && ordinary?.Severity == 0.35f &&
            !CombatantTag(receiver, SSCDefOf.SSC_Hediff_Combatant_Final), "宿主终极或高进度污染源人格");
    }

    private static void CombatantFinalTransfer()
    {
        foreach (var type in new[] { SexSlaveSpecializationType.Combatant, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.None })
        {
            var source = Body("FinalSource"); var comp = CombatantTraining(source, 1f);
            source.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
            comp.SetSpecialization(type);
            var gel = Excrete(source);
            CompSexSlaveTraining.ReconcileSpecialization(source);
            Assert(gel.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final) && !gel.HasTag(SSCDefOf.SSC_Hediff_Combatant), "终极采集不互斥");
            Assert(!CombatantTag(source, SSCDefOf.SSC_Hediff_Combatant_Final) && !CombatantTag(source, SSCDefOf.SSC_Hediff_Combatant), "旧身体保留成果");
            var receiver = Body("Host"); var host = CombatantTraining(receiver, 0.8f);
            receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
            Assert(ExcretionUtility.InheritEverything(receiver, gel), "终极植入失败");
            var final = receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant_Final);
            for (int i = 0; i < 3; i++) CompSexSlaveTraining.ReconcileSpecialization(receiver);
            Assert(host.specializationType == type && final != null &&
                receiver.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Combatant_Final) == final &&
                !CombatantTag(receiver, SSCDefOf.SSC_Hediff_Combatant), "恢复改变方向或重建、叠加状态");
            Assert(host.ExportSpecializationProgress()["Combatant"] == 1f, "终极历史未恢复");
        }
    }

    private static void CombatantEmptyTransfer()
    {
        foreach (bool nullTags in new[] { false, true })
        {
            var receiver = Body("OldHost"); var host = CombatantTraining(receiver, 1f);
            receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
            receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
            var gel = Gel(); if (nullTags) gel.hediffTags = null;
            Assert(ExcretionUtility.InheritEverything(receiver, gel), "空标签人格植入失败");
            CompSexSlaveTraining.ReconcileSpecialization(receiver);
            Assert(!receiver.health.hediffSet.hediffs.Any(h => CombatantSpecializationGelUtility.IsCombatantTag(h.def)) &&
                host.ExportSpecializationProgress().Count == 0, "宿主历史或终极残留");
        }
    }

    private static void CombatantInactiveExtraction()
    {
        var source = Body("OtherDirection"); var comp = CombatantTraining(source, 0.6f);
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        source.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        var other = source.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
        var gel = Excrete(source);
        Assert(comp.specializationType == SexSlaveSpecializationType.Cow && comp.specializationProgress == 0.22f &&
            !comp.ExportSpecializationProgress().ContainsKey("Combatant") && source.health.hediffSet.hediffs.Contains(other), "抽取修改其他方向");
        Assert(gel.specializationProgressByType["Combatant"] == 0.6f && gel.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final), "非当前战斗员记录丢失");
    }

    private static void CombatantTagCapture()
    {
        var p = Body("Capture"); var comp = CombatantTraining(p, 0f); var gel = Gel();
        gel.StorePawnData(p);
        Assert(gel.specializationProgress == 0 && gel.GetTagSeverity(SSCDefOf.SSC_Hediff_Combatant) == 0.01f, "显示下限变成经验");
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final); gel.StorePawnData(p);
        Assert(gel.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final) && !gel.HasTag(SSCDefOf.SSC_Hediff_Combatant), "终极优先失败");
        CombatantSpecializationGelUtility.RemoveAllStates(p); comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant); gel.StorePawnData(p);
        Assert(!gel.hediffTags.Keys.Any(CombatantSpecializationGelUtility.IsCombatantTag), "非当前普通状态或旧终极混入快照");
    }

    private static void CombatantGelPersistence()
    {
        var source = Body("Snapshot"); CombatantTraining(source, 0.999f);
        var gel = Excrete(source);
        Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
        var loaded = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        var copy = Gel(); copy.CopyFrom(loaded);
        Assert(copy.specializationType == SexSlaveSpecializationType.Combatant && copy.specializationProgress == 0.999f &&
            copy.HasTag(SSCDefOf.SSC_Hediff_Combatant), "存档字段往返丢失记录");
        copy.specializationProgressByType["Combatant"] = 0.2f; copy.RemoveTag(SSCDefOf.SSC_Hediff_Combatant);
        Assert(gel.specializationProgressByType["Combatant"] == 0.999f && loaded.specializationProgressByType["Combatant"] == 0.999f &&
            loaded.HasTag(SSCDefOf.SSC_Hediff_Combatant), "复制共享字典");
    }

    private static CompPersonalityStore CompletedCombatantGel()
    {
        var gel = Gel(); gel.parent.def = SSCDefOf.SSC_PersonalitySlime;
        gel.parent.Comps[typeof(CompPersonalityStore)] = gel;
        gel.specializationType = SexSlaveSpecializationType.Combatant; gel.specializationProgress = 0.999f;
        gel.specializationProgressByType = new Dictionary<string, float> { ["Cow"] = 0.22f, ["Combatant"] = 0.999f };
        gel.SetTag(SSCDefOf.SSC_Hediff_Combatant, 0.999f);
        return gel;
    }

    private static RecipeDef_PSTag CombatantRecipe() => new RecipeDef_PSTag
    {
        hediffToAdd = SSCDefOf.SSC_Hediff_Combatant_Final,
        requireSpecializationType = SexSlaveSpecializationType.Combatant, requireSpecializationComplete = true,
        exclusiveTags = new List<HediffDef> { SSCDefOf.SSC_Hediff_Combatant_Final }
    };

    private static bool CombatantIngredientAllowed(CompPersonalityStore gel)
    {
        bool allowed = true;
        Patch_IsFixedOrAllowedIngredient.Postfix(new Bill { recipe = CombatantRecipe() }, gel.parent, ref allowed);
        return allowed;
    }

    private static Action<CompPersonalityStore>[] CombatantInvalidations() => new Action<CompPersonalityStore>[]
    {
        g => g.specializationProgress = 0.9989f, g => g.specializationProgress = float.NaN,
        g => g.specializationProgress = float.PositiveInfinity, g => g.specializationProgress = float.NegativeInfinity,
        g => g.specializationProgress = 1.01f, g => g.specializationType = SexSlaveSpecializationType.Cow,
        g => g.RemoveTag(SSCDefOf.SSC_Hediff_Combatant), g => g.SetTag(SSCDefOf.SSC_Hediff_Combatant_Final, 1f)
    };

    private static void CombatantRecipeEligibility()
    {
        foreach (float progress in new[] { 0.999f, 1f })
        {
            var gel = CompletedCombatantGel(); gel.specializationProgress = progress;
            Assert(CombatantRecipeUtility.IsEligibleGel(gel) && CombatantIngredientAllowed(gel), "合法完成原料被拒绝");
        }
        foreach (var invalidate in CombatantInvalidations())
        {
            var gel = CompletedCombatantGel(); invalidate(gel);
            Assert(!CombatantRecipeUtility.IsEligibleGel(gel) && !CombatantIngredientAllowed(gel), "筛料与资格不一致");
        }
    }

    private static void CombatantRecipeAllBases()
    {
        var bases = new[] { SSCDefOf.SSC_PersonalitySlime, SSCDefOf.SSC_PS_P, SSCDefOf.SSC_PS_U, SSCDefOf.SSC_PS_P_U,
            SSCDefOf.SSC_PersonalitySlime_Edited, SSCDefOf.SSC_PS_P_Edited, SSCDefOf.SSC_PS_U_Edited, SSCDefOf.SSC_PS_P_U_Edited };
        var outputs = new[] { SSCDefOf.SSC_PersonalitySlime_Edited, SSCDefOf.SSC_PS_P_Edited, SSCDefOf.SSC_PS_U_Edited, SSCDefOf.SSC_PS_P_U_Edited };
        for (int i = 0; i < bases.Length; i++)
        {
            var source = CompletedCombatantGel(); source.parent.def = bases[i];
            source.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final, 1f);
            Assert(CombatantIngredientAllowed(source), "凝胶基底被拒绝");
            var crafter = RecipeCrafter(CombatantRecipe(), source.parent); var bill = crafter.CurJob.bill;
            GuardedRecipeCompletion(crafter).initAction();
            var output = GenPlace.LastPlaced?.TryGetComp<CompPersonalityStore>();
            Assert(source.parent.Destroyed && output?.parent.def == outputs[i % 4] && bill.completedIterations == 1 && bill.repeatCount == 0,
                "凝胶映射、消耗或计数错误");
            Assert(output.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final) && !output.HasTag(SSCDefOf.SSC_Hediff_Combatant) &&
                output.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final) && output.specializationProgressByType["Cow"] == 0.22f,
                "未互斥替换或丢失其他成果");
            output.specializationProgressByType["Cow"] = 0.8f; output.RemoveTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            Assert(source.specializationProgressByType["Cow"] == 0.22f && source.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final), "源产物共享数据");
        }
    }

    private static void CombatantRecipeChangedDuringWork()
    {
        foreach (var invalidate in CombatantInvalidations())
        {
            var source = CompletedCombatantGel(); var crafter = RecipeCrafter(CombatantRecipe(), source.parent);
            var job = crafter.CurJob; var toil = GuardedRecipeCompletion(crafter);
            Assert(CombatantIngredientAllowed(source), "初始材料不合格");
            invalidate(source); toil.initAction(); AssertRejectedRecipe(crafter, job);
            Assert(!source.parent.Destroyed && job.placedThings[0].Count == 1, "失效时吞料");
        }
    }

    private static void CombatantRecipeInvalidSources()
    {
        for (int scenario = 0; scenario < 8; scenario++)
        {
            var source = CompletedCombatantGel(); var extra = CompletedCombatantGel();
            var crafter = RecipeCrafter(CombatantRecipe(), source.parent); var job = crafter.CurJob;
            if (scenario == 0) job.placedThings = null;
            if (scenario == 1) job.placedThings.Clear();
            if (scenario == 2) job.placedThings[0].Count = 0;
            if (scenario == 3) job.placedThings[0].Count = 2;
            if (scenario == 4) job.placedThings.Add(new ThingCountClass(extra.parent, 1));
            if (scenario == 5) source.parent.Destroy(DestroyMode.Vanish);
            if (scenario == 6) source.parent.stackCount = 2;
            if (scenario == 7) source.parent.def = new ThingDef { defName = "UnknownGel" };
            GuardedRecipeCompletion(crafter).initAction(); AssertRejectedRecipe(crafter, job);
            Assert(!extra.parent.Destroyed && (scenario == 5 || !source.parent.Destroyed), "异常集合吞料");
        }
    }

    private static void CombatantRecipeUnfinished()
    {
        var source = CompletedCombatantGel(); var outside = CompletedCombatantGel();
        var unfinished = new UnfinishedThing { ingredients = new List<Thing> { source.parent } };
        var crafter = RecipeCrafter(CombatantRecipe(), outside.parent); var job = crafter.CurJob;
        job.targetC = new LocalTargetInfo(unfinished); var toil = GuardedRecipeCompletion(crafter);
        source.specializationProgress = 0.5f; toil.initAction(); AssertRejectedRecipe(crafter, job);
        Assert(!unfinished.Destroyed && !source.parent.Destroyed && !outside.parent.Destroyed, "失败吞掉半成品");
        source.specializationProgress = 1f; crafter = RecipeCrafter(CombatantRecipe(), outside.parent);
        crafter.CurJob.targetC = new LocalTargetInfo(unfinished); GuardedRecipeCompletion(crafter).initAction();
        Assert(unfinished.Destroyed && source.parent.Destroyed && !outside.parent.Destroyed && GenPlace.LastPlaced != null, "半成品来源错误");
    }

    private static void CombatantWorkerRecheck()
    {
        foreach (var invalidate in CombatantInvalidations())
        {
            var source = CompletedCombatantGel(); invalidate(source); GenPlace.LastPlaced = null;
            new Recipe_PSTagWorker { recipe = CombatantRecipe() }.Notify_IterationCompleted(Body("Caller"), new List<Thing> { source.parent });
            Assert(GenPlace.LastPlaced == null, "工作者生成不合法成果");
        }
    }
}
