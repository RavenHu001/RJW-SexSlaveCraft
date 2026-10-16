using System;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>真实完成后缀与任务认领负责次数；这里不模拟 PNA 身体效果。</summary>
    private static void RunPnaSupplyDispatchTests()
    {
        foreach (bool forced in new[] { false, true })
            Run("PNA 普通/强迫完成共用一次认领，保留原经验 forced=" + forced, () =>
            {
                ResetSexAwards(); var p = People(); var d = Driver(p.a, p.b, rape: forced); Begin(d); d.ticks_left = 0;
                d.Sexprops.isRevese = true; d.Sexprops.usedCondom = true;
                SexUtility.ProcessSex(d.Sexprops); SexUtility.ProcessSex(d.Sexprops);
                Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1, "经验不重复");
                Assert(SSCPnaSexSupplyUtility.Calls.Count == 2 && SSCPnaSexSupplyUtility.Calls[0] == (p.a, p.b) &&
                    SSCPnaSexSupplyUtility.Calls[1] == (p.b, p.a), "反向动作/避孕套不改变双方逐方向分发");
            });
        Run("PNA 过早及跳过原结算不消费，正常完成后才分发", () =>
        {
            ResetSexAwards(); var p = People(); var d = Driver(p.a, p.b, rape: false); Begin(d);
            SexUtility.ProcessSex(d.Sexprops); d.ticks_left = 0;
            SSCTrainerInitiatedSexProgressHook.Postfix(d.Sexprops, false);
            Assert(SSCPnaSexSupplyUtility.Calls.Count == 0, "不得提前分发");
            SexUtility.ProcessSex(d.Sexprops); Assert(SSCPnaSexSupplyUtility.Calls.Count == 2, "正常完成仍可领取");
        });
        Run("PNA 已领取存档恢复不再分发", () =>
        {
            ResetSexAwards(); var p = People(); var old = Driver(p.a, p.b, rape: false); Begin(old); old.ticks_left = 0;
            SexUtility.ProcessSex(old.Sexprops);
            var restored = new JobDriver_SexBaseInitiator { pawn = p.a, job = old.job, Sexprops = old.Sexprops, PartnerPawn = p.b, ticks_left = 0 };
            p.a.jobs.curDriver = restored; Restore(old, restored); SexUtility.ProcessSex(restored.Sexprops);
            Assert(SSCPnaSexSupplyUtility.Calls.Count == 2 && TrainerSpecializationProgressUtility.InitiatedSexAwards == 1, "已领取不回填");
        });
        Run("PNA 经验回调重入不会抢占或重发完成", () =>
        {
            ResetSexAwards(); var p = People(); var d = Driver(p.a, p.b, rape: false); Begin(d); d.ticks_left = 0;
            TrainerSpecializationProgressUtility.OnAward = () => SexUtility.ProcessSex(d.Sexprops);
            SexUtility.ProcessSex(d.Sexprops);
            Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1 && SSCPnaSexSupplyUtility.Calls.Count == 2, "重入重复分发");
        });
        foreach (bool changeId in new[] { false, true })
            Run("PNA 经验回调换任务或复用 Job 后停止供给 id=" + changeId, () =>
            {
                ResetSexAwards(); var p = People(); var d = Driver(p.a, p.b, rape: false); Begin(d); d.ticks_left = 0;
                TrainerSpecializationProgressUtility.OnAward = () =>
                {
                    if (changeId) d.job.loadID++;
                    else p.a.jobs.curDriver = new JobDriver_SexBaseInitiator { pawn = p.a, job = new Job(), PartnerPawn = p.b };
                };
                SexUtility.ProcessSex(d.Sexprops);
                Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards == 1 && SSCPnaSexSupplyUtility.Calls.Count == 0, "旧任务不得继续供给");
            });
        Run("PNA 第一方向供给回调改目标后停止第二方向", () =>
        {
            ResetSexAwards(); var p = People(); var d = Driver(p.a, p.b, rape: false); Begin(d); d.ticks_left = 0;
            SSCPnaSexSupplyUtility.OnSupply = () => { d.PartnerPawn = p.c; d.job.targetA = new LocalTargetInfo { Thing = p.c }; };
            SexUtility.ProcessSex(d.Sexprops);
            Assert(SSCPnaSexSupplyUtility.Calls.Count == 1, "回调变更后继续了第二方向");
        });
        Run("PNA 同场独立完成逐对处理，不靠 tick 合并", () =>
        {
            ResetSexAwards(); var p = People(); SSCMod.settings.enableSexSlaveProtectionRules = false;
            var first = Driver(p.a, p.b, rape: false); Begin(first); first.ticks_left = 0;
            SexUtility.ProcessSex(first.Sexprops);
            var second = Driver(p.c, p.b, rape: false); Begin(second); second.ticks_left = 0;
            SexUtility.ProcessSex(second.Sexprops);
            Assert(SSCPnaSexSupplyUtility.Calls.Count == 4 && TrainerSpecializationProgressUtility.InitiatedSexAwards == 2,
                "独立任务应分别完成");
            Assert(SSCPnaSexSupplyUtility.Calls[2] == (p.c, p.b) && SSCPnaSexSupplyUtility.Calls[3] == (p.b, p.c), "同场串对");
        });
        Run("PNA 原版 Lovin 标记交给独立适配器且普通后缀不分发", () =>
        {
            ResetSexAwards(); var p = People(); var d = Driver(p.a, p.b, rape: false); Begin(d); d.ticks_left = 0;
            d.Sexprops.isCoreLovin = true; SexUtility.ProcessSex(d.Sexprops);
            Assert(SSCPnaSexSupplyUtility.Calls.Count == 0 && TrainerSpecializationProgressUtility.InitiatedSexAwards == 0, "原版来源不得由普通适配器认领");
        });
        Run("PNA SSC 专用任务和单人任务不分发", () =>
        {
            ResetSexAwards(); var p = People();
            foreach (JobDriver_SexBaseInitiator d in new JobDriver_SexBaseInitiator[]
                { new JobDriver_Training(), new JobDriver_RitualTraining(), new JobDriver_PE(), new JobDriver_Masturbate() })
            {
                d.pawn = p.a; d.PartnerPawn = d is JobDriver_Masturbate ? p.a : p.b;
                d.job = new Job { targetA = new LocalTargetInfo { Thing = d.PartnerPawn } };
                d.Sexprops = new SexProps { pawn = p.a, partner = d.PartnerPawn }; p.a.jobs.curDriver = d; d.ticks_left = 0;
                SexUtility.ProcessSex(d.Sexprops);
            }
            Assert(SSCPnaSexSupplyUtility.Calls.Count == 0 && TrainerSpecializationProgressUtility.InitiatedSexAwards == 0, "专用流程混入");
        });
    }
}
