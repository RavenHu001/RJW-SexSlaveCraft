// AI 辅助生成
using System;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCBondRelationUtility
    {
        /// <summary>仅供完整仪式结算调用；保留实际主人、80 好感及不重复发放三个条件。</summary>
        public static bool TryCreateAfterRitual(Pawn master, Pawn slave, Func<bool> canContinue = null)
        {
            // 主持者只提供本场仪式角色，不能据此推断所有权。
            // 这里额外检查 slave → master 的有向锁链，保留原有好感门槛并阻止重复奖励。
            if (SSCDefOf.SSC_FlawedLovers == null || master?.relations == null || slave?.relations == null ||
                !IsValidPair(master, slave) || SSCBondUtility.GetBoundMaster(slave) != master ||
                slave.relations.OpinionOf(master) < 80 ||
                master.relations.DirectRelationExists(SSCDefOf.SSC_FlawedLovers, slave)) return false;
            // 好感查询可被外部补丁回调；写入前再次确认本场完成资格及实际归属。
            if ((canContinue != null && !canContinue()) || !IsValidPair(master, slave) ||
                SSCBondUtility.GetBoundMaster(slave) != master) return false;
            // 原生接口负责添加反向记录、刷新双方社交缓存和通知健康系统。
            master.relations.AddDirectRelation(SSCDefOf.SSC_FlawedLovers, slave);
            return true;
        }

        /// <summary>只删除失效的奖励关系，不按绑定或好感自动补发；使用原生接口更新双方及社交缓存。</summary>
        public static int RemoveInvalidRelations(Pawn pawn)
        {
            if (pawn?.relations == null || SSCDefOf.SSC_FlawedLovers == null) return 0;
            int removed = 0;
            // 先取得失效记录快照，再调用会同时修改双方列表的原生删除接口。
            // 只删除本模组奖励关系；普通关系及仍匹配锁链的奖励不受好感变化影响。
            foreach (var relation in pawn.relations.DirectRelations
                .Where(r => r.def == SSCDefOf.SSC_FlawedLovers && !IsValidPair(pawn, r.otherPawn)).ToList())
            {
                // 同一对关系可能已在另一端的清理中删除；原生 Try 接口允许重复维护。
                if (pawn.relations.TryRemoveDirectRelation(relation.def, relation.otherPawn)) removed++;
            }
            return removed;
        }
    }
}
