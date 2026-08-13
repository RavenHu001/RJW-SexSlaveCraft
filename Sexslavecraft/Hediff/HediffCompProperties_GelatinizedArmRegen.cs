using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_GelatinizedArmRegen : HediffCompProperties
    {
        public int healIntervalTicks = 600;
        public float healAmount = 0.4f;
        public bool healAllInjuries = false;

        public HediffCompProperties_GelatinizedArmRegen()
        {
            compClass = typeof(HediffComp_GelatinizedArmRegen);
        }
    }
}
