// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版分手 Def 没有对应 InteractionDefOf 字段；独立 DefOf 只引用 Core 原定义，不新增或复制 Def。</summary>
    [DefOf]
    public static class SSCRomanceDefOf
    {
        public static InteractionDef Breakup;
        /// <summary>沿游戏 DefOf 生命周期解析原版 Breakup，不在高频互动中按字符串搜索数据库。</summary>
        static SSCRomanceDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SSCRomanceDefOf));
    }

    /// <summary>自然、手动和 RJW 共用的实际互动适配；查询不写配置、不发记忆、不建立关系。</summary>
    public static class SSCRomanceInteractionUtility
    {
        /// <summary>只接管三个已取证的原版 Def；不以标签、名称片段或未知 worker 猜测行为。</summary>
        public static bool TryGetAction(InteractionDef interaction, out SSCRomanceAction action)
        {
            action = default;
            if (interaction == null) return false;
            if (interaction == InteractionDefOf.RomanceAttempt) return true;
            if (interaction == InteractionDefOf.MarriageProposal) { action = SSCRomanceAction.Proposal; return true; }
            if (interaction == SSCRomanceDefOf.Breakup) { action = SSCRomanceAction.Breakup; return true; }
            return false;
        }

        /// <summary>共同入口仅判断发起与必要条件；接收方必拒不能使互动提前取消，普通入口不取得命令授权。</summary>
        public static bool CanStart(Pawn actor, Pawn target, SSCRomanceAction action)
            => SSCRomanceEligibility.Check(actor, target, action) == SSCRomanceEligibilityReason.None &&
               SSCRomancePolicy.Evaluate(actor, target, action).CanInitiate;

        /// <summary>供共同入口在副作用前使用当前有效 Def 再检查；普通互动透传，不在查询中创建命令豁免。</summary>
        public static bool CanEnter(Pawn actor, Pawn target, InteractionDef interaction)
            => !TryGetAction(interaction, out var action) || CanStart(actor, target, action);

        /// <summary>为玩家主动点击提供原因；高频 AI／权重查询不弹消息，不依赖界面禁用来确保执行安全。</summary>
        public static string Rejection(Pawn actor, Pawn target, SSCRomanceAction action)
        {
            var eligibility = SSCRomanceEligibility.Check(actor, target, action);
            if (eligibility != SSCRomanceEligibilityReason.None) return ("SSC_Romance_" + eligibility).Translate();
            var decision = SSCRomancePolicy.Evaluate(actor, target, action);
            return decision.CanInitiate ? null : ("SSC_Romance_" + decision.InitiatorReason).Translate();
        }
    }
}
