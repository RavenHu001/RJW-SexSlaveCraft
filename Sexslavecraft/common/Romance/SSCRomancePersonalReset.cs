// AI 辅助生成：恢复十一项个人默认，原编辑凭据与心情刷新复用，不恢复克隆或任务状态。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次个人重置请求，额外固定原始标量以拒绝外部直接写字段后的旧点击。</summary>
    public sealed class SSCRomanceResetContext
    {
        internal SSCRomanceEditContext Edit;
        internal SSCRomanceConfig Saved;
        internal bool Authorized;
    }
    /// <summary>标题按钮只读视图；缺配置已是模块默认，不因绘制创建配置。</summary>
    public sealed class SSCRomanceResetView
    {
        public bool CanReset { get; internal set; }
        public string Reason { get; internal set; }
        public SSCRomanceResetContext Context { get; internal set; }
    }
    public static partial class SSCRomancePreferences
    {
        /// <summary>使用二阶个人编辑资格；重置整份偏好不会提前开放三阶规则。</summary>
        public static SSCRomanceResetView GetResetView(Pawn pawn)
        {
            var state = SSCRomanceResolver.Resolve(pawn);
            var context = new SSCRomanceResetContext { Edit = new SSCRomanceEditContext(state), Saved = state.Comp?.romanceConfig?.Copy() };
            var view = new SSCRomanceResetView { Context = context, Reason = "Unavailable" };
            if (ChangeFailure(state, SSCRomanceSetting.InitiateRomance) != SSCRomanceReason.None) return view;
            if (!ResetContextCurrent(pawn, state, context)) { view.Reason = "Stale"; return view; }
            view.Reason = state.Comp.romanceConfig?.IsDefault != false ? "Default" : null;
            view.CanReset = view.Reason == null;
            context.Authorized = view.CanReset;
            return view;
        }

        /// <summary>一次替换十一项默认，仅通知持续思想；不循环单项 setter，不清历史或重发命令。</summary>
        public static bool TryReset(Pawn pawn, SSCRomanceResetContext context, out string reason)
        {
            reason = "Stale";
            // 灰显时捕获的上下文没有写权限；资格恢复后必须由界面取得新请求。
            if (context?.Authorized != true) return false;
            var state = SSCRomanceResolver.Resolve(pawn);
            if (!ResetContextCurrent(pawn, state, context)) return false;
            if (ChangeFailure(state, SSCRomanceSetting.InitiateRomance) != SSCRomanceReason.None) { reason = "Unavailable"; return false; }
            if (!ResetContextCurrent(pawn, state, context)) return false;
            if (state.Comp.romanceConfig?.IsDefault != false) { reason = "Default"; return false; }
            state.Comp.romanceConfig = new SSCRomanceConfig();
            SSCRomanceMoodRefresh.Notify(pawn, "ResetDefaults");
            reason = null;
            return true;
        }

        /// <summary>复用原编辑身份／绑定／授权，并逐值验证配置；不向旧点击签发新 token。</summary>
        private static bool ResetContextCurrent(Pawn pawn, SSCRomanceState state, SSCRomanceResetContext reset)
        {
            var c = reset?.Edit;
            if (c == null || pawn == null || c.Pawn != pawn || c.Comp == null || state.Comp != c.Comp ||
                c.Chain?.LinkedPawn != c.Master || state.Chain != c.Chain ||
                state.Master != c.Master || state.Comp.romanceConfig != c.Config || state.Comp.romanceAuthorizationId != c.AuthorizationId ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, c.Token)) return false;
            var saved = reset.Saved;
            if (c.Config == null || saved == null) return c.Config == saved;
            if (c.Config.version != saved.version) return false;
            foreach (SSCRomanceSetting setting in System.Enum.GetValues(typeof(SSCRomanceSetting)))
                if (c.Config.Get(setting) != saved.Get(setting)) return false;
            return true;
        }
    }
}
