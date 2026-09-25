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
        Run("3D 普通双人成功结算只给实际发起者一次通知", OrdinarySexOutcomeOnce);
        Run("3D 合法强迫行为也给发起者经验通知", PermittedForcedSexOutcome);
        Run("3D 准备及拒绝任务不能领取行为经验", UnstartedSexHasNoOutcome);
        Run("3D 发起任务倒计时未结束不能提前领取", PrematureProcessSexCannotClaim);
        Run("3D 其他 SexProps 和接收方回调不能冒领", ForeignSexPropsCannotClaim);
        Run("3D 原结算被其他补丁跳过时不能领取经验", SkippedProcessSexCannotClaim);
        Run("3D 日常与仪式调教不重复领取普通行为经验", TrainingDoesNotClaimOrdinarySex);
        Run("3D 保存后的已领取标记阻止读档重复发放", OrdinarySexClaimSurvivesSave);
    }

    /// <summary>每个场景独立重置通知记录，避免其他测试的成功结算影响断言。</summary>
    private static void ResetSexAwards()
    {
        TrainerSpecializationProgressUtility.InitiatedSexAwards = 0;
        TrainerSpecializationProgressUtility.LastInitiator = null;
        TrainerSpecializationProgressUtility.LastRecipient = null;
    }

    /// <summary>正常双人任务成功 Start 后由 RJW 结算；重复处理同一 SexProps 不再发奖。</summary>
    private static void OrdinarySexOutcomeOnce()
    {
        ResetSexAwards();
        var p = People();
        var driver = Driver(p.a, p.b, rape: false);
        Begin(driver);
        Assert(driver.StartCalls == 1 && driver.Sexprops != null, "普通行为必须先完成 Start");

        driver.ticks_left = 0;
        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1 &&
            TrainerSpecializationProgressUtility.LastInitiator == p.a &&
            TrainerSpecializationProgressUtility.LastRecipient == p.b,
            "成功后应按真实任务方向通知发起者");
        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1, "同一任务不得重复领取");
    }

    /// <summary>主人对已绑定目标的合法强迫行为属于普通 RJW 双人来源，结算时应被纳入。</summary>
    private static void PermittedForcedSexOutcome()
    {
        ResetSexAwards();
        var p = People();
        var driver = Driver(p.a, p.b);
        Begin(driver);
        Assert(driver.StartCalls == 1 && driver.Sexprops.isRape, "强迫行为应被规则允许且确实开始");

        driver.ticks_left = 0;
        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1 &&
            TrainerSpecializationProgressUtility.LastInitiator == p.a,
            "允许的强迫行为应计入普通主动行为来源");
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

    /// <summary>提前调用 RJW 结算不能领取；同一个已开始任务计时结束后仍可领取。</summary>
    private static void PrematureProcessSexCannotClaim()
    {
        ResetSexAwards();
        var p = People();
        var driver = Driver(p.a, p.b, rape: false);
        Begin(driver);

        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 0,
            "任务尚在计时时不能发奖");
        driver.ticks_left = 0;
        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1,
            "过早结算不能消耗正常完成时的一次奖励");
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

    /// <summary>Harmony 后缀也可能在原方法被前缀跳过时运行，此时不能消耗奖励资格。</summary>
    private static void SkippedProcessSexCannotClaim()
    {
        ResetSexAwards();
        var p = People();
        var driver = Driver(p.a, p.b, rape: false);
        Begin(driver);
        driver.ticks_left = 0;

        SSCTrainerInitiatedSexProgressHook.Postfix(driver.Sexprops, false);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 0,
            "被跳过的 RJW 结算不能发奖");
        SexUtility.ProcessSex(driver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1,
            "跳过的回调不能消耗后续真实结算的奖励");
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

    /// <summary>已消费标记随同一 Job 的驱动保存；读档后再次进入 RJW 结算仍不能重复发奖。</summary>
    private static void OrdinarySexClaimSurvivesSave()
    {
        ResetSexAwards();
        var p = People();
        var oldDriver = Driver(p.a, p.b, rape: false);
        Begin(oldDriver);
        oldDriver.ticks_left = 0;
        SexUtility.ProcessSex(oldDriver.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1, "读档前应已领取一次");

        var restored = new JobDriver_SexBaseInitiator
        {
            pawn = p.a, job = oldDriver.job, Sexprops = oldDriver.Sexprops,
            PartnerPawn = p.b, ticks_left = 0
        };
        p.a.jobs.curDriver = restored;
        Restore(oldDriver, restored);
        SexUtility.ProcessSex(restored.Sexprops);
        Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1,
            "已领取状态读档后仍只能领取一次");
    }
}
