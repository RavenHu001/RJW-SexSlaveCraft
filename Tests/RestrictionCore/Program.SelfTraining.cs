using System;
using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunSelfTrainingTests()
    {
        Run("Self-training requires slave identity and chain even when restrictions are disabled", () =>
        {
            Pawn noChain = Pawn("no chain", PawnIdentity.Slave);
            Decision(noChain, null, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.SelfTrainingIneligible);
            Pawn wrongIdentity = BoundPawn("wrong identity");
            wrongIdentity.Training.pawnIdentity = PawnIdentity.Master;
            Decision(wrongIdentity, null, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.SelfTrainingIneligible);
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            Decision(noChain, null, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.SelfTrainingIneligible);
            Decision(BoundPawn("eligible"), null, SSCInteractionKind.SelfTraining, true, SSCRestrictionReason.SystemDisabled);
        });
        Run("Self-training and ordinary masturbation permissions are independent", () =>
        {
            Pawn pawn = BoundPawn("slave");
            Decision(pawn, null, SSCInteractionKind.SelfTraining, true, SSCRestrictionReason.Allowed);
            Decision(pawn, null, SSCInteractionKind.Masturbation, false, SSCRestrictionReason.RuleDenied);
            Equal(true, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.SelfTraining, SSCRestrictionValue.Deny));
            pawn.Training.restrictionConfig.rules.allowMasturbation = true;
            Decision(pawn, pawn, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.RuleDenied);
            Decision(pawn, null, SSCInteractionKind.Masturbation, true, SSCRestrictionReason.Allowed);
            Decision(pawn, Pawn("other"), SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.IncompleteContext);
        });
        Run("Chain without a valid owner can initialize and edit only self-training", () =>
        {
            Pawn pawn = Pawn("unowned", PawnIdentity.Slave);
            pawn.HasChain = true;
            pawn.Training.restrictionConfig = null;
            Equal(false, SSCRestrictionResolver.IsApplicable(pawn));
            Equal(true, SSCRestrictionLifecycle.Refresh(pawn, null, out _));
            Decision(pawn, null, SSCInteractionKind.SelfTraining, true, SSCRestrictionReason.Allowed);
            Equal(true, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.SelfTraining, SSCRestrictionValue.Deny));
            Equal(false, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.Masturbation, SSCRestrictionValue.Allow));
            Decision(pawn, null, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Self-training follows template batch application and equipment override", () =>
        {
            Pawn pawn = BoundPawn("slave");
            SSCMod.settings.restrictionDefaults.allowSelfTraining = false;
            pawn.Training.restrictionConfig = null;
            Equal(true, SSCRestrictionEditor.TryInitialize(pawn));
            Decision(pawn, null, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.RuleDenied);
            var template = new SSCRestrictionRules { allowSelfTraining = true };
            SSCRestrictionBatchResult result = SSCRestrictionLifecycle.ApplyDefaults(new[] { pawn }, template);
            Equal(1, result.Applied);
            Decision(pawn, null, SSCInteractionKind.SelfTraining, true, SSCRestrictionReason.Allowed);
            Wear(pawn, "self-training restraint", new SSCRestrictionOverrides { selfTraining = SSCRestrictionValue.Deny });
            SSCRestrictionDecision denied = Decision(pawn, null, SSCInteractionKind.SelfTraining, false, SSCRestrictionReason.RuleDenied);
            Equal(SSCRestrictionSource.Equipment, denied.Entry.Source);
            Equal(true, pawn.Training.restrictionConfig.rules.allowSelfTraining);
        });
        Run("Missing self-training fields default on, explicit off survives pawn and template reload", () =>
        {
            var config = new SSCRestrictionConfig();
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; Scribe_Deep.Look(ref config, "config");
            var configNode = (Dictionary<string, object>)Scribe.node["config"];
            var rulesNode = (Dictionary<string, object>)configNode["rules"];
            rulesNode.Remove("allowSelfTraining");
            Scribe.mode = LoadSaveMode.LoadingVars; Scribe_Deep.Look(ref config, "config");
            Equal(true, config.rules.allowSelfTraining);
            config.rules.allowSelfTraining = false;
            Scribe.mode = LoadSaveMode.Saving; Scribe_Deep.Look(ref config, "config");
            Scribe.mode = LoadSaveMode.LoadingVars; Scribe_Deep.Look(ref config, "config");
            Equal(false, config.rules.allowSelfTraining);
            Scribe.node = new Dictionary<string, object>();
            SSCSettings settings = new SSCSettings();
            Scribe.mode = LoadSaveMode.Saving; settings.ExposeData();
            var defaults = (Dictionary<string, object>)Scribe.node["restrictionDefaults"];
            defaults.Remove("allowSelfTraining");
            settings = new SSCSettings();
            Scribe.mode = LoadSaveMode.LoadingVars; settings.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; settings.ExposeData();
            Equal(true, settings.restrictionDefaults.allowSelfTraining);
        });
        Run("Stage probability uses current chain stage and the below-first-stage floor", () =>
        {
            Pawn pawn = BoundPawn("slave");
            foreach (var sample in new[] { (0.05f, 0.30f), (0.1f, 0.30f), (0.3f, 0.55f),
                (0.5f, 0.80f), (0.9f, 0.50f) })
            {
                pawn.ChainSeverity = sample.Item1;
                Equal(sample.Item2, SSCSelfTrainingUtility.GetAutoSelectionChance(pawn));
            }
            pawn.ChainSeverity = 0.3f;
            Equal(0.55f, SSCSelfTrainingUtility.GetAutoSelectionChance(pawn));
            pawn.HasChain = false; pawn.BoundMaster = null;
            Equal(0f, SSCSelfTrainingUtility.GetAutoSelectionChance(pawn));
        });
        Run("Snapshot selects valid owner then trainer and preserves raw inputs", () =>
        {
            Pawn pawn = BoundPawn("slave");
            Pawn master = pawn.BoundMaster;
            Pawn trainer = Pawn("trainer", PawnIdentity.Master);
            pawn.Training.selectedTrainer = trainer;
            pawn.needs.Corruption.CurLevel = 0.3f;
            pawn.relations.Opinions[master] = -50;
            pawn.relations.Opinions[trainer] = 80;
            SSCSelfTrainingSnapshot snapshot = SSCSelfTrainingUtility.CaptureAtSceneStart(pawn);
            Equal(master, snapshot.ImaginedPawn); Equal(-50, snapshot.RawOpinionAtStart);
            Equal(true, snapshot.HadImaginedPawnAtStart);
            Equal(master.LabelShortCap, snapshot.ImaginedPawnLabelAtStart);
            Equal(16f, snapshot.Score);
            Equal(true, Math.Abs(0.0285f - snapshot.CorruptionGain) < 0.000001f);
            master.Dead = true;
            Equal(trainer, SSCSelfTrainingUtility.GetImaginedPawn(pawn));
            pawn.BoundMaster = null;
            pawn.relations.Opinions[trainer] = -90;
            Equal(master, snapshot.ImaginedPawn); Equal(-50, snapshot.RawOpinionAtStart);
            pawn.Training.selectedTrainer = null;
            Equal(null, SSCSelfTrainingUtility.GetImaginedPawn(pawn));
            Equal(16f, snapshot.Score);
        });
        Run("Scoring examples, limits and completion claim survive snapshot serialization", () =>
        {
            foreach (var sample in new[] { (0.1f, 17f, 0.0295f), (0.3f, 21f, 0.0335f),
                (0.5f, 25f, 0.0375f), (0.9f, 33f, 0.0455f) })
            {
                float score = SSCSelfTrainingUtility.CalculateScore(sample.Item1, 50);
                Equal(sample.Item2, score);
                Equal(true, Math.Abs(sample.Item3 - SSCSelfTrainingUtility.CalculateCorruptionGain(score)) < 0.000001f);
            }
            Equal(10f, SSCSelfTrainingUtility.CalculateScore(-1f, -100));
            Equal(40f, SSCSelfTrainingUtility.CalculateScore(2f, 200));
            Equal(true, Math.Abs(0.03f - SSCSelfTrainingUtility.CalculateGrantedGain(0.04f, 0.27f, 0.30f)) < 0.000001f);
            Equal(0f, SSCSelfTrainingUtility.CalculateGrantedGain(0.04f, 0.30f, 0.30f));
            Pawn pawn = BoundPawn("slave");
            pawn.needs.Corruption.CurLevel = 0.5f;
            pawn.relations.Opinions[pawn.BoundMaster] = -30;
            SSCSelfTrainingSnapshot snapshot = SSCSelfTrainingUtility.CaptureAtSceneStart(pawn);
            Equal(true, snapshot.TryClaimCompletion());
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; Scribe_Deep.Look(ref snapshot, "snapshot");
            Scribe.mode = LoadSaveMode.LoadingVars; Scribe_Deep.Look(ref snapshot, "snapshot");
            Equal(true, snapshot.Captured); Equal(true, snapshot.CompletionClaimed);
            Equal(pawn.BoundMaster, snapshot.ImaginedPawn);
            Equal(true, snapshot.HadImaginedPawnAtStart);
            Equal(pawn.BoundMaster.LabelShortCap, snapshot.ImaginedPawnLabelAtStart);
            Equal(-30, snapshot.RawOpinionAtStart);
            Equal(false, snapshot.TryClaimCompletion());
        });
        Run("Self-training completion mood covers stage, trait and imagined opinion", () =>
        {
            int[] stageMood = { -4, -2, 0, 2 };
            int[] opinionMood = { 0, -3, 0, 2 };
            for (int stage = 1; stage <= 4; stage++)
                foreach (bool masochist in new[] { false, true })
                    for (int category = 0; category <= 3; category++)
                        Equal(stageMood[stage - 1] + (masochist ? 2 : 0) + opinionMood[category],
                            SSCSelfTrainingFeedbackRules.CompletionMood(stage, masochist, category));
            Equal(1, SSCSelfTrainingFeedbackRules.OpinionCategory(true, -30));
            Equal(2, SSCSelfTrainingFeedbackRules.OpinionCategory(true, -29));
            Equal(2, SSCSelfTrainingFeedbackRules.OpinionCategory(true, 29));
            Equal(3, SSCSelfTrainingFeedbackRules.OpinionCategory(true, 30));
            Equal(0, SSCSelfTrainingFeedbackRules.OpinionCategory(false, -100));
            Equal(SSCSelfTrainingFeedbackRules.CompletionMood(1, false, 1),
                SSCSelfTrainingFeedbackRules.CompletionMood(0, false, 1));
        });
        Run("Self-training permission moods cover both states and eight stage/trait combinations", () =>
        {
            int[,] expected = { { -5, -6, -7, -8 }, { -3, -2, -1, 0 },
                { -2, -1, 0, 1 }, { 0, 1, 2, 3 } };
            foreach (bool trainingAllowed in new[] { false, true })
                foreach (bool masochist in new[] { false, true })
                    for (int stage = 1; stage <= 4; stage++)
                    {
                        int row = (trainingAllowed ? 2 : 0) + (masochist ? 1 : 0);
                        Equal(expected[row, stage - 1], SSCSelfTrainingFeedbackRules.PermissionMood(
                            trainingAllowed, stage, masochist));
                        Equal(stage - 1 + (masochist ? 4 : 0),
                            SSCSelfTrainingFeedbackRules.PermissionStageIndex(stage, masochist));
                    }
        });
        Run("Situational feedback requires an effective SSC denial and valid self-training decision", () =>
        {
            Equal(true, SSCSelfTrainingFeedbackRules.TryPermissionKind(
                SSCRestrictionReason.RuleDenied, SSCRestrictionReason.Allowed, out bool onlyTraining));
            Equal(true, onlyTraining);
            Equal(true, SSCSelfTrainingFeedbackRules.TryPermissionKind(
                SSCRestrictionReason.RuleDenied, SSCRestrictionReason.RuleDenied, out bool bothDenied));
            Equal(false, bothDenied);
            foreach (SSCRestrictionReason reason in new[] { SSCRestrictionReason.Allowed,
                SSCRestrictionReason.NotApplicable, SSCRestrictionReason.SystemDisabled,
                SSCRestrictionReason.ConfigurationInvalid })
                Equal(false, SSCSelfTrainingFeedbackRules.TryPermissionKind(
                    reason, SSCRestrictionReason.RuleDenied, out _));
            Equal(false, SSCSelfTrainingFeedbackRules.TryPermissionKind(
                SSCRestrictionReason.RuleDenied, SSCRestrictionReason.ConfigurationMissing, out _));
        });
    }
}
