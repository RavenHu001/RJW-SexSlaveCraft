// AI 辅助生成：复用武器最终绘制变换叠画矛尖，不改动作角度、位置、贴图或原始材质。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(JitterHandler), nameof(JitterHandler.ProcessPostTickVisuals))]
    internal static class Harmony_SSCPnaSpearGlowRecovery
    {
        private static void Postfix(JitterHandler __instance, int ticksPassed)
            => SSCPnaSpearGlowUtility.Advance(__instance, ticksPassed);
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    internal static class Harmony_SSCPnaSpearGlowDraw
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            MethodInfo original = AccessTools.Method(typeof(Graphics), nameof(Graphics.DrawMesh),
                new[] { typeof(Mesh), typeof(Matrix4x4), typeof(Material), typeof(int) });
            MethodInfo replacement = AccessTools.Method(typeof(Harmony_SSCPnaSpearGlowDraw), nameof(DrawWeapon));
            // 只替换此方法中唯一的四参 DrawMesh；不在未知 IL 上猜测插入点。
            if (original == null || codes.Count(c => c.Calls(original)) != 1)
                throw new InvalidOperationException("[SSC PNA glow] Unexpected DrawEquipmentAiming draw call layout.");
            foreach (CodeInstruction code in codes)
            {
                if (!code.Calls(original)) { yield return code; continue; }
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code).MoveBlocksFrom(code);
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
        }

        internal static void DrawWeapon(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Thing weapon)
        {
            // 工业短鞭与太空丝鞭复用此入口；各自选图和握持变换，不增加全局绘制补丁。
            if (SSCPnaWhipRenderer.TryDraw(weapon, mesh, matrix, material, layer)) return;
            // 其余武器原绘制完整执行一次；短矛既有叠画逻辑保持。
            Graphics.DrawMesh(mesh, matrix, material, layer);
            SSCPnaSpearGlowRenderer.Draw(weapon, mesh, matrix, material, layer);
        }
    }

    [StaticConstructorOnStartup]
    internal static class SSCPnaSpearGlowRenderer
    {
        // 当前 256px 横置短矛从 x=183 起为完整紫色刃部；原图 alpha 精确保留刃部轮廓。
        // 用裁切网格充当遮罩，无需新建像素贴图或每帧读图。各层正常／镜像网格分别缓存。
        internal const float BladeStartU = 183f / 256f;
        // 原图 alpha>32 的完整矛头范围：x=[183,250)、y=[114,142)，中心位于原画布右侧。
        internal const float BladeEndU = 250f / 256f;
        internal const float BladeHeight = 28f / 256f;
        internal const float HaloScale = 4.5f;
        private static Mesh normal, flipped;
        private static Mesh haloNormal, haloFlipped;
        private static Material haloMaterial;
        private static readonly Dictionary<Texture2D, Material> bladeMaterials = new Dictionary<Texture2D, Material>();
        private static MaterialPropertyBlock block;

        internal static void Draw(Thing weapon, Mesh source, Matrix4x4 matrix, Material material, int layer)
        {
            float alpha = SSCPnaSpearGlowUtility.GetAlpha(weapon);
            if (alpha <= 0f || weapon.MapHeld == null || weapon.MapHeld != Find.CurrentMap ||
                material?.mainTexture is not Texture2D texture ||
                (source != MeshPool.plane10 && source != MeshPool.plane10Flip)) return;
            bool flip = source == MeshPool.plane10Flip;
            Mesh mask = flip ? (flipped ??= CreateMask(true)) : (normal ??= CreateMask(false));
            // 不复用 MaterialPool 中原版 Fleck 可能取得的同一材质实例。
            // 静态材质只存固定纹理/白色底色，逐次亮度始终放在提交时复制的属性块中。
            if (!bladeMaterials.TryGetValue(texture, out Material glow))
                bladeMaterials.Add(texture, glow = CreateMaterial(texture));
            block ??= new MaterialPropertyBlock();
            // 独立的原版柔边纹理形成真正向外淡开的光晕；不放大矛头原图，避免双刃重影。
            haloMaterial ??= CreateMaterial(ContentFinder<Texture2D>.Get("Things/Mote/BrightFlash"));
            Mesh halo = flip ? (haloFlipped ??= CreateHalo(true)) : (haloNormal ??= CreateHalo(false));
            Matrix4x4 haloMatrix = matrix;
            haloMatrix.m13 += .0005f;
            block.SetColor(ShaderPropertyIDs.Color, new Color(.85f, .3f, 1f, alpha * .8f));
            Graphics.DrawMesh(halo, haloMatrix, haloMaterial, layer, null, 0, block);
            block.SetColor(ShaderPropertyIDs.Color, new Color(1.6f, 1.1f, 2f, alpha * .85f));
            // 复制最终矩阵，只抬高极少绘制高度避免与矛体重合；不修改原矩阵或武器角度。
            matrix.m13 += .001f;
            Graphics.DrawMesh(mask, matrix, glow, layer, null, 0, block);
        }

        /// <summary>纹理只读共享；材质由 SSC 独占，避免与原版火花/闪光共用可变实例。</summary>
        internal static Material CreateMaterial(Texture2D texture)
        {
            Material material = new Material(ShaderDatabase.MoteGlow);
            material.name = "SSC PNA spearhead glow";
            material.mainTexture = texture;
            material.color = new Color(1f, 1f, 1f, 1f);
            return material;
        }

        /// <summary>只围绕矛头局部中心扩张；复用武器矩阵，所有朝向与镜像保持同样的 4.5 倍范围。</summary>
        internal static Mesh CreateHalo(bool flip)
        {
            float center = (BladeStartU + BladeEndU) * .5f - .5f;
            if (flip) center = -center;
            float halfWidth = (BladeEndU - BladeStartU) * HaloScale * .5f;
            float halfHeight = BladeHeight * HaloScale * .5f;
            var mesh = new Mesh { name = "SSC PNA spearhead soft halo" };
            mesh.vertices = new[] { new Vector3(center - halfWidth, 0f, -halfHeight),
                new Vector3(center - halfWidth, 0f, halfHeight),
                new Vector3(center + halfWidth, 0f, halfHeight),
                new Vector3(center + halfWidth, 0f, -halfHeight) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            // MoteGlow 会乘顶点色；显式给出白色，不依赖兼容渲染器补缺省属性。
            mesh.colors = WhiteVertices();
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Mesh CreateMask(bool flip)
        {
            float left = flip ? -.5f : BladeStartU - .5f;
            float right = flip ? .5f - BladeStartU : .5f;
            float uLeft = flip ? 1f : BladeStartU;
            float uRight = flip ? BladeStartU : 1f;
            var mesh = new Mesh { name = "SSC PNA spearhead glow" };
            mesh.vertices = new[] { new Vector3(left, 0f, -.5f), new Vector3(left, 0f, .5f),
                new Vector3(right, 0f, .5f), new Vector3(right, 0f, -.5f) };
            mesh.uv = new[] { new Vector2(uLeft, 0f), new Vector2(uLeft, 1f),
                new Vector2(uRight, 1f), new Vector2(uRight, 0f) };
            mesh.colors = WhiteVertices();
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color[] WhiteVertices() => new[] { new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f) };
    }
}
