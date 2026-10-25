// AI 辅助生成
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn reviewRebindActor, reviewRestoreOwner;
    /// <summary>模拟关系清理回调恢复原主人；只触发一次，实际换主仍调用生产入口。</summary>
    private static void RestoreOwnerDuringCleanup(Pawn pawn)
    {
        if (pawn != reviewRebindActor || reviewRestoreOwner == null) return;
        var owner = reviewRestoreOwner; reviewRestoreOwner = null;
        Hediff_ChainOfSexSlave.AddToPawn(pawn, owner);
    }
    /// <summary>通过真实第四阶段入口取得求爱凭据，失败立即令所属用例报错。</summary>
    private static SSCRomanceAuthorization Authorize(Pawn actor, Pawn target)
    { Require(SSCRomanceLifecycle.TryAuthorize(actor, target, SSCRomanceAction.Romance, out var auth)); return auth; }
    /// <summary>执行退阶、换主、删链、人格及克隆事件，验证旧世代失效后不能复活。</summary>
    private static void LifecycleTests()
    {
        Check("command overrides only actor active restriction and only this action/target", () =>
        {
            var a = Slave(); var b = Slave(); Comp(a).romanceConfig = Denied(); Comp(b).romanceConfig = Denied(); var auth = Authorize(a, b);
            var r = SSCRomancePolicy.Evaluate(a, b, SSCRomanceAction.Romance, auth); Require(r.CanInitiate); Equal(SSCRomanceReception.ForceReject, r.Reception);
            Require(!SSCRomancePolicy.Evaluate(a, b, SSCRomanceAction.Proposal, auth).CanInitiate);
            Require(!SSCRomancePolicy.Evaluate(a, Pawn(), SSCRomanceAction.Romance, auth).CanInitiate);
            Require(!Decide(a, b).CanInitiate);
        });
        foreach (string change in new[] { "stage", "owner", "remove", "personality", "component", "identity" })
            Check("old authorization cannot revive after " + change, () =>
            {
                var a = Slave(); var b = Pawn(); var chain = SSCBondUtility.GetChain(a); var owner = chain.target; var auth = Authorize(a, b);
                switch (change)
                {
                    case "stage": chain.Severity = .5f; chain.Severity = .9f; break;
                    case "owner": Hediff_ChainOfSexSlave.AddToPawn(a, Pawn(PawnIdentity.Master)); Hediff_ChainOfSexSlave.AddToPawn(a, owner as Pawn); break;
                    case "remove": a.health.hediffSet.hediffs.Remove(chain); a.health.Notify_HediffChanged(chain); a.health.hediffSet.hediffs.Add(chain); SSCRomanceLifecycle.Notify(a); break;
                    case "personality": Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a, out var scope)); using (scope) { } break;
                    case "component": a.AllComps.Clear(); a.AllComps.Add(new CompSexSlaveTraining { parent = a, pawnIdentity = PawnIdentity.Slave }); break;
                    case "identity": Comp(a).pawnIdentity = PawnIdentity.Unset; SSCRomanceLifecycle.Notify(a); Comp(a).pawnIdentity = PawnIdentity.Slave; SSCRomanceLifecycle.Notify(a); break;
                }
                Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance)); Require(!SSCRomanceLifecycle.TryResume(auth.Stamp, out _));
            });
        Check("direct owner change is detected and old stamp cannot authorize different master", () =>
        { var a = Slave(); var b = Pawn(); var auth = Authorize(a, b); SSCBondUtility.GetChain(a).target = Pawn(); Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance)); Require(!SSCRomanceLifecycle.TryResume(auth.Stamp, out _)); });
        Check("relation cleanup restoring original owner cannot conceal intermediate rebind", () =>
        {
            var a = Slave(); var b = Pawn(); var auth = Authorize(a, b); reviewRebindActor = a; reviewRestoreOwner = SSCBondUtility.GetBoundMaster(a);
            var callbackPatch = new Harmony("ssc.tests.romance.reentrant-owner");
            try
            {
                callbackPatch.Patch(AccessTools.Method(typeof(SSCBondRelationUtility), nameof(SSCBondRelationUtility.RemoveInvalidRelations)),
                    prefix: new HarmonyMethod(typeof(Program), nameof(RestoreOwnerDuringCleanup)));
                Hediff_ChainOfSexSlave.AddToPawn(a, Pawn(PawnIdentity.Master));
                Equal(auth.Stamp.Master, SSCBondUtility.GetBoundMaster(a));
                Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance)); Require(!SSCRomanceLifecycle.TryResume(auth.Stamp, out _));
            }
            finally { callbackPatch.UnpatchAll(callbackPatch.Id); reviewRebindActor = reviewRestoreOwner = null; }
        });
        Check("repeat lifecycle observation does not invalidate unchanged authorization", () =>
        { var a = Slave(); var b = Pawn(); var auth = Authorize(a, b); for (int i = 0; i < 10; i++) SSCRomanceLifecycle.Notify(a); Require(auth.IsCurrent(a, b, SSCRomanceAction.Romance)); Require(SSCRomanceLifecycle.TryResume(auth.Stamp, out _)); });
        Check("hollow health notifications revoke old authorization even after marker removal", () =>
        {
            var a = Slave(); var b = Pawn(); var auth = Authorize(a, b);
            var shell = new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done, pawn = a };
            var callback = AccessTools.Method(production.GetType("SexSlaveCraft.SSCAbilityToggleHealthRemoved"), "Postfix");
            // 使用现有健康事件转发器，模拟外部添加／移除空壳状态而不经过 SSC 人格替换作用域。
            a.health.hediffSet.hediffs.Add(shell); callback.Invoke(null, new object[] { shell });
            Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance));
            a.health.hediffSet.hediffs.Remove(shell); callback.Invoke(null, new object[] { shell });
            Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance)); Require(!SSCRomanceLifecycle.TryResume(auth.Stamp, out _));
        });
        Check("first hollow health observation invalidates a previously captured edit context", () =>
        {
            var a = Slave(); var view = SSCRomancePreferences.GetView(a, SSCRomanceSetting.AcceptRomance);
            AccessTools.Field(typeof(CompSexSlaveTraining), "romanceObserved").SetValue(Comp(a), false);
            var shell = new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done, pawn = a };
            var callback = AccessTools.Method(production.GetType("SexSlaveCraft.SSCAbilityToggleHealthRemoved"), "Postfix");
            a.health.hediffSet.hediffs.Add(shell); callback.Invoke(null, new object[] { shell });
            a.health.hediffSet.hediffs.Remove(shell); callback.Invoke(null, new object[] { shell });
            Require(!SSCRomancePreferences.TrySet(a, SSCRomanceSetting.AcceptRomance, false, view.Context, out var reason));
            Equal(SSCRomanceReason.StaleContext, reason);
        });
        Check("chain without a valid persistent load ID cannot create command authorization", () =>
        { var a = Slave(); SSCBondUtility.GetChain(a).loadID = -1; Require(!SSCRomanceLifecycle.TryAuthorize(a, Pawn(), SSCRomanceAction.Romance, out _)); });
        foreach (float severity in new[] { .1f, .3f, .5f, .8999f })
            Check("commands locked below fourth stage " + severity, () => Require(!SSCRomanceLifecycle.TryAuthorize(Slave(severity), Pawn(), SSCRomanceAction.Romance, out _)));
        Check("mutation failure/exception always releases scope but keeps old authorization invalid", () =>
        {
            var a = Slave(); var b = Pawn(); var auth = Authorize(a, b);
            try { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a, out var scope)); using (scope) { Require(!SSCPersonalityRuntimeUtility.TryBeginMutation(a, out _)); throw new InvalidOperationException("fixture"); } }
            catch (InvalidOperationException) { }
            Require(!SSCPersonalityRuntimeUtility.IsMutating(a)); Require(!SSCRomanceLifecycle.TryResume(auth.Stamp, out _));
        });
        Check("actual existing clone postfix resets romance without touching source/binding/abilities", () =>
        {
            var source = Slave(); Comp(source).romanceConfig = Denied(); var clone = Slave(); Comp(clone).romanceConfig = Denied();
            Comp(clone).abilityToggles.Set("FixtureAbility", true); var chain = SSCBondUtility.GetChain(clone); var owner = chain.target;
            var auth = Authorize(clone, Pawn());
            AccessTools.Method(production.GetType("SexSlaveCraft.SSCRestrictionCloneHook"), "Postfix").Invoke(null, new object[] { clone });
            Require(Comp(clone).romanceConfig == null); Require(!Comp(source).romanceConfig.IsDefault);
            foreach (var setting in Enum.GetValues<SSCRomanceSetting>()) Equal(setting <= SSCRomanceSetting.AcceptProposal || setting == SSCRomanceSetting.InitiateToSexSlave, SSCRomancePreferences.GetView(clone, setting).Saved);
            Require(Comp(clone).abilityToggles.Get("FixtureAbility")); Require(ReferenceEquals(chain, SSCBondUtility.GetChain(clone))); Equal(owner, chain.target);
            Require(!SSCRomanceLifecycle.TryResume(auth.Stamp, out _));
        });
        Check("normal lifecycle after clone edit does not reapply newborn defaults", () =>
        {
            var p = Slave(); SSCRomanceLifecycle.ResetNewClone(p); var v = SSCRomancePreferences.GetView(p, SSCRomanceSetting.InitiateRomance);
            Require(SSCRomancePreferences.TrySet(p, SSCRomanceSetting.InitiateRomance, false, v.Context, out _));
            SSCRomanceLifecycle.Notify(p); Require(!Decide(p, Pawn()).CanInitiate);
        });
    }
    /// <summary>执行生产采集、加工与恢复接口，验证原始选择、副本隔离和破坏宿主前的预检。</summary>
    private static void SnapshotTests()
    {
        Check("real StorePawnData captures dormant raw config independently", () =>
        {
            var p = Slave(.1f); Comp(p).romanceConfig = Denied(); var store = new CompPersonalityStore(); store.StorePawnData(p);
            Require(!ReferenceEquals(store.romanceConfig, Comp(p).romanceConfig)); Require(!store.romanceConfig.allowInitiateRomance && store.romanceConfig.alwaysAcceptMaster);
            Comp(p).romanceConfig.allowInitiateRomance = true; Require(!store.romanceConfig.allowInitiateRomance);
        });
        Check("real gel CopyFrom copies config independently and old snapshot overwrites stale destination", () =>
        {
            var source = new CompPersonalityStore { romanceConfig = Denied() }; var copy = new CompPersonalityStore(); copy.CopyFrom(source);
            Require(!ReferenceEquals(source.romanceConfig, copy.romanceConfig)); source.romanceConfig.allowAcceptProposal = true; Require(!copy.romanceConfig.allowAcceptProposal);
            source.romanceConfig = null; copy.CopyFrom(source); Require(copy.romanceConfig == null);
        });
        foreach (bool sourceDefault in new[] { false, true })
        foreach (bool targetDefault in new[] { false, true })
            Check($"precise restore sourceDefault={sourceDefault}/targetDefault={targetDefault}", () =>
            {
                var p = Slave(); Comp(p).romanceConfig = targetDefault ? null : Denied(); var incoming = sourceDefault ? null : Denied();
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); using (scope) { SSCRomancePreferences.Clear(p); Require(SSCRomancePreferences.Restore(p, incoming)); }
                Equal(sourceDefault, Comp(p).romanceConfig == null);
                if (incoming != null) { Require(!ReferenceEquals(incoming, Comp(p).romanceConfig)); Require(!Comp(p).romanceConfig.allowAcceptRomance); Require(Comp(p).romanceConfig.initiateOnlyMaster); }
            });
        Check("unknown version and missing carrier are rejected before replacing saved preference", () =>
        {
            var p = Slave(); var old = Comp(p).romanceConfig = Denied(); var unknown = Denied(); unknown.version = 88;
            Require(!SSCRomancePreferences.Restore(p, unknown)); Require(ReferenceEquals(old, Comp(p).romanceConfig));
            var bare = Pawn(); AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(bare, null);
            Require(!SSCRomancePreferences.CanRestore(bare, Denied())); Require(SSCRomancePreferences.CanRestore(bare, null));
            Require(!SSCRomancePreferences.CanRestore(null, null)); Require(!SSCRomancePreferences.Restore(null, null));
        });
    }
    /// <summary>补充核对完整场景入口的接线及未实现边界；静态检查不冒充完整任务实测。</summary>
    private static void WiringTests(string repo)
    {
        // 补充静态接线检查；不把这些检查称作已执行完整人格提取／植入或完整克隆出生。
        Check("shared personality consumers wire preflight, clear-source and precise final restore under RW17", () =>
        {
            string text = File.ReadAllText(Path.Combine(repo, "Sexslavecraft/common/ExcretionUtility.cs"));
            Require(text.IndexOf("SSCRomancePreferences.CanRestore") < text.IndexOf("CleanConsumerDataCore(consumer);", text.IndexOf("public static bool InheritEverything")));
            Require(text.IndexOf("GenSpawn.Spawn(slime") < text.IndexOf("SSCRomancePreferences.Clear(victim)"));
            Require(text.Contains("SSCRomancePreferences.Clear(consumer)") && text.Contains("SSCRomancePreferences.Restore(consumer, romanceSnapshot)"));
            text = File.ReadAllText(Path.Combine(repo, "Sexslavecraft/JobDriver/JobDriver_PE.cs"));
            Require(text.IndexOf("originalComp?.romanceConfig?.IsSupported") < text.IndexOf("StripVictimPersonality(victim)"));
            Require(text.IndexOf("GenSpawn.Spawn(product") < text.IndexOf("SSCRomancePreferences.Clear(victim)"));
        });
        Check("maturation never reapplies romance defaults and rule layer never writes relations or installs hooks", () =>
        {
            string text = File.ReadAllText(Path.Combine(repo, "Sexslavecraft/common/RabbitCloneUtility.cs")); Require(!text.Contains("SSCRomanceLifecycle.ResetNewClone"));
            foreach (string path in Directory.GetFiles(Path.Combine(repo, "Sexslavecraft/common/Romance"), "*.cs"))
            { text = File.ReadAllText(path); Require(!text.Contains("AddDirectRelation(") && !text.Contains("RemoveDirectRelation(") && !text.Contains("HarmonyPatch(")); }
        });
    }
}
