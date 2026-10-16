using System;
using System.Collections.Generic;
using Verse;

// 此宿主检查真实完成适配器/快照/恶堕；PNA 身体写入由 PnaLoad 的真实核心覆盖。
namespace Verse
{
    public class RaceProperties { public bool Humanlike=true, IsFlesh=true; public BodyDef body=new(); }
    public class InfestationNeeds
    {
        public SexSlaveCraft.Need_Corruption corruption = new SexSlaveCraft.Need_Corruption();
        public T TryGetNeed<T>() where T : class => corruption as T;
    }
}
namespace SexSlaveCraft
{
    public static partial class SSCDefOf { public static ResearchProjectDef SSC_BodyPartTraining=new(); }
    public class Need_Corruption
    { public float CurLevel; public float MaxLevel=>1f; public float CurLevelPercentage=>CurLevel; }
    public static class CorruptionUtility { public static void AddCorruption(Pawn pawn,float gain)=>pawn.needs.corruption.CurLevel+=gain; }
    public static class TrainingOutcomeUtility { public static float GetChainCap(Pawn pawn)=>.12f; public static float GetScore(Pawn a,Pawn b)=>50f; public static float GetChainStageMult(Pawn p)=>1f; }
    public static class SSCMasterProgressionUtility { public static bool IsHollow(Pawn pawn)=>pawn.Hollow; }
    public static class SSCPnaExposureUtility
    { public static bool IsValidTarget(Pawn pawn)=>pawn!=null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && pawn.health!=null && pawn.RaceProps.IsFlesh; }
    public static class SSCPnaSourceUtility { public static bool IsPnaProducer(Pawn pawn)=>pawn?.Producer==true; }
    public static class SSCAbilityToggleUtility
    {
        public const string MasterDesireInfestation="SSC_MasterDesireInfestation";
        public static bool IsActive(Pawn pawn,string id)=>pawn?.Training?.Infestation==true && SSCIdentityUtility.IsMaster(pawn) &&
            !pawn.Dead && !pawn.Hollow && !SSCPersonalityRuntimeUtility.IsMutating(pawn);
        public static void SyncMarkers(Pawn pawn) { }
    }
}
