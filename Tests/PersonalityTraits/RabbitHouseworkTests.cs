#if SSC_RW17
using System;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private const SexSlaveSpecializationType Rabbit = SexSlaveSpecializationType.PetRabbit;

    private static void RunRabbitHouseworkCases()
    {
        var research = new ResearchProjectDef { defName = PetSpecializationUtility.GetResearchDefName(Rabbit) };
        DefDatabase<ResearchProjectDef>.AllDefs.Add(research);
        try
        {
            Run("兔选择遵循研究并只从对应当前家务结果获得进度", () => RabbitHouseworkProgress(research));
            Run("兔从工作完成培养到提取、凝胶终极化及植入", RabbitFinalizationPipeline);
            Run("兔提取放置失败保留来源进度与普通状态", RabbitExtractionPlacementFailure);
        }
        finally { DefDatabase<ResearchProjectDef>.AllDefs.Remove(research); }
    }

    private static void RabbitHouseworkProgress(ResearchProjectDef research)
    {
        var pawn = Body("Housework");
        var comp = Training(pawn);
        comp.pawnIdentity = PawnIdentity.Slave;
        research.IsFinished = false;
        try
        {
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, comp, Rabbit, out var failure)
                && failure == PetSpecializationFailure.ResearchRequired, "兔跳过研究要求");
        }
        finally { research.IsFinished = true; }
        Assert(PetSpecializationUtility.TrySelectPetSpecialization(pawn, comp, Rabbit, out _), "1.7 兔入口未开放");

        var records = new Pawn_RecordsTracker { pawn = pawn };
        pawn.jobs.curDriver = new JobDriver_CleanFilth();
        Patch_RabbitHouseworkProgress.Postfix(records, RecordDefOf.MealsCooked);
        Assert(comp.specializationProgress == 0f, "清洁任务误计餐食经验");
        Patch_RabbitHouseworkProgress.Postfix(records, RecordDefOf.MessesCleaned);
        Assert(comp.specializationProgress == .002f, "清完一堆污物的经验不符");

        pawn.jobs.curDriver = new JobDriver_DoBill();
        var recipe = new RecipeDef { workSkill = SkillDefOf.Social };
        pawn.jobs.curJob = new Job { bill = new Bill_Production { recipe = recipe } };
        Patch_RabbitHouseworkProgress.Postfix(records, RecordDefOf.MealsCooked);
        Assert(comp.specializationProgress == .002f, "非烹饪配方获得家务经验");
        recipe.workSkill = SkillDefOf.Cooking;
        for (int i = 0; i < 4; i++) Patch_RabbitHouseworkProgress.Postfix(records, RecordDefOf.MealsCooked);
        Assert(Math.Abs(comp.specializationProgress - .006f) < .000001f, "四份实际餐食的经验不符");

        comp.pawnIdentity = PawnIdentity.Unset;
        Patch_RabbitHouseworkProgress.Postfix(records, RecordDefOf.MealsCooked);
        Assert(Math.Abs(comp.specializationProgress - .006f) < .000001f, "失去身份后仍获兔经验");
        comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        Patch_RabbitHouseworkProgress.Postfix(records, RecordDefOf.MealsCooked);
        Assert(comp.specializationProgress == 0f && Math.Abs(comp.ExportSpecializationProgress()["PetRabbit"] - .006f) < .000001f,
            "切换后家务增加了其他方向或兔历史经验");
    }

    private static void RabbitFinalizationPipeline()
    {
        var source = Body("CompletedRabbit");
        var comp = Training(source);
        comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        comp.specializationProgress = .22f;
        Assert(PetSpecializationUtility.TrySelectPetSpecialization(source, comp, Rabbit, out _), "无法选择兔方向");
        comp.specializationProgress = .9985f;
        source.jobs.curDriver = new JobDriver_CleanFilth();
        Patch_RabbitHouseworkProgress.Postfix(new Pawn_RecordsTracker { pawn = source }, RecordDefOf.MessesCleaned);
        Assert(comp.specializationProgress == 1f && source.health.hediffSet.GetFirstHediffOfDef(PetBase(Rabbit)).Severity == 1f,
            "家务未推到普通完成并同步健康状态");

        var gel = Excrete(source);
        CompSexSlaveTraining.ReconcileSpecialization(source);
        Assert(comp.specializationType == SexSlaveSpecializationType.None && !source.health.hediffSet.HasHediff(PetBase(Rabbit))
            && !comp.ExportSpecializationProgress().ContainsKey("PetRabbit") && comp.ExportSpecializationProgress()["Cow"] == .22f,
            "提取后原身体保留兔培养或丢失组外历史");
        var recipe = PetRecipe(Rabbit);
        Assert(PetIngredientAllowed(recipe, gel), "新取得的普通完成兔凝胶未通过筛料");
        var output = CompletePetRecipe(recipe, gel);
        var receiver = Body("FinalRabbit");
        var host = Training(receiver);
        Assert(ExcretionUtility.InheritEverything(receiver, output), "终极兔凝胶植入失败");
        host.SetSpecialization(SexSlaveSpecializationType.Cow);
        Assert(receiver.health.hediffSet.HasHediff(PetFinal(Rabbit)) && !receiver.health.hediffSet.HasHediff(PetBase(Rabbit))
            && PetSpecializationUtility.GetEffectivePetProgress(receiver, Rabbit) == 1f,
            "终极兔标签未替换普通标签，或转练后终极失效");
        var finalGel = Excrete(receiver);
        Assert(finalGel.HasTag(PetFinal(Rabbit)) && !receiver.health.hediffSet.HasHediff(PetFinal(Rabbit))
            && host.specializationType == SexSlaveSpecializationType.Cow && !host.ExportSpecializationProgress().ContainsKey("PetRabbit"),
            "跨方向的终极兔提取未正确清源");
    }

    private static void RabbitExtractionPlacementFailure()
    {
        var source = Body("RabbitPlacementFailure");
        var comp = Training(source);
        comp.pawnIdentity = PawnIdentity.Slave;
        Assert(PetSpecializationUtility.TrySelectPetSpecialization(source, comp, Rabbit, out _), "无法选择兔方向");
        comp.specializationProgress = .5f;
        CompSexSlaveTraining.ReconcileSpecialization(source);
        GenSpawn.FailSpawn = true;
        try
        {
            ExcretionUtility.DoExcretion(source);
            Assert(false, "测试放置失败未触发");
        }
        catch (InvalidOperationException exception) when (exception.Message == "test spawn failure") { }
        finally { GenSpawn.FailSpawn = false; }
        Assert(comp.specializationType == Rabbit && comp.specializationProgress == .5f
            && source.health.hediffSet.GetFirstHediffOfDef(PetBase(Rabbit)).Severity == .5f,
            "放置失败提前清除兔成果");
    }
}

namespace Verse.AI
{
    // 宿主只向生产 Postfix 提供当前驱动类型及配方，原版事件位置由目标 DLL 核验。
    public class JobDriver { }
    public class JobDriver_DoBill : JobDriver { }
}

namespace RimWorld
{
    public class JobDriver_CleanFilth : JobDriver { }
    public class RecordDef : Verse.Def { }
    public static class RecordDefOf
    {
        public static readonly RecordDef MessesCleaned = new();
        public static readonly RecordDef MealsCooked = new();
        public static readonly RecordDef ResearchPointsResearched = new();
        public static readonly RecordDef TimesTendedOther = new();
    }
    public class Pawn_RecordsTracker
    {
        public Pawn pawn;
        public void Increment(RecordDef def) { }
        public void AddTo(RecordDef def, float value) { }
    }
}
#endif
