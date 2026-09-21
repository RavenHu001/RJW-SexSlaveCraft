using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>用生产工作入口、角色、规则和绑定服务验证阶段3B的许可与关系边界。</summary>
    private static void RunStage3BTests()
    {
        Run("自动工作只由指定者执行，主人手动命令不受第三方指派排除", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var other = Pawn(PawnIdentity.Master, f.slave.Map);
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            f.slave.Training.selectedTrainer = other;
            var work = new WorkGiver_Training();
            Assert(work.JobOnThing(f.master, f.slave, false) == null);
            Assert(work.JobOnThing(f.master, f.slave, true) != null);
            Assert(work.JobOnThing(other, f.slave, false) != null);
            f.slave.Training.selectedTrainer = null;
            Assert(work.JobOnThing(other, f.slave, true) == null && work.JobOnThing(f.master, f.slave, false) == null);
            Assert(work.JobOnThing(f.master, f.slave, true) != null);
        });
        Run("非主人调教只读取调教条目，旧开放和公交车不能放行", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var other = Pawn(PawnIdentity.Slave, f.slave.Map, true); f.slave.Training.selectedTrainer = other;
            f.slave.Training.AllowsOthersForTrainingOrSex = true;
            f.slave.Training.IsBusSpecialized = f.slave.Training.BusState = true;
            var rules = f.slave.Training.restrictionConfig.rules;
            rules.receiveConsensual = rules.receiveForced = true;
            var work = new WorkGiver_Training(); TrainingJobUtility.ValidationCalls = 0;
            Assert(work.JobOnThing(other, f.slave, false) == null && work.JobOnThing(other, f.slave, true) == null);
            Assert(TrainingJobUtility.ValidationCalls == 0);
            rules.receiveTraining = true; rules.receiveConsensual = rules.receiveForced = false;
            Assert(work.JobOnThing(other, f.slave, false) != null);
        });
        Run("停用或工作暂停保留指派，不开放自动接替", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var other = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            f.slave.Training.selectedTrainer = other; f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            other.workSettings.Active = false;
            Assert(TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == other);
            Assert(!new WorkGiver_Training().PotentialWorkThingsGlobal(f.master).Contains(f.slave));
            SSCIdentityUtility.SetTrainerEnabled(other, false);
            Assert(new WorkGiver_Training().JobOnThing(other, f.slave, true) == null);
            Assert(new WorkGiver_Training().JobOnThing(f.master, f.slave, true) != null);
            Assert(f.slave.Training.selectedTrainer == other);
        });
        Run("总开关关闭不解除唯一工作指派或授予初次绑定主人资格", () =>
        {
            var f = Setup(); var other = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            f.slave.Training.selectedTrainer = other;
            Assert(new WorkGiver_Training().JobOnThing(other, f.slave, true) == null);
            SSCBondUtility.Bind(f.master, f.slave); f.slave.Training.selectedTrainer = other;
            Assert(new WorkGiver_Training().JobOnThing(other, f.slave, false) != null);
            Assert(new WorkGiver_Training().JobOnThing(f.master, f.slave, false) == null);
        });
        Run("主人和有效性奴调教员均可主持，双方先后选角一致", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var other = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            f.slave.Training.selectedTrainer = other; f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            var masterRole = new RitualRole_BindingMaster(); var slaveRole = new RitualRole_BindingSlave();
            foreach (var host in new[] { f.master, other })
            {
                Assert(masterRole.AppliesToPawn(host, out _, default));
                Assert(slaveRole.AppliesToPawn(f.slave, out _, default, assignments: new RitualRoleAssignments { Master = host }));
                Assert(slaveRole.AppliesToPawn(f.slave, out _, default));
                Assert(masterRole.AppliesToPawn(host, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
            }
            Assert(!SSCBondUtility.Bind(other, f.slave));
            Assert(SSCBondUtility.GetBoundMaster(f.slave) == f.master && SSCIdentityUtility.IsSexSlave(other));
        });
        Run("空指派、停用指派及损坏配置都不能排除主人主持", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave);
            var off = Pawn(PawnIdentity.Slave, f.slave.Map);
            f.slave.Training.restrictionConfig.version = 999;
            foreach (var selected in new[] { null, off })
            {
                f.slave.Training.selectedTrainer = selected;
                Assert(new RitualRole_BindingMaster().AppliesToPawn(f.master, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
                Assert(new RitualRole_BindingSlave().AppliesToPawn(f.slave, out _, default));
                Assert(new RitualRole_BindingSlave().AppliesToPawn(f.slave, out _, default, assignments: new RitualRoleAssignments { Master = f.master }));
            }
        });
        Run("非主人主持被新调教条目拒绝，普通条目及旧例外无效", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave); var other = Pawn(PawnIdentity.Master, f.slave.Map);
            f.slave.Training.selectedTrainer = other;
            f.slave.Training.AllowsOthersForTrainingOrSex = f.slave.Training.BusState = true;
            Assert(!new RitualRole_BindingMaster().AppliesToPawn(other, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
            Assert(!new RitualRole_BindingSlave().AppliesToPawn(f.slave, out _, default, assignments: new RitualRoleAssignments { Master = other }));
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            Assert(new RitualRole_BindingMaster().AppliesToPawn(other, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
        });
        Run("第三方具主人身份也不能抢主持资格，选角不更改指派与绑定", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave); var other = Pawn(PawnIdentity.Master, f.slave.Map);
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            Assert(!new RitualRole_BindingMaster().AppliesToPawn(other, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
            Assert(!new RitualRole_BindingSlave().AppliesToPawn(f.slave, out _, default, assignments: new RitualRoleAssignments { Master = other }));
            Assert(f.slave.Training.selectedTrainer == f.master && SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        Run("首次绑定准备可通过默认禁止条目，但仅限指定合法主人", () =>
        {
            var f = Setup(); var other = Pawn(PawnIdentity.Master, f.slave.Map);
            var request = SSCRestrictionTrainingUtility.CreateRequest(f.master, f.slave, true);
            Assert(request.Kind == SSCInteractionKind.BindingPreparation);
            Assert(SSCRestrictionTrainingUtility.TryEvaluate(request, false, out var result, out _) && result.Reason != SSCRestrictionReason.BoundOwner);
            Assert(new WorkGiver_Training().JobOnThing(f.master, f.slave, false) != null);
            Assert(!new RitualRole_BindingMaster().AppliesToPawn(other, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
            Assert(SSCBondUtility.GetBoundMaster(f.slave) == null);
        });
        Run("保护装备只强制已声明的条目，不拦截调教", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave); var other = Pawn(PawnIdentity.Master, f.slave.Map);
            f.slave.Training.selectedTrainer = other; f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            var gear = new Apparel(); gear.def.defName = "Protection";
            gear.def.modExtensions.Add(new SSCRestrictionEquipmentExtension { forced = new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny } });
            f.slave.apparel.WornApparel.Add(gear);
            Assert(new WorkGiver_Training().JobOnThing(other, f.slave, false) != null);
            Assert(new RitualRole_BindingMaster().AppliesToPawn(other, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
        });
    }
}
