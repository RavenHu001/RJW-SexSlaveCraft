using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_GelatinizationSeverity : HediffCompProperties
    {
        public FloatRange severityGainRange = new FloatRange(0.003f, 0.006f);
        
        public int scanInterval = 600;
        
        public HediffCompProperties_GelatinizationSeverity()
        {
            compClass = typeof(HediffComp_GelatinizationSeverity);
        }
    }
}
