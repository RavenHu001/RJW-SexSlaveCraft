using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Minimal host for the inherited RimWorld quality/data APIs, checked against local 1.6 IL.
// Attendance entries model data supplied by vanilla Tick/Scribe; neither runs in this suite.
namespace Verse
{
    public class Thing { }
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
        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            // Original Count uses the configured duration whenever it is nonzero.
            float duration = ritual.DurationTicks != 0 ? ritual.DurationTicks : ritual.TicksPassedWithProgress;
            return ((RitualOutcomeComp_DataThingPresence)data).presentForTicks.Count(p => p.Value >= duration / 2f);
        }
    }
}
