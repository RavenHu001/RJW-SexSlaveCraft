// AI 辅助生成。独立于身份/训练组件；1.6 冻结资源不接入此组件。
using System.Threading;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public sealed class HediffCompProperties_FullGelAppearance : HediffCompProperties
    {
        public HediffCompProperties_FullGelAppearance() { compClass = typeof(HediffComp_FullGelAppearance); }
    }

    public sealed class HediffComp_FullGelAppearance : HediffComp
    {
        private bool custom;
        // 新身体默认开启。旧档缺字段时只在读档阶段继承旧全局设置，运行时不再查询总开关。
        private bool renderingEnabled = true;
        // 这些标量仅用于Scribe；绘制线程统一读下方不可变快照。
        private float red = FullGelAppearanceUtility.DefaultRed, green = FullGelAppearanceUtility.DefaultGreen,
            blue = FullGelAppearanceUtility.DefaultBlue, opacity = FullGelAppearanceUtility.DefaultOpacity;
        // 配置只在应用/读档时分配一次快照；并行绘制读取完整版本，避免四个通道读到混合值。
        private sealed class Snapshot
        {
            internal readonly FullGelAppearance Value;
            internal readonly bool RenderingEnabled;
            internal Snapshot(FullGelAppearance value, bool enabled) { Value = value; RenderingEnabled = enabled; }
        }
        private Snapshot snapshot = new Snapshot(FullGelAppearanceUtility.Default, true);
        internal bool Custom => custom;
        internal bool RenderingEnabled => Volatile.Read(ref snapshot).RenderingEnabled;
        // 运行时序号不入档；读档后缓存从空开始。稳定帧只读这份值快照。
        internal int Revision { get; private set; }
        internal FullGelAppearance Appearance => Volatile.Read(ref snapshot).Value;

        internal bool Set(FullGelAppearance value, bool reset)
        {
            // 恢复默认同时清除自定义标记；已保存的自定义颜色不会因默认S变化被自动迁移。
            var next = reset ? FullGelAppearanceUtility.Default : value;
            if (custom == !reset && Appearance.Same(next)) return false;
            red = next.Color.r; green = next.Color.g; blue = next.Color.b; opacity = next.Color.a;
            custom = !reset; Revision++;
            // 所有保存字段写完后一次发布，使并行绘制只看到旧配置或完整的新配置。
            Volatile.Write(ref snapshot, new Snapshot(next, renderingEnabled));
            return true;
        }

        /// <summary>个人渲染与自定义配色独立；关闭不丢弃配色，恢复配色不改变开关。</summary>
        internal bool SetRenderingEnabled(bool enabled)
        {
            if (renderingEnabled == enabled) return false;
            renderingEnabled = enabled; Revision++;
            Volatile.Write(ref snapshot, new Snapshot(Appearance, enabled));
            return true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            // 保留原存档键名。缺字段取新默认S90；明确保存的自定义RGB/Alpha原样读取。
            Scribe_Values.Look(ref custom, "sscGelCustom", false);
            Scribe_Values.Look(ref red, "sscGelRed", FullGelAppearanceUtility.DefaultRed);
            Scribe_Values.Look(ref green, "sscGelGreen", FullGelAppearanceUtility.DefaultGreen);
            Scribe_Values.Look(ref blue, "sscGelBlue", FullGelAppearanceUtility.DefaultBlue);
            Scribe_Values.Look(ref opacity, "sscGelOpacity", FullGelAppearanceUtility.DefaultOpacity);
            // 必须强制保存 true/false：即使与旧设置相同也保留个人字段，后续加载不再重复迁移。
            Scribe_Values.Look(ref renderingEnabled, "sscGelRenderingEnabled",
                SSCMod.settings?.enableFullGelatinizedBodyTint ?? true, forceSave: true);
            // 加载各字段后归一化并发布；custom=false的旧数值不覆盖当前默认。
            Volatile.Write(ref snapshot, new Snapshot(custom ? new FullGelAppearance(new Color(red, green, blue, 1), opacity) : FullGelAppearanceUtility.Default, renderingEnabled));
        }

        // 外观入口统一由ITab提供；不再向小人命令栏重复添加调色指令。
    }
}
