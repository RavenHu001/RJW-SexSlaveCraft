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
    }

    /// <summary>绘制仅记录原版API调用，不模拟鼠标Targeter、GPU或Effecter调度。</summary>
    private static void ValidTargetVisuals()
    {
        foreach (bool distant in new[] { false, true })
        {
            Reset(); var p = Pair(); p.target.LabelName = "named animal"; if (distant) p.target.Position = new IntVec3(100, 21); p.caster.Map.LineOfSight = false;
            var verb = Verb(p.ability); verb.DrawHighlight(p.target); verb.OnGUI(p.target);
            Check(GenDraw.Highlights.Count == 1 && GenDraw.Highlights[0].layer == AltitudeLayer.MetaOverlays && GenDraw.Highlights[0].position.x == p.target.Position.x + .5f, "target circle missing/wrong location");
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
}
