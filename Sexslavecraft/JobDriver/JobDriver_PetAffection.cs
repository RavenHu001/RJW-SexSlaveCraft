using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>附近空闲宠物接近实际主人，完成两秒可见陪伴后统一结算亲昵。</summary>
    public class JobDriver_PetAffection : JobDriver
    {
        private const int AffectionDurationTicks = 120;
        private Pawn waitingMaster;
        private int waitingJobId = -1;
        private bool interactionStarted;

        private Pawn Master => job.GetTarget(TargetIndex.A).Pawn;
        private bool IsCurrentJob => !ended && pawn.jobs?.curDriver == this && pawn.CurJob == job;

        public override void ExposeData()
        {
            base.ExposeData();
            // 读档由原版恢复当前 toil 和剩余 tick，不重新 ForceWait 或补发奖励。
            // 保存明确等待归属，让取消旧任务时只释放本次创建的主人等待。
            Scribe_References.Look(ref waitingMaster, "sscAffectionWaitingMaster");
            Scribe_Values.Look(ref waitingJobId, "sscAffectionWaitingJobId", -1);
            Scribe_Values.Look(ref interactionStarted, "sscAffectionInteractionStarted", false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 不预约主人或替换其任务；只有通过空闲检查且到达接触位置才短暂停留。
            return PetSpecializationUtility.CanApproachPetAffectionNow(pawn, Master) &&
                PetSpecializationUtility.CanStartPetAffectionJobWithoutDisruptingWork(Master);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !PetSpecializationUtility.CanApproachPetAffectionNow(pawn, Master));
            // 全任务结束时清理，不能在互动 toil 结束时提前释放、使最终结算失去目标。
            AddFinishAction(condition => ReleaseOwnMasterWait());

            // 自动发现范围仍是 2.9 格；原版路径跟踪主人，靠近期间主人变忙就取消。
            Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            approach.FailOn(() => !PetSpecializationUtility.CanStartPetAffectionJobWithoutDisruptingWork(Master));
            yield return approach;

            // 主人等待多一 tick，防止双方更新顺序让主人先自然到期；互动与读条仍为120 tick。
            // WaitWith 自带停止移动、主人 ForceWait、接触检查、宠物朝向及延时进度条。
            Toil affection = Toils_General.WaitWith(TargetIndex.A, AffectionDurationTicks + 1,
                useProgressBar: true, maintainPosture: false, maintainSleep: false,
                face: TargetIndex.A, pathEndMode: PathEndMode.Touch);
            affection.defaultDuration = AffectionDurationTicks;
            Action startInteraction = affection.initAction;
            affection.initAction = delegate
            {
                if (!IsCurrentJob) return;
                interactionStarted = true;
                // 路径到达与任务安装之间再复查；重要工作开始后不能强制创建等待。
                if (!PetSpecializationUtility.CanDoPetAffectionNow(pawn, Master) ||
                    !PetSpecializationUtility.CanStartPetAffectionJobWithoutDisruptingWork(Master))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Pawn master = Master;
                Job previous = master.CurJob;
                try
                {
                    startInteraction();
                }
                finally
                {
                    // ForceWait 的旧任务清理可能重入取消宠物，或在新等待安装后抛异常。
                    // 即使原 init 未正常返回也认领确切的新等待；异常仍交给原版恢复处理。
                    Job waiting = master.CurJob;
                    if (waiting != null && waiting != previous && waiting.def == JobDefOf.Wait &&
                        waiting.expiryInterval == AffectionDurationTicks + 1 && !waiting.playerForced &&
                        waiting.startTick == Find.TickManager.TicksGame)
                    {
                        waitingMaster = master;
                        waitingJobId = waiting.loadID;
                        if (!IsCurrentJob) ReleaseOwnMasterWait();
                    }
                }
                if (!IsCurrentJob) return;
                // 其他模组替换的任务不归本任务清理，也不能冒充成功的互动准备。
                if (!HasOwnMasterWait())
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                master.rotationTracker.FaceTarget(pawn);
            };
            // 原版在 init 前也检查 toil 失败条件；尚未开始时查空闲，开始后查等待归属。
            affection.FailOn(() => !PetSpecializationUtility.CanDoPetAffectionNow(pawn, Master) ||
                (interactionStarted ? !HasOwnMasterWait() :
                    !PetSpecializationUtility.CanStartPetAffectionJobWithoutDisruptingWork(Master)));
            // 保留原版 tickIntervalAction 的宠物朝向，只追加当前归属等待的主人朝向。
            affection.tickAction = delegate
            {
                if (IsCurrentJob && HasOwnMasterWait()) waitingMaster.rotationTracker.FaceTarget(pawn);
            };
            EffecterDef effect = DefDatabase<EffecterDef>.GetNamedSilentFail("SSC_PetAffectionInteraction");
            if (effect != null) affection.WithEffect(effect, TargetIndex.A);
            yield return affection;

            Toil finish = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            finish.initAction = delegate
            {
                if (!IsCurrentJob) return;
                // 目标结束或替换了本次等待就不完成；统一入口继续认领冷却、记忆与猫经验。
                if (HasOwnMasterWait() && PetSpecializationUtility.CompletePetAffection(pawn, Master))
                    ReleaseOwnMasterWait();
                else
                    EndJobWith(JobCondition.Incompletable);
            };
            yield return finish;
        }

        /// <summary>主人换任务后，亲昵不能把新任务当成本次互动，也不能再修改其朝向。</summary>
        private bool HasOwnMasterWait()
        {
            Job waiting = waitingMaster?.CurJob;
            return waitingJobId >= 0 && waitingMaster == Master && waiting != null &&
                waiting.loadID == waitingJobId && waiting.def == JobDefOf.Wait;
        }

        /// <summary>先撤销认领再结束确切等待；重复清理或主人已接手的新任务均不受影响。</summary>
        private void ReleaseOwnMasterWait()
        {
            Pawn master = waitingMaster;
            int id = waitingJobId;
            waitingMaster = null;
            waitingJobId = -1;
            Job waiting = master?.CurJob;
            if (waiting != null && waiting.loadID == id && waiting.def == JobDefOf.Wait)
                master.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }
}
