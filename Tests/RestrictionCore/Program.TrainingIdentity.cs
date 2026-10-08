using SexSlaveCraft;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class Program
{
    private static void RunTrainingIdentityTests()
    {
        foreach (SSCInteractionKind kind in new[] { SSCInteractionKind.DailyTraining,
            SSCInteractionKind.RitualTraining, SSCInteractionKind.BindingPreparation })
        foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master, PawnIdentity.Slave })
        foreach (bool protection in new[] { true, false })
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
        Run("Training identity and explicit recovery translations ship in all four languages with matching mirrors", () =>
        {
            string[] keys = { "SSC_Training_TargetIdentityRequired", "SSC_Identity_RitualRoleChanged",
                "SSC_Identity_LegacyRecoveryHint", "SSC_Identity_LegacyRecoveryButton", "SSC_Identity_LegacyRecoveryConfirm",
                "SSC_Identity_LegacyRecoveryRejected", "SSC_Identity_LegacyRecoveryCompleted" };
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                string relative = Path.Combine("Languages", language, "Keyed", "SSC_TrainingIdentity.xml");
                string contents = File.ReadAllText(Path.Combine(root, relative));
                var values = XDocument.Parse(contents).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                Equal(true, keys.ToHashSet().SetEquals(values.Keys));
                foreach (string key in keys) Equal(false, string.IsNullOrWhiteSpace(values[key]));
                Equal(true, values["SSC_Identity_LegacyRecoveryConfirm"].Contains("{0}"));
                Equal(true, values["SSC_Identity_LegacyRecoveryConfirm"].Contains("{1}"));
                Equal(true, values["SSC_Identity_LegacyRecoveryCompleted"].Contains("{0}"));
                Equal(contents, File.ReadAllText(Path.Combine(root, "Sexslavecraft", relative)));
            }
        });
    }
}
