// AI 辅助生成。1.7 个体凝胶外观：数据归当前身体，渲染线程只读取不可变值。
using System;
using UnityEngine;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>RGB 与皮肤不透明度分开保存；不改变五官、毛发或原贴图覆盖率。</summary>
    internal readonly struct FullGelAppearance
    {
        internal readonly Color Color;
        internal FullGelAppearance(Color rgb, float opacity)
        {
            // 单独修复每个非法通道，避免一个坏值将其余合法自定义颜色一起重置。
            Color = new Color(Unit(rgb.r, FullGelAppearanceUtility.DefaultRed), Unit(rgb.g, FullGelAppearanceUtility.DefaultGreen),
                Unit(rgb.b, FullGelAppearanceUtility.DefaultBlue), Unit(opacity, FullGelAppearanceUtility.DefaultOpacity));
        }
        /// <summary>存档损坏时使用对应默认通道；普通有限值钳制到Shader接受的0..1。</summary>
        private static float Unit(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        /// <summary>稳定帧按有效RGBA精确比较，不用近似阈值吞掉玩家的微小改色。</summary>
        internal bool Same(FullGelAppearance other) => Color.r == other.Color.r && Color.g == other.Color.g && Color.b == other.Color.b && Color.a == other.Color.a;
    }

    internal static class FullGelAppearanceUtility
    {
        // 原深紫HSV为H=272.5°、V=68%；只把S改为90%，得到RGB(.3995,.068,.68)。
        // 使用常量统一初始化、缺字段回退、恢复默认及UI色板，不逐帧做HSV转换。
        internal const float DefaultRed = .3995f, DefaultGreen = .068f, DefaultBlue = .68f, DefaultOpacity = .85f;
        internal static readonly FullGelAppearance Default = new FullGelAppearance(new Color(DefaultRed, DefaultGreen, DefaultBlue, 1f), DefaultOpacity);

        /// <summary>每次核对当前健康列表，不缓存资格；移除或替换 Hediff 后立即失效。</summary>
        internal static HediffWithComps GetOwner(Pawn pawn)
        {
            var def = SSCDefOf.SSC_FullGelatinizedBody;
            return def == null ? null : pawn?.health?.hediffSet?.GetFirstHediffOfDef(def) as HediffWithComps;
        }

        /// <summary>兼容未带新组件的边界对象；不补建组件、不改变健康列表。</summary>
        internal static FullGelAppearance Read(HediffWithComps owner)
            => owner?.TryGetComp<HediffComp_FullGelAppearance>()?.Appearance ?? Default;

        /// <summary>所有身体、头部、SA及五官阶段共用个人准入；缺组件的边界对象沿用默认开启。</summary>
        internal static bool ShouldRender(Pawn pawn)
        {
            var owner = GetOwner(pawn);
            return owner != null && (owner.TryGetComp<HediffComp_FullGelAppearance>()?.RenderingEnabled ?? true);
        }

        /// <summary>点击时重新核对身体；只刷新目标角色，旧面板不能操作新获得的凝胶身体。</summary>
        internal static bool SetRenderingEnabled(Pawn pawn, HediffWithComps expected, bool enabled)
        {
            if (!UnityData.IsInMainThread || expected == null || !ReferenceEquals(GetOwner(pawn), expected)) return false;
            var comp = expected.TryGetComp<HediffComp_FullGelAppearance>();
            if (comp == null) return false;
            if (comp.SetRenderingEnabled(enabled)) Refresh(pawn);
            return true;
        }

        /// <summary>确认时精确匹配原身体状态；窗口打开后移除/替换状态不能把草稿写到新身体。</summary>
        internal static bool Apply(Pawn pawn, HediffWithComps expected, FullGelAppearance appearance, bool reset)
        {
            if (!UnityData.IsInMainThread || expected == null || !ReferenceEquals(GetOwner(pawn), expected)) return false;
            // 旧/失效窗口不补建组件；同值确认视为成功，但不重复刷新整棵渲染树。
            var comp = expected.TryGetComp<HediffComp_FullGelAppearance>();
            if (comp == null) return false;
            if (comp.Set(appearance, reset)) Refresh(pawn);
            return true;
        }

        /// <summary>只在应用真实变化时刷新；拖动 UI 不标脏图形、不改共享材质。</summary>
        internal static void Refresh(Pawn pawn)
        {
            pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            if (pawn != null) PortraitsCache.SetDirty(pawn);
        }

        /// <summary>克隆复制身体外观的值；不向人格凝胶记录写入身体配色。</summary>
        internal static void Copy(Hediff source, Hediff target)
        {
            // 仅在两端都有外观组件时复制；其他SSC健康状态经过此入口时安全跳过。
            var from = (source as HediffWithComps)?.TryGetComp<HediffComp_FullGelAppearance>();
            var to = (target as HediffWithComps)?.TryGetComp<HediffComp_FullGelAppearance>();
            if (from != null && to != null)
            {
                to.Set(from.Appearance, !from.Custom);
                to.SetRenderingEnabled(from.RenderingEnabled);
            }
        }
    }
}
