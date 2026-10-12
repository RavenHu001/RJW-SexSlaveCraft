using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 道具入口只授予永久资格及原版研究蓝图；当前身份仍由身份选择入口管理。
    public static partial class SSCMasterQualificationUtility
    {
        /// <summary>菜单、原版使用任务和最终结算共用检查；手动身份设置不影响道具资格。</summary>
        public static AcceptanceReport CanUseQualificationItem(Pawn pawn, Thing item)
        {
            // 先验证接收资格的人格仍有效。空壳身体不能通过新道具重新取得已转走的人格资格。
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.RaceProps?.Humanlike != true ||
                pawn.TryGetComp<CompSexSlaveTraining>() == null ||
                (SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                    pawn.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true))
                return "SSC_MasterQualification_InvalidPawn".Translate().ToString();

            // 道具按 SSC 当前身份判定，原版奴隶身份不代替 SSC Slave。
            // 手动选择主人但尚未解锁的角色仍可使用，已解锁的人格不重复消耗。
            CompSexSlaveTraining training = pawn.TryGetComp<CompSexSlaveTraining>();
            if (training.pawnIdentity == PawnIdentity.Slave)
                return "SSC_MasterQualification_Slave".Translate().ToString();
            if (!Enum.IsDefined(typeof(PawnIdentity), training.pawnIdentity))
                return "SSC_MasterQualification_InvalidPawn".Translate().ToString();
            if (training.masterQualificationUnlocked)
                return "SSC_MasterQualification_AlreadyUnlocked".Translate().ToString();
            // 配套定义与存档研究管理器都必须存在；短路游戏检查，避免主菜单访问 Find 时空引用。
            if (SSCDefOf.SSC_MasterQualificationItem == null || SSCDefOf.SSC_BasicTraining == null ||
                Current.Game == null || Find.ResearchManager == null)
                return "SSC_MasterQualification_Unavailable".Translate().ToString();
            // 使用效果也可能被其他入口直接调用，不能仅信任最初菜单选择的物品。
            if (item == null || item.Destroyed || item.stackCount < 1 || item.def != SSCDefOf.SSC_MasterQualificationItem)
                return "SSC_MasterQualification_InvalidItem".Translate().ToString();
            return true;
        }

        /// <summary>成功授予与单个消耗放在同一入口；拒绝时不写个人记录、研究或物品。</summary>
        public static bool TryUseQualificationItem(Pawn pawn, Thing item, out string reason)
        {
            // 读条期间身份、资格和物品都可能变化；最终结算重新检查，拒绝时不产生副作用。
            AcceptanceReport report = CanUseQualificationItem(pawn, item);
            reason = report.Reason;
            if (!report.Accepted) return false;

            // 蓝图属于存档研究，不属于个人。原版管理器负责保存计数，已满足时不重复添加。
            // 已开始/完成的旧研究由原版加载兼容补齐，这里不再维护第二套迁移记录。
            ResearchProjectDef project = SSCDefOf.SSC_BasicTraining;
            int missing = project.TechprintCount - project.TechprintsApplied;
            // 原版在无 Royalty 时返回 0；只补蓝图，不调用带经验/研究加速的 ApplyTechprint。
            if (missing > 0) Find.ResearchManager.AddTechprints(project, missing);
            // 研究边界调用成功后写个人资格及当前格式版本，身份、绑定和培养进度保持原值。
            // 消耗只在此成功路径执行；不能另挂独立销毁效果，否则拒绝授予也可能损失道具。
            pawn.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
            item.SplitOff(1).Destroy();
            reason = null;
            return true;
        }
    }
}
