// AI 辅助生成。菜单、候选、许可与原子指派均链接生产源码，不用界面替身模拟授权结果。
using System;
using System.Linq;
using SexSlaveCraft;
using Verse;

namespace Verse
{
    public enum ProgramState { Entry, Playing }
    public static class Current { public static Game Game = new Game(); public static ProgramState ProgramState = ProgramState.Playing; }
    public class FloatMenuOption
    {
        public readonly string Label; public readonly Action action;
        public string tooltip;
        public FloatMenuOption(string label, Action action) { Label = label; this.action = action; }
    }
}

internal static partial class Program
{
    private static void RunTrainerMenuTests()
    {
        Run("共用菜单枚举候选不提交指派或调教授权", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var other = Pawn(PawnIdentity.Master, f.slave.Map); other.LabelShort = "Replacement";
            var rules = f.slave.Training.restrictionConfig.rules;
            var options = SSCTrainerAssignmentMenu.Options(f.slave);
            Assert(options.Any(o => o.Label.StartsWith("Replacement")) && f.slave.Training.selectedTrainer == f.master &&
                ReferenceEquals(rules, f.slave.Training.restrictionConfig.rules) && !rules.receiveTraining);
        });
        Run("共用菜单选择非主人原子提交授权且保留锁链主人", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var other = Pawn(PawnIdentity.Master, f.slave.Map); other.LabelShort = "Replacement";
            var chain = SSCBondUtility.GetChain(f.slave); int callbacks = 0;
            SSCTrainerAssignmentMenu.Options(f.slave, () => callbacks++).Single(o => o.Label.StartsWith("Replacement")).action();
            Assert(f.slave.Training.selectedTrainer == other && f.slave.Training.restrictionConfig.rules.receiveTraining && callbacks == 1 &&
                ReferenceEquals(chain, SSCBondUtility.GetChain(f.slave)) && SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        foreach (string change in new[] { "work", "map", "forced", "identity" }) Run("共用菜单迟到选择重新检查实际规则 " + change, () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var other = Pawn(PawnIdentity.Master, f.slave.Map); other.LabelShort = "Replacement";
            var rules = f.slave.Training.restrictionConfig.rules;
            var option = SSCTrainerAssignmentMenu.Options(f.slave).Single(o => o.Label.StartsWith("Replacement"));
            if (change == "work") other.workSettings.Active = false;
            if (change == "map") { f.slave.Map.mapPawns.FreeColonistsSource.Remove(other); other.Map = new Map(); }
            if (change == "identity") other.Training.pawnIdentity = PawnIdentity.Unset;
            if (change == "forced")
            {
                var gear = new Apparel(); gear.def.modExtensions.Add(new SSCRestrictionEquipmentExtension
                    { forced = new SSCRestrictionOverrides { receiveTraining = SSCRestrictionValue.Deny } });
                f.slave.apparel.WornApparel.Add(gear);
            }
            option.action();
            Assert(f.slave.Training.selectedTrainer == f.master && ReferenceEquals(rules, f.slave.Training.restrictionConfig.rules) && !rules.receiveTraining);
        });
        Run("共用菜单不允许清空绕过主人强制来源，允许的清空不解绑", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            Assert(SSCTrainerAssignmentMenu.Options(f.slave).Last().action == null);
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            var option = SSCTrainerAssignmentMenu.Options(f.slave).Last(); Assert(option.action != null); option.action();
            Assert(f.slave.Training.selectedTrainer == null && SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        Run("共用菜单上下文过期不提交真实指派与授权", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var other = Pawn(PawnIdentity.Master, f.slave.Map); other.LabelShort = "Replacement";
            bool current = true; var option = SSCTrainerAssignmentMenu.Options(f.slave, current: () => current).Single(o => o.Label.StartsWith("Replacement"));
            current = false; option.action(); Assert(f.slave.Training.selectedTrainer == f.master && !f.slave.Training.restrictionConfig.rules.receiveTraining);
        });
    }
}
