using System;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>比较浮点进度与设计增量，忽略逐次相加产生的表示误差。</summary>
    private static bool NearProgress(float actual, float expected)
        => Math.Abs(actual - expected) < 0.00001f;

    /// <summary>以生产经验工具检验三种收益、前置资格、异常值和终极化边界。</summary>
    private static void RunTrainerProgressTests()
    {
        Run("真实浮点累计达到普通完成容差时显示完成", () =>
        {
            // 两条正常成长路径会在单精度中略低于 1。用生产奖励入口累加，
            // 确认显示与配方共用的完成容差一致，而非仅测试直接赋值 1f。
            foreach (var scenario in new[]
            {
                (start: 0f, count: 100, amount: TrainerSpecializationProgressUtility.InitiatedSexProgress),
                (start: 0.2f, count: 32, amount: TrainerSpecializationProgressUtility.ProvidedTrainingProgress)
            })
            {
                Pawn pawn = EligibleTrainerPawn();
                pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
                pawn.Training.specializationProgress = scenario.start;
                for (int i = 0; i < scenario.count; i++)
                    TrainerSpecializationProgressUtility.TryGainProgress(pawn, scenario.amount);
                Assert(pawn.Training.specializationProgress >= CompSexSlaveTraining.SpecializationCompletionProgress);
                Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.OrdinaryComplete);
            }

            // 完成容差以下仍显示基础阶段；恰好到达公共边界时才转为普通完成。
            Pawn boundary = EligibleTrainerPawn();
            boundary.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            boundary.Training.specializationProgress = 0.998f;
            Assert(TrainerSpecializationUtility.GetDisplayState(boundary) == TrainerSpecializationDisplayState.BasicUnlocked);
            boundary.Training.specializationProgress = CompSexSlaveTraining.SpecializationCompletionProgress;
            Assert(TrainerSpecializationUtility.GetDisplayState(boundary) == TrainerSpecializationDisplayState.OrdinaryComplete);
        });

        Run("训导官三类经验单次收益按被调教、施教、主动行为递减", () =>
        {
            // 数值来自统一工具，避免不同事件自行写常量导致策划顺序失效。
            Assert(TrainerSpecializationProgressUtility.ReceivedTrainingProgress == 0.05f);
            Assert(TrainerSpecializationProgressUtility.ProvidedTrainingProgress == 0.025f);
            Assert(TrainerSpecializationProgressUtility.InitiatedSexProgress == 0.01f);
            Assert(TrainerSpecializationProgressUtility.ReceivedTrainingProgress
                > TrainerSpecializationProgressUtility.ProvidedTrainingProgress);
            Assert(TrainerSpecializationProgressUtility.ProvidedTrainingProgress
                > TrainerSpecializationProgressUtility.InitiatedSexProgress);
        });

        Run("主动双人行为从零进度起步，只有实际发起者获得单次收益", () =>
        {
            // 本事件入口只接收已完成任务的实际发起者；接收方与本人重合均不能获益。
            Pawn actor = EligibleTrainerPawn();
            actor.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            Pawn recipient = Pawn(PawnIdentity.Unset);
            Assert(NearProgress(TrainerSpecializationProgressUtility.NotifyInitiatedSexCompleted(actor, recipient), 0.01f));
            Assert(NearProgress(actor.Training.specializationProgress, 0.01f));
            Assert(recipient.Training.specializationProgress == 0f);
            Assert(TrainerSpecializationProgressUtility.NotifyInitiatedSexCompleted(actor, actor) == 0f);

            // 尚未达到 20% 且未开启调教员开关时，普通主动行为仍可提供起步经验。
            Assert(!actor.Training.slaveTrainerEnabled);
            for (int i = 1; i < 20; i++)
                TrainerSpecializationProgressUtility.NotifyInitiatedSexCompleted(actor, recipient);
            Assert(actor.Training.specializationProgress >= TrainerSpecializationUtility.BasicQualificationProgress);
            Assert(TrainerSpecializationUtility.HasTrainerQualification(actor, out _));
        });

        Run("成功日常调教分别给符合条件的受训者和施教者结算", () =>
        {
            // 20% 基础阶段前，接收方获得最高收益，施教性奴不会绕过门槛。
            Pawn actor = EligibleTrainerPawn();
            actor.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            actor.Training.slaveTrainerEnabled = true;
            Pawn receiver = EligibleTrainerPawn();
            receiver.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            TrainerSpecializationProgressUtility.NotifyTrainingCompleted(actor, receiver);
            Assert(actor.Training.specializationProgress == 0f);
            Assert(NearProgress(receiver.Training.specializationProgress, 0.05f));

            // 解锁后再成功完成一场，双方独立领取对应来源，普通性交来源不得叠加在此入口。
            actor.Training.specializationProgress = 0.2f;
            TrainerSpecializationLifecycle.Maintain(actor);
            TrainerSpecializationProgressUtility.NotifyTrainingCompleted(actor, receiver);
            Assert(NearProgress(actor.Training.specializationProgress, 0.225f));
            Assert(NearProgress(receiver.Training.specializationProgress, 0.1f));

            // 玩家关闭个人选择后只停止施教经验，受训者的方向进度仍按自身条件结算。
            actor.Training.slaveTrainerEnabled = false;
            TrainerSpecializationProgressUtility.NotifyTrainingCompleted(actor, receiver);
            Assert(NearProgress(actor.Training.specializationProgress, 0.225f));
            Assert(NearProgress(receiver.Training.specializationProgress, 0.15f));
        });

        Run("异常增量、失格、切方向与终极记录都不能写入训导官进度", () =>
        {
            // 专属进度入口必须拒绝 NaN、无穷值以及非正的经验增量。
            Pawn slave = EligibleTrainerPawn();
            slave.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            foreach (float amount in new[] { 0f, -0.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert(TrainerSpecializationProgressUtility.TryGainProgress(slave, amount) == 0f);
            Assert(slave.Training.specializationProgress == 0f);

            // 失去持续条件后不继续成长；历史进度或其他当前方向也不能被误写。
            SSCBondUtility.GetChain(slave).Severity = 0.3f;
            Assert(TrainerSpecializationProgressUtility.TryGainProgress(slave, 0.05f) == 0f);
            SSCBondUtility.GetChain(slave).Severity = 0.5f;
            slave.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            Assert(TrainerSpecializationProgressUtility.TryGainProgress(slave, 0.05f) == 0f);
            Assert(slave.Training.specializationProgress == 0f);

            // 有效与禁用终极标记都是完成记录，当前方向切回训导官也不再积累普通进度。
            slave.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            slave.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            Assert(TrainerSpecializationProgressUtility.TryGainProgress(slave, 0.05f) == 0f);
            slave.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            Assert(TrainerSpecializationProgressUtility.TryGainProgress(slave, 0.05f) == 0f);
        });
    }
}
