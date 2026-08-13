using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    public class HediffComp_FullGelatinizedRegen : HediffComp
    {
        private int ticksSinceLastHeal;

        public HediffCompProperties_FullGelatinizedRegen Props => (HediffCompProperties_FullGelatinizedRegen)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksSinceLastHeal, "ticksSinceLastHeal", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (parent?.pawn == null || parent.pawn.Dead) return;

            ticksSinceLastHeal++;
            if (ticksSinceLastHeal < Props.healIntervalTicks) return;
            ticksSinceLastHeal = 0;

            List<Hediff_Injury> injuries = parent.pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(injury => injury.CanHealNaturally() && injury.Severity > 0f)
                .ToList();

            if (injuries.Count == 0) return;

            for (int i = 0; i < injuries.Count; i++)
            {
                injuries[i].Heal(Props.healAmount);
            }
        }
    }
}
