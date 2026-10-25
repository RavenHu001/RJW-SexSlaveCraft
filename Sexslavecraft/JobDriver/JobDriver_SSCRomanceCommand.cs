// AI 辅助生成
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>第四阶段一次性命令；原生 Toil 忽略互动返回值，本驱动自行记录真实结果而不复制结算。</summary>
    public sealed class JobDriver_SSCRomanceCommand : JobDriver
    {
        private const int MaximumWaitTicks = 600;
        private string requestId;
        private int waitTicks;
        private SSCRomanceCommandRecord record;
        /// <summary>Job 目标和执行者都不独占预约；目标仍可执行自身任务，不创建配对性行为任务。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        /// <summary>保存归属校验及等待进度；原生 Job/Driver 保存位置、toil 索引，读档不重置等待期限。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref requestId, "sscRomanceRequestId");
            Scribe_Values.Look(ref waitTicks, "sscRomanceWaitTicks", 0);
        }
        /// <summary>实际当前驱动领取原登记；加载重建 toil 时不领取、不维护半恢复的链。</summary>
        private bool EnsureClaim()
        {
            if (ended) return false;
            if (record != null)
            {
                if (record.runtimeDriver != this || record.runtimeJob != job || pawn.CurJob != job || pawn.jobs?.curDriver != this) return false;
                // 领取后的 Job 字段仍可能被外部替换；在原生接近 toil 读取目标前拒绝，不能只等最终互动守卫。
                if (!SSCRomanceCommandGameComponent.Matches(record, job))
                { SSCRomanceCommandGameComponent.Cancel(record, "InvalidRecord"); return false; }
                return true;
            }
            var store = SSCRomanceCommandGameComponent.Current;
            var claimed = store?.FindOwned(this);
            // 取消记录只用于显示原因/退出，绝不能因为读取它而再次取得授权。
            if (claimed != null && !claimed.Terminal) claimed = store.Claim(this);
            if (claimed == null || (requestId != null && requestId != claimed.RequestId)) return false;
            if (claimed.Terminal) claimed.runtimeDriver = this;
            record = claimed; requestId = claimed.RequestId; return true;
        }
        /// <summary>执行中持续验证身份/目标/关系，已完成记录只允许收尾，不能再次调用互动。</summary>
        private bool Invalid()
        {
            if (!EnsureClaim()) return true;
            if (record.Terminal) return false;
            string reason = SSCRomanceCommandUtility.ActorFailure(pawn) ?? (!SSCRomanceCommandGameComponent.IsCurrent(record) ? "InvalidAuthorization" :
                SSCRomanceEligibility.Check(pawn, record.Stamp.Target, record.Stamp.Action) != SSCRomanceEligibilityReason.None ? "InvalidParticipants" :
                SSCRomanceCommandUtility.TaskFailure(pawn, record.Stamp.Target, record.Stamp.Action));
            if (reason == null) return false;
            SSCRomanceCommandGameComponent.Cancel(record, reason); return true;
        }
        /// <summary>所有 toil 继续前处理终态：成功立即收尾，其余立即停止，取消后不再沿原路径追赶目标。</summary>
        private JobCondition CurrentEndCondition()
        {
            // TaskFailure 的路径查询可回调修改 Job；结束资格检查后再核对一次原任务字段。
            if (Invalid() || !EnsureClaim()) return JobCondition.Incompletable;
            if (!record.Terminal) return JobCondition.Ongoing;
            return record.Status == SSCRomanceCommandStatus.Completed ? JobCondition.Succeeded : JobCondition.Incompletable;
        }
        /// <summary>接近、有限等待、重新定位后进行一次互动；所有阶段关闭随机社交，不唤醒/占用目标。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 统一 Invalid 先取得记录再解释目标离图/死亡，避免原生提前失败吞掉具体取消原因。
            AddEndCondition(CurrentEndCondition);
            AddFinishAction(Finish);
            yield return Toils_General.Do(() =>
            {
                if (!EnsureClaim()) EndJobWith(JobCondition.Incompletable);
                else if (record.Terminal) EndJobWith(record.Status == SSCRomanceCommandStatus.Completed ? JobCondition.Succeeded : JobCondition.Incompletable);
            });
            var approach = Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
            approach.socialMode = RandomSocialMode.Off;
            yield return approach;
            var wait = ToilMaker.MakeToil("SSC_RomanceWait");
            wait.socialMode = RandomSocialMode.Off; wait.defaultCompleteMode = ToilCompleteMode.Never;
            // 两种回调只推进等待逻辑；计数不在 init 重置，正常读档继续剩余等待。
            wait.initAction = () => WaitForInteraction(0);
            wait.tickIntervalAction = delta => WaitForInteraction(delta);
            yield return wait;
            yield return Toils_Jump.JumpIf(approach, () => !SocialInteractionUtility.IsGoodPositionForInteraction(pawn, job.targetA.Pawn));
            var execute = Toils_General.Do(ExecuteOnce); execute.socialMode = RandomSocialMode.Off;
            yield return execute;
        }
        /// <summary>冷却/清醒可短暂等待，超时明确未互动；不强制通过原版能力或位置检查。</summary>
        private void WaitForInteraction(int delta)
        {
            if (ended) return;
            if (Invalid()) { EndJobWith(JobCondition.Incompletable); return; }
            if (ended) return;
            if (record.Terminal) { EndJobWith(record.Status == SSCRomanceCommandStatus.Completed ? JobCondition.Succeeded : JobCondition.Incompletable); return; }
            waitTicks += delta;
            bool canInteract = pawn.interactions.CanInteractNowWith(record.Stamp.Target, job.interaction);
            // 可交互性回调也能中断当前驱动，旧等待回调不能再推进 toil 或发起第二次结束。
            if (ended) return;
            if (canInteract) ReadyForNextToil();
            else if (waitTicks >= MaximumWaitTicks)
            { SSCRomanceCommandGameComponent.Cancel(record, "InteractionTimeout"); EndJobWith(JobCondition.Incompletable); }
        }
        /// <summary>唯一互动入口；重复 initAction、驱动恢复或 Cleanup 均不能重新执行终态记录。</summary>
        public void ExecuteOnce()
        {
            if (ended) return;
            if (Invalid()) { EndJobWith(JobCondition.Incompletable); return; }
            if (ended || record.Terminal) return;
            SSCRomanceExecutionScope.Execute(this, record);
            SSCRomanceCommandGameComponent.Feedback(record);
        }
        /// <summary>结束回调只释放认领并取消未完成请求，不建立关系、不发记忆、不再次调用 worker。</summary>
        private void Finish(JobCondition condition)
        {
            // 任务可能在第一条 initAction 领取前就被打断；只找回原 Job 的清理归属，不能因此认领执行权限。
            record ??= SSCRomanceCommandGameComponent.Current?.FindOwned(this);
            if (record == null) return;
            if (record.runtimeDriver != null && record.runtimeDriver != this) return;
            SSCRomanceCommandGameComponent.Cancel(record, "JobEnded");
            SSCRomanceCommandGameComponent.Feedback(record);
            record.runtimeDriver = null;
        }
    }
}
