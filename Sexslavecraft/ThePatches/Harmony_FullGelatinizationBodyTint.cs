using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

// EN: This patch paints the body of pawns in the `全凝胶化完成` state.
// EN: It only swaps the material when SSC's full-gel tint setting is enabled, and then caches the tinted material for later draws.
// CN: 这个补丁负责给处于“全凝胶化完成”状态的 Pawn 身体上色。
// CN: 它只会在 SSC 的完全胶化染色设置开启时替换材质，并把染色结果缓存下来供后续绘制复用。
namespace SexSlaveCraft
{
    public static class Harmony_FullGelatinizationBodyTint
    {
        private static readonly Color TintColor = new Color(0.72f, 0.38f, 0.95f, 0.82f);
        private static Shader cachedGelShader;
        private static bool loggedShaderChoice;

        public static void Postfix(Pawn pawn, ref Graphic __result)
        {
            TryApplyFullGelTint(pawn, ref __result);
        }

        public static void SizedApparelWorkerPostfix(PawnRenderNode node, ref Graphic __result)
        {
            Pawn pawn = AccessTools.Field(node?.GetType(), "pawn")?.GetValue(node) as Pawn;
            TryApplyFullGelTint(pawn, ref __result);
        }

        public static void SizedApparelApparelPostfix(Apparel apparel, ref ApparelGraphicRecord rec, bool __result)
        {
            if (!__result || apparel?.Wearer == null || rec.graphic == null) return;
            if (!ShouldTintSizedApparelGraphic(apparel.Wearer, rec.graphic)) return;

            Graphic graphic = rec.graphic;
            TryApplyFullGelTint(apparel.Wearer, ref graphic);
            rec = new ApparelGraphicRecord(graphic, rec.sourceApparel);
        }

        private static void TryApplyFullGelTint(Pawn pawn, ref Graphic graphic)
        {
            if (graphic == null || !FullGelatinizationUtility.ShouldTintFullGelGraphic(pawn)) return;

            graphic = graphic.GetColoredVersion(GetFullGelBodyShader(), TintColor, TintColor);
        }

        private static bool ShouldTintSizedApparelGraphic(Pawn pawn, Graphic graphic)
        {
            if (!FullGelatinizationUtility.ShouldTintFullGelGraphic(pawn) || graphic == null) return false;

            return !string.IsNullOrEmpty(graphic.path)
                && graphic.path.IndexOf("SizedApparel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Shader GetFullGelBodyShader()
        {
            if (cachedGelShader != null)
            {
                return cachedGelShader;
            }

            ShaderTypeDef shaderDef = DefDatabase<ShaderTypeDef>.GetNamedSilentFail("SSC_FullGelBody");
            cachedGelShader = shaderDef?.Shader ?? ShaderDatabase.TransparentPostLight;

            if (!loggedShaderChoice)
            {
                loggedShaderChoice = true;
                if (shaderDef?.Shader != null)
                {
                    SSCLog.Important($"[SSC FullGel] Using custom body shader: {shaderDef.defName} ({shaderDef.shaderPath})");
                }
                else
                {
                    SSCLog.WarningImportant("[SSC FullGel] Custom body shader not resolved, falling back to TransparentPostLight.");
                }
            }

            return cachedGelShader;
        }
    }
}
