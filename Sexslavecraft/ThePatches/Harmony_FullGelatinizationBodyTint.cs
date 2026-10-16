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
        // 主、次颜色使用同一紫色及透明度；日志标记只抑制重复输出，不缓存角色或绘制状态。
        private static readonly Color TintColor = new Color(0.72f, 0.38f, 0.95f, 0.82f);
        private static bool loggedShaderChoice;

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

        /// <summary>1.7 明确使用内置透明 Shader，并只记录一次实际选用的方案。</summary>
        private static Shader GetFullGelBodyShader()
        {
            // 1.7 的未注册自定义 Shader 也会返回非空 late-bound 对象。
            // 旧 ShaderLab 源码尚未移植，明确使用内置透明材质，避免误用引擎默认 Shader。
            // isSupported 在该 late-bound 对象上也可为 true，故不能将它补作加载成功判据。
            if (!loggedShaderChoice)
            {
                loggedShaderChoice = true;
                SSCLog.Important("[SSC FullGel] Using built-in TransparentPostLight for RimWorld 1.7.");
            }

            // 内置 Shader 的生命周期由 ShaderDatabase 管理，此补丁不另持有可失效的自定义缓存。
            return ShaderDatabase.TransparentPostLight;
        }
    }
}
