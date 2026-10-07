using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>固定主人配对的亲昵后续；地点选择、接收任务、互动与结算完整复用 RJW Quickie。</summary>
    public class JobDriver_PetAffectionFollowup : JobDriver_SexQuick
    {
        private bool masterQueueRecorded;
        private List<int> originalMasterQueueIds = new List<int>();
        private int receiverJobId = -1;
        // 同步 native init 内暂存的旧 idle 队列只存活于回调栈；实际 Start 验证时将其与当前队列一并核对。
        private List<QueuedJob> temporarilyHeldMasterQueue;

        // Start 的原生 Action 无法用返回值中止；只在此驱动的同步 native init 栈内消费这个取消信号。
        private sealed class NativeInitializationAborted : Exception { }

        private bool IsCurrentJob => !ended && pawn?.jobs?.curDriver == this && pawn.CurJob == job;
        private bool HasStarted => SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.Consensual);

        public override void ExposeData()
        {
            base.ExposeData();
            // 只保存主人的原排队任务编号；Job 本身仍由原版队列保存，不复制任务或在读档后重建命令。
            Scribe_Values.Look(ref masterQueueRecorded, "sscAffectionFollowupMasterQueueRecorded", false);
            Scribe_Collections.Look(ref originalMasterQueueIds, "sscAffectionFollowupMasterQueueIds", LookMode.Value);
            Scribe_Values.Look(ref receiverJobId, "sscAffectionFollowupReceiverJobId", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && originalMasterQueueIds == null)
                originalMasterQueueIds = new List<int>();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 缓存驱动尚未接管宠物，预约只读核对双方资格；准备归属由实际运行驱动检查。
            return (HasStarted || PetAffectionFollowupUtility.CanContinue(pawn, Partner)) &&
                base.TryMakePreToilReservations(errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // ForceWait 可以保留被暂停的原空闲任务；第一次安装时记住它们，恢复存档时沿用原编号。
            if (!masterQueueRecorded && Scribe.mode == LoadSaveMode.Inactive)
            {
                originalMasterQueueIds = Partner?.jobs?.jobQueue
                    .Where(queued => queued.job != null && !SSCRestrictionJobGuard.OwnsPreparedJob(this, Partner, queued.job))
                    .Select(queued => queued.job.loadID).ToList() ?? new List<int>();
                masterQueueRecorded = true;
            }

            // 真正 Start 之前持续复查绑定、许可外的 RJW 资格和准备归属；开始之后保留原场景正常收尾。
            this.FailOn(() => !HasStarted && !CanContinuePreparation());
            AddFinishAction(condition => ReleaseOwnUnstartedReceiver());
            foreach (Toil toil in base.MakeNewToils())
            {
                Action original = toil.initAction;
                toil.initAction = () =>
                {
                    if (!IsCurrentJob) return;
                    if (HasStarted)
                    {
                        original?.Invoke();
                        return;
                    }
                    // 全局 FailOn 与 init 之间可能发生同步重入；每个原回调前再次核对，覆盖接收与 Start 步骤。
                    if (!CanContinuePreparation())
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    InvokePreservingMasterQueue(original);
                };
                yield return toil;
            }
        }

        /// <summary>只允许本请求登记的准备或指回宠物的标准接收驱动，主人新工作和新排队命令会取消准备。</summary>
        private bool CanContinuePreparation()
        {
            Pawn master = Partner;
            if (!IsCurrentJob || pawn.jobs.jobQueue.Any(queued => queued.job != null) ||
                !masterQueueRecorded || originalMasterQueueIds == null ||
                !PetAffectionFollowupUtility.CanContinue(pawn, master) || !IsCurrentJob || Partner != master ||
                SSCBondUtility.GetBoundMaster(pawn) != master || master?.jobs == null ||
                master.CurJob == null || master.CurJob.playerForced) return false;

            bool ownedPreparation = SSCRestrictionJobGuard.OwnsPreparedJob(this, master, master.CurJob);
            bool ownedReceiver = master.CurJob.loadID == receiverJobId && master.CurJob.def == xxx.getting_quickie &&
                master.jobs.curDriver is JobDriver_SexBaseReciever receiver && receiver.Partner == pawn;
            if (!ownedPreparation && !ownedReceiver) return false;

            // 原 idle 队列必须仍保持相同顺序；除此之外只接受本事件新增的准备，不忽略玩家追加的命令。
            var retained = new List<int>();
            if (temporarilyHeldMasterQueue != null)
            {
                foreach (QueuedJob queued in temporarilyHeldMasterQueue)
                {
                    if (!IsRetainedIdleJob(queued.job)) return false;
                    retained.Add(queued.job.loadID);
                }
            }
            foreach (QueuedJob queued in master.jobs.jobQueue)
            {
                if (queued.job == null || queued.job.playerForced) return false;
                if (!SSCRestrictionJobGuard.OwnsPreparedJob(this, master, queued.job))
                {
                    if (!IsRetainedIdleJob(queued.job)) return false;
                    retained.Add(queued.job.loadID);
                }
            }
            return originalMasterQueueIds.SequenceEqual(retained);
        }

        /// <summary>原 Quickie 的地点准备会 StopAll；临时保管原 idle 队列，避免它清空不属于本事件的任务。</summary>
        private void InvokePreservingMasterQueue(Action original)
        {
            Pawn master = Partner;
            Job previous = master.CurJob;
            JobQueue queue = master.jobs.jobQueue;
            var retained = new List<QueuedJob>();
            var preparation = new List<QueuedJob>();
            while (queue.Count > 0)
            {
                QueuedJob queued = queue.Dequeue();
                if (originalMasterQueueIds.Contains(queued.job.loadID)) retained.Add(queued);
                else preparation.Add(queued);
            }
            foreach (QueuedJob queued in preparation) queue.EnqueueLast(queued.job, queued.tag);

            temporarilyHeldMasterQueue = retained;
            try
            {
                original?.Invoke();
            }
            catch (NativeInitializationAborted)
            {
                // 已由 Start 守卫结束当前任务。只截断原 init 后续对空 Sexprops 的访问，其他异常仍由引擎处理。
            }
            finally
            {
                // 只认领这个原回调实际新建的接收任务，不能仅凭相同类型与 Partner 接管后来别的场景。
                Job receiving = master.CurJob;
                if (receiving != null && receiving != previous && receiving.def == xxx.getting_quickie &&
                    !receiving.playerForced && master.jobs.curDriver is JobDriver_SexBaseReciever receiver &&
                    receiver.Partner == pawn)
                    receiverJobId = receiving.loadID;

                // Dequeue 不清理或归池，原任务引用和 tag 均能保留；即使原回调异常也恢复一次。
                foreach (QueuedJob queued in retained)
                    if (master.CurJob != queued.job && !queue.Contains(queued.job))
                        queue.EnqueueLast(queued.job, queued.tag);
                temporarilyHeldMasterQueue = null;
                if (!IsCurrentJob) ReleaseOwnUnstartedReceiver();
            }
        }

        /// <summary>ForceWait 可挂起的原空闲任务与普通亲昵的入口一致，重要工作不能作为保留队列混入。</summary>
        private static bool IsRetainedIdleJob(Job idle)
            => idle != null && !idle.playerForced && idle.def != null &&
                (idle.def == JobDefOf.Wait_Wander || idle.def.defName == "GotoWander" ||
                 (idle.def == JobDefOf.Wait && idle.expiryInterval <= 0));

        /// <summary>实际 Start 必须仍由本运行驱动发起，并已有本次创建且指回宠物的接收任务。</summary>
        internal bool CanStartNow()
        {
            if (!IsCurrentJob) return false;
            if (HasStarted) return true;
            return CanContinuePreparation() && Partner.CurJob.loadID == receiverJobId &&
                Partner.CurJob.def == xxx.getting_quickie &&
                Partner.jobs.curDriver is JobDriver_SexBaseReciever receiver && receiver.Partner == pawn;
        }

        /// <summary>实际入口再次复查；迟到旧回调只拒绝自身，不结束宠物后来接手的新工作。</summary>
        internal bool CheckBeforeStart()
        {
            if (CanStartNow()) return true;
            if (IsCurrentJob) EndJobWith(JobCondition.Incompletable);
            return false;
        }

        /// <summary>其他前缀也可拒绝 Start；在原 init 栈内截断后续语句，外部直接调用只结束当前失败任务。</summary>
        internal void CheckAfterStart(bool ranOriginal)
        {
            // 原 Start 确实执行已足以证明开始，避免依赖各个 Postfix 写入共享开始凭据的相对顺序。
            if (ranOriginal || HasStarted) return;
            if (IsCurrentJob) EndJobWith(JobCondition.Incompletable);
            if (temporarilyHeldMasterQueue != null) throw new NativeInitializationAborted();
        }

        /// <summary>未实际开始的原 SexToil 取消不能执行 RJW End，它会读取尚未生成的 Sexprops。</summary>
        internal bool CanEndScene() => HasStarted;

        /// <summary>原版接收任务安装后、Start 前取消时只清理本次接收，已开始场景与后来替换的工作均保留。</summary>
        private void ReleaseOwnUnstartedReceiver()
        {
            if (HasStarted) return;
            Pawn master = Partner;
            int id = receiverJobId;
            receiverJobId = -1;
            if (master?.CurJob?.loadID != id || master.CurJob.def != xxx.getting_quickie ||
                !(master.jobs.curDriver is JobDriver_SexBaseReciever receiver) || receiver.Partner != pawn) return;
            receiver.parteners?.Remove(pawn);
            if (receiver.parteners == null || receiver.parteners.Count == 0)
                master.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }
    }

    [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), nameof(JobDriver_SexBaseInitiator.Start))]
    internal static class PetAffectionFollowupStartHook
    {
        /// <summary>只追加亲昵后续的完整资格边界；其他 RJW 场景继续使用已有统一 Start 守卫。</summary>
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(JobDriver_SexBaseInitiator __instance)
            => !(__instance is JobDriver_PetAffectionFollowup followup) || followup.CheckBeforeStart();

        /// <summary>即使更早的前缀让本前缀被跳过，也用原方法是否执行拦住原 Quickie init 的后续访问。</summary>
        public static void Postfix(JobDriver_SexBaseInitiator __instance, bool __runOriginal)
        {
            if (__instance is JobDriver_PetAffectionFollowup followup) followup.CheckAfterStart(__runOriginal);
        }
    }

    [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), nameof(JobDriver_SexBaseInitiator.End))]
    internal static class PetAffectionFollowupEndHook
    {
        /// <summary>只约束此新驱动的未开始收尾；已经开始的后续及其他 RJW 驱动保持原有 End 行为。</summary>
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(JobDriver_SexBaseInitiator __instance)
            => !(__instance is JobDriver_PetAffectionFollowup followup) || followup.CanEndScene();
    }
}
