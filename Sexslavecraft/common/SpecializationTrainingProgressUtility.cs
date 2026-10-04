using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>完整日常调教或整场仪式领取一次结算资格后，发放当前方向的基础受训经验。</summary>
    /// <remarks>事件去重由 Job 或仪式状态负责；特色经验仍由各自的事件入口处理。</remarks>
    public static class SpecializationTrainingProgressUtility
    {
        // 仪式原有的结果分数与日常评分量级相近；部位经验使用的 quality * 1000
        // 是另一种量纲，不能传给特化公式（否则所有正常仪式都会触及单次上限）。
        public const float RitualScorePerQuality = 75f;

        public static float RitualScore(float quality)
        {
            return CombatantSpecializationProgressUtility.IsPositiveFinite(quality)
                ? quality * RitualScorePerQuality : 0f;
        }

        public static float NotifyTrainingCompleted(Pawn trainer, Pawn receiver, float score)
        {
            if (trainer == null || receiver == null || trainer == receiver ||
                trainer.Dead || trainer.Destroyed || receiver.Dead || receiver.Destroyed) return 0f;

            // 当前沿用战斗员已经验证的公式和实际绑定主人倍率。选择方向的研究
            // 只约束选择入口，不在既有培养进行时重新检查。
            bool byBoundMaster = SSCBondUtility.GetBoundMaster(receiver) == trainer;
            float amount = CombatantSpecializationProgressUtility.TrainingProgress(score, byBoundMaster);
            return TryGainProgress(receiver, amount);
        }

        /// <summary>只写当前培养方向；完成、终极化、失格和坏数值均不发奖。</summary>
        public static float TryGainProgress(Pawn receiver, float amount)
        {
            if (receiver == null || receiver.Dead || receiver.Destroyed ||
                !CombatantSpecializationProgressUtility.IsPositiveFinite(amount) ||
                !SSCIdentityUtility.IsSexSlave(receiver)) return 0f;

            CompSexSlaveTraining comp = receiver.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return 0f;
            SexSlaveSpecializationType type = comp.specializationType;
            if (!CanReceiveTraining(receiver, type)) return 0f;

            // 旧档或外部写入的普通标记先并入组件，避免这次奖励把旧进度
            // 误算为新增长量，或在已有普通完成状态上再次发奖。
            switch (type)
            {
                case SexSlaveSpecializationType.Bus:
                    BusSpecializationUtility.EnsureBusHediffFromSpecialization(receiver);
                    break;
                case SexSlaveSpecializationType.Cow:
                    BusSpecializationUtility.EnsureCowHediffFromSpecialization(receiver);
                    break;
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    PetSpecializationUtility.EnsurePetHediffFromSpecialization(receiver);
                    break;
            }
            float before = CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress);
            comp.specializationProgress = before;
            if (before >= CompSexSlaveTraining.SpecializationCompletionProgress) return 0f;

            switch (type)
            {
                case SexSlaveSpecializationType.Combatant:
                    return CombatantSpecializationProgressUtility.TryGainProgress(receiver, amount);
                case SexSlaveSpecializationType.TrainerOfficer:
                    return TrainerSpecializationProgressUtility.TryGainProgress(receiver, amount);
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    PetSpecializationUtility.TryGainPetProgress(receiver, type, amount);
                    break;
                case SexSlaveSpecializationType.Bus:
                    comp.AddSpecializationProgress(amount);
                    BusSpecializationUtility.EnsureBusHediffFromSpecialization(receiver);
                    if (before < 0.20f && comp.specializationProgress >= 0.20f)
                        Messages.Message(Strings.Message_BusSpecializationUnlocked(receiver.LabelShort),
                            receiver, MessageTypeDefOf.PositiveEvent, false);
                    break;
                case SexSlaveSpecializationType.Cow:
                    comp.AddSpecializationProgress(amount);
                    BusSpecializationUtility.EnsureCowHediffFromSpecialization(receiver);
                    if (before < 0.20f && comp.specializationProgress >= 0.20f)
                        Messages.Message(Strings.Message_CowSpecializationUnlocked(receiver.LabelShort),
                            receiver, MessageTypeDefOf.PositiveEvent, false);
                    break;
            }
            return Math.Max(0f, comp.specializationProgress - before);
        }

        private static bool CanReceiveTraining(Pawn receiver, SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.Bus:
                    return !BusSpecializationUtility.HasFinalBusState(receiver);
                case SexSlaveSpecializationType.Cow:
                    return !BusSpecializationUtility.HasFinalCowState(receiver);
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    return PetSpecializationUtility.CanTrainPetSpecialization(receiver, type);
                case SexSlaveSpecializationType.TrainerOfficer:
                    return TrainerSpecializationUtility.MeetsContinuousConditions(receiver, out _)
                        && !TrainerSpecializationUtility.HasFinalRecord(receiver);
                case SexSlaveSpecializationType.Combatant:
                    return !CombatantSpecializationUtility.HasFinalState(receiver);
                default:
                    return false;
            }
        }
    }
}
