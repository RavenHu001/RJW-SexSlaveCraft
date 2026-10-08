using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Minimal host for the inherited RimWorld quality/data APIs, checked against local 1.6 IL.
// The production collector runs in tick cases; map geometry is an explicit engine boundary.
// Direct-data cases model restored attendance entries; actual Scribe and Unity are not executed.
namespace Verse
{
    public class Thing
    {
        public IntVec3 Position;
        public Map Map;
        public Map MapHeld => Map;
    }
    public readonly struct IntVec3
    {
        public readonly int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public bool InHorDistOf(IntVec3 other, float radius)
            => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z) <= radius * radius;
    }
    public readonly struct CurvePoint
    {
        public readonly float x, y;
        public CurvePoint(float x, float y) { this.x = x; this.y = y; }
    }
    public class SimpleCurve
    {
        public List<CurvePoint> Points;
        public SimpleCurve(IEnumerable<CurvePoint> points) { Points = points.ToList(); }
        public float Evaluate(float x)
        {
            if (x <= Points[0].x) return Points[0].y;
            for (int i = 1; i < Points.Count; i++)
                if (x <= Points[i].x)
                {
                    var a = Points[i - 1]; var b = Points[i];
                    return a.y + (b.y - a.y) * (x - a.x) / (b.x - a.x);
                }
            return Points[Points.Count - 1].y;
        }
    }
    public static class AudienceText
    {
        public static string CapitalizeFirst(this string text) => text;
    }
}
namespace RimWorld
{
    public static class DutyDefOf { public static Verse.AI.DutyDef Spectate = new Verse.AI.DutyDef(); }
    public static class GatheringsUtility
    {
        public static Func<Verse.IntVec3, Verse.IntVec3, Verse.Map, bool> Area = (cell, spot, map) => true;
        public static bool InGatheringArea(Verse.IntVec3 cell, Verse.IntVec3 spot, Verse.Map map) => Area(cell, spot, map);
    }
    public class QualityFactor
    {
        public string label, count, qualityChange;
        public float quality, priority;
        public bool positive;
    }
    public class RitualOutcomeComp_Data { }
    public class RitualOutcomeComp_DataThingPresence : RitualOutcomeComp_Data
    {
        public Dictionary<Verse.Thing, float> presentForTicks = new Dictionary<Verse.Thing, float>();
        public void Reset() => presentForTicks.Clear();
    }
    public abstract class RitualOutcomeComp_Quality
    {
        public string label;
        public Verse.SimpleCurve curve;
        public float MaxValue => curve.Points[curve.Points.Count - 1].x;
        public abstract float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data);
        public virtual RitualOutcomeComp_Data MakeData() => null;
        public virtual void Tick(LordJob_Ritual ritual, RitualOutcomeComp_Data data, float progressPerTick) { }
        public float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => curve.Evaluate(Count(ritual, data));
        public string GetDesc(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
            => Count(ritual, data) + " / " + MaxValue + " " + label + ": " + QualityOffset(ritual, data).ToString("P0", CultureInfo.InvariantCulture);
        public string ExpectedOffsetDesc(bool positive, float quality) => quality.ToString("P0", CultureInfo.InvariantCulture);
        public virtual QualityFactor GetQualityFactor(Precept_Ritual ritual, Verse.TargetInfo target,
            RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data) => null;
    }
    public class RitualOutcomeComp_ParticipantCount : RitualOutcomeComp_Quality
    {
        public override RitualOutcomeComp_Data MakeData() => new RitualOutcomeComp_DataThingPresence();
        public override void Tick(LordJob_Ritual ritual, RitualOutcomeComp_Data data, float progressPerTick)
        {
            var presence = (RitualOutcomeComp_DataThingPresence)data;
            foreach (var pawn in ritual.PawnsToCountTowardsPresence)
            {
                var role = ritual.assignments.RoleForPawn(pawn);
                if (role != null && !role.countsAsParticipant) continue;
                if (!GatheringsUtility.InGatheringArea(pawn.Position, ritual.Spot, pawn.MapHeld)) continue;
                presence.presentForTicks.TryGetValue(pawn, out var ticks);
                presence.presentForTicks[pawn] = ticks + progressPerTick;
            }
        }
        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            // Original Count uses the configured duration whenever it is nonzero.
            float duration = ritual.DurationTicks != 0 ? ritual.DurationTicks : ritual.TicksPassedWithProgress;
            return ((RitualOutcomeComp_DataThingPresence)data).presentForTicks.Count(p => p.Value >= duration / 2f);
        }
    }
}
namespace Verse.AI
{
    public class DutyDef { }
    public class PawnDuty { public DutyDef def; public Verse.LocalTargetInfo focus; }
    public class Pawn_MindState { public PawnDuty duty; }
}
