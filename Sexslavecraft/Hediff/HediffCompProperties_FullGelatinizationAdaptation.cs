using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_FullGelatinizationAdaptation : HediffCompProperties
    {
        public int scanInterval = 600;
        public FloatRange baseGainRange = new FloatRange(0.01f, 0.04f);
        public FloatRange guaranteedGainRange = new FloatRange(0.30f, 0.45f);

        public HediffCompProperties_FullGelatinizationAdaptation()
        {
            compClass = typeof(HediffComp_FullGelatinizationAdaptation);
        }
    }
}
