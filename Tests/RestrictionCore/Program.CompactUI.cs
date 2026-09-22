using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>验证紧凑开关的对象偏好能保存、复制并兼容旧档，且不能绕过原六项许可或覆盖规则。</summary>
    private static void RunCompactUiPreferenceTests()
    {
        Run("Consensual toggle preserves each target choice through repeated disable and save reload", () =>
        {
            foreach (var choice in new[] { SSCRestrictionValue.OwnerOnly, SSCRestrictionValue.Allow })
            {
                var rules = new SSCRestrictionRules();
                rules.Set(SSCRestrictionRule.ConsensualInitiation, choice);
                rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
                rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
                Equal(choice, rules.ConsensualTarget);
                var restored = ReloadCompactRules(rules);
                Equal(SSCRestrictionValue.Deny, restored.consensualInitiation);
                Equal(choice, restored.ConsensualTarget);
                restored.Set(SSCRestrictionRule.ConsensualInitiation, restored.ConsensualTarget);
                Equal(choice, restored.consensualInitiation);
            }
        });
        Run("Old three-state saves infer target preference without changing permission", () =>
        {
            foreach (var saved in new[] { SSCRestrictionValue.Deny, SSCRestrictionValue.OwnerOnly, SSCRestrictionValue.Allow })
            {
                Scribe.node = new Dictionary<string, object> { ["consensualInitiation"] = saved };
                var rules = new SSCRestrictionRules();
                Scribe.mode = LoadSaveMode.LoadingVars; rules.ExposeData();
                Equal(saved, rules.consensualInitiation);
                Equal(saved == SSCRestrictionValue.Allow ? saved : SSCRestrictionValue.OwnerOnly, rules.ConsensualTarget);
            }
        });
        Run("Preference cannot permit disabled activity or override equipment and owner semantics", () =>
        {
            var p = Pair(); var rules = p.b.Training.restrictionConfig.rules;
            rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow);
            rules.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
            Equal(SSCRestrictionValue.Allow, rules.ConsensualTarget);
            Decision(p.b, p.a, SSCInteractionKind.Consensual, false, SSCRestrictionReason.RuleDenied);
            Decision(p.a, p.b, SSCInteractionKind.Consensual, true, SSCRestrictionReason.BoundOwner);
            Wear(p.b, "gear", ReadEquipment());
            rules.receiveForced = true;
            Decision(Pawn("other"), p.b, SSCInteractionKind.Forced, false, SSCRestrictionReason.RuleDenied);
            Decision(p.a, p.b, SSCInteractionKind.Forced, true, SSCRestrictionReason.BoundOwner);
        });
        Run("Independent default copies retain their target preference without sharing future changes", () =>
        {
            var original = new SSCRestrictionRules();
            original.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Allow);
            original.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.Deny);
            var copy = original.Copy();
            original.Set(SSCRestrictionRule.ConsensualInitiation, SSCRestrictionValue.OwnerOnly);
            Equal(SSCRestrictionValue.Allow, copy.ConsensualTarget);
            Equal(SSCRestrictionValue.Deny, copy.consensualInitiation);
            var settings = new SSCSettings { restrictionDefaults = copy };
            Scribe.node = new Dictionary<string, object>();
            Scribe.mode = LoadSaveMode.Saving; settings.ExposeData();
            var restored = new SSCSettings();
            Scribe.mode = LoadSaveMode.LoadingVars; restored.ExposeData();
            Equal(SSCRestrictionValue.Allow, restored.restrictionDefaults.ConsensualTarget);
            Equal(SSCRestrictionValue.Deny, restored.restrictionDefaults.consensualInitiation);
        });
        Run("Malformed display preference falls back narrowly without changing the actual rule", () =>
        {
            Scribe.node = new Dictionary<string, object> { ["consensualInitiation"] = SSCRestrictionValue.Deny,
                ["preferredConsensualTarget"] = (SSCRestrictionValue)999 };
            var rules = new SSCRestrictionRules();
            Scribe.mode = LoadSaveMode.LoadingVars; rules.ExposeData();
            Equal(SSCRestrictionValue.OwnerOnly, rules.ConsensualTarget);
            Equal(SSCRestrictionValue.Deny, rules.consensualInitiation);
            Equal(true, rules.IsValid());
            Throws(() => rules.Set(SSCRestrictionRule.ConsensualInitiation, (SSCRestrictionValue)999));
            Equal(SSCRestrictionValue.Deny, rules.consensualInitiation);
            Equal(SSCRestrictionValue.OwnerOnly, rules.ConsensualTarget);
        });
    }

    /// <summary>通过生产序列化入口完成单份规则的字典往返，不模拟实际游戏 Scribe 引用恢复。</summary>
    private static SSCRestrictionRules ReloadCompactRules(SSCRestrictionRules rules)
    {
        Scribe.node = new Dictionary<string, object>();
        Scribe.mode = LoadSaveMode.Saving; rules.ExposeData();
        var result = new SSCRestrictionRules();
        Scribe.mode = LoadSaveMode.LoadingVars; result.ExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        return result;
    }
}
