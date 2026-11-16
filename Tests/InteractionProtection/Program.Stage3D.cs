using System;
using HarmonyLib;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>覆盖 Stage 3 普通 RJW 双人行为的实际结算入口、任务凭据和跨存档去重。</summary>
    private static void RunStage3DTests()
    {
        Run("3D 准备及拒绝任务不能领取行为经验", UnstartedSexHasNoOutcome);
        Run("3D 其他 SexProps 和接收方回调不能冒领", ForeignSexPropsCannotClaim);
        Run("3D 日常与仪式调教不重复领取普通行为经验", TrainingDoesNotClaimOrdinarySex);
    }

    /// <summary>每个场景独立重置通知记录，避免其他测试的成功结算影响断言。</summary>
    private static void ResetSexAwards()
    {
        TrainerSpecializationProgressUtility.InitiatedSexAwards = 0;
        TrainerSpecializationProgressUtility.LastInitiator = null;
        TrainerSpecializationProgressUtility.LastRecipient = null;
        TrainerSpecializationProgressUtility.OnAward = null;
        SSCPnaSexSupplyUtility.Calls.Clear();
        SSCPnaSexSupplyUtility.StrongCalls = 0;
        SSCPnaSexSupplyUtility.StrongDoses.Clear();
        SSCPnaSexSupplyUtility.FluidDoseReader = null;
        SexUtility.OnProcess = null;
        SSCPnaSexSupplyUtility.OnSupply = null;
    }

    /// <summary>预分配属性不等于开始；权限拒绝的任务也不能通过迟到结算伪造成功。</summary>
    private static void UnstartedSexHasNoOutcome()
    {
        ResetSexAwards();
        var p = People();
        var prepared = Driver(p.a, p.b, rape: false);
        prepared.Sexprops = new SexProps { pawn = p.a, partner = p.b };
        SexUtility.ProcessSex(prepared.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 0, "准备阶段不能领取经验");

        var denied = Driver(p.c, p.b);
        Begin(denied);
        denied.Sexprops = new SexProps { pawn = p.c, partner = p.b, isRape = true };
        SexUtility.ProcessSex(denied.Sexprops);
        Assert(denied.StartCalls == 0 && TrainerSpecializationProgressUtility.InitiatedSexAwards == 0,
            "拒绝任务不能领取经验");
    }

    /// <summary>传入陌生属性或接收端视角时，结算不能借已开始任务冒领。</summary>
    private static void ForeignSexPropsCannotClaim()
    {
        ResetSexAwards();
        var p = People();
        var driver = Driver(p.a, p.b, rape: false);
        Begin(driver);
        driver.ticks_left = 0;

        SexUtility.ProcessSex(new SexProps { pawn = p.a, partner = p.b });
        SexUtility.ProcessSex(new SexProps { pawn = p.b, partner = p.a, isReceiver = true });
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 0,
            "任务之外或接收端的 SexProps 不能领取");
        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1,
            "无效回调不能提前消耗合法场景的奖励");
    }

    /// <summary>训练任务即便内部调用 RJW 结算，也只能由训练自己的成功入口给经验。</summary>
    private static void TrainingDoesNotClaimOrdinarySex()
    {
        ResetSexAwards();
        var p = People();
        p.a.Training.pawnIdentity = PawnIdentity.Master;
        p.b.Training.selectedTrainer = p.a;

        foreach (bool ritual in new[] { false, true })
        {
            JobDriver_SexBaseInitiator driver = ritual ? new JobDriver_RitualTraining() : new JobDriver_Training();
            driver.pawn = p.a;
            driver.job = new Job { def = new JobDef { defName = ritual ? "SSC_RitualTraining" : "SSC_Training_SexSlave" },
                targetA = new LocalTargetInfo { Thing = p.b } };
            driver.job.def.driverClass = driver.GetType();
            p.a.jobs.curDriver = driver;
            driver.MakeScenarioToils();
            Begin(driver);
            Assert(driver.StartCalls == 1, "训练场景应真正开始");
            driver.ticks_left = 0;
            SexUtility.ProcessSex(driver.Sexprops);
        }
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 0,
            "日常及仪式调教不能再领取普通双人行为经验");
    }
}
