using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>将原版种子使用任务连接到统一用途结算；1.7的资格与定型分支均由服务判定。</summary>
    public class CompUseEffect_MasterQualification : CompUseEffect
    {
        /// <summary>原版菜单、任务启动和执行期间都复用该条件，拒绝原因由资格服务提供。</summary>
        public override AcceptanceReport CanBeUsedBy(Pawn pawn)
        {
            return SSCMasterQualificationUtility.CanUseQualificationItem(pawn, parent);
        }

        /// <summary>提交当前可用用途并在结果确定后提示；预约、移动、等待和一般中断仍交给原版 UseItem。</summary>
        public override void DoEffect(Pawn usedBy)
        {
            // 不依赖菜单曾经通过检查；失败显示服务返回的原因，物品消耗只由服务成功路径负责。
            if (!SSCMasterQualificationUtility.TryUseQualificationItem(usedBy, parent, out string reason))
            {
                Messages.Message(reason, usedBy, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            // 道具可能已整体销毁，因此成功提示以使用者为目标，在实际结算成功后发出。
            Messages.Message(
#if SSC_RW17
                "SSC_Imprint_SeedUsed".Translate(usedBy.LabelShort), usedBy,
#else
                "SSC_MasterQualification_Granted".Translate(usedBy.LabelShort), usedBy,
#endif
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
