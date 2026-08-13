using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>
    /// Manual Harmony postfix/prefix methods for Binding Ritual UI and quality fixes.
    /// The ritual instance's def is a Festival precept, so SSC identifies it by behavior.def.defName.
    /// </summary>
    public static class RitualFixPatches
    {
        private const int CustomPenaltyTicks = 180000;
        private const int CustomPenaltyDays = 3;
        private const float CustomPenaltyFactor = -0.2f;
        private const float TicksPerDay = 60000f;

        private static readonly FieldInfo DialogRitualField = AccessTools.Field(typeof(Dialog_BeginRitual), "ritual");
        private static readonly FieldInfo CommandRitualField = AccessTools.Field(typeof(Command_Ritual), "ritual");
        private static readonly FieldInfo VanillaRepeatPenaltyDurationDaysField = AccessTools.Field(typeof(Precept_Ritual), "RepeatPenaltyDurationDays");
        private static readonly IntVec2 PenaltyIconSize = new IntVec2(16, 16);
        private static readonly Texture2D CooldownBarTex = SolidColorMaterials.NewSolidColorTexture(new Color32(170, 150, 0, 60));
        private static Texture2D penaltyArrowTex;

        private static bool IsOurRitual(Precept_Ritual ritual)
        {
            if (ritual == null) return false;
            return ritual.behavior?.def?.defName == "SSC_BindingRitualBehavior";
        }

        private static Texture2D PenaltyArrowTex
        {
            get
            {
                if (penaltyArrowTex == null)
                {
                    penaltyArrowTex = ContentFinder<Texture2D>.Get("UI/Icons/Rituals/QualityPenalty");
                }

                return penaltyArrowTex;
            }
        }

        private static int VanillaRepeatPenaltyDays
        {
            get
            {
                try
                {
                    object value = VanillaRepeatPenaltyDurationDaysField?.GetRawConstantValue();
                    if (value is int days && days > 0) return days;
                }
                catch
                {
                    // Fall back below. Older RimWorld builds used a different constant, but this path is display-only.
                }

                return 20;
            }
        }

        private static int CustomTicksLeft(Precept_Ritual ritual)
        {
            return Mathf.Max(0, CustomPenaltyTicks - ritual.TicksSinceLastPerformed);
        }

        private static float RoundedTenths(float value)
        {
            return (float)Mathf.RoundToInt(Mathf.Max(0f, value) * 10f) / 10f;
        }

        private static string RepeatPenaltyWarning(int totalDays, float passedDays, string penalty, float remainingDays)
        {
            return "RitualRepeatPenaltyTip".Translate(totalDays, passedDays, penalty, remainingDays)
                .Resolve()
                .Colorize(ColoredText.ThreatColor);
        }

        public static void RepeatPenaltyActive_Postfix(Precept_Ritual __instance, ref bool __result)
        {
            if (IsOurRitual(__instance))
            {
                __result = __instance.isAnytime
                    && __instance.lastFinishedTick != -1
                    && __instance.def.useRepeatPenalty
                    && __instance.TicksSinceLastPerformed < CustomPenaltyTicks;
            }
        }

        public static void RepeatPenaltyProgress_Postfix(Precept_Ritual __instance, ref float __result)
        {
            if (IsOurRitual(__instance))
            {
                __result = (float)__instance.TicksSinceLastPerformed / CustomPenaltyTicks;
            }
        }

        public static void RepeatQualityPenalty_Postfix(Precept_Ritual __instance, ref float __result)
        {
            if (IsOurRitual(__instance))
            {
                __result = __instance.TicksSinceLastPerformed < CustomPenaltyTicks
                    ? CustomPenaltyFactor
                    : 0f;
            }
        }

        public static void RepeatPenaltyTimeLeft_Postfix(Precept_Ritual __instance, ref string __result)
        {
            if (IsOurRitual(__instance))
            {
                __result = CustomTicksLeft(__instance).ToStringTicksToPeriod();
            }
        }

        public static void TipMainPart_Postfix(Precept_Ritual __instance, ref string __result)
        {
            try
            {
                if (!IsOurRitual(__instance) || !__instance.RepeatPenaltyActive || __result.NullOrEmpty()) return;

                int vanillaDays = VanillaRepeatPenaltyDays;
                string penalty = __instance.RepeatQualityPenalty.ToStringPercent();
                string oldWarning = RepeatPenaltyWarning(
                    vanillaDays,
                    RoundedTenths(__instance.RepeatPenaltyProgress * vanillaDays),
                    penalty,
                    RoundedTenths((1f - __instance.RepeatPenaltyProgress) * vanillaDays));
                string newWarning = RepeatPenaltyWarning(
                    CustomPenaltyDays,
                    RoundedTenths(__instance.TicksSinceLastPerformed / TicksPerDay),
                    penalty,
                    RoundedTenths(CustomTicksLeft(__instance) / TicksPerDay));

                if (__result.Contains(oldWarning))
                {
                    __result = __result.Replace(oldWarning, newWarning);
                    return;
                }

                string doubleNewLine = Environment.NewLine + Environment.NewLine;
                int separator = __result.IndexOf(doubleNewLine, StringComparison.Ordinal);
                int separatorLength = doubleNewLine.Length;
                if (separator < 0)
                {
                    separator = __result.IndexOf("\n\n", StringComparison.Ordinal);
                    separatorLength = 2;
                }

                if (separator >= 0 && __result.StartsWith("<color=", StringComparison.Ordinal))
                {
                    __result = newWarning + __result.Substring(separator, separatorLength)
                        + __result.Substring(separator + separatorLength);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC RitualFix] TipMainPart_Postfix exception: {ex}");
            }
        }

        public static bool CommandRitualDrawIcon_Prefix(Command_Ritual __instance, Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            try
            {
                var ritual = CommandRitualField?.GetValue(__instance) as Precept_Ritual;
                if (!IsOurRitual(ritual)) return true;

                DrawBaseCommandIcon(__instance, rect, buttonMat, parms);

                if (ritual.RepeatPenaltyActive)
                {
                    float fill = Mathf.InverseLerp(CustomPenaltyTicks, 0f, ritual.TicksSinceLastPerformed);
                    Widgets.FillableBar(rect.ContractedBy(1f), Mathf.Clamp01(fill), CooldownBarTex, null, false);

                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    float daysRemaining = CustomTicksLeft(ritual) / TicksPerDay;
                    float labelDays = daysRemaining >= 1f
                        ? Mathf.RoundToInt(daysRemaining)
                        : Mathf.Floor(daysRemaining * 10f) / 10f;
                    Widgets.Label(rect, "PeriodDays".Translate(labelDays));
                    Text.Anchor = TextAnchor.UpperLeft;

                    GUI.DrawTexture(
                        new Rect(rect.xMax - PenaltyIconSize.x, rect.yMin + 4f, PenaltyIconSize.x, PenaltyIconSize.z),
                        PenaltyArrowTex);
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC RitualFix] CommandRitualDrawIcon_Prefix exception: {ex}");
                return true;
            }
        }

        private static void DrawBaseCommandIcon(Command command, Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            Texture texture = command.icon ?? BaseContent.BadTex;
            rect.position += new Vector2(command.iconOffset.x * rect.size.x, command.iconOffset.y * rect.size.y);

            if (!command.Disabled || parms.lowLight)
            {
                GUI.color = command.IconDrawColor;
            }
            else
            {
                GUI.color = command.IconDrawColor.SaturationChanged(0f);
            }

            if (parms.lowLight)
            {
                GUI.color = GUI.color.ToTransparent(0.6f);
            }

            Widgets.DrawTextureFitted(
                rect,
                texture,
                command.iconDrawScale * 0.85f,
                command.iconProportions,
                command.iconTexCoords,
                command.iconAngle,
                command.overrideMaterial ?? buttonMat);
            GUI.color = Color.white;
        }

        public static void PopulateQualityFactors_Postfix(object __instance, ref List<QualityFactor> __result)
        {
            try
            {
                var ritual = DialogRitualField?.GetValue(__instance) as Precept_Ritual;
                if (!IsOurRitual(ritual)) return;

                var expectationsOffset = RitualOutcomeEffectWorker_FromQuality.GetExpectationsOffset(
                    Find.CurrentMap, ritual?.def);

                if (expectationsOffset != null)
                {
                    float expectedQuality = expectationsOffset.Item2;
                    for (int i = __result.Count - 1; i >= 0; i--)
                    {
                        if (Math.Abs(__result[i].quality - expectedQuality) < 0.001f
                            && __result[i].positive
                            && __result[i].noMiddleColumnInfo)
                        {
                            __result.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC RitualFix] PopulateQualityFactors_Postfix exception: {ex}");
            }
        }

        public static void PredictedQuality_Postfix(object __instance, ref FloatRange __result)
        {
            try
            {
                var ritual = DialogRitualField?.GetValue(__instance) as Precept_Ritual;
                if (!IsOurRitual(ritual)) return;

                var expectationsOffset = RitualOutcomeEffectWorker_FromQuality.GetExpectationsOffset(
                    Find.CurrentMap, ritual?.def);

                if (expectationsOffset != null)
                {
                    float offset = expectationsOffset.Item2;
                    __result = new FloatRange(
                        Mathf.Clamp(__result.min - offset, 0f, 1f),
                        Mathf.Clamp(__result.max - offset, 0f, 1f));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC RitualFix] PredictedQuality_Postfix exception: {ex}");
            }
        }

        public static void ExpectedDurationLabel_Postfix(object __instance, ref TaggedString __result)
        {
            try
            {
                var ritual = DialogRitualField?.GetValue(__instance) as Precept_Ritual;
                if (!IsOurRitual(ritual)) return;

                __result = "{0}: {1}".Formatted(
                    "ExpectedLordJobDuration".Translate(),
                    "SSC_RitualExpectedDuration".Translate());
            }
            catch (Exception ex)
            {
                Log.Error($"[SSC RitualFix] ExpectedDurationLabel_Postfix exception: {ex}");
            }
        }
    }
}
