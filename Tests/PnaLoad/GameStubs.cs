using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

// 仅模拟健康接口、组件调度、字段保存与 Harmony 目标；不替代 Unity 能力计算或真实 Scribe。
namespace Verse
{
    public class Thing { public bool Destroyed, Discarded; }
    public class Pawn : Thing
    {
        public bool Dead, Suspended, IsSlave;
        public float Corruption;
        public bool IsMaster;
        public RaceProperties RaceProps = new RaceProperties();
        public Pawn_HealthTracker health;
        public RimWorld.Pawn_NeedsTracker needs;
        public Pawn() { health = new Pawn_HealthTracker(this); needs = new RimWorld.Pawn_NeedsTracker(this); }
    }
    public class RaceProperties { public bool IsFlesh = true; }
    public class HediffDef
    {
        public string defName;
        public float initialSeverity = 0.01f, maxSeverity = 2f;
        public float[] thresholds = new[] { 0f };
        public bool HasPnaComp;
    }
    public class BodyPartRecord { }
    public struct DamageInfo { }
    public class DamageWorker { public class DamageResult { } }
    public class Hediff
    {
        public Pawn pawn;
        public HediffDef def;
        public float RawSeverity;
        public virtual int CurStageIndex => Array.FindLastIndex(def.thresholds, threshold => RawSeverity >= threshold);
        public virtual float Severity
        {
            get => RawSeverity;
            set
            {
                int stage = CurStageIndex;
                RawSeverity = float.IsNaN(value) ? value : Math.Clamp(value, 0f, def.maxSeverity);
                if (stage != CurStageIndex && pawn.health.hediffSet.hediffs.Contains(this)) pawn.health.Notify_HediffChanged(this);
            }
        }
        public virtual string SeverityLabel { [MethodImpl(MethodImplOptions.NoInlining)] get => (Severity * 100).ToString("F0") + "%"; }
    }
    public class HediffWithComps : Hediff
    {
        public List<HediffComp> comps = new List<HediffComp>();
        public T GetComp<T>() where T : HediffComp => comps.OfType<T>().FirstOrDefault();
    }
    public class HediffSet { public List<Hediff> hediffs = new List<Hediff>(); }
    public class Pawn_HealthTracker
    {
        public readonly Pawn Pawn;
        public readonly HediffSet hediffSet = new HediffSet();
        public Action<Hediff> OnChanged, OnAdded, OnRemoved;
        public int Notifications;
        public Pawn_HealthTracker(Pawn pawn) { Pawn = pawn; }
        public void AddHediff(Hediff hediff) => AddHediff(hediff, null, null, null);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddHediff(Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
        {
            Hediff previous = hediffSet.hediffs.FirstOrDefault(h => h.def == hediff.def && h is not SexSlaveCraft.Hediff_ChainOfSexSlave);
            if (previous != null) previous.Severity += hediff.Severity;
            else hediffSet.hediffs.Add(hediff);
            OnAdded?.Invoke(hediff);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void RemoveHediff(Hediff hediff)
        {
            if (!hediffSet.hediffs.Remove(hediff)) return;
            Notifications++;
            OnRemoved?.Invoke(hediff);
        }
        public void Notify_HediffChanged(Hediff hediff) { Notifications++; OnChanged?.Invoke(hediff); }
    }
    public class HediffCompProperties { public Type compClass; }
    public class HediffComp
    {
        public HediffWithComps parent;
        public HediffCompProperties props;
        public Pawn Pawn => parent.pawn;
        public virtual void CompPostTick(ref float adjustment) { }
        public virtual void CompPostTickInterval(ref float adjustment, int delta) { }
        public virtual void CompExposeData() { }
        public virtual bool CompShouldRemove => false;
        public virtual string CompTipStringExtra => null;
        public virtual string CompDebugString() => null;
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn)
        {
            var record = new HediffWithComps { def = def, pawn = pawn, RawSeverity = def.initialSeverity };
            if (def.HasPnaComp) record.comps.Add(new SexSlaveCraft.HediffComp_SSCPnaLoad { parent = record, props = new SexSlaveCraft.HediffCompProperties_SSCPnaLoad() });
            return record;
        }
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; public static Dictionary<string, object> Data = new Dictionary<string, object>(); }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Scribe.Data.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
        }
    }
    public class Game { }
    public class GameComponent { public GameComponent() { } public virtual void FinalizeInit() { } }
    public static class Find { public static Selector Selector = new Selector(); }
    public class Selector { public Thing SingleSelectedThing; }
    public static class Messages
    {
        public static readonly List<string> Sent = new List<string>();
        public static void Message(string text, RimWorld.MessageTypeDef type) => Sent.Add(text);
        public static void Message(string text, Pawn pawn, RimWorld.MessageTypeDef type, bool historical) => Sent.Add(text);
    }
    public static class StringExtensions
    {
        public static string Translate(this string text, params object[] args) => text + string.Join(",", args);
        public static string ToStringPercent(this float value) => (value * 100).ToString("F0") + "%";
    }
}
namespace RimWorld
{
    public class Pawn_NeedsTracker
    {
        private readonly Verse.Pawn pawn;
        public float? PnaNeed;
        public int Refreshes;
        public Pawn_NeedsTracker(Verse.Pawn pawn) { this.pawn = pawn; }
        public void AddOrRemoveNeedsAsAppropriate()
        {
            Refreshes++;
            if (!pawn.health.hediffSet.hediffs.Any(h => SexSlaveCraft.SSCPnaExposureUtility.IsAddictionDef(h.def))) PnaNeed = null;
        }
    }
    public class ChemicalDef { public Verse.HediffDef addictionHediff; }
    public class CompProperties_Drug { public ChemicalDef chemical; }
    public class CompDrug
    {
        public CompProperties_Drug Props;
        public int OriginalCalls;
        [MethodImpl(MethodImplOptions.NoInlining)] public void PrePostIngested(Verse.Pawn ingester) { OriginalCalls++; }
    }
    public class MessageTypeDef { }
    public static class MessageTypeDefOf { public static MessageTypeDef RejectInput = new MessageTypeDef(), NeutralEvent = new MessageTypeDef(); }
}
namespace LudeonTK
{
    public enum AllowedGameStates { PlayingOnMap }
    public class DebugActionAttribute : Attribute
    {
        public AllowedGameStates allowedGameStates;
        public DebugActionAttribute(string category, string name) { }
    }
}
namespace SexSlaveCraft
{
    public static class SSCDefOf { public static Verse.HediffDef SSC_PnaLoad, SSC_PnaRootedLoad; }
    public class Hediff_ChainOfSexSlave : Verse.Hediff
    {
        public Verse.Pawn LinkedPawn;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Hediff_ChainOfSexSlave AddToPawn(Verse.Pawn pawn, Verse.Pawn master)
        {
            var chain = pawn.health.hediffSet.hediffs.OfType<Hediff_ChainOfSexSlave>().FirstOrDefault();
            if (chain == null)
            {
                chain = new Hediff_ChainOfSexSlave { pawn = pawn, LinkedPawn = master, def = Program.ChainDef };
                pawn.health.AddHediff(chain);
            }
            else chain.LinkedPawn = master;
            return chain;
        }
    }
    public static class SSCRestrictionGameComponent
    {
        public static List<Verse.Pawn> Pawns = new List<Verse.Pawn>();
        public static List<Verse.Pawn> AllPawns() => Pawns.ToList();
    }
}
