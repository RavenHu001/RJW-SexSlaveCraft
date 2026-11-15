// AI 辅助生成。命令栏只缓存当前单选来源的关系视图及控件。
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public sealed partial class SSCBondRelationGameComponent
    {
        internal SSCBondOverviewSession Overview { get; private set; }
        private SSCBondOverviewView gizmoView;
        private Gizmo_SSCBondOverview slavesGizmo, trainerGizmo;
        private Command_SSCBondSelection selectAllGizmo, masterGizmo;
        partial void InitializeOverview(Game game)
        { Overview = new SSCBondOverviewSession(game, () => Time.realtimeSinceStartup, report: ex => Log.Error("[SSC] Bond overview query: " + ex)); }
        internal Gizmo GetOverviewGizmo(Pawn pawn)
        { foreach (Gizmo gizmo in GetOverviewGizmos(pawn)) return gizmo; return null; }
        internal IEnumerable<Gizmo> GetOverviewGizmos(Pawn pawn)
        {
            if (!SSCBondOverviewAccess.IsCardSource(pawn)) yield break;
            SSCBondOverviewView view = Overview.ObserveCard(pawn, Find.CurrentMap);
            SetGizmoView(view);
            if (!view.ShowCard) yield break;
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            // 空方向不占位置；异常双向数据分别呈现，调教员永远不混进绑定计数或主人头像。
            if (snapshot?.TotalSlaveCount > 0 || (snapshot?.Identity ?? comp.pawnIdentity) == PawnIdentity.Master)
            {
                yield return slavesGizmo ?? (slavesGizmo = new Gizmo_SSCBondOverview(Overview));
                yield return selectAllGizmo ?? (selectAllGizmo = new Command_SSCBondSelection(Overview, selectMaster: false));
            }
            if (snapshot?.Master != null || snapshot?.MasterState == SSCBondOverviewMasterState.InvalidTarget ||
                snapshot == null && SSCBondUtility.GetChain(pawn) != null)
                yield return masterGizmo ?? (masterGizmo = new Command_SSCBondSelection(Overview, selectMaster: true));
            if (SSCIdentityUtility.IsSexSlave(pawn) ||
#if SSC_RW17
                comp.AssignedTrainerCount != 0)
#else
                comp.selectedTrainer != null)
#endif
                yield return trainerGizmo ?? (trainerGizmo = new Gizmo_SSCBondOverview(Overview, SSCBondOverviewPanel.Trainer));
        }
        public override void GameComponentUpdate()
        {
            slavesGizmo?.ClearPending(); selectAllGizmo?.ClearPending(); masterGizmo?.ClearPending(); trainerGizmo?.ClearPending();
            Overview.Update(Time.frameCount, SSCBondOverviewAccess.IsCardSource(Overview.Card?.Source), Find.CurrentMap);
            SetGizmoView(Overview.Card);
        }

        private void SetGizmoView(SSCBondOverviewView view)
        {
            if (ReferenceEquals(gizmoView, view)) return;
            slavesGizmo?.Clear(); selectAllGizmo?.Clear(); masterGizmo?.Clear(); trainerGizmo?.Clear();
            slavesGizmo = trainerGizmo = null;
            selectAllGizmo = masterGizmo = null;
            gizmoView = view;
        }
    }
    internal static class SSCBondOverviewAccess
    {
        internal static bool IsCurrentCard(SSCBondOverviewSession session, SSCBondOverviewView view)
            => view != null && ReferenceEquals(session.Owner, Current.Game) && ReferenceEquals(session.Card, view) &&
                ReferenceEquals(view.Map, Find.CurrentMap) && IsCardSource(view.Source) && view.ShowCard;

        internal static bool IsCardSource(Pawn pawn)
            => Current.Game != null && Current.ProgramState == ProgramState.Playing && Scribe.mode == LoadSaveMode.Inactive &&
                SSCBondOverviewUtility.IsValidPawn(pawn) && !pawn.Dead && pawn.Spawned && ReferenceEquals(pawn.Map, Find.CurrentMap) &&
                pawn.TryGetComp<CompSexSlaveTraining>() != null && Find.Selector?.NumSelected == 1 &&
                ReferenceEquals(Find.Selector.SingleSelectedThing, pawn);
    }
}
