using System.Collections.Generic;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>右键与任务开始共用 RJW 的单人交互解析，身体条件变化时不改选其他部位。</summary>
    internal static class SSCSelfTrainingInteractions
    {
        internal static IEnumerable<InteractionDef> Available(Pawn pawn)
        {
            if (pawn == null) yield break;
            foreach (InteractionDef def in SexUtility.SexInteractions)
                if (TryBuild(pawn, def, out _)) yield return def;
        }

        internal static bool TryBuild(Pawn pawn, InteractionDef def, out SexProps props)
        {
            props = null;
            if (pawn == null || def?.GetModExtension<SexInteractionExtension>() == null) return false;
            var interaction = new SexInteraction(def);
            if (interaction.Sextype != xxx.rjwSextype.Masturbation) return false;
            var selected = new SexProps(pawn, pawn) { interaction = interaction, canBeGuilty = false };
            SexInteractionResolved resolved = SexInteractionHelper.ResolveInteraction(selected);
            if (resolved == null || resolved.Interaction?.Def != def) return false;
            selected.resolved = resolved;
            props = selected;
            return true;
        }
    }
}
