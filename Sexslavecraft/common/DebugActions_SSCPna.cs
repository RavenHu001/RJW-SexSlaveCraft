// AI 辅助生成
using LudeonTK;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal static class DebugActions_SSCPna
    {
        // 调试加量复用共享入口；普通身体达满可建成瘾，但不加深已有成瘾或补充旧需求。
        [DebugAction("SSC", "PNA: add 8% load", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddOrdinary() => Add(SSCPnaMath.OrdinaryExposureDose);

        [DebugAction("SSC", "PNA: add 20% load", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddTwenty() => Add(0.20f);

        [DebugAction("SSC", "PNA: add 70% load", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddSeventy() => Add(0.70f);

        [DebugAction("SSC", "PNA: fill load (can create addiction)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Fill() => Add(1f);

        [DebugAction("SSC", "PNA: clear load (keep addiction)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Clear()
        {
            // 仅清除身体负荷，保留成瘾以便继续观察原版戒断／恢复。
            Pawn pawn = Selected();
            if (pawn != null) SSCPnaExposureUtility.ClearLoad(pawn);
        }

        private static Pawn Selected()
        {
            // 统一处理无选中角色的提示，使各调试动作使用相同准入反馈。
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) Messages.Message("SSC_Pna_SelectPawn".Translate(), MessageTypeDefOf.RejectInput);
            return pawn;
        }

        private static void Add(float dose)
        {
            // 只负责读取选中角色、传入剂量；负荷、依赖与锁链分流由核心决定。
            Pawn pawn = Selected();
            if (pawn == null) return;
            SSCPnaExposureResult result = SSCPnaExposureUtility.ApplyExposure(pawn, dose, SSCPnaExposureSource.Debug);
            // 显示最终实际负荷及分流结果，避免用请求剂量替代上限钳制后的状态。
            string key = !result.Accepted ? "SSC_Pna_InvalidTarget" : result.Rooted ? "SSC_Pna_Rooted" : "SSC_Pna_DebugResult";
            Messages.Message(key.Translate((result.FinalLoad * 100f).ToString("F1")), pawn,
                result.Accepted ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
        }
    }
}
