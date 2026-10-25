// AI 辅助生成：原版 worker 的关系、记忆、姓氏及床位交接；不在测试中复刻结算。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>覆盖实际成功／失败分支、三种关系结束、默认结果与异常关系防护。</summary>
    private static void SettlementTests()
    {
        foreach (float roll in new[] { 0f, .6f, 1f })
            Check("batch2 default romance preserves exact original comparison " + roll, () =>
            {
                ResetSocial(roll); var a = SocialSlave(); var b = SocialPawn();
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
                Equal(roll < .6f, a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b)); Equal(0, socialRolls.Count);
            });
        Check("batch2 master proposal guarantee uses full fiance transition with zero native probability", () =>
        {
            ResetSocial(1f); socialChance = 0; var a = SocialPawn(); var b = SocialSlave(a);
            Comp(b).romanceConfig = new() { allowAcceptProposal = false, alwaysAcceptMaster = true };
            a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
            a.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.RejectedMyProposal, b);
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.MarriageProposal));
            Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b)); Require(a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b));
            Equal(0, a.needs.mood.thoughts.memories.Memories.Count); Equal(0, socialRolls.Count);
            Equal(0f, InteractionWorker_MarriageProposal.AcceptanceChance(a, b));
        });
        Check("batch2 free target remains able to reject a fourth stage actor", () =>
        {
            ResetSocial(1f); var a = SocialSlave(); var b = SocialPawn();
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b)); Equal(1, a.needs.mood.thoughts.memories.Memories.Count);
        });
        foreach (var relation in new[] { PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance, PawnRelationDefOf.Spouse })
            Check("batch2 full native breakup retains SSC bond and reward " + relation.defName, () =>
            {
                ResetSocial(0f, 0f, 0f); var master = SocialPawn(); var slave = SocialSlave(master);
                Comp(slave).romanceConfig = new() { initiateOnlyMaster = true, allowAcceptRomance = false, alwaysAcceptMaster = true };
                var trainer = Comp(slave).selectedTrainer = SocialPawn(); var chain = SSCBondUtility.GetChain(slave);
                SSCDefOf.SSC_FlawedLovers ??= new PawnRelationDef { defName = "SSC_FlawedLovers", reflexive = true, workerClass = typeof(PawnRelationWorker) };
                slave.relations.AddDirectRelation(SSCDefOf.SSC_FlawedLovers, master); slave.relations.AddDirectRelation(relation, master);
                var bed = new Building_Bed(); AccessTools.Field(typeof(Pawn_Ownership), "intOwnedBed").SetValue(slave.ownership, bed);
                AccessTools.Field(typeof(Pawn_Ownership), "intOwnedBed").SetValue(master.ownership, bed);
                slave.Name = new NameTriple("Slave", "Slave", "Married"); slave.story.birthLastName = "Original";
                master.Name = new NameTriple("Master", "Master", "Married"); master.story.birthLastName = "Birth";
                Require(slave.interactions.TryInteractWith(master, SSCRomanceDefOf.Breakup));
                Require(!slave.relations.DirectRelationExists(relation, master));
                Require(slave.relations.DirectRelationExists(relation == PawnRelationDefOf.Spouse ? PawnRelationDefOf.ExSpouse : PawnRelationDefOf.ExLover, master));
                Equal(1, taleCalls); Equal(1, unclaimCalls); Equal(0, shareBedCalls);
                if (relation == PawnRelationDefOf.Spouse) { Equal("Original", ((NameTriple)slave.Name).Last); Equal("Birth", ((NameTriple)master.Name).Last); }
                else Require(master.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.BrokeUpWithMe));
                Require(ReferenceEquals(chain, SSCBondUtility.GetChain(slave))); Equal(master, chain.target); Equal(trainer, Comp(slave).selectedTrainer);
                Require(slave.relations.DirectRelationExists(SSCDefOf.SSC_FlawedLovers, master)); Require(SSCBondRelationUtility.IsValidPair(slave, master));
            });
        Check("batch2 free partner may end relationship despite slave active breakup denial", () =>
        {
            ResetSocial(); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowInitiateBreakup = false };
            a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b); Require(a.interactions.TryInteractWith(b, SSCRomanceDefOf.Breakup));
            Require(a.relations.DirectRelationExists(PawnRelationDefOf.ExLover, b));
        });
        Check("batch2 only-master does not prevent ending third-person relationship", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); Comp(a).romanceConfig = new() { initiateOnlyMaster = true };
            a.relations.AddDirectRelation(PawnRelationDefOf.Fiance, b); Require(a.interactions.TryInteractWith(b, SSCRomanceDefOf.Breakup));
            Require(a.relations.DirectRelationExists(PawnRelationDefOf.ExLover, b));
        });
        Check("batch2 romance success preserves native old-lover/fiance cleanup despite breakup denial", () =>
        {
            ResetSocial(0f); socialWilling = false; var a = SocialSlave(); var b = SocialSlave();
            Comp(a).romanceConfig = new() { allowInitiateBreakup = false }; Comp(b).romanceConfig = new() { allowInitiateBreakup = false };
            var oldLover = SocialPawn(); var oldFiance = SocialPawn(); var spouse = SocialPawn();
            a.relations.AddDirectRelation(PawnRelationDefOf.Lover, oldLover); b.relations.AddDirectRelation(PawnRelationDefOf.Fiance, oldFiance);
            a.relations.AddDirectRelation(PawnRelationDefOf.Spouse, spouse);
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            Require(a.relations.DirectRelationExists(PawnRelationDefOf.ExLover, oldLover)); Require(b.relations.DirectRelationExists(PawnRelationDefOf.ExLover, oldFiance));
            Require(a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, spouse)); Require(a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            Equal(1, taleCalls); Equal(1, shareBedCalls);
        });
        Check("batch2 native permissive relationship-count rule keeps old partners", () =>
        {
            ResetSocial(0f); var a = SocialSlave(); var b = SocialPawn(); var old = SocialPawn(); a.relations.AddDirectRelation(PawnRelationDefOf.Fiance, old);
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Require(a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, old));
        });
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("batch2 necessary relationship guard prevents synthetic interaction " + action, () =>
            {
                ResetSocial(); var a = SocialSlave(); var b = SocialPawn();
                if (action == SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Spouse, b);
                Require(!a.interactions.TryInteractWith(b, SocialDef(action))); Equal(0, bubbleCalls + taleCalls + playLogCalls);
            });
        Check("batch2 fiance cannot propose again through RJW-compatible shared entry", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); a.relations.AddDirectRelation(PawnRelationDefOf.Fiance, b);
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.MarriageProposal)); Equal(0, bubbleCalls + playLogCalls);
            Require(a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b));
        });
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
        foreach (bool acceptMaster in new[] { false, true })
        foreach (bool allowPassive in new[] { false, true })
            Check($"batch2 real master acceptance matrix {action}/guarantee={acceptMaster}/permission={allowPassive}", () =>
            {
                ResetSocial(1f, 1f); socialChance = 0; var a = SocialPawn(); var b = SocialSlave(a);
                Comp(b).romanceConfig = new() { allowAcceptRomance = allowPassive, allowAcceptProposal = allowPassive, alwaysAcceptMaster = acceptMaster };
                if (action == SSCRomanceAction.Proposal) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                Require(a.interactions.TryInteractWith(b, SocialDef(action)));
                Equal(acceptMaster, a.relations.DirectRelationExists(action == SSCRomanceAction.Romance ? PawnRelationDefOf.Lover : PawnRelationDefOf.Fiance, b));
            });
        Check("batch2 passive rejection defeats native certain acceptance and retains low-opinion memory", () =>
        {
            ResetSocial(0f); socialChance = 1; socialOpinionValue = -100; var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            Require(b.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood));
        });
        Check("batch2 controlled initiator denial wins before recipient master guarantee", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialSlave(a); Comp(a).romanceConfig = new() { allowInitiateRomance = false };
            Comp(b).romanceConfig = new() { allowAcceptRomance = false, alwaysAcceptMaster = true };
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, playLogCalls + bubbleCalls);
        });
        foreach (float severity in new[] { .3f, .5f })
            Check("batch2 real passive override unlocks at third stage " + severity, () =>
            {
                ResetSocial(0f); var a = SocialPawn(); var b = SocialSlave(severity: severity); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
                Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(severity < .5f, a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            });
        Check("batch2 new master is read at execution without inheriting old master exemption", () =>
        {
            ResetSocial(0f); var oldMaster = SocialPawn(); var newMaster = SocialPawn(); var slave = SocialSlave(oldMaster);
            Comp(slave).romanceConfig = new() { allowAcceptRomance = false, alwaysAcceptMaster = true };
            SSCBondUtility.GetChain(slave).target = newMaster; SSCRomanceLifecycle.Notify(slave);
            Require(oldMaster.interactions.TryInteractWith(slave, InteractionDefOf.RomanceAttempt)); Require(!oldMaster.relations.DirectRelationExists(PawnRelationDefOf.Lover, slave));
            ResetSocial(1f); socialChance = 0; Require(newMaster.interactions.TryInteractWith(slave, InteractionDefOf.RomanceAttempt));
            Require(newMaster.relations.DirectRelationExists(PawnRelationDefOf.Lover, slave));
        });
    }
    /// <summary>从固定行为枚举取得真实原版 Def，测试不使用名称猜测业务方向。</summary>
    private static InteractionDef SocialDef(SSCRomanceAction action) => action == SSCRomanceAction.Romance ? InteractionDefOf.RomanceAttempt :
        action == SSCRomanceAction.Proposal ? InteractionDefOf.MarriageProposal : SSCRomanceDefOf.Breakup;
}
