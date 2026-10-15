// AI 辅助生成：只读识别当前身体的普通 PNA 产出能力。
using Verse;

namespace SexSlaveCraft
{
    public static class SSCPnaSourceUtility
    {
        public const string ProducerDefName = "SSC_Purple_HyperLactation";

        /// <summary>供吸引评价及后续接触结算共用；查询不结算负荷或推进生产。</summary>
        public static bool IsPnaProducer(Pawn pawn)
        {
            // 来源必须是当前可用的存活肉体；普通负荷、成瘾、主人身份均不代表产出能力。
            // 主人的药效免疫不阻止其成为来源；兔分身产出暂不纳入。
            if (!SSCPnaExposureUtility.IsValidTarget(pawn)) return false;
            // 产出严重度只是生产进度，不能作为浓度或剩余可用量，也不据此缩放偏好。
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                if (hediff?.def?.defName == ProducerDefName) return true;
            return false;
        }
    }
}
