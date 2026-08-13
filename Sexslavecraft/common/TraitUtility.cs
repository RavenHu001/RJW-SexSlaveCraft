using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class TraitUtility
    {
        //给指定 Pawn 添加或更新带等级的 Trait（通过名字）。 Add or update a leveled Trait to a specified Pawn (by defname).
        public static bool AddOrUpdateTrait(Pawn pawn, string traitDefName, int degree)
        {
            if (pawn?.story?.traits == null) return false;

            TraitDef def = TraitDef.Named(traitDefName);
            if (def == null)
            {
                Log.Error($"TraitDef '{traitDefName}' 未找到！");
                return false;
            }

            return AddOrUpdateTrait(pawn, def, degree);
        }

        // 给指定 Pawn 添加或更新带等级的 Trait（直接传 TraitDef）。  Add or update a leveled Trait to a specified Pawn (by directly passing the TraitDef).
        public static bool AddOrUpdateTrait(Pawn pawn, TraitDef traitDef, int degree)
        {
            if (pawn?.story?.traits?.allTraits == null || traitDef == null) return false;

            List<Trait> existingTraits = pawn.story.traits.allTraits
                .Where(trait => trait?.def == traitDef)
                .ToList();
            if (existingTraits.Count == 1 && existingTraits[0].Degree == degree)
            {
                return true;
            }

            foreach (Trait existingTrait in existingTraits)
            {
                if (!existingTrait.Suppressed && pawn.story.traits.GetTrait(traitDef) != null)
                {
                    pawn.story.traits.RemoveTrait(existingTrait);
                }
                else
                {
                    // RimWorld's RemoveTrait refuses suppressed traits, so remove stale mirrors directly.
                    pawn.story.traits.allTraits.Remove(existingTrait);
                }
            }

            Trait newTrait = new Trait(traitDef, degree);
            pawn.story.traits.GainTrait(newTrait);
            bool applied = pawn.story.traits.allTraits.Any(trait =>
                trait?.def == traitDef && trait.Degree == degree);
            if (applied)
            {
                Log.Message($"Pawn {pawn.Name} 获得 Trait {traitDef.defName}，等级 {degree}.");
            }
            else
            {
                Log.Error($"Pawn {pawn.Name} 未能获得 Trait {traitDef.defName}，目标等级 {degree}.");
            }

            return applied;
        }
        public static bool TraitCheck(Pawn pawn, string traitDefName, int? degree = null)
        {
            if (pawn?.story?.traits == null)
                return false;

            TraitDef def = TraitDef.Named(traitDefName);
            if (def == null)
                return false;

            foreach (Trait t in pawn.story.traits.allTraits)
            {
                if (t.def == def)
                {
                    if (degree.HasValue)
                        return t.Degree == degree.Value;

                    return true;
                }
            }
            return false;
        }
    }
}
