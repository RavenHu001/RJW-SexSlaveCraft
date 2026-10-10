using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

// 整场 Apply 链接生产 worker；这些外部接口只供算分、信件和未启用的原版附加效果编译。
// 用例只有一个结果选项且没有原版记忆/观众/附加效果，不模拟这些子系统的业务规则。
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float value) => Math.Abs(value);
    }
}
namespace Verse
{
    public readonly struct FloatRange { public FloatRange(float min, float max) { } }
    public readonly struct TaggedString
    {
        private readonly string value;
        private TaggedString(string value) => this.value = value;
        public static implicit operator TaggedString(string value) => new TaggedString(value);
        public static implicit operator string(TaggedString value) => value.value;
    }
    public static class OutcomeTextInterfaces
    {
        public static string ToStringPercent(this float value) => value.ToString();
        public static string ToStringWithSign(this int value) => value.ToString();
        public static string CapitalizeFirst(this string value) => value;
        public static string Resolve(this string value) => value;
        public static string Formatted(this string value, params object[] args) => value;
        public static string Colorize(this string value, object color) => value;
        public static object Named(this string value, string name) => value;
        public static bool NullOrEmpty(this string value) => string.IsNullOrEmpty(value);
        public static bool NullOrEmpty<T>(this List<T> value) => value == null || value.Count == 0;
        public static T RandomElementByWeight<T>(this List<T> values, Func<T, float> weight) => values.Single();
    }
    public static class ColoredText { public static object TipSectionTitleColor; }
    public static partial class Find { public static LetterStack LetterStack = new LetterStack(); }
    public sealed class LetterStack
    {
        /// <summary>只记录结算信件的实际派发次数，便于检测重复与中途退出。</summary>
        public void ReceiveLetter(string label, string text, object def, LookTargets targets) => TestWorld.RitualLetters++;
    }
    public partial class Pawn
    {
        public object guest;
        public OutcomeNeeds needs = new OutcomeNeeds();
        public MindState mindState;
        public Map MapHeld => Map;
    }
    public sealed class MindState { public Duty duty; }
    public sealed class Duty { public Def def; public object focus; }
    public partial class RelationTracker
    {
        public int OpinionOf(Pawn other) => 0;
        public bool DirectRelationExists(object relation, Pawn other) => false;
        public void AddDirectRelation(object relation, Pawn other) { }
    }
}
namespace RimWorld
{
    public sealed class OutcomeNeeds { public OutcomeMood mood = new OutcomeMood(); }
    public sealed class OutcomeMood { public OutcomeThoughts thoughts = new OutcomeThoughts(); }
    public sealed class OutcomeThoughts { public OutcomeMemories memories = new OutcomeMemories(); }
    public sealed class OutcomeMemories { public void TryGainMemory(Thought_Memory memory) { } }
    public class ThoughtDef { }
    public class Thought_Memory { }
    public class ExpectationDef { }
    public class PreceptDef { }
    public sealed class RitualOutcomeEffectDef
    {
        public bool allowAttachableOutcome, givesDevelopmentPoints;
        public float startingQuality = 0.5f, minQuality, maxQuality = 1f;
        public List<RitualOutcomeComp> comps = new List<RitualOutcomeComp>();
        public List<RitualOutcomePossibility> outcomeChances = new List<RitualOutcomePossibility>();
    }
    public sealed class RitualOutcomePossibility
    {
        public bool Positive;
        public float chance = 1f;
        public List<string> roleIdsNotGainingMemory;
        public ThoughtDef memory;
        public string description = "outcome", label = "outcome";
    }
    public class RitualOutcomeComp
    {
        public bool Applies(LordJob_Ritual job) => true;
        public float QualityOffset(LordJob_Ritual job, object data) => 0f;
        public string GetDesc(LordJob_Ritual job = null, object data = null) => "";
    }
    public class RitualOutcomeComp_Quality : RitualOutcomeComp { }
    public sealed class RitualOutcomeComp_DataThingPresence { public Dictionary<Pawn, float> presentForTicks = new Dictionary<Pawn, float>(); }
    public class RitualOutcomeEffectWorker
    {
        public RitualOutcomeEffectDef def;
        public virtual bool SupportsAttachableOutcomeEffect => false;
        public RitualOutcomeEffectWorker() { }
        public RitualOutcomeEffectWorker(RitualOutcomeEffectDef def) => this.def = def;
        protected object DataForComp(RitualOutcomeComp comp) => null;
        protected Thought_Memory MakeMemory(Pawn pawn, LordJob_Ritual job, ThoughtDef memory) => null;
        public virtual string ExtraAlertParagraph(Precept_Ritual ritual) => "";
        public virtual void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual ritual) { }
    }
    public sealed class RitualRole { public string id; }
    public sealed class OutcomeAssignments
    {
        public List<Pawn> SpectatorsForReading = new List<Pawn>();
        public RitualRole RoleForPawn(Pawn pawn) => null;
    }
    public partial class LordJob_Ritual
    {
        public bool repeatPenalty;
        public int TicksPassedWithProgress;
        public Map Map => lord.Map;
        public IntVec3 Spot;
        public OutcomeAssignments assignments = new OutcomeAssignments();
    }
    public partial class Precept_Ritual
    {
        public int lastFinishedTick = -1;
        public string Label => "ritual";
        public AttachableOutcome attachableOutcomeEffect;
        public RitualOutcomeEffectWorker outcomeEffect;
        public OutcomeIdeo ideo;
    }
    public sealed class AttachableOutcome
    {
        public AttachableWorker Worker = new AttachableWorker();
        public bool AppliesToOutcome(RitualOutcomeEffectDef def, RitualOutcomePossibility outcome) => false;
    }
    public sealed class AttachableWorker
    {
        public void Apply(Dictionary<Pawn, int> presence, LordJob_Ritual job, RitualOutcomePossibility outcome,
            out string description, ref LookTargets targets) => description = null;
    }
    public sealed class OutcomeIdeo { public bool Fluid; public OutcomeDevelopment development = new OutcomeDevelopment(); }
    public sealed class OutcomeDevelopment
    {
        public int Points;
        public bool TryGainDevelopmentPointsForRitualOutcome(Precept_Ritual ritual, int outcome, out int points) { points = 0; return false; }
    }
    public static class GatheringsUtility { public static bool InGatheringArea(IntVec3 position, IntVec3 spot, Map map) => true; }
    public static class LetterDefOf { public static object RitualOutcomePositive, RitualOutcomeNegative; }
}
namespace SexSlaveCraft
{
    public sealed class RitualOutcomeComp_BindingSpectatorCount : RimWorld.RitualOutcomeComp_Quality
    {
        public static int AttendanceDurationTicks(RimWorld.LordJob_Ritual job) => 0;
        public int Count(RimWorld.LordJob_Ritual job, RimWorld.RitualOutcomeComp_DataThingPresence data) => 0;
    }
    public static partial class Strings
    {
        public static string Ritual_RepeatPenalty => "repeat";
        public static string Ritual_FlawedLoversCreated(string slave, string master) => "relation";
    }
    public static partial class SSCDefOf { public static object SSC_FlawedLovers; }
    public static partial class ConditioningUtility
    {
        /// <summary>只记录生产 Apply 进入正式整场后果的次数，并提供外部回调边界。</summary>
        public static string ExecuteRitualOutcome(Pawn trainer, Pawn receiver, float quality, RimWorld.ThoughtDef memory)
        {
            TestWorld.RitualOutcomes++;
            TestWorld.OnRitualOutcome?.Invoke();
            return "outcome";
        }
    }
    public static partial class SpecializationTrainingProgressUtility
    {
        /// <summary>保持生产 RitualScorePerQuality 的 75 倍量纲；完整经验算法由特化进度套件验证。</summary>
        public static float RitualScore(float quality) => quality * 75f;
    }
}
