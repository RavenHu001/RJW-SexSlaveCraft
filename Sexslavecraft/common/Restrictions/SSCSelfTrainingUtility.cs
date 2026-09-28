using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>自我调教的资格、阶段概率与场景开始时的评分输入；不创建任务或发放收益。</summary>
    public static class SSCSelfTrainingUtility
    {
        public static float GetAutoSelectionChance(Pawn pawn)
        {
            if (!SSCSelfTrainingEligibility.IsEligible(pawn)) return 0f;
            return GetStageChance(SSCIdentityUtility.GetSexSlaveStage(pawn));
        }

        public static float GetStageChance(int stage)
        {
            if (stage <= 1) return 0.30f;
            if (stage == 2) return 0.55f;
            if (stage == 3) return 0.80f;
            return 0.50f;
        }

        /// <summary>主人优先；失效后才选有效指定调教员，不要求同图或在场。</summary>
        public static Pawn GetImaginedPawn(Pawn pawn)
        {
            Pawn master = SSCBondUtility.GetBoundMaster(pawn);
            if (IsValidAssociation(pawn, master)) return master;
            Pawn trainer = TrainerAssignmentUtility.GetActiveAssignedTrainer(pawn);
            return IsValidAssociation(pawn, trainer) ? trainer : null;
        }

        private static bool IsValidAssociation(Pawn pawn, Pawn other)
        {
            return other != null && other != pawn && !other.Dead && !other.Destroyed;
        }

        /// <summary>保留原始负好感供后续记忆使用；评分中的关系项单独钳制。</summary>
        public static SSCSelfTrainingSnapshot CaptureAtSceneStart(Pawn pawn)
        {
            if (!SSCSelfTrainingEligibility.IsEligible(pawn)) return null;
            Pawn imagined = GetImaginedPawn(pawn);
            return new SSCSelfTrainingSnapshot
            {
                ImaginedPawn = imagined,
                CorruptionAtStart = pawn.needs?.TryGetNeed<Need_Corruption>()?.CurLevel ?? 0f,
                RawOpinionAtStart = imagined != null ? pawn.relations?.OpinionOf(imagined) ?? 0 : 0,
                Captured = true
            };
        }

        public static float CalculateScore(float corruption, int rawOpinion)
        {
            float current = Clamp01(corruption);
            float affection = Clamp01(rawOpinion / 100f);
            return 10f + 20f * current + 10f * affection;
        }

        public static float CalculateCorruptionGain(float score)
        {
            float baseGain = Math.Max(0.01f, Math.Min(0.12f, score / 500f + 0.025f));
            return 0.5f * baseGain;
        }

        /// <summary>只发放当前锁链阶段仍容纳的增量；不会通过本任务创建新的阶段推进规则。</summary>
        public static float CalculateGrantedGain(float plannedGain, float currentCorruption, float stageCap)
        {
            return Math.Max(0f, Math.Min(plannedGain, stageCap - currentCorruption));
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    /// <summary>随未来自我调教 JobDriver 保存的本次输入和一次性结算状态。</summary>
    public sealed class SSCSelfTrainingSnapshot : IExposable
    {
        public Pawn ImaginedPawn;
        public float CorruptionAtStart;
        public int RawOpinionAtStart;
        public bool Captured;
        public bool CompletionClaimed;

        public float Score => SSCSelfTrainingUtility.CalculateScore(CorruptionAtStart, RawOpinionAtStart);
        public float CorruptionGain => SSCSelfTrainingUtility.CalculateCorruptionGain(Score);

        /// <summary>仅记录领取资格；阶段 2 的驱动仍需先确认任务正常完成。</summary>
        public bool TryClaimCompletion()
        {
            if (!Captured || CompletionClaimed) return false;
            CompletionClaimed = true;
            return true;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref ImaginedPawn, "sscSelfTrainingImaginedPawn");
            Scribe_Values.Look(ref CorruptionAtStart, "sscSelfTrainingCorruptionAtStart", 0f);
            Scribe_Values.Look(ref RawOpinionAtStart, "sscSelfTrainingRawOpinionAtStart", 0);
            Scribe_Values.Look(ref Captured, "sscSelfTrainingCaptured", false);
            Scribe_Values.Look(ref CompletionClaimed, "sscSelfTrainingCompletionClaimed", false);
        }
    }
}
