// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存和读取无主性奴化的连续进度。</summary>
    /// <remarks>
    /// <see cref="Severity"/> 是唯一进度保存值，沿用原版 Hediff 的 Scribe 流程。三阶 80% 读档后仍是 80%，阶段号按阈值重算。
    /// SSC 身份由调教组件保存，实际归属由绑定锁链保存。发作成形与仪式交接各自负责写入对应载体。
    /// </remarks>
    public sealed class Hediff_SSCOwnerlessSlave : Hediff
    {
        internal const string DefName = "SSC_Hediff_OwnerlessSlave";

        /// <summary>只查询当前健康列表中的无主记录，供身份锁与阶段解析共用。</summary>
        /// <remarks>手选或印记形成的零阶身份通常没有此记录。缺少定义或健康记录时返回空。</remarks>
        internal static Hediff_SSCOwnerlessSlave Find(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(DefName);
            return def == null ? null : pawn?.health?.hediffSet?.GetFirstHediffOfDef(def) as Hediff_SSCOwnerlessSlave;
        }

        /// <summary>沿用原版严重度写入和裁剪，已在体内的记录变化后使落差心情重算。</summary>
        public override float Severity
        {
            get => base.Severity;
            set
            {
                float previous = base.Severity;
                base.Severity = value;
                // 创建或读取记录时可能先写严重度。加入健康列表后的首次刷新由 PostAdd 负责。
                if (previous != base.Severity && ReferenceEquals(Find(pawn), this))
                    SSCCorruptionStages.NotifyContextChanged(pawn);
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // 恶堕率可以完全相同。当前阶允许的恶堕区间已变，所以仍需重算落差心情。
            SSCCorruptionStages.NotifyContextChanged(pawn);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            // 移除后按当前剩余载体重新判定，包含回到零阶的情况。
            SSCCorruptionStages.NotifyContextChanged(pawn);
        }
    }
}
