// AI 辅助生成
using LudeonTK;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal static class DebugActions_SSCPna
    {
        // 加量动作复用同一个 Debug 来源入口；达满按 P2 建依赖，但不额外补旧需求。
        [DebugAction("SSC", "PNA P1: add ordinary dose (+8%)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddOrdinary() => Add(SSCPnaMath.OrdinaryExposureDose);

        [DebugAction("SSC", "PNA P1: add 20% test dose", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddTwenty() => Add(0.20f);

        [DebugAction("SSC", "PNA P1: add 70% test dose", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddSeventy() => Add(0.70f);

        [DebugAction("SSC", "PNA P2: fill load (can create dependence)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Fill() => Add(1f);

        [DebugAction("SSC", "PNA P1: clear load", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Clear()
        {
            // 清除负荷只用于隔离恢复条件，保留依赖以便继续观察戒断／恢复。
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
