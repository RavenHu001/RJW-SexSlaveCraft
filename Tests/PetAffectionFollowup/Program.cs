using System;
using System.Linq;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Console.WriteLine("模式：亲昵后续生产资格/调度/驱动 + 可观察游戏与RJW边界");
        RunUtilityCases();
        RunAffectionCases();
        RunDerivedCases();
        RunResourceCases();
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }
    private static void Run(string name, Action test)
    {
        Reset();
        try { test(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception error) { failed++; Console.WriteLine("失败：" + name + "\n" + error); }
    }
    private static void Assert(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Reset()
    {
        Find.TickManager.TicksGame = 200000;
        Scribe.Values.Clear(); Scribe.mode = LoadSaveMode.Inactive;
        Rand.Chances.Clear(); Rand.ChanceResult = true; Rand.OnChance = null;
        JobMaker.Made = 0; JobMaker.Returned.Clear();
        PetSpecializationUtility.RewardCalls = 0; PetSpecializationUtility.CompletionResult = true; PetSpecializationUtility.OnComplete = null;
        SSCDefOf.SSC_Job_PetAffectionFollowup = new() { defName = "SSC_Job_PetAffectionFollowup", driverClass = typeof(JobDriver_PetAffectionFollowup) };
        SSCRestrictionJobGuard.PrepareResult = true; SSCRestrictionJobGuard.Started = false;
        SSCRestrictionJobGuard.PrepareCalls = SSCRestrictionJobGuard.RegisterCalls = SSCRestrictionJobGuard.CancelCalls = 0;
        SSCRestrictionJobGuard.PreparedJob = SSCRestrictionJobGuard.RegisteredJob = SSCRestrictionJobGuard.RegisteredWait = null;
        SSCRestrictionJobGuard.OnPrepare = SSCRestrictionJobGuard.OnRegister = null;
        SSCRestrictionJobGuard.OwnedPreparation.Clear();
        Toils_General.NativeWaitWithInit = null; JobDriver_SexQuick.NativeQuickieInit = null;
        JobDriver_SexQuick.NativeQuickieSecondInit = null; JobDriver_SexBaseInitiator.SharedStartAllowed = true;
        ResetRjw();
    }
    private static (Pawn pet, Pawn master, Job waiting) Pair()
    {
        object map = new();
        var master = new Pawn { Map = map, Cat = false, LabelShort = "master" };
        var pet = new Pawn { Map = map, BoundMaster = master, LabelShort = "pet" };
        var wait = new Job { def = JobDefOf.Wait, expiryInterval = 121, startTick = Find.TickManager.TicksGame };
        master.jobs.curJob = wait;
        ConfigurePair(pet, master);
        return (pet, master, wait);
    }
    private static void NoRollOrStart(Pawn pet)
    {
        Assert(Rand.Chances.Count == 0 && pet.jobs.StartCalls == 0 && SSCRestrictionJobGuard.RegisterCalls == 0,
            "资格拒绝或未完成亲昵仍然掷骰/调度/登记等待");
    }
}
