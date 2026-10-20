// AI 辅助生成：短鞭统一柄心握持与待机45°姿态；复用原版绘制入口和正常／镜像网格。
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    internal static class Harmony_SSCPnaWhipAttack
    {
        // 从原版所有近战动作的共用入口取主手武器，不依赖本次选择了鞭束、柄击或拳击。
        // 不挂 Notify_MeleeAttackOn：该通知亦被灭火／点火动作使用。
        private static void Prefix(Verb_MeleeAttack __instance, out Thing __state)
        {
            __state = default;
            Pawn pawn = __instance.CasterPawn;
            Thing weapon = pawn?.equipment?.Primary;
            // 对齐原版的两个提前退出条件；不修改原方法或它的返回值。
            if (SSCPnaWhipPoseUtility.IsWhip(weapon) && pawn.Spawned && !pawn.stances.FullBodyBusy)
            {
                __state = weapon;
                SSCPnaWhipPoseUtility.Clear(weapon);
            }
        }

        private static void Postfix(Verb_MeleeAttack __instance, Thing __state, bool __result)
        {
            // 普通 Postfix 不在父类异常时执行。命中、打空、闪避的正常返回都播放。
            // 返回值只读：命中（含完全挡伤）标记残影，打空／闪避仍播放武器姿态。
            if (__state != null) SSCPnaWhipPoseUtility.Begin(__state, __instance.CasterPawn, __result);
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved))]
    internal static class Harmony_SSCPnaWhipUnequip
    {
        // 原版移除通知正常执行；避免同 tick 丢下再捡起时恢复旧动作。
        private static void Postfix(ThingWithComps eq) => SSCPnaWhipPoseUtility.Clear(eq);
    }

    [StaticConstructorOnStartup]
    internal static class SSCPnaWhipRenderer
    {
        internal const string TexturePrefix = "Things/Item/Equipment/SSC_PnaShortWhip_";
        // 三张手持图共同的柄心标定；不可按各姿态外包围框分别居中。
        internal const float HandleU = 50f / 256f;
        private static readonly string[] Names = { "Idle", "Attack", "Recovery" };
        private static readonly Dictionary<Material, Material[]> Materials = new Dictionary<Material, Material[]>();

        /// <summary>
        /// 从既有 DrawEquipmentAiming 的唯一绘制分支调用，成功时恰好绘制一次。
        /// 物品图仍由 ThingDef 负责，不通过更改 Thing.Graphic 或 Def 切换姿态。
        /// </summary>
        internal static bool TryDraw(Thing weapon, Mesh mesh, Matrix4x4 matrix, Material original, int layer)
        {
            if (!SSCPnaWhipPoseUtility.IsWhip(weapon) || weapon.Destroyed ||
                Scribe.mode != LoadSaveMode.Inactive || original == null ||
                (mesh != MeshPool.plane10 && mesh != MeshPool.plane10Flip)) return false;

            SSCPnaWhipPose pose = SSCPnaWhipPoseUtility.GetPose(weapon);
            if (!Materials.TryGetValue(original, out Material[] poses))
            {
                poses = new Material[Names.Length];
                for (int i = 0; i < poses.Length; i++)
                {
                    // 独立副本保留原着色器和颜色；不修改共享原材质，亦不使用泛光着色器。
                    poses[i] = new Material(original) {
                        name = "SSC PNA whip " + Names[i],
                        mainTexture = ContentFinder<Texture2D>.Get(TexturePrefix + Names[i])
                    };
                }
                Materials.Add(original, poses);
            }
            bool flip = mesh == MeshPool.plane10Flip;
            // 原版传入位置是画布中心。先把实际柄心移到该位置，让放大、镜像及姿态切换
            // 都围绕同一个握持点；保留原版角色朝向、体型距离和出手位移，不另取 Pawn.DrawPos。
            matrix = GripMatrix(matrix, flip);
            // 仅待机姿态固定为柄尾朝下的 45°；挥击与回落继续跟随原版目标方向。
            if (pose == SSCPnaWhipPose.Idle) matrix = IdleMatrix(matrix, flip);
            // 残影先画在鞭束下方；共用最终矩阵，不另外驱动武器旋转或插入动作。
            SSCPnaWhipHitUtility.Draw(weapon, mesh, matrix, layer);
            Graphics.DrawMesh(mesh, matrix, poses[(int)pose], layer);
            return true;
        }

        /// <summary>
        /// 将标定柄心对齐到传入矩阵的原点。平移补偿沿已缩放、已旋转的局部横轴计算，
        /// 镜像同时反转局部柄心；只改平面平移，不改大小、方向或绘制高度。
        /// </summary>
        internal static Matrix4x4 GripMatrix(Matrix4x4 matrix, bool flip)
        {
            float anchorX = (HandleU - .5f) * (flip ? -1f : 1f);
            matrix.m03 -= matrix.m00 * anchorX;
            matrix.m23 -= matrix.m20 * anchorX;
            return matrix;
        }

        /// <summary>绕已校正的柄心旋转，保持其世界位置、缩放、绘制高度与镜像不变。</summary>
        internal static Matrix4x4 IdleMatrix(Matrix4x4 matrix, bool flip)
        {
            const float diagonal = .7071067812f;
            float anchorX = (HandleU - .5f) * (flip ? -1f : 1f);
            float scaleX = (float)Math.Sqrt(matrix.m00 * matrix.m00 + matrix.m20 * matrix.m20);
            float scaleZ = (float)Math.Sqrt(matrix.m02 * matrix.m02 + matrix.m22 * matrix.m22);
            // 原图柄尾朝左；正常图旋转 -45°、镜像图 +45°，两者柄尾的世界 z 均朝下。
            float new00 = diagonal * scaleX;
            float new20 = (flip ? -diagonal : diagonal) * scaleX;
            matrix.m03 += (matrix.m00 - new00) * anchorX;
            matrix.m23 += (matrix.m20 - new20) * anchorX;
            matrix.m00 = new00;
            matrix.m20 = new20;
            matrix.m02 = (flip ? diagonal : -diagonal) * scaleZ;
            matrix.m22 = diagonal * scaleZ;
            return matrix;
        }
    }
}
