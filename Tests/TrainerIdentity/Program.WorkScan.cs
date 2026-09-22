using System.Linq;
using SexSlaveCraft;
using Verse.AI;
using Verse;

internal static partial class Program
{
    /// <summary>验证生产工作入口的查询、准备与创建边界；不在测试中复制许可或目标筛选算法。</summary>
    private static void RunWorkScanTests()
    {
        Run("候选枚举无恢复和兼容副作用，完整查询无任务分配", () =>
        {
            var f = Setup(); var work = new WorkGiver_Training();
            f.slave.Training.isBeingTrained = true;
            BindingRitualStateUtility.RecoveryCalls = TrainingJobUtility.ValidationCalls = JobMaker.CreatedJobs = 0;
            Assert(work.PotentialWorkThingsGlobal(f.master).Contains(f.slave));
            Assert(f.slave.Training.isBeingTrained && BindingRitualStateUtility.RecoveryCalls == 0
                && TrainingJobUtility.ValidationCalls == 0 && JobMaker.CreatedJobs == 0);
            Assert(work.HasJobOnThing(f.master, f.slave));
            Assert(!f.slave.Training.isBeingTrained && BindingRitualStateUtility.RecoveryCalls == 1
                && TrainingJobUtility.ValidationCalls == 1 && JobMaker.CreatedJobs == 0);
            Assert(work.JobOnThing(f.master, f.slave)?.target == f.slave && JobMaker.CreatedJobs == 1);
        });
        Run("候选允许留待完整许可判断，拒绝不执行身体兼容准备", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var trainer = Pawn(PawnIdentity.Master, f.slave.Map); f.slave.Training.selectedTrainer = trainer;
            var work = new WorkGiver_Training(); TrainingJobUtility.ValidationCalls = JobMaker.CreatedJobs = 0;
            Assert(work.PotentialWorkThingsGlobal(trainer).Contains(f.slave));
            Assert(!work.HasJobOnThing(trainer, f.slave) && work.JobOnThing(trainer, f.slave) == null);
            Assert(TrainingJobUtility.ValidationCalls == 0 && JobMaker.CreatedJobs == 0);
        });
        Run("查询通过后撤销条目许可，创建阶段重新验证而不复用旧结果", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var trainer = Pawn(PawnIdentity.Master, f.slave.Map); f.slave.Training.selectedTrainer = trainer;
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            var work = new WorkGiver_Training(); TrainingJobUtility.ValidationCalls = JobMaker.CreatedJobs = 0;
            Assert(work.HasJobOnThing(trainer, f.slave) && TrainingJobUtility.ValidationCalls == 1);
            f.slave.Training.restrictionConfig.rules.receiveTraining = false;
            Assert(work.JobOnThing(trainer, f.slave) == null && JobMaker.CreatedJobs == 0
                && TrainingJobUtility.ValidationCalls == 1);
        });
        Run("完整准备保留有效接收任务占用且拒绝重复启动", () =>
        {
            var f = Setup(); f.slave.Training.isBeingTrained = true; f.slave.CurJobDef = SSCDefOf.SSC_TrainingReceiver;
            TrainingJobUtility.ValidationCalls = JobMaker.CreatedJobs = 0;
            Assert(!new WorkGiver_Training().HasJobOnThing(f.master, f.slave, true));
            Assert(f.slave.Training.isBeingTrained && JobFailReason.Last == Strings.Train_Reason_AlreadyBeingTrained
                && TrainingJobUtility.ValidationCalls == 0 && JobMaker.CreatedJobs == 0);
        });
        Run("明确非主人指派只授权调教，普通双人拒绝保持有效", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var trainer = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, trainer));
            Assert(f.slave.Training.restrictionConfig.rules.receiveTraining && f.slave.Training.selectedTrainer == trainer);
            Assert(new WorkGiver_Training().HasJobOnThing(trainer, f.slave));
            Assert(!SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(trainer, f.slave, SSCInteractionKind.Consensual, true)).Allowed);
            Assert(!SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(trainer, f.slave, SSCInteractionKind.Forced, true)).Allowed);
        });
        Run("装备强制禁止阻止非主人指派，不留下部分授权", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var trainer = Pawn(PawnIdentity.Master, f.slave.Map);
            var original = f.slave.Training.restrictionConfig.rules;
            var gear = new Apparel(); gear.def.modExtensions.Add(new SSCRestrictionEquipmentExtension
                { forced = new SSCRestrictionOverrides { receiveTraining = SSCRestrictionValue.Deny } });
            f.slave.apparel.WornApparel.Add(gear);
            Assert(!TrainerAssignmentUtility.CanAssignTrainerTo(f.slave, trainer));
            Assert(!SSCBondUtility.TryAssignTrainer(f.slave, trainer));
            Assert(f.slave.Training.selectedTrainer == f.master && ReferenceEquals(original, f.slave.Training.restrictionConfig.rules)
                && !original.receiveTraining);
        });
        Run("失格候选不能通过明确指派取得调教授权", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var trainer = Pawn(PawnIdentity.Unset, f.slave.Map);
            Assert(!SSCBondUtility.TryAssignTrainer(f.slave, trainer));
            Assert(f.slave.Training.selectedTrainer == f.master && !f.slave.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("未绑定指派不初始化或修改个人限制配置", () =>
        {
            var f = Setup(); f.slave.Training.restrictionConfig = null;
            var trainer = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, trainer));
            Assert(f.slave.Training.selectedTrainer == trainer && f.slave.Training.restrictionConfig == null);
        });
        Run("停用限制时明确指派仍保存调教授权且不修改普通项目", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            var trainer = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, trainer));
            var rules = f.slave.Training.restrictionConfig.rules;
            Assert(rules.receiveTraining && !rules.receiveConsensual && !rules.receiveForced);
            SSCMod.settings.enableSexSlaveProtectionRules = true;
            Assert(new WorkGiver_Training().HasJobOnThing(trainer, f.slave));
        });
        Run("停用限制后缺失或损坏配置不阻止有效指派，也不隐式修复配置", () =>
        {
            foreach (bool missing in new[] { true, false })
            {
                var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
                SSCMod.settings.enableSexSlaveProtectionRules = false;
                var trainer = Pawn(PawnIdentity.Master, f.slave.Map);
                SSCRestrictionConfig original = missing ? null : new SSCRestrictionConfig { version = 99 };
                f.slave.Training.restrictionConfig = original;
                Assert(SSCBondUtility.TryAssignTrainer(f.slave, trainer));
                Assert(f.slave.Training.selectedTrainer == trainer && ReferenceEquals(original, f.slave.Training.restrictionConfig));
                if (!missing) Assert(original.version == 99 && !original.rules.receiveTraining);
            }
        });
        Run("恢复事务内解绑仍清理原主人指派，保留第三方或明确不清理的指派", () =>
        {
            foreach (bool thirdParty in new[] { false, true })
            foreach (bool clearAssignment in new[] { false, true })
            {
                var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
                var assigned = thirdParty ? Pawn(PawnIdentity.Master, f.slave.Map) : f.master;
                f.slave.Training.selectedTrainer = assigned;
                f.slave.Training.restrictionRestoreDepth = 1;
                Assert(SSCBondUtility.Unbind(f.slave, clearAssignment));
                Assert(SSCBondUtility.GetBoundMaster(f.slave) == null);
                Assert(f.slave.Training.selectedTrainer == (clearAssignment && !thirdParty ? null : assigned));
                Assert(f.slave.Training.restrictionRestoreDepth == 1);
            }
        });
    }
}
