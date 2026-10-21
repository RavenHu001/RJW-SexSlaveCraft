// 引擎和其余分量的边界；不实现反馈分档或替换算法。
using System;
using System.Collections.Generic;
using Verse;
namespace Verse
{
    public class Def {public string defName;}
    public interface IExposable {void ExposeData();}
    public enum LoadSaveMode {Inactive,Saving,LoadingVars,PostLoadInit}
    public static class Scribe {public static LoadSaveMode mode;public static Dictionary<string,object> node=new();}
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value,string key,T defaultValue=default)
        {if(Scribe.mode==LoadSaveMode.Saving)Scribe.node[key]=value;else if(Scribe.mode==LoadSaveMode.LoadingVars)value=Scribe.node.TryGetValue(key,out var saved)?(T)saved:defaultValue;}
    }
    public static class Scribe_Defs {public static void Look<T>(ref T value,string key) where T:class=>Scribe_Values.Look(ref value,key);}
    public static class Scribe_References {public static void Look<T>(ref T value,string key,bool saveDestroyedThings=false) where T:class=>Scribe_Values.Look(ref value,key);}
    public static class Log {public static List<string> Warnings=new();public static void Warning(string s)=>Warnings.Add(s);}
    public partial class Pawn
    {
        public string LabelShort="pawn";
        public bool Dead,Destroyed,Discarded,Hollow;
        public object health=new();
        public RaceProperties RaceProps=new();
        public Needs needs=new();
        public Relations relations=new();
        public SexSlaveCraft.CompSexSlaveTraining Training=new();
        public T TryGetComp<T>() where T:class=>Training as T;
    }
    public class RaceProperties {public bool Humanlike=true;}
    public class Needs
    {
        public SexSlaveCraft.Need_Corruption corruption=new();public RimWorld.Need_Mood mood;
        public T TryGetNeed<T>() where T:class=>corruption as T;
    }
    public class Relations
    {
        public int Opinion;public Func<Pawn,int> Query;
        public int OpinionOf(Pawn p)=>Query!=null?Query(p):Opinion;
    }
    public static class DisplayExtensions {public static string ToStringWithSign(this float f)=>f.ToString("+0;-0;0");}
}
namespace SexSlaveCraft
{
    public enum PawnIdentity {Unset,Master,Slave}
    public class CompSexSlaveTraining {public PawnIdentity pawnIdentity;public bool Infestation=true;}
    public class Need_Corruption {public float CurLevel;public float MaxLevel=>1f;}
    public static class SSCMod {public static Settings settings=new();}
    public class Settings {public bool useOldScoring;}
    public static class SSCIdentityUtility {public static bool IsMaster(Pawn p)=>p?.Training?.pawnIdentity==PawnIdentity.Master;}
    public static class SSCMasterProgressionUtility {public static bool IsHollow(Pawn p)=>p.Hollow;}
    public static class SSCAbilityToggleUtility
    {
        public const string MasterDesireInfestation="SSC_MasterDesireInfestation";
        public static bool IsActive(Pawn p,string id)=>SSCIdentityUtility.IsMaster(p)&&p.Training.Infestation&&!p.Hollow;
        public static void SyncMarkers(Pawn p) { }
    }
    public static class SSCPnaExposureUtility {public static bool IsValidTarget(Pawn p)=>p!=null&&!p.Dead&&!p.Destroyed&&!p.Discarded&&p.health!=null;}
    public static class SSCPnaSourceUtility {public static bool IsPnaProducer(Pawn p)=>false;}
    public static class SSCPnaSexSupplyUtility
    {
        public static int Calls;public static Action OnApply;
        public static float CaptureInfestationDose(Pawn source)=>.08f;
        public static void ApplyInfestation(Pawn source,Pawn target,float dose,Func<bool> current){Calls++;OnApply?.Invoke();}
        public static void ApplyFromProducer(Pawn source,Pawn target,Func<bool> current) { }
    }
    public static class TrainingOutcomeUtility {public static float GetChainCap(Pawn p)=>.12f;}
    public static class CorruptionUtility
    {
        public static Action OnApply;
        public static void AddCorruption(Pawn p,float gain){p.needs.corruption.CurLevel+=gain;OnApply?.Invoke();}
    }
    public static class SSCDefOf {public static RimWorld.ThoughtDef SSC_DesireInfestation_Mood,SSC_DesireInfestation_Opinion;}
    public sealed class SSCOrdinarySexPartsSnapshot { }
    public static class SSCDesireInfestationBodyUtility
    {
        public sealed class Plan {public void Apply(Func<bool> current) { }}
        public static Plan Capture(Pawn source,Pawn target,SSCOrdinarySexPartsSnapshot parts)=>null;
    }
}
