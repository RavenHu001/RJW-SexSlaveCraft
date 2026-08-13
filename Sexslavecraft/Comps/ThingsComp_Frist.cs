using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class CompApparel_CorruptionModifierWithTraitFrist : CompApparel_CorruptionModifier
    {

        // 支持多 Trait 检查，覆盖原来的 MinCorruptionLevel
        public new float MinCorruptionLevel
        {
            get
            {
                var apparel = parent as Apparel;
                if (apparel == null)
                    return 0f;

                Pawn wearer = apparel.Wearer;
                if (wearer == null)
                    return 0f;

                // The apparel floor is enabled by the authoritative chain health stage.
                    if (SSCIdentityUtility.GetSexSlaveStage(wearer) > 0)
                    {
                    return Props.minCorruptionLevel;
                    }
                    else 
                        {
                        return 0f;
                        }
            }
        }
    }
    public class CompProperties_CorruptionModifierWithTraitFrist : CompProperties
    {
        public float corruptionMultiplier = 1f;
        public float minCorruptionLevel = 0f;

        public CompProperties_CorruptionModifierWithTraitFrist()
        {
            this.compClass = typeof(CompApparel_CorruptionModifierWithTraitFrist);
        }
    }
}
