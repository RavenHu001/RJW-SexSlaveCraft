// AI 辅助生成。普通单选入口，不借用研究、训练许可或开发按钮的准入。
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        internal Gizmo BondOverviewGizmo(Pawn pawn)
            => SSCBondOverviewAccess.IsCardSource(pawn)
                ? Current.Game.GetComponent<SSCBondRelationGameComponent>()?.GetOverviewGizmo(pawn) : null;
    }
}
