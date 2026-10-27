// AI 辅助生成：任务报告和一次性反馈共用行为分类；查询不领取命令或结算互动。
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>只用于展示的命令类别；Breakup 表示尚无可靠原关系时的通用结束关系描述。</summary>
    public enum SSCRomanceCommandDisplay { None, Romance, Proposal, EndLover, EndFiance, Divorce, Breakup }

    public static class SSCRomanceCommandPresentation
    {
        /// <summary>调度前保存展示类别，队列与读档不因后来关系变化误改原命令文字；此值不参与执行资格。</summary>
        internal static SSCRomanceCommandDisplay Capture(SSCRomanceCommandStamp stamp)
            => From(stamp.Action, SSCRomanceMoodEvents.Relation(stamp.Actor, stamp.Target));

        /// <summary>行为与原关系唯一映射；无可靠结束关系类型时使用通用文本，不猜测离婚。</summary>
        private static SSCRomanceCommandDisplay From(SSCRomanceAction action, SSCRomanceOldRelation before)
            => action == SSCRomanceAction.Romance ? SSCRomanceCommandDisplay.Romance :
               action == SSCRomanceAction.Proposal ? SSCRomanceCommandDisplay.Proposal :
               action != SSCRomanceAction.Breakup ? SSCRomanceCommandDisplay.None :
               before == SSCRomanceOldRelation.Spouse ? SSCRomanceCommandDisplay.Divorce :
               before == SSCRomanceOldRelation.Fiance ? SSCRomanceCommandDisplay.EndFiance :
               before == SSCRomanceOldRelation.Lover ? SSCRomanceCommandDisplay.EndLover : SSCRomanceCommandDisplay.Breakup;

        /// <summary>完成事实优先表达实际行为；持久展示字段缺失或不匹配时仅按原 Job 行为回退。</summary>
        public static SSCRomanceCommandDisplay Kind(SSCRomanceCommandRecord record, Job job = null)
        {
            if (record?.ConfirmedFact != null)
                return From(record.Stamp.Action, record.ConfirmedFact.Before);
            var action = record?.Stamp?.Action ?? (SSCRomanceInteractionUtility.TryGetAction(job?.interaction, out var a) ? a : (SSCRomanceAction)(-1));
            var saved = record?.displayKind ?? SSCRomanceCommandDisplay.None;
            if (action == SSCRomanceAction.Romance && saved == SSCRomanceCommandDisplay.Romance ||
                action == SSCRomanceAction.Proposal && saved == SSCRomanceCommandDisplay.Proposal ||
                action == SSCRomanceAction.Breakup && saved >= SSCRomanceCommandDisplay.EndLover && saved <= SSCRomanceCommandDisplay.Breakup) return saved;
            return From(action, SSCRomanceOldRelation.None);
        }

        /// <summary>返回原生 TargetA 占位模板，由驱动沿原版 ReportStringProcessed 解析目标。</summary>
        public static string Report(SSCRomanceCommandRecord record, Job job)
        {
            var kind = Kind(record, job);
            return kind == SSCRomanceCommandDisplay.None ? job?.def?.reportString : ("SSC_Romance_Report_" + kind).Translate().ToString();
        }

        /// <summary>只格式化既有终态；拒婚附带分手必须有共用事实见证，不能用泛化 Rejected 推断。</summary>
        public static string ResultText(SSCRomanceCommandRecord record)
        {
            // 展示自身也拒绝不一致的完成数据，不依赖调用者必定已运行读档对账。
            string outcome = record.Status == SSCRomanceCommandStatus.Completed && !record.FactMatchesOutcome ?
                SSCRomanceCommandOutcome.Unconfirmed.ToString() : record.Outcome.ToString();
            if (record.Status == SSCRomanceCommandStatus.Completed && record.Outcome == SSCRomanceCommandOutcome.Rejected &&
                record.FactMatchesOutcome && record.ConfirmedFact.Kind == SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated)
                outcome = "RejectedAndSeparated";
            var action = ("SSC_Romance_CommandAction_" + Kind(record)).Translate();
            return ("SSC_Romance_Result_" + outcome).Translate(record.Stamp.Actor.LabelShort,
                record.Stamp.Target?.LabelShort ?? "?", action).ToString();
        }
    }
}
