// AI 辅助生成
using SexSlaveCraft;
using HarmonyLib;
using Verse;

internal static partial class Program
{
    /// <summary>验证阈值、许可独立性、双边优先级和异常回退；策略通过不等于互动已执行。</summary>
    private static void PolicyTests()
    {
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
                var p = Slave(); Comp(p).romanceConfig = new() { initiateOnlyMaster = onlyOwner, alwaysAcceptMaster = guarantee };
                Require(Decide(p, SSCBondUtility.GetBoundMaster(p)).CanInitiate);
                Equal(!onlyOwner, Decide(p, Pawn()).CanInitiate);
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
