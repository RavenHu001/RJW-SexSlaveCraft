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
            if (comp.specializationType == SexSlaveSpecializationType.PetCat ||
                comp.specializationType == SexSlaveSpecializationType.PetRabbit)
            {
                label += " " + Strings.ITab_SpecializationUnfinishedSuffix;
            }

            return label;
        }

        /// <summary>生成当前培养方向的进度文字，供绘制与无界面回归共用。</summary>
        private static string GetSpecializationProgressText(Pawn pawn, CompSexSlaveTraining comp)
        {
            // 进度与按钮采用同一个当前方向；None 不能因为其他完成记录而显示已完成。
            // 终极记录仍由各自健康状态及训导官独立状态行展示，不在这里改写其效果。
            bool displayedTypeFinalized = IsTypeFinalized(pawn, comp.specializationType);
            string progressText = displayedTypeFinalized
                ? Strings.ITab_SpecializationComplete
                : comp.specializationProgress.ToStringPercent();
            return progressText;
        }
    }
}
