// AI 辅助生成
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>菜单捕获原组件与人格，提交时复查，不使旧菜单授权换入的人格。</summary>
        private static FloatMenuOption BuildFertilitySpecializationOption(Pawn pawn, CompSexSlaveTraining comp)
        {
            bool available = FertilitySpecializationUtility.CanSelect(pawn, out string reason);
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            string label = "SSC_Fertility_Select".Translate();
            if (FertilitySpecializationUtility.HasFinalState(pawn)) label += " " + Strings.ITab_SpecializationFinalizedSuffix;
            else if (!available && !string.IsNullOrEmpty(reason)) label += " (" + reason + ")";
            return new FloatMenuOption(label, available ? (Action)(() =>
            {
                if (pawn.TryGetComp<CompSexSlaveTraining>() != comp || !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) ||
                    !FertilitySpecializationUtility.CanSelect(pawn, out _)) return;
                comp.SetSpecialization(SexSlaveSpecializationType.Fertility);
            }) : null);
        }
    }
}
