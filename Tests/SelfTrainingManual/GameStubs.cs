using System.Collections.Generic;

namespace Verse
{
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, ResolvingCrossRefs, PostLoadInit }
    public enum LookMode { Value, Def }
    public static partial class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> Data = new();
    }
    public static class Scribe_Collections
    {
        public static void Look<K, V>(ref Dictionary<K, V> value, string key, LookMode keyMode, LookMode valueMode)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = new Dictionary<K, V>(value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.Data.TryGetValue(key, out object saved) ? new Dictionary<K, V>((Dictionary<K, V>)saved) : null;
        }
    }
    public partial class Pawn
    {
        public Verse.AI.Job CurJob;
        public Verse.AI.Pawn_JobTracker jobs = new();
        public readonly HashSet<RimWorld.InteractionDef> Available = new();
    }
}
namespace Verse.AI
{
    public partial class Job { public int loadID; }
    public class QueuedJob { public Job job; }
    public partial class Pawn_JobTracker { public List<QueuedJob> jobQueue = new(); }
}
namespace RimWorld
{
    public partial class InteractionDef
    {
        public rjw.Modules.Interactions.SexInteractionExtension Extension;
        public T GetModExtension<T>() where T : class => Extension as T;
    }
}
namespace rjw
{
    public static partial class xxx { public enum rjwSextype { None, Masturbation, Vaginal } }
    public static partial class SexUtility { public static readonly List<RimWorld.InteractionDef> SexInteractions = new(); }
    public partial class SexProps
    {
        public Verse.Pawn pawn, partner;
        public bool canBeGuilty = true;
        public SexProps(Verse.Pawn pawn, Verse.Pawn partner) { this.pawn = pawn; this.partner = partner; }
    }
}
namespace rjw.Modules.Interactions
{
    public class SexInteractionExtension { public rjw.xxx.rjwSextype Type; public bool Reverse; }
    public partial class SexInteraction
    {
        public RimWorld.InteractionDef Def;
        public SexInteractionExtension Extension;
        public rjw.xxx.rjwSextype Sextype => Extension.Type;
        public SexInteraction(RimWorld.InteractionDef def) { Def = def; Extension = def.Extension; }
    }
    public class SexInteractionResolved { public SexInteraction Interaction; }
    public static class SexInteractionHelper
    {
        public static SexInteractionResolved ResolveInteraction(rjw.SexProps props)
            => props.pawn.Available.Contains(props.interaction.Def)
                ? new SexInteractionResolved { Interaction = props.interaction } : null;
    }
}
namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public void ExposeForTest() => ExposeManualSelfTraining();
        public void ReconcileForTest(Verse.Pawn pawn) => ReconcileManualSelfTraining(pawn);
    }
}
