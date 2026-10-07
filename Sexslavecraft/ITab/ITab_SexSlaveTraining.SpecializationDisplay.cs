using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>按指定特化方向检查对应终极状态，不让其他方向的完成状态影响结果。</summary>
        private static bool IsTypeFinalized(Pawn pawn, SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.Combatant:
                    return CombatantSpecializationUtility.HasFinalState(pawn);
                case SexSlaveSpecializationType.Bus:
                    return BusSpecializationUtility.HasFinalBusState(pawn);
                case SexSlaveSpecializationType.Cow:
                    return BusSpecializationUtility.HasFinalCowState(pawn);
                case SexSlaveSpecializationType.TrainerOfficer:
                    return TrainerSpecializationUtility.HasFinalRecord(pawn);
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    return PetSpecializationUtility.HasFinalPetState(pawn, type);
                default:
                    return false;
            }
        }

        /// <summary>生成当前培养方向的标签；没有当前方向时始终显示未选择。</summary>
        private static string GetSpecializationLabel(Pawn pawn, CompSexSlaveTraining comp)
        {
            string label;
            switch (comp.specializationType)
            {
                case SexSlaveSpecializationType.Bus:
                    label = Strings.ITab_SpecializationBus;
                    break;
                case SexSlaveSpecializationType.Cow:
                    label = Strings.ITab_SpecializationCow;
                    break;
                case SexSlaveSpecializationType.Combatant:
                    label = Strings.ITab_SpecializationCombatant;
                    break;
                case SexSlaveSpecializationType.TrainerOfficer:
                    label = Strings.ITab_SpecializationTrainerOfficer;
                    break;
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    label = PetSpecializationUtility.GetSpecializationLabel(comp.specializationType);
                    break;
                default:
                    label = Strings.ITab_SpecializationNone;
                    break;
            }

            // 选择按钮只表示当前培养方向。保留的终极健康状态属于已取得的效果，
            // 不能在 None 时拿历史完成方向替代“未选择”，否则玩家清空后看不到变化。
            bool finalized = IsTypeFinalized(pawn, comp.specializationType);
            if (finalized)
            {
                label += " " + Strings.ITab_SpecializationFinalizedSuffix;
            }
            // 猫狗普通培养和终极技能已开放；未开放的兔仍显示未完成。
            if (comp.specializationType == SexSlaveSpecializationType.PetRabbit)
            {
                label += " " + Strings.ITab_SpecializationUnfinishedSuffix;
            }

            return label;
        }

        /// <summary>性奴特化仅向 SSC 性奴显示，不以原版奴隶身份代替。</summary>
        private static bool CanShowSpecializationSection(CompSexSlaveTraining comp)
        {
            return comp != null && comp.pawnIdentity == PawnIdentity.Slave;
        }

        /// <summary>保留其他方向原本来自页签外层的身份与基础研究门槛。</summary>
        private static bool CanShowLegacySpecializationOptions(CompSexSlaveTraining comp)
        {
            return CanShowSpecializationSection(comp) &&
                ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining);
        }

        /// <summary>生成当前培养方向的进度文字，供绘制与无界面回归共用。</summary>
        private static string GetSpecializationProgressText(Pawn pawn, CompSexSlaveTraining comp)
        {
            // 进度与按钮采用同一个当前方向；None 不能因为其他完成记录而显示已完成。
            // 终极记录仍由各自健康状态及训导官独立状态行展示，不在这里改写其效果。
            if (comp.specializationType == SexSlaveSpecializationType.Combatant ||
                comp.specializationType == SexSlaveSpecializationType.PetCat ||
                comp.specializationType == SexSlaveSpecializationType.PetDog)
            {
                if (IsTypeFinalized(pawn, comp.specializationType))
                    return Strings.ITab_SpecializationComplete;
                float progress = CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress);
                // 猫狗与战斗员使用一位小数，避免 99.5% 被整数格式舍入成 100%；
                // 达到公共完成容差时明确显示普通培养完成，不把普通完成写成终极成果。
                return progress >= CompSexSlaveTraining.SpecializationCompletionProgress
                    ? Strings.ITab_SpecializationOrdinaryComplete : progress.ToString("P1");
            }
            bool displayedTypeFinalized = IsTypeFinalized(pawn, comp.specializationType);
            string progressText = displayedTypeFinalized
                ? Strings.ITab_SpecializationComplete
                : comp.specializationProgress.ToStringPercent();
            return progressText;
        }

        /// <summary>普通培养完成后的只读引导；绘制与回归共用，不修改进度、成果或配方资格。</summary>
        private static string GetSpecializationProgressTip(Pawn pawn, CompSexSlaveTraining comp)
        {
            if (comp == null || CompSexSlaveTraining.NormalizeSpecializationProgress(comp.specializationProgress) <
                CompSexSlaveTraining.SpecializationCompletionProgress) return null;

            // 引导只属于当前方向。普通完成仍需提取、加工和植入，不能将历史进度
            // 或其他方向保留的终极成果解释为当前方向已经终极化。
            if (comp.specializationType == SexSlaveSpecializationType.Combatant)
                return IsTypeFinalized(pawn, comp.specializationType)
                    ? null : "SSC_ITab_CombatantFinalizationTip".Translate().ToString();

            // 任意宠物终极成果都会阻止再次进行宠物终极加工；有成果时不提供
            // 无法执行的加工建议。猫狗使用各自的配方与技能说明，兔仍不开放。
            if (PetSpecializationUtility.HasAnyFinalPetState(pawn)) return null;
            switch (comp.specializationType)
            {
                case SexSlaveSpecializationType.PetCat:
                    return "SSC_ITab_PetCatFinalizationTip".Translate().ToString();
                case SexSlaveSpecializationType.PetDog:
                    return "SSC_ITab_PetDogFinalizationTip".Translate().ToString();
                default:
                    return null;
            }
        }
    }
}
