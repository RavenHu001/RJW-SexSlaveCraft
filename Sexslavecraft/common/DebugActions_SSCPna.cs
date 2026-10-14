// AI 辅助生成
using LudeonTK;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal static class DebugActions_SSCPna
    {
        [DebugAction("SSC", "PNA P1: add ordinary dose (+8%)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddOrdinary() => Add(SSCPnaMath.OrdinaryExposureDose);

        [DebugAction("SSC", "PNA P1: add 20% test dose", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddTwenty() => Add(0.20f);

        [DebugAction("SSC", "PNA P1: add 70% test dose", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddSeventy() => Add(0.70f);

        [DebugAction("SSC", "PNA P1: fill load (no new addiction)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Fill() => Add(1f);

        [DebugAction("SSC", "PNA P1: clear load", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Clear()
        {
            Pawn pawn = Selected();
            if (pawn != null) SSCPnaExposureUtility.ClearLoad(pawn);
        }

        private static Pawn Selected()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) Messages.Message("SSC_Pna_SelectPawn".Translate(), MessageTypeDefOf.RejectInput);
            return pawn;
        }

        private static void Add(float dose)
        {
            Pawn pawn = Selected();
            if (pawn == null) return;
            SSCPnaExposureResult result = SSCPnaExposureUtility.ApplyExposure(pawn, dose, SSCPnaExposureSource.Debug);
            string key = !result.Accepted ? "SSC_Pna_InvalidTarget" : result.Rooted ? "SSC_Pna_Rooted" : "SSC_Pna_DebugResult";
            Messages.Message(key.Translate((result.FinalLoad * 100f).ToString("F1")), pawn,
                result.Accepted ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
        }
    }
}
