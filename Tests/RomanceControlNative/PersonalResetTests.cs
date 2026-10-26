// AI 辅助生成：两个真实个人后台、默认构建、原回调及 Scribe，完整地图与 GUI 另测。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action personalResetDuringBuild;
    /// <summary>真实模块上检验默认、保存、跨板块隔离与过期请求，不能以 UI 灰显代替业务校验。</summary>
    private static void PersonalResetTests(Harmony owner)
    {
        var previous = SSCMod.settings;
        var profile = new SSCRestrictionProfileDef { defName = "PersonalResetBus", specialization = SexSlaveSpecializationType.Bus,
            defaults = new SSCRestrictionOverrides { receiveConsensual = SSCRestrictionValue.Allow },
            forced = new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Allow } };
        DefDatabase<SSCRestrictionProfileDef>.Add(profile);
        var boundary = new Harmony("ssc.tests.personal-reset");
        boundary.Patch(AccessTools.Method(typeof(SSCRestrictionConfigurationBuilder), nameof(SSCRestrictionConfigurationBuilder.TryCreateInitial)),
            postfix: new HarmonyMethod(typeof(Program), nameof(PersonalResetBuildCallback)));
        try
        {
            // 禁用视图不能等条件恢复后自行取得提交权限，必须重新绘制并取得新请求。
            foreach (string mode in new[] { "stage", "loading", "faction" })
                Check("personal romance unavailable view cannot acquire later authority " + mode, () =>
                {
                    var p = SocialSlave(); Comp(p).romanceConfig = InvertedRomanceDefaults();
                    var chain = SSCBondUtility.GetChain(p); float severity = chain.Severity;
                    if (mode == "stage") chain.Severity = .1f;
                    if (mode == "loading") Scribe.mode = LoadSaveMode.LoadingVars;
                    if (mode == "faction") AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, null);
                    SSCRomanceResetView view;
                    try { view = SSCRomancePreferences.GetResetView(p); Require(!view.CanReset); }
                    finally { Scribe.mode = LoadSaveMode.Inactive; chain.Severity = severity; AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, Faction.OfPlayer); }
                    var config = Comp(p).romanceConfig;
                    Require(!SSCRomancePreferences.TryReset(p, view.Context, out _)); Equal(config, Comp(p).romanceConfig);
                    var fresh = SSCRomancePreferences.GetResetView(p); Require(fresh.CanReset);
                    Require(SSCRomancePreferences.TryReset(p, fresh.Context, out _));
                });
            foreach (string mode in new[] { "restore", "loading", "faction" })
                Check("personal sex unavailable view requires fresh authority " + mode, () =>
                {
                    SSCMod.settings = new SSCSettings(); var p = SexResetPawn();
                    var depth = AccessTools.Field(typeof(CompSexSlaveTraining), "restrictionRestoreDepth");
                    if (mode == "restore") depth.SetValue(Comp(p), 1);
                    if (mode == "loading") Scribe.mode = LoadSaveMode.LoadingVars;
                    if (mode == "faction") AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, null);
                    SSCRestrictionResetView view;
                    try { view = SSCRestrictionEditor.GetResetView(p); Require(!view.CanReset); }
                    finally { Scribe.mode = LoadSaveMode.Inactive; depth.SetValue(Comp(p), 0); AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, Faction.OfPlayer); }
                    var config = Comp(p).restrictionConfig;
                    Require(!SSCRestrictionEditor.TryReset(p, view.Context, out _)); Equal(config, Comp(p).restrictionConfig);
                    var fresh = SSCRestrictionEditor.GetResetView(p); Require(fresh.CanReset);
                    Require(SSCRestrictionEditor.TryReset(p, fresh.Context, out _));
                });
            foreach (int stage in new[] { 1, 2, 3, 4 })
                Check("personal romance restore full defaults at editable stage " + stage, () =>
                {
                    var p = SocialSlave(severity: stage == 1 ? .1f : stage == 2 ? .3f : stage == 3 ? .5f : .9f);
                    var c = Comp(p).romanceConfig = InvertedRomanceDefaults();
                    var view = SSCRomancePreferences.GetResetView(p); Equal(stage >= 2, view.CanReset); Equal(c, Comp(p).romanceConfig);
                    Equal(stage >= 2, SSCRomancePreferences.TryReset(p, view.Context, out _));
                    if (stage >= 2) { Require(Comp(p).romanceConfig.IsDefault); Require(!SSCRomancePreferences.GetResetView(p).CanReset); }
                    else Equal(c, Comp(p).romanceConfig);
                });
            foreach (bool absent in new[] { false, true })
                Check("personal romance already default is read only " + absent, () =>
                {
                    var p = SocialSlave(); Comp(p).romanceConfig = absent ? null : new(); var c = Comp(p).romanceConfig;
                    var v = SSCRomancePreferences.GetResetView(p); Require(!v.CanReset && v.Reason == "Default");
                    Require(!SSCRomancePreferences.TryReset(p, v.Context, out _)); Equal(c, Comp(p).romanceConfig);
                });
            foreach (var mode in new[] { "value", "reference", "token", "master", "chain", "faction", "unknown", "loading" })
                Check("personal romance refuses stale or unavailable reset " + mode, () =>
                {
                    var p = SocialSlave(); Comp(p).romanceConfig = InvertedRomanceDefaults(); var v = SSCRomancePreferences.GetResetView(p);
                    if (mode == "value") Comp(p).romanceConfig.allowAcceptProposal = true;
                    if (mode == "reference") Comp(p).romanceConfig = Comp(p).romanceConfig.Copy();
                    if (mode == "token") { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var s)); s.Dispose(); }
                    if (mode == "master") SSCBondUtility.GetChain(p).target = SocialPawn();
                    if (mode == "chain") p.health.hediffSet.hediffs.Clear();
                    if (mode == "faction") AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, null);
                    if (mode == "unknown") Comp(p).romanceConfig.version = 99;
                    if (mode == "loading") Scribe.mode = LoadSaveMode.LoadingVars;
                    var c = Comp(p).romanceConfig;
                    try { Require(!SSCRomancePreferences.TryReset(p, v.Context, out _)); Equal(c, Comp(p).romanceConfig); }
                    finally { Scribe.mode = LoadSaveMode.Inactive; }
                });
            Check("personal romance preserves sex settings command and historical memory", () =>
            {
                var (p, _, _, record) = SocialLogEvent(7);
                var log = moodLastLog; var memories = p.needs.mood.thoughts.memories.Memories.ToArray();
                var sex = Comp(p).restrictionConfig = new(); var trainer = Comp(p).selectedTrainer; var token = SSCPersonalityRuntimeUtility.Capture(p);
                Comp(p).romanceConfig = InvertedRomanceDefaults(); int feedback = commandFeedback;
                Require(SSCRomancePreferences.TryReset(p, SSCRomancePreferences.GetResetView(p).Context, out _));
                Equal(sex, Comp(p).restrictionConfig); Equal(trainer, Comp(p).selectedTrainer); Equal(log, moodLastLog);
                Require(memories.SequenceEqual(p.needs.mood.thoughts.memories.Memories)); Equal(feedback, commandFeedback);
                Require(SSCPersonalityRuntimeUtility.IsCurrent(p, token)); Equal(SSCRomanceCommandStatus.Completed, record.Status);
            });
            // 用互补模板逐条核对七项及关闭前的对象偏好，避免只验证出厂模板的一部分字段。
            foreach (bool allow in new[] { false, true })
                Check("personal sex restores every saved rule and hidden target preference " + allow, () =>
                {
                    var defaults = new SSCRestrictionRules();
                    foreach (var rule in SSCRestrictionRules.All) defaults.Set(rule, allow ? SSCRestrictionValue.Allow : SSCRestrictionValue.Deny);
                    defaults.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow);
                    defaults.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
                    SSCMod.settings = new SSCSettings { restrictionDefaults = defaults };
                    var p = SexResetPawn(); Comp(p).restrictionConfig.rules.allowMasturbation = !allow;
                    var master = SSCBondUtility.GetBoundMaster(p); var other = SocialPawn(); Comp(p).selectedTrainer = other;
                    Require(SSCRestrictionEditor.TryReset(p, SSCRestrictionEditor.GetResetView(p).Context, out _));
                    var rules = Comp(p).restrictionConfig.rules;
                    foreach (var rule in SSCRestrictionRules.All) Equal(defaults.Get(rule), rules.Get(rule));
                    Equal(SSCRestrictionValue.Allow, rules.ConsensualTarget);
                    Equal(allow ? other : master, Comp(p).selectedTrainer);
                });
            Check("personal sex missing global template uses original module defaults", () =>
            {
                SSCMod.settings = new SSCSettings { restrictionDefaults = null }; var p = SexResetPawn();
                Require(SSCRestrictionEditor.TryReset(p, SSCRestrictionEditor.GetResetView(p).Context, out _));
                var defaults = new SSCRestrictionRules();
                foreach (var rule in SSCRestrictionRules.All) Equal(defaults.Get(rule), Comp(p).restrictionConfig.rules.Get(rule));
            });
            foreach (bool bus in new[] { false, true })
                Check("personal sex restores current template and specialization without saving forced layer " + bus, () =>
                {
                    SSCMod.settings = new SSCSettings { restrictionDefaults = new SSCRestrictionRules { allowMasturbation = true } };
                    var p = SexResetPawn(); if (bus) Comp(p).specializationType = SexSlaveSpecializationType.Bus;
                    var old = Comp(p).restrictionConfig; var romance = Comp(p).romanceConfig = InvertedRomanceDefaults();
                    var template = SSCMod.settings.restrictionDefaults; var copy = template.Copy();
                    var v = SSCRestrictionEditor.GetResetView(p); Require(v.CanReset); Equal(old, Comp(p).restrictionConfig);
                    Require(SSCRestrictionEditor.TryReset(p, v.Context, out var reason), reason);
                    Require(Comp(p).restrictionConfig != old); var rules = Comp(p).restrictionConfig.rules;
                    Require(rules.allowMasturbation && !rules.receiveForced); Equal(bus, rules.receiveConsensual);
                    Equal(bus, Comp(p).restrictionConfig.busDefaultsApplied); Equal(romance, Comp(p).romanceConfig);
                    foreach (var r in SSCRestrictionRules.All) Equal(copy.Get(r), template.Get(r));
                    if (bus) Equal(SSCRestrictionValue.Allow, SSCRestrictionResolver.Resolve(p, rules, SSCRestrictionRule.ReceiveForced).Value);
                    Require(!SSCRestrictionEditor.GetResetView(p).CanReset);
                });
            foreach (string mode in new[] { "value", "hidden-target", "reference", "template", "profile", "specialization", "token", "master", "chain" })
                Check("personal sex rejects changes during candidate build " + mode, () =>
                {
                    SSCMod.settings = new SSCSettings(); var p = SexResetPawn();
                    var v = SSCRestrictionEditor.GetResetView(p); Require(v.CanReset);
                    var oldDefault = profile.defaults.receiveConsensual;
                    personalResetDuringBuild = () =>
                    {
                        if (mode == "value") Comp(p).restrictionConfig.rules.receiveForced = true;
                        if (mode == "hidden-target") { Comp(p).restrictionConfig.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow); Comp(p).restrictionConfig.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny); }
                        if (mode == "reference") Comp(p).restrictionConfig = Comp(p).restrictionConfig.Copy();
                        if (mode == "template") SSCMod.settings.restrictionDefaults.allowSelfTraining = false;
                        if (mode == "profile") profile.defaults.receiveConsensual = SSCRestrictionValue.Deny;
                        if (mode == "specialization") Comp(p).specializationType = SexSlaveSpecializationType.Bus;
                        if (mode == "token") { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var s)); s.Dispose(); }
                        if (mode == "master") SSCBondUtility.GetChain(p).target = SocialPawn();
                        if (mode == "chain") p.health.hediffSet.hediffs.Clear();
                    };
                    var old = Comp(p).restrictionConfig;
                    try { Require(!SSCRestrictionEditor.TryReset(p, v.Context, out _)); if (mode != "reference") Equal(old, Comp(p).restrictionConfig); }
                    finally { personalResetDuringBuild = null; profile.defaults.receiveConsensual = oldDefault; }
                });
            foreach (string mode in new[] { "unknown", "legacy", "restore", "invalid-template", "unbound", "non-player", "loading" })
                Check("personal sex does not repair protected or invalid input " + mode, () =>
                {
                    SSCMod.settings = new SSCSettings(); var p = SexResetPawn();
                    if (mode == "unknown") Comp(p).restrictionConfig.version = 99;
                    if (mode == "legacy") Comp(p).legacyRestrictionInput = new();
                    if (mode == "restore") AccessTools.Field(typeof(CompSexSlaveTraining), "restrictionRestoreDepth").SetValue(Comp(p), 1);
                    if (mode == "invalid-template") SSCMod.settings.restrictionDefaults.consensualInitiation = (SSCRestrictionValue)99;
                    if (mode == "unbound") p.health.hediffSet.hediffs.Clear();
                    if (mode == "non-player") AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, null);
                    if (mode == "loading") Scribe.mode = LoadSaveMode.LoadingVars;
                    var old = Comp(p).restrictionConfig;
                    try { var v = SSCRestrictionEditor.GetResetView(p); Require(!v.CanReset); Require(!SSCRestrictionEditor.TryReset(p, v.Context, out _)); Equal(old, Comp(p).restrictionConfig); }
                    finally { Scribe.mode = LoadSaveMode.Inactive; }
                });
            Check("personal reset results survive native Scribe without reset contexts", () =>
            {
                SSCMod.settings = new SSCSettings(); var p = SexResetPawn();
                Comp(p).romanceConfig = InvertedRomanceDefaults();
                Require(SSCRestrictionEditor.TryReset(p, SSCRestrictionEditor.GetResetView(p).Context, out _));
                Require(SSCRomancePreferences.TryReset(p, SSCRomancePreferences.GetResetView(p).Context, out _));
                var sex = Comp(p).restrictionConfig; var romance = Comp(p).romanceConfig;
                var path = Path.Combine(saveDirectory, "personal-reset-" + (++id) + ".xml");
                Scribe.saver.InitSaving(path, "PersonalReset"); Scribe_Deep.Look(ref sex, "sex"); Scribe_Deep.Look(ref romance, "romance"); Scribe.saver.FinalizeSaving();
                Require(!File.ReadAllText(path).Contains("Context"));
                SSCRestrictionConfig restoredSex = null; SSCRomanceConfig restoredRomance = null;
                Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref restoredSex, "sex"); Scribe_Deep.Look(ref restoredRomance, "romance"); Scribe.loader.FinalizeLoading();
                foreach (var r in SSCRestrictionRules.All) Equal(sex.rules.Get(r), restoredSex.rules.Get(r));
                Equal(sex.rules.ConsensualTarget, restoredSex.rules.ConsensualTarget); Require(restoredRomance.IsDefault);
                Equal(sex.busDefaultsApplied, restoredSex.busDefaultsApplied);
            });
            Check("personal sex defaults remain editable while global enforcement is paused", () =>
            {
                SSCMod.settings = new SSCSettings { enableSexSlaveProtectionRules = false, enableSpecializationRestrictionOverrides = false };
                var p = SexResetPawn(); Comp(p).specializationType = SexSlaveSpecializationType.Bus;
                Require(SSCRestrictionEditor.TryReset(p, SSCRestrictionEditor.GetResetView(p).Context, out _));
                Require(Comp(p).restrictionConfig.rules.receiveConsensual && !Comp(p).restrictionConfig.rules.receiveForced);
                Require(!SSCMod.settings.enableSexSlaveProtectionRules && !SSCMod.settings.enableSpecializationRestrictionOverrides);
            });
            Check("personal reset old requests cannot replay and sex initialization remains explicit", () =>
            {
                SSCMod.settings = new SSCSettings(); var p = SexResetPawn(); Comp(p).romanceConfig = InvertedRomanceDefaults();
                var sex = SSCRestrictionEditor.GetResetView(p); var romance = SSCRomancePreferences.GetResetView(p);
                Require(SSCRestrictionEditor.TryReset(p, sex.Context, out _)); Require(!SSCRestrictionEditor.TryReset(p, sex.Context, out _));
                Require(SSCRomancePreferences.TryReset(p, romance.Context, out _)); Require(!SSCRomancePreferences.TryReset(p, romance.Context, out _));
                Comp(p).restrictionConfig = null; var empty = SSCRestrictionEditor.GetResetView(p);
                Require(!empty.CanReset && !SSCRestrictionEditor.TryReset(p, empty.Context, out _)); Require(Comp(p).restrictionConfig == null);
            });
        }
        finally { SSCMod.settings = previous; personalResetDuringBuild = null; boundary.UnpatchAll(boundary.Id); AccessTools.Method(typeof(DefDatabase<SSCRestrictionProfileDef>), "Remove").Invoke(null, new object[] { profile }); }
    }

    /// <summary>所有十一项均与出厂值相反，以免只恢复部分字段仍能通过。</summary>
    private static SSCRomanceConfig InvertedRomanceDefaults() => new() { allowInitiateRomance = false, allowInitiateProposal = false,
        allowInitiateBreakup = false, allowAcceptRomance = false, allowAcceptProposal = false,
        allowInitiateToMaster = false, allowInitiateToSexSlave = false, allowInitiateToOthers = false,
        alwaysAcceptMaster = true, alwaysAcceptSexSlave = true, alwaysAcceptOthers = true };
    /// <summary>只建立当前适用个人与非默认七项规则，不模拟整张地图或默认初始化业务。</summary>
    private static Pawn SexResetPawn()
    {
        var p = SocialSlave(); Comp(p).restrictionConfig = new SSCRestrictionConfig { rules = new SSCRestrictionRules { allowForcedInitiation = true, allowSelfTraining = false, consensualInitiation = SSCRestrictionValue.Deny } };
        return p;
    }
    /// <summary>在真实候选构建返回后注入一次外部变更，验证提交前的末次上下文检查。</summary>
    private static void PersonalResetBuildCallback()
    { var callback = personalResetDuringBuild; personalResetDuringBuild = null; callback?.Invoke(); }
}
