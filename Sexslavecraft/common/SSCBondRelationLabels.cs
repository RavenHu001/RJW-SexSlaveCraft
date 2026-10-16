// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class SSCBondRelationLabels
    {
        /// <summary>在同时知道查看者和对方时按实际主从方向命名，其他关系保留原版性别称谓。</summary>
        public static string GetLabel(PawnRelationDef relation, Pawn other, Pawn observer)
            => GetDirectionalLabel(relation, other, observer) ?? relation.GetGenderSpecificLabel(other);

        private static string GetDirectionalLabel(PawnRelationDef relation, Pawn other, Pawn observer)
        {
            // 只有本模组且当前有效的主从关系使用方向称谓。
            // 返回空值表示交回调用者使用对应原版名称入口，不在显示查询中修改关系。
            if (relation != SSCDefOf.SSC_FlawedLovers || !SSCBondRelationUtility.IsValidPair(observer, other))
                return null;
            // 查看者的锁链指向对方时，对方是主人；反方向则对方是性奴。
            // 主从角色先于性别判断，女性后缀仅用于需要语法性别的语言。
            string role = SSCBondUtility.GetBoundMaster(observer) == other ? "Master" : "Slave";
            string suffix = other.gender == Gender.Female ? "Female" : string.Empty;
            return ("SSC_BondRelation_" + role + suffix).Translate().ToString();
        }

        public static string GetLabelCap(PawnRelationDef relation, Pawn other, Pawn observer)
        {
            string directional = GetDirectionalLabel(relation, other, observer);
            // 其他关系仍走原版 Cap 入口，也保留第三方对该入口的修改。
            return directional == null ? relation.GetGenderSpecificLabelCap(other) : directional.CapitalizeFirst();
        }
    }
}
