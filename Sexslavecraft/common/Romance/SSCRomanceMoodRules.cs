// AI 辅助生成
using System;

namespace SexSlaveCraft
{
    /// <summary>受控感受与关系损失分开编码；None 不代表可以生成零值记忆。</summary>
    public enum SSCRomanceMoodEvent { None, ForcedRefusal, ForcedAcceptance, CommandRomance, CommandProposal, CommandBreakup, RelationshipLoss }
    /// <summary>互动前直接关系，不使用 Ex 关系或当前好感推断。</summary>
    public enum SSCRomanceOldRelation { None, Lover, Fiance, Spouse }

    /// <summary>无游戏副作用的数值与替换契约；比较冻结基准值，不比较衰减后的显示效果。</summary>
    public static class SSCRomanceMoodRules
    {
        /// <summary>缺需求是中性修正，不能当作零恶堕；25% 与 75% 使用精确包含边界。</summary>
        public static int CorruptionModifier(float? current) => !current.HasValue ? 0 : current.Value < .25f ? -1 : current.Value >= .75f ? 1 : 0;
        /// <summary>冻结互动前好感分类；亲近只表示好感，不能推断爱情。</summary>
        public static int OpinionCategory(int opinion) => opinion <= 0 ? 0 : opinion < 40 ? 1 : 2;

        /// <summary>只有有效限制才计算；普通／受虐狂互斥，恶堕只加入一次。</summary>
        public static int Continuous(int stage, bool active, bool passive, bool masochist, float? corruption)
        {
            active &= stage >= 2; passive &= stage >= 3;
            if (!active && !passive) return 0;
            int value = stage >= 4 ? (active && passive ? 2 : active ? 1 : 0) :
                stage == 3 ? (active && passive ? -3 : active ? -1 : -2) : -3;
            return Math.Clamp(value + (masochist ? 2 : 0) + CorruptionModifier(corruption), -4, 4);
        }

        /// <summary>第一组仅三阶被动／四阶命令；零值由调用方忽略，不据此刷新旧记忆。</summary>
        public static int Experience(SSCRomanceMoodEvent kind, int stage, bool masochist, float? corruption, int opinionCategory)
        {
            bool passive = kind == SSCRomanceMoodEvent.ForcedRefusal || kind == SSCRomanceMoodEvent.ForcedAcceptance;
            if (passive ? stage < 3 : stage < 4 || kind < SSCRomanceMoodEvent.CommandRomance || kind > SSCRomanceMoodEvent.CommandBreakup) return 0;
            int value = stage >= 4 ? (kind == SSCRomanceMoodEvent.CommandRomance || kind == SSCRomanceMoodEvent.CommandProposal ? 2 : 1) :
                kind == SSCRomanceMoodEvent.ForcedRefusal ? -2 : -3;
            if (masochist) value += 2;
            if (kind == SSCRomanceMoodEvent.ForcedRefusal) value += opinionCategory == 2 ? -2 : 0;
            else if (kind != SSCRomanceMoodEvent.CommandBreakup) value += opinionCategory == 0 ? -2 : opinionCategory == 2 ? 1 : 0;
            return Math.Clamp(value + CorruptionModifier(corruption), -6, 6);
        }

        /// <summary>关系损失不受阶段、受虐狂及恶堕减免；无旧关系或反感对象不补发。</summary>
        public static int Loss(SSCRomanceOldRelation relation, int opinionCategory)
            => relation == SSCRomanceOldRelation.None || opinionCategory == 0 ? 0 : opinionCategory == 1 ?
                (relation == SSCRomanceOldRelation.Lover ? -2 : -3) : relation == SSCRomanceOldRelation.Lover ? -4 : -5;
        /// <summary>短期受控经历三天；关系损失按旧恋人／婚约二十五天、配偶三十天。</summary>
        public static int Duration(SSCRomanceMoodEvent kind, SSCRomanceOldRelation relation)
            => (kind != SSCRomanceMoodEvent.RelationshipLoss ? 3 : relation == SSCRomanceOldRelation.Spouse ? 30 : 25) * 60000;

        /// <summary>返回是否允许替换；有效期由记忆合并入口处理，零值绝不进入。</summary>
        public static bool Replace(int oldValue, int newValue, bool loss)
            => newValue != 0 && (loss ? newValue <= oldValue : oldValue < 0 ? newValue <= oldValue : newValue < 0 || newValue >= oldValue);
    }
}
