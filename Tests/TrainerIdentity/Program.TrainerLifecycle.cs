using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>直接运行生产生命周期代码，覆盖普通退出、终极切换和稳定状态写入。</summary>
    private static void RunTrainerLifecycleTests()
    {
        Run("训导官普通状态只在变化时创建和同步，稳定维护零写入", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            Hediff ordinary = Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
            Assert(ordinary != null && ordinary.Severity == 0.01f);

            pawn.Training.specializationProgress = 0.4f;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(ordinary.Severity == 0.4f);
            int adds = pawn.health.Adds, removes = pawn.health.Removes, writes = ordinary.SeverityWrites;
            for (int i = 0; i < 10; i++) TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(pawn.health.Adds == adds && pawn.health.Removes == removes && ordinary.SeverityWrites == writes);
        });

        Run("训导官失格归档进度并清理普通状态，恢复条件不自动重选", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            pawn.Training.specializationProgress = 0.62f;
            SSCBondUtility.GetChain(pawn).Severity = 0.3f;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None);
            Assert(pawn.Training.ExportSpecializationProgress()["TrainerOfficer"] == 0.62f);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer) == null);
            Assert(pawn.Training.trainerInvalidExitBlocksAdoption);

            SSCBondUtility.GetChain(pawn).Severity = 0.5f;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None);
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            Assert(pawn.Training.specializationProgress == 0.62f);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer) != null);
            Assert(!pawn.Training.trainerInvalidExitBlocksAdoption);
        });

        Run("解绑事务退出普通训导官但保留其历史和个人指派", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            pawn.Training.specializationProgress = 0.35f;
            Pawn assigned = Pawn(PawnIdentity.Master, pawn.Map);
            pawn.Training.selectedTrainer = assigned;
            Assert(SSCBondUtility.Unbind(pawn, false));
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None);
            Assert(pawn.Training.ExportSpecializationProgress()["TrainerOfficer"] == 0.35f);
            Assert(pawn.Training.selectedTrainer == assigned);
        });

        Run("有效终极跨方向失格转禁用，恢复后不修改当前方向", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            SSCBondUtility.GetChain(pawn).Severity = 0.3f;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) == null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) != null);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.Cow);

            SSCBondUtility.GetChain(pawn).Severity = 0.5f;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) != null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) == null);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.Cow);
            int adds = pawn.health.Adds, removes = pawn.health.Removes;
            for (int i = 0; i < 10; i++) TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(pawn.health.Adds == adds && pawn.health.Removes == removes);
        });

        Run("双终极标记按条件归并并清除同系普通状态", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer) == null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) != null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) == null);

            SSCBondUtility.GetChain(pawn).Severity = 0.3f;
            pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) == null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) != null);
            int adds = pawn.health.Adds, removes = pawn.health.Removes;
            for (int i = 0; i < 10; i++) TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(pawn.health.Adds == adds && pawn.health.Removes == removes);
        });

        Run("目标终极标记添加失败时保留原记录", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            Hediff active = pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            SSCBondUtility.GetChain(pawn).Severity = 0.3f;
            pawn.health.FailAdds = true;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) == active);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) == null);
            pawn.health.FailAdds = false;
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) == null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) != null);
        });

        Run("人格恢复作用域与读档通知不处理中间失格状态", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            pawn.Training.specializationProgress = 0.7f;
            pawn.Training.restrictionRestoreDepth++;
            SSCBondUtility.GetChain(pawn).Severity = 0.3f;
            TrainerSpecializationLifecycle.Notify(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer);
            pawn.Training.restrictionRestoreDepth--;

            Scribe.mode = LoadSaveMode.PostLoadInit;
            try { TrainerSpecializationLifecycle.Notify(pawn); }
            finally { Scribe.mode = LoadSaveMode.Inactive; }
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer);
            TrainerSpecializationLifecycle.Maintain(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None);
        });

        Run("绑定事务内暂时失格不退出普通方向，结束后按最终锁链判断", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            pawn.Training.specializationProgress = 0.55f;
            using (TrainerSpecializationLifecycle.BeginMutation(pawn))
            {
                SSCBondUtility.GetChain(pawn).Severity = 0.3f;
                TrainerSpecializationLifecycle.Notify(pawn);
                Assert(pawn.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer);
                SSCBondUtility.GetChain(pawn).Severity = 0.5f;
            }
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer);
            Assert(pawn.Training.specializationProgress == 0.55f);
        });

        Run("原版奴隶身份变化由事件维护退出，终极记录禁用后保留", () =>
        {
            Pawn pawn = EligibleTrainerPawn();
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            pawn.IsSlave = true;
            TrainerSpecializationLifecycle.Notify(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) == null);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) != null);
            pawn.IsSlave = false;
            TrainerSpecializationLifecycle.Notify(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None);
            Assert(Marker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final) != null);
        });

        Run("有效孤儿普通标记可认领，失格孤儿标记只清理", () =>
        {
            Pawn valid = EligibleTrainerPawn();
            Hediff ordinary = valid.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer);
            ordinary.Severity = 0.45f;
            TrainerSpecializationLifecycle.Maintain(valid);
            Assert(valid.Training.specializationType == SexSlaveSpecializationType.TrainerOfficer);
            Assert(valid.Training.specializationProgress == 0.45f);

            Pawn invalid = EligibleTrainerPawn();
            SSCBondUtility.GetChain(invalid).Severity = 0.3f;
            invalid.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer);
            TrainerSpecializationLifecycle.Maintain(invalid);
            Assert(invalid.Training.specializationType == SexSlaveSpecializationType.None);
            Assert(Marker(invalid, SSCDefOf.SSC_Hediff_TrainerOfficer) == null);
        });
    }

    /// <summary>创建已有完整主人绑定和第 3 阶段锁链的测试角色。</summary>
    private static Pawn EligibleTrainerPawn()
    {
        var pair = Setup();
        pair.slave.Training.parent = pair.slave;
        Assert(SSCBondUtility.Bind(pair.master, pair.slave));
        SSCBondUtility.GetChain(pair.slave).Severity = 0.5f;
        return pair.slave;
    }

    /// <summary>只读查找模型健康状态，用于断言生产维护器实际写入的结果。</summary>
    private static Hediff Marker(Pawn pawn, HediffDef def)
    {
        return pawn.health.hediffSet.GetFirstHediffOfDef(def);
    }
}
