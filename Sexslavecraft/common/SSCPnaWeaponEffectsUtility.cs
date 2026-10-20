// AI 辅助生成：武器攻击与受药的轻量视觉反馈；不输入药效，不改动身体或战斗状态。
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    [DefOf]
    public static class SSCPnaWeaponEffectDefOf
    {
        public static FleckDef SSC_PnaAbsorptionHeart;

        static SSCPnaWeaponEffectDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SSCPnaWeaponEffectDefOf));
    }

    /// <summary>每次接受武器施药只提交一个爱心 Fleck；矛尖泛光由独立绘制工具管理。</summary>
    internal static class SSCPnaWeaponEffectsUtility
    {
        /// <summary>仅由两种武器入口在最终 exposure.Accepted 为 true 时调用，满负荷仍可反馈。</summary>
        internal static void ShowAbsorption(Pawn pawn)
        {
            // 伤害／治疗回调后重新只读检查；离图、死亡、主人和存读档不生成地图反应。
            // 不 RefreshBodyState，避免一个显示函数反过来触发健康协调或建立药效记录。
            if (Scribe.mode != LoadSaveMode.Inactive || !SSCPnaExposureUtility.IsValidTarget(pawn) ||
                SSCIdentityUtility.IsMaster(pawn) || !pawn.Spawned) return;
            Map map = pawn.Map;
            Vector3 position = pawn.DrawPos;
            position.z += .6f;
            FleckDef def = SSCPnaWeaponEffectDefOf.SSC_PnaAbsorptionHeart;
            if (!CanDraw(position, map, def)) return;
            var data = FleckMaker.GetDataStatic(position, map, def, .36f);
            // Core Heart 自带粉红 RGB：提高蓝通道以补偿原图颜色，最终呈紫色；普通 Mote 不发光。
            data.instanceColor = new Color(.78f, .8f, 2.45f, 1f);
            // 保持倒置心形，不随箭矢方向旋转；延长至 1.2 秒后减慢速度，总上浮仍约 0.26 格。
            data.rotation = 180f;
            data.velocity = new Vector3(0f, 0f, .22f);
            map.flecks.CreateFleck(data);
        }

        private static bool CanDraw(Vector3 position, Map map, FleckDef def)
            => def != null && IsVisible(position, map);

        private static bool IsVisible(Vector3 position, Map map)
            => map != null && map == Find.CurrentMap && Finite(position) &&
                position.ShouldSpawnMotesAt(map, false);

        private static bool Finite(Vector3 value)
            => SSCPnaMath.IsFinite(value.x) && SSCPnaMath.IsFinite(value.y) && SSCPnaMath.IsFinite(value.z);
    }
}
