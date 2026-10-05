// 仅隔离游戏容器及原版 Ability 边界；不实现猫资格、分支选择或冷却迁移算法。
using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
    }
}

namespace Verse
{
    public class Def { public string defName; }
    public class HediffDef : Def
    {
        public Type hediffClass = typeof(HediffWithComps);
        public List<HediffCompProperties> comps = new();
        public float initialSeverity = .01f;
        public RimWorld.AbilityDef grantedAbility;
        public int disappearsAfterTicks;
    }
    public class ResearchProjectDef : Def { public bool IsFinished = true; }
    public class ThoughtDef : Def { }
    public class TraitDef : Def { }
    public class JobDef : Def { public bool abilityCasting; }
    public class Thing { public bool Destroyed; public Map Map = new(); public IntVec3 Position; }
    public class Map { public bool LineOfSight = true; }
    public static class GenSight
    {
        public static bool LineOfSight(IntVec3 from, IntVec3 to, Map map, bool skipFirstCell = false) => map?.LineOfSight == true;
    }
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public float DistanceToSquared(IntVec3 other) => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z);
        public bool InHorDistOf(IntVec3 other, float distance) => DistanceToSquared(other) <= distance * distance;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.x == b.x && a.z == b.z;
        public static bool operator !=(IntVec3 a, IntVec3 b) => !(a == b);
        public override bool Equals(object other) => other is IntVec3 cell && this == cell;
        public override int GetHashCode() => HashCode.Combine(x, z);
    }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        public Pawn Pawn => Thing as Pawn;
        public bool IsValid => Thing != null;
        public IntVec3 Cell => Thing?.Position ?? default;
        public static implicit operator LocalTargetInfo(Thing thing) => new() { Thing = thing };
    }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public static implicit operator AcceptanceReport(bool value) => new() { Accepted = value };
        public static implicit operator AcceptanceReport(string reason) => false;
        public static implicit operator bool(AcceptanceReport value) => value.Accepted;
    }
    public static class Translator { public static string Translate(this string key) => key; }
    public static class DefDatabase<T> where T : Def
    {
        public static readonly Dictionary<string, T> Definitions = new();
        public static T GetNamedSilentFail(string name) => name != null && Definitions.TryGetValue(name, out T def) ? def : null;
        public static void Add(T def) => Definitions[def.defName] = def;
    }
    public partial class Pawn : Thing
    {
        public bool Dead, Downed, Drafted, Spawned = true;
        public bool Conscious { get => health?.capacities?.CanBeAwake == true; set => health.capacities.CanBeAwake = value; }
        public bool Sleeping, Reachable = true;
        public Verse.AI.PathEndMode LastReachMode;
        public bool InMentalState => MentalState != null;
        public Verse.AI.MentalState MentalState;
        public CompSexSlaveTraining Training;
        public PawnHealth health;
        public RimWorld.Pawn_AbilityTracker abilities;
        public RimWorld.Faction Faction = RimWorld.Faction.OfPlayer;
        public RaceProperties RaceProps = new();
        public PawnNeeds needs = new();
        public PawnStory story = new();
        public Pawn BoundMaster;
        public Verse.AI.PawnJobTracker jobs = new();
        public JobDef CurJobDef;
        public string LabelShort => "cat";
        public Pawn() { health = new PawnHealth { Owner = this }; abilities = new RimWorld.Pawn_AbilityTracker(this); }
        public T TryGetComp<T>() where T : class => Training as T;
        public bool IsHashIntervalTick(int interval) => true;
        public bool CanReach(Thing target, Verse.AI.PathEndMode mode, Danger danger) { LastReachMode = mode; return Reachable; }
    }
    public class RaceProperties { public bool Humanlike = true, Animal; }
    public class PawnStory { public TraitTracker traits = new(); }
    public class TraitTracker
    {
        public readonly HashSet<TraitDef> traits = new();
        public bool HasTrait(TraitDef def) => traits.Contains(def);
    }
    public class PawnNeeds { public Mood mood = new(); }
    public class Mood { public Thoughts thoughts = new(); }
    public class Thoughts { public Memories memories = new(); }
    public class Memories { public void TryGainMemory(ThoughtDef def, Pawn other) { } }
    public class Hediff
    {
        public Pawn pawn;
        public HediffDef def;
        public float Severity;
        public HediffComp_GiveAbility Grant;
        public HediffComp_Disappears Timer;
        public T TryGetComp<T>() where T : class => Grant as T ?? Timer as T;
    }
    public class HediffWithComps : Hediff { }
    public class HediffCompProperties { public Type compClass; }
    public class HediffComp
    {
        public Hediff parent;
        public HediffCompProperties props;
        public virtual void CompPostTick(ref float severityAdjustment) { }
        public virtual void CompPostPostRemoved() { }
    }
    public class HediffComp_Disappears : HediffComp { public int ticksToDisappear; }
    public class HediffCompProperties_Disappears : HediffCompProperties { public int disappearsAfterTicks; }
    public class VerbProperties
    {
        public float range, warmupTime = 2;
    }
    public partial class Verb { public VerbProperties verbProps = new(); }
    public enum Danger { Deadly }
    public class HediffSet
    {
        public readonly List<Hediff> hediffs = new();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) != null;
    }
    public class PawnHealth
    {
        public Pawn Owner;
        public PawnCapacityTracker capacities = new();
        public readonly HediffSet hediffSet = new();
        public Hediff AddHediff(HediffDef def)
        {
            var h = new Hediff { pawn = Owner, def = def, Severity = def.initialSeverity };
            if (def.grantedAbility != null)
                h.Grant = new HediffComp_GiveAbility { parent = h, props = new HediffCompProperties_GiveAbility { abilityDef = def.grantedAbility } };
            if (def.disappearsAfterTicks > 0) h.Timer = new HediffComp_Disappears { parent = h, ticksToDisappear = def.disappearsAfterTicks };
            hediffSet.hediffs.Add(h); return h;
        }
        public void RemoveHediff(Hediff h) { if (hediffSet.hediffs.Remove(h)) h.Grant?.CompPostPostRemoved(); }
    }
    public class PawnCapacityTracker { public bool CanBeAwake = true; }
    public static class Find { public static TickManager TickManager = new(); }
    public class TickManager { public int TicksGame; }
    public static class GenTicks { public const int TickRareInterval = 250; public static int TicksGame => Find.TickManager.TicksGame; }
    public enum LoadSaveMode { Inactive, LoadingVars, PostLoadInit, Saving }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class ThingExtensions { public static bool DestroyedOrNull(this Thing thing) => thing == null || thing.Destroyed; }
    public static class Messages
    {
        public static readonly List<string> Requests = new();
        public static void Message(string text, Pawn pawn, object type, bool historical = false) => Requests.Add(text);
    }
}

namespace Verse.AI
{
    public class MentalState
    {
        public Verse.Pawn pawn;
        public int RecoverCalls;
        public bool causedByMood, causedByPsycast, causedByDamage;
        public Action OnRecover;
        public void RecoverFromState()
        {
            RecoverCalls++;
            if (pawn?.MentalState == this) pawn.MentalState = null;
            OnRecover?.Invoke();
        }
    }
    public partial class Job { public int expiryInterval; }
    public enum JobTag { Misc }
    public partial class PawnJobTracker { public bool TryTakeOrderedJob(Job job, JobTag tag) => true; }
    public static class JobMaker { public static Job MakeJob(Verse.JobDef def, Verse.Thing target) => new(); }
}

namespace RimWorld
{
    public class Faction { public static readonly Faction OfPlayer = new(); public bool IsPlayer => ReferenceEquals(this, OfPlayer); }
    public static class RestUtility
    {
        public static bool Awake(Verse.Pawn pawn) => !pawn.Sleeping;
        public static void WakeUp(Verse.Pawn pawn, bool startJob = true) { pawn.Sleeping = false; pawn.NativeWakeCalls++; }
    }
    public class AbilityDef : Verse.Def
    {
        public int cooldownTicks = 60000;
        public bool stunTargetWhileCasting = true;
        public Func<Ability, CompAbilityEffect> EffectFactory;
    }
    public class Ability
    {
        public Verse.Pawn pawn;
        public AbilityDef def;
        public bool BaseAllowed = true;
        public int ActivationCalls, CooldownStartCalls;
        private int cooldownEnd;
        public readonly List<CompAbilityEffect> EffectComps = new();
        public Verse.Verb verb;
        public bool Casting => verb?.WarmingUp == true;
        public Ability() { }
        public Ability(Verse.Pawn pawn) { this.pawn = pawn; }
        public Ability(Verse.Pawn pawn, AbilityDef def)
        {
            this.pawn = pawn; this.def = def;
            verb = new Verb_PetCatComfort { ability = this, caster = pawn };
            if (def?.EffectFactory != null) EffectComps.Add(def.EffectFactory(this));
        }
        public virtual Verse.AcceptanceReport CanCast => BaseAllowed && CooldownTicksRemaining == 0;
        public virtual void ExposeData() { }
        public int CooldownTicksRemaining => Math.Max(0, cooldownEnd - Verse.Find.TickManager.TicksGame);
        public void StartCooldown(int ticks) { CooldownStartCalls++; cooldownEnd = Verse.Find.TickManager.TicksGame + ticks; }
        public void ResetCooldown() { cooldownEnd = 0; }
        public virtual bool CanApplyOn(Verse.LocalTargetInfo target) => EffectComps.All(e => e.CanApplyOn(target, default));
        public virtual bool Activate(Verse.LocalTargetInfo target, Verse.LocalTargetInfo dest)
        {
            // 对应核验后的原版边界顺序：先记录进入PreActivate并启动冷却，再分发效果。
            // 不在宿主复制猫资格；拒绝是否发生在扣冷却前由真实能力类负责。
            ActivationCalls++; StartCooldown(def.cooldownTicks);
            foreach (var effect in EffectComps) effect.Apply(target, dest);
            return true;
        }
    }
    public class CompProperties_AbilityEffect { public Type compClass; }
    public class CompAbilityEffect
    {
        public bool BaseAllowed = true;
        public int BaseApplyCalls;
        public Ability parent;
        public CompProperties_AbilityEffect props;
        public CompProperties_AbilityEffect Props => props;
        public virtual bool Valid(Verse.LocalTargetInfo target, bool throwMessages = false) => BaseAllowed;
        public virtual bool CanApplyOn(Verse.LocalTargetInfo target, Verse.LocalTargetInfo dest) => Valid(target);
        public virtual void Apply(Verse.LocalTargetInfo target, Verse.LocalTargetInfo dest) { BaseApplyCalls++; }
    }
    public class Pawn_AbilityTracker
    {
        private readonly Verse.Pawn pawn;
        private readonly Dictionary<AbilityDef, Ability> entries = new();
        public Pawn_AbilityTracker(Verse.Pawn pawn) { this.pawn = pawn; }
        public Ability GetAbility(AbilityDef def) => def != null && entries.TryGetValue(def, out var ability) ? ability : null;
        public void GainAbility(AbilityDef def)
        {
            if (!entries.ContainsKey(def)) entries.Add(def, def == SSCDefOf.SSC_PetCatComfort ? new Ability_PetCatComfort(pawn, def) : new Ability(pawn, def));
        }
        public void RemoveAbility(AbilityDef def) { if (def != null) entries.Remove(def); }
    }
    public static class MessageTypeDefOf { public static readonly object PositiveEvent = new(), RejectInput = new(); }
    public static class JobDefOf { public static readonly Verse.JobDef Wait = new(), Wait_Wander = new(); }
}

namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public enum RabbitReproductionMode { Offspring, Clone }
    public partial class CompSexSlaveTraining
    {
        public Thing parent;
        public PawnIdentity pawnIdentity;
        public bool specializationExplicitlyUnset, trainerInvalidExitBlocksAdoption;
        public RabbitReproductionMode rabbitReproductionMode;
        public float savedCowReservoirCharge;
        public int lastPetAffectionTick = -999999;
        public const int PetAffectionCooldownTicks = 60000;
    }
    public static class SSCIdentityUtility { public static bool IsSexSlave(Pawn pawn) => pawn?.Training?.pawnIdentity == PawnIdentity.Slave; }
    public static class SSCBondUtility { public static Pawn GetBoundMaster(Pawn pawn) => pawn?.BoundMaster; }
    public static class ResearchUtils { public static bool IsResearchFinished(ResearchProjectDef research) => research?.IsFinished == true; }
    public static class SSCDefOf
    {
        public static readonly RimWorld.AbilityDef SSC_PetCatComfort = new() { defName = "SSC_PetCatComfort" };
        public static readonly HediffDef SSC_Hediff_PetCatEncouragement = new() { defName = "SSC_Hediff_PetCatEncouragement", initialSeverity = 1, disappearsAfterTicks = 30000 };
        public static readonly HediffDef SSC_PersonalityExcreted_Done = new() { defName = "SSC_PersonalityExcreted_Done" };
        public static readonly TraitDef SSC_Trait_PE = new() { defName = "SSC_Trait_PE" };
        public static readonly ResearchProjectDef SSC_BasicTraining = new();
        public static readonly HediffDef SSC_Hediff_Bus = new(), SSC_Hediff_Bus_Final = new(), SSC_Hediff_Cow = new(), SSC_Hediff_Cow_Final = new();
        public static readonly HediffDef SSC_Hediff_TrainerOfficer = new(), SSC_Hediff_Combatant = new(), SSC_Hediff_Combatant_Final = new();
        public static readonly JobDef SSC_Job_PetAffection = new();
    }
    public class CompPersonalityStore
    {
        public readonly Dictionary<HediffDef, float> hediffTags = new();
        public void RemoveTag(HediffDef def) { if (def != null) hediffTags.Remove(def); }
        public void SetTag(HediffDef def, float severity) { if (def != null) hediffTags[def] = severity; }
        public bool HasTag(HediffDef def) => def != null && hediffTags.ContainsKey(def);
        public float GetTagSeverity(HediffDef def) => hediffTags.TryGetValue(def, out float value) ? value : 0;
    }
    public class HediffComp_CowMilkReservoir { public float CurrentCharge; }
    public static class TrainerSpecializationLifecycle { public static void Notify(Pawn pawn) { } public static void Maintain(Pawn pawn) { } }
    public static class TrainerSpecializationUtility
    {
        public static bool HasFinalRecord(Pawn pawn) => false;
        public static bool MeetsContinuousConditions(Pawn pawn, out object failure) { failure = null; return true; }
    }
    public static class CombatantSpecializationUtility
    {
        public static void Sync(Pawn pawn) { }
        public static void RemoveOrdinaryState(Pawn pawn) { }
        public static bool HasFinalState(Pawn pawn) => pawn?.health?.hediffSet?.HasHediff(SSCDefOf.SSC_Hediff_Combatant_Final) == true;
    }
    public static class BusSpecializationUtility
    {
        public static bool HasFinalBusState(Pawn pawn) => pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus_Final);
        public static bool HasFinalCowState(Pawn pawn) => pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow_Final);
        public static void EnsureBusHediffFromSpecialization(Pawn pawn) { }
        public static void EnsureCowHediffFromSpecialization(Pawn pawn) { }
    }
    public static class Strings
    {
        public const string ITab_SpecializationPetCat = "cat", ITab_SpecializationPetDog = "dog", ITab_SpecializationPetRabbit = "rabbit";
        public const string ITab_SpecializationNone = "none", ITab_SelectSpecializationNone = "none";
        public const string ITab_SelectSpecializationPetCat = "cat", ITab_SelectSpecializationPetDog = "dog", ITab_SelectSpecializationPetRabbit = "rabbit";
        public const string ITab_SpecializationUnfinishedSuffix = "unfinished", ITab_SpecializationFinalizedSuffix = "final";
        public const string ITab_SpecializationPetDisabledResearch = "research", ITab_SpecializationPetDisabledMissingRequirements = "requirements", ITab_SpecializationPetDisabledConflictingFinal = "conflict";
        public static string Message_PetSpecializationUnlocked(string name, string type) => name + type;
    }
}
