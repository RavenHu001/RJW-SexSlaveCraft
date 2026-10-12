using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>把原版物品使用流程连接到统一资格服务，表现提示在结算结果确定后发送。</summary>
    public class CompUseEffect_MasterQualification : CompUseEffect
    {
        /// <summary>原版菜单、任务启动和执行期间都复用该条件，拒绝原因由资格服务提供。</summary>
        public override AcceptanceReport CanBeUsedBy(Pawn pawn)
        {
            return SSCMasterQualificationUtility.CanUseQualificationItem(pawn, parent);
        }

        /// <summary>最终结算复查并授予；原版预约、移动、等待和中断由 UseItem 任务负责。</summary>
        public override void DoEffect(Pawn usedBy)
        {
            // 不依赖菜单曾经通过检查；失败只显示原因，服务不写资格、不补研究、不消耗。
            if (!SSCMasterQualificationUtility.TryUseQualificationItem(usedBy, parent, out string reason))
            {
                Messages.Message(reason, usedBy, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            // 道具可能已整体销毁，因此成功提示以使用者为目标，在实际结算成功后发出。
            Messages.Message("SSC_MasterQualification_Granted".Translate(usedBy.LabelShort), usedBy,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
