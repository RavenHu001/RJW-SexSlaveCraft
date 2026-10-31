// AI 辅助生成。执行生产查询、菜单及提交；工作边界模拟外部setter拒绝／回调，非生产算法副本。
using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunTrainerWorkTests()
    {
        Run("未启用工作主人候选只读，显式选择启用并保留原绑定", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            f.master.workSettings.Active = false;
            var option = SSCTrainerAssignmentMenu.Options(f.slave).Last(o => o.Label.StartsWith(f.master.LabelShort));
            Assert(f.master.workSettings.SetCalls == 0 && SSCIdentityUtility.IsTrainer(f.master));
            option.action();
            Assert(f.master.workSettings.RawPriority == 3 && f.slave.Training.selectedTrainer == f.master &&
                !f.slave.Training.restrictionConfig.rules.receiveTraining && SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        Run("已有优先级在数字和勾选模式均不被显式指派改写", () =>
        {
            var f = Setup();
            f.master.workSettings.RawPriority = 2;
            var request = new SSCTrainerWorkUtility.Request(f.slave, f.master);
            Assert(request.TrySubmit(out _) && f.master.workSettings.RawPriority == 2 && f.master.workSettings.SetCalls == 0);
            f.master.workSettings.UsePriorities = false;
            Assert(new SSCTrainerWorkUtility.Request(f.slave, f.master).TrySubmit(out _) &&
                f.master.workSettings.RawPriority == 2 && f.master.workSettings.SetCalls == 0);
        });
        Run("合格训导官未启用工作可指派并授权，不替代任职门槛", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var officer = QualifiedTrainer(f.slave.Map); officer.LabelShort = "Officer"; officer.workSettings.Active = false;
            SSCTrainerAssignmentMenu.Options(f.slave).Single(o => o.Label.StartsWith("Officer")).action();
            Assert(officer.workSettings.Active && f.slave.Training.selectedTrainer == officer && f.slave.Training.restrictionConfig.rules.receiveTraining);
            officer.Training.slaveTrainerEnabled = false;
            Assert(!SSCTrainerWorkUtility.CanConsider(f.slave, officer) && f.slave.Training.selectedTrainer == officer);
        });
        Run("原版工作禁止和未初始化均显示禁用且查询不初始化", () =>
        {
            var f = Setup(); f.master.WorkDisabled = true;
            Assert(SSCTrainerAssignmentMenu.Options(f.slave).Last(o => o.Label.StartsWith(f.master.LabelShort)).action == null);
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, f.master).TrySubmit(out _));
            f.master.WorkDisabled = false; f.master.workSettings.Initialized = false;
            Assert(SSCTrainerAssignmentMenu.Options(f.slave).Last(o => o.Label.StartsWith(f.master.LabelShort)).action == null &&
                !f.master.workSettings.Initialized && f.master.workSettings.SetCalls == 0);
        });
        Run("已有指派就地启用不授权，关闭和清空不删除其他工作安排", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var other = Pawn(PawnIdentity.Master, f.slave.Map);
            f.slave.Training.selectedTrainer = other; other.workSettings.Active = false;
            var rules = f.slave.Training.restrictionConfig.rules;
            Assert(TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == other);
            SSCTrainerAssignmentMenu.EnableOption(f.slave).action();
            Assert(other.workSettings.Active && ReferenceEquals(rules, f.slave.Training.restrictionConfig.rules) && !rules.receiveTraining);
            rules.receiveTraining = true;
            SSCTrainerAssignmentMenu.Options(f.slave).Last().action();
            Assert(f.slave.Training.selectedTrainer == null && other.workSettings.Active);
        });
        Run("就地启用迟到换指定者后不打开原调教员工作", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave)); f.master.workSettings.Active = false;
            var option = SSCTrainerAssignmentMenu.EnableOption(f.slave);
            f.slave.Training.selectedTrainer = Pawn(PawnIdentity.Master, f.slave.Map);
            option.action(); Assert(!f.master.workSettings.Active);
        });
        foreach (bool source in new[] { false, true }) Run("同Comp换人格使旧指派并启用失效 " + source, () =>
        {
            var f = Setup(); f.master.workSettings.Active = false;
            var request = new SSCTrainerWorkUtility.Request(f.slave, f.master);
            Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(source ? f.slave : f.master, out var scope)); scope.Dispose();
            Assert(!request.TrySubmit(out _) && !f.master.workSettings.Active);
        });
        Run("工作设置载体或菜单后工作状态变化拒绝旧请求", () =>
        {
            var f = Setup(); var request = new SSCTrainerWorkUtility.Request(f.slave, f.master);
            f.master.workSettings = new Pawn_WorkSettings { RawPriority = 0 };
            Assert(!request.TrySubmit(out _) && !f.master.workSettings.Active);
            request = new SSCTrainerWorkUtility.Request(f.slave, f.master);
            f.master.workSettings.RawPriority = 1;
            Assert(!request.TrySubmit(out _) && f.master.workSettings.RawPriority == 1);
        });
        Run("启用后外部强制禁止使指派失败并撤回本次工作启用", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var other = Pawn(PawnIdentity.Master, f.slave.Map); other.workSettings.Active = false;
            var rules = f.slave.Training.restrictionConfig.rules;
            other.workSettings.AfterSet = n => { if (n != 0) ForceDenyTraining(f.slave); };
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, other).TrySubmit(out _) && !other.workSettings.Active &&
                f.slave.Training.selectedTrainer == f.master && ReferenceEquals(rules, f.slave.Training.restrictionConfig.rules) && !rules.receiveTraining);
        });
        Run("工作setter拒绝和写后异常均不留下指派或工作半成品", () =>
        {
            var f = Setup(); f.master.workSettings.Active = false; f.master.workSettings.IgnoreEnable = true;
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, f.master).TrySubmit(out _) && !f.master.workSettings.Active && f.slave.Training.selectedTrainer == f.master);
            f.master.workSettings.IgnoreEnable = false;
            f.master.workSettings.AfterSet = n => { if (n != 0) throw new InvalidOperationException("external setter failure"); };
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, f.master).TrySubmit(out _) && !f.master.workSettings.Active && f.slave.Training.selectedTrainer == f.master);
        });
        Run("工作setter重入拒绝且失败收尾不覆盖外部新优先级", () =>
        {
            var f = Setup(); f.master.workSettings.Active = false; bool nested = true;
            f.master.workSettings.UsePriorities = false;
            f.master.workSettings.AfterSet = n =>
            {
                if (n == 0) return;
                nested = new SSCTrainerWorkUtility.Request(f.slave, f.master).TrySubmit(out _);
                f.master.workSettings.RawPriority = 1;
            };
            Assert(!new SSCTrainerWorkUtility.Request(f.slave, f.master).TrySubmit(out _) && !nested &&
                f.master.workSettings.RawPriority == 1 && f.slave.Training.selectedTrainer == f.master);
        });
    }

    private static void ForceDenyTraining(Pawn target)
    {
        var gear = new Apparel(); gear.def.modExtensions.Add(new SSCRestrictionEquipmentExtension
            { forced = new SSCRestrictionOverrides { receiveTraining = SSCRestrictionValue.Deny } });
        target.apparel.WornApparel.Add(gear);
    }
}
