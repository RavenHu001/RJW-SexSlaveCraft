using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_FullGelatinizedRegen : HediffCompProperties
    {
        public int healIntervalTicks = 300;
        public float healAmount = 1.2f;

        public HediffCompProperties_FullGelatinizedRegen()
        {
            compClass = typeof(HediffComp_FullGelatinizedRegen);
        }
    }
}
