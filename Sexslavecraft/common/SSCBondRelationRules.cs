// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCBondRelationUtility
    {
        /// <summary>社交关系只能连接锁链实际绑定的双方；主人死亡不等于解绑。</summary>
        public static bool IsValidPair(Pawn first, Pawn second)
        {
            // 该关系在原版中双向保存，合法性必须接受两个观察方向。
            // 唯一归属依据仍是性奴侧锁链；调教员指派、Master 身份和缰绳列表均不能替代它。
            // 死亡保留历史绑定；销毁或丢弃则与锁链自身的失效条件一致。
            return first != null && second != null && first != second &&
                !first.Destroyed && !second.Destroyed && !first.Discarded && !second.Discarded &&
                (SSCBondUtility.GetBoundMaster(first) == second || SSCBondUtility.GetBoundMaster(second) == first);
        }
    }
}
