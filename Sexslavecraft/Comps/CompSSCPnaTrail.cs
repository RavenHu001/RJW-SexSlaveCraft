// AI 辅助生成：PNA 飞行尾迹；只提交原版 Fleck 绘制数据，不参与伤害、命中或施药。
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>三种弹丸共用的视觉配置；尾迹的生命周期和收细曲线由 FleckDef 管理。</summary>
    public class CompProperties_SSCPnaTrail : CompProperties
    {
        public FleckDef fleckDef;
        public float width = .26f;
        public float maxSegmentLength = .35f;
        public float overlap = .55f;
        /// <summary>尾迹前缘与弹丸贴图尾端的间隙；沿实际飞行方向计算，不使用固定地图偏移。</summary>
        public float tailGap = .02f;
        public Color color = new Color(.78f, .3f, 1f, .95f);

        public CompProperties_SSCPnaTrail() { compClass = typeof(CompSSCPnaTrail); }
    }

    /// <summary>将连续移动路径分成有重叠的细长片段；原版粒子系统负责渐细、淡出和回收。</summary>
    public class CompSSCPnaTrail : ThingComp
    {
        private Vector3 previous;
        private Map previousMap;
        private bool hasPrevious;
        private CompProperties_SSCPnaTrail Props => (CompProperties_SSCPnaTrail)props;

        /// <summary>纯临时视觉历史不进存档；加载或重新生成后从下一次真实移动重新采样。</summary>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            hasPrevious = false;
            previousMap = null;
        }

        /// <summary>仅由视觉弹丸在原版移动完成后调用，避免从绘制回调修改 Fleck 容器。</summary>
        internal void NotifyPosition()
        {
            var projectile = parent as Projectile;
            Map map = projectile?.Map;
            if (projectile == null || projectile.Destroyed || !projectile.Spawned ||
                map == null || map != Find.CurrentMap || Props.fleckDef == null ||
                !Positive(Props.width) || !Positive(Props.maxSegmentLength) ||
                !Finite(Props.overlap) || Props.overlap < 0f ||
                !Finite(Props.tailGap) || Props.tailGap < 0f)
            {
                hasPrevious = false;
                previousMap = null;
                return;
            }

            Vector3 current = projectile.DrawPos;
            if (!Finite(current.x) || !Finite(current.y) || !Finite(current.z))
            {
                hasPrevious = false;
                return;
            }
            if (!hasPrevious || previousMap != map)
            {
                previous = current;
                previousMap = map;
                hasPrevious = true;
                return;
            }

            Vector3 start = previous;
            previous = current;
            double dx = current.x - start.x, dz = current.z - start.z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            // 不为停顿反复叠加，也不把传送／外部改位连接成长线；单次最多 16 段。
            if (distance < .0001 || distance > 4d || !current.ShouldSpawnMotesAt(map, false)) return;
            int count = (int)Math.Min(16d, Math.Max(1d, Math.Ceiling(distance / Props.maxSegmentLength)));
            float angle = (float)(Math.Atan2(dx, dz) * (180d / Math.PI));
            float length = (float)(distance / count) + Props.overlap;
            // 片段重叠会向前多伸 overlap/2；同时扣除它和弹丸半长，使前缘停在箭尾之后。
            // 三种竖向投射物图均以本地 Y 轴表示长度；不改变实际位置、速度或粒子尺寸。
            float projectileLength = projectile.def?.graphicData?.drawSize.y ?? 0f;
            float rearOffset = Props.overlap * .5f + Props.tailGap +
                (Positive(projectileLength) ? projectileLength * .5f : 0f);
            if (!Finite(rearOffset)) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = Vector3.Lerp(start, current, (i + .5f) / count);
                position.x -= (float)(dx / distance) * rearOffset;
                position.z -= (float)(dz / distance) * rearOffset;
                if (!position.ShouldSpawnMotesAt(map, false)) continue;
                FleckCreationData data = FleckMaker.GetDataStatic(position, map, Props.fleckDef);
                data.rotation = angle;
                data.exactScale = new Vector3(Props.width, 1f, length);
                data.instanceColor = Props.color;
                map.flecks.CreateFleck(data);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0f;
    }

    /// <summary>仍完整执行 Bullet 移动和命中；仅在存活弹丸完成移动后通知视觉组件。</summary>
    public class Projectile_SSCPnaTrail : Bullet
    {
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Destroyed && Spawned) this.TryGetComp<CompSSCPnaTrail>()?.NotifyPosition();
        }
    }
}
