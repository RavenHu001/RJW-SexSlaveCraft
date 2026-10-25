// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>由显示时捕获的原上下文授权提交；组件、人格、绑定和配置替换都会使旧点击失效。</summary>
    public sealed class SSCRomanceEditContext
    {
        internal readonly Pawn Pawn, Master;
        internal readonly CompSexSlaveTraining Comp;
        internal readonly Hediff_ChainOfSexSlave Chain;
        internal readonly SSCRomanceConfig Config;
        internal readonly object Token;
        internal readonly string AuthorizationId;
        /// <summary>捕获显示时的原载体、配置、绑定和人格凭据，点击时不得重新捕获以续权。</summary>
        internal SSCRomanceEditContext(SSCRomanceState state)
        {
            Pawn = state.Pawn; Comp = state.Comp; Chain = state.Chain; Master = state.Master;
            Config = Comp?.romanceConfig; AuthorizationId = Comp?.romanceAuthorizationId;
            Token = SSCPersonalityRuntimeUtility.Capture(Pawn);
        }
    }
    /// <summary>单次显示快照，分别表达保存值、阶段解锁和可修改资格，不另存运行效果。</summary>
    public sealed class SSCRomanceView
    {
        public bool Saved { get; internal set; }
        public bool Unlocked { get; internal set; }
        public bool CanChange { get; internal set; }
        public SSCRomanceReason Reason { get; internal set; }
        public SSCRomanceEditContext Context { get; internal set; }
    }
    public static partial class SSCRomancePreferences
    {
        /// <summary>只读构造显示值与提交上下文；灰显或隐藏不能代替 TrySet 的最终校验。</summary>
        public static SSCRomanceView GetView(Pawn pawn, SSCRomanceSetting setting)
        {
            var state = SSCRomanceResolver.Resolve(pawn);
            var reason = ChangeFailure(state, setting);
            return new SSCRomanceView { Saved = state.GetSaved(setting),
                Unlocked = state.Applicable && state.Stage >= SSCRomanceResolver.RequiredStage(setting),
                CanChange = reason == SSCRomanceReason.None, Reason = reason, Context = new SSCRomanceEditContext(state) };
        }
        /// <summary>校验条目、当前载体和玩家编辑资格；非玩家不能编辑不代表其保存规则失效。</summary>
        private static SSCRomanceReason ChangeFailure(SSCRomanceState state, SSCRomanceSetting setting)
        {
            if (!SSCRomanceResolver.IsKnown(setting)) return SSCRomanceReason.UnknownSetting;
            if (!state.Applicable) return state.Reason;
            if (state.Pawn.Dead) return SSCRomanceReason.InvalidPawn;
            if (Faction.OfPlayer == null || state.Pawn.Faction != Faction.OfPlayer) return SSCRomanceReason.PlayerOnly;
            return state.Stage < SSCRomanceResolver.RequiredStage(setting) ? SSCRomanceReason.Stage : SSCRomanceReason.None;
        }
        /// <summary>点击不重新捕获凭据；提交前复查当前资格，写入新副本令旧视图立即过期。</summary>
        public static bool TrySet(Pawn pawn, SSCRomanceSetting setting, bool value, SSCRomanceEditContext context, out SSCRomanceReason reason)
        {
            var state = SSCRomanceResolver.Resolve(pawn);
            reason = SSCRomanceReason.StaleContext;
            // 先证明请求仍属于原上下文，防止同一 Pawn 换 Comp／人格／绑定后沿用旧点击。
            if (context == null || pawn == null || context.Pawn != pawn || context.Comp == null || state.Comp != context.Comp ||
                state.Chain != context.Chain || state.Master != context.Master || state.Comp.romanceConfig != context.Config ||
                state.Comp.romanceAuthorizationId != context.AuthorizationId || !SSCPersonalityRuntimeUtility.IsCurrent(pawn, context.Token)) return false;
            reason = ChangeFailure(state, setting);
            // 凭据有效仍需检查当前解锁资格；用新对象提交，后续旧视图不能覆盖这次修改。
            if (reason != SSCRomanceReason.None) return false;
            var replacement = state.Comp.romanceConfig?.Copy() ?? new SSCRomanceConfig();
            replacement.Set(setting, value);
            state.Comp.romanceConfig = replacement;
            SSCRomanceMoodRefresh.Notify(pawn);
            return true;
        }
    }
}
