// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>单次只读解析，不补身份、不修链、不补默认配置；死去或离图的主人仍是绑定主人。</summary>
    public sealed class SSCRomanceState
    {
        public Pawn Pawn { get; internal set; }
        public CompSexSlaveTraining Comp { get; internal set; }
        public Hediff_ChainOfSexSlave Chain { get; internal set; }
        public Pawn Master { get; internal set; }
        public int Stage { get; internal set; }
        public SSCRomanceReason Reason { get; internal set; }
        public bool Applicable => Reason == SSCRomanceReason.None;
        /// <summary>读取保存偏好而非生效结果；缺配置仍按许可允许、附加开关关闭解释。</summary>
        public bool GetSaved(SSCRomanceSetting setting) => Comp?.romanceConfig?.Get(setting) ??
            SSCRomanceResolver.DefaultEnabled(setting);
    }

    public static class SSCRomanceResolver
    {
        /// <summary>拒绝未知枚举值，避免把未注册条目当成默认允许的许可。</summary>
        public static bool IsKnown(SSCRomanceSetting setting) => setting >= SSCRomanceSetting.InitiateRomance && setting <= SSCRomanceSetting.AlwaysAcceptOthers;
        /// <summary>明确主动二阶、被动三阶；不依赖枚举排列，三个目标许可与主动规则同步解锁。</summary>
        public static int RequiredStage(SSCRomanceSetting setting)
        {
            switch (setting)
            {
                case SSCRomanceSetting.InitiateRomance:
                case SSCRomanceSetting.InitiateProposal:
                case SSCRomanceSetting.InitiateBreakup:
                case SSCRomanceSetting.InitiateToMaster:
                case SSCRomanceSetting.InitiateToSexSlave:
                case SSCRomanceSetting.InitiateToOthers: return 2;
                case SSCRomanceSetting.AcceptRomance:
                case SSCRomanceSetting.AcceptProposal:
                case SSCRomanceSetting.AlwaysAcceptMaster:
                case SSCRomanceSetting.AlwaysAcceptSexSlave:
                case SSCRomanceSetting.AlwaysAcceptOthers: return 3;
                default: return int.MaxValue;
            }
        }
        /// <summary>行为许可及三个目标许可默认允许，三个强制接受开关默认关闭。</summary>
        public static bool DefaultEnabled(SSCRomanceSetting setting) => IsKnown(setting) &&
            (setting <= SSCRomanceSetting.AcceptProposal || setting == SSCRomanceSetting.InitiateToMaster ||
             setting == SSCRomanceSetting.InitiateToSexSlave || setting == SSCRomanceSetting.InitiateToOthers);
        /// <summary>目标互斥分类：自己的实际主人优先，其次其他 SSC 性奴，其余均为其他人；不额外限制阵营或法律身份。</summary>
        public static SSCRomanceSetting TargetSetting(Pawn target, Pawn master)
            => target != null && target == master ? SSCRomanceSetting.InitiateToMaster :
                SSCIdentityUtility.IsSexSlave(target) ? SSCRomanceSetting.InitiateToSexSlave : SSCRomanceSetting.InitiateToOthers;
        /// <summary>高频读取不写载体；人格替换及加载期暂停本模块的覆盖，实际互动仍须检查可执行性。</summary>
        public static SSCRomanceState Resolve(Pawn pawn)
        {
            // 同次解析复用从属侧锁链；指定调教员和反向缰绳不能替代实际主人。
            var state = new SSCRomanceState { Pawn = pawn, Comp = pawn?.TryGetComp<CompSexSlaveTraining>() };
            state.Chain = SSCBondUtility.GetChain(pawn);
            state.Master = state.Chain?.LinkedPawn;
            state.Stage = SSCIdentityUtility.GetSexSlaveStageFromChain(state.Chain);
            // 人格与加载保护先于配置解释，防止半恢复状态触发保证接受或提前认领身份。
            // 此处不以主人死亡／离图否定绑定；每次互动的存活、位置等由执行入口另查。
            if (pawn == null || pawn.Destroyed || pawn.Discarded || state.Comp == null || pawn.RaceProps?.Humanlike != true)
                state.Reason = SSCRomanceReason.InvalidPawn;
            else if (Scribe.mode != LoadSaveMode.Inactive) state.Reason = SSCRomanceReason.Loading;
            else if (SSCPersonalityRuntimeUtility.IsMutating(pawn)) state.Reason = SSCRomanceReason.Mutating;
            else if (SSCMasterProgressionUtility.IsHollow(pawn)) state.Reason = SSCRomanceReason.Hollow;
            else if (!SSCIdentityUtility.IsSexSlave(pawn)) state.Reason = SSCRomanceReason.Identity;
            else if (state.Master == null || state.Master == pawn || state.Master.Destroyed || state.Master.Discarded ||
                pawn.health?.hediffSet?.hediffs.Contains(state.Chain) != true) state.Reason = SSCRomanceReason.NoBinding;
            else if (state.Comp.romanceConfig?.IsSupported == false) state.Reason = SSCRomanceReason.UnsupportedConfig;
            return state;
        }
    }
}
