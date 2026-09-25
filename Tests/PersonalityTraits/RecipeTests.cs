using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>以真实配方资格函数覆盖当前方向、完成阈值与两种终极记录。</summary>
    private static void TrainerRecipeEligibility()
    {
        // 唯一合法原料是当前训导官方向、有限完成进度和普通状态同时存在。
        CompPersonalityStore gel = Gel();
        gel.specializationType = SexSlaveSpecializationType.TrainerOfficer;
        gel.specializationProgress = 0.999f;
        gel.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer, 0.999f);
        Assert(TrainerOfficerRecipeUtility.IsEligibleGel(gel), "完成门槛上的普通凝胶应可终极化。");
        gel.specializationProgress = 1f;
        Assert(TrainerOfficerRecipeUtility.IsEligibleGel(gel), "100% 的普通凝胶应可终极化。");

        // 门槛以下及异常数值不能借比较漏洞进入生产加工步骤。
        foreach (float progress in new[] { 0.998f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1.01f })
        {
            gel.specializationProgress = progress;
            Assert(!TrainerOfficerRecipeUtility.IsEligibleGel(gel), "未完成或异常进度必须被拒绝。");
        }
        gel.specializationProgress = 1f;

        // 历史方向、缺普通标签、已有有效或禁用终极记录均不得再次加工。
        gel.specializationType = SexSlaveSpecializationType.Cow;
        Assert(!TrainerOfficerRecipeUtility.IsEligibleGel(gel), "其他当前方向不能使用训导官历史进度。");
        gel.specializationType = SexSlaveSpecializationType.TrainerOfficer;
        gel.RemoveTag(SSCDefOf.SSC_Hediff_TrainerOfficer);
        Assert(!TrainerOfficerRecipeUtility.IsEligibleGel(gel), "缺失普通标签时进度本身不足以加工。");
        gel.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer, 1f);
        foreach (HediffDef final in new[]
        {
            SSCDefOf.SSC_Hediff_TrainerOfficer_Final,
            SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled
        })
        {
            gel.SetTag(final, 1f);
            Assert(!TrainerOfficerRecipeUtility.IsEligibleGel(gel), "任一种完成记录都应禁止重复终极化。");
            gel.RemoveTag(final);
        }
    }

    /// <summary>按原版先消耗、后通知账单和工作者的顺序，确认正常产物不受新保护影响。</summary>
    private static void TrainerRecipeOutput()
    {
        // 构造带有其他方向历史的已完成普通凝胶，使加工后的进度继承可被观察。
        CompPersonalityStore source = Gel();
        source.parent.def = new ThingDef { defName = "SourceGel" };
        source.parent.Comps[typeof(CompPersonalityStore)] = source;
        source.specializationType = SexSlaveSpecializationType.TrainerOfficer;
        source.specializationProgress = 1f;
        source.specializationProgressByType = new Dictionary<string, float> { ["Cow"] = 0.4f, ["TrainerOfficer"] = 1f };
        source.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer, 1f);
        var recipe = new RecipeDef_PSTag
        {
            hediffToAdd = SSCDefOf.SSC_Hediff_TrainerOfficer_Final,
            severity = 1f,
            exclusiveTags = new List<HediffDef>
            {
                SSCDefOf.SSC_Hediff_TrainerOfficer_Final,
                SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled
            }
        };
        // 通过真实结算补丁和工作者，而非直接调用已晚于消耗的工作者回调。
        // 原版阶段替身严格保持核对过的调用顺序，使账单计数也参与断言。
        Pawn crafter = RecipeCrafter(recipe, source.parent);
        Bill_Production bill = crafter.CurJob.bill;
        Toil completion = GuardedRecipeCompletion(crafter);
        completion.initAction();
        CompPersonalityStore output = GenPlace.LastPlaced?.TryGetComp<CompPersonalityStore>();
        Assert(source.parent.Destroyed && output != null, "有效原料应被加工成新的凝胶。");
        Assert(output.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final)
            && !output.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer)
            && !output.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled),
            "产物只能保留有效终极标签。");
        Assert(output.specializationType == SexSlaveSpecializationType.TrainerOfficer
            && output.specializationProgress == 1f
            && output.specializationProgressByType["Cow"] == 0.4f,
            "加工不得清除当前方向和其他方向历史。");
        output.specializationProgressByType["Cow"] = 0.9f;
        Assert(source.specializationProgressByType["Cow"] == 0.4f,
            "产物不得与原料共享可变历史字典。");
        Assert(bill.repeatCount == 0 && bill.completedIterations == 1 && Toils_Recipe.OriginalCompletionCalls == 1,
            "正常配方应完整执行一次原版结算并减少一次重复次数。");
        Assert(Toils_Recipe.CompletionOrder.SequenceEqual(new[]
            { "CalculateIngredients", "MakeRecipeProducts", "ConsumeIngredients", "NotifyBillCompleted" }),
            "配方回归必须保留本机 RimWorld 1.6 的消耗和完成通知顺序。");
    }

    /// <summary>加工开始时有效、完成前失效的材料必须在任何原版消耗和账单计数之前被挡住。</summary>
    private static void TrainerRecipeChangedDuringWork()
    {
        // 每个变更发生在 Toil 创建之后，避免只在生成任务时检查一次的实现通过测试。
        Action<CompPersonalityStore>[] invalidate =
        {
            gel => gel.specializationProgress = float.NaN,
            gel => gel.specializationProgress = 0.5f,
            gel => gel.specializationType = SexSlaveSpecializationType.Cow,
            gel => gel.RemoveTag(SSCDefOf.SSC_Hediff_TrainerOfficer),
            gel => gel.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled, 1f)
        };
        foreach (Action<CompPersonalityStore> change in invalidate)
        {
            CompPersonalityStore source = CompletedTrainerGel();
            Assert(TrainerOfficerRecipeUtility.IsEligibleGel(source), "筛料时原料应合法。");
            Pawn crafter = RecipeCrafter(TrainerFinalRecipe(), source.parent);
            Job job = crafter.CurJob;
            Toil completion = GuardedRecipeCompletion(crafter);
            change(source);
            completion.initAction();

            AssertRejectedRecipe(crafter, job);
            Assert(!source.parent.Destroyed && job.placedThings[0].Count == 1,
                "失效原料必须保留，且原版分堆/清空原料步骤不能被执行。");
        }
    }

    /// <summary>半成品容器必须在 CalculateIngredients 销毁它之前检查，并优先于地上原料列表。</summary>
    private static void TrainerRecipeUnfinishedProtection()
    {
        CompPersonalityStore source = CompletedTrainerGel();
        CompPersonalityStore unrelated = CompletedTrainerGel();
        var unfinished = new UnfinishedThing { ingredients = new List<Thing> { source.parent } };
        Pawn crafter = RecipeCrafter(TrainerFinalRecipe(), unrelated.parent);
        Job job = crafter.CurJob;
        job.targetC = new LocalTargetInfo(unfinished);
        Toil completion = GuardedRecipeCompletion(crafter);
        source.specializationProgress = 0.25f;
        completion.initAction();

        // 地上有合法凝胶也不能掩盖实际半成品原料失效；容器和两份人格都应保留。
        AssertRejectedRecipe(crafter, job);
        Assert(!unfinished.Destroyed && !source.parent.Destroyed && !unrelated.parent.Destroyed,
            "拒绝时必须保留半成品容器及其内外原料。");

        // 修复数据后重新创建任务，合法半成品仍可按原版顺序完成。
        source.specializationProgress = 1f;
        Pawn retry = RecipeCrafter(TrainerFinalRecipe(), unrelated.parent);
        Bill_Production bill = retry.CurJob.bill;
        retry.CurJob.targetC = new LocalTargetInfo(unfinished);
        GuardedRecipeCompletion(retry).initAction();
        Assert(unfinished.Destroyed && source.parent.Destroyed && !unrelated.parent.Destroyed
            && GenPlace.LastPlaced != null && bill.completedIterations == 1,
            "合法半成品应转换其真实内装人格，不消耗被忽略的地上凝胶。");
    }

    /// <summary>异常来源集合不能被误判成一份可加工的人格，避免吞掉多余或不存在的凝胶。</summary>
    private static void TrainerRecipeInvalidIngredientCollections()
    {
        foreach (int scenario in Enumerable.Range(0, 7))
        {
            CompPersonalityStore source = CompletedTrainerGel();
            CompPersonalityStore extra = CompletedTrainerGel();
            Pawn crafter = RecipeCrafter(TrainerFinalRecipe(), source.parent);
            Job job = crafter.CurJob;
            // 分别覆盖无容器、无人格来源、零数量、超量、额外人格、已销毁来源及异常堆叠。
            if (scenario == 0) job.placedThings = null;
            if (scenario == 1) job.placedThings = new List<ThingCountClass> { new ThingCountClass(new Thing(), 1) };
            if (scenario == 2) job.placedThings[0].Count = 0;
            if (scenario == 3) job.placedThings[0].Count = 2;
            if (scenario == 4) job.placedThings.Add(new ThingCountClass(extra.parent, 1));
            if (scenario == 5) source.parent.Destroy(DestroyMode.Vanish);
            if (scenario == 6) source.parent.stackCount = 2;
            GuardedRecipeCompletion(crafter).initAction();
            AssertRejectedRecipe(crafter, job);
            Assert(!extra.parent.Destroyed && (scenario == 5 || !source.parent.Destroyed),
                "异常原料集合中的尚存人格不能被消耗。");
            if (scenario == 6)
                Assert(source.parent.stackCount == 2 && job.placedThings[0].Count == 1,
                    "请求一份但实体堆叠两份时，必须在原版分堆前拒绝并保留整堆原料。");
        }

        // 半成品也不能藏入第二个人格；正常脚本只生成一个产物，不能丢失多余来源。
        CompPersonalityStore first = CompletedTrainerGel();
        CompPersonalityStore second = CompletedTrainerGel();
        var unfinished = new UnfinishedThing { ingredients = new List<Thing> { first.parent, second.parent } };
        Pawn mixedCrafter = RecipeCrafter(TrainerFinalRecipe(), first.parent);
        Job mixedJob = mixedCrafter.CurJob;
        mixedJob.targetC = new LocalTargetInfo(unfinished);
        GuardedRecipeCompletion(mixedCrafter).initAction();
        AssertRejectedRecipe(mixedCrafter, mixedJob);
        Assert(!unfinished.Destroyed && !first.parent.Destroyed && !second.parent.Destroyed,
            "多份人格半成品必须完整保留。");
    }

    /// <summary>新增守护不应用于其他人格加工配方，更不能改变普通生产的完成次数。</summary>
    private static void TrainerRecipeLeavesOtherRecipesAlone()
    {
        // 不满足训导官前提的凝胶依然能执行其他合法标签加工。
        CompPersonalityStore source = CompletedTrainerGel();
        source.specializationProgress = 0.2f;
        var otherTag = new HediffDef { defName = "OtherSpecializationFinal" };
        Pawn crafter = RecipeCrafter(new RecipeDef_PSTag { hediffToAdd = otherTag }, source.parent);
        Bill_Production bill = crafter.CurJob.bill;
        GuardedRecipeCompletion(crafter).initAction();
        Assert(source.parent.Destroyed && bill.completedIterations == 1
            && GenPlace.LastPlaced?.TryGetComp<CompPersonalityStore>()?.HasTag(otherTag) == true,
            "其他标签加工应照常消费、生成产物并计数。");

        // 普通 RecipeDef 使用完全无人格组件的原料，也应原样经过结算。
        var ordinaryIngredient = new Thing();
        Pawn ordinaryCrafter = RecipeCrafter(new RecipeDef(), ordinaryIngredient);
        Bill_Production ordinaryBill = ordinaryCrafter.CurJob.bill;
        GuardedRecipeCompletion(ordinaryCrafter).initAction();
        Assert(ordinaryIngredient.Destroyed && ordinaryBill.completedIterations == 1
            && Toils_Recipe.OriginalCompletionCalls == 1,
            "普通配方不可因缺少人格原料而被拦截。");
    }

    /// <summary>为结算时序用例创建一个合法、可独立观察的普通训导官凝胶。</summary>
    private static CompPersonalityStore CompletedTrainerGel()
    {
        CompPersonalityStore source = Gel();
        source.parent.def = new ThingDef { defName = "SourceGel" };
        source.parent.Comps[typeof(CompPersonalityStore)] = source;
        source.specializationType = SexSlaveSpecializationType.TrainerOfficer;
        source.specializationProgress = 1f;
        source.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer, 1f);
        return source;
    }

    private static RecipeDef_PSTag TrainerFinalRecipe() => new RecipeDef_PSTag
    {
        hediffToAdd = SSCDefOf.SSC_Hediff_TrainerOfficer_Final,
        severity = 1f
    };

    /// <summary>绑定生产配方工作者与仅做一次的账单，使消耗和成功计数均可观察。</summary>
    private static Pawn RecipeCrafter(RecipeDef recipe, Thing ingredient)
    {
        recipe.Worker = recipe is RecipeDef_PSTag
            ? new Recipe_PSTagWorker { recipe = recipe }
            : new RecipeWorker { recipe = recipe };
        Pawn crafter = Body("Crafter");
        crafter.jobs.curJob = new Job
        {
            bill = new Bill_Production { recipe = recipe },
            placedThings = new List<ThingCountClass> { new ThingCountClass(ingredient, 1) }
        };
        return crafter;
    }

    /// <summary>将真实 SSC 补丁应用于遵守原版副作用顺序的结算动作，随后才绑定执行者。</summary>
    private static Toil GuardedRecipeCompletion(Pawn crafter)
    {
        GenPlace.LastPlaced = null;
        Toils_Recipe.OriginalCompletionCalls = 0;
        Toils_Recipe.CompletionOrder.Clear();
        Toil completion = Toils_Recipe.FinishRecipeAndStartStoringProduct(TargetIndex.B);
        Harmony_TrainerRecipeCompletion.Postfix(completion);
        completion.actor = crafter;
        return completion;
    }

    private static void AssertRejectedRecipe(Pawn crafter, Job rejectedJob)
    {
        Assert(crafter.jobs.endedWith == JobCondition.Incompletable,
            "失效加工必须结束，不能留在结算步骤或标记成功。");
        Assert(GenPlace.LastPlaced == null && Toils_Recipe.OriginalCompletionCalls == 0
            && rejectedJob.bill.repeatCount == 1 && rejectedJob.bill.completedIterations == 0,
            "拒绝必须发生于整个原版结算前，不得产生终极产物或扣账单次数。");
    }
}
