// AI 辅助生成
#if SSC_RW17
using RimWorld;
using rjw;
using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    /// <summary>只为实际SSC调教的受方保留倒地期间的接收任务，不改变健康状态或授予新的调教许可。</summary>
    internal static class SSCTrainingDownedUtility
    {
        /// <summary>确认原驱动仍属于当前Job和编号；资格查询返回后再核对，不能用旧配对保护新工作。</summary>
        private static bool IsCurrent(JobDriver_Sex driver, TrainingJobUtility.JobContext context)
            => driver != null && context.IsCurrent && driver.pawn?.CurJob == context.Job &&
               driver.job == context.Job && driver.job.loadID == context.JobId;

        /// <summary>开始前建立中断条件时只能依据当前发起任务；receiver此时可能尚未安装。</summary>
        private static bool IsTrainingTarget(JobDriver_Sex initiator, Pawn target)
        {
            if (!(initiator is JobDriver_Training) && !(initiator is JobDriver_RitualTraining)) return false;
            var context = new TrainingJobUtility.JobContext(initiator);
            Pawn actor = initiator.pawn;
            if (!IsCurrent(initiator, context) || actor == null || target == null || actor == target ||
                actor.Dead || actor.Destroyed || target.Dead || target.Destroyed || !actor.Spawned || !target.Spawned ||
                actor.Map != target.Map || context.Target.Thing != target || initiator.Partner != target ||
                !SSCIdentityUtility.IsSexSlave(target)) return false;
            // 主持者不一定是锁链主人。仪式只依赖本场Lord实际主从角色，不读取指定调教员或旧标记授权。
            if (initiator is JobDriver_RitualTraining &&
                !BindingRitualStateUtility.IsActiveRitualFor(actor, target, actor.GetLord())) return false;
            return IsCurrent(initiator, context) && context.Target.Thing == target && initiator.Partner == target;
        }

        /// <summary>双方接收任务必须相互指回；单个训练标记、旧receiver或普通RJW任务都不能取得保护。</summary>
        private static JobDriver_SexBaseInitiator FindInitiator(JobDriver_TrainingReceiver receiver)
        {
            if (receiver == null) return null;
            var context = new TrainingJobUtility.JobContext(receiver);
            if (!IsCurrent(receiver, context) || receiver.job.def != SSCDefOf.SSC_TrainingReceiver) return null;
            Pawn actor = receiver.Partner;
            var initiator = actor?.jobs?.curDriver as JobDriver_SexBaseInitiator;
            if (actor == null || context.Target.Thing != actor || !IsTrainingTarget(initiator, receiver.pawn)) return null;
            return IsCurrent(receiver, context) && receiver.Partner == actor ? initiator : null;
        }

        /// <summary>仅在MakeDowned原有职责判断位置补充已实际开始的SSC接收者，其他倒地通知仍完整执行。</summary>
        internal static bool KeepDutyWhenDowned(Pawn pawn, bool onlyInBed)
        {
            bool original = PawnUtility.DutyActiveWhenDown(pawn, onlyInBed);
            if (original || !SSCTrainingDownedPatches.Ready) return original;
            var receiver = pawn?.jobs?.curDriver as JobDriver_TrainingReceiver;
            var receiverContext = new TrainingJobUtility.JobContext(receiver);
            JobDriver_SexBaseInitiator initiator = FindInitiator(receiver);
            if (initiator == null || initiator.pawn.Downed) return false;
            var initiatorContext = new TrainingJobUtility.JobContext(initiator);
            // 请求可能属于绑定准备；使用原请求Kind取得Guard的真实开始凭据，不另外保存bool。
            if (!SSCRestrictionJobContext.TryCreate(initiator, out SSCRestrictionRequest request) ||
                !SSCRestrictionJobGuard.HasStartedScene(initiator, request.Kind)) return false;
            return IsCurrent(receiver, receiverContext) && IsCurrent(initiator, initiatorContext) &&
                FindInitiator(receiver) == initiator;
        }

        /// <summary>沿用RJW已倒地受方可继续的原快照语义，只解除受训者的新倒地中断；施教者仍读取原值。</summary>
        internal static bool InitialDownedForAbort(Pawn observed, JobDriver_Sex owner)
        {
            bool original = observed.Downed;
            if (original || !SSCTrainingDownedPatches.Ready) return original;
            if (owner is JobDriver_TrainingReceiver receiver)
                return observed == receiver.pawn && FindInitiator(receiver) != null;
            return observed != owner?.pawn && IsTrainingTarget(owner, observed);
        }
    }
}
#endif
