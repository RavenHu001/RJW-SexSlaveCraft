using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>覆盖真实生产生命周期的迁移、恢复、复制和批量事件；替身仅提供游戏对象与存档边界。</summary>
    private static void RunLifecycleTests()
    {
        Run("All legacy flag combinations map the six rules without equipment or total-switch leakage", () =>
        {
            for (int flags = 0; flags < 64; flags++)
            {
                bool ownerOnly = (flags & 1) != 0, rape = (flags & 2) != 0, busProtect = (flags & 4) != 0;
                bool otherProtect = (flags & 8) != 0, open = (flags & 16) != 0, bus = (flags & 32) != 0;
                var legacy = new SSCRestrictionLegacySettings { ownerOnly = ownerOnly, allowForcedReception = rape,
                    protectBusInitiator = busProtect, protectOtherInitiator = otherProtect };
                var config = legacy.Convert(open, bus);
                bool consensual = !ownerOnly || open || bus;
                Equal(consensual, config.rules.allowMasturbation);
                Equal(consensual ? SSCRestrictionValue.Allow : SSCRestrictionValue.OwnerOnly, config.rules.consensualInitiation);
                Equal(bus ? !busProtect : !otherProtect, config.rules.allowForcedInitiation);
                Equal(consensual, config.rules.receiveConsensual);
                Equal(rape || bus, config.rules.receiveForced);
                Equal(open || bus, config.rules.receiveTraining);
                Equal(bus, config.busDefaultsApplied);
            }
        });
        Run("Old settings template migrates globals once and preserves explicit phase-one templates", () =>
        {
            var settings = new SSCSettings { protectNonRapeOwnerOnly = false, allowSexSlaveRape = true,
                protectChainedAggressorRape = false };
            Scribe.node = new Dictionary<string, object> { ["protectNonRapeOwnerOnly"] = false,
                ["allowSexSlaveRape"] = true, ["protectChainedAggressorRape"] = false };
            Scribe.mode = LoadSaveMode.LoadingVars; settings.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; settings.ExposeData();
            Equal(true, settings.restrictionDefaults.allowMasturbation);
            Equal(true, settings.restrictionDefaults.receiveForced);
            Equal(true, settings.restrictionDefaults.allowForcedInitiation);
            Equal(false, settings.restrictionDefaults.receiveTraining);
            settings.restrictionDefaults.receiveForced = false;
            settings.ExposeData(); Equal(false, settings.restrictionDefaults.receiveForced);
        });
        Run("Retired globals survive settings save and migrate another old save independently of new defaults", () =>
        {
            // 模拟先升级一个存档并改过新默认，再打开另一个仍未升级的旧存档。
            // 原来的全局值必须保留，不能因旧控件删除而退回出厂值或套用新默认。
            var settings = new SSCSettings();
            Scribe.node = new Dictionary<string, object> { ["protectNonRapeOwnerOnly"] = false,
                ["allowSexSlaveRape"] = true, ["protectBusAggressorRape"] = false,
                ["protectChainedAggressorRape"] = false };
            Scribe.mode = LoadSaveMode.LoadingVars; settings.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; settings.ExposeData();
            settings.restrictionDefaults = new SSCRestrictionRules();
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; settings.ExposeData();
            Equal(true, Scribe.node["allowSexSlaveRape"]);
            var reloaded = new SSCSettings();
            Scribe.mode = LoadSaveMode.LoadingVars; reloaded.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; reloaded.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive; SSCMod.settings = reloaded;
            Pawn old = FreshBoundPawn("second old save");
            LoadOldRestrictions(old); StartRestrictionGame(old);
            Equal(true, old.Training.restrictionConfig.rules.receiveForced);
            Equal(true, old.Training.restrictionConfig.rules.receiveConsensual);
            Equal(true, old.Training.restrictionConfig.rules.allowForcedInitiation);
            Equal(false, reloaded.restrictionDefaults.receiveForced);
            Equal(false, reloaded.restrictionDefaults.receiveConsensual);
            Equal(false, reloaded.protectBusAggressorRape);
        });
        Run("Missing retired keys use historical defaults and explicit new values are never remigrated", () =>
        {
            var settings = new SSCSettings();
            Scribe.mode = LoadSaveMode.LoadingVars; settings.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; settings.ExposeData();
            Equal(false, settings.restrictionDefaults.receiveForced);
            Equal(SSCRestrictionValue.OwnerOnly, settings.restrictionDefaults.consensualInitiation);
            settings.restrictionDefaults.receiveTraining = true;
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; settings.ExposeData();
            Scribe.node["protectNonRapeOwnerOnly"] = false;
            Scribe.node["allowSexSlaveRape"] = true;
            var restored = new SSCSettings();
            Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; restored.ExposeData();
            Equal(true, restored.restrictionDefaults.receiveTraining);
            Equal(false, restored.restrictionDefaults.receiveForced);
            Equal(SSCRestrictionValue.OwnerOnly, restored.restrictionDefaults.consensualInitiation);
        });
        Run("Closed legacy pawn keeps its designated trainer while the unified system is disabled", () =>
        {
            Pawn pawn = FreshBoundPawn("old closed"), owner = FreshPawn("owner"), other = FreshPawn("trainer");
            pawn.BoundMaster = owner; pawn.Training.selectedTrainer = other;
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            LoadOldRestrictions(pawn); StartRestrictionGame(pawn, owner, other);
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
            Equal(other, pawn.Training.selectedTrainer);
            SSCMod.settings.enableSexSlaveProtectionRules = true;
            SSCRestrictionGameComponent.SettingsChanged();
            Equal(owner, pawn.Training.selectedTrainer);
        });
        Run("Old eligible pawn captures pre-repair choices and migrates once after references", () =>
        {
            Pawn pawn = FreshBoundPawn("old");
            pawn.Training.allowOthersForTrainingOrSex = true;
            LoadOldRestrictions(pawn);
            Equal(true, pawn.Training.legacyRestrictionInput.open);
            pawn.Training.allowOthersForTrainingOrSex = false;
            StartRestrictionGame(pawn);
            Equal(true, SSCTrainerIdentityMigration.Ran);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
            Equal(null, pawn.Training.legacyRestrictionInput);
            pawn.Training.restrictionConfig.rules.receiveTraining = false;
            Current.Game.Restrictions.FinalizeInit();
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Old inactive pawn consumes the legacy marker then uses new defaults on first activation", () =>
        {
            Pawn pawn = FreshPawn("ordinary");
            pawn.Training.allowOthersForTrainingOrSex = true;
            LoadOldRestrictions(pawn);
            Equal(true, pawn.Training.restrictionLifecycleSeen);
            Equal(null, pawn.Training.legacyRestrictionInput);
            StartRestrictionGame(pawn);
            Equal(null, pawn.Training.restrictionConfig);
            SSCMod.settings.restrictionDefaults.receiveTraining = false;
            pawn.Training.pawnIdentity = PawnIdentity.Slave;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(null, pawn.Training.restrictionConfig);
            pawn.BoundMaster = FreshPawn("first owner", PawnIdentity.Master);
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Legacy binding without SSC identity still migrates its own old choices", () =>
        {
            Pawn pawn = FreshPawn("bound legacy"), owner = FreshPawn("owner", PawnIdentity.Master);
            pawn.BoundMaster = owner;
            LoadOldRestrictions(pawn);
            Equal(true, pawn.Training.legacyRestrictionInput != null);
            StartRestrictionGame(pawn, owner);
            Equal(true, pawn.Training.restrictionConfig != null);
            Equal(owner, pawn.Training.selectedTrainer);
        });
        Run("Fresh eligible pawn initializes while disabled and keeps its activation-time defaults", () =>
        {
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            SSCMod.settings.restrictionDefaults.receiveTraining = true;
            Pawn pawn = FreshBoundPawn("new");
            StartRestrictionGame(pawn);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
            SSCMod.settings.restrictionDefaults.receiveTraining = false;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
            Equal(false, ReferenceEquals(SSCMod.settings.restrictionDefaults, pawn.Training.restrictionConfig.rules));
        });
        Run("Delayed legacy pawn uses persisted save snapshot after global settings change", () =>
        {
            SSCMod.settings.protectNonRapeOwnerOnly = false;
            StartRestrictionGame();
            Scribe.mode = LoadSaveMode.Saving; Current.Game.Restrictions.ExposeData();
            var snapshot = Scribe.node;
            SSCMod.settings.protectNonRapeOwnerOnly = true;
            Current.Game = new Game();
            Current.Game.Restrictions = new SSCRestrictionGameComponent(Current.Game);
            Scribe.mode = LoadSaveMode.LoadingVars; Current.Game.Restrictions.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; Current.Game.Restrictions.ExposeData();
            Scribe.mode = LoadSaveMode.Inactive; Current.Game.Restrictions.FinalizeInit();
            Pawn delayed = FreshBoundPawn("delayed");
            LoadOldRestrictions(delayed);
            SSCRestrictionGameComponent.Notify(delayed);
            Equal(true, delayed.Training.restrictionConfig.rules.receiveConsensual);
            Equal(false, SSCMod.settings.restrictionDefaults.receiveConsensual);
        });
        Run("Pending legacy migration cannot be replaced by manual initialization", () =>
        {
            Pawn pawn = FreshBoundPawn("pending");
            LoadOldRestrictions(pawn);
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn));
            Equal(false, SSCRestrictionLifecycle.Refresh(pawn, null, out _));
            Equal(null, pawn.Training.restrictionConfig);
            Equal(true, pawn.Training.legacyRestrictionInput != null);
        });
        Run("Pawn migration markers and pending inputs survive save reload", () =>
        {
            Pawn pawn = FreshBoundPawn("old bus"); pawn.HasBusState = true;
            pawn.Training.allowOthersForTrainingOrSex = true;
            LoadOldRestrictions(pawn);
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; pawn.Training.ExposeRestrictions();
            Equal(false, Scribe.node.ContainsKey("allowOthersForTrainingOrSex"));
            Pawn restored = FreshBoundPawn("restored");
            Scribe.mode = LoadSaveMode.LoadingVars; restored.Training.ExposeRestrictions();
            Scribe.mode = LoadSaveMode.PostLoadInit; restored.Training.ExposeRestrictions();
            Equal(true, restored.Training.restrictionLifecycleSeen);
            Equal(true, restored.Training.legacyRestrictionInput.bus);
            Equal(true, restored.Training.legacyRestrictionInput.open);
            Equal(false, restored.Training.allowOthersForTrainingOrSex);
            Scribe.mode = LoadSaveMode.Inactive;
            StartRestrictionGame(restored);
            Equal(true, restored.Training.restrictionConfig.busDefaultsApplied);
            Equal(true, restored.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Existing phase-one values survive upgrade even when old fields disagree", () =>
        {
            Pawn pawn = FreshBoundPawn("phase one");
            pawn.Training.restrictionConfig = new SSCRestrictionConfig { rules = PermissiveRules() };
            Scribe.mode = LoadSaveMode.Saving; pawn.Training.ExposeRestrictions();
            Scribe.node.Remove("sscRestrictionLifecycleSeen");
            Scribe.mode = LoadSaveMode.LoadingVars; pawn.Training.ExposeRestrictions();
            Scribe.mode = LoadSaveMode.PostLoadInit; pawn.Training.ExposeRestrictions();
            Scribe.mode = LoadSaveMode.Inactive;
            StartRestrictionGame(pawn);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
            Equal(null, pawn.Training.legacyRestrictionInput);
        });
        Run("First bus acquisition applies only bus defaults once even with overrides off", () =>
        {
            Pawn pawn = FreshBoundPawn("new");
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            StartRestrictionGame(pawn);
            pawn.Training.restrictionConfig.rules.allowMasturbation = true;
            pawn.Training.specializationType = SexSlaveSpecializationType.Bus;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveForced);
            Equal(true, pawn.Training.restrictionConfig.rules.allowMasturbation);
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
            pawn.Training.restrictionConfig.rules.receiveForced = false;
            pawn.Training.specializationType = SexSlaveSpecializationType.None;
            SSCRestrictionGameComponent.Notify(pawn);
            pawn.HasBusState = true;
            SSCRestrictionGameComponent.Notify(pawn);
            SSCMod.settings.enableSpecializationRestrictionOverrides = true;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(false, pawn.Training.restrictionConfig.rules.receiveForced);
        });
        Run("Invalid first bus defaults are atomic and can succeed after correction", () =>
        {
            Pawn pawn = FreshBoundPawn("bus");
            StartRestrictionGame(pawn);
            pawn.Training.specializationType = SexSlaveSpecializationType.Bus;
            BusProfile().defaults.receiveForced = SSCRestrictionValue.OwnerOnly;
            SSCRestrictionGameComponent.Notify(pawn);
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(false, pawn.Training.restrictionConfig.busDefaultsApplied);
            Equal(false, pawn.Training.restrictionConfig.rules.receiveConsensual);
            Equal(1, Log.Errors.Count);
            BusProfile().defaults.receiveForced = SSCRestrictionValue.Allow;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(true, pawn.Training.restrictionConfig.busDefaultsApplied);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveConsensual);
        });
        Run("Legacy bus preserves its training mapping without applying new defaults again", () =>
        {
            Pawn pawn = FreshBoundPawn("legacy bus"); pawn.HasBusState = true;
            LoadOldRestrictions(pawn);
            BusProfile().defaults.receiveForced = SSCRestrictionValue.Deny;
            StartRestrictionGame(pawn);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveForced);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Personality insertion preserves recipient choices and consumes current bus defaults", () =>
        {
            Pawn pawn = FreshBoundPawn("recipient");
            StartRestrictionGame(pawn);
            SSCRestrictionConfig original = pawn.Training.restrictionConfig;
            using (SSCRestrictionLifecycle.BeginRestore(pawn))
            {
                pawn.Training.pawnIdentity = PawnIdentity.Unset;
                pawn.Training.specializationType = SexSlaveSpecializationType.Bus;
                pawn.Training.pawnIdentity = PawnIdentity.Slave;
                SSCRestrictionGameComponent.Notify(pawn);
                Equal(false, original.busDefaultsApplied);
                Equal(false, original.rules.receiveForced);
            }
            Equal(original, pawn.Training.restrictionConfig);
            Equal(true, original.busDefaultsApplied);
            Equal(false, original.rules.receiveForced);
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(false, original.rules.receiveForced);
        });
        Run("Uninitialized personality recipient waits until the outermost restore completes", () =>
        {
            Pawn pawn = FreshPawn("empty"); StartRestrictionGame(pawn);
            using (SSCRestrictionLifecycle.BeginRestore(pawn))
            {
                using (SSCRestrictionLifecycle.BeginRestore(pawn))
                {
                    pawn.Training.pawnIdentity = PawnIdentity.Slave;
                    pawn.BoundMaster = FreshPawn("restored owner", PawnIdentity.Master);
                    pawn.HasBusState = true;
                    SSCRestrictionGameComponent.Notify(pawn);
                }
                Equal(null, pawn.Training.restrictionConfig);
            }
            Equal(true, pawn.Training.restrictionConfig.rules.receiveForced);
            Equal(0, pawn.Training.restrictionRestoreDepth);
        });
        Run("Restore exceptions release suppression and refresh the state actually left behind", () =>
        {
            Pawn pawn = FreshPawn("failed restore"); StartRestrictionGame(pawn);
            try
            {
                using (SSCRestrictionLifecycle.BeginRestore(pawn))
                {
                    pawn.Training.pawnIdentity = PawnIdentity.Slave;
                    pawn.BoundMaster = FreshPawn("restored owner", PawnIdentity.Master);
                    throw new InvalidOperationException("fixture");
                }
            }
            catch (InvalidOperationException) { }
            Equal(0, pawn.Training.restrictionRestoreDepth);
            Equal(true, pawn.Training.restrictionConfig != null);
        });
        Run("New clone drops copied objects and markers while the source remains untouched", () =>
        {
            Pawn source = FreshBoundPawn("source"), clone = FreshPawn("clone", PawnIdentity.Slave);
            StartRestrictionGame(source);
            source.Training.restrictionConfig.rules = PermissiveRules();
            source.Training.restrictionConfig.busDefaultsApplied = true;
            clone.Training.restrictionConfig = source.Training.restrictionConfig;
            clone.Training.legacyRestrictionInput = new SSCRestrictionLegacyPawn { open = true };
            SSCRestrictionLifecycle.ResetNewClone(clone);
            Equal(null, clone.Training.restrictionConfig);
            Equal(null, clone.Training.legacyRestrictionInput);
            // 新生克隆即使复制了身份也尚未启用；实际建立自己的绑定后再按当时模板创建独立配置。
            clone.BoundMaster = FreshPawn("clone owner", PawnIdentity.Master);
            SSCRestrictionGameComponent.Notify(clone);
            Equal(false, ReferenceEquals(source.Training.restrictionConfig, clone.Training.restrictionConfig));
            Equal(false, clone.Training.restrictionConfig.rules.receiveTraining);
            Equal(false, clone.Training.restrictionConfig.busDefaultsApplied);
            Equal(null, clone.Training.legacyRestrictionInput);
            Equal(true, source.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Existing old clone follows its own legacy input instead of fresh-clone reset", () =>
        {
            Pawn pawn = FreshBoundPawn("old clone");
            pawn.Training.allowOthersForTrainingOrSex = true;
            LoadOldRestrictions(pawn); StartRestrictionGame(pawn);
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Losing identity and changing owner preserve settings while coordinating the new relationship", () =>
        {
            Pawn pawn = FreshBoundPawn("target"), owner = FreshPawn("owner", PawnIdentity.Master);
            StartRestrictionGame(pawn, owner);
            var config = pawn.Training.restrictionConfig;
            pawn.Training.pawnIdentity = PawnIdentity.Unset;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(config, pawn.Training.restrictionConfig);
            pawn.BoundMaster = owner;
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(config, pawn.Training.restrictionConfig);
            Equal(owner, pawn.Training.selectedTrainer);
        });
        Run("Batch defaults deduplicate targets, include off-map objects, and supersede pending migration", () =>
        {
            Pawn map = FreshBoundPawn("map"), world = FreshBoundPawn("world");
            Pawn caravan = FreshBoundPawn("caravan"), unrelated = FreshPawn("unrelated");
            LoadOldRestrictions(world);
            var template = new SSCRestrictionRules { receiveTraining = true };
            var result = SSCRestrictionLifecycle.ApplyDefaults(new[] { map, world, caravan, unrelated, map, null }, template);
            Equal(3, result.Applied); Equal(1, result.Skipped); Equal(0, result.Failed);
            Equal(null, world.Training.legacyRestrictionInput);
            SSCRestrictionLifecycle.Refresh(world, new SSCRestrictionLegacySettings(), out _);
            Equal(true, world.Training.restrictionConfig.rules.receiveTraining);
            map.Training.restrictionConfig.rules.receiveTraining = false;
            Equal(true, world.Training.restrictionConfig.rules.receiveTraining);
            Equal(true, template.receiveTraining);
        });
        Run("Game-wide target snapshot includes explicit caravan and transport collections exactly once", () =>
        {
            Pawn map = FreshBoundPawn("map"), caravan = FreshBoundPawn("caravan");
            Pawn transport = FreshBoundPawn("transport");
            RimWorld.PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead.AddRange(new[] { caravan, transport, map });
            StartRestrictionGame(map);
            Equal(3, SSCRestrictionGameComponent.AllPawns().Count);
            Equal(true, caravan.Training.restrictionConfig != null);
            Equal(true, transport.Training.restrictionConfig != null);
        });
        Run("Batch failure preserves complete old configs and continues with other pawns", () =>
        {
            Pawn bus = FreshBoundPawn("bus"), normal = FreshBoundPawn("normal");
            StartRestrictionGame(bus, normal);
            var saved = bus.Training.restrictionConfig;
            bus.HasBusState = true;
            BusProfile().defaults.receiveForced = SSCRestrictionValue.OwnerOnly;
            var result = SSCRestrictionLifecycle.ApplyDefaults(new[] { bus, normal }, new SSCRestrictionRules { receiveTraining = true });
            Equal(1, result.Failed); Equal(1, result.Applied);
            Equal(saved, bus.Training.restrictionConfig);
            Equal(false, saved.rules.receiveTraining);
            Equal(true, normal.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Batch defaults never save equipment forcing or change the global switches", () =>
        {
            Pawn pawn = FreshBoundPawn("equipped"); pawn.HasBusState = true;
            Wear(pawn, "gear", ReadEquipment());
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            SSCRestrictionLifecycle.ApplyDefaults(new[] { pawn }, new SSCRestrictionRules());
            Equal(true, pawn.Training.restrictionConfig.rules.receiveForced);
            Equal(false, SSCMod.settings.enableSexSlaveProtectionRules);
            Equal(false, SSCMod.settings.enableSpecializationRestrictionOverrides);
            SSCMod.settings.enableSexSlaveProtectionRules = true;
            Equal(SSCRestrictionValue.Deny, SSCRestrictionResolver.Resolve(pawn, pawn.Training.restrictionConfig.rules,
                SSCRestrictionRule.ReceiveForced).Value);
            pawn.apparel.WornApparel.Clear();
            Equal(SSCRestrictionValue.Allow, SSCRestrictionResolver.Resolve(pawn, pawn.Training.restrictionConfig.rules,
                SSCRestrictionRule.ReceiveForced).Value);
        });
        Run("Disabled system preserves assignments until re-enabled then never restores hidden choices", () =>
        {
            Pawn pawn = FreshBoundPawn("slave"), owner = FreshPawn("owner"), other = FreshPawn("other");
            pawn.BoundMaster = owner; pawn.Training.selectedTrainer = other;
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            StartRestrictionGame(pawn, owner, other);
            SSCRestrictionLifecycle.ApplyDefaults(new[] { pawn }, new SSCRestrictionRules());
            Equal(other, pawn.Training.selectedTrainer);
            Equal(null, SSCRestrictionLifecycle.GetForcedTrainer(pawn));
            SSCMod.settings.enableSexSlaveProtectionRules = true;
            SSCRestrictionGameComponent.SettingsChanged();
            Equal(owner, pawn.Training.selectedTrainer);
            SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow);
            Equal(null, SSCRestrictionLifecycle.GetForcedTrainer(pawn));
            Equal(owner, pawn.Training.selectedTrainer);
        });
        Run("Allowed non-owner training keeps inactive assignments and only clears dead references", () =>
        {
            Pawn pawn = FreshBoundPawn("slave"), other = FreshPawn("inactive");
            SSCMod.settings.restrictionDefaults.receiveTraining = true;
            pawn.Training.selectedTrainer = other; StartRestrictionGame(pawn);
            Equal(other, pawn.Training.selectedTrainer);
            other.Dead = true; SSCRestrictionGameComponent.Notify(pawn);
            Equal(null, pawn.Training.selectedTrainer);
        });
        Run("Unsupported configurations stay untouched through load and lifecycle notifications", () =>
        {
            Pawn pawn = FreshBoundPawn("future");
            var future = new SSCRestrictionConfig { version = 99 }; pawn.Training.restrictionConfig = future;
            pawn.HasBusState = true; StartRestrictionGame(pawn);
            SSCRestrictionGameComponent.Notify(pawn);
            Equal(future, pawn.Training.restrictionConfig);
            Equal(99, future.version); Equal(false, future.busDefaultsApplied);
        });
        Run("Loading events do not initialize before all references and migration identities are ready", () =>
        {
            Pawn pawn = FreshBoundPawn("loading");
            Current.Game = new Game(); Current.Game.Restrictions = new SSCRestrictionGameComponent(Current.Game);
            SSCRestrictionGameComponent.Notify(pawn); Equal(null, pawn.Training.restrictionConfig);
            Scribe.mode = LoadSaveMode.LoadingVars;
            SSCRestrictionGameComponent.Notify(pawn); Equal(null, pawn.Training.restrictionConfig);
            Scribe.mode = LoadSaveMode.Inactive;
            RimWorld.PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Add(pawn);
            Current.Game.Restrictions.FinalizeInit();
            Equal(true, pawn.Training.restrictionConfig != null);
            Equal(true, SSCTrainerIdentityMigration.Ran);
        });
    }

    /// <summary>创建没有新配置的角色，并连接组件父对象以便执行生产序列化和生命周期代码。</summary>
    private static Pawn FreshPawn(string name, PawnIdentity identity = PawnIdentity.Unset)
    {
        var pawn = new Pawn { LabelShort = name };
        pawn.Training.parent = pawn;
        pawn.Training.pawnIdentity = identity;
        return pawn;
    }

    /// <summary>建立显式绑定但不创建配置，供初始化、迁移及持久化用例从生效边界开始运行。</summary>
    private static Pawn FreshBoundPawn(string name)
    {
        Pawn pawn = FreshPawn(name, PawnIdentity.Slave);
        pawn.BoundMaster = FreshPawn(name + " owner", PawnIdentity.Master);
        return pawn;
    }

    /// <summary>模拟旧档缺少所有限制字段，完成字段加载与引用恢复阶段；旧身份和例外由用例先设置。</summary>
    private static void LoadOldRestrictions(Pawn pawn)
    {
        Scribe.node = new Dictionary<string, object> { ["allowOthersForTrainingOrSex"] = pawn.Training.allowOthersForTrainingOrSex };
        Scribe.mode = LoadSaveMode.LoadingVars; pawn.Training.ExposeRestrictions();
        Scribe.mode = LoadSaveMode.PostLoadInit; pawn.Training.ExposeRestrictions();
        Scribe.mode = LoadSaveMode.Inactive;
    }

    /// <summary>以给定地图及世界角色快照创建当前存档，并调用真实的全局初始化入口。</summary>
    private static void StartRestrictionGame(params Pawn[] pawns)
    {
        Current.Game = new Game();
        Current.Game.Restrictions = new SSCRestrictionGameComponent(Current.Game);
        RimWorld.PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead = pawns.ToList();
        Current.Game.Restrictions.FinalizeInit();
    }
}
