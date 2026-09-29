using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

// Engine/RJW boundary model, not a second implementation of self-training.
// The production driver, interaction selection, snapshot serialization and scoring
// are linked by the project. Timers and toil order follow RJW 1.6's Masturbate driver.
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public static partial class Scribe
    {
        public static readonly Dictionary<Pawn, Pawn> LoadedPawns = new();
        public static void Within(Dictionary<string, object> node, Action expose)
        {
            var parent = Data;
            Data = node;
            try { expose(); }
            finally { Data = parent; }
        }
    }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue = default)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.Data.TryGetValue(key, out var saved) ? (T)saved : defaultValue;
        }
    }
    public static class Scribe_Defs
    {
        public static void Look<T>(ref T value, string key) => Scribe_Values.Look(ref value, key);
    }
    public static class Scribe_References
    {
        public static void Look(ref Pawn value, string key)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                var original = Scribe.Data.TryGetValue(key, out var saved) ? (Pawn)saved : null;
                value = original != null && Scribe.LoadedPawns.TryGetValue(original, out var loaded) ? loaded : original;
            }
        }
    }
    public static class Scribe_Deep
    {
        public static void Look<T>(ref T value, string key) where T : class, IExposable, new()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var node = value == null ? null : new Dictionary<string, object>();
                Scribe.Data[key] = node;
                if (value != null) Scribe.Within(node, value.ExposeData);
            }
            else
            {
                var node = Scribe.Data.TryGetValue(key, out var saved) ? (Dictionary<string, object>)saved : null;
                if (Scribe.mode == LoadSaveMode.LoadingVars) value = node == null ? null : new T();
                if (node != null && value != null) Scribe.Within(node, value.ExposeData);
            }
        }
    }
    public class Map { }
    public enum Danger { Deadly }
    public readonly struct IntVec3
    {
        public readonly int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public bool IsValid => x >= 0 && z >= 0;
        public bool InBounds(Map map) => map != null && IsValid && x < 1000 && z < 1000;
    }
    public readonly struct LocalTargetInfo
    {
        public Pawn Pawn { get; }
        public IntVec3 Cell { get; }
        public LocalTargetInfo(Pawn pawn) { Pawn = pawn; Cell = default; }
        public LocalTargetInfo(IntVec3 cell) { Pawn = null; Cell = cell; }
    }
    public class Health { public bool Downed; }
    public class Needs
    {
        public Need_Corruption Corruption = new() { CurLevel = 0.2f };
        public T TryGetNeed<T>() where T : class => Corruption as T;
    }
    public class Relations
    {
        public readonly Dictionary<Pawn, int> Opinions = new();
        public int OpinionOf(Pawn pawn) => Opinions.TryGetValue(pawn, out int opinion) ? opinion : 0;
    }
    public partial class Pawn
    {
        public bool Dead, Destroyed, Drafted, Burning, Fighting;
        public bool Spawned = true, Reachable = true, NativeCanMasturbate = true, IsSlave = true;
        public bool AllowSelfTraining = true;
        public int Stage = 2;
        public float ChainCap = 0.5f;
        public object Chain = new();
        public Pawn Master, Trainer;
        public string LabelShort = "actor";
        public string LabelShortCap => LabelShort;
        public Map Map = new();
        public Health health = new();
        public Needs needs = new();
        public Relations relations = new();
        public CompSexSlaveTraining Training = new();
        public bool IsBurning() => Burning;
        public bool IsFighting() => Fighting;
        public bool CanReach(IntVec3 cell, PathEndMode mode, Danger danger) => Reachable;
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public static class Translations { public static string Translate(this string key) => key; }
    public static class Messages
    {
        public static readonly List<string> Rejections = new();
        public static void Message(string text, Pawn pawn, object type, bool historical) => Rejections.Add(text);
    }
    public static class Log { public static void Error(string message) => throw new Exception(message); }
}
namespace Verse.AI
{
    public enum TargetIndex { A, B, C }
    public enum PathEndMode { OnCell }
    public enum JobCondition { Succeeded, Incompletable, InterruptForced }
    public enum ToilCompleteMode { PatherArrival, Never, Instant }
    public partial class Job
    {
        public bool playerForced;
        public LocalTargetInfo targetA, targetC;
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.A ? targetA : targetC;
    }
    public class Toil
    {
        public Action initAction, tickAction, finishAction;
        public ToilCompleteMode defaultCompleteMode;
        public int defaultDuration;
    }
    public partial class Pawn_JobTracker
    {
        public rjw.JobDriver_Masturbate curDriver;
        public int EndCalls;
        public void EndCurrentJob(JobCondition condition, bool startNewJob = true)
        {
            EndCalls++;
            var previous = curDriver;
            previous?.FinishCurrentForHost();
            if (curDriver == previous) curDriver = null;
        }
    }
    public static class JobDriverExtensions
    {
        public static void FailOn(this rjw.JobDriver_Masturbate driver, Func<bool> fail) => driver.Failures.Add(fail);
    }
}
namespace RimWorld
{
    public partial class InteractionDef { public string defName; }
    public static class MessageTypeDefOf { public static readonly object RejectInput = new(); }
}
namespace rjw.Modules.Interactions
{
    public enum SexInteractionTag { Reverse }
    public partial class SexInteraction
    {
        public bool HasInteractionTag(SexInteractionTag tag) => Extension.Reverse;
    }
}
namespace rjw
{
    public static partial class xxx
    {
        public static bool can_masturbate(Pawn pawn) => pawn.NativeCanMasturbate;
    }
    public partial class SexProps : IExposable
    {
        public SexProps() { }
        public bool isRevese;
        public InteractionDef dictionaryKey;
        private Modules.Interactions.SexInteractionResolved cached;
        public Modules.Interactions.SexInteraction interaction
        {
            get => dictionaryKey == null ? null : new(dictionaryKey);
            set { dictionaryKey = value?.Def; cached = null; }
        }
        public Modules.Interactions.SexInteractionResolved resolved
        {
            get => cached ??= dictionaryKey == null ? null : Modules.Interactions.SexInteractionHelper.ResolveInteraction(this);
            set { cached = value; if (value != null) dictionaryKey = value.Interaction.Def; }
        }
        public xxx.rjwSextype sexType => interaction?.Sextype ?? xxx.rjwSextype.None;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref partner, "partner");
            Scribe_Defs.Look(ref dictionaryKey, "dictionaryKey");
            Scribe_Values.Look(ref canBeGuilty, "canBeGuilty", true);
        }
    }
    public static partial class SexUtility
    {
        public static int SelectionCalls, AftersexCalls;
        public static readonly List<string> Events = new();
        public static SexProps SelectSextype(Pawn pawn, Pawn partner, bool forced, bool whoring)
        {
            SelectionCalls++;
            var def = SexInteractions.FirstOrDefault(pawn.Available.Contains);
            return def == null ? null : new SexProps(pawn, partner) { interaction = new(def) };
        }
        public static void Aftersex(SexProps props) { AftersexCalls++; Events.Add("RJW outcome"); }
    }
    public class JobDriver_Masturbate : IExposable
    {
        protected const TargetIndex iTarget = TargetIndex.A;
        public Pawn pawn;
        public Job job;
        public SexProps Sexprops;
        public int ticks_left, sex_ticks, duration, orgasmstick, orgasmStartTick;
        public int ticks_between_hearts, ticks_between_hits, ticks_between_thrusts;
        public bool Started, CanRunStart = true;
        public int StartCalls, EndCalls, AnimationInitCalls, ToilIndex = -1;
        public Action<JobDriver_Masturbate> AnimationStart;
        public List<Toil> Toils = new();
        public readonly List<Func<bool>> Failures = new();
        private bool currentFinished;
        private bool nextToil;
        public IntVec3 cell => job.targetC.Cell;

        public virtual void ExposeData()
        {
            // Verse.JobDriver reconstructs toils in PostLoadInit before RJW's
            // ExposeData returns. LoadingVars already restored the timer fields.
            if (Scribe.mode == LoadSaveMode.PostLoadInit) BuildForHost();
            Scribe_Values.Look(ref ToilIndex, "curToilIndex", -1);
            Scribe_Values.Look(ref ticks_left, "ticks_left");
            Scribe_Values.Look(ref sex_ticks, "sex_ticks");
            Scribe_Values.Look(ref duration, "duration");
            Scribe_Values.Look(ref orgasmstick, "orgasmstick");
            Scribe_Values.Look(ref orgasmStartTick, "orgasmStartTick");
            Scribe_Values.Look(ref ticks_between_hearts, "ticks_between_hearts");
            Scribe_Values.Look(ref ticks_between_hits, "ticks_between_hits");
            Scribe_Values.Look(ref ticks_between_thrusts, "ticks_between_thrusts");
            Scribe_Deep.Look(ref Sexprops, "Sexprops");
            Scribe_Values.Look(ref Started, "sscSceneStarted");
        }
        protected virtual IEnumerable<Toil> MakeNewToils()
        {
            duration = ticks_left = sex_ticks = orgasmStartTick = 12;
            orgasmstick = 3;
            ticks_between_hearts = 7; ticks_between_hits = 8; ticks_between_thrusts = 9;
            this.FailOn(() => pawn.Drafted || pawn.health.Downed || pawn.Burning || pawn.Fighting);
            yield return new Toil { defaultCompleteMode = ToilCompleteMode.PatherArrival };
            yield return new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Never,
                defaultDuration = duration,
                initAction = () =>
                {
                    if (CanRunStart && pawn.jobs.curDriver == this)
                    {
                        Started = true; StartCalls++;
                        AnimationStart?.Invoke(this);
                    }
                    // Models the animation framework's additional init delegate.
                    AnimationInitCalls++;
                },
                tickAction = () => { ticks_left--; sex_ticks--; if (ticks_left <= 0) nextToil = true; },
                finishAction = () => EndCalls++
            };
            yield return new Toil { initAction = () => SexUtility.Aftersex(Sexprops), defaultCompleteMode = ToilCompleteMode.Instant };
        }
        public void BuildForHost() { Failures.Clear(); Toils = MakeNewToils().ToList(); }
        public void AdvanceForHost()
        {
            FinishCurrentForHost();
            do
            {
                if (pawn.jobs.curDriver != this) return;
                ToilIndex++;
                if (ToilIndex >= Toils.Count) { pawn.jobs.EndCurrentJob(JobCondition.Succeeded); return; }
                currentFinished = false;
                if (CheckFailureForHost()) return;
                Toils[ToilIndex].initAction?.Invoke();
                if (pawn.jobs.curDriver != this) return;
                if (Toils[ToilIndex].defaultCompleteMode != ToilCompleteMode.Instant) return;
                FinishCurrentForHost();
            } while (true);
        }
        public void TickForHost()
        {
            if (pawn.jobs.curDriver != this || CheckFailureForHost()) return;
            nextToil = false;
            Toils[ToilIndex].tickAction?.Invoke();
            if (nextToil && pawn.jobs.curDriver == this) AdvanceForHost();
        }
        private bool CheckFailureForHost()
        {
            if (!Failures.Any(fail => fail())) return false;
            pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            return true;
        }
        public void FinishCurrentForHost()
        {
            if (currentFinished || ToilIndex < 0 || ToilIndex >= Toils.Count) return;
            currentFinished = true;
            Toils[ToilIndex].finishAction?.Invoke();
        }
    }
}
namespace SexSlaveCraft
{
    public class Need_Corruption { public float CurLevel; }
    public static class SSCIdentityUtility
    {
        public static bool IsSexSlave(Pawn pawn) => pawn?.IsSlave == true;
        public static int GetSexSlaveStage(Pawn pawn) => pawn.Stage;
    }
    public static class SSCBondUtility
    {
        public static object GetChain(Pawn pawn) => pawn?.Chain;
        public static Pawn GetBoundMaster(Pawn pawn) => pawn.Master;
    }
    public static class TrainerAssignmentUtility { public static Pawn GetActiveAssignedTrainer(Pawn pawn) => pawn.Trainer; }
    // Permission and start-receipt implementations have their own production tests
    // in InteractionProtection. These are controllable inputs to the driver host.
    public enum SSCInteractionKind { SelfTraining }
    public class SSCRestrictionRequest
    {
        public readonly Pawn Actor;
        public SSCRestrictionRequest(Pawn actor, Pawn target, SSCInteractionKind kind, bool known) { Actor = actor; }
    }
    public class SSCRestrictionDecision { public bool Allowed; }
    public static class SSCRestrictionPolicy
    {
        public static SSCRestrictionDecision Evaluate(SSCRestrictionRequest request)
            => new() { Allowed = request.Actor.AllowSelfTraining && SSCSelfTrainingEligibility.IsEligible(request.Actor) };
    }
    public static class SSCRestrictionJobGuard
    {
        public static bool HasStartedScene(rjw.JobDriver_Masturbate driver, SSCInteractionKind kind) => driver.Started;
    }
    public static class TrainingOutcomeUtility { public static float GetChainCap(Pawn pawn) => pawn.ChainCap; }
    public static class CorruptionUtility
    {
        public static int AddCalls;
        public static void AddCorruption(Pawn pawn, float gain) { AddCalls++; pawn.needs.Corruption.CurLevel += gain; }
    }
    public static class SSCSelfTrainingFeedback
    {
        public static int Calls;
        public static SSCSelfTrainingSnapshot LastSnapshot;
        public static Action OnFeedback;
        public static void OnCompleted(Pawn pawn, SSCSelfTrainingSnapshot snapshot)
        {
            Calls++; LastSnapshot = snapshot;
            rjw.SexUtility.Events.Add("SSC feedback");
            OnFeedback?.Invoke();
        }
    }
    public static class SSCLog { public static void Important(string message) { } }
}
