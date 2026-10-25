// AI 辅助生成：其他人接受保证的方向、分类、真实结算与保存生命周期专项。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>直接执行生产策略、原生互动和真实 Scribe；不以配置为 true 代替接受结果验证。</summary>
    private static void OtherAcceptanceTests()
    {
        // 二／三阶边界及普通许可组合：保证只在接收方三阶起覆盖，而非替主动方发起。
        foreach (float severity in new[] { .1f, .3f, .4999f, .5f, .9f })
        foreach (bool guarantee in new[] { false, true })
        foreach (bool allowed in new[] { false, true })
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
            Check($"other guarantee stage={severity} guarantee={guarantee} allowed={allowed} {action}", () =>
            {
                var a = Pawn(); var b = Slave(severity);
                Comp(b).romanceConfig = new() { alwaysAcceptOthers = guarantee, allowAcceptRomance = allowed, allowAcceptProposal = allowed };
                var expected = severity < .5f ? SSCRomanceReception.Vanilla : guarantee ? SSCRomanceReception.ForceAccept :
                    allowed ? SSCRomanceReception.Vanilla : SSCRomanceReception.ForceReject;
                Equal(expected, Decide(a, b, action).Reception); Require(Decide(a, b, action).CanInitiate);
                Require(Decide(b, a, action).CanInitiate);
                Equal(SSCRomanceReception.Vanilla, Decide(b, a, action).Reception);
                Equal(SSCRomanceReception.NotApplicable, Decide(a, b, SSCRomanceAction.Breakup).Reception);
                Equal(severity >= .5f, SSCRomancePreferences.GetView(b, SSCRomanceSetting.AlwaysAcceptOthers).CanChange);
            });
        foreach (string category in new[] { "master", "slave", "unchained slave", "master also slave" })
            Check("other guarantee never absorbs excluded category " + category, () =>
            {
                var a = category == "slave" || category == "master also slave" ? Slave() : Pawn();
                if (category == "unchained slave") Comp(a).pawnIdentity = PawnIdentity.Slave;
                var b = Slave(master: category.StartsWith("master") ? a : null);
                Comp(b).romanceConfig = new() { alwaysAcceptOthers = true, allowAcceptRomance = false, allowAcceptProposal = false };
                foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
                    Equal(SSCRomanceReception.ForceReject, Decide(a, b, action).Reception);
            });
        // 原版 guestStatusInt 已按目标 DLL 核对；仅构造法律身份输入，不运行招募／奴役流程。
        foreach (var legalStatus in new[] { GuestStatus.Guest, GuestStatus.Prisoner, GuestStatus.Slave })
            Check("other guarantee uses SSC identity rather than vanilla legal status " + legalStatus, () =>
            {
                var a = Pawn(); a.guest = new Pawn_GuestTracker(a);
                AccessTools.Field(typeof(Pawn_GuestTracker), "guestStatusInt").SetValue(a.guest, legalStatus);
                AccessTools.Field(typeof(Thing), "factionInt").SetValue(a, null);
                var b = Slave(); Comp(b).romanceConfig = new() { alwaysAcceptOthers = true, allowAcceptRomance = false };
                Equal(SSCRomanceReception.ForceAccept, Decide(a, b).Reception);
                Comp(a).pawnIdentity = PawnIdentity.Slave;
                Equal(SSCRomanceReception.ForceReject, Decide(a, b).Reception);
            });
        Check("other master qualifies but a new own master immediately leaves others category", () =>
        {
            var a = Pawn(PawnIdentity.Master); var b = Slave();
            Comp(b).romanceConfig = new() { alwaysAcceptOthers = true, allowAcceptRomance = false };
            Equal(SSCRomanceReception.ForceAccept, Decide(a, b).Reception);
            SSCBondUtility.GetChain(b).target = a; SSCRomanceLifecycle.Notify(b);
            Equal(SSCRomanceReception.ForceReject, Decide(a, b).Reception);
        });
        Check("other guarantee alone is nondefault and cannot disappear into missing carrier", () =>
        {
            var cfg = new SSCRomanceConfig(); Require(cfg.IsDefault);
            cfg.alwaysAcceptOthers = true; Require(!cfg.IsDefault);
            var copy = cfg.Copy(); copy.alwaysAcceptOthers = false; Require(cfg.alwaysAcceptOthers && copy.IsDefault);
            // 复用宿主缺组件边界；不保留与原生组件索引不一致的空列表。
            var p = Pawn(); AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(p, null);
            Require(!SSCRomancePreferences.CanRestore(p, cfg));
        });
        Check("other guarantee missing Scribe field defaults off without erasing other choices", () =>
        {
            var p = Slave(); Comp(p).romanceConfig = new() { alwaysAcceptOthers = true, alwaysAcceptMaster = true, allowAcceptRomance = false };
            string path = Save(new RomanceFixture(Comp(p))); Edit(path, doc => doc.Descendants("alwaysAcceptOthers").Remove());
            var restored = Slave(); Load(path, Comp(restored)); var cfg = Comp(restored).romanceConfig;
            Require(!cfg.alwaysAcceptOthers && cfg.alwaysAcceptMaster && !cfg.allowAcceptRomance);
        });
        Check("other guarantee downgrade unbind and clone reset preserve their distinct contracts", () =>
        {
            var p = Slave(); Comp(p).romanceConfig = new() { alwaysAcceptOthers = true }; var a = Pawn();
            SSCBondUtility.GetChain(p).Severity = .3f;
            Equal(SSCRomanceReception.Vanilla, Decide(a, p).Reception); Require(Comp(p).romanceConfig.alwaysAcceptOthers);
            SSCBondUtility.GetChain(p).Severity = .5f;
            Equal(SSCRomanceReception.ForceAccept, Decide(a, p).Reception);
            SSCBondUtility.GetChain(p).target = null;
            Equal(SSCRomanceReception.Vanilla, Decide(a, p).Reception); Require(Comp(p).romanceConfig.alwaysAcceptOthers);
            SSCRomanceLifecycle.ResetNewClone(p);
            Require(Comp(p).romanceConfig == null && !SSCRomancePreferences.GetView(p, SSCRomanceSetting.AlwaysAcceptOthers).Saved);
        });
        Check("other guarantee rejects an old edit after personality replacement", () =>
        {
            var p = Slave(); var view = SSCRomancePreferences.GetView(p, SSCRomanceSetting.AlwaysAcceptOthers);
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); scope.Dispose();
            Require(!SSCRomancePreferences.TrySet(p, SSCRomanceSetting.AlwaysAcceptOthers, true, view.Context, out var reason));
            Equal(SSCRomanceReason.StaleContext, reason); Require(Comp(p).romanceConfig == null);
        });
        // 零概率与随机端点仍须严格接受；关掉保证则保留原拒绝、记忆与消息路径。
        foreach (bool guarantee in new[] { false, true })
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
            Check($"other guarantee real worker zero chance/end point {guarantee}/{action}", () =>
            {
                CommandReset(1f, .9f); socialChance = 0f; var a = SocialPawn(); var b = SocialSlave();
                Comp(b).romanceConfig = new() { alwaysAcceptOthers = guarantee, allowAcceptRomance = false, allowAcceptProposal = false };
                if (action == SSCRomanceAction.Proposal) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                Require(a.interactions.TryInteractWith(b, SocialDef(action)));
                Equal(guarantee, a.relations.DirectRelationExists(action == SSCRomanceAction.Romance ? PawnRelationDefOf.Lover : PawnRelationDefOf.Fiance, b));
                Equal(guarantee ? 0 : 1, commandFeedback); Equal(1, playLogCalls);
                Equal(0f, action == SSCRomanceAction.Romance ? InteractionWorker_RomanceAttempt.SuccessChance(a, b) : InteractionWorker_MarriageProposal.AcceptanceChance(a, b));
                if (guarantee) Require(!a.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.RebuffedMyRomanceAttempt || m.def == ThoughtDefOf.RejectedMyProposal));
            });
        Check("other guarantee cannot bypass interaction ability or proposal relationship prerequisites", () =>
        {
            CommandReset(); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { alwaysAcceptOthers = true };
            socialCanInteract = false; Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt));
            socialCanInteract = true; Require(!a.interactions.TryInteractWith(b, InteractionDefOf.MarriageProposal));
            Equal(0, playLogCalls + commandFeedback);
        });
        Check("other guarantee cannot authorize a blocked SSC initiator", () =>
        {
            CommandReset(); var a = SocialSlave(); var b = SocialSlave();
            Comp(a).romanceConfig = new() { allowInitiateRomance = false }; Comp(b).romanceConfig = new() { alwaysAcceptOthers = true };
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Equal(0, playLogCalls + commandFeedback);
        });
        Check("other guarantee menu follows guaranteed preview without altering public chance", () =>
        {
            CommandReset(); var a = SocialPawn(); var b = SocialSlave(); Comp(b).romanceConfig = new() { alwaysAcceptOthers = true, allowAcceptRomance = false };
            var option = new FloatMenuOption("native", () => { }); float chance = .6f; bool eligible = true;
            Harmony_SSCRomanceMenu.Postfix(a, b, ref option, ref chance, ref eligible);
            Require(eligible && option.action != null); Equal(1f, chance); Equal(.6f, InteractionWorker_RomanceAttempt.SuccessChance(a, b)); Equal(0, commandFeedback);
        });
    }
}
