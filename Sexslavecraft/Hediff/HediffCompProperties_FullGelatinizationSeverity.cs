using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_FullGelatinizationSeverity : HediffCompProperties
    {
        public int scanInterval = 600;
        public FloatRange severityGainRange = new FloatRange(0.02f, 0.08f);

        public HediffCompProperties_FullGelatinizationSeverity()
        {
            compClass = typeof(HediffComp_FullGelatinizationSeverity);
        }
    }
}
