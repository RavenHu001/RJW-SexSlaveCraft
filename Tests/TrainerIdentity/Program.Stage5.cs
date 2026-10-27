using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>阶段 5 直接验证生产选择判断和界面状态模型，并核对四语实际键文件。</summary>
    private static void RunStage5Tests(string repo)
    {
        Run("训导官菜单资格随研究、身份、绑定和锁链即时变化", () =>
        {
            // 从缺失角色开始，逐步满足持续条件；所有拒绝原因都由生产判断产生。
            Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(null, out var reason)
                && reason == TrainerSpecializationFailure.MissingPawnOrTrainingComp);
            Pawn pawn = Pawn(PawnIdentity.Master);
            Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                && reason == TrainerSpecializationFailure.NotSexSlave);
            pawn.Training.pawnIdentity = PawnIdentity.Slave;
            pawn.IsColonist = false;
            Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                && reason == TrainerSpecializationFailure.NotFreeColonist);
            pawn.IsColonist = true;
            Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                && reason == TrainerSpecializationFailure.NoBoundMaster);

            // 用真实绑定服务建链；研究是选择门槛，不参与培养后的持续条件。
            Assert(SSCBondUtility.Bind(Pawn(PawnIdentity.Master), pawn));
            var chain = SSCBondUtility.GetChain(pawn);
            chain.Severity = 0.3f;
            Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                && reason == TrainerSpecializationFailure.ChainStageTooLow);
            chain.Severity = 0.5f;
            ResearchUtils.TrainerOfficerResearchFinished = false;
            try
            {
                Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                    && reason == TrainerSpecializationFailure.ResearchNotFinished);
                ResearchUtils.TrainerOfficerResearchFinished = true;
                Assert(TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                    && reason == TrainerSpecializationFailure.None);
                // 菜单打开时合格，点击前退阶时同一方法必须拒绝提交。
                chain.Severity = 0.49f;
                Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out reason)
                    && reason == TrainerSpecializationFailure.ChainStageTooLow);
            }
            finally { ResearchUtils.TrainerOfficerResearchFinished = true; }
        });

        Run("训导官显示区分普通阶段、终极记录与即时禁用", () =>
        {
            // 起始无方向，当前训导官方向按 20% 和 100% 显示不同阶段。
            Pawn pawn = QualifiedTrainerPawn();
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.None);
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.Training);
            pawn.Training.specializationProgress = 0.1999f;
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.Training);
            pawn.Training.specializationProgress = 0.2f;
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.BasicUnlocked);
            pawn.Training.specializationProgress = 1f;
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.OrdinaryComplete);

            // 完成记录可跨方向保留；条件退阶时读界面立即显示禁用，不等低频维护。
            pawn.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.None);
            pawn.health.hediffSet.hediffs.Add(new Hediff { def = SSCDefOf.SSC_Hediff_TrainerOfficer_Final });
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.Finalized);
            Assert(!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn, out var reason)
                && reason == TrainerSpecializationFailure.AlreadyFinalized);
            SSCBondUtility.GetChain(pawn).Severity = 0.3f;
            Assert(TrainerSpecializationUtility.GetDisplayState(pawn) == TrainerSpecializationDisplayState.FinalDisabled);
        });
    }
}
