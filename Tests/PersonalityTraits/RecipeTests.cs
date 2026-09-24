using System;
using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

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

    /// <summary>执行生产配方工作者，确认加工前复查及凝胶输出转换。</summary>
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
        var worker = new Recipe_PSTagWorker { recipe = recipe };

        // 实际生产工作者须销毁被消费的原料，并放置独立的新凝胶。
        GenPlace.LastPlaced = null;
        worker.Notify_IterationCompleted(Body("Crafter"), new List<Thing> { source.parent });
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

        // 账单筛料后仍须在真正结算时重查，拒绝变化后的坏原料且不消耗它。
        CompPersonalityStore invalid = Gel();
        invalid.parent.def = new ThingDef { defName = "ChangedGel" };
        invalid.parent.Comps[typeof(CompPersonalityStore)] = invalid;
        invalid.specializationType = SexSlaveSpecializationType.TrainerOfficer;
        invalid.specializationProgress = float.NaN;
        invalid.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer, 1f);
        GenPlace.LastPlaced = null;
        worker.Notify_IterationCompleted(Body("Crafter"), new List<Thing> { invalid.parent });
        Assert(!invalid.parent.Destroyed && GenPlace.LastPlaced == null,
            "结算时失效的原料不能被消耗或生成终极凝胶。");
    }
}
