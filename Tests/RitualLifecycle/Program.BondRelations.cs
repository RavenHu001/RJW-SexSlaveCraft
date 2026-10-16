// AI 辅助生成
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>只配置可观察的原生好感和双向存储，不模拟生产奖励条件。</summary>
    private static void PrepareRelations(RitualFixture fixture, int opinion)
    {
        SSCDefOf.SSC_FlawedLovers = new object();
        fixture.Master.relations.Owner = fixture.Master;
        fixture.Slave.relations.Owner = fixture.Slave;
        fixture.Slave.relations.Opinion = opinion;
    }

    /// <summary>经真实六阶段认领和完整 Apply 验证奖励门槛、第三方主持及回调中断。</summary>
    private static void RunBondRelationOutcomeTests()
    {
        // 在完整仪式中验证门槛和重复结算保护，防止只测试辅助函数却遗漏实际接线。
        foreach (int opinion in new[] { 79, 80, 100 })
            Check("real whole ritual ownership relation threshold " + opinion, () =>
            {
                var f = new RitualFixture(); PrepareRelations(f, opinion); Advance(f, 6);
                var worker = FeedbackOutcome(); ApplyFeedbackOutcome(worker, f); ApplyFeedbackOutcome(worker, f);
                Equal(opinion >= 80 ? 1 : 0, f.Slave.relations.DirectRelations.Count, "single slave-side relation");
                Equal(opinion >= 80 ? 1 : 0, f.Master.relations.DirectRelations.Count, "single master-side relation");
                Equal(1, TestWorld.RitualOutcomes, "one completed ritual");
            });
        // 两类第三方继续完成原有仪式；断言它们不会改写锁链或新建主人关系。
        foreach (bool officer in new[] { false, true })
            Check("authorized third party completes ritual without becoming master; officer=" + officer, () =>
            {
                var f = officer ? OfficerRitual() : NonOwnerRitual(); PrepareRelations(f, 100);
                Pawn owner = f.Slave.BoundMaster;
                Advance(f, 6); ApplyFeedbackOutcome(FeedbackOutcome(), f);
                Equal(1, TestWorld.RitualOutcomes, "third-party ritual remains supported");
                Equal(0, f.Slave.relations.DirectRelations.Count, "no false master relation");
                Equal(owner, f.Slave.BoundMaster, "actual owner unchanged");
            });
        // 好感足够也不能绕过六阶段完成资格。
        Check("incomplete ritual at 100 opinion cannot gain relationship", () =>
        {
            var f = new RitualFixture(); PrepareRelations(f, 100); Advance(f, 5);
            ApplyFeedbackOutcome(FeedbackOutcome(), f);
            Equal(0, f.Slave.relations.DirectRelations.Count, "no partial reward");
        });
        // 分别在读好感和写关系的外部回调边界撤销身份，验证写入前及后续派发前的重查。
        Check("opinion callback revoking ritual identity prevents relation write", () =>
        {
            var f = new RitualFixture(); PrepareRelations(f, 80); Advance(f, 6);
            f.Slave.relations.OnOpinion = () => f.Comp.pawnIdentity = PawnIdentity.Unset;
            ApplyFeedbackOutcome(FeedbackOutcome(), f);
            Equal(0, f.Slave.relations.DirectRelations.Count, "no relationship after revoked completion");
        });
        Check("relation-added callback invalidation stops remaining ritual payout", () =>
        {
            var f = new RitualFixture(); PrepareRelations(f, 80); Advance(f, 6);
            f.Master.relations.OnAdd = () => f.Comp.pawnIdentity = PawnIdentity.Unset;
            ApplyFeedbackOutcome(FeedbackOutcome(), f);
            Equal(0, TestWorld.RitualLetters, "no stale outcome letter");
        });
    }
}
