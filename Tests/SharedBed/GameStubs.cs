// Minimal model of the inspected RimWorld 1.6 methods. Actual Harmony installs all production patches.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;

namespace Verse
{
    public static class Log
    {
        public static readonly Dictionary<int, string> Errors = new Dictionary<int, string>();
        public static void ErrorOnce(string message, int key) { if (!Errors.ContainsKey(key)) Errors.Add(key, message); }
    }
    public class Thing { public bool Destroyed; public ThingDef def = new ThingDef(); public Map Map; }
    public class ThingDef { public float maxBodySize = 2f; }
    public class Map { public MapPawns mapPawns = new MapPawns(); }
    public class MapPawns { public List<Pawn> SlavesOfColonySpawned = new List<Pawn>(); public List<Pawn> FreeColonists = new List<Pawn>(); }
    public class Pawn : Thing
    {
        public bool Dead, Deathresting, Sleeping, MedicalRest;
        public float BodySize = 1f;
        public string LabelShortCap = "Pawn";
        public GuestStatus? GuestStatus;
        public bool IsSlave { [MethodImpl(MethodImplOptions.NoInlining)] get { return GuestStatus == RimWorld.GuestStatus.Slave; } }
        public bool IsPrisoner => GuestStatus == RimWorld.GuestStatus.Prisoner;
        public Needs needs = new Needs();
        public Ownership ownership = new Ownership();
        public Relations relations = new Relations();
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public Hediff_ChainOfSexSlave Chain;
        public Building_Bed Bed, VanillaBed;
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class Needs
    {
        public Need_Corruption Corruption = new Need_Corruption();
        public Mood mood = new Mood();
        public T TryGetNeed<T>() where T : class => Corruption as T;
    }
    public class Ownership { public Building_Bed OwnedBed; }
    public class Relations
    {
        public HashSet<Pawn> Lovers = new HashSet<Pawn>();
        public HashSet<Pawn> FlawedLovers = new HashSet<Pawn>();
        public int Opinion;
        public int OpinionOf(Pawn pawn) => Opinion;
        public bool DirectRelationExists(object def, Pawn other) => def == SSCDefOf.SSC_FlawedLovers && FlawedLovers.Contains(other);
    }
    public class Mood { public Thoughts thoughts = new Thoughts(); }
    public class Thoughts { public Memories memories = new Memories(); }
    public class Memories
    {
        public List<Thought_Memory> Items = new List<Thought_Memory>();
        // 原版先读取 def.IsMemory；空定义不能像简单列表删除那样被静默接受。
        public void RemoveMemoriesOfDef(ThoughtDef def) { if (def.IsMemory) Items.RemoveAll(m => m.def == def); }
        public void RemoveMemoriesOfDefIf(ThoughtDef def, Predicate<Thought_Memory> filter) => Items.RemoveAll(m => m.def == def && filter(m));
        public void TryGainMemory(Thought_Memory memory, Pawn other) { memory.otherPawn = other; Items.Add(memory); }
    }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public string Reason;
        public static AcceptanceReport WasAccepted => new AcceptanceReport { Accepted = true };
        public static implicit operator AcceptanceReport(string reason) => new AcceptanceReport { Reason = reason };
    }
    public interface IExposable { void ExposeData(); }
    public static class Scribe
    {
        public static bool Loading;
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key, T fallback)
        {
            if (Loading) value = Data.TryGetValue(key, out object stored) ? (T)stored : fallback;
            else Data[key] = value;
        }
    }
    public static class Scribe_References { public static void Look<T>(ref T value, string key) where T : class => Scribe.Look(ref value, key, null); }
    public static class Scribe_Values { public static void Look<T>(ref T value, string key, T fallback) => Scribe.Look(ref value, key, fallback); }
    public enum GameFont { Tiny, Small }
    public static class Text
    {
        public static GameFont Font = GameFont.Small;
        public static TextAnchor Anchor;
        public static Vector2 CalcSize(string text) => new Vector2(text.Length * 7f, 16);
    }
    public struct TaggedString
    {
        private string value;
        public override string ToString() => value;
        public static implicit operator string(TaggedString value) => value.value;
        public static implicit operator TaggedString(string value) => new TaggedString { value = value };
    }
    public static class Translation
    {
        public static Dictionary<string, string> Keys = new Dictionary<string, string>();
        public static TaggedString Translate(this string key, params object[] args) => string.Format(Keys.TryGetValue(key, out string value) ? value : key, args);
    }
    public static class Widgets
    {
        public static List<(Rect rect, string text)> Labels = new List<(Rect, string)>();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void LabelEllipses(Rect rect, string text) => Labels.Add((rect, text));
        public static void Label(Rect rect, string text) => Labels.Add((rect, text));
        public static void DrawBoxSolid(Rect rect, Color color) { }
    }
    public static class TooltipHandler
    {
        public static string LastTooltip;
        public static void TipRegion(Rect rect, string text) => LastTooltip = text;
    }
}

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax => x + width;
        public float xMin { get => x; set { float right = xMax; x = value; width = right - x; } }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public enum TextAnchor { UpperLeft, MiddleCenter }
    public static class GUI { public static Color color = new Color(1, 1, 1, 1); }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a, b); }
}

namespace RimWorld
{
    using Verse;
    public enum GuestStatus { Guest, Prisoner, Slave }
    public class Building_Bed : Thing
    {
        public bool Medical, ForPrisoners, ForSlaves;
        public bool Spawned = true, Reachable = true, Reservable = true;
        public bool Burning, Vacuum, Forbidden, IdeologyForbidden;
        public bool AnyUnoccupiedSleepingSlot = true;
        public int SleepingSlotsCount = 2;
        public List<Pawn> OwnersForReading = new List<Pawn>();
        public bool AnyUnownedSleepingSlot => OwnersForReading.Count < SleepingSlotsCount;
    }
    public class CompAssignableToPawn { public Thing parent; }
    public class CompAssignableToPawn_Bed : CompAssignableToPawn
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public AcceptanceReport CanAssignTo(Pawn pawn)
        {
            Building_Bed bed = (Building_Bed)parent;
            if (pawn.BodySize > bed.def.maxBodySize) return "TooLargeForBed";
            if (bed.ForSlaves && !pawn.IsSlave) return "CannotAssignBedToColonist";
            if (!bed.ForSlaves && pawn.IsSlave) return "CannotAssignBedToSlave";
            return AcceptanceReport.WasAccepted;
        }
        public IEnumerable<Pawn> AssigningCandidates
        {
            [MethodImpl(MethodImplOptions.NoInlining)] get => parent.Map.mapPawns.FreeColonists;
        }
    }
    public static class LovePartnerRelationUtility
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool LovePartnerRelationExists(Pawn first, Pawn second) => first.relations.Lovers.Contains(second);
    }
    public static class RestUtility
    {
        public static int ValidationCalls;
        public static bool LastCheckSocial, LastIgnoreReservations, LastAllowMed;
        public static Pawn LastTraveler;
        public static Building_Bed CurrentBed(this Pawn pawn) => pawn?.Bed;
        public static bool Awake(this Pawn pawn) => !pawn.Sleeping;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool CanUseBedNow(Thing bedThing, Pawn sleeper, bool checkSocialProperness,
            bool allowMedBedEvenIfSetToNoCare, GuestStatus? guestStatusOverride)
        {
            Building_Bed bed = bedThing as Building_Bed;
            if (bed == null || !bed.Spawned || bed.Map != sleeper.Map || bed.Burning || bed.Vacuum
                || sleeper.BodySize > bed.def.maxBodySize || bed.IdeologyForbidden || bed.Forbidden) return false;
            bool owner = bed.OwnersForReading.Contains(sleeper);
            if (!bed.AnyUnoccupiedSleepingSlot && !owner && sleeper.Bed != bed) return false;
            GuestStatus? effective = guestStatusOverride ?? sleeper.GuestStatus;
            if (bed.ForPrisoners != (effective == GuestStatus.Prisoner) || bed.ForSlaves != (effective == GuestStatus.Slave)) return false;
            return bed.Medical || owner || BedOwnerWillShare(bed, sleeper, guestStatusOverride);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool BedOwnerWillShare(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus)
        {
            if (bed.OwnersForReading.Count == 0) return true;
            if (!bed.AnyUnownedSleepingSlot) return false;
            if (sleeper.IsSlave || sleeper.IsPrisoner || guestStatus == GuestStatus.Slave || guestStatus == GuestStatus.Prisoner) return true;
            return bed.OwnersForReading.Any(p => LovePartnerRelationUtility.LovePartnerRelationExists(sleeper, p));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsValidBedFor(Thing bedThing, Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool allowMedBedEvenIfSetToNoCare, bool ignoreOtherReservations, GuestStatus? guestStatus)
        {
            ValidationCalls++;
            LastTraveler = traveler; LastCheckSocial = checkSocialProperness;
            LastIgnoreReservations = ignoreOtherReservations; LastAllowMed = allowMedBedEvenIfSetToNoCare;
            return CanUseBedNow(bedThing, sleeper, checkSocialProperness, allowMedBedEvenIfSetToNoCare, guestStatus)
                && ((Building_Bed)bedThing).Reachable && (ignoreOtherReservations || ((Building_Bed)bedThing).Reservable);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Building_Bed FindBedFor(Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool ignoreOtherReservations, GuestStatus? guestStatus) => sleeper.VanillaBed;
    }
    public static class HealthAIUtility { public static bool ShouldSeekMedicalRest(Pawn pawn) => pawn.MedicalRest; }
    public static class ThingDefOf { public static ThingDef DeathrestCasket = new ThingDef(); }
    public class ThoughtDef { public string defName; public bool IsMemory = true; public float[] moods = { -6, -3, 2, 5, 9, 13 }; }
    public class Thought_Memory
    {
        public ThoughtDef def;
        public int CurStageIndex;
        public Pawn otherPawn;
        public float MoodOffset() => def.moods[CurStageIndex];
    }
    public static class ThoughtMaker
    {
        public static Thought_Memory MakeThought(ThoughtDef def, int stage) => new Thought_Memory { def = def, CurStageIndex = stage };
    }
    public static class ThoughtDefOf
    {
        public static ThoughtDef SleptInBedroom = new ThoughtDef { defName = "Bedroom" };
        public static ThoughtDef SleptInBarracks = new ThoughtDef { defName = "Barracks" };
    }
    public static class Toils_LayDown
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ApplyBedRelatedEffects(Pawn p, Building_Bed bed, bool asleep, bool gainRest, int delta) { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ApplyBedThoughts(Pawn actor, Building_Bed bed) { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void FinalizeLayingJob(Pawn pawn, Building_Bed bed, bool deathrest) { ApplyBedThoughts(pawn, bed); }
    }
    public class Dialog_AssignBuildingOwner
    {
        private CompAssignableToPawn assignable;
        public Dialog_AssignBuildingOwner(CompAssignableToPawn assignable) { this.assignable = assignable; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawAssignedRow(Pawn pawn, ref float y, Rect viewRect, int i) { Widgets.LabelEllipses(viewRect, pawn.LabelShortCap); y += 35f; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawUnassignedRow(Pawn pawn, ref float y, Rect viewRect, int i) { Widgets.LabelEllipses(viewRect, pawn.LabelShortCap); y += 35f; }
        public void Draw(Pawn pawn, bool assigned)
        {
            float y = 0;
            if (assigned) DrawAssignedRow(pawn, ref y, new Rect(0, 0, 200, 35), 0);
            else DrawUnassignedRow(pawn, ref y, new Rect(0, 0, 200, 35), 0);
        }
    }
}

namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public class Need_Corruption { public float CurLevelPercentage = 0.2f; }
    public class CompSexSlaveTraining { public PawnIdentity pawnIdentity; public Pawn selectedTrainer; public SSCSharedSleepRecord sharedSleep; }
    public class Hediff_ChainOfSexSlave { public Pawn LinkedPawn; }
    public static class SSCIdentityUtility { public static bool IsSexSlave(Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Slave; }
    public static class SSCBondUtility { public static Hediff_ChainOfSexSlave GetChain(Pawn pawn) => pawn?.Chain; }
    public static class SSCDefOf
    {
        public static ThoughtDef SSC_SharedBedWithMaster = new ThoughtDef { defName = "SSC_SharedBedWithMaster" };
        public static ThoughtDef SSC_SharedBedWithTrainer = new ThoughtDef { defName = "SSC_SharedBedWithTrainer" };
        public static object SSC_FlawedLovers = new object();
    }
}
