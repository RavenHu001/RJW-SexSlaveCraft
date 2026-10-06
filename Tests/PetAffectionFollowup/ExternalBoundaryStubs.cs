using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public enum SexSlaveSpecializationType { None, PetCat, PetDog, PetRabbit }
    public class CompSexSlaveTraining { }
    public static class SSCDefOf
    {
        public static readonly JobDef SSC_Job_PetAffection = new() { defName = "SSC_Job_PetAffection" };
        public static JobDef SSC_Job_PetAffectionFollowup = new() { defName = "SSC_Job_PetAffectionFollowup" };
    }
    public static class SSCBondUtility { public static Pawn GetBoundMaster(Pawn pawn) => pawn?.BoundMaster; }
    public static class PetSpecializationUtility
    {
        public static int RewardCalls;
        public static bool CompletionResult = true;
        public static Action OnComplete;
        public static bool HasPetAffectionQualification(Pawn pawn) => pawn?.AffectionQualified == true;
        public static bool HasActivePetEffects(Pawn pawn, SexSlaveSpecializationType type) => type == SexSlaveSpecializationType.PetCat ? pawn?.Cat == true : type == SexSlaveSpecializationType.PetDog && pawn?.Dog == true;
        public static bool CanApproachPetAffectionNow(Pawn pawn, Pawn master) => pawn?.AffectionQualified == true && pawn.BoundMaster == master;
        public static bool CanDoPetAffectionNow(Pawn pawn, Pawn master) => CanApproachPetAffectionNow(pawn, master) && pawn.Contact;
        public static bool CanStartPetAffectionJobWithoutDisruptingWork(Pawn pawn) => pawn?.Idle == true;
        public static bool CompletePetAffection(Pawn pawn, Pawn master) { OnComplete?.Invoke(); if (CompletionResult) RewardCalls++; return CompletionResult; }
    }
    public enum SSCRestrictionEvent { None, TradeConsensual, TradeForced, Dog, PetAffection }
    public enum SSCInteractionKind { Consensual }
    public static class SSCRestrictionJobGuard
    {
        public static bool PrepareResult = true, Started;
        public static int PrepareCalls, RegisterCalls, CancelCalls;
        public static Job PreparedJob, RegisteredJob, RegisteredWait, CancelledJob;
        public static Pawn PreparedActor, RegisteredTarget;
        public static SSCRestrictionEvent Source;
        public static Action OnPrepare, OnRegister;
        public static readonly HashSet<Job> OwnedPreparation = new();
        public static bool PrepareEvent(Pawn pawn, Job job, SSCRestrictionEvent source)
        { PrepareCalls++; PreparedActor = pawn; PreparedJob = job; Source = source; OnPrepare?.Invoke(); return PrepareResult; }
        public static void RegisterEventWait(Pawn pawn, Job job, Pawn target, Job wait)
        { RegisterCalls++; RegisteredJob = job; RegisteredTarget = target; RegisteredWait = wait; OnRegister?.Invoke(); }
        public static void CancelPendingEvent(Job job) { CancelCalls++; CancelledJob = job; }
        public static bool HasStartedScene(rjw.JobDriver_Sex driver, SSCInteractionKind kind) => Started;
        public static bool OwnsPreparedJob(rjw.JobDriver_Sex driver, Pawn target, Job job) => OwnedPreparation.Contains(job);
    }
}
namespace rjw
{
    public class JobDriver_Sex : JobDriver { protected override IEnumerable<Toil> MakeNewToils() { yield break; } }
    public class JobDriver_SexBaseInitiator : JobDriver_Sex
    {
        public int NativeStartCalls, NativeEndCalls;
        public static bool SharedStartAllowed = true;
        // 明确执行生产Prefix/Postfix；共享拒绝是外部守卫输出，不在宿主重写许可策略。
        public void Start()
        {
            bool ranOriginal = SharedStartAllowed && SexSlaveCraft.PetAffectionFollowupStartHook.Prefix(this);
            if (ranOriginal) { NativeStartCalls++; SexSlaveCraft.SSCRestrictionJobGuard.Started = true; }
            SexSlaveCraft.PetAffectionFollowupStartHook.Postfix(this, ranOriginal);
        }
        public void End() { if (SexSlaveCraft.PetAffectionFollowupEndHook.Prefix(this)) NativeEndCalls++; }
    }
    public class JobDriver_SexQuick : JobDriver_SexBaseInitiator
    {
        public Pawn Partner => job.GetTarget(TargetIndex.A).Pawn;
        public static Action NativeQuickieInit;
        public static Action NativeQuickieSecondInit;
        protected override IEnumerable<Toil> MakeNewToils()
        { yield return new Toil { initAction = () => NativeQuickieInit?.Invoke() }; yield return new Toil { initAction = () => NativeQuickieSecondInit?.Invoke() }; }
    }
    public class JobDriver_SexBaseReciever : JobDriver_Sex { public Pawn Partner; public List<Pawn> parteners = new(); }
    #pragma warning disable CS8981 // RJW实际类型名为小写xxx。
    public static class xxx
    {
        public static readonly JobDef getting_quickie = new() { defName = "GettinQuickie" };
        public static bool is_nympho(Pawn pawn) => pawn?.Nympho == true;
        public static bool is_frustrated(Pawn pawn) => pawn?.Frustrated == true;
        public static bool is_horny(Pawn pawn) => pawn?.Horny == true;
    }
    #pragma warning restore CS8981
    public static class RJWHookupSettings { public static bool HookupsEnabled = true, QuickHookupsEnabled = true, NymphosCanCheat; }
    public static class RJWSettings { public static bool WildMode, HippieMode; }
    public enum AgeCategory { Child, Teenager, Adult }
    public static class PawnExtensions
    {
        public static AgeCategory GetAgeCategory(this Pawn pawn) => pawn.AdultCategory && pawn.Adult ? AgeCategory.Adult : AgeCategory.Teenager;
        public static bool HasBeerGoggles(this Pawn pawn) => pawn.BeerGoggles;
    }
    public static class CasualSex_Helper
    {
        public static bool AllowedSettings = true;
        public static Action OnCanHaveSex;
        public static bool CanHaveSex(Pawn pawn) { OnCanHaveSex?.Invoke(); return pawn?.CanHaveSex == true; }
        public static bool CanTargetHookup(Pawn pawn, Pawn target) => pawn?.CanTargetHookup == true;
        public static bool CasualHookupAllowedViaSettings(rjw.Modules.Attraction.AppraisalResult result) => AllowedSettings;
    }
    public static class SexUtility
    {
        public static bool ReadyForLovin(Pawn pawn) => pawn?.ReadyForLovin == true;
        public static bool ReadyForHookup(Pawn pawn) => pawn?.ReadyForHookup == true;
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPriority : Attribute { public HarmonyPriority(int priority) { } }
    public static class Priority { public const int First = 800; }
}
namespace rjw.Modules.Attraction
{
    public enum AttractionPurpose { ForFucking }
    public class AppraisalSettings { public AppraisalSettings(AttractionPurpose purpose) { } }
    public class AppraisalResult
    {
        public Pawn Actor, Target;
        public AppraisalResult(Pawn actor, AppraisalSettings actorSettings, Pawn target, AppraisalSettings targetSettings) { Actor = actor; Target = target; }
    }
    public static class SexAppraiser
    {
        public static bool Accepted = true;
        public static AppraisalResult LastResult;
        public static IEnumerable<AppraisalResult> FindBestResults(IEnumerable<AppraisalResult> results)
        { foreach (var result in results) { LastResult = result; if (Accepted) yield return result; } }
    }
    public static class AttractionUtility { public static bool CanFoolAround(Pawn pawn, bool strict) => pawn.FoolAround; }
}
