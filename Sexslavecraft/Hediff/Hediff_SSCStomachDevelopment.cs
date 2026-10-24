// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>当前身体胃部的独立开发进度；各档收益由只读定义提供。</summary>
    public sealed class Hediff_SSCStomachDevelopment : Hediff
    {
        private static readonly HediffStage NeutralStage = new HediffStage();

        /// <summary>仅用有效进度判断完成，不依赖旧口部开发、身份或绑定。</summary>
        public bool IsComplete => float.IsFinite(Severity) && Severity >= 1f;

        /// <summary>约束运行时进度；归零仍由原版移除，不伪造恢复进度。</summary>
        public override float Severity
        {
            get => base.Severity;
            set => base.Severity = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
        }

        /// <summary>失效或离体记录不提供属性，其余状态直接使用原版阶段选择。</summary>
        public override HediffStage CurStage => SSCStomachDevelopmentUtility.IsCurrentRecord(this)
            ? base.CurStage : NeutralStage;

        /// <summary>新记录初始化时定位当前胃部，兼容正常手术及既有克隆添加入口。</summary>
        public override void PostMake()
        {
            Part = SSCStomachDevelopmentUtility.FindStomach(pawn);
            base.PostMake();
        }

        /// <summary>沿用原版健康回调，再清理当前身体的重复或无效记录。</summary>
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            SSCStomachDevelopmentUtility.Normalize(pawn);
        }

        /// <summary>重复加入取最大进度，避免原版严重度相加造成额外培养。</summary>
        public override bool TryMergeWith(Hediff other)
        {
            if (other == this) return true;
            if (other is not Hediff_SSCStomachDevelopment incoming || other.def != def ||
                !SSCStomachDevelopmentUtility.IsCurrentRecord(this)) return false;
            Severity = Math.Max(Severity, float.IsFinite(incoming.Severity) ? incoming.Severity : 0f);
            return true;
        }

        /// <summary>进度和胃部引用由基类保存；引用恢复后才规范化，不另存永久状态。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                SSCStomachDevelopmentUtility.Normalize(pawn);
        }
    }
}
