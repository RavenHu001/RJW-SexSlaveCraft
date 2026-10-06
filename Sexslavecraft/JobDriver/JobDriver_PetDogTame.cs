using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>狗终极驯导的同格接近任务；到达后复用原版技能预热、暂停与进度条。</summary>
    public class JobDriver_PetDogTame : JobDriver_CastAbility
    {
        private const int ApproachRetryIntervalTicks = 60;
        private const int MaxApproachIdleRetries = 10;
        private int approachIdleRetries;

        private Pawn TargetPawn => job.GetTarget(TargetIndex.A).Pawn;

        public override void ExposeData()
        {
            base.ExposeData();
            // 正常寻路、目标引用与预热剩余 tick 由原版 Job/PathFollower/Stance 保存。
            // 这里只保存无进展重试次数；读档不重新启动预热或再次暂停目标。
            Scribe_Values.Look(ref approachIdleRetries, "sscDogTameApproachIdleRetries", 0);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !HasCurrentAbility());
            AddFinishAction(condition => CleanupOwnWarmup());

            // 目标保持 Pawn 引用，原版寻路会追踪移动中的当前位置。
            // 不提前暂停目标，也不预订或替换目标任务，以免改变尚未驯服的动物行为。
            Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
            approach.FailOn(() => !job.ability.CanCast ||
                !PetDogAbilityUtility.CanTarget(pawn, TargetPawn, out _));
            Action startApproach = approach.initAction;
            approach.initAction = delegate
            {
                approachIdleRetries = 0;
                startApproach();
            };
            approach.tickAction = delegate
            {
                if (pawn.jobs?.curDriver != this || TargetPawn == null || pawn.Position == TargetPawn.Position) return;
                if (pawn.pather.MovingNow)
                {
                    approachIdleRetries = 0;
                    return;
                }
                // 当前 toil 的 init 不会因读档重跑。仅在行走阶段停滞时有限补寻路，
                // 不影响正常长距离行走，不在读条阶段发起移动或刷新目标眩晕。
                if (Find.TickManager.TicksGame % ApproachRetryIntervalTicks != 0) return;
                if (++approachIdleRetries > MaxApproachIdleRetries ||
                    !PetDogAbilityUtility.CanSelectTarget(pawn, TargetPawn, out _))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                pawn.pather.StartPath(TargetPawn, PathEndMode.OnCell);
            };
            yield return approach;

            // 到达通知与目标走动可能发生在相邻 tick；未同格就回到接近阶段。
            yield return Toils_Jump.JumpIf(approach, () => pawn.Position != TargetPawn.Position);
            yield return Toils_General.Do(() => pawn.pather.StopDead());

            // 不调用 base.MakeNewToils：其中的结束回调可能为 abilityCasting 任务扣冷却。
            // 这里只复用一次 CastVerb，原版预热负责 2 秒倒计时、暂停、气泡和实际 Activate。
            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, false);
            // 预热中的资格每 tick 复查；完成后冷却已启动，驯服也会改变目标阵营资格。
            // 此时允许原版 FinishedBusy 正常收尾，不能把已成功结算误标成失败。
            cast.FailOn(() => job.verbToUse.WarmingUp &&
                !PetDogAbilityUtility.CanApply(pawn, TargetPawn, out _));
            cast.WithProgressBar(TargetIndex.A, () => job.verbToUse.WarmupProgress, false, -0.5f, false);
            yield return cast;
        }

        /// <summary>终极标记撤销或人格转移后，原任务不能使用失去归属的技能实例。</summary>
        private bool HasCurrentAbility()
        {
            return job.ability is Ability_PetDogTame && job.verbToUse is Verb_PetDogTame &&
                job.ability.pawn == pawn && job.verbToUse == job.ability.verb &&
                pawn.abilities?.GetAbility(job.ability.def) == job.ability;
        }

        /// <summary>任务中断只清本人预热，冷却仅由成功 Activate 扣除。</summary>
        private void CleanupOwnWarmup()
        {
            if (pawn.jobs?.curDriver != this) return;
            // Interrupt 结束 stance 并清理其效果，Reset 再清目标和完成回调，避免取消后施放。
            // 目标眩晕沿用原版限时自然结束，不清除目标可能已有的其他来源眩晕。
            Stance_Warmup warmup = pawn.stances?.curStance as Stance_Warmup;
            if (warmup != null && warmup.verb == job.verbToUse) warmup.Interrupt();
            job.verbToUse?.Reset();
        }
    }
}
