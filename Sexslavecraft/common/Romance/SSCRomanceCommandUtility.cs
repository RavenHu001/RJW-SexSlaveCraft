// AI 辅助生成
using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>原菜单/预览的只读上下文，目标变化沿用原执行者凭据；点击不重新拍摄执行者身份。</summary>
    public sealed class SSCRomanceCommandContext
    {
        internal readonly SSCRomanceEditContext actor;
        internal readonly object targetToken;
        internal readonly Game game;
        internal bool consumed;
        public Pawn Actor => actor.Pawn;
        public Pawn Target { get; }
        public SSCRomanceAction Action { get; }
        /// <summary>构造只捕获显示身份，绝不调用会维护授权世代的 TryAuthorize。</summary>
        internal SSCRomanceCommandContext(SSCRomanceEditContext actor, Pawn target, SSCRomanceAction action, Game game, object originalTargetToken = null)
        { this.actor = actor; Target = target; Action = action; targetToken = originalTargetToken ?? SSCPersonalityRuntimeUtility.Capture(target); this.game = game; }
        /// <summary>选择新的目标/行为时保留最初执行者上下文，旧窗口不能追随新人格续权。</summary>
        public SSCRomanceCommandContext ForTarget(Pawn target, SSCRomanceAction action)
            // 同一目标切换行为不能重新拍摄新人格；只有选择不同目标才捕获该目标的显示凭据。
            => new SSCRomanceCommandContext(actor, target, action, game, target == Target ? targetToken : null) { consumed = consumed };
    }
    /// <summary>命令查询与提交唯一入口；不使用原版菜单或自主权重作为点名资格。</summary>
    public static class SSCRomanceCommandUtility
    {
        // 本构建的原版近亲检查为 private，缓存精确签名委托；版本不符时拒绝该类命令。
        private static readonly Func<Pawn, Pawn, bool> incest = AccessTools.Method(typeof(RelationsUtility), "Incestuous", new[] { typeof(Pawn), typeof(Pawn) })?
            .CreateDelegate(typeof(Func<Pawn, Pawn, bool>)) as Func<Pawn, Pawn, bool>;
        /// <summary>打开窗口时捕获原执行者；目标尚未选择时也不写配置或授权。</summary>
        public static SSCRomanceCommandContext Capture(Pawn actor, Pawn target = null, SSCRomanceAction action = SSCRomanceAction.Romance)
            => new SSCRomanceCommandContext(new SSCRomanceEditContext(SSCRomanceResolver.Resolve(actor)), target, action, Verse.Current.Game);
        /// <summary>分离展示资格与目标资格，第一阶段和失效绑定也能在面板得到具体门槛。</summary>
        public static string ActorFailure(Pawn pawn)
        {
            var state = SSCRomanceResolver.Resolve(pawn);
            if (!state.Applicable) return state.Reason.ToString();
            if (pawn.health == null || pawn.Dead) return "InvalidPawn";
            if (Faction.OfPlayer == null || pawn.Faction != Faction.OfPlayer) return "PlayerOnly";
            if (state.Stage < 4) return "CommandStage";
            if (!SSCRomanceCommandHooks.Ready) return "HooksUnavailable";
            if (SSCRomanceCommandGameComponent.Current?.Ready != true) return "Loading";
            return null;
        }
        /// <summary>原引用、组件、链、人格和存档均须一致；配置开关变化不重新签发旧菜单权限。</summary>
        private static bool IsCurrent(SSCRomanceCommandContext context)
        {
            if (context == null || context.consumed || context.game != Verse.Current.Game || context.Actor == null) return false;
            var original = context.actor; var state = SSCRomanceResolver.Resolve(context.Actor);
            return state.Comp == original.Comp && state.Chain == original.Chain && state.Master == original.Master &&
                state.Comp?.romanceAuthorizationId == original.AuthorizationId &&
                SSCPersonalityRuntimeUtility.IsCurrent(context.Actor, original.Token) &&
                (context.Target == null || SSCPersonalityRuntimeUtility.IsCurrent(context.Target, context.targetToken));
        }
        /// <summary>检查必要角色/关系及任务条件，保留普通零成功率目标和被动必拒目标。</summary>
        public static string Failure(SSCRomanceCommandContext context)
        {
            if (!IsCurrent(context)) return "StaleContext";
            string reason = ActorFailure(context.Actor); if (reason != null) return reason;
            var eligibility = SSCRomanceEligibility.Check(context.Actor, context.Target, context.Action);
            if (eligibility != SSCRomanceEligibilityReason.None) return eligibility.ToString();
            return TaskFailure(context.Actor, context.Target, context.Action);
        }
        /// <summary>命令必须能形成真实社交；不越过年龄、近亲、精神状态、征召或地图路径条件。</summary>
        public static string TaskFailure(Pawn actor, Pawn target, SSCRomanceAction action)
        {
            if (SSCPersonalityRuntimeUtility.IsMutating(target)) return "Mutating";
            if (!actor.Spawned || !target.Spawned || actor.Map != target.Map) return "SameMapRequired";
            if (actor.jobs == null || actor.pather == null || actor.interactions == null || actor.Downed || target.Downed ||
                actor.Drafted || target.Drafted || actor.InMentalState || target.InMentalState) return "UnableToInteract";
            if (action != SSCRomanceAction.Breakup && (actor.ageTracker == null || target.ageTracker == null ||
                actor.ageTracker.AgeBiologicalYearsFloat < 16f || target.ageTracker.AgeBiologicalYearsFloat < 16f || incest == null || incest(actor, target))) return "InvalidRomancePair";
            if (target.IsForbidden(actor) || !actor.CanReach(target, PathEndMode.Touch, Danger.Deadly)) return "Unreachable";
            return null;
        }
        /// <summary>先消费原提交上下文再登记/调度；重复点击或调度回调不能建立第二个请求。</summary>
        public static bool TrySubmit(SSCRomanceCommandContext context, out SSCRomanceCommandRecord record, out string reason)
        {
            record = null; reason = Failure(context); if (reason != null) return false;
            context.consumed = true;
            if (!SSCRomanceLifecycle.TryAuthorize(context.Actor, context.Target, context.Action, out var proof))
            { reason = "InvalidAuthorization"; return false; }
            var job = JobMaker.MakeJob(SSCRomanceDefOf.SSC_RomanceCommand, context.Target);
            job.interaction = Interaction(context.Action);
            if (!proof.IsCurrent(context.Actor, context.Target, context.Action) || !SSCPersonalityRuntimeUtility.IsCurrent(context.Target, context.targetToken))
            { reason = "InvalidAuthorization"; return false; }
            var store = SSCRomanceCommandGameComponent.Current;
            record = store.Register(job, proof);
            if (record == null) { reason = "DuplicateCommand"; return false; }
            // TryTakeOrderedJob 可同步开始，也可因已有相同任务直接 true；必须检查原 Job 是否实际被接纳。
            try
            {
                bool scheduled = context.Actor.jobs.TryTakeOrderedJob(job);
                if (scheduled && (record.Terminal || SSCRomanceCommandGameComponent.Jobs(context.Actor).Contains(job))) return true;
                SSCRomanceCommandGameComponent.Cancel(record, "JobRejected"); reason = "JobRejected"; return false;
            }
            catch
            { SSCRomanceCommandGameComponent.Cancel(record, "JobRejected"); throw; }
            finally { record.scheduling = false; }
        }
        /// <summary>只映射三种已知原生 Def，非法行为不默认为求爱。</summary>
        public static InteractionDef Interaction(SSCRomanceAction action) => action == SSCRomanceAction.Romance ? InteractionDefOf.RomanceAttempt :
            action == SSCRomanceAction.Proposal ? InteractionDefOf.MarriageProposal : action == SSCRomanceAction.Breakup ? SSCRomanceDefOf.Breakup : null;
        /// <summary>所有面板/命令窗口共用原因文本，查询不发送反馈。</summary>
        public static string Explain(string reason) => reason == null ? null : ("SSC_Romance_" + reason).Translate();
    }
}
