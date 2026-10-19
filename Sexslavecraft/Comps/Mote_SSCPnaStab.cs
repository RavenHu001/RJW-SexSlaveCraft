// AI 辅助生成：固定尾部的尖梭形刺芒；只更新单个原版 Mote 的位置与缩放。
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>先向前伸长，再收细淡出；原版 Mote 负责透明度、暂停、生命周期和回收。</summary>
    public class Mote_SSCPnaStab : Mote
    {
        private Vector3 root, direction;
        private float maximumLength;
        private bool initialized;
        internal const float ExtendTime = .10f;
        internal const float Lifetime = .42f;

        /// <summary>保存出手瞬间的地图坐标，不追踪目标，不参与实际武器移动或伤害。</summary>
        internal void Initialize(Vector3 origin, Vector3 unitDirection, float length, float rotation)
        {
            root = origin;
            direction = unitDirection;
            maximumLength = length;
            exactRotation = rotation;
            // 贴图自带紫色与明暗，不再次着色；原透明画布留白由 graphicData.drawSize 补偿。
            instanceColor = new Color(1f, 1f, 1f, 1f);
            initialized = true;
            UpdateGeometry(0f);
        }

        protected override void TimeInterval(float deltaTime)
        {
            base.TimeInterval(deltaTime);
            if (!Destroyed && initialized) UpdateGeometry(AgeSecs);
        }

        private void UpdateGeometry(float age)
        {
            // 前六个正常游戏 tick 向前伸展。长度与中心同时变化，尾部始终停在 root，
            // 而非围绕一个亮点对称放大；x/z 两轴都沿快照方向计算，斜向攻击同样成立。
            float progress = Clamp01(age / ExtendTime);
            float extension = 1f - (1f - progress) * (1f - progress);
            float length = maximumLength * (.12f + .88f * extension);
            float fade = Clamp01((age - ExtendTime) / (Lifetime - ExtendTime));
            float width = .34f * Math.Max(.02f, (1f - fade) * (1f - fade));
            linearScale = new Vector3(width, 1f, length);
            exactPosition = new Vector3(root.x + direction.x * length * .5f, root.y,
                root.z + direction.z * length * .5f);
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
