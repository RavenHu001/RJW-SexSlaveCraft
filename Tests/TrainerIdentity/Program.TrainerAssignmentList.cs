// AI 辅助生成。覆盖多人指派提交与现有生命周期。
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunTrainerAssignmentListTests()
    {
        Run("名单第二人可通过日常工作和首次建绑准入，非成员仍被拒绝", () =>
        {
            var f = Setup();
            Pawn second = Pawn(PawnIdentity.Master, f.slave.Map);
            Pawn outsider = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(new SSCTrainerWorkUtility.Request(f.slave, second, SSCTrainerAssignmentOperation.Add).TrySubmit(out _));
            f.master.workSettings.Active = false;
            var work = new SexSlaveCraft.WorkGiver_Training();
            Assert(work.PotentialWorkThingsGlobal(second).Contains(f.slave));
            Assert(work.JobOnThing(second, f.slave)?.target == f.slave);
            Assert(!work.HasJobOnThing(outsider, f.slave, true));
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            f.slave.Training.ReplaceAssignedTrainers(new[] { second });
            Assert(work.HasJobOnThing(second, f.slave) && work.HasJobOnThing(f.master, f.slave, true));
            Assert(!work.HasJobOnThing(f.master, f.slave));
        });

        Run("追加到三人后逐人启用和撤销，旧单人入口不能覆盖名单", () =>
        {
            var f = Setup();
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            Pawn second = Pawn(PawnIdentity.Master, f.slave.Map);
            Pawn third = Pawn(PawnIdentity.Master, f.slave.Map);
            var comp = f.slave.Training;
            second.workSettings.RawPriority = 0;
            Assert(new SSCTrainerWorkUtility.Request(f.slave, second, SSCTrainerAssignmentOperation.Add).TrySubmit(out _));
            Assert(second.workSettings.RawPriority == 3 && comp.restrictionConfig.rules.receiveTraining);
            Assert(!SSCTrainerWorkUtility.CanConsiderForAdd(f.slave, second));
            Assert(SSCTrainerWorkUtility.CanConsiderForAdd(f.slave, third));

            var stale = new SSCTrainerWorkUtility.Request(f.slave, second, SSCTrainerAssignmentOperation.Remove);
            Assert(new SSCTrainerWorkUtility.Request(f.slave, third, SSCTrainerAssignmentOperation.Add).TrySubmit(out _));
            Assert(!stale.TrySubmit(out _) && !SSCBondUtility.TryAssignTrainer(f.slave, null));
            Assert(comp.CopyAssignedTrainers().SequenceEqual(new[] { f.master, second, third }));

            second.workSettings.RawPriority = 0;
            third.workSettings.RawPriority = 0;
            Assert(new SSCTrainerWorkUtility.Request(f.slave, second, SSCTrainerAssignmentOperation.EnableWork).TrySubmit(out _));
            Assert(second.workSettings.RawPriority == 3 && third.workSettings.RawPriority == 0);
            second.Downed = true;
            Assert(new SSCTrainerWorkUtility.Request(f.slave, second, SSCTrainerAssignmentOperation.Remove).TrySubmit(out _));
            Assert(comp.CopyAssignedTrainers().SequenceEqual(new[] { f.master, third }));
            Assert(comp.restrictionConfig.rules.receiveTraining && SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });

        Run("名单恢复原内容仍拒绝旧指派及其工作启用", () =>
        {
            var f = Setup();
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            other.workSettings.RawPriority = 0;
            var add = new SSCTrainerWorkUtility.Request(f.slave, other, SSCTrainerAssignmentOperation.Add);
            f.slave.Training.ClearAssignedTrainers();
            f.slave.Training.ReplaceAssignedTrainers(new[] { f.master });
            Assert(!add.TrySubmit(out _) && other.workSettings.RawPriority == 0);
            Assert(f.slave.Training.CopyAssignedTrainers().SequenceEqual(new[] { f.master }));
        });

        Run("只准主人时拒绝逐人移除锁定主人", () =>
        {
            var f = Setup();
            Pawn officer = QualifiedTrainer(f.slave.Map);
            Assert(SSCIdentityUtility.IsTrainer(officer) && !SSCIdentityUtility.IsMaster(officer));
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, officer, SSCTrainerAssignmentOperation.Add).TrySubmit(out _));
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            Assert(f.slave.Training.CopyAssignedTrainers().SequenceEqual(new[] { f.master }));
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, f.master, SSCTrainerAssignmentOperation.Remove).TrySubmit(out _));
        });

        Run("多人解绑保留第三方，退出身份清理名单且副本独立", () =>
        {
            var f = Setup();
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(new SSCTrainerWorkUtility.Request(f.slave, other, SSCTrainerAssignmentOperation.Add).TrySubmit(out _));
            Pawn clone = Pawn(PawnIdentity.Slave, f.slave.Map);
            clone.Training.ReplaceAssignedTrainers(f.slave.Training.CopyAssignedTrainers());
            Assert(SSCBondUtility.Unbind(f.slave));
            Assert(f.slave.Training.CopyAssignedTrainers().SequenceEqual(new[] { other }));
            Assert(clone.Training.CopyAssignedTrainers().SequenceEqual(new[] { f.master, other }));
            Assert(SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Unset) && f.slave.Training.AssignedTrainerCount == 0);
        });
    }
}
