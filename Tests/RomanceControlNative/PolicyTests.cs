// AI 辅助生成
using SexSlaveCraft;
using HarmonyLib;
using Verse;

internal static partial class Program
{
    /// <summary>验证阈值、许可独立性、双边优先级和异常回退；策略通过不等于互动已执行。</summary>
    private static void PolicyTests()
    {
        // 穷举三个类别的八种组合与行为许可：类别之间取并集，类别与行为之间取交集。
        foreach (int mask in Enumerable.Range(0, 8))
        foreach (bool actionAllowed in new[] { false, true })
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
            Check($"unified targets mask={mask} allowed={actionAllowed} action={action}", () =>
            {
                var a = Slave(.3f); var master = SSCBondUtility.GetBoundMaster(a); var slave = Slave(); var other = Pawn();
                Comp(a).romanceConfig = new() { allowInitiateRomance = actionAllowed, allowInitiateProposal = actionAllowed,
                    allowInitiateToMaster = (mask & 1) != 0, allowInitiateToSexSlave = (mask & 2) != 0, allowInitiateToOthers = (mask & 4) != 0 };
                Equal(actionAllowed && (mask & 1) != 0, Decide(a, master, action).CanInitiate);
                Equal(actionAllowed && (mask & 2) != 0, Decide(a, slave, action).CanInitiate);
                Equal(actionAllowed && (mask & 4) != 0, Decide(a, other, action).CanInitiate);
                Require(Decide(a, other, SSCRomanceAction.Breakup).CanInitiate);
            });
        Check("bound master that is also a sex slave belongs only to master category", () =>
        {
            var master = Slave(); var a = Slave(.3f, master); Comp(a).romanceConfig = new() {
                allowInitiateToMaster = true, allowInitiateToSexSlave = false, allowInitiateToOthers = false };
            Require(Decide(a, master).CanInitiate); Comp(a).romanceConfig.allowInitiateToMaster = false;
            Comp(a).romanceConfig.allowInitiateToSexSlave = true; Require(!Decide(a, master).CanInitiate);
        });
        // 新增同伴身份规则：验证方向、二／三阶门槛、双边优先级及单次命令边界；同时验证主动过滤器及被动接受保证。
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
        foreach (var severity in new[] { .1f, .3f, .5f, .9f })
            Check($"peer controls follow direction and stage {severity}/{action}", () =>
            {
                var a = Slave(severity); var b = Slave(); var other = Pawn();
                Comp(a).romanceConfig = new() { allowInitiateToSexSlave = false, alwaysAcceptSexSlave = true,
                    allowAcceptRomance = false, allowAcceptProposal = false };
                Equal(severity < .3f, Decide(a, b, action).CanInitiate);
                Require(Decide(a, other, action).CanInitiate);
                Equal(severity >= .5f ? SSCRomanceReception.ForceAccept : SSCRomanceReception.Vanilla, Decide(b, a, action).Reception);
                Equal(severity >= .5f ? SSCRomanceReception.ForceReject : SSCRomanceReception.Vanilla, Decide(other, a, action).Reception);
                Require(Decide(a, b, SSCRomanceAction.Breakup).CanInitiate);
            });
        Check("master-only target selection belongs to active stage two", () =>
        {
            var a = Slave(.3f); Comp(a).romanceConfig = new() { allowInitiateToMaster = true, allowInitiateToSexSlave = false, allowInitiateToOthers = false };
            Require(SSCRomancePreferences.GetView(a, SSCRomanceSetting.InitiateToMaster).CanChange);
            Require(!SSCRomancePreferences.GetView(a, SSCRomanceSetting.AlwaysAcceptMaster).CanChange);
            Require(!Decide(a, Slave()).CanInitiate); Require(Decide(a, SSCBondUtility.GetBoundMaster(a)).CanInitiate);
        });
        Check("peer guarantee cannot reopen prohibited initiation on the other participant", () =>
        {
            var a = Slave(); var b = Slave(); Comp(a).romanceConfig = new() { allowInitiateToSexSlave = false };
            Comp(b).romanceConfig = new() { alwaysAcceptSexSlave = true, allowAcceptRomance = false };
            var r = Decide(a, b); Require(!r.CanInitiate); Equal(SSCRomanceReception.ForceAccept, r.Reception);
        });
        foreach (var (severity, stage) in new[] { (.099f, 0), (.1f, 1), (.2999f, 1), (.3f, 2), (.4999f, 2), (.5f, 3), (.8999f, 3), (.9f, 4) })
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check($"current chain stage {severity}/{action}", () =>
            {
                var p = Slave(severity); Comp(p).romanceConfig = Denied();
                Equal(stage, SSCRomanceResolver.Resolve(p).Stage);
                Equal(stage < 2, Decide(p, Pawn(), action).CanInitiate);
                var r = Decide(Pawn(), p, action);
                Equal(action == SSCRomanceAction.Breakup ? SSCRomanceReception.NotApplicable :
                    stage >= 3 ? SSCRomanceReception.ForceReject : SSCRomanceReception.Vanilla, r.Reception);
            });
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("old missing config preserves ordinary decision: " + action, () =>
            { var p = Slave(); var q = Pawn(); Require(Decide(p, q, action).CanInitiate); Equal(action == SSCRomanceAction.Breakup ? SSCRomanceReception.NotApplicable : SSCRomanceReception.Vanilla, Decide(q, p, action).Reception); Require(Comp(p).romanceConfig == null); });
        foreach (bool guarantee in new[] { false, true })
        foreach (bool accept in new[] { false, true })
        foreach (bool owner in new[] { false, true })
        foreach (var action in new[] { SSCRomanceAction.Romance, SSCRomanceAction.Proposal })
            Check($"recipient priority guarantee={guarantee} accept={accept} owner={owner} {action}", () =>
            {
                var p = Slave(); Comp(p).romanceConfig = new() { alwaysAcceptMaster = guarantee, allowAcceptRomance = accept, allowAcceptProposal = accept };
                var r = Decide(owner ? SSCBondUtility.GetBoundMaster(p) : Pawn(), p, action);
                Require(r.CanInitiate); Equal(owner && guarantee ? SSCRomanceReception.ForceAccept : accept ? SSCRomanceReception.Vanilla : SSCRomanceReception.ForceReject, r.Reception);
            });
        foreach (bool onlyOwner in new[] { false, true })
        foreach (bool guarantee in new[] { false, true })
            Check($"two independent extras {onlyOwner}/{guarantee}", () =>
            {
                var p = Slave(); Comp(p).romanceConfig = new() { allowInitiateToMaster = onlyOwner, alwaysAcceptMaster = guarantee };
                Equal(onlyOwner, Decide(p, SSCBondUtility.GetBoundMaster(p)).CanInitiate);
                Require(Decide(p, Pawn()).CanInitiate);
                Require(Decide(p, Pawn(), SSCRomanceAction.Breakup).CanInitiate);
            });
        foreach (var setting in new[] { SSCRomanceSetting.InitiateRomance, SSCRomanceSetting.InitiateProposal, SSCRomanceSetting.InitiateBreakup })
            Check("three active permissions independent: " + setting, () =>
            {
                var p = Slave(); var view = SSCRomancePreferences.GetView(p, setting);
                Require(SSCRomancePreferences.TrySet(p, setting, false, view.Context, out _));
                foreach (var action in Enum.GetValues<SSCRomanceAction>()) Equal((int)setting != (int)action, Decide(p, SSCBondUtility.GetBoundMaster(p), action).CanInitiate);
            });
        Check("both controlled: target guarantee cannot authorize blocked initiator", () =>
        {
            var a = Slave(); var b = Slave(master: a); Comp(a).romanceConfig = Denied(); Comp(b).romanceConfig = Denied();
            var r = Decide(a, b); Require(!r.CanInitiate); Equal(SSCRomanceReception.ForceAccept, r.Reception);
        });
        Check("stage1 actor does not suppress other participant rule", () =>
        { var a = Slave(.1f); var b = Slave(); Comp(a).romanceConfig = Denied(); Comp(b).romanceConfig = Denied(); Require(Decide(a, b).CanInitiate); Equal(SSCRomanceReception.ForceReject, Decide(a, b).Reception); });
        Check("explicit identity required; assigned trainer never substitutes master", () =>
        {
            var p = Slave(); Comp(p).pawnIdentity = PawnIdentity.Unset; Comp(p).romanceConfig = Denied();
            Require(Decide(p, Pawn()).CanInitiate); Equal(SSCRomanceReason.Identity, SSCRomanceResolver.Resolve(p).Reason);
            Comp(p).pawnIdentity = PawnIdentity.Slave; SSCBondUtility.GetChain(p).target = null; Comp(p).selectedTrainer = Pawn();
            Equal(SSCRomanceReason.NoBinding, SSCRomanceResolver.Resolve(p).Reason);
        });
        Check("read-only evaluation leaves references and saved state untouched", () =>
        { var p = Slave(); var cfg = Comp(p).romanceConfig = Denied(); var chain = SSCBondUtility.GetChain(p); for (int i = 0; i < 100; i++) Decide(p, Pawn()); Require(ReferenceEquals(cfg, Comp(p).romanceConfig)); Require(ReferenceEquals(chain, SSCBondUtility.GetChain(p))); Equal(1, p.health.hediffSet.hediffs.Count); });
        Check("unsupported configuration explicitly falls back without overriding acceptance", () =>
        { var p = Slave(); Comp(p).romanceConfig = Denied(); Comp(p).romanceConfig.version = 99; var r = Decide(p, Pawn()); Require(r.CanInitiate); Equal(SSCRomanceReason.UnsupportedConfig, r.InitiatorReason); Equal(SSCRomanceReception.Vanilla, Decide(Pawn(), p).Reception); Equal(99, Comp(p).romanceConfig.version); });
        Check("old protection global switch never disables romance rules or changes defaults", () =>
        {
            var settings = SSCMod.settings;
            try { SSCMod.settings = new SSCSettings { enableSexSlaveProtectionRules = false }; var p = Slave(); Comp(p).romanceConfig = Denied(); Require(!Decide(p, Pawn()).CanInitiate); Require(SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance).CanChange); SSCRomanceLifecycle.ResetNewClone(p); Require(SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance).Saved); }
            finally { SSCMod.settings = settings; }
        });
        Check("dead off-map master remains bound; readonly lookup does not recreate reciprocal link", () =>
        {
            var p = Slave(); var owner = SSCBondUtility.GetBoundMaster(p);
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(owner.health, PawnHealthState.Dead);
            Require(owner.Dead && !owner.Spawned); var state = SSCRomanceResolver.Resolve(p); Require(state.Applicable); Equal(owner, state.Master);
        });
        Check("non-player configuration still applies while player editing is refused", () =>
        {
            var p = Slave(); AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, null); Comp(p).romanceConfig = Denied();
            Require(!Decide(p, Pawn()).CanInitiate); Equal(SSCRomanceReason.PlayerOnly, SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance).Reason);
        });
    }
    /// <summary>验证保存偏好与阶段资格分离，以及旧配置、载体或人格下的点击不能提交。</summary>
    private static void PreferenceTests()
    {
        Check("saved choice and current unlock remain separate on downgrade", () =>
        { var p = Slave(); Comp(p).romanceConfig = Denied(); SSCBondUtility.GetChain(p).Severity = .1f; var v = SSCRomancePreferences.GetView(p, SSCRomanceSetting.InitiateRomance); Require(!v.Saved && !v.Unlocked && !v.CanChange); SSCBondUtility.GetChain(p).Severity = .9f; Require(!Decide(p, Pawn()).CanInitiate); });
        Check("captured edit commits once and never reuses old config context", () =>
        { var p = Slave(); var v = SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance); Require(v.CanChange); Require(SSCRomancePreferences.TrySet(p, SSCRomanceSetting.AcceptRomance, false, v.Context, out _)); Require(!SSCRomancePreferences.TrySet(p, SSCRomanceSetting.AcceptRomance, true, v.Context, out var reason)); Equal(SSCRomanceReason.StaleContext, reason); });
        Check("old edit cannot cross personality replacement", () =>
        { var p = Slave(); var v = SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance); Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); using (scope) Require(!SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance).CanChange); Require(!SSCRomancePreferences.TrySet(p, SSCRomanceSetting.AcceptRomance, false, v.Context, out _)); });
        Check("old edit cannot cross component replacement", () =>
        { var p = Slave(); var v = SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance); p.AllComps.Clear(); p.AllComps.Add(new CompSexSlaveTraining { parent = p, pawnIdentity = PawnIdentity.Slave }); Require(!SSCRomancePreferences.TrySet(p, SSCRomanceSetting.AcceptRomance, false, v.Context, out _)); });
        Check("loading suspends changes without writing missing configuration", () =>
        { var p = Slave(); try { Scribe.mode = LoadSaveMode.LoadingVars; Require(!SSCRomancePreferences.GetView(p, SSCRomanceSetting.AcceptRomance).CanChange); Require(Comp(p).romanceConfig == null); } finally { Scribe.mode = LoadSaveMode.Inactive; } });
        Check("unknown settings are rejected", () =>
        { var p = Slave(); var v = SSCRomancePreferences.GetView(p, (SSCRomanceSetting)88); Equal(SSCRomanceReason.UnknownSetting, v.Reason); Require(!SSCRomancePreferences.TrySet(p, (SSCRomanceSetting)88, false, v.Context, out _)); });
    }
}
