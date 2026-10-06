using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunVisualCases()
    {
        Run("远近地图合法目标高亮及原版预览，非法目标无圈且保持同格执行", MapSelectionHighlight);
        Run("合法地图目标显示技能光标和带目标名提示，非法目标禁用光标且无标签", MapSelectionCursorAndHint);
        Run("原版适用性入口遵守地图资格与消息选项，远处适用不等于同格可施放", NativeApplicabilityBoundary);
        Run("悬停期间目标失去及恢复意识时，原版圈光标提示按当前资格刷新", HoverQualificationChanges);
    }

    /// <summary>原版Touch绘制动态调用真实适用性入口；只记录圈及预览API，不模拟原版Thing绘制。</summary>
    private static void MapSelectionHighlight()
    {
        foreach (var position in new[] { new IntVec3(0, 0), new IntVec3(1, 0), new IntVec3(200, 100) })
        {
            Reset(); var p = Pair(); p.target.Position = position; p.caster.Map.LineOfSight = false;
            var before = p.caster.Training.ExportSpecializationProgress(); var verb = Verb(p.ability);
            verb.DrawHighlight(p.target);
            Check(GenDraw.Highlights.Count == 1 && GenDraw.Highlights.Single().Pawn == p.target,
                "selectable map target has no native thing highlight");
            Check(p.ability.EffectPreviews.Count == 1 && p.ability.EffectPreviews.Single().Pawn == p.target, "valid native effect preview missing");
            Check(verb.CanHitTarget(p.target) == (position == p.caster.Position) && !verb.WarmingUp && verb.BaseStartCalls == 0 &&
                p.target.stances.stunner.StunCalls == 0 && Messages.Requests.Count == 0, "UI broadened execution or began interaction");
            Check(before.OrderBy(e => e.Key).SequenceEqual(p.caster.Training.ExportSpecializationProgress().OrderBy(e => e.Key)), "highlight changed training history");
            NoSettlement(p);
        }
        for (int scenario = 0; scenario < 12; scenario++)
        {
            Reset(); var p = Pair(); Pawn target = p.target;
            if (scenario == 0) target = null;
            if (scenario == 1) target = p.caster;
            if (scenario == 2) target.Conscious = false;
            if (scenario == 3) target.Faction = new Faction();
            if (scenario == 4) p.caster.Reachable = false;
            if (scenario == 5) target.RaceProps.Humanlike = false;
            if (scenario == 6) target.Map = new Map();
            if (scenario == 7) target.needs = null;
            if (scenario == 8) Effect(p.ability).BaseAllowed = false;
            if (scenario == 9) Effect(p.ability).Props.durationTicks = 0;
            if (scenario == 10) p.caster.health.RemoveHediff(p.caster.health.hediffSet.GetFirstHediffOfDef(CatFinal));
            if (scenario == 11) target.Spawned = false;
            Verb(p.ability).DrawHighlight(target);
            Check(GenDraw.Highlights.Count == 0 && p.ability.EffectPreviews.Count == 0 && Messages.Requests.Count == 0, "invalid hover drew or emitted rejection message " + scenario);
            NoSettlement(p);
        }
    }

    /// <summary>真实OnGUI通过原版附加标签分发到真实Comp；翻译替身只保留目标名字参数。</summary>
    private static void MapSelectionCursorAndHint()
    {
        var p = Pair(); var verb = Verb(p.ability); p.target.LabelName = "target-name"; p.target.Position = new IntVec3(170, 80);
        for (int refresh = 0; refresh < 3; refresh++) verb.OnGUI(p.target);
        Check(GenUI.Attachments.Count == 3 && GenUI.Attachments.All(texture => ReferenceEquals(texture, verb.UIIcon)), "remote valid target uses rejection cursor");
        string expected = "SSC_PetCatComfortTargetHint(target-name)";
        Check(Widgets.AttachedLabels.Count == 3 && Widgets.AttachedLabels.All(label => label == expected) &&
            Effect(p.ability).ExtraLabelMouseAttachment(p.target) == expected, "native label dispatcher lost hint or target name");
        Check(!verb.CanHitTarget(p.target) && !verb.TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) &&
            !verb.WarmingUp && p.target.stances.stunner.StunCalls == 0 && Messages.Requests.Count == 0, "valid distant UI bypassed approach requirement"); NoSettlement(p);

        for (int scenario = 0; scenario < 6; scenario++)
        {
            Reset(); p = Pair(); Pawn target = p.target;
            if (scenario == 0) target = null;
            if (scenario == 1) target.Conscious = false;
            if (scenario == 2) target.Faction = new Faction();
            if (scenario == 3) p.caster.Reachable = false;
            if (scenario == 4) Effect(p.ability).BaseAllowed = false;
            if (scenario == 5) Effect(p.ability).Props.encouragementHediff = null;
            Verb(p.ability).OnGUI(target);
            Check(GenUI.Attachments.Count == 1 && ReferenceEquals(GenUI.Attachments.Single(), TexCommand.CannotShoot) &&
                Widgets.AttachedLabels.Count == 0 && Effect(p.ability).ExtraLabelMouseAttachment(target) == null && Messages.Requests.Count == 0,
                "invalid hover cursor/hint mismatch " + scenario);
            Check(!Verb(p.ability).WarmingUp && Verb(p.ability).BaseStartCalls == 0, "invalid hover started cast"); NoSettlement(p);
        }
    }

    /// <summary>原版Touch通过适用性判断高亮；必须同时保留组件限制和调用者的消息开关。</summary>
    private static void NativeApplicabilityBoundary()
    {
        var p = Pair(); p.target.Position = new IntVec3(250, 140); p.caster.Map.LineOfSight = false;
        Verb_CastAbility nativeEntry = Verb(p.ability);
        Check(nativeEntry.IsApplicableTo(p.target, false) && nativeEntry.ValidateTarget(p.target, false) &&
            !nativeEntry.CanHitTarget(p.target), "native applicability conflated remote selection and execution");
        p.caster.Reachable = false;
        Check(!nativeEntry.IsApplicableTo(p.target, false) && Messages.Requests.Count == 0, "hover emitted rejection or accepted unreachable target");
        Check(!nativeEntry.IsApplicableTo(p.target, true) && Messages.Requests.Count == 1, "explicit applicability request lost rejection message");
        p.caster.Reachable = true; Effect(p.ability).BaseAllowed = false;
        Check(!nativeEntry.IsApplicableTo(p.target, false), "native applicability bypassed effect component restriction");
        Check(!nativeEntry.TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) &&
            nativeEntry.BaseStartCalls == 0 && !nativeEntry.WarmingUp, "native applicability bypassed same-cell cast guard");
        NoSettlement(p);
    }

    /// <summary>在同一Verb上刷新真实资格，不将开始悬停时的结果缓存到原版UI。</summary>
    private static void HoverQualificationChanges()
    {
        var p = Pair(); p.target.Position = new IntVec3(220, 120); p.target.LabelName = "changing-target";
        Verb_CastAbilityTouch nativeEntry = Verb(p.ability);
        nativeEntry.DrawHighlight(p.target); nativeEntry.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 1 && GenUI.Attachments.Single() == nativeEntry.UIIcon && Widgets.AttachedLabels.Count == 1,
            "initial remote selection missing native feedback");
        p.target.Conscious = false; nativeEntry.DrawHighlight(p.target); nativeEntry.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 1 && p.ability.EffectPreviews.Count == 1 &&
            GenUI.Attachments.Last() == TexCommand.CannotShoot && Widgets.AttachedLabels.Count == 1 && Messages.Requests.Count == 0,
            "invalidated target retained selectable feedback or emitted hover message");
        p.target.Conscious = true; nativeEntry.DrawHighlight(p.target); nativeEntry.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 2 && p.ability.EffectPreviews.Count == 2 &&
            GenUI.Attachments.Last() == nativeEntry.UIIcon && Widgets.AttachedLabels.Count == 2,
            "restored target failed to refresh native feedback");
        Check(!nativeEntry.TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) &&
            nativeEntry.BaseStartCalls == 0 && p.target.stances.stunner.StunCalls == 0 && p.caster.pather.StartPathCalls == 0,
            "hover refresh started distant cast or movement");
        NoSettlement(p);
    }
}
