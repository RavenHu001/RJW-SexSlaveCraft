// AI 辅助生成：建筑独立半径滑条；绘制不推进药效，也不保存第二份功率。
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>将单台设备的整数半径映射到原版滑条，数值与功率仍由设备组件持有。</summary>
    public sealed class Gizmo_SSCAphrodisiacRadius : Gizmo_Slider
    {
        private readonly CompSSCAphrodisiacEmitter emitter;
        private bool dragging;
        /// <summary>绑定原设备组件，保留提交上下文；旧控件不能改为操作其他建筑。</summary>
        public Gizmo_SSCAphrodisiacRadius(CompSSCAphrodisiacEmitter emitter) { this.emitter = emitter; }
        protected override float Width => 200f;
        // 5～20共15个区间，原版量化后每步对应1格；最终范围与取整仍由组件校验。
        protected override int Increments => CompSSCAphrodisiacEmitter.MaxRadius - CompSSCAphrodisiacEmitter.MinRadius;
        protected override bool IsDraggable => emitter.CanConfigure;
        protected override bool DraggingBar { get => dragging; set => dragging = value; }
        // 原版滑条使用0～1比例；读取设备当前值，不在控件中另存半径副本。
        protected override float ValuePercent => (emitter.Radius - CompSSCAphrodisiacEmitter.MinRadius) / (float)Increments;
        /// <summary>将拖动比例还原为半径并提交；单纯绘制不产生配置写入。</summary>
        protected override float Target
        {
            get => ValuePercent;
            set
            {
                // 原版每次GUI都会回写Target；仅真正拖动时提交，避免旧UI值覆盖读档后的状态。
                if (dragging) emitter.TrySetRadius(CompSSCAphrodisiacEmitter.MinRadius + Mathf.Clamp01(value) * Increments);
            }
        }
        protected override string Title => "SSC_Emitter_RadiusTitle".Translate();
        protected override string BarLabel => "SSC_Emitter_RadiusPower".Translate(emitter.Radius, emitter.DemandWatts.ToString("F0"));
        /// <summary>提供本地化的范围与耗电规则说明。</summary>
        protected override string GetTooltip() => "SSC_Emitter_RadiusDesc".Translate();
        /// <summary>各设备半径独立，禁止多选时将本滑条合并为一个控件。</summary>
        public override bool GroupsWith(Gizmo other) => false;
        /// <summary>不传播其他控件的拖动操作，确保一次提交只修改原设备。</summary>
        public override bool InheritInteractionsFrom(Gizmo other) => false;
    }
}
