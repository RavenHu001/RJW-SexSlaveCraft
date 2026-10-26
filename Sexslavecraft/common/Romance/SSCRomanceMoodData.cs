// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>综合事件稳定编号；None 仅表示尚未形成真实事件。</summary>
    public enum SSCRomanceCompositeEvent
    {
        None = 0, ForcedRomanceAccepted = 1, ForcedProposalAccepted = 2,
        ForcedRomanceRejected = 3, ForcedProposalRejected = 4, ForcedProposalRejectedAndSeparated = 5,
        CommandRomanceAccepted = 6, CommandRomanceRejected = 7, CommandProposalAccepted = 8,
        CommandProposalRejected = 9, CommandProposalRejectedAndSeparated = 10,
        CommandEndLover = 11, CommandEndFiance = 12, CommandDivorce = 13
    }

    /// <summary>综合三档；原始好感另存，用于长期资格。</summary>
    public enum SSCRomanceOpinionBand { Disliked = 0, General = 1, Close = 2 }
    /// <summary>只表示短／长期保存载体，不再决定记忆互斥范围。</summary>
    public enum SSCRomanceMoodGroup { None = 0, Short = 1, Long = 2 }
    /// <summary>同对象的五类经历；阶段、特质、好感、正负及期限不另占名额。</summary>
    public enum SSCRomanceSemanticGroup { None = 0, PassiveRomance = 1, CommandRomance = 2, PassiveProposal = 3, CommandProposal = 4, Separation = 5 }

    /// <summary>事件到语义的唯一映射；不读取人物、当前关系或尚未定稿的标签。</summary>
    public static class SSCRomanceMoodSemantics
    {
        /// <summary>十三类事件各映射一次；拒婚附带分手只进入关系结束组。</summary>
        public static SSCRomanceSemanticGroup Of(SSCRomanceCompositeEvent kind)
        {
            switch (kind)
            {
                case SSCRomanceCompositeEvent.ForcedRomanceAccepted:
                case SSCRomanceCompositeEvent.ForcedRomanceRejected: return SSCRomanceSemanticGroup.PassiveRomance;
                case SSCRomanceCompositeEvent.CommandRomanceAccepted:
                case SSCRomanceCompositeEvent.CommandRomanceRejected: return SSCRomanceSemanticGroup.CommandRomance;
                case SSCRomanceCompositeEvent.ForcedProposalAccepted:
                case SSCRomanceCompositeEvent.ForcedProposalRejected: return SSCRomanceSemanticGroup.PassiveProposal;
                case SSCRomanceCompositeEvent.CommandProposalAccepted:
                case SSCRomanceCompositeEvent.CommandProposalRejected: return SSCRomanceSemanticGroup.CommandProposal;
                case SSCRomanceCompositeEvent.ForcedProposalRejectedAndSeparated:
                case SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated:
                case SSCRomanceCompositeEvent.CommandEndLover:
                case SSCRomanceCompositeEvent.CommandEndFiance:
                case SSCRomanceCompositeEvent.CommandDivorce: return SSCRomanceSemanticGroup.Separation;
                default: return SSCRomanceSemanticGroup.None;
            }
        }
    }
    /// <summary>持续思想的当前有效限制；不把三个状态相加，也不虚构目标好感。</summary>
    public enum SSCRomanceControlState { None = 0, Active = 1, Passive = 2, Both = 3 }

    /// <summary>仅 1.7 的单条历史恢复通知；不保存、不授予事件或命令权限，不影响其他记忆类型。</summary>
    internal interface ISSCRomanceHistoricalRestore
    {
        void BeginHistoricalRestore();
        void EndHistoricalRestore();
        void RestoreHistorical(Pawn owner, Pawn other, System.Func<bool> current);
        void CompleteHistoricalRestore(Pawn owner, System.Func<bool> current);
    }

    /// <summary>历史基准快照；普通保存、人格凝胶及原生克隆均复制，不携带授权或待结算请求。</summary>
    public sealed class SSCRomanceMoodData : IExposable
    {
        public SSCRomanceOldRelation Relation;
        public int Stage, Value;
        public bool Masochist;
        public SSCRomanceSetting Rule;
        public bool HasRule;
        public Pawn Master;
        public const int CompositeVersion = 2;
        // 格式标记只校验本方案结构；不迁移或解释未发布旧方案。
        public int DataVersion;
        public string BranchId;
        public SSCRomanceCompositeEvent CompositeEvent;
        public bool HasRawOpinion;
        public int RawOpinion;
        public SSCRomanceOpinionBand OpinionBand;
        public SSCRomanceMoodGroup Group;
        public int NominalDurationTicks;
        // 真实事件确认时分配的保存顺序；不是诊断编号、人格凭据或恢复时的新获得时间。
        public long OccurrenceOrder;
        /// <summary>语义由已保存事件唯一派生，避免额外保存一份可能不一致的分类。</summary>
        public SSCRomanceSemanticGroup SemanticGroup => SSCRomanceMoodSemantics.Of(CompositeEvent);
        /// <summary>只识别本版格式；实际数据完整性由目录查询检查，不把此属性当成发放授权。</summary>
        public bool IsComposite => DataVersion == CompositeVersion;
        /// <summary>值字段独立复制，历史主人保留原引用；不带原人格运行凭据。</summary>
        public SSCRomanceMoodData Copy() => (SSCRomanceMoodData)MemberwiseClone();
        /// <summary>只保存综合方案的冻结事实；授权、调用帧及原版变更账本均不进入存档。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref Relation, "relation"); Scribe_Values.Look(ref Stage, "stage");
            Scribe_Values.Look(ref Value, "value"); Scribe_Values.Look(ref Masochist, "masochist");
            Scribe_Values.Look(ref Rule, "rule");
            Scribe_Values.Look(ref HasRule, "hasRule");
            Scribe_References.Look(ref Master, "master", saveDestroyedThings: true);
            // 标准期限与原生 age／durationTicksOverride 分开保存，避免恢复时缩短已延长的历史。
            Scribe_Values.Look(ref DataVersion, "dataVersion", 0);
            Scribe_Values.Look(ref BranchId, "branchId");
            Scribe_Values.Look(ref CompositeEvent, "compositeEvent");
            Scribe_Values.Look(ref HasRawOpinion, "hasRawOpinion", false);
            Scribe_Values.Look(ref RawOpinion, "rawOpinion", 0);
            Scribe_Values.Look(ref OpinionBand, "opinionBand");
            Scribe_Values.Look(ref Group, "group");
            Scribe_Values.Look(ref NominalDurationTicks, "nominalDurationTicks", 0);
            Scribe_Values.Look(ref OccurrenceOrder, "occurrenceOrder", 0L);
            // 载入人格凝胶等离体历史同样抬高水位，但绝不重新分配事件顺序。
            if (Scribe.mode == LoadSaveMode.PostLoadInit) SSCRomanceMoodOrder.Observe(OccurrenceOrder);
        }
    }

}
