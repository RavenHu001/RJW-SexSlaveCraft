using System;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

// EN: This patch paints the body of pawns in the `全凝胶化完成` state.
// EN: It replaces the returned graphic only when SSC's full-gel tint setting is enabled; RimWorld owns graphic/material reuse.
// CN: 这个补丁负责给处于“全凝胶化完成”状态的 Pawn 身体上色。
// CN: 它只会在 SSC 的完全胶化染色设置开启时替换返回图形，图形与材质的复用由游戏自身负责。
namespace SexSlaveCraft
{
    public static class Harmony_FullGelatinizationBodyTint
    {
        // 颜色和透明度分开调节：暗部柔化由原生 Shader 完成，不依赖降低 Alpha 隐藏黑线。
        // 原深紫/.85已获实机确认；本批仅把默认S设为90，颜色来源统一交给个体外观工具。
        internal const string NativeShaderDefName = "SSC_FullGelBody17";
        // 三处渲染共用光照后队列；只在 ShouldTintFullGelGraphic 已通过的分支中使用。
        internal const int PostLightRenderQueue = 3150;
        // 头部使用同一配置来源，后续调色无需维护第二份常量。
        internal static Color FullGelColor => FullGelAppearanceUtility.Default.Color;
        private static bool loggedShaderChoice;
        private static bool loggedShaderFallback;

        /// <summary>身体渲染节点的后缀，只替换本次返回的身体图形，头部仍走自身绘制路径。</summary>
        public static void Postfix(Pawn pawn, ref Graphic __result)
        {
            TryApplyFullGelTint(pawn, ref __result);
        }

        private static Type sizedApparelWorkerType;

        /// <summary>SA GetGraphic 会在并行预绘制调用；只在最终材质入口追加染色，不再挂 GetGraphic。</summary>
        public static void RegisterSizedApparel(Harmony harmony)
        {
            sizedApparelWorkerType = AccessTools.TypeByName("SizedApparel.SizedApparelRenderNodeWorker");
            if (sizedApparelWorkerType == null) return;
            var original = AccessTools.Method(typeof(PawnRenderNodeWorker), "GetFinalizedMaterial",
                new[] { typeof(PawnRenderNode), typeof(PawnDrawParms) });
            if (original == null) throw new MissingMethodException("PawnRenderNodeWorker.GetFinalizedMaterial");
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(Harmony_FullGelatinizationBodyTint), nameof(SizedApparelMaterialPostfix)));
            SSCLog.Important("[SSC FullGel] SizedApparel final material hook registered; no Graphic creation in draw workers.");
        }

        /// <summary>直接染色 SA 已提供的本帧材质；缓存未命中也不创建 Graphic，不改变节点位置。</summary>
        public static void SizedApparelMaterialPostfix(PawnRenderNodeWorker __instance, PawnRenderNode node,
            PawnDrawParms parms, ref Material __result)
        {
            if (sizedApparelWorkerType == null || !sizedApparelWorkerType.IsInstanceOfType(__instance)
                || node?.tree?.pawn == null || !ReferenceEquals(node.tree.pawn, parms.pawn)
                || parms.Statue || parms.rotDrawMode != RotDrawMode.Fresh
                || (parms.flags & PawnRenderFlags.Invisible) != 0 || parms.pawn.IsPsychologicallyInvisible()
                || !FullGelatinizationUtility.ShouldTintFullGelGraphic(parms.pawn)) return;
            // 继续使用已验收的身体颜色程序和队列；不改变原 SA 的节点、贴图及动画帧选择。
            Harmony_FullGelatinizationHeadTint.ApplyMaterial(ref __result, NativeShaderDefName, false, pawn: parms.pawn);
        }

        /// <summary>仅处理已成功生成的 SizedApparel 服装变体，保留图形记录对应的服装来源。</summary>
        public static void SizedApparelApparelPostfix(Apparel apparel, ref ApparelGraphicRecord rec, bool __result)
        {
            // 原图形获取失败或没有穿戴者时不能制造替代结果，普通服装也不进入胸型变体染色。
            if (!__result || apparel?.Wearer == null || rec.graphic == null) return;
            if (!ShouldTintSizedApparelGraphic(apparel.Wearer, rec.graphic)) return;

            // ApparelGraphicRecord 是值记录；修改局部图形后重建记录，继续携带原 sourceApparel。
            Graphic graphic = rec.graphic;
            TryApplyFullGelTint(apparel.Wearer, ref graphic);
            rec = new ApparelGraphicRecord(graphic, rec.sourceApparel);
        }

        /// <summary>统一检查设置与全胶状态，取得染色图形并通过 ref 替换当前调用结果。</summary>
        private static void TryApplyFullGelTint(Pawn pawn, ref Graphic graphic)
        {
            // 所有渲染入口共用同一准入条件，关闭设置、无角色或未完全胶化时保留原图形。
            if (graphic == null || !UnityData.IsInMainThread || !FullGelatinizationUtility.ShouldTintFullGelGraphic(pawn)) return;

            // 不直接改共享 Graphic 的颜色或材质，交由游戏 API 获取对应的染色版本。
            // 旧版服装软依赖入口及身体预缓存仅允许主线程调用 GraphicDatabase；失败保留原图。
            // 预缓存只标记受管原生程序，不按滑块历史值制造 Graphic 变体。
            // 保留源 Alpha，最终材质阶段只乘一次个体凝胶 Alpha。
            Shader shader = GetFullGelBodyShader();
            Color neutral = new Color(1, 1, 1, graphic.Color.a);
            Color color = shader == ShaderDatabase.TransparentPostLight ? FullGelAppearanceUtility.Read(FullGelAppearanceUtility.GetOwner(pawn)).Color : neutral;
            if (shader == ShaderDatabase.TransparentPostLight) color.a *= graphic.Color.a;
            Graphic tinted = graphic.GetColoredVersion(shader, color, color);
            if (tinted != null) graphic = tinted;
        }

        /// <summary>仅识别本模组主动生成的身体/SA 图形，不按普通服装颜色猜测归属。</summary>
        internal static bool IsManagedMaterial(Material material)
            => material != null && TryGetNativeShader(NativeShaderDefName, out Shader shader) && ReferenceEquals(material.shader, shader);

        /// <summary>识别路径带 SizedApparel 的服装变体，避免给该角色全部普通服装统一染色。</summary>
        private static bool ShouldTintSizedApparelGraphic(Pawn pawn, Graphic graphic)
        {
            if (!FullGelatinizationUtility.ShouldTintFullGelGraphic(pawn) || graphic == null) return false;

            return !string.IsNullOrEmpty(graphic.path)
                && graphic.path.IndexOf("SizedApparel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>仅使用已注册的 1.7 原生程序；资源缺失时明确回退，不接受 late-bound 假成功。</summary>
        private static Shader GetFullGelBodyShader()
        {
            // ShaderDef.Shader 非空、isSupported=true 均不能证明 Waylith 注册成功。
            // 每次从注册表确认，既不永久缓存早期失败，也不在静态字段持有 Unity 资源。
            if (TryGetNativeShader(NativeShaderDefName, out Shader shader))
            {
                if (!loggedShaderChoice)
                {
                    loggedShaderChoice = true;
                    SSCLog.Important("[SSC FullGel] Using registered native shader: " + NativeShaderDefName);
                }
                return shader;
            }

            // 降级只保证可显示；不声称内置乘色 Shader 也能柔化黑边。警告每次启动最多一次。
            if (!loggedShaderFallback)
            {
                loggedShaderFallback = true;
                SSCLog.WarningImportant("[SSC FullGel] Native shader is unavailable; using TransparentPostLight. Gel edge softening is disabled.");
            }
            return ShaderDatabase.TransparentPostLight;
        }

        /// <summary>共用真实注册检查；遮罩头部不允许降级成忽略遮罩的普通 Shader。</summary>
        internal static bool TryGetNativeShader(string defName, out Shader shader)
        {
            shader = null;
            ShaderDef def = DefDatabase<ShaderDef>.GetNamedSilentFail(defName);
            if (def == null || !Waylith.Rendering.ShaderRegistry.TryResolve(defName, out var registered)
                || ReferenceEquals(registered, Waylith.Rendering.ShaderRegistry.Default)) return false;
            shader = def.Shader;
            return shader != null;
        }
    }
}
