// AI 辅助生成。三个关系面板共用单一会话；不创建逐角色常驻缓存或保存字段。
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public sealed partial class SSCBondRelationGameComponent
    {
        internal SSCBondOverviewSession Overview { get; private set; }
        private Gizmo_SSCBondOverview slavesGizmo, masterGizmo, trainerGizmo;
        partial void InitializeOverview(Game game)
        { Overview = new SSCBondOverviewSession(game, () => Time.realtimeSinceStartup, report: ex => Log.Error("[SSC] Bond overview query: " + ex)); }
        internal Gizmo GetOverviewGizmo(Pawn pawn)
        { foreach (Gizmo gizmo in GetOverviewGizmos(pawn)) return gizmo; return null; }
        internal IEnumerable<Gizmo> GetOverviewGizmos(Pawn pawn)
        {
            if (!SSCBondOverviewAccess.IsCardSource(pawn)) yield break;
            SSCBondOverviewView view = Overview.ObserveCard(pawn, Find.CurrentMap, Time.frameCount);
            if (!view.ShowCard) yield break;
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            // 空方向不占位置；异常双向数据分别呈现，调教员永远不混进绑定计数或主人头像。
            if (snapshot?.TotalSlaveCount > 0 || (snapshot?.Identity ?? comp.pawnIdentity) == PawnIdentity.Master)
                yield return slavesGizmo ?? (slavesGizmo = new Gizmo_SSCBondOverview(Overview));
            if (snapshot?.Master != null || snapshot?.MasterState == SSCBondOverviewMasterState.InvalidTarget ||
                snapshot == null && SSCBondUtility.GetChain(pawn) != null)
                yield return masterGizmo ?? (masterGizmo = new Gizmo_SSCBondOverview(Overview, SSCBondOverviewPanel.Master));
            if (SSCIdentityUtility.IsSexSlave(pawn) || comp.selectedTrainer != null)
                yield return trainerGizmo ?? (trainerGizmo = new Gizmo_SSCBondOverview(Overview, SSCBondOverviewPanel.Trainer));
        }
        public override void GameComponentUpdate()
        {
            slavesGizmo?.ClearPending(); masterGizmo?.ClearPending(); trainerGizmo?.ClearPending();
            Overview.Update(Time.frameCount, SSCBondOverviewAccess.IsCardSource(Overview.Card?.Source), Find.CurrentMap);
            if (Overview.Card == null)
            {
                slavesGizmo?.Clear(); masterGizmo?.Clear(); trainerGizmo?.Clear();
                slavesGizmo = masterGizmo = trainerGizmo = null;
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
