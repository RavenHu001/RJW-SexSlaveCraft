// AI 辅助生成。独立于身份/训练组件；1.6 冻结资源不接入此组件。
using System.Collections.Generic;
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
        // 这些标量仅用于Scribe；绘制线程统一读下方不可变快照。
        private float red = FullGelAppearanceUtility.DefaultRed, green = FullGelAppearanceUtility.DefaultGreen,
            blue = FullGelAppearanceUtility.DefaultBlue, opacity = FullGelAppearanceUtility.DefaultOpacity;
        // 配置只在应用/读档时分配一次快照；并行绘制读取完整版本，避免四个通道读到混合值。
        private sealed class Snapshot
        {
            internal readonly FullGelAppearance Value;
            internal Snapshot(FullGelAppearance value) { Value = value; }
        }
        private Snapshot snapshot = new Snapshot(FullGelAppearanceUtility.Default);
        internal bool Custom => custom;
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
            Volatile.Write(ref snapshot, new Snapshot(next));
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
            // 加载各字段后归一化并发布；custom=false的旧数值不覆盖当前默认。
            Volatile.Write(ref snapshot, new Snapshot(custom ? new FullGelAppearance(new Color(red, green, blue, 1), opacity) : FullGelAppearanceUtility.Default));
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            // 原版 Pawn 健康指令入口负责玩家控制范围；此处只检查当前组件仍附着。
            Pawn pawn = parent?.pawn;
            if (pawn == null || parent == null || !ReferenceEquals(FullGelAppearanceUtility.GetOwner(pawn), parent)) yield break;
            Color swatch = Appearance.Color;
            yield return new Command_Action
            {
                defaultLabel = "SSC_GelAppearance_Title".Translate(),
                defaultDesc = "SSC_GelAppearance_Description".Translate(),
                // 复用原版白色图标并染成当前基色；0%皮肤也保留可见入口，不生成纹理资产。
                icon = BaseContent.WhiteTex,
                defaultIconColor = new Color(swatch.r, swatch.g, swatch.b, 1),
                action = () =>
                {
                    if (ReferenceEquals(FullGelAppearanceUtility.GetOwner(pawn), parent))
                        Find.WindowStack.Add(new Dialog_FullGelAppearance(pawn, parent));
                }
            };
        }
    }
}
