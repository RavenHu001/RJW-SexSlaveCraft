using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.AddTo),
        new[] { typeof(RecordDef), typeof(float) })]
    internal static class Patch_FoxResearchProgress
    {
        private const double ProgressPerResearchPoint = 0.000025d;

        internal static void Postfix(Pawn_RecordsTracker __instance, RecordDef def, float value)
        {
            if (def != RecordDefOf.ResearchPointsResearched || DebugSettings.fastResearch ||
                !float.IsFinite(value) || value <= 0f) return;

            Pawn pawn = __instance.pawn;
            if (pawn?.jobs?.curDriver is not JobDriver_Research || pawn.Dead || pawn.Destroyed ||
                !SSCIdentityUtility.IsSexSlave(pawn)) return;

            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            const SexSlaveSpecializationType type = SexSlaveSpecializationType.PetFox;
            if (comp == null || comp.specializationType != type ||
                comp.specializationProgress >= CompSexSlaveTraining.SpecializationCompletionProgress ||
                !PetSpecializationUtility.CanTrainPetSpecialization(pawn, type)) return;

            // 累积原版已入账的小数，避免每次微量收益直接加到 float 进度时丢失。
            double total = CompSexSlaveTraining.NormalizeFoxResearchPointRemainder(comp.foxResearchPointRemainder) + value;
            double wholePoints = Math.Floor(total);
            comp.foxResearchPointRemainder = total - wholePoints;
            if (wholePoints > 0d)
                PetSpecializationUtility.TryGainPetProgress(pawn, type,
                    (float)Math.Min(1d, wholePoints * ProgressPerResearchPoint));
        }
    }

    [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment))]
    internal static class Patch_FoxTendingProgress
    {
        internal static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
        {
            if (def != RecordDefOf.TimesTendedOther) return;

            Pawn doctor = __instance.pawn;
            if (doctor?.jobs?.curDriver is not JobDriver_TendPatient || doctor.Dead || doctor.Destroyed ||
                !SSCIdentityUtility.IsSexSlave(doctor)) return;

            // 原版在一次 DoTend 后记一次，包括自疗。目标 A 是该护理任务的患者。
            Pawn patient = doctor.CurJob?.targetA.Pawn;
            if (patient == null || patient == doctor || patient.Dead || patient.Destroyed ||
                !patient.RaceProps.Humanlike) return;

            PetSpecializationUtility.TryGainPetProgress(doctor, SexSlaveSpecializationType.PetFox, 0.001f);
        }
    }
}
