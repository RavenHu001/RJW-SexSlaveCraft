// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public static class SSCRomanceAcceptanceUtility
    {
        /// <summary>只覆盖本次已发生互动的接受结果；原概率计算和一次随机取样保留，默认结果逐值透传。</summary>
        public static bool Resolve(bool vanillaAccepted, Pawn initiator, Pawn recipient, SSCRomanceAction action)
        {
            // 不修改公共 SuccessChance：它也参与菜单资格，改成零会把被动必拒误变为不可选目标。
            // 原版使用 Rand.Value < chance，且随机值可等于 1；覆盖 bool 才能严格保证接受。
            var reception = SSCRomancePolicy.Evaluate(initiator, recipient, action).Reception;
            return reception == SSCRomanceReception.ForceAccept ||
                (reception != SSCRomanceReception.ForceReject && vanillaAccepted);
        }
    }
}
