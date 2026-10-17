using System;
using RimWorld;
using UnityEngine;
using Verse;

// EN: This patch paints the body of pawns in the `全凝胶化完成` state.
// EN: It replaces the returned graphic only when SSC's full-gel tint setting is enabled; RimWorld owns graphic/material reuse.
// CN: 这个补丁负责给处于“全凝胶化完成”状态的 Pawn 身体上色。
// CN: 它只会在 SSC 的完全胶化染色设置开启时替换返回图形，图形与材质的复用由游戏自身负责。
namespace SexSlaveCraft
{
    public static class Harmony_FullGelatinizationBodyTint
    {
        // 颜色和透明度分开调节：暗部柔化由原生 Shader 完成，不依赖降低 Alpha 隐藏黑线。
        // 深紫色已获实机确认，Alpha 经 0.9 对比后调回 0.85；保留黑边柔化与亮部逻辑。
        private static readonly Color TintColor = new Color(0.46f, 0.20f, 0.68f, 0.85f);
        private const string NativeShaderDefName = "SSC_FullGelBody17";
        private static bool loggedShaderChoice;
        private static bool loggedShaderFallback;

        /// <summary>身体渲染节点的后缀，只替换本次返回的身体图形，头部仍走自身绘制路径。</summary>
        public static void Postfix(Pawn pawn, ref Graphic __result)
        {
            TryApplyFullGelTint(pawn, ref __result);
        }

        /// <summary>从实际渲染树取得 SizedApparel 节点所属角色，再复用身体染色条件。</summary>
        public static void SizedApparelWorkerPostfix(PawnRenderNode node, ref Graphic __result)
        {
            // 1.7 的节点及本机 SA 派生节点没有 pawn 字段；角色保存在 node.tree.pawn。
            // 节点、渲染树或角色缺失时传入 null，由共享入口安全跳过。
            TryApplyFullGelTint(node?.tree?.pawn, ref __result);
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
            if (graphic == null || !FullGelatinizationUtility.ShouldTintFullGelGraphic(pawn)) return;

            // 不直接改共享 Graphic 的颜色或材质，交由游戏 API 获取对应的染色版本。
            graphic = graphic.GetColoredVersion(GetFullGelBodyShader(), TintColor, TintColor);
        }

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
            ShaderDef def = DefDatabase<ShaderDef>.GetNamedSilentFail(NativeShaderDefName);
            if (def != null
                && Waylith.Rendering.ShaderRegistry.TryResolve(NativeShaderDefName, out var registered)
                && !ReferenceEquals(registered, Waylith.Rendering.ShaderRegistry.Default))
            {
                if (!loggedShaderChoice)
                {
                    loggedShaderChoice = true;
                    SSCLog.Important("[SSC FullGel] Using registered native shader: " + NativeShaderDefName);
                }
                return def.Shader;
            }

            // 降级只保证可显示；不声称内置乘色 Shader 也能柔化黑边。警告每次启动最多一次。
            if (!loggedShaderFallback)
            {
                loggedShaderFallback = true;
                SSCLog.WarningImportant("[SSC FullGel] Native shader is unavailable; using TransparentPostLight. Gel edge softening is disabled.");
            }
            return ShaderDatabase.TransparentPostLight;
        }
    }
}
