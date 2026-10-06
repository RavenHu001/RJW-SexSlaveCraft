using System;
using System.Linq;
using RimWorld;
using Verse;

internal static partial class Program
{
    private static void RunVisualCases()
    {
        Run("合法远处目标悬停显示原版圈光标名字提示，UI不放宽同格施放", ValidTargetVisuals);
        Run("非法目标无圈提示且使用禁用光标，UI不进入预热或结算", InvalidTargetVisuals);
        Run("原版适用性入口保留地图及效果资格和拒绝消息，远处不能直接施放", NativeApplicabilityBoundary);
        Run("动物变玩家阵营且无待训项目后禁用，恢复野生资格重新显示反馈", HoverQualificationChanges);
    }

    /// <summary>绘制仅记录原版API调用，不模拟鼠标Targeter、GPU或Effecter调度。</summary>
    private static void ValidTargetVisuals()
    {
        foreach (bool distant in new[] { false, true })
        {
            Reset(); var p = Pair(); p.target.LabelName = "named animal"; if (distant) p.target.Position = new IntVec3(100, 21); p.caster.Map.LineOfSight = false;
            var verb = Verb(p.ability); verb.DrawHighlight(p.target); verb.OnGUI(p.target);
            Check(GenDraw.Highlights.Count == 1 && GenDraw.Highlights[0].Pawn == p.target, "native thing highlight missing/wrong target");
            Check(p.ability.EffectPreviews.Single().Pawn == p.target && GenUI.Attachments.Single() == p.ability.def.uiIcon && Widgets.AttachedLabels.Single().Contains("named animal"), "preview/icon/name hint missing");
            Check(verb.CanHitTarget(p.target) == !distant && p.target.stances.stunner.StunCalls == 0 && p.target.NativeWakeCalls == 0 && p.caster.pather.StartPathCalls == 0, "UI changed cast/path/target"); NoSettlement(p);
        }
    }
    private static void InvalidTargetVisuals()
    {
        for (int scenario = 0; scenario < 5; scenario++)
        {
            Reset(); var p = Pair();
            if (scenario == 0) p.target.Conscious = false;
            if (scenario == 1) p.target.Faction = Faction.OfPlayer;
            if (scenario == 2) p.target.MentalState = new Verse.AI.MentalState();
            if (scenario == 3) p.caster.Reachable = false;
            if (scenario == 4) p.target.NativeTamingAllowed = false;
            var verb = Verb(p.ability); verb.DrawHighlight(p.target); verb.OnGUI(p.target);
            Check(GenDraw.Highlights.Count == 0 && p.ability.EffectPreviews.Count == 0 && Widgets.AttachedLabels.Count == 0 && GenUI.Attachments.Single() == TexCommand.CannotShoot, "invalid UI appears selectable " + scenario);
            Check(p.target.stances.stunner.StunCalls == 0 && p.target.NativeWakeCalls == 0 && !verb.WarmingUp, "UI entered native start"); NoSettlement(p);
        }
    }

    /// <summary>原版高亮通过适用性入口判断，远处资格不放宽实际预热及生效的同格条件。</summary>
    private static void NativeApplicabilityBoundary()
    {
        var p = Pair(); p.target.Position = new IntVec3(250, 140); p.caster.Map.LineOfSight = false;
        Verb_CastAbility nativeEntry = Verb(p.ability);
        Check(nativeEntry.IsApplicableTo(p.target, false) && nativeEntry.ValidateTarget(p.target, false) &&
            !nativeEntry.CanHitTarget(p.target), "native applicability conflated remote selection and execution");
        p.caster.Reachable = false;
        Check(!nativeEntry.IsApplicableTo(p.target, false) && Messages.Requests.Count == 0, "hover accepted unreachable target or emitted message");
        Check(!nativeEntry.IsApplicableTo(p.target, true) && Messages.Requests.Count == 1, "explicit applicability request lost rejection message");
        p.caster.Reachable = true; Effect(p.ability).BaseAllowed = false;
        Check(!nativeEntry.IsApplicableTo(p.target, false), "native applicability bypassed effect restriction");
        Check(!nativeEntry.TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) &&
            nativeEntry.BaseStartCalls == 0 && !nativeEntry.WarmingUp, "native applicability bypassed same-cell cast guard");
        NoSettlement(p);
    }

    /// <summary>同一Verb按当前资格刷新；已驯服且无待训项目不继续显示合法目标反馈。</summary>
    private static void HoverQualificationChanges()
    {
        var p = Pair(); p.target.Position = new IntVec3(220, 120); p.target.LabelName = "changing-animal";
        Verb_CastAbilityTouch nativeEntry = Verb(p.ability);
        nativeEntry.DrawHighlight(p.target); nativeEntry.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 1 && GenUI.Attachments.Single() == nativeEntry.UIIcon && Widgets.AttachedLabels.Count == 1,
            "initial remote selection missing native feedback");
        p.target.Faction = Faction.OfPlayer; nativeEntry.DrawHighlight(p.target); nativeEntry.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 1 && p.ability.EffectPreviews.Count == 1 &&
            GenUI.Attachments.Last() == TexCommand.CannotShoot && Widgets.AttachedLabels.Count == 1 && Messages.Requests.Count == 0,
            "recruited target retained selectable feedback or emitted hover message");
        p.target.Faction = null; nativeEntry.DrawHighlight(p.target); nativeEntry.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 2 && p.ability.EffectPreviews.Count == 2 &&
            GenUI.Attachments.Last() == nativeEntry.UIIcon && Widgets.AttachedLabels.Count == 2,
            "restored target failed to refresh native feedback");
        Check(!nativeEntry.TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) &&
            nativeEntry.BaseStartCalls == 0 && p.target.stances.stunner.StunCalls == 0 && p.target.NativeWakeCalls == 0 && p.caster.pather.StartPathCalls == 0,
            "hover refresh started distant cast or movement");
        NoSettlement(p);
    }
}
