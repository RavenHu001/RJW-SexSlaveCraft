using SexSlaveCraft;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class Program
{
    private static void RunTrainingIdentityTests()
    {
        // 共享身份门槛在日常入口覆盖全部身份／总开关；其余入口验证接入及关闭保护后仍拒绝非法身份。
        foreach (var (kind, identity, protection) in new[]
        {
            (SSCInteractionKind.DailyTraining, PawnIdentity.Unset, true),
            (SSCInteractionKind.DailyTraining, PawnIdentity.Unset, false),
            (SSCInteractionKind.DailyTraining, PawnIdentity.Master, true),
            (SSCInteractionKind.DailyTraining, PawnIdentity.Master, false),
            (SSCInteractionKind.DailyTraining, PawnIdentity.Slave, true),
            (SSCInteractionKind.DailyTraining, PawnIdentity.Slave, false),
            (SSCInteractionKind.RitualTraining, PawnIdentity.Unset, false),
            (SSCInteractionKind.RitualTraining, PawnIdentity.Slave, false),
            (SSCInteractionKind.BindingPreparation, PawnIdentity.Master, false),
            (SSCInteractionKind.BindingPreparation, PawnIdentity.Slave, false)
        })
            Run($"Training identity admission: {kind}, {identity}, protection={protection}", () =>
            {
                var pair = Pair();
                pair.b.Training.pawnIdentity = identity;
                pair.b.Training.selectedTrainer = pair.a;
                if (kind == SSCInteractionKind.BindingPreparation) pair.b.BoundMaster = null;
                SSCMod.settings.enableSexSlaveProtectionRules = protection;
                var admission = SSCRestrictionTrainingUtility.Evaluate(
                    new SSCRestrictionRequest(pair.a, pair.b, kind, true), true);
                Equal(identity == PawnIdentity.Slave, admission.Allowed);
                Equal(identity == PawnIdentity.Slave ? SSCTrainingFailure.None :
                    SSCTrainingFailure.TargetIdentityRequired, admission.Failure);
                Equal(identity, pair.b.Training.pawnIdentity);
                Equal(pair.a, pair.b.Training.selectedTrainer);
            });

        Run("Legacy bound Unset keeps ordinary protection while training is suspended", () =>
        {
            var pair = Pair(); pair.b.Training.pawnIdentity = PawnIdentity.Unset;
            var oldRules = pair.b.Training.restrictionConfig.rules;
            Equal(true, SSCRestrictionResolver.IsApplicable(pair.b));
            Decision(Pawn("third party"), pair.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            var admission = SSCRestrictionTrainingUtility.Evaluate(
                SSCRestrictionTrainingUtility.CreateRequest(pair.a, pair.b, false), false);
            Equal(SSCTrainingFailure.TargetIdentityRequired, admission.Failure);
            Equal(pair.a, pair.b.BoundMaster);
            Equal(oldRules, pair.b.Training.restrictionConfig.rules);
        });
        Run("Non-training requests do not acquire a training identity gate", () =>
        {
            var pair = Pair(); pair.b.Training.pawnIdentity = PawnIdentity.Unset;
            var admission = SSCRestrictionTrainingUtility.Evaluate(
                new SSCRestrictionRequest(pair.a, pair.b, SSCInteractionKind.Consensual, true), false);
            Equal(true, admission.Allowed);
            Equal(SSCRestrictionReason.BoundOwner, admission.Permission.Reason);
        });
        Run("Malformed training requests fail without a null dereference", () =>
        {
            Equal(false, SSCRestrictionTrainingUtility.Evaluate(null, false).Allowed);
            var pair = Pair(); pair.b.Training = null;
            Equal(SSCTrainingFailure.TargetIdentityRequired, SSCRestrictionTrainingUtility.Evaluate(
                new SSCRestrictionRequest(pair.a, pair.b, SSCInteractionKind.DailyTraining, true), false).Failure);
        });
    }
}
