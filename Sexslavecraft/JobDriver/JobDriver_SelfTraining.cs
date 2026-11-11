using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>复用 RJW 的单人场景，仅在完整结束后领取独立的自我调教收益。</summary>
    public class JobDriver_SelfTraining : JobDriver_Masturbate
    {
        private SSCSelfTrainingSnapshot snapshot;
        private InteractionDef manualInteraction;

        /// <summary>沿原继承链保存计时及第三批余数节点，自身继续保存原完成快照与手动交互。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref snapshot, "sscSelfTrainingSnapshot");
            Scribe_Defs.Look(ref manualInteraction, "sscManualSelfTrainingInteraction");
        }

        /// <summary>供开发入口及开始步骤共用；许可仍由统一策略和任务守卫复查。</summary>
        internal static bool CanStart(Pawn actor, IntVec3 destination, out string reason)
        {
            reason = null;
            if (actor == null || actor.Dead || actor.Destroyed || !actor.Spawned || actor.Map == null ||
                actor.health?.Downed == true || actor.Drafted || actor.IsBurning() || actor.IsFighting())
            {
                reason = "SSC_SelfTraining_CannotStart".Translate();
                return false;
            }
            SSCRestrictionDecision permission = SSCRestrictionPolicy.Evaluate(
                new SSCRestrictionRequest(actor, actor, SSCInteractionKind.SelfTraining, true));
            if (!permission.Allowed)
            {
                reason = "SSC_SelfTraining_PermissionDenied".Translate();
                return false;
            }
            if (!xxx.can_masturbate(actor))
            {
                reason = "SSC_SelfTraining_RJWBlocked".Translate();
                return false;
            }
            if (!destination.IsValid || !destination.InBounds(actor.Map) ||
                !actor.CanReach(destination, PathEndMode.OnCell, Danger.Deadly))
            {
                reason = "SSC_SelfTraining_DestinationUnavailable".Translate();
                return false;
            }
            if (actor.needs?.TryGetNeed<Need_Corruption>() == null)
            {
                reason = "SSC_SelfTraining_NeedUnavailable".Translate();
                return false;
            }
            return true;
        }

        /// <summary>确认角色当前仍运行此驱动；旧完成回调不能结束后来换入的任务。</summary>
        private bool IsActiveDriver => pawn?.jobs?.curDriver == this;
        /// <summary>自我目标必须仍为本人；恢复候选和实际步骤均复用这一原业务资格。</summary>
        private bool IsCurrentJob => IsActiveDriver && job?.GetTarget(iTarget).Pawn == pawn;

        /// <summary>只中断仍属于自己的当前任务，不接续失效的交互或完成收益。</summary>
        private void Abort()
        {
            if (IsActiveDriver) pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }

        /// <summary>基类三个步骤保持原位，让动画框架对 RJW 单人 MakeNewToils 的补丁仍可修改第二步。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // RJW 在构造步骤时重置计时；已有场景读档后保留当前进度。
            bool resuming = snapshot?.Captured == true && !snapshot.CompletionClaimed;
            int remaining = ticks_left, sexRemaining = sex_ticks, elapsedDuration = duration;
            int orgasmAt = orgasmstick, orgasmStart = orgasmStartTick;
            int hearts = ticks_between_hearts, hits = ticks_between_hits, thrusts = ticks_between_thrusts;
            List<Toil> toils = base.MakeNewToils().ToList();
            if (resuming)
            {
                ticks_left = remaining;
                sex_ticks = sexRemaining;
                duration = elapsedDuration;
                orgasmstick = orgasmAt;
                orgasmStartTick = orgasmStart;
                ticks_between_hearts = hearts;
                ticks_between_hits = hits;
                ticks_between_thrusts = thrusts;
                if (toils.Count > 1) toils[1].defaultDuration = elapsedDuration;
#if SSC_RW17
                // 原周期恢复不是一次新周期；只废弃旧样本，恢复候选在完整枚举末尾再通知。
                SSCOrgasmProgressSampling.InvalidateSamples(this);
#endif
            }
            this.FailOn(() => !IsCurrentJob || pawn.Map == null || !SSCSelfTrainingEligibility.IsEligible(pawn) ||
                !xxx.can_masturbate(pawn) || !cell.IsValid || !cell.InBounds(pawn.Map) ||
                (snapshot?.Captured == true && !HasValidInteraction()));

            if (toils.Count < 3)
            {
                Log.Error("[SSC SelfTraining] RJW masturbation toils are incomplete.");
                yield break;
            }

            Toil scene = toils[1];
            Action startRjw = scene.initAction;
            scene.initAction = () =>
            {
                if (!IsCurrentJob) { Abort(); return; }
                if (snapshot?.Captured == true)
                {
                    if (!SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.SelfTraining)) Abort();
                    return;
                }
                if (!CanStart(pawn, cell, out string reason) || !TryPrepareInteraction(out reason))
                {
                    if (job?.playerForced == true)
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                    Abort();
                    return;
                }
                startRjw?.Invoke();
                if (!IsCurrentJob || !SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.SelfTraining))
                {
                    Abort();
                    return;
                }
                snapshot = SSCSelfTrainingUtility.CaptureAtSceneStart(pawn);
                if (snapshot == null) Abort();
            };
            for (int i = 0; i < toils.Count; i++) yield return toils[i];

            // 基类的完成步骤先执行 RJW 的 Aftersex；本步骤统一处理自我调教收益和反馈。
            yield return new Toil
            {
                initAction = CompleteSelfTraining,
                defaultCompleteMode = ToilCompleteMode.Instant
            };
#if SSC_RW17
            // 只有外围 SetupToils 枚举、Dispose 均正常完成后，才会接受这里的原周期恢复候选。
            if (resuming && IsCurrentJob) SSCOrgasmAdvanceRuntime.MarkRestored(this);
#endif
        }

        /// <summary>沿既有手动／自动交互选择生成RJW场景，不因敏感度或读档恢复绕过原许可。</summary>
        private bool TryPrepareInteraction(out string reason)
        {
            reason = null;
            if (manualInteraction == null)
                manualInteraction = pawn.TryGetComp<CompSexSlaveTraining>()?.TakeManualSelfTraining(job);
            if (manualInteraction != null)
            {
                if (!SSCSelfTrainingInteractions.TryBuild(pawn, manualInteraction, out SexProps selected))
                {
                    reason = "SSC_SelfTraining_InteractionChanged".Translate();
                    return false;
                }
                Sexprops = selected;
            }
            else if (Sexprops == null)
                Sexprops = SexUtility.SelectSextype(pawn, pawn, false, false);
            if (!HasValidInteraction())
            {
                reason = "SSC_SelfTraining_NoInteraction".Translate();
                return false;
            }
            Sexprops.isRevese = Sexprops.interaction.HasInteractionTag(SexInteractionTag.Reverse);
            return true;
        }

        /// <summary>确认当前仍是本人自慰且解析Def一致；读档或改档不能使旧交互跨上下文复用。</summary>
        private bool HasValidInteraction()
        {
            return Sexprops?.pawn == pawn && Sexprops.partner == pawn && Sexprops.dictionaryKey != null &&
                Sexprops.sexType == xxx.rjwSextype.Masturbation &&
                Sexprops.resolved?.Interaction?.Def == Sexprops.dictionaryKey;
        }

        /// <summary>只有原场景完整结束才一次性认领自我调教收益；敏感度不增加完成次数。</summary>
        private void CompleteSelfTraining()
        {
            if (!IsCurrentJob || ticks_left > 0 || !HasValidInteraction() ||
                !SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.SelfTraining) ||
                !SSCSelfTrainingEligibility.IsEligible(pawn) || snapshot?.Captured != true) return;
            Need_Corruption need = pawn.needs?.TryGetNeed<Need_Corruption>();
            if (need == null || !snapshot.TryClaimCompletion()) return;
            float before = need.CurLevel;
            float stageCap = TrainingOutcomeUtility.GetChainCap(pawn);
            float gain = SSCSelfTrainingUtility.CalculateGrantedGain(snapshot.CorruptionGain,
                before, stageCap);
            if (gain > 0f) CorruptionUtility.AddCorruption(pawn, gain);
            gain = Math.Max(0f, need.CurLevel - before);
            SSCSelfTrainingFeedback.OnCompleted(pawn, snapshot);
            SSCLog.Important($"[SSC SelfTraining] Completed: pawn={pawn.LabelShort}, interaction={Sexprops.dictionaryKey?.defName}, " +
                $"score={snapshot.Score:F2}, sceneCorruption={snapshot.CorruptionAtStart:F4}, " +
                $"sceneOpinion={snapshot.RawOpinionAtStart}, planned={snapshot.CorruptionGain:F4}, " +
                $"before={before:F4}, stage={SSCIdentityUtility.GetSexSlaveStage(pawn)}, cap={stageCap:F4}, " +
                $"remaining={Math.Max(0f, stageCap - before):F4}, gain={gain:F4}, after={need.CurLevel:F4}");
        }
    }
}
