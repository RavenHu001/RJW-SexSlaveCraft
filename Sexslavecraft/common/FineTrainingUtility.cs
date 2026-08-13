using System.Collections.Generic;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    public enum FineBodyPart
    {
        Horn, Tail, Ear, Scale, Leg, Chest, Torso, Vagina, Foot, Wing, Mouth
    }

    public struct FinePartData : IExposable
    {
        public float sensitivity;
        public float progress;
        public float discoveredSensitivity;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sensitivity, "sensitivity", 0f);
            Scribe_Values.Look(ref progress, "progress", 0f);
            Scribe_Values.Look(ref discoveredSensitivity, "discoveredSensitivity", 0f);
        }
    }

    public static class FineTrainingUtility
    {
        public static bool HasBodyPart(Pawn pawn, FineBodyPart part) => false;
        public static List<FineBodyPart> GetAvailableParts(Pawn pawn) => new List<FineBodyPart>();
        public static float GenerateSensitivity() => 0f;
        public static float GetSensitivityFactor(float sensitivity) => 0f;
        public static string GetSensitivityLabel(float discoveredSensitivity) => "";
        public static List<FineBodyPart> GetPartsForSexType(xxx.rjwSextype sexType) => new List<FineBodyPart>();
        public static float CalculateFineTrainingBonus(CompSexSlaveTraining comp, xxx.rjwSextype sexType) => 0f;
        public static void AdvanceFineTrainingProgress(CompSexSlaveTraining comp, xxx.rjwSextype sexType, float score) { }
        public static void DiscoverSensitivity(ref FinePartData data, float score) { }
    }
}
