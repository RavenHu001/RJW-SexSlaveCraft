// AI 辅助生成。沿用既有 GameComponent，仅新增不保存的 UI 会话。
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public sealed partial class SSCBondRelationGameComponent
    {
        internal SSCBondOverviewSession Overview { get; private set; }
        private Gizmo_SSCBondOverview overviewGizmo;

        partial void InitializeOverview(Game game)
        {
            // 构造与读档时都不查世界；原 FinalizeInit 的玩法维护仍走原来的独立路径。
            Overview = new SSCBondOverviewSession(game, () => Time.realtimeSinceStartup,
                report: ex => Log.Error("[SSC] Bond overview query: " + ex));
        }

        internal Gizmo GetOverviewGizmo(Pawn pawn)
        {
            if (!SSCBondOverviewAccess.IsCardSource(pawn)) return null;
            SSCBondOverviewView view = Overview.ObserveCard(pawn, Find.CurrentMap, Time.frameCount);
            if (!view.ShowCard) return null;
            return overviewGizmo ?? (overviewGizmo = new Gizmo_SSCBondOverview(Overview));
        }

        public override void GameComponentUpdate()
        {
            overviewGizmo?.ClearPending();
            Overview.Update(Time.frameCount, SSCBondOverviewAccess.IsCardSource(Overview.Card?.Source), Find.CurrentMap);
            if (Overview.Card == null)
            {
                // 引擎的 Gizmo 列表可能暂留旧控件；清除控件自己的快照／行引用。
                overviewGizmo?.Clear(); overviewGizmo = null;
            }
        }
    }

    internal static class SSCBondOverviewAccess
    {
        internal static bool IsCardSource(Pawn pawn)
            => Current.Game != null && Current.ProgramState == ProgramState.Playing && Scribe.mode == LoadSaveMode.Inactive &&
                SSCBondOverviewUtility.IsValidPawn(pawn) && !pawn.Dead && pawn.Spawned && ReferenceEquals(pawn.Map, Find.CurrentMap) &&
                pawn.TryGetComp<CompSexSlaveTraining>() != null && Find.Selector?.NumSelected == 1 &&
                ReferenceEquals(Find.Selector.SingleSelectedThing, pawn);
    }
}
