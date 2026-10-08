using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RunTrainingIdentityTests()
    {
        foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
        foreach (bool ritual in new[] { false, true })
        foreach (bool protection in new[] { false, true })
        foreach (bool forced in new[] { false, true })
            Run($"调教身份兜底：{identity}, 仪式={ritual}, 限制={protection}, 强制={forced}", () =>
            {
                var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
                p.b.Training.pawnIdentity = identity; p.b.Training.selectedTrainer = p.a;
                SSCMod.settings.enableSexSlaveProtectionRules = protection;
                var driver = TrainingDriver(p.a, p.b, ritual, forced);
                Assert(!driver.TryMakePreToilReservations(false), "实际主人和限制总开关不能绕过受训身份");
                Begin(driver);
                Assert(driver.Ended && driver.StartCalls == 0 && driver.CompletedEffects == 0,
                    "直接步骤也不得开始或结算");
                Assert(p.b.Training.pawnIdentity == identity && p.b.Training.selectedTrainer == p.a,
                    "拒绝不自动认领身份或抹除保存指派");
            });

        Run("未设置身份的手动命令在修改原任务前拒绝", () =>
        {
            var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.pawnIdentity = PawnIdentity.Unset;
            var driver = TrainingDriver(p.a, p.b, forced: true);
            driver.job.playerForced = false;
            driver.job.CachedDriver = driver; p.a.jobs.pawn = p.a;
            Assert(!p.a.jobs.TryTakeOrderedJob(driver.job), "命令前缀拒绝身份失格目标");
            Assert(p.a.jobs.OrderedMutations == 0 && !driver.job.playerForced, "没有原版命令副作用");
        });
        foreach (bool ritual in new[] { false, true })
            Run($"已开始场景身份失格不能借用开始凭据：仪式={ritual}", () =>
            {
                var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
                p.b.Training.selectedTrainer = p.a;
                var driver = TrainingDriver(p.a, p.b, ritual, true); Begin(driver);
                Assert(driver.StartCalls == 1, "合法场景已开始");
                p.b.Training.pawnIdentity = PawnIdentity.Unset;
                Assert(SSCRestrictionJobGuard.TryCheck(driver, "Finish", out bool allowed) && !allowed,
                    "身份失格必须重查，不能使用 Started 短路");
                Assert(driver.Ended && driver.CompletedEffects == 0, "中止不发完成效果");
                Assert(p.a.jobs.ImmediateJobSearches == 0, "拒绝不递归选择新任务");
            });
        Run("旧档日常 Started 恢复后仍要求受训身份", () =>
        {
            var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.pawnIdentity = PawnIdentity.Unset; p.b.Training.selectedTrainer = p.a;
            var driver = TrainingDriver(p.a, p.b, forced: true);
            driver.Sexprops = new SexProps { pawn = p.a, partner = p.b };
            driver.duration = 100; driver.ticks_left = 80;
            Scribe.mode = LoadSaveMode.LoadingVars; driver.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; driver.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive;
            Assert(SSCRestrictionJobGuard.TryCheck(driver, "Resume", out bool allowed) && !allowed && driver.Ended,
                "旧档开始凭据不能永久豁免新身份资格");
        });
        Run("普通已开始场景保留原身份变化收尾语义", () =>
        {
            var p = People(); var driver = Driver(p.a, p.b); Begin(driver);
            p.b.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(SSCRestrictionJobGuard.TryCheck(driver, "Finish", out bool allowed) && allowed,
                "训练身份检查不改变普通场景");
        });
        Run("失格调教的迟到回调不能结束新任务", () =>
        {
            var p = People(); p.a.Training.pawnIdentity = PawnIdentity.Master;
            p.b.Training.selectedTrainer = p.a;
            var old = TrainingDriver(p.a, p.b, forced: true); Begin(old);
            var current = Driver(p.a, p.c, rape: false);
            p.b.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(SSCRestrictionJobGuard.TryCheck(old, "LateCallback", out bool allowed) && !allowed,
                "旧任务拒绝回调");
            Assert(p.a.jobs.curDriver == current && !current.Ended, "保留新任务");
        });
    }
}
