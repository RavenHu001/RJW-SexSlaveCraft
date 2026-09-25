using System.Linq;
using RimWorld;
using SexSlaveCraft;

internal static partial class Program
{
    /// <summary>验证身份开关通过实际同床补丁生效，保留软限制、主人许可及既有睡眠记录。</summary>
    private static void RunTrainerIdentityBedRules()
    {
        Run("调教员关闭后不再允许新增同床分配，重新开启恢复", () =>
        {
            var f = Setup(); Assert(Use(f.bed, f.slave) && Comp(f.bed).CanAssignTo(f.slave).Accepted);
            SSCIdentityUtility.SetTrainerEnabled(f.partner, false);
            Assert(!Use(f.bed, f.slave) && !Comp(f.bed).CanAssignTo(f.slave).Accepted);
            AssertBadges(f.slave, f.bed, null);
            SSCIdentityUtility.SetTrainerEnabled(f.partner, true);
            Assert(Use(f.bed, f.slave) && Comp(f.bed).CanAssignTo(f.slave).Accepted);
        });
        Run("原版和 Mint 均立即移除停用调教员标签，保留自身性奴标签", () =>
        {
            var f = Setup(); Assign(f.bed, f.slave);
            AssertBadges(f.partner, f.bed, "性奴 · 调教员");
            SSCIdentityUtility.SetTrainerEnabled(f.partner, false);
            AssertBadges(f.partner, f.bed, "性奴");
            Assert(f.slave.Training.selectedTrainer == f.partner);
        });
        Run("已开启个人选择但资格失效时移除调教员标签和新增同床许可", () =>
        {
            var f = Setup();
            Assert(Use(f.bed, f.slave));
            f.partner.Training.TrainerQualified = false;
            Assert(f.partner.Training.slaveTrainerEnabled);
            Assert(!Use(f.bed, f.slave) && !Comp(f.bed).CanAssignTo(f.slave).Accepted);
            AssertBadges(f.partner, f.bed, "性奴");
            f.partner.Training.TrainerQualified = true;
            Assert(Use(f.bed, f.slave));
        });
        Run("未选择身份即使残留开关也不提供调教员同床许可", () =>
        {
            var f = Setup(); f.partner.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(f.partner.Training.slaveTrainerEnabled);
            Assert(!SSCSharedBedUtility.GetAllowedPartners(f.slave).Any());
            Assert(!Comp(f.bed).CanAssignTo(f.slave).Accepted);
        });
        Run("调教员停用不移除主人同床分支", () =>
        {
            var f = WithSeparateMasterBed();
            f.slave.Training.selectedTrainer.Training.slaveTrainerEnabled = false;
            Assert(Find(f.slave) == f.masterBed);
            Assert(SSCSharedBedUtility.GetAllowedPartners(f.slave).Single() == f.slave.Chain.LinkedPawn);
        });
        Run("关闭调教员不强制解除已分配床位", () =>
        {
            var f = Setup(); Assign(f.bed, f.slave);
            SSCIdentityUtility.SetTrainerEnabled(f.partner, false);
            Assert(f.slave.ownership.OwnedBed == f.bed && f.bed.OwnersForReading.Contains(f.slave));
            Assert(Find(f.slave) == f.bed);
        });
        Run("已记录睡眠关闭后照常结算，停用后新睡眠不生成调教员记忆", () =>
        {
            var f = Sleeping(); Tick(f.slave);
            SSCIdentityUtility.SetTrainerEnabled(f.partner, false); Finish(f.slave);
            Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithTrainer);
            var next = Sleeping(); SSCIdentityUtility.SetTrainerEnabled(next.partner, false);
            Tick(next.slave); Finish(next.slave); Assert(Memories(next.slave).Count == 0);
        });
    }
}
