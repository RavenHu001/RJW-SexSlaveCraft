using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>以当前接收驱动的有效配对确定人格排泄执行者，不保存额外占用状态。</summary>
    public static class PersonalityExcretionJobUtility
    {
        /// <summary>已开始的人格排泄只允许原执行者继续或重复交接，其他执行者必须等待。</summary>
        public static bool IsOccupiedByOther(Pawn receiver, Pawn actor)
        {
            Pawn initiator = GetActiveInitiator(receiver);
            return initiator != null && initiator != actor;
        }

        /// <summary>从当前接收驱动与已登记参与者识别有效配对，不依赖玩家指派标记或持久化锁。</summary>
        public static Pawn GetActiveInitiator(Pawn receiver)
        {
            if (!IsAvailable(receiver)
                || !(receiver.jobs?.curDriver is JobDriver_SexBaseReciever receiverDriver)
                || receiverDriver.pawn != receiver
                || receiverDriver.job == null || receiverDriver.job != receiver.CurJob
                || (receiverDriver.job.def != SSCDefOf.SSC_TrainingReceiver
                    && !OnaholeCompatibilityUtility.IsBeOnaholeDriver(receiverDriver))
                || receiverDriver.parteners == null)
                return null;

            // DoSetup 在 Start 前登记，End 会注销；因此准备阶段已受保护，收尾后立即释放。
            // 兼容设备可登记多个参与者，不能只依赖接收 Job 的 targetA。
            foreach (Pawn actor in receiverDriver.parteners)
            {
                if (actor == receiver || !IsAvailable(actor) || actor.Map != receiver.Map) continue;
                if (actor.jobs?.curDriver is JobDriver_PE initiator
                    && initiator.pawn == actor
                    && initiator.job != null && initiator.job == actor.CurJob
                    && initiator.Partner == receiver)
                    return actor;
            }
            return null;
        }

        /// <summary>参与者失效、离场、征召或精神失常时保留原本的中止与重新选任务路径。</summary>
        private static bool IsAvailable(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned
                && pawn.Map != null && !pawn.Drafted && !pawn.InMentalState;
        }
    }
}
