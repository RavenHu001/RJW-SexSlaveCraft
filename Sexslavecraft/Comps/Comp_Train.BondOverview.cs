// AI 辅助生成。普通单选入口，不借用研究、训练许可或开发按钮的准入。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        internal IEnumerable<Gizmo> BondOverviewGizmos(Pawn pawn)
        {
            if (!SSCBondOverviewAccess.IsCardSource(pawn)) yield break;
            foreach (Gizmo gizmo in Current.Game.GetComponent<SSCBondRelationGameComponent>().GetOverviewGizmos(pawn)) yield return gizmo;
        }

        internal Gizmo BondOverviewGizmo(Pawn pawn)
            => SSCBondOverviewAccess.IsCardSource(pawn)
                ? Current.Game.GetComponent<SSCBondRelationGameComponent>()?.GetOverviewGizmo(pawn) : null;
    }
}
