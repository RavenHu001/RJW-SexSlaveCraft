// 只模拟引擎/RJW 数据边界；评分、部位凭据、预算、精确写入及完成分发均使用生产源码。
using System;
using System.Collections.Generic;
using System.Linq;
namespace Verse
{
    public class RaceProperties { public bool Humanlike=true, IsFlesh=true; public BodyDef body=new(); }
    public partial class Pawn
    {
        public string LabelShort="test";
        public bool Dead,Destroyed,Discarded,Hollow,Infestation=true;
        public int Stage;
        public RaceProperties RaceProps=new();
        public InfestationHealth health=new();
        public Needs needs=new();
        public SexSlaveCraft.CompSexSlaveTraining Training=new();
        public T TryGetComp<T>() where T:class=>Training as T;
    }
    public class Needs
    {
        public SexSlaveCraft.Need_Corruption corruption=new();
        public Mood mood=new();
        public T TryGetNeed<T>() where T:class=>corruption as T;
    }
    public class Mood { public Thoughts thoughts=new(); }
    public class Thoughts { public Memories memories=new(); }
    public class Memories { public void TryGainMemory(object def,Pawn pawn) { } }
    public static partial class Rand
    {
        public static float Range(float low,float high) {DrawState++;return low+(high-low)*.6f;}
        public static bool Chance(float chance)=>Range(0,1)<chance;
    }
}
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);
        public static float Clamp01(float x)=>Clamp(x,0,1);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Round(float x)=>(float)Math.Round(x);
    }
}
namespace rjw
{
    public partial class SexProps
    {
        public Verse.Pawn pawn,partner;
        public bool isReceiver,isCoreLovin;
        public Verse.Pawn initiator=>isReceiver?partner:pawn;
        public Verse.Pawn recipient=>isReceiver?pawn:partner;
    }
}
namespace SexSlaveCraft
{
    // 完整记忆业务在反馈专项及完成集成执行；本宿主聚焦原评分、部位和健康回调。
    public static class SSCDesireInfestationFeedbackUtility
    {
        public sealed class Plan { public void Apply(Func<bool> current) { } }
        public static Plan Capture(Verse.Pawn source,Verse.Pawn target,Need_Corruption need)=>null;
    }
    public enum PawnIdentity {Unset,Master,SexSlave}
    public class CompSexSlaveTraining {public PawnIdentity pawnIdentity;}
    public class Need_Corruption {public float CurLevel;public float CurLevelPercentage=>CurLevel; public float MaxLevel=>1;}
    public static class CorruptionUtility
    {
        public static Action OnApplied;
        public static void AddCorruption(Verse.Pawn p,float gain) {p.needs.corruption.CurLevel+=gain;OnApplied?.Invoke();}
    }
    public static class SSCMod {public static Settings settings=new();}
    public class Settings {public bool useOldScoring;}
    public static class SSCIdentityUtility
    {public static bool IsMaster(Verse.Pawn p)=>p?.Training?.pawnIdentity==PawnIdentity.Master;public static int GetSexSlaveStage(Verse.Pawn p)=>p.Stage;}
    public static class SSCMasterProgressionUtility {public static bool IsHollow(Verse.Pawn p)=>p.Hollow;}
    public static class SSCAbilityToggleUtility
    {
        public const string MasterDesireInfestation="SSC_MasterDesireInfestation";
        public static bool IsActive(Verse.Pawn p,string id)=>p.Infestation&&SSCIdentityUtility.IsMaster(p)&&!p.Hollow;
        public static void SyncMarkers(Verse.Pawn p) { }
    }
    public static class SSCPnaExposureUtility
    {public static bool IsValidTarget(Verse.Pawn p)=>p!=null&&!p.Dead&&!p.Destroyed&&!p.Discarded&&p.health!=null&&p.RaceProps.IsFlesh;}
    public static class SSCPnaSourceUtility {public static bool IsPnaProducer(Verse.Pawn p)=>false;}
    public static class SSCPnaSexSupplyUtility
    {
        public static int Calls;
        public static Action OnApplied;
        public static float CaptureInfestationDose(Verse.Pawn source)=>.08f;
        public static void ApplyInfestation(Verse.Pawn a,Verse.Pawn b,float dose,Func<bool> current) {Calls++;OnApplied?.Invoke();}
        public static void ApplyFromProducer(Verse.Pawn a,Verse.Pawn b,Func<bool> current) { }
    }
    public static class SSCPnaMath {public static bool IsFinite(float f)=>float.IsFinite(f);}
    public static class PartSizeCalculator
    {
        public static bool TryGetLength(Verse.Hediff h,out float f){f=h.Length;return f>0;}
        public static bool TryGetGirth(Verse.Hediff h,out float f){f=h.Girth;return f>0;}
    }
    public enum TrainingMemorySet {Existing,Officer,None}
    public static class SSCDefOf
    {
        public static Verse.ResearchProjectDef SSC_BodyPartTraining=new();
        public static object SSC_TrainerTraining_Mood_Lvl1,SSC_TrainerTraining_Mood_Lvl2,SSC_TrainerTraining_Mood_Lvl3,
            SSC_TrainerTraining_Social_Lvl1,SSC_TrainerTraining_Social_Lvl2,SSC_TrainerTraining_Social_Lvl3,
            SSC_Training_Mood_Lvl1,SSC_Training_Mood_Lvl2,SSC_Training_Mood_Lvl3,
            SSC_Training_Social_Lvl1,SSC_Training_Social_Lvl2,SSC_Training_Social_Lvl3;
    }
}
