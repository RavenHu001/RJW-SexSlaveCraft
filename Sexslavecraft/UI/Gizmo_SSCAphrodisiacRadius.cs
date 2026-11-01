// AI 辅助生成：建筑独立半径滑条；绘制不推进药效，也不保存第二份功率。
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public sealed class Gizmo_SSCAphrodisiacRadius : Gizmo_Slider
    {
        private readonly CompSSCAphrodisiacEmitter emitter;
        private bool dragging;
        public Gizmo_SSCAphrodisiacRadius(CompSSCAphrodisiacEmitter emitter) { this.emitter = emitter; }
        protected override float Width => 200f;
        protected override int Increments => CompSSCAphrodisiacEmitter.MaxRadius - CompSSCAphrodisiacEmitter.MinRadius;
        protected override bool IsDraggable => emitter.CanConfigure;
        protected override bool DraggingBar { get => dragging; set => dragging = value; }
        protected override float ValuePercent => (emitter.Radius - CompSSCAphrodisiacEmitter.MinRadius) / (float)Increments;
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
        protected override string GetTooltip() => "SSC_Emitter_RadiusDesc".Translate();
        public override bool GroupsWith(Gizmo other) => false;
        public override bool InheritInteractionsFrom(Gizmo other) => false;
    }
}
