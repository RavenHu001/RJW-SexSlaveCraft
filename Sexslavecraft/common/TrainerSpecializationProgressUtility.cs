using Verse;

namespace SexSlaveCraft
{
    /// <summary>在已经确认成功的行为结算点，为训导官普通培养发放进度。</summary>
    /// <remarks>任务是否真正完成及一次性认领由各自的 Job 或仪式状态负责；这里统一检查领取者资格与数值。</remarks>
    public static class TrainerSpecializationProgressUtility
    {
        // 被调教经验由所有方向共用的动态评分入口发放；这里只保留特色来源。
        public const float ProvidedTrainingProgress = 0.025f;
        public const float InitiatedSexProgress = 0.01f;

        /// <summary>向当前培养训导官且仍满足持续条件的性奴加入正的有限进度，返回实际增加量。</summary>
        public static float TryGainProgress(Pawn pawn, float amount)
        {
            // 公共进度方法只知道“当前方向”，所以在调用它之前还必须确认本方向、
            // 身份与锁链条件。坏档或其他补丁传入的 NaN、无穷值不能污染存档。
            if (pawn == null || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return 0f;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.specializationType != SexSlaveSpecializationType.TrainerOfficer ||
                !TrainerSpecializationUtility.MeetsContinuousConditions(pawn, out _) ||
                TrainerSpecializationUtility.HasFinalRecord(pawn)) return 0f;

            // 普通进度已达 100% 但尚未使用人格凝胶终极化时，不再重复累计；
            // 终极化记录也不能借当前方向重新获得普通培养进度。
            float before = comp.specializationProgress;
            if (float.IsNaN(before) || float.IsInfinity(before) || before < 0f ||
                before >= CompSexSlaveTraining.SpecializationCompletionProgress) return 0f;
            float after = comp.AddSpecializationProgress(amount);
            float gained = after - before;
            if (gained <= 0f || float.IsNaN(gained) || float.IsInfinity(gained)) return 0f;

            // 成功事件立刻同步普通 Hediff 与组件进度。生命周期自身有重入防护；
            // 不增加按 tick 扫描，也不触发终极化。
            TrainerSpecializationLifecycle.Notify(pawn);
            return gained;
        }

        /// <summary>一场完成的日常调教或整场绑定仪式，只判断施教者的训导官特色收益。</summary>
        public static void NotifyProvidedTrainingCompleted(Pawn trainer, Pawn receiver)
        {
            if (trainer == null || receiver == null || trainer == receiver) return;
            if (trainer.Dead || trainer.Destroyed || receiver.Dead || receiver.Destroyed) return;

            // 施教经验只能来自真正以调教员身份完成的工作。个人开关和 20% 门槛
            // 不限制被调教或主动性行为经验；终极记录及失格情况由下层再核对。
            CompSexSlaveTraining comp = trainer.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || receiver.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity != PawnIdentity.Slave ||
                !comp.slaveTrainerEnabled || trainer.Downed ||
                comp.specializationType != SexSlaveSpecializationType.TrainerOfficer ||
                !TrainerSpecializationUtility.HasTrainerQualification(trainer, out _)) return;
            TryGainProgress(trainer, ProvidedTrainingProgress);
        }

        /// <summary>一次已由 RJW 正常完成且由该性奴主动发起的双人行为，只奖励发起者。</summary>
        public static float NotifyInitiatedSexCompleted(Pawn initiator, Pawn recipient)
        {
            // 行为用途、方向、开始凭据和防重复领由 RJW 任务适配层先验证；
            // 此处再拒绝单人或同人场景，不把接受行为当成主动经验。
            if (initiator == null || recipient == null || initiator == recipient ||
                initiator.Dead || recipient.Dead || initiator.Destroyed || recipient.Destroyed) return 0f;
            return TryGainProgress(initiator, InitiatedSexProgress);
        }
    }
}
