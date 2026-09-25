using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed;
    private static int failed;

    /// <summary>以同一次恢复事务贯通生产进度、绑定、凝胶标签、恢复作用域和资格维护。</summary>
    private static int Main()
    {
        Run("普通人格恢复的临时无绑定不退出方向或混入宿主进度", OrdinaryRestoreDefersIntermediateState);
        Run("普通人格最终失格会退出方向并保留源历史", IneligibleOrdinaryRestoreKeepsHistory);
        foreach (bool sourceDisabled in new[] { false, true })
        foreach (bool hostEligible in new[] { false, true })
        {
            Run($"终极人格源禁用={sourceDisabled}、宿主合格={hostEligible}按最终身体条件恢复",
                () => FinalRestoreUsesHostConditions(sourceDisabled, hostEligible));
        }
        Run("没有源标签的恢复清除宿主全部训导官状态", MissingSourceTagsClearHost);
        Run("零进度人格恢复不从普通标签显示下限取得经验", ZeroProgressRemainsZero);
        Run("嵌套恢复仅最外层结束时转换一次且重复释放无副作用", NestedRestoreMaintainsAtOuterBoundary);
        Run("恢复异常退出仍释放深度并维护当前保留状态", ExceptionalRestoreReleasesScope);
        Console.WriteLine($"{passed}/{passed + failed} passed (production trainer restoration integration; game interface model).");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>创建组件已连接身体的对象；主从身份通过生产 setter 写入。</summary>
    private static Pawn Pawn(PawnIdentity identity)
    {
        var pawn = new Pawn();
        pawn.Training.parent = pawn;
        Assert(SSCIdentityUtility.TrySetIdentity(pawn, identity), "准备对象身份失败");
        return pawn;
    }

    /// <summary>通过真实绑定事务准备第三阶段锁链，再用生产入口开始培养。</summary>
    private static Pawn OrdinaryPawn(float progress, Pawn master = null)
    {
        Pawn pawn = Pawn(PawnIdentity.Slave);
        Assert(SSCBondUtility.Bind(master ?? Pawn(PawnIdentity.Master), pawn), "准备绑定失败");
        SSCBondUtility.GetChain(pawn).Severity = 0.5f;
        pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
        pawn.Training.specializationProgress = progress;
        TrainerSpecializationLifecycle.Maintain(pawn);
        return pawn;
    }

    /// <summary>源进度使用真实导出，互斥标签使用真实采集；不在测试里重写状态优先级。</summary>
    private static CompPersonalityStore Capture(Pawn source)
    {
        var data = new CompPersonalityStore
        {
            specializationType = source.Training.specializationType,
            specializationProgress = source.Training.specializationProgress,
            specializationProgressByType = source.Training.ExportSpecializationProgress()
        };
        TrainerSpecializationGelUtility.StoreExclusiveTrainerTags(data, source);
        return data;
    }

    /// <summary>复现 ExcretionUtility 中与本方向有关的清理和导入顺序；外层作用域由调用者持有。</summary>
    private static void RestoreBeforeBinding(Pawn host, CompPersonalityStore data)
    {
        // 植入先解绑并清掉旧身份，此时持续条件故意不成立，生产维护器必须尊重恢复深度。
        SSCBondUtility.Unbind(host);
        Assert(SSCIdentityUtility.TrySetIdentity(host, PawnIdentity.Unset), "宿主清理身份失败");
        TrainerSpecializationGelUtility.RemoveAllTrainerStates(host);

        // 当前方向和各方向历史整体替换；SetSpecialization 自身会通知真实维护器。
        host.Training.RestoreSpecializationProgress(data.specializationType,
            data.specializationProgress, data.specializationProgressByType);
        Assert(SSCIdentityUtility.TrySetIdentity(host, PawnIdentity.Slave), "恢复源身份失败");
    }

    /// <summary>在延迟作用域内重建源主人和锁链；低严重度的中间状态不能退出培养。</summary>
    private static void RestoreBinding(Pawn host, Pawn master)
    {
        Assert(SSCBondUtility.Bind(master, host, true), "恢复源绑定失败");
        SSCBondUtility.GetChain(host).Severity = 0.5f;
    }

    /// <summary>应用真实标签并显式模拟健康事件通知；通知必须受到真实恢复深度保护。</summary>
    private static void ApplyTags(Pawn host, CompPersonalityStore data)
    {
        TrainerSpecializationGelUtility.ApplyExclusiveTrainerTags(host, data);
        TrainerSpecializationLifecycle.Notify(host);
    }

    /// <summary>普通方向和历史在临时失格阶段保持源值，恢复结束后只采用源严重度。</summary>
    private static void OrdinaryRestoreDefersIntermediateState()
    {
        Pawn source = OrdinaryPawn(0.35f);
        source.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
        source.Training.specializationProgress = 0.4f;
        source.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
        CompPersonalityStore data = Capture(source);
        Pawn host = OrdinaryPawn(0.8f);
        host.Training.SetSpecialization(SexSlaveSpecializationType.Bus);
        host.Training.specializationProgress = 0.9f;
        host.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
        // 同时留下宿主终极标记，确认源人格不继承宿主的完成记录。
        host.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);

        using (SSCRestrictionLifecycle.BeginRestore(host))
        {
            RestoreBeforeBinding(host, data);
            TrainerSpecializationLifecycle.Maintain(host);
            Assert(SSCBondUtility.GetBoundMaster(host) == null, "必须覆盖临时无绑定状态");
            Assert(host.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer,
                "临时无绑定不能使源方向退出");
            Assert(host.Training.specializationProgress == 0.35f, "宿主进度不能混入源人格");
            RestoreBinding(host, SSCBondUtility.GetBoundMaster(source));
            ApplyTags(host, data);
        }

        Assert(host.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer,
            "完整恢复后应继续普通培养");
        Assert(host.Training.specializationProgress == 0.35f && Marker(host, Ordinary)?.Severity == 0.35f,
            "普通进度与标签必须保留源值");
        Dictionary<string, float> history = host.Training.ExportSpecializationProgress();
        Assert(history["Cow"] == 0.4f && !history.ContainsKey("Bus"), "源历史整体替换宿主历史");
        Assert(!TrainerSpecializationUtility.HasFinalRecord(host), "宿主终极事实不能遗留");
    }

    /// <summary>最终仍无主时退出普通培养，但以后重新满足条件可从源历史恢复。</summary>
    private static void IneligibleOrdinaryRestoreKeepsHistory()
    {
        CompPersonalityStore data = Capture(OrdinaryPawn(0.35f));
        Pawn host = OrdinaryPawn(0.8f);
        using (SSCRestrictionLifecycle.BeginRestore(host))
        {
            RestoreBeforeBinding(host, data);
            ApplyTags(host, data);
            Assert(host.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer,
                "最终失格检查应延迟到作用域结束");
        }
        Assert(host.Training.specializationType == SexSlaveSpecializationType.None && Marker(host, Ordinary) == null,
            "最终无主身体不能继续普通培养");
        Assert(host.Training.ExportSpecializationProgress()["TrainerOfficer"] == 0.35f,
            "退出必须归档源进度而非清零或混入宿主进度");

        // 后续重新建立完整条件，玩家再选择本方向时仍得到保存的普通进度。
        RestoreBinding(host, Pawn(PawnIdentity.Master));
        host.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
        Assert(host.Training.specializationProgress == 0.35f, "重新选择应恢复源进度");
    }

    /// <summary>源终极记录可跨当前方向迁移，最终有效性由接收身体自由身份及绑定决定。</summary>
    private static void FinalRestoreUsesHostConditions(bool sourceDisabled, bool hostEligible)
    {
        Pawn source = OrdinaryPawn(1f);
        source.health.AddHediff(ActiveFinal);
        source.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
        source.Training.specializationProgress = 0.4f;
        if (sourceDisabled) source.IsSlave = true;
        TrainerSpecializationLifecycle.Maintain(source);
        CompPersonalityStore data = Capture(source);
        Assert(data.HasTag(sourceDisabled ? DisabledFinal : ActiveFinal), "快照应保留源完成状态");

        Pawn host = OrdinaryPawn(0.8f);
        host.health.AddHediff(sourceDisabled ? ActiveFinal : DisabledFinal);
        host.IsSlave = !hostEligible;
        using (SSCRestrictionLifecycle.BeginRestore(host))
        {
            RestoreBeforeBinding(host, data);
            RestoreBinding(host, SSCBondUtility.GetBoundMaster(source));
            ApplyTags(host, data);
        }

        Assert(Marker(host, hostEligible ? ActiveFinal : DisabledFinal) != null
            && Marker(host, hostEligible ? DisabledFinal : ActiveFinal) == null && Marker(host, Ordinary) == null,
            "仅保留符合接收身体条件的一种终极标签");
        Assert(TrainerSpecializationUtility.HasActiveFinalEffect(host) == hostEligible,
            "实际终极资格必须与最终标签一致");
        Assert(host.Training.specializationType == SexSlaveSpecializationType.Cow
            && host.Training.specializationProgress == 0.4f,
            "终极恢复不能修改源人格当前其他方向");
        Assert(host.Training.ExportSpecializationProgress()["TrainerOfficer"] == 1f,
            "源训导官历史不能被宿主覆盖");
    }

    /// <summary>空方向、空标签的源人格不能重新认领宿主遗留的普通或终极事实。</summary>
    private static void MissingSourceTagsClearHost()
    {
        Pawn host = OrdinaryPawn(0.8f);
        host.health.AddHediff(ActiveFinal);
        host.health.AddHediff(DisabledFinal);
        var data = new CompPersonalityStore { specializationProgressByType = new Dictionary<string, float>() };
        using (SSCRestrictionLifecycle.BeginRestore(host))
        {
            RestoreBeforeBinding(host, data);
            RestoreBinding(host, Pawn(PawnIdentity.Master));
            ApplyTags(host, data);
        }
        Assert(host.Training.specializationType == SexSlaveSpecializationType.None
            && host.Training.ExportSpecializationProgress().Count == 0,
            "空源人格不能保留宿主方向和历史");
        Assert(!host.health.hediffSet.hediffs.Any(hediff => TrainerSpecializationGelUtility.IsTrainerTag(hediff.def as HediffDef)),
            "清理源为空的三个宿主标签后不能重新认领");
    }

    /// <summary>选择时的 0.01 标签下限只供显示，跨体恢复仍须保持零经验。</summary>
    private static void ZeroProgressRemainsZero()
    {
        Pawn source = OrdinaryPawn(0f);
        CompPersonalityStore data = Capture(source);
        Pawn host = OrdinaryPawn(0.8f);
        using (SSCRestrictionLifecycle.BeginRestore(host))
        {
            RestoreBeforeBinding(host, data);
            RestoreBinding(host, SSCBondUtility.GetBoundMaster(source));
            ApplyTags(host, data);
        }
        Assert(host.Training.specializationProgress == 0f && Marker(host, Ordinary)?.Severity == 0.01f,
            "显示下限不得进入源普通进度");
    }

    /// <summary>用标签增删与通知次数证明内层释放不维护，外层仅转换一次且支持幂等释放。</summary>
    private static void NestedRestoreMaintainsAtOuterBoundary()
    {
        Pawn source = OrdinaryPawn(1f);
        source.health.AddHediff(ActiveFinal);
        TrainerSpecializationLifecycle.Maintain(source);
        CompPersonalityStore data = Capture(source);
        Pawn host = Pawn(PawnIdentity.Slave);
        IDisposable outer = SSCRestrictionLifecycle.BeginRestore(host);
        IDisposable inner = SSCRestrictionLifecycle.BeginRestore(host);
        RestoreBeforeBinding(host, data);
        ApplyTags(host, data);
        int adds = host.health.Adds, removes = host.health.Removes;
        int notifications = SSCRestrictionGameComponent.Notifications;

        inner.Dispose();
        TrainerSpecializationLifecycle.Notify(host);
        Assert(host.Training.restrictionRestoreDepth == 1 && Marker(host, ActiveFinal) != null
            && host.health.Adds == adds && host.health.Removes == removes,
            "内层释放或中途事件不能处理暂时无主的有效标签");
        Assert(SSCRestrictionGameComponent.Notifications == notifications, "内层不得发出最终刷新通知");

        outer.Dispose();
        Assert(host.Training.restrictionRestoreDepth == 0 && Marker(host, DisabledFinal) != null
            && Marker(host, ActiveFinal) == null, "最外层结束须转换最终无主的终极状态");
        Assert(host.health.Adds == adds + 1 && host.health.Removes == removes + 1,
            "一次最终转换只能增加禁用并移除有效各一次");
        Assert(SSCRestrictionGameComponent.Notifications == notifications + 1, "最终刷新只能通知一次");
        outer.Dispose();
        inner.Dispose();
        TrainerSpecializationLifecycle.Maintain(host);
        Assert(host.Training.restrictionRestoreDepth == 0 && host.health.Adds == adds + 1
            && host.health.Removes == removes + 1 && SSCRestrictionGameComponent.Notifications == notifications + 1,
            "重复释放和稳定维护不得重复转换或使深度变负");
    }

    /// <summary>模拟恢复过程中异常；using 调用真实 Dispose，不能永久抑制后续维护。</summary>
    private static void ExceptionalRestoreReleasesScope()
    {
        Pawn host = OrdinaryPawn(1f);
        host.health.AddHediff(ActiveFinal);
        TrainerSpecializationLifecycle.Maintain(host);
        try
        {
            using (SSCRestrictionLifecycle.BeginRestore(host))
            {
                SSCBondUtility.Unbind(host);
                throw new InvalidOperationException("test interruption");
            }
        }
        catch (InvalidOperationException) { }
        Assert(host.Training.restrictionRestoreDepth == 0 && !host.Training.trainerMaintenanceInProgress,
            "异常退出必须解除维护抑制");
        Assert(Marker(host, DisabledFinal) != null && Marker(host, ActiveFinal) == null,
            "异常退出按当前残留身体条件保存禁用完成事实");
        RestoreBinding(host, Pawn(PawnIdentity.Master));
        TrainerSpecializationLifecycle.Notify(host);
        Assert(TrainerSpecializationUtility.HasActiveFinalEffect(host), "恢复正常绑定后仍可再次维护");
    }

    private static HediffDef Ordinary => SSCDefOf.SSC_Hediff_TrainerOfficer;
    private static HediffDef ActiveFinal => SSCDefOf.SSC_Hediff_TrainerOfficer_Final;
    private static HediffDef DisabledFinal => SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled;
    private static Hediff Marker(Pawn pawn, HediffDef def) => pawn.health.hediffSet.GetFirstHediffOfDef(def);

    /// <summary>重置外围统计，各用例新建 Pawn，避免历史关系共享造成误通过。</summary>
    private static void Run(string name, Action test)
    {
        Scribe.mode = LoadSaveMode.Inactive;
        SSCRestrictionGameComponent.Notifications = 0;
        SSCMod.settings = new Settings();
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + ": " + exception); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
