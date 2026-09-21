using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static string root;
    private static int passed, failed;

    /// <summary>以指定仓库目录运行阶段1规则、覆盖及序列化回归用例，汇总结果并用退出码报告失败。</summary>
    private static int Main(string[] args)
    {
        root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        Run("Factory defaults match six-rule contract", () =>
        {
            var rules = new SSCRestrictionRules();
            foreach (SSCRestrictionRule rule in Enum.GetValues<SSCRestrictionRule>())
                Equal(rule == SSCRestrictionRule.ConsensualInitiation ? SSCRestrictionValue.OwnerOnly : SSCRestrictionValue.Deny, rules.Get(rule));
        });
        Run("Unknown/OwnerOnly boolean values cannot be saved", () =>
        {
            var rules = new SSCRestrictionRules();
            Throws(() => rules.Set(SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.OwnerOnly));
            Throws(() => rules.Set((SSCRestrictionRule)777, SSCRestrictionValue.Allow));
        });
        Run("Rule catalog covers each declared rule once and rejects unknown rules", () =>
        {
            Equal(Enum.GetValues<SSCRestrictionRule>().Length, SSCRestrictionRules.All.Count);
            Equal(SSCRestrictionRules.All.Count, SSCRestrictionRules.All.Distinct().Count());
            Equal(true, Enum.GetValues<SSCRestrictionRule>().ToHashSet().SetEquals(SSCRestrictionRules.All));
            foreach (var rule in SSCRestrictionRules.All) Equal(true, SSCRestrictionRules.IsKnown(rule));
            foreach (int value in new[] { -1, 777 })
            {
                Equal(false, SSCRestrictionRules.IsKnown((SSCRestrictionRule)value));
                Equal(false, SSCRestrictionRules.IsValid((SSCRestrictionRule)value, SSCRestrictionValue.Allow));
            }
        });
        Run("Resolver rejects an unknown rule without throwing or applying overrides", () =>
        {
            var pawn = Pawn("subject", PawnIdentity.Slave); pawn.HasBusState = true;
            Wear(pawn, "gear", ReadEquipment());
            var result = SSCRestrictionResolver.Resolve(pawn, pawn.Training.restrictionConfig.rules, (SSCRestrictionRule)777);
            Equal(false, result.Valid); Equal(SSCRestrictionValue.Unspecified, result.Value);
            Equal((SSCRestrictionRule)777, result.Rule); Equal(SSCRestrictionSource.Saved, result.Source);
        });
        foreach (SSCInteractionKind kind in Enum.GetValues<SSCInteractionKind>().Concat(new[] { (SSCInteractionKind)777 }))
            Run("Actual owner immediately allows " + kind, () =>
            {
                var p = Pair();
                p.a.Training.pawnIdentity = PawnIdentity.Slave;
                p.a.Training.restrictionConfig = null;
                p.b.Training.restrictionConfig.version = 99;
                p.b.Training.selectedTrainer = Pawn("someone else");
                p.b.Training.specializationType = SexSlaveSpecializationType.Bus;
                BusProfile().forced.receiveForced = (SSCRestrictionValue)99;
                Wear(p.b, "gear", new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny });
                Decision(p.a, p.b, kind, true, SSCRestrictionReason.BoundOwner);
            });
        Run("Unknown direction cannot claim owner permission", () =>
        {
            var p = Pair();
            var d = SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(p.a, p.b, SSCInteractionKind.Forced, false));
            Equal(false, d.Allowed); Equal(SSCRestrictionReason.IncompleteContext, d.Reason);
        });
        Run("Request constructors require an explicit direction declaration", () =>
        {
            foreach (var constructor in typeof(SSCRestrictionRequest).GetConstructors())
            {
                var direction = constructor.GetParameters().Single(p => p.Name == "directionKnown");
                Equal(typeof(bool), direction.ParameterType); Equal(false, direction.IsOptional);
            }
        });
        Run("Every declared purpose has an explicit tested rule mapping", () =>
        {
            var purposes = new Dictionary<SSCInteractionKind, (SSCRestrictionRule active, SSCRestrictionRule passive)>
            {
                { SSCInteractionKind.Consensual, (SSCRestrictionRule.ConsensualInitiation, SSCRestrictionRule.ReceiveConsensual) },
                { SSCInteractionKind.Forced, (SSCRestrictionRule.ForcedInitiation, SSCRestrictionRule.ReceiveForced) },
                { SSCInteractionKind.PersonalityExcretion, (SSCRestrictionRule.ConsensualInitiation, SSCRestrictionRule.ReceiveConsensual) }
            };
            var independent = new[] { SSCInteractionKind.Unknown, SSCInteractionKind.Masturbation,
                SSCInteractionKind.DailyTraining, SSCInteractionKind.RitualTraining, SSCInteractionKind.BindingPreparation };
            Equal(true, Enum.GetValues<SSCInteractionKind>().ToHashSet().SetEquals(purposes.Keys.Concat(independent)));
            foreach (var purpose in purposes)
            {
                Pawn actor = Pawn("actor", PawnIdentity.Slave), target = Pawn("target", PawnIdentity.Slave);
                actor.Training.restrictionConfig.rules = PermissiveRules();
                target.Training.restrictionConfig.rules = PermissiveRules();
                actor.Training.restrictionConfig.rules.Set(purpose.Value.active, SSCRestrictionValue.Deny);
                var active = Decision(actor, target, purpose.Key, false, SSCRestrictionReason.RuleDenied);
                Equal(actor, active.Subject); Equal(purpose.Value.active, active.Entry.Rule);
                actor.Training.restrictionConfig.rules.Set(purpose.Value.active, SSCRestrictionValue.Allow);
                target.Training.restrictionConfig.rules.Set(purpose.Value.passive, SSCRestrictionValue.Deny);
                var passive = Decision(actor, target, purpose.Key, false, SSCRestrictionReason.RuleDenied);
                Equal(target, passive.Subject); Equal(purpose.Value.passive, passive.Entry.Rule);
            }
            Decision(Pawn("A"), Pawn("B", PawnIdentity.Slave), (SSCInteractionKind)777, false, SSCRestrictionReason.IncompleteContext);
        });
        Run("Master identity and trainer assignment are not ownership", () =>
        {
            var p = Pair(); var c = Pawn("C", PawnIdentity.Master);
            p.b.Training.selectedTrainer = c;
            Decision(c, p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Reverse direction does not inherit owner direct allow", () =>
        {
            var p = Pair();
            Decision(p.b, p.a, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Decision(p.b, p.a, SSCInteractionKind.Consensual, true, SSCRestrictionReason.Allowed);
        });
        Run("OwnerOnly rejects third parties", () =>
        {
            var p = Pair();
            Decision(p.b, Pawn("C"), SSCInteractionKind.Consensual, false, SSCRestrictionReason.OwnerOnly);
        });
        foreach (SSCRestrictionValue active in new[] { SSCRestrictionValue.Deny, SSCRestrictionValue.OwnerOnly, SSCRestrictionValue.Allow })
        foreach (bool passive in new[] { false, true })
            Run("Both sides checked: " + active + "/" + passive, () =>
            {
                Pawn a = Pawn("A", PawnIdentity.Slave), b = Pawn("B", PawnIdentity.Slave);
                a.Training.restrictionConfig.rules.consensualInitiation = active;
                b.Training.restrictionConfig.rules.receiveConsensual = passive;
                Equal(active == SSCRestrictionValue.Allow && passive,
                    SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(a, b, SSCInteractionKind.Consensual, true)).Allowed);
            });
        Run("Forced permission checks both active and passive", () =>
        {
            Pawn a = Pawn("A", PawnIdentity.Slave), b = Pawn("B", PawnIdentity.Slave);
            a.Training.restrictionConfig.rules.allowForcedInitiation = true;
            Decision(a, b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            b.Training.restrictionConfig.rules.receiveForced = true;
            Decision(a, b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            a.Training.restrictionConfig.rules.allowForcedInitiation = false;
            Decision(a, b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Solo permission is independent of consensual initiation", () =>
        {
            var p = Pair();
            p.b.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Allow;
            Decision(p.b, null, SSCInteractionKind.Masturbation, false, SSCRestrictionReason.RuleDenied);
            p.b.Training.restrictionConfig.rules.allowMasturbation = true;
            p.b.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            Decision(p.b, null, SSCInteractionKind.Masturbation, true, SSCRestrictionReason.Allowed);
            Decision(p.b, p.b, SSCInteractionKind.Masturbation, true, SSCRestrictionReason.Allowed);
        });
        Run("Missing dual target is not solo or a null bypass", () =>
        {
            var p = Pair();
            p.b.Training.restrictionConfig.rules.allowMasturbation = true;
            Decision(p.b, null, SSCInteractionKind.Consensual, false, SSCRestrictionReason.IncompleteContext);
            Decision(null, p.b, SSCInteractionKind.Consensual, false, SSCRestrictionReason.IncompleteContext);
            Decision(p.b, Pawn("C"), SSCInteractionKind.Masturbation, false, SSCRestrictionReason.IncompleteContext);
        });
        Run("Unknown purposes reject only relevant requests", () =>
        {
            Decision(Pawn("A"), Pawn("B", PawnIdentity.Slave), SSCInteractionKind.Unknown, false, SSCRestrictionReason.IncompleteContext);
            Decision(Pawn("A"), Pawn("B"), SSCInteractionKind.Unknown, true, SSCRestrictionReason.NotApplicable);
        });
        Run("Merely having a comp or old option does not activate rules", () =>
        {
            var a = Pawn("A"); a.Training.allowOthersForTrainingOrSex = true;
            a.Training.specializationType = SexSlaveSpecializationType.Bus;
            Equal(false, SSCRestrictionResolver.IsApplicable(a));
            Decision(a, Pawn("B"), SSCInteractionKind.Forced, true, SSCRestrictionReason.NotApplicable);
        });
        Run("Legacy bound pawn is applicable without a selected identity", () =>
        {
            var p = Pair(); p.b.Training.pawnIdentity = PawnIdentity.Unset;
            Equal(true, SSCRestrictionResolver.IsApplicable(p.b));
            Decision(Pawn("C"), p.b, SSCInteractionKind.Consensual, false, SSCRestrictionReason.RuleDenied);
        });
        foreach (SSCInteractionKind kind in new[] { SSCInteractionKind.DailyTraining, SSCInteractionKind.RitualTraining })
            Run("Training ignores ordinary permissions: " + kind, () =>
            {
                Pawn a = Pawn("A", PawnIdentity.Slave), b = Pawn("B", PawnIdentity.Slave);
                a.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
                b.Training.restrictionConfig.rules.receiveTraining = true;
                Decision(a, b, kind, true, SSCRestrictionReason.Allowed);
                b.Training.restrictionConfig.rules.receiveTraining = false;
                b.Training.restrictionConfig.rules.receiveConsensual = true;
                Decision(a, b, kind, false, SSCRestrictionReason.RuleDenied);
            });
        Run("Personality task uses ordinary rules, not training", () =>
        {
            var p = Pair(); var c = Pawn("C");
            p.b.Training.restrictionConfig.rules.receiveTraining = true;
            Decision(c, p.b, SSCInteractionKind.PersonalityExcretion, false, SSCRestrictionReason.RuleDenied);
            p.b.Training.restrictionConfig.rules.receiveConsensual = true;
            p.b.Training.restrictionConfig.rules.receiveTraining = false;
            Decision(c, p.b, SSCInteractionKind.PersonalityExcretion, true, SSCRestrictionReason.Allowed);
        });
        Run("First binding preparation requires selected legal candidate", () =>
        {
            var p = Pair(); p.b.BoundMaster = null; p.b.Training.selectedTrainer = p.a;
            p.b.Training.restrictionConfig = null;
            Decision(p.a, p.b, SSCInteractionKind.BindingPreparation, true, SSCRestrictionReason.Allowed);
            Decision(Pawn("C", PawnIdentity.Master), p.b, SSCInteractionKind.BindingPreparation, false, SSCRestrictionReason.InvalidBindingPreparation);
            p.a.Training.pawnIdentity = PawnIdentity.Slave;
            Decision(p.a, p.b, SSCInteractionKind.BindingPreparation, false, SSCRestrictionReason.InvalidBindingPreparation);
        });
        Run("First-binding purpose cannot bypass an existing owner", () =>
        {
            var p = Pair(); var c = Pawn("C", PawnIdentity.Master); p.b.Training.selectedTrainer = c;
            Decision(c, p.b, SSCInteractionKind.BindingPreparation, false, SSCRestrictionReason.InvalidBindingPreparation);
        });
        Run("Bus direction applies before health state appears; only two passive entries", () =>
        {
            var p = Pair(); p.b.Training.specializationType = SexSlaveSpecializationType.Bus;
            var c = Pawn("C");
            Decision(c, p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            Decision(c, p.b, SSCInteractionKind.Consensual, true, SSCRestrictionReason.Allowed);
            Decision(c, p.b, SSCInteractionKind.DailyTraining, false, SSCRestrictionReason.RuleDenied);
            Decision(p.b, c, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Decision(p.b, c, SSCInteractionKind.Consensual, false, SSCRestrictionReason.OwnerOnly);
        });
        Run("Retained bus state applies after changing direction", () =>
        {
            var p = Pair(); p.b.Training.specializationType = SexSlaveSpecializationType.Cow; p.b.HasBusState = true;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            p.b.HasBusState = false;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Disabling specialization restores saved values without rewriting them", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Equal(false, p.b.Training.restrictionConfig.rules.receiveForced);
        });
        Run("Equipment overrides bus; unrelated entries are unchanged", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            Wear(p.b, "Apparel_EFOutfit", ReadEquipment());
            var d = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Equal(SSCRestrictionSource.Equipment, d.Entry.Source); Equal("Apparel_EFOutfit", d.Entry.SourceDef);
            Decision(Pawn("C"), p.b, SSCInteractionKind.Consensual, true, SSCRestrictionReason.Allowed);
            Equal(false, p.b.Training.restrictionConfig.rules.receiveForced);
            p.b.apparel.WornApparel.Clear();
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
        });
        Run("Specialization switch does not disable equipment", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig.rules.receiveForced = true;
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            Wear(p.b, "gear", ReadEquipment());
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            p.b.apparel.WornApparel.Clear();
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
        });
        Run("Global off permits without reading missing configs or equipment", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig = null;
            Wear(p.b, "gear", ReadEquipment());
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.SystemDisabled);
            Equal(null, p.b.Training.restrictionConfig);
        });
        Run("Equipment is an override source, not a fixed post-check", () =>
        {
            var p = Pair();
            Wear(p.b, "test allowing gear", new SSCRestrictionOverrides { receiveTraining = SSCRestrictionValue.Allow });
            var d = Decision(Pawn("C"), p.b, SSCInteractionKind.DailyTraining, true, SSCRestrictionReason.Allowed);
            Equal(SSCRestrictionSource.Equipment, d.Entry.Source);
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Active-only allowance retains the resolved source", () =>
        {
            Pawn actor = Pawn("A", PawnIdentity.Slave);
            Wear(actor, "active_gear", new SSCRestrictionOverrides { consensualInitiation = SSCRestrictionValue.Allow });
            var d = Decision(actor, Pawn("B"), SSCInteractionKind.Consensual, true, SSCRestrictionReason.Allowed);
            Equal(actor, d.Subject); Equal(SSCRestrictionSource.Equipment, d.Entry.Source);
            Equal("active_gear", d.Entry.SourceDef);
        });
        Run("Same-layer conflicts are order-independent and stricter wins", () =>
        {
            var p = Pair();
            Wear(p.b, "Z_allow", new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Allow });
            Wear(p.b, "B_deny", ReadEquipment()); Wear(p.b, "A_deny", ReadEquipment());
            foreach (int _ in new[] { 0, 1 })
            {
                var d = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
                Equal("A_deny", d.Entry.SourceDef); p.b.apparel.WornApparel.Reverse();
            }
        });
        Run("Undeclared overrides leave saved values intact", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig.rules.allowMasturbation = true;
            Wear(p.b, "gear", ReadEquipment()); p.b.HasBusState = true;
            var d = Decision(p.b, null, SSCInteractionKind.Masturbation, true, SSCRestrictionReason.Allowed);
            Equal(SSCRestrictionSource.Saved, d.Entry.Source);
        });
        Run("Bad override reports definition error and a specific denied decision", () =>
        {
            var invalid = new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.OwnerOnly };
            Equal(1, invalid.ConfigErrors().Count());
            var p = Pair(); Wear(p.b, "bad_gear", invalid);
            var d = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
            Equal("bad_gear", d.Entry.SourceDef);
        });
        Run("Invalid specialization retains its failing source despite valid equipment", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            BusProfile().forced.receiveForced = SSCRestrictionValue.OwnerOnly;
            Wear(p.b, "valid_gear", new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Allow });
            var decision = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
            Equal(SSCRestrictionSource.Specialization, decision.Entry.Source);
            Equal("SSC_Restriction_Bus", decision.Entry.SourceDef);
            Equal(SSCRestrictionRule.ReceiveForced, decision.Entry.Rule);
            Equal(SSCRestrictionValue.OwnerOnly, decision.Entry.Value);
        });
        Run("Multiple matching specializations resolve strictness and ties independently of order", () =>
        {
            var p = Pair(); p.b.HasBusState = true; p.b.Training.specializationType = SexSlaveSpecializationType.Cow;
            AddProfile("Z_cow_deny", SexSlaveSpecializationType.Cow, new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny });
            AddProfile("A_cow_deny", SexSlaveSpecializationType.Cow, new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny });
            for (int pass = 0; pass < 2; pass++)
            {
                var denied = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
                Equal("A_cow_deny", denied.Entry.SourceDef);
                var allowed = Decision(Pawn("C"), p.b, SSCInteractionKind.Consensual, true, SSCRestrictionReason.Allowed);
                Equal("SSC_Restriction_Bus", allowed.Entry.SourceDef);
                DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Reverse();
            }
            p.b.HasBusState = false;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Consensual, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Specialization errors choose the first invalid definition by name in either order", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            AddProfile("Z_bad", SexSlaveSpecializationType.Bus, new SSCRestrictionOverrides { receiveForced = (SSCRestrictionValue)99 });
            AddProfile("A_bad", SexSlaveSpecializationType.Bus, new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.OwnerOnly });
            AddProfile("0_valid_deny", SexSlaveSpecializationType.Bus, new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny });
            for (int pass = 0; pass < 2; pass++)
            {
                var d = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
                Equal("A_bad", d.Entry.SourceDef); Equal(SSCRestrictionValue.OwnerOnly, d.Entry.Value);
                DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Reverse();
            }
        });
        Run("Equipment errors retain deterministic invalid values despite valid stricter entries", () =>
        {
            var p = Pair();
            Wear(p.b, "Z_bad", new SSCRestrictionOverrides { receiveForced = (SSCRestrictionValue)99 });
            Wear(p.b, "A_bad", new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.OwnerOnly });
            Wear(p.b, "0_valid_deny", new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny });
            for (int pass = 0; pass < 2; pass++)
            {
                var d = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
                Equal(SSCRestrictionSource.Equipment, d.Entry.Source);
                Equal("A_bad", d.Entry.SourceDef); Equal(SSCRestrictionValue.OwnerOnly, d.Entry.Value);
                p.b.apparel.WornApparel.Reverse();
            }
        });
        Run("Ordinary equipment and sparse unrelated overrides leave provenance intact", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            p.b.apparel.WornApparel.Add(new Apparel { def = new ThingDef { defName = "ordinary" } });
            Wear(p.b, "unrelated", new SSCRestrictionOverrides { receiveTraining = SSCRestrictionValue.Allow });
            var d = Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            Equal(SSCRestrictionSource.Specialization, d.Entry.Source); Equal("SSC_Restriction_Bus", d.Entry.SourceDef);
        });
        Run("Defaults factory clones template and applies bus defaults with force off", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            var template = SSCMod.settings.restrictionDefaults;
            var saved = p.b.Training.restrictionConfig;
            Equal(true, SSCRestrictionResolver.TryCreateInitialConfiguration(p.b, template, out var made, out var error));
            Equal(null, error);
            Equal(true, made.rules.receiveForced); Equal(true, made.rules.receiveConsensual);
            Equal(true, made.busDefaultsApplied); Equal(false, template.receiveForced);
            Equal(saved, p.b.Training.restrictionConfig);
            made.rules.allowMasturbation = true; Equal(false, template.allowMasturbation);
        });
        Run("Existing configurations never reapply specialization defaults on query", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Consensual, false, SSCRestrictionReason.RuleDenied);
            Equal(false, p.b.Training.restrictionConfig.busDefaultsApplied);
        });
        Run("Invalid template fails with its original value and does not write a config", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig = null;
            var template = new SSCRestrictionRules { consensualInitiation = (SSCRestrictionValue)88 };
            Equal(false, SSCRestrictionResolver.TryCreateInitialConfiguration(p.b, template, out var made, out var error));
            Equal(null, made); Equal(null, p.b.Training.restrictionConfig);
            Equal(SSCRestrictionSource.DefaultTemplate, error.Source); Equal(SSCRestrictionRule.ConsensualInitiation, error.Rule);
            Equal((SSCRestrictionValue)88, error.Value); Equal((SSCRestrictionValue)88, template.consensualInitiation);
        });
        Run("Invalid specialization defaults fail without committing earlier rules", () =>
        {
            var p = Pair(); p.b.HasBusState = true; p.b.Training.restrictionConfig = null;
            BusProfile().defaults.masturbation = SSCRestrictionValue.Allow;
            BusProfile().defaults.receiveForced = SSCRestrictionValue.OwnerOnly;
            var template = new SSCRestrictionRules();
            Equal(false, SSCRestrictionResolver.TryCreateInitialConfiguration(p.b, template, out var made, out var error));
            Equal(null, made); Equal(null, p.b.Training.restrictionConfig); Equal(false, template.allowMasturbation);
            Equal(SSCRestrictionSource.SpecializationDefault, error.Source); Equal("SSC_Restriction_Bus", error.SourceDef);
            Equal(SSCRestrictionRule.ReceiveForced, error.Rule); Equal(SSCRestrictionValue.OwnerOnly, error.Value);
        });
        Run("Initialization merges all matching default profiles with sparse deterministic precedence", () =>
        {
            var p = Pair(); p.b.HasBusState = true; p.b.Training.specializationType = SexSlaveSpecializationType.Cow;
            AddProfile("Z_cow", SexSlaveSpecializationType.Cow, null, new SSCRestrictionOverrides
                { receiveForced = SSCRestrictionValue.Deny, masturbation = SSCRestrictionValue.Allow });
            AddProfile("A_cow", SexSlaveSpecializationType.Cow, null, new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny });
            for (int pass = 0; pass < 2; pass++)
            {
                Equal(true, SSCRestrictionResolver.TryCreateInitialConfiguration(p.b, null, out var made, out var error));
                Equal(null, error); Equal(true, made.busDefaultsApplied);
                Equal(false, made.rules.receiveForced); Equal(true, made.rules.receiveConsensual); Equal(true, made.rules.allowMasturbation);
                Equal(SSCRestrictionValue.OwnerOnly, made.rules.consensualInitiation);
                DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Reverse();
            }
        });
        Run("Template edits do not change existing configuration", () =>
        {
            var p = Pair();
            Equal(true, SSCRestrictionResolver.TryCreateInitialConfiguration(p.b, SSCMod.settings.restrictionDefaults, out var made, out var error));
            Equal(null, error);
            p.b.Training.restrictionConfig = made;
            SSCMod.settings.restrictionDefaults.receiveForced = true;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Copy owns separate rules and metadata", () =>
        {
            var source = new SSCRestrictionConfig { busDefaultsApplied = true };
            var copy = source.Copy(); copy.rules.receiveTraining = true; copy.busDefaultsApplied = false;
            Equal(false, source.rules.receiveTraining); Equal(true, source.busDefaultsApplied);
        });
        Run("Query never initializes missing configuration", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig = null;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationMissing);
            Equal(null, p.b.Training.restrictionConfig);
        });
        Run("Read-only game preview uses same policy and temporary defaults", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig = null; p.b.HasBusState = true;
            var request = new SSCRestrictionRequest(Pawn("C"), p.b, SSCInteractionKind.Forced, true) { PreviewDefaults = true };
            Equal(true, SSCRestrictionPolicy.Evaluate(request).Allowed);
            Wear(p.b, "gear", ReadEquipment());
            Equal(false, SSCRestrictionPolicy.Evaluate(request).Allowed);
            Equal(null, p.b.Training.restrictionConfig);
        });
        Run("Unsupported config version is not silently reset", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig.version = 42;
            Decision(Pawn("C"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
            Equal(42, p.b.Training.restrictionConfig.version);
        });
        Run("Corrupt stored scope is rejected without rewriting it", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig.rules.consensualInitiation = (SSCRestrictionValue)88;
            Decision(p.b, Pawn("C"), SSCInteractionKind.Consensual, false, SSCRestrictionReason.ConfigurationInvalid);
            Equal((SSCRestrictionValue)88, p.b.Training.restrictionConfig.rules.consensualInitiation);
        });
        Run("An invalid saved entry makes all consulted uses reject consistently", () =>
        {
            var subject = Pawn("corrupt", PawnIdentity.Slave);
            subject.Training.restrictionConfig.rules = PermissiveRules();
            subject.Training.restrictionConfig.rules.consensualInitiation = (SSCRestrictionValue)88;
            Equal(false, subject.Training.restrictionConfig.IsValid());
            Equal(false, SSCRestrictionEditor.IsValid(subject.Training.restrictionConfig));
            foreach (var kind in new[] { SSCInteractionKind.Consensual, SSCInteractionKind.Forced, SSCInteractionKind.PersonalityExcretion,
                SSCInteractionKind.DailyTraining, SSCInteractionKind.RitualTraining })
                Decision(Pawn("other"), subject, kind, false, SSCRestrictionReason.ConfigurationInvalid);
            Decision(subject, null, SSCInteractionKind.Masturbation, false, SSCRestrictionReason.ConfigurationInvalid);
            Decision(subject, Pawn("other"), SSCInteractionKind.Forced, false, SSCRestrictionReason.ConfigurationInvalid);
            Equal((SSCRestrictionValue)88, subject.Training.restrictionConfig.rules.consensualInitiation);
        });
        Run("Owner and system disable precede invalid whole-configuration checks", () =>
        {
            var p = Pair(); p.b.Training.restrictionConfig.rules.consensualInitiation = (SSCRestrictionValue)88;
            Decision(p.a, p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.BoundOwner);
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            Decision(Pawn("other"), p.b, SSCInteractionKind.DailyTraining, true, SSCRestrictionReason.SystemDisabled);
            Decision(p.a, p.b, SSCInteractionKind.Unknown, true, SSCRestrictionReason.BoundOwner);
        });
        Run("Resolver rejects corrupt saved rules before valid specialization or equipment", () =>
        {
            var p = Pair(); p.b.HasBusState = true;
            p.b.Training.restrictionConfig.rules.consensualInitiation = (SSCRestrictionValue)88;
            Wear(p.b, "allow", new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Allow });
            var result = SSCRestrictionResolver.Resolve(p.b, p.b.Training.restrictionConfig.rules, SSCRestrictionRule.ReceiveForced);
            Equal(false, result.Valid); Equal(SSCRestrictionSource.Saved, result.Source);
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            result = SSCRestrictionResolver.Resolve(p.b, p.b.Training.restrictionConfig.rules, SSCRestrictionRule.ReceiveForced);
            Equal(true, result.Valid); Equal(SSCRestrictionSource.SystemDisabled, result.Source);
        });
        Run("Independent training does not consult the initiator's unrelated broken configuration", () =>
        {
            Pawn actor = Pawn("actor", PawnIdentity.Slave), target = Pawn("target", PawnIdentity.Slave);
            actor.Training.restrictionConfig.rules.consensualInitiation = (SSCRestrictionValue)88;
            target.Training.restrictionConfig.rules.receiveTraining = true;
            Decision(actor, target, SSCInteractionKind.DailyTraining, true, SSCRestrictionReason.Allowed);
            Decision(actor, target, SSCInteractionKind.RitualTraining, true, SSCRestrictionReason.Allowed);
        });
        Run("Pawn config serialization round trip preserves explicit values", () =>
        {
            var comp = new CompSexSlaveTraining { restrictionConfig = new SSCRestrictionConfig { busDefaultsApplied = true } };
            comp.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            comp.restrictionConfig.rules.receiveForced = true;
            Scribe.mode = LoadSaveMode.Saving; comp.ExposeRestrictions();
            var loaded = new CompSexSlaveTraining(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeRestrictions();
            Equal(true, loaded.restrictionConfig.busDefaultsApplied);
            Equal(SSCRestrictionValue.Deny, loaded.restrictionConfig.rules.consensualInitiation);
            Equal(true, loaded.restrictionConfig.rules.receiveForced);
            Equal(false, loaded.restrictionConfig.rules.receiveTraining);
            Equal(1, loaded.restrictionConfig.version);
            loaded.restrictionConfig.rules.receiveForced = false;
            Equal(true, comp.restrictionConfig.rules.receiveForced);
        });
        Run("Old save without new field stays uninitialized", () =>
        {
            var comp = new CompSexSlaveTraining();
            Scribe.mode = LoadSaveMode.LoadingVars; comp.ExposeRestrictions();
            Equal(null, comp.restrictionConfig);
        });
        Run("Old settings acquire defaults but never overwrite explicit new values", () =>
        {
            var settings = new SSCSettings();
            Scribe.mode = LoadSaveMode.LoadingVars; settings.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; settings.ExposeData();
            Equal(true, settings.enableSpecializationRestrictionOverrides);
            Equal(SSCRestrictionValue.OwnerOnly, settings.restrictionDefaults.consensualInitiation);
            settings.enableSpecializationRestrictionOverrides = false;
            settings.restrictionDefaults.receiveTraining = true;
            Scribe.mode = LoadSaveMode.Saving; settings.ExposeData();
            var loaded = new SSCSettings(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData();
            Equal(false, loaded.enableSpecializationRestrictionOverrides); Equal(true, loaded.restrictionDefaults.receiveTraining);
        });
        Run("XML definitions are valid and source/runtime copies match", () =>
        {
            Equal(true, DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Count > 0);
            Equal("SSC_Restriction_Bus", BusProfile().defName);
            foreach (var profile in DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading)
                Equal(0, profile.ConfigErrors().Count());
            Equal(0, new SSCRestrictionEquipmentExtension { forced = ReadEquipment() }.ConfigErrors().Count());
            foreach (string path in ProfileXmlFiles().Select(path => Path.GetRelativePath(root, path))
                .Concat(new[] { "Defs/ThingsDefs/SexSlave_EvilFallOutfit.xml" }))
                Equal(File.ReadAllText(Path.Combine(root, path)), File.ReadAllText(Path.Combine(root, "Sexslavecraft", path)));
        });
        Run("Explicit editor initialization owns defaults and never replaces an existing config", () =>
        {
            var pawn = Pawn("editable", PawnIdentity.Slave);
            pawn.Training.restrictionConfig = null;
            pawn.Training.selectedTrainer = Pawn("trainer");
            pawn.Training.allowOthersForTrainingOrSex = true;
            SSCMod.settings.enableSexSlaveProtectionRules = false;
            SSCMod.settings.restrictionDefaults.receiveTraining = true;
            Equal(true, SSCRestrictionEditor.TryInitialize(pawn));
            Equal(true, pawn.Training.restrictionConfig.rules.receiveTraining);
            Equal(true, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Deny));
            Equal(true, SSCMod.settings.restrictionDefaults.receiveTraining);
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn));
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
            Equal(true, pawn.Training.allowOthersForTrainingOrSex);
            Equal("trainer", pawn.Training.selectedTrainer.LabelShort);
        });
        Run("Editor cannot create or edit rules for an inapplicable pawn", () =>
        {
            var pawn = Pawn("unmanaged");
            Equal(false, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow));
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
            pawn.Training.restrictionConfig = null;
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn));
            Equal(null, pawn.Training.restrictionConfig);
            pawn.BoundMaster = Pawn("owner");
            Equal(true, SSCRestrictionEditor.TryInitialize(pawn));
        });
        Run("Editor initialization reports bad global defaults without throwing or saving", () =>
        {
            var pawn = Pawn("editable", PawnIdentity.Slave); pawn.Training.restrictionConfig = null;
            SSCMod.settings.restrictionDefaults.consensualInitiation = (SSCRestrictionValue)88;
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn, out var error));
            Equal(null, pawn.Training.restrictionConfig); Equal(SSCRestrictionSource.DefaultTemplate, error.Source);
            Equal(SSCRestrictionRule.ConsensualInitiation, error.Rule); Equal((SSCRestrictionValue)88, error.Value);
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn));
        });
        Run("Editor and read-only preview report bad profile defaults without changing the pawn", () =>
        {
            var pawn = Pawn("editable", PawnIdentity.Slave); pawn.Training.restrictionConfig = null; pawn.HasBusState = true;
            BusProfile().defaults.receiveForced = SSCRestrictionValue.OwnerOnly;
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn, out var error));
            Equal("SSC_Restriction_Bus", error.SourceDef); Equal(null, pawn.Training.restrictionConfig);
            var request = new SSCRestrictionRequest(Pawn("other"), pawn, SSCInteractionKind.Forced, true) { PreviewDefaults = true };
            var decision = SSCRestrictionPolicy.Evaluate(request);
            Equal(false, decision.Allowed); Equal(SSCRestrictionReason.ConfigurationInvalid, decision.Reason);
            Equal("SSC_Restriction_Bus", decision.Entry.SourceDef); Equal(SSCRestrictionValue.OwnerOnly, decision.Entry.Value);
            Equal(null, pawn.Training.restrictionConfig);
        });
        Run("Editor preserves unsupported or corrupt configurations", () =>
        {
            var pawn = Pawn("invalid", PawnIdentity.Slave);
            pawn.Training.restrictionConfig.version = 99;
            Equal(false, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow));
            Equal(false, SSCRestrictionEditor.TryInitialize(pawn));
            Equal(99, pawn.Training.restrictionConfig.version);
            pawn.Training.restrictionConfig.version = SSCRestrictionConfig.CurrentVersion;
            pawn.Training.restrictionConfig.rules.consensualInitiation = (SSCRestrictionValue)88;
            Equal(false, SSCRestrictionEditor.IsValid(pawn.Training.restrictionConfig));
            Equal(false, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow));
            Equal((SSCRestrictionValue)88, pawn.Training.restrictionConfig.rules.consensualInitiation);
        });
        Run("Editor preserves saved choices beneath equipment and specialization overrides", () =>
        {
            var p = Pair(); p.b.HasBusState = true; p.b.Training.restrictionConfig = null;
            Wear(p.b, "gear", ReadEquipment());
            Equal(true, SSCRestrictionEditor.TryInitialize(p.b));
            Equal(true, p.b.Training.restrictionConfig.rules.receiveForced);
            var other = Pawn("other");
            Decision(other, p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Equal(true, SSCRestrictionEditor.TrySet(p.b, SSCRestrictionRule.ReceiveForced, SSCRestrictionValue.Deny));
            p.b.apparel.WornApparel.Clear();
            Decision(other, p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.Allowed);
            SSCMod.settings.enableSpecializationRestrictionOverrides = false;
            Decision(other, p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Decision(p.a, p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.BoundOwner);
        });
        Run("Editor rejects invalid choices without initializing or changing stored values", () =>
        {
            var pawn = Pawn("editable", PawnIdentity.Slave);
            Equal(false, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.OwnerOnly));
            Equal(false, SSCRestrictionEditor.TrySet(pawn, (SSCRestrictionRule)999, SSCRestrictionValue.Allow));
            Equal(false, pawn.Training.restrictionConfig.rules.receiveTraining);
            pawn.Training.restrictionConfig = null;
            Equal(false, SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow));
            Equal(null, pawn.Training.restrictionConfig);
        });
        Run("Editor changes survive save reload and drive the same permission entry", () =>
        {
            var p = Pair(); var other = Pawn("other");
            Decision(other, p.b, SSCInteractionKind.DailyTraining, false, SSCRestrictionReason.RuleDenied);
            Equal(true, SSCRestrictionEditor.TrySet(p.b, SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow));
            Scribe.mode = LoadSaveMode.Saving; p.b.Training.ExposeRestrictions();
            p.b.Training = new CompSexSlaveTraining { pawnIdentity = PawnIdentity.Slave };
            Scribe.mode = LoadSaveMode.LoadingVars; p.b.Training.ExposeRestrictions();
            Decision(other, p.b, SSCInteractionKind.DailyTraining, true, SSCRestrictionReason.Allowed);
            Decision(other, p.b, SSCInteractionKind.Consensual, false, SSCRestrictionReason.RuleDenied);
        });
        Run("Test interface translations cover controls and enum results in both shipped languages", () =>
        {
            var keys = new HashSet<string>();
            foreach (string source in new[] { "common/Restrictions/Dialog_SSCRestrictions.cs", "common/Settings.cs" })
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(Path.Combine(root, "Sexslavecraft", source)), "\"(SSC_Restrictions_[A-Za-z]+)\""))
                    keys.Add(match.Groups[1].Value);
            foreach (SSCRestrictionRule rule in Enum.GetValues<SSCRestrictionRule>()) keys.Add("SSC_Restrictions_Rule_" + rule);
            foreach (SSCRestrictionReason reason in Enum.GetValues<SSCRestrictionReason>()) keys.Add("SSC_Restrictions_Reason_" + reason);
            foreach (SSCRestrictionSource source in Enum.GetValues<SSCRestrictionSource>()) keys.Add("SSC_Restrictions_Source_" + source);
            foreach (SSCInteractionKind kind in Enum.GetValues<SSCInteractionKind>().Where(k => k != SSCInteractionKind.Unknown)) keys.Add("SSC_Restrictions_Kind_" + kind);
            foreach (SSCRestrictionValue value in Enum.GetValues<SSCRestrictionValue>().Where(v => v != SSCRestrictionValue.Unspecified)) keys.Add("SSC_Restrictions_Value_" + value);
            foreach (string language in new[] { "English", "ChineseSimplified" })
            {
                string path = Path.Combine("Languages", language, "Keyed/SSC_Restrictions.xml");
                var translations = XDocument.Load(Path.Combine(root, path)).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                foreach (string key in keys) Equal(true, translations.ContainsKey(key) && !string.IsNullOrWhiteSpace(translations[key]));
                Equal(File.ReadAllText(Path.Combine(root, path)), File.ReadAllText(Path.Combine(root, "Sexslavecraft", path)));
            }
        });
        Console.WriteLine($"{passed}/{passed + failed} passed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>创建指定身份的测试角色，并为其分配独立的新规则配置，避免用例对象共用保存值。</summary>
    private static Pawn Pawn(string name, PawnIdentity identity = PawnIdentity.Unset)
    {
        return new Pawn { LabelShort = name, Training = new CompSexSlaveTraining { pawnIdentity = identity, restrictionConfig = new SSCRestrictionConfig() } };
    }
    /// <summary>创建主人 a 与已绑定目标 b，作为方向、主人许可和反向请求测试的共同起点。</summary>
    private static (Pawn a, Pawn b) Pair()
    {
        var a = Pawn("owner", PawnIdentity.Master); var b = Pawn("bound", PawnIdentity.Slave); b.BoundMaster = a; return (a, b);
    }
    /// <summary>调用生产代码的统一入口，断言许可及原因，再返回结果供用例检查条目来源等细节。</summary>
    private static SSCRestrictionDecision Decision(Pawn a, Pawn b, SSCInteractionKind kind, bool allowed, SSCRestrictionReason reason)
    {
        var d = SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(a, b, kind, true));
        Equal(allowed, d.Allowed); Equal(reason, d.Reason); return d;
    }
    /// <summary>为测试角色添加带指定强制条目的装备，用于验证覆盖优先级与同层冲突。</summary>
    private static void Wear(Pawn pawn, string name, SSCRestrictionOverrides values)
    {
        var def = new ThingDef { defName = name }; def.modExtensions.Add(new SSCRestrictionEquipmentExtension { forced = values });
        pawn.apparel.WornApparel.Add(new Apparel { def = def });
    }
    /// <summary>构造全部开放的保存值，供测试通过单项拒绝区分实际检查的条目。</summary>
    private static SSCRestrictionRules PermissiveRules()
    {
        return new SSCRestrictionRules
        {
            allowMasturbation = true, consensualInitiation = SSCRestrictionValue.Allow, allowForcedInitiation = true,
            receiveConsensual = true, receiveForced = true, receiveTraining = true
        };
    }
    /// <summary>向当前用例加入独立特化定义，验证同层冲突及多个方向同时适用时的稀疏合成。</summary>
    private static void AddProfile(string name, SexSlaveSpecializationType specialization, SSCRestrictionOverrides forced,
        SSCRestrictionOverrides defaults = null)
    {
        DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Add(new SSCRestrictionProfileDef
            { defName = name, specialization = specialization, forced = forced, defaults = defaults });
    }
    /// <summary>读取实际装备 XML 中的新系统扩展，确保测试使用随模组发布的强制条目。</summary>
    private static SSCRestrictionOverrides ReadEquipment()
    {
        return ReadOverrides(XDocument.Load(Path.Combine(root, "Defs/ThingsDefs/SexSlave_EvilFallOutfit.xml"))
            .Descendants("li").Single(e => (string)e.Attribute("Class") == "SexSlaveCraft.SSCRestrictionEquipmentExtension").Element("forced"));
    }
    /// <summary>将 XML 节点的已声明字段映射为稀疏覆盖；缺失节点返回 null，未声明字段保持 Unspecified。</summary>
    private static SSCRestrictionOverrides ReadOverrides(XElement node)
    {
        if (node == null) return null;
        var result = new SSCRestrictionOverrides();
        foreach (XElement child in node.Elements())
            typeof(SSCRestrictionOverrides).GetField(child.Name.LocalName).SetValue(result, Enum.Parse<SSCRestrictionValue>(child.Value));
        return result;
    }
    /// <summary>枚举实际 Defs 目录中的所有特化规则文件，支持多个文件和一个文件内的多份定义。</summary>
    private static IEnumerable<string> ProfileXmlFiles()
    {
        return Directory.EnumerateFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .Where(path => XDocument.Load(path).Root.Elements("SexSlaveCraft.SSCRestrictionProfileDef").Any())
            .OrderBy(path => path, StringComparer.Ordinal);
    }
    /// <summary>从所有实际 XML 读取特化定义，不依赖定义数量、文件名或定义顺序。</summary>
    private static List<SSCRestrictionProfileDef> ReadProfiles()
    {
        return ProfileXmlFiles().SelectMany(path => XDocument.Load(path).Root.Elements("SexSlaveCraft.SSCRestrictionProfileDef"))
            .Select(node => new SSCRestrictionProfileDef
            {
                defName = (string)node.Element("defName"),
                specialization = Enum.Parse<SexSlaveSpecializationType>((string)node.Element("specialization")),
                defaults = ReadOverrides(node.Element("defaults")), forced = ReadOverrides(node.Element("forced"))
            }).ToList();
    }
    /// <summary>按稳定定义名定位实际公交车规则，其他特化定义的加入不会改变测试目标。</summary>
    private static SSCRestrictionProfileDef BusProfile()
    {
        return DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Single(p => p.defName == "SSC_Restriction_Bus");
    }
    /// <summary>重置全局设置、序列化环境及全部实际特化定义后执行用例；初始化异常也计入失败汇总。</summary>
    private static void Run(string name, Action test)
    {
        try
        {
            SSCMod.settings = new SSCSettings(); Scribe.mode = LoadSaveMode.Inactive; Scribe.node = new Dictionary<string, object>();
            DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading = ReadProfiles();
            test(); passed++; Console.WriteLine("PASS " + name);
        }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + "\n" + error); }
    }
    /// <summary>使用类型默认比较器断言相等，失败时报告预期值与实际值。</summary>
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
    /// <summary>断言非法规则写入抛出 ArgumentOutOfRangeException；无异常或其他异常均使测试失败。</summary>
    private static void Throws(Action action)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Expected rejected invalid value");
    }
}
