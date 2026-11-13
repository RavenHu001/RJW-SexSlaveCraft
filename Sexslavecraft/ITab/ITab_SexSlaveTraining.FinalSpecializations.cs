// AI 辅助生成：调教窗口的终极成果只读数据。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        private static readonly SexSlaveSpecializationType[] FinalSpecializationDisplayOrder =
        {
            SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.Cow,
            SexSlaveSpecializationType.Combatant, SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit, SexSlaveSpecializationType.PetFox,
            SexSlaveSpecializationType.TrainerOfficer, SexSlaveSpecializationType.Fertility
        };

        /// <summary>终极成果卡直接取得现有生效状态，悬浮文本继续提供具体原因。</summary>
        internal static List<(SexSlaveSpecializationType Type, string Name, string Status, bool Active, string Tip)>
            GetFinalSpecializationCards(Pawn pawn)
        {
            var cards = new List<(SexSlaveSpecializationType, string, string, bool, string)>();
            foreach (SexSlaveSpecializationType type in FinalSpecializationDisplayOrder)
            {
                if (!IsTypeFinalized(pawn, type)) continue;
                // 仅复用已有生效／禁用规则；列表不为其他方向增设新的禁用门槛。
                bool active = true;
                string tip = null;
                if (type == SexSlaveSpecializationType.TrainerOfficer)
                {
                    active = TrainerSpecializationUtility.HasActiveFinalEffect(pawn);
                    tip = active ? "SSC_TrainerIdentity_RestrictionTip".Translate().ToString()
                        : GetTrainerFinalDisabledReason(pawn);
                }
                else if (type == SexSlaveSpecializationType.Fertility)
                {
                    active = FertilitySpecializationUtility.MeetsConditions(pawn, out string reason);
                    tip = active ? "SSC_Fertility_Tip".Translate().ToString() : reason;
                }
                // 失格的成果继续保留卡片，状态与悬浮原因都使用本次查询的结果。
                string status = (active ? "SSC_SpecializationStatus_Finalized"
                    : "SSC_SpecializationStatus_FinalDisabled").Translate();
                cards.Add((type, GetSpecializationName(type), status, active,
                    string.IsNullOrEmpty(tip) ? status : status + "\n\n" + tip));
            }
            return cards;
        }
    }
}
