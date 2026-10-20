// AI 辅助生成：与短鞭最终绘制位置重合的宽半弧残影，不参与伤害或施药。
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    internal static class SSCPnaWhipHitUtility
    {
        internal const float PivotX = 85f / 256f - .5f;
        internal const float Radius = .43f;
        internal const float HalfWidth = .30f;
        internal const int Segments = 36;
        private const int RadialSteps = 4;
        private static Mesh normal, flipped;
        private static Material material;
        private static MaterialPropertyBlock block;

        /// <summary>前 6 tick 保持显著，后 12 tick 随回落淡出；暂停与加速复用姿态时钟。</summary>
        internal static float GetAlpha(Thing weapon)
        {
            int age = SSCPnaWhipPoseUtility.GetHitAge(weapon);
            if (age < 0) return 0f;
            if (age < 2) return .65f + age * .175f;
            if (age < SSCPnaWhipPoseUtility.AttackTicks) return 1f;
            return (SSCPnaWhipPoseUtility.LifetimeTicks - age) / (float)SSCPnaWhipPoseUtility.RecoveryTicks;
        }

        internal static void Draw(Thing weapon, Mesh source, Matrix4x4 matrix, int layer)
        {
            float alpha = GetAlpha(weapon);
            if (alpha <= 0f || weapon.MapHeld == null || weapon.MapHeld != Find.CurrentMap ||
                (source != MeshPool.plane10 && source != MeshPool.plane10Flip)) return;
            bool flip = source == MeshPool.plane10Flip;
            Mesh mesh = flip ? (flipped ??= CreateMesh(true)) : (normal ??= CreateMesh(false));
            // 原版纹理只读共享；创建私有材质，不污染原版 Fleck 或其他武器的共享材质。
            material ??= new Material(ShaderDatabase.Mote) {
                name = "SSC PNA whip broad sweep", mainTexture = ContentFinder<Texture2D>.Get("Things/Mote/BrightFlash"),
                color = new Color(1f, 1f, 1f, 1f)
            };
            block ??= new MaterialPropertyBlock();
            block.SetColor(ShaderPropertyIDs.Color, new Color(1f, .4f, 1f, alpha * .95f));
            // 先画残影、略低于鞭束，保留实体鞭束轮廓；不修改传入的武器矩阵。
            matrix.m13 -= .0005f;
            Graphics.DrawMesh(mesh, matrix, material, layer, null, 0, block);
        }

        /// <summary>
        /// 以标定束根为轴，从上方 +155° 顺时针扫至下方 -25°。前缘跨过鞭束，呈现向下抽落。
        /// 固定的 36 段与 4 层径向带仅创建两份镜像网格；每帧不采样鞭尖、不分配顶点数组。
        /// </summary>
        internal static Mesh CreateMesh(bool flip)
        {
            int stride = RadialSteps + 1;
            var vertices = new Vector3[(Segments + 1) * stride];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[Segments * RadialSteps * 6];
            for (int segment = 0; segment <= Segments; segment++)
            {
                double along = segment / (double)Segments;
                double angle = (155 - along * 180) * Math.PI / 180;
                // 提高整片亮度并扩展高亮区域，两端仍透明收淡，保留从上往下的前缘层次。
                float alongAlpha = (float)Math.Pow(Math.Sin(along * Math.PI), .30) * (.65f + .35f * (float)along);
                for (int radial = 0; radial <= RadialSteps; radial++)
                {
                    float across = radial / (float)RadialSteps;
                    float radius = Radius + (across * 2 - 1) * HalfWidth;
                    int index = segment * stride + radial;
                    float x = PivotX + (float)Math.Cos(angle) * radius;
                    vertices[index] = new Vector3(flip ? -x : x, 0f, (float)Math.Sin(angle) * radius);
                    // BrightFlash 的纵向柔边叠加透明边缘，避免半透明方块或硬切环边。
                    uv[index] = new Vector2(.5f, across);
                    float edgeAlpha = radial == 0 || radial == RadialSteps ? 0f : 1f;
                    colors[index] = new Color(1f, 1f, 1f, edgeAlpha * alongAlpha);
                }
            }
            int next = 0;
            for (int segment = 0; segment < Segments; segment++)
                for (int radial = 0; radial < RadialSteps; radial++)
                {
                    int a = segment * stride + radial, b = a + stride;
                    // 顺时针上半弧先反转绕序；水平镜像再反转一次，两个版本均朝上。
                    bool reverse = !flip;
                    triangles[next++] = a; triangles[next++] = reverse ? b + 1 : b; triangles[next++] = reverse ? b : b + 1;
                    triangles[next++] = a; triangles[next++] = reverse ? a + 1 : b + 1; triangles[next++] = reverse ? b + 1 : a + 1;
                }
            var mesh = new Mesh {name = "SSC PNA whip half-circle afterimage", vertices = vertices, uv = uv, colors = colors};
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
