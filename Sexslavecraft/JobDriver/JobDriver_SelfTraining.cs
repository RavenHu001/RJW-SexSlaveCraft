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
                reason = "Pawn cannot start a solo scene. / 角色当前无法开始单人场景。";
                return false;
            }
            SSCRestrictionDecision permission = SSCRestrictionPolicy.Evaluate(
                new SSCRestrictionRequest(actor, actor, SSCInteractionKind.SelfTraining, true));
            if (!permission.Allowed)
            {
                reason = "Self-training permission or eligibility denied: " + permission.Reason +
                    " / 自我调教许可或资格不满足。";
                return false;
            }
            if (!xxx.can_masturbate(actor))
            {
                reason = "RJW masturbation ability is blocked. / RJW 自慰能力被阻断。";
                return false;
            }
            if (!destination.IsValid || !destination.InBounds(actor.Map) ||
                !actor.CanReach(destination, PathEndMode.OnCell, Danger.Deadly))
            {
                reason = "Destination is unavailable. / 目标地点不可达。";
                return false;
            }
            if (actor.needs?.TryGetNeed<Need_Corruption>() == null)
            {
                reason = "Corruption need is unavailable. / 角色没有恶堕需求。";
                return false;
            }
            return true;
        }

        private bool IsActiveDriver => pawn?.jobs?.curDriver == this;
        private bool IsCurrentJob => IsActiveDriver && job?.GetTarget(iTarget).Pawn == pawn;

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

            // 基类的完成步骤先执行 RJW 的 Aftersex；本步骤是阶段 5 记忆的统一结算入口。
            yield return new Toil
            {
                initAction = CompleteSelfTraining,
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

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

        private bool HasValidInteraction()
        {
            return Sexprops?.pawn == pawn && Sexprops.partner == pawn && Sexprops.dictionaryKey != null &&
                Sexprops.sexType == xxx.rjwSextype.Masturbation &&
                Sexprops.resolved?.Interaction?.Def == Sexprops.dictionaryKey;
        }

        private void CompleteSelfTraining()
        {
            if (!IsCurrentJob || ticks_left > 0 || !HasValidInteraction() ||
                !SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.SelfTraining) ||
                !SSCSelfTrainingEligibility.IsEligible(pawn) || snapshot?.Captured != true) return;
            Need_Corruption need = pawn.needs?.TryGetNeed<Need_Corruption>();
            if (need == null || !snapshot.TryClaimCompletion()) return;
            float before = need.CurLevel;
            float gain = SSCSelfTrainingUtility.CalculateGrantedGain(snapshot.CorruptionGain,
                before, TrainingOutcomeUtility.GetChainCap(pawn));
            if (gain > 0f) CorruptionUtility.AddCorruption(pawn, gain);
            gain = Math.Max(0f, need.CurLevel - before);
            SSCSelfTrainingFeedback.OnCompleted(pawn, snapshot, gain);
            SSCLog.Important($"[SSC SelfTraining] Completed: pawn={pawn.LabelShort}, interaction={Sexprops.dictionaryKey?.defName}, " +
                $"score={snapshot.Score:F2}, gain={gain:F4}");
        }
    }
}
