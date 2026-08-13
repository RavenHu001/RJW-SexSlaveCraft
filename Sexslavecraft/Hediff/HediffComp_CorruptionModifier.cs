using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_CorruptionModifier : HediffCompProperties
    {
        public float decayMultiplier = 1f;
        public float minCorruptionLevel = 0f;

        public HediffCompProperties_CorruptionModifier()
        {
            this.compClass = typeof(HediffComp_CorruptionModifier);
        }
    }

    public class HediffComp_CorruptionModifier : HediffComp, ICorruptionModifierSource
    {
        public HediffCompProperties_CorruptionModifier Props => (HediffCompProperties_CorruptionModifier)props;

        public float GetDecayMultiplier() => Props.decayMultiplier;
        public float GetMinCorruption() => Props.minCorruptionLevel;
    }
}
