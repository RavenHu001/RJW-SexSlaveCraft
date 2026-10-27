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
