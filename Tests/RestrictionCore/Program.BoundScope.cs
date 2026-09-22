using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>覆盖真实绑定生效门槛、未启用数据保留、重新绑定以及配置候选的原子提交边界。</summary>
    private static void RunBoundScopeTests()
    {
        Run("Slave identity and trainer assignment alone never activate ordinary restrictions", () =>
        {
            Pawn subject = Pawn("identity only", PawnIdentity.Slave), other = Pawn("other", PawnIdentity.Master);
            subject.Training.selectedTrainer = other;
            subject.HasBusState = true;
            subject.Training.restrictionConfig.version = 99;
            Wear(subject, "bad gear", new SSCRestrictionOverrides { receiveForced = (SSCRestrictionValue)99 });
            Equal(false, SSCRestrictionResolver.IsApplicable(subject));
            foreach (SSCInteractionKind kind in new[] { SSCInteractionKind.Consensual, SSCInteractionKind.Forced,
                SSCInteractionKind.PersonalityExcretion, SSCInteractionKind.DailyTraining, SSCInteractionKind.RitualTraining })
            {
                Decision(other, subject, kind, true, SSCRestrictionReason.NotApplicable);
                Decision(subject, other, kind, true, SSCRestrictionReason.NotApplicable);
            }
            Decision(subject, null, SSCInteractionKind.Masturbation, true, SSCRestrictionReason.NotApplicable);
            Equal(99, subject.Training.restrictionConfig.version);
            Equal(false, SSCRestrictionEditor.TrySet(subject, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow));
            subject.Training.restrictionConfig = null;
            Equal(false, SSCRestrictionEditor.TryInitialize(subject));
            Equal(null, subject.Training.restrictionConfig);
        });
        Run("Mixed pairs consult only the bound side without reading inactive broken configuration", () =>
        {
            Pawn bound = BoundPawn("bound"), inactive = Pawn("inactive", PawnIdentity.Slave);
            bound.Training.restrictionConfig.rules = PermissiveRules();
            inactive.Training.restrictionConfig.version = 99;
            Decision(inactive, bound, SSCInteractionKind.Consensual, true, SSCRestrictionReason.Allowed);
            Decision(bound, inactive, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            bound.Training.restrictionConfig.rules.receiveConsensual = false;
            Equal(bound, Decision(inactive, bound, SSCInteractionKind.Consensual, false,
                SSCRestrictionReason.RuleDenied).Subject);
            bound.Training.restrictionConfig.rules.allowForcedInitiation = false;
            Equal(bound, Decision(bound, inactive, SSCInteractionKind.Forced, false,
                SSCRestrictionReason.RuleDenied).Subject);
        });
        Run("First binding chooses activation-time defaults after an identity-only waiting period", () =>
        {
            Pawn subject = FreshPawn("unbound", PawnIdentity.Slave), owner = FreshPawn("owner", PawnIdentity.Master);
            StartRestrictionGame(subject, owner);
            Equal(null, subject.Training.restrictionConfig);
            subject.Training.selectedTrainer = owner;
            SSCRestrictionGameComponent.Notify(subject);
            Equal(null, subject.Training.restrictionConfig);
            SSCMod.settings.restrictionDefaults.receiveTraining = true;
            subject.BoundMaster = owner;
            SSCRestrictionGameComponent.Notify(subject);
            Equal(true, subject.Training.restrictionConfig.rules.receiveTraining);
            SSCMod.settings.restrictionDefaults.receiveTraining = false;
            SSCRestrictionGameComponent.Notify(subject);
            Equal(true, subject.Training.restrictionConfig.rules.receiveTraining);
        });
        Run("Unbinding pauses rules and preserves choices until rebinding without overwriting from template", () =>
        {
            Pawn subject = FreshBoundPawn("bound"), other = FreshPawn("other");
            StartRestrictionGame(subject, other);
            SSCRestrictionConfig saved = subject.Training.restrictionConfig;
            saved.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow);
            saved.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
            subject.BoundMaster = null;
            SSCRestrictionGameComponent.Notify(subject);
            Equal(false, SSCRestrictionResolver.IsApplicable(subject));
            Equal(saved, subject.Training.restrictionConfig);
            Decision(subject, other, SSCInteractionKind.Consensual, true, SSCRestrictionReason.NotApplicable);
            SSCMod.settings.restrictionDefaults = PermissiveRules();
            var batch = SSCRestrictionLifecycle.ApplyDefaults(new[] { subject }, SSCMod.settings.restrictionDefaults);
            Equal(0, batch.Applied); Equal(1, batch.Skipped); Equal(0, batch.Failed);
            Equal(saved, subject.Training.restrictionConfig);
            subject.BoundMaster = FreshPawn("new owner", PawnIdentity.Master);
            SSCRestrictionGameComponent.Notify(subject);
            Equal(saved, subject.Training.restrictionConfig);
            Equal(SSCRestrictionValue.Allow, saved.rules.ConsensualTarget);
            Decision(subject, other, SSCInteractionKind.Consensual, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Inactive specialization changes defer defaults until binding and preserve disabled target preference", () =>
        {
            Pawn subject = FreshBoundPawn("bus candidate"); StartRestrictionGame(subject);
            SSCRestrictionConfig saved = subject.Training.restrictionConfig;
            saved.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow);
            saved.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
            subject.BoundMaster = null;
            subject.Training.specializationType = SexSlaveSpecializationType.Bus;
            SSCRestrictionGameComponent.Notify(subject);
            Equal(saved, subject.Training.restrictionConfig);
            Equal(false, saved.busDefaultsApplied); Equal(false, saved.rules.receiveForced);
            subject.BoundMaster = FreshPawn("new owner", PawnIdentity.Master);
            SSCRestrictionGameComponent.Notify(subject);
            Equal(false, ReferenceEquals(saved, subject.Training.restrictionConfig));
            Equal(true, subject.Training.restrictionConfig.busDefaultsApplied);
            Equal(true, subject.Training.restrictionConfig.rules.receiveForced);
            Equal(SSCRestrictionValue.Deny, subject.Training.restrictionConfig.rules.consensualInitiation);
            Equal(SSCRestrictionValue.Allow, subject.Training.restrictionConfig.rules.ConsensualTarget);
            Equal(false, saved.busDefaultsApplied); Equal(false, saved.rules.receiveForced);
        });
        Run("Unbound legacy choices survive pending save reload and migrate only after actual binding", () =>
        {
            Pawn old = FreshPawn("old unbound slave", PawnIdentity.Slave);
            old.Training.allowOthersForTrainingOrSex = true;
            LoadOldRestrictions(old);
            SSCRestrictionLegacyPawn pending = old.Training.legacyRestrictionInput;
            Equal(true, pending.open);
            StartRestrictionGame(old);
            Equal(null, old.Training.restrictionConfig);
            Equal(pending, old.Training.legacyRestrictionInput);
            Equal(false, SSCRestrictionEditor.TryInitialize(old));
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; old.Training.ExposeRestrictions();
            Equal(false, Scribe.node.ContainsKey("allowOthersForTrainingOrSex"));
            Pawn restored = FreshPawn("restored unbound", PawnIdentity.Slave);
            Scribe.mode = LoadSaveMode.LoadingVars; restored.Training.ExposeRestrictions();
            Scribe.mode = LoadSaveMode.PostLoadInit; restored.Training.ExposeRestrictions();
            Scribe.mode = LoadSaveMode.Inactive;
            SSCRestrictionGameComponent.Notify(restored);
            Equal(null, restored.Training.restrictionConfig);
            Equal(true, restored.Training.legacyRestrictionInput.open);
            SSCMod.settings.restrictionDefaults = new SSCRestrictionRules();
            restored.BoundMaster = FreshPawn("first owner", PawnIdentity.Master);
            SSCRestrictionGameComponent.Notify(restored);
            Equal(true, restored.Training.restrictionConfig.rules.receiveTraining);
            Equal(true, restored.Training.restrictionConfig.rules.receiveConsensual);
            Equal(null, restored.Training.legacyRestrictionInput);
        });
        Run("Inactive unsupported configuration stays intact and becomes diagnosable when binding returns", () =>
        {
            Pawn subject = FreshPawn("inactive future", PawnIdentity.Slave), other = FreshPawn("other");
            var saved = new SSCRestrictionConfig { version = 99 };
            subject.Training.restrictionConfig = saved;
            subject.HasBusState = true;
            StartRestrictionGame(subject, other);
            Equal(saved, subject.Training.restrictionConfig);
            Decision(other, subject, SSCInteractionKind.Forced, true, SSCRestrictionReason.NotApplicable);
            subject.BoundMaster = FreshPawn("owner", PawnIdentity.Master);
            SSCRestrictionGameComponent.Notify(subject);
            Equal(saved, subject.Training.restrictionConfig);
            Equal(false, saved.busDefaultsApplied);
            Decision(other, subject, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
        });
        Run("Dead or destroyed bound owners are not reassigned while ownership and trainer lock remain", () =>
        {
            foreach (bool destroyed in new[] { false, true })
            {
                Pawn subject = FreshBoundPawn("bound"), owner = subject.BoundMaster;
                subject.Training.selectedTrainer = owner;
                owner.Destroyed = destroyed; owner.Dead = !destroyed;
                StartRestrictionGame(subject, owner);
                Equal(null, subject.Training.selectedTrainer);
                Equal(owner, subject.BoundMaster);
                Equal(owner, SSCRestrictionLifecycle.GetForcedTrainer(subject));
                Equal(true, SSCRestrictionResolver.IsApplicable(subject));
                SSCRestrictionLifecycle.CoordinateTrainer(subject);
                Equal(null, subject.Training.selectedTrainer);
                Decision(FreshPawn("stranger"), subject, SSCInteractionKind.DailyTraining, false,
                    SSCRestrictionReason.RuleDenied);
            }
        });
        Run("Bus configuration builder returns an independent candidate and leaves input intact on success or failure", () =>
        {
            Pawn subject = BoundPawn("bus builder"); subject.HasBusState = true;
            SSCRestrictionConfig saved = subject.Training.restrictionConfig;
            saved.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow);
            saved.rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
            Equal(true, SSCRestrictionConfigurationBuilder.TryApplyBusDefaults(subject, saved, out var candidate, out var error));
            Equal(null, error); Equal(saved, subject.Training.restrictionConfig);
            Equal(false, ReferenceEquals(saved, candidate)); Equal(false, ReferenceEquals(saved.rules, candidate.rules));
            Equal(true, candidate.busDefaultsApplied); Equal(true, candidate.rules.receiveForced);
            Equal(SSCRestrictionValue.Allow, candidate.rules.ConsensualTarget);
            Equal(SSCRestrictionValue.Deny, candidate.rules.consensualInitiation);
            Equal(false, saved.busDefaultsApplied); Equal(false, saved.rules.receiveForced);
            BusProfile().defaults.receiveForced = SSCRestrictionValue.OwnerOnly;
            Equal(false, SSCRestrictionConfigurationBuilder.TryApplyBusDefaults(subject, saved, out candidate, out error));
            Equal(null, candidate); Equal(SSCRestrictionRule.ReceiveForced, error.Rule);
            Equal(false, saved.busDefaultsApplied); Equal(false, saved.rules.receiveConsensual);
            Equal(SSCRestrictionValue.Allow, saved.rules.ConsensualTarget);
        });
    }
}
