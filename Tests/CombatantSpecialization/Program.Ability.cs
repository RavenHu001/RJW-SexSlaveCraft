using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunAbilityCases()
    {
        Run("超频定义固定六项倍率、2500 ticks、四小时冷却和自身征召配置", OverdriveDefinitions);
        Run("超频四语名称、效果与征召拒绝文本完整", OverdriveTranslations);
        Run("超频要求终极与征召并保留原版使用限制", OverdriveEligibility);
        Run("排队后解除征召、死亡或失去人格不会激活或扣冷却", OverdriveExecutionGuard);
        Run("普通完成不授予，终极授予仅一份且跨方向征召保持冷却", OverdriveGrantLifecycle);
        Run("删除重复终极标记不移除技能或重置冷却", OverdriveDuplicateFinal);
        Run("移除最后终极标记撤销对应技能，不影响其他技能", OverdriveRemoveFinal);
    }

    private static void TickGrant(Pawn pawn)
    {
        float adjustment = 0;
        foreach (var h in pawn.health.hediffSet.hediffs.ToArray()) h.Grant?.CompPostTick(ref adjustment);
    }

    private static void OverdriveDefinitions()
    {
        var ability = XDocument.Load(Path.Combine(root, "Defs/AbilityDefs/SSC_CombatOverdrive.xml")).Root.Element("AbilityDef");
        Check((string)ability.Element("abilityClass") == typeof(Ability_CombatOverdrive).FullName, "未使用执行保护");
        Check((string)ability.Element("targetRequired") == "false" &&
            (string)ability.Element("showWhenDrafted") == "true" &&
            (string)ability.Element("displayGizmoWhileUndrafted") == "false" &&
            (string)ability.Element("disableGizmoWhileUndrafted") == "true", "征召或自身配置缺失");
        Equal(10000, Value(ability, "cooldownTicksRange"));
        var effect = ability.Element("comps").Elements().Single();
        Check((string)effect.Attribute("Class") == "CompProperties_AbilityGiveHediff" &&
            (string)effect.Element("hediffDef") == "SSC_Hediff_CombatOverdrive" &&
            (string)effect.Element("onlyApplyToSelf") == "true" &&
            (string)effect.Element("replaceExisting") == "true", "效果不是唯一自身临时状态");
        // 与已核对原版 GenTicks.SecondsToTicks 的乘 60 并四舍五入规则一致。
        Check(MathF.Round(Value(effect, "durationSecondsOverride") * 60f) == 2500 &&
            MathF.Round(Value(ability.Element("statBases"), "Ability_Duration") * 60f) == 2500, "时长覆盖或显示不符");
        var defs = XDocument.Load(Path.Combine(root, "Defs/HediffDefs/SSC_HediffDefs_CombatantSpecialization.xml")).Root.Elements();
        var final = defs.Single(e => (string)e.Element("defName") == "SSC_Hediff_Combatant_Final");
        var buff = defs.Single(e => (string)e.Element("defName") == "SSC_Hediff_CombatOverdrive");
        Check((string)final.Element("hediffClass") == "HediffWithComps" && (string)buff.Element("hediffClass") == "HediffWithComps", "组件无法运行");
        var timer = buff.Element("comps").Elements().Single();
        Check((string)timer.Attribute("Class") == "HediffCompProperties_Disappears", "未复用原版计时");
        Equal(2500, Value(timer, "disappearsAfterTicks"));
        var stage = buff.Element("stages").Elements().Single(); var factors = stage.Element("statFactors");
        Check(factors.Elements().Count() == 5, "多余或缺失属性");
        Equal(1.3f, Value(factors, "MeleeDamageFactor")); Equal(.75f, Value(factors, "MeleeCooldownFactor"));
        Equal(.8f, Value(factors, "RangedCooldownFactor")); Equal(.75f, Value(factors, "IncomingDamageFactor"));
        Equal(1.25f, Value(factors, "MoveSpeed")); Equal(.5f, Value(stage, "painFactor"));
    }

    private static void OverdriveTranslations()
    {
        foreach (string lang in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            var keyed = XDocument.Load(Path.Combine(root, "Languages", lang, "Keyed/SSC_CombatOverdrive.xml"));
            Check(!string.IsNullOrWhiteSpace((string)keyed.Root.Element("SSC_CombatOverdriveRequiresDrafted")), "缺少征召原因");
            if (lang == "ChineseSimplified") continue; // 简中使用根 Def 文本。
            foreach (var item in new[] { ("AbilityDef/SSC_CombatOverdrive.xml", "SSC_CombatOverdrive"),
                ("HediffDef/SSC_HediffDefs_CombatantSpecialization.xml", "SSC_Hediff_CombatOverdrive") })
            {
                var text = XDocument.Load(Path.Combine(root, "Languages", lang, "DefInjected", item.Item1));
                foreach (string field in new[] { "label", "description" })
                    Check(!string.IsNullOrWhiteSpace((string)text.Root.Element(item.Item2 + "." + field)), "缺少能力/状态译文");
            }
        }
    }

    private static void OverdriveEligibility()
    {
        var p = Pawn(); var ability = new Ability_CombatOverdrive(p, SSCDefOf.SSC_CombatOverdrive);
        p.Drafted = true; Train(p, 1);
        Check(!ability.CanCast, "普通完成可发动");
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        foreach (PawnIdentity identity in Enum.GetValues<PawnIdentity>())
        {
            p.Training.pawnIdentity = identity;
            Check(ability.CanCast, "终极增加身份门槛");
        }
        p.Drafted = false; Check(!ability.CanCast, "未征召可发动");
        p.Drafted = true; ability.BaseAllowed = false; Check(!ability.CanCast, "绕过原版条件");
        ability.BaseAllowed = true; ability.Cooldown = 99; Check(!ability.CanCast, "绕过原版冷却");
    }

    private static void OverdriveExecutionGuard()
    {
        foreach (int change in new[] { 0, 1, 2, 3 })
        {
            var p = Pawn(); p.Drafted = true;
            var final = p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
            var ability = new Ability_CombatOverdrive(p, SSCDefOf.SSC_CombatOverdrive);
            Check(ability.CanCast, "初始不可施放");
            if (change == 0) p.Drafted = false;
            if (change == 1) p.health.RemoveHediff(final);
            if (change == 2) p.Dead = true;
            if (change == 3) ability.BaseAllowed = false;
            Check(!ability.Activate(default, default) && ability.ActivationCalls == 0 && ability.Cooldown == 0, "失败时仍进入原版激活");
        }
        var ready = Pawn(); ready.Drafted = true; ready.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        var cast = new Ability_CombatOverdrive(ready, SSCDefOf.SSC_CombatOverdrive);
        Check(cast.Activate(default, default) && cast.ActivationCalls == 1 && cast.Cooldown == 10000, "未委托原版激活");
        Check(!cast.Activate(default, default) && cast.ActivationCalls == 1, "冷却中重复激活");
    }

    private static void OverdriveGrantLifecycle()
    {
        var p = Pawn(); Train(p, 1); TickGrant(p);
        Check(p.abilities.GetAbility(SSCDefOf.SSC_CombatOverdrive) == null, "普通授予能力");
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final); TickGrant(p);
        var ability = p.abilities.GetAbility(SSCDefOf.SSC_CombatOverdrive); Check(ability != null, "终极未授予");
        ability.Cooldown = 7654; // 代表原版已恢复或正在运行的冷却，维护不得重建实例。
        foreach (var direction in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.Combatant })
        foreach (bool drafted in new[] { true, false })
        {
            p.Drafted = drafted; p.Training.SetSpecialization(direction);
            for (int i = 0; i < 3; i++) { CompSexSlaveTraining.ReconcileSpecialization(p); TickGrant(p); }
            Check(ReferenceEquals(ability, p.abilities.GetAbility(SSCDefOf.SSC_CombatOverdrive)) && ability.Cooldown == 7654, "维护刷新技能或冷却");
        }
    }

    private static void OverdriveDuplicateFinal()
    {
        var p = Pawn(); p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final); TickGrant(p);
        var ability = p.abilities.GetAbility(SSCDefOf.SSC_CombatOverdrive); ability.Cooldown = 4321;
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        CombatantSpecializationUtility.Sync(p); TickGrant(p);
        Check(ReferenceEquals(ability, p.abilities.GetAbility(SSCDefOf.SSC_CombatOverdrive)) && ability.Cooldown == 4321, "删除重复状态刷新冷却");
    }

    private static void OverdriveRemoveFinal()
    {
        var p = Pawn(); var final = p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final); TickGrant(p);
        var other = new AbilityDef(); p.abilities.GainAbility(other);
        p.health.RemoveHediff(final);
        Check(p.abilities.GetAbility(SSCDefOf.SSC_CombatOverdrive) == null && p.abilities.GetAbility(other) != null, "技能撤销范围不符");
    }
}
