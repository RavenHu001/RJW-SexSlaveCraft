using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    public class HediffCompProperties_GiveAbility : HediffCompProperties
    {
        public AbilityDef abilityDef;
        public float minSeverity = 0.01f; // 默认只要有这个Hediff(0.01)就给技能

        public HediffCompProperties_GiveAbility()
        {
            this.compClass = typeof(HediffComp_GiveAbility);
        }
    }

    public class HediffComp_GiveAbility : HediffComp
    {
        public HediffCompProperties_GiveAbility Props => (HediffCompProperties_GiveAbility)this.props;
        private const int CheckInterval = 60; // 每秒检查一次即可，节省性能

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn == null || parent.pawn.Dead) return;

            if (parent.pawn.IsHashIntervalTick(CheckInterval))
            {
                var tracker = parent.pawn.abilities;
                if (tracker == null || Props.abilityDef == null) return;

                bool hasAbility = tracker.GetAbility(Props.abilityDef) != null;
                bool conditionMet = parent.Severity >= Props.minSeverity;

                // 达到严重度且没技能，则添加
                if (conditionMet && !hasAbility)
                {
                    tracker.GainAbility(Props.abilityDef);
                }
                // 严重度掉回去了且有技能，则没收
                else if (!conditionMet && hasAbility)
                {
                    tracker.RemoveAbility(Props.abilityDef);
                }
            }
        }

        // 状态被治愈/移除时，没收技能
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Props.abilityDef != null && parent.pawn?.abilities != null)
            {
                parent.pawn.abilities.RemoveAbility(Props.abilityDef);
            }
        }
    }
}