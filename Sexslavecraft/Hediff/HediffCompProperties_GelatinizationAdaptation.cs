using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_GelatinizationAdaptation : HediffCompProperties
    {
        public float baseAdaptChance = 0.5f;
        
        public FloatRange adaptGainRange = new FloatRange(0.005f, 0.01f);
        
        public float corruptionBonusMultiplier = 0.4f;
        
        public int scanInterval = 600;
        
        public HediffCompProperties_GelatinizationAdaptation()
        {
            compClass = typeof(HediffComp_GelatinizationAdaptation);
        }
    }
}
