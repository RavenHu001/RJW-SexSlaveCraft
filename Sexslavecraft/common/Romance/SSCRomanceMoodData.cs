// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>综合格式独立编号；不能改动旧 SSCRomanceMoodEvent 的已保存枚举值。</summary>
    public enum SSCRomanceCompositeEvent
    {
        None = 0, ForcedRomanceAccepted = 1, ForcedProposalAccepted = 2,
        ForcedRomanceRejected = 3, ForcedProposalRejected = 4, ForcedProposalRejectedAndSeparated = 5,
        CommandRomanceAccepted = 6, CommandRomanceRejected = 7, CommandProposalAccepted = 8,
        CommandProposalRejected = 9, CommandProposalRejectedAndSeparated = 10,
        CommandEndLover = 11, CommandEndFiance = 12, CommandDivorce = 13
    }

    /// <summary>新三档仅属于综合格式；旧 Opinion 字段仍保留原来的 0／40 分类语义。</summary>
    public enum SSCRomanceOpinionBand { Disliked = 0, General = 1, Close = 2 }
    /// <summary>分组是保存事实，不从当前人物状态或剩余期限反推。</summary>
    public enum SSCRomanceMoodGroup { None = 0, Short = 1, Long = 2 }
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
        public SSCRomanceMoodEvent Kind;
        public SSCRomanceOldRelation Relation;
        public int Stage, Opinion, Value;
        public bool Masochist;
        public float? Corruption;
        public SSCRomanceSetting Rule;
        public bool HasRule;
        public Pawn Master;
        public const int CompositeVersion = 2;
        // 新字段缺省为零：旧记忆不被猜配成综合分支，未知未来版本也不当成当前格式。
        public int DataVersion;
        public string BranchId;
        public SSCRomanceCompositeEvent CompositeEvent;
        public bool HasRawOpinion;
        public int RawOpinion;
        public SSCRomanceOpinionBand OpinionBand;
        public SSCRomanceMoodGroup Group;
        public int NominalDurationTicks;
        /// <summary>只识别本版格式；实际数据完整性由目录查询检查，不把此属性当成发放授权。</summary>
        public bool IsComposite => DataVersion == CompositeVersion;
        /// <summary>值字段独立复制，历史主人保留原引用；不带原人格运行凭据。</summary>
        public SSCRomanceMoodData Copy() => (SSCRomanceMoodData)MemberwiseClone();
        /// <summary>保存新格式身份与标准期限并保留旧字段；可空恶堕仅供旧历史读取，不参与新目录。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref Kind, "kind"); Scribe_Values.Look(ref Relation, "relation");
            Scribe_Values.Look(ref Stage, "stage"); Scribe_Values.Look(ref Opinion, "opinion");
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
            // Scribe 不直接序列化 Nullable；独立存在位保留合法零值及缺需求区别。
            bool has = Corruption.HasValue; float value = Corruption ?? 0;
            Scribe_Values.Look(ref has, "hasCorruption"); Scribe_Values.Look(ref value, "corruption");
            if (Scribe.mode == LoadSaveMode.LoadingVars) Corruption = has ? value : (float?)null;
        }
    }

}
