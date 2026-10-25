// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>人格所属的原始选择；阶段、主人和命令授权不属于这份快照。</summary>
    public sealed class SSCRomanceConfig : IExposable
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        // 保存原始选择；未解锁或解绑不回写，重新取得资格后仍使用原选择。
        public bool allowInitiateRomance = true, allowInitiateProposal = true, allowInitiateBreakup = true;
        public bool allowAcceptRomance = true, allowAcceptProposal = true;
        public bool alwaysAcceptMaster;
        // 接收端个人保证；缺字段默认关闭，不与主动“其他人”目标许可共用保存值。
        public bool alwaysAcceptOthers;
        public bool allowInitiateToMaster = true, allowInitiateToOthers = true;
        public bool allowInitiateToSexSlave = true, alwaysAcceptSexSlave;
        public bool IsSupported => version == CurrentVersion;
        public bool IsDefault => IsSupported && allowInitiateRomance && allowInitiateProposal &&
            allowInitiateBreakup && allowAcceptRomance && allowAcceptProposal && allowInitiateToMaster && allowInitiateToOthers && !alwaysAcceptMaster && allowInitiateToSexSlave && !alwaysAcceptSexSlave && !alwaysAcceptOthers;

        /// <summary>十一个标量逐值复制；未知版本保持版本及可读取字段，不冒充当前格式。</summary>
        public SSCRomanceConfig Copy() => (SSCRomanceConfig)MemberwiseClone();
        /// <summary>读取指定保存值；未知条目返回 false，调用方仍须独立检查条目合法性。</summary>
        public bool Get(SSCRomanceSetting setting)
        {
            switch (setting)
            {
                case SSCRomanceSetting.InitiateRomance: return allowInitiateRomance;
                case SSCRomanceSetting.InitiateProposal: return allowInitiateProposal;
                case SSCRomanceSetting.InitiateBreakup: return allowInitiateBreakup;
                case SSCRomanceSetting.AcceptRomance: return allowAcceptRomance;
                case SSCRomanceSetting.AcceptProposal: return allowAcceptProposal;
                case SSCRomanceSetting.InitiateToMaster: return allowInitiateToMaster;
                case SSCRomanceSetting.InitiateToOthers: return allowInitiateToOthers;
                case SSCRomanceSetting.AlwaysAcceptMaster: return alwaysAcceptMaster;
                case SSCRomanceSetting.InitiateToSexSlave: return allowInitiateToSexSlave;
                case SSCRomanceSetting.AlwaysAcceptSexSlave: return alwaysAcceptSexSlave;
                case SSCRomanceSetting.AlwaysAcceptOthers: return alwaysAcceptOthers;
                default: return false;
            }
        }
        /// <summary>仅供通过上下文和资格校验的提交入口写入副本，不修改阶段或关系。</summary>
        internal void Set(SSCRomanceSetting setting, bool value)
        {
            switch (setting)
            {
                case SSCRomanceSetting.InitiateRomance: allowInitiateRomance = value; break;
                case SSCRomanceSetting.InitiateProposal: allowInitiateProposal = value; break;
                case SSCRomanceSetting.InitiateBreakup: allowInitiateBreakup = value; break;
                case SSCRomanceSetting.AcceptRomance: allowAcceptRomance = value; break;
                case SSCRomanceSetting.AcceptProposal: allowAcceptProposal = value; break;
                case SSCRomanceSetting.InitiateToMaster: allowInitiateToMaster = value; break;
                case SSCRomanceSetting.InitiateToOthers: allowInitiateToOthers = value; break;
                case SSCRomanceSetting.AlwaysAcceptMaster: alwaysAcceptMaster = value; break;
                case SSCRomanceSetting.InitiateToSexSlave: allowInitiateToSexSlave = value; break;
                case SSCRomanceSetting.AlwaysAcceptSexSlave: alwaysAcceptSexSlave = value; break;
                case SSCRomanceSetting.AlwaysAcceptOthers: alwaysAcceptOthers = value; break;
            }
        }
        /// <summary>缺失许可为允许，显式 false 正常保存；缺失附加开关为关闭。</summary>
        public void ExposeData()
        {
            // 保留未知版本及已知字段，交给上层拒绝有损迁移，不在读写过程中擅自升级。
            Scribe_Values.Look(ref version, "version", CurrentVersion);
            Scribe_Values.Look(ref allowInitiateRomance, "allowInitiateRomance", true);
            Scribe_Values.Look(ref allowInitiateProposal, "allowInitiateProposal", true);
            Scribe_Values.Look(ref allowInitiateBreakup, "allowInitiateBreakup", true);
            Scribe_Values.Look(ref allowAcceptRomance, "allowAcceptRomance", true);
            Scribe_Values.Look(ref allowAcceptProposal, "allowAcceptProposal", true);
            // 仅限主人旧字段不读取、不转换；未发布配置改为三个独立目标许可，缺字段全部允许。
            Scribe_Values.Look(ref allowInitiateToMaster, "allowInitiateToMaster", true);
            Scribe_Values.Look(ref allowInitiateToOthers, "allowInitiateToOthers", true);
            Scribe_Values.Look(ref alwaysAcceptMaster, "alwaysAcceptMaster", false);
            // 向旧格式追加字段：旧档允许向性奴发起、不保证接受性奴；人格副本及克隆清理仍复用原流程。
            Scribe_Values.Look(ref allowInitiateToSexSlave, "allowInitiateToSexSlave", true);
            Scribe_Values.Look(ref alwaysAcceptSexSlave, "alwaysAcceptSexSlave", false);
            // 追加标量不改既有格式版本；旧存档／凝胶缺字段关闭，新克隆仍由原清配置入口恢复默认。
            Scribe_Values.Look(ref alwaysAcceptOthers, "alwaysAcceptOthers", false);
        }
    }

    /// <summary>十一项持久选择；未发布的仅主人项已改为主人目标许可，不提供旧语义转换；阶段和默认值按语义解析。</summary>
    public enum SSCRomanceSetting
    { InitiateRomance, InitiateProposal, InitiateBreakup, AcceptRomance, AcceptProposal, InitiateToMaster, AlwaysAcceptMaster, InitiateToSexSlave, AlwaysAcceptSexSlave, InitiateToOthers, AlwaysAcceptOthers }
    /// <summary>实际发起的互动；分手与离婚共用 Breakup，不包含被动分手许可。</summary>
    public enum SSCRomanceAction { Romance, Proposal, Breakup }
    /// <summary>接收端覆盖方式；Vanilla 不保证接受，NotApplicable 表示没有接受判定。</summary>
    public enum SSCRomanceReception { Vanilla, ForceReject, ForceAccept, NotApplicable }
    /// <summary>供入口和界面解释状态的原因；查询本身不发送消息或修复载体。</summary>
    public enum SSCRomanceReason
    {
        None, InvalidPawn, Identity, NoBinding, Stage, Hollow, Mutating, Loading,
        UnsupportedConfig, UnknownSetting, PlayerOnly, StaleContext, RuleDenied, TargetDenied, InvalidAuthorization, SexSlaveTargetDenied
    }
}
