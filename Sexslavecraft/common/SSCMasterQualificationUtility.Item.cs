using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 首次授予资格与蓝图；1.7 另按当前身份及已有资格授予定型印记。
    public static partial class SSCMasterQualificationUtility
    {
        /// <summary>菜单、原版使用任务与最终结算共用的只读准入；1.7按当前身份、永久资格和印记决定可用用途。</summary>
        public static AcceptanceReport CanUseQualificationItem(Pawn pawn, Thing item)
        {
            // 先验证接收资格的人格仍有效。空壳身体不能通过新道具重新取得已转走的人格资格。
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.RaceProps?.Humanlike != true ||
                pawn.TryGetComp<CompSexSlaveTraining>() == null ||
                (SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                    pawn.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true))
                return "SSC_MasterQualification_InvalidPawn".Translate().ToString();

            // 道具按 SSC 当前身份判定，原版奴隶身份不代替 SSC Slave。
            // 手动主人尚未解锁时仍只取得资格；1.7 已解锁者走主人态，性奴分支优先。
            CompSexSlaveTraining training = pawn.TryGetComp<CompSexSlaveTraining>();
#if SSC_RW17
            // 人格写入期间不接收新请求；初始和分化印记者都不能用种子，不提供提前定型或叠加用途。
            if (SSCPersonalityRuntimeUtility.IsMutating(pawn))
                return "SSC_MasterQualification_InvalidPawn".Translate().ToString();
            if (SSCImprintUtility.GetKind(pawn) != SSCImprintKind.None)
                return "SSC_Imprint_SeedAlreadyMarked".Translate().ToString();
            if (!SSCImprintUtility.DefinitionsAvailable)
                return "SSC_MasterQualification_Unavailable".Translate().ToString();
            // 已有资格的非性奴必须能真正切成主人；原锁链等身份锁不能在种子结算时被绕过。
            if (training.pawnIdentity != PawnIdentity.Slave && training.masterQualificationUnlocked &&
                training.pawnIdentity != PawnIdentity.Master && SSCIdentityUtility.IsIdentityLocked(pawn))
                return "SSC_Imprint_SeedIdentityBlocked".Translate().ToString();
#else
            if (training.pawnIdentity == PawnIdentity.Slave)
                return "SSC_MasterQualification_Slave".Translate().ToString();
#endif
            if (!Enum.IsDefined(typeof(PawnIdentity), training.pawnIdentity))
                return "SSC_MasterQualification_InvalidPawn".Translate().ToString();
#if !SSC_RW17
            if (training.masterQualificationUnlocked)
                return "SSC_MasterQualification_AlreadyUnlocked".Translate().ToString();
#endif
            // 配套定义与存档研究管理器都必须存在；短路游戏检查，避免主菜单访问 Find 时空引用。
            if (SSCDefOf.SSC_MasterQualificationItem == null || SSCDefOf.SSC_BasicTraining == null ||
                Current.Game == null || Find.ResearchManager == null)
                return "SSC_MasterQualification_Unavailable".Translate().ToString();
            // 使用效果也可能被其他入口直接调用，不能仅信任最初菜单选择的物品。
            if (item == null || item.Destroyed || item.stackCount < 1 || item.def != SSCDefOf.SSC_MasterQualificationItem)
                return "SSC_MasterQualification_InvalidItem".Translate().ToString();
            return true;
        }

        /// <summary>最终复查后提交一个用途，成功路径仅消耗一枚；定型用途不重发首次资格的研究蓝图。</summary>
        public static bool TryUseQualificationItem(Pawn pawn, Thing item, out string reason)
        {
            // 读条期间身份、资格和物品都可能变化；任何收益写入前先重查，准入拒绝时不产生副作用。
            AcceptanceReport report = CanUseQualificationItem(pawn, item);
            reason = report.Reason;
            if (!report.Accepted) return false;
#if SSC_RW17
            // 捕获本次结算的人格与资格阶段；不按累计吃过几枚推断，也不在回调后重新取得授权。
            CompSexSlaveTraining training = pawn.TryGetComp<CompSexSlaveTraining>();
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            bool qualified = training.masterQualificationUnlocked;
            PawnIdentity originalIdentity = training.pawnIdentity;
            // 当前性奴分支优先；有历史主人资格也不改方向或绑定。
            if (originalIdentity == PawnIdentity.Slave || qualified)
            {
                PawnIdentity target = originalIdentity == PawnIdentity.Slave ? PawnIdentity.Slave : PawnIdentity.Master;
                // 先走正常身份提交，再授予定型 Hediff；反过来添加会先把原身份锁住。
                // 身份和健康写入之间的回调必须仍属于原人格、原 Comp，且物品与资格仍有效。
                if (!SSCIdentityUtility.TrySetIdentity(pawn, target) ||
                    !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || pawn.TryGetComp<CompSexSlaveTraining>() != training ||
                    training.masterQualificationUnlocked != qualified || !CanUseQualificationItem(pawn, item).Accepted ||
                    !SSCImprintUtility.TryGrantDifferentiated(pawn, target) ||
                    !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token))
                { reason = "SSC_Imprint_SeedIdentityBlocked".Translate().ToString(); return false; }
                // 本分支到此直接结束，避免继续落入首次资格分支重复补蓝图或消耗第二枚。
                item.SplitOff(1).Destroy();
                reason = null;
                return true;
            }
#endif

            // 蓝图属于存档研究，不属于个人。原版管理器负责保存计数，已满足时不重复添加。
            // 已开始/完成的旧研究由原版加载兼容补齐，这里不再维护第二套迁移记录。
            ResearchProjectDef project = SSCDefOf.SSC_BasicTraining;
            int missing = project.TechprintCount - project.TechprintsApplied;
            // 原版在无 Royalty 时返回 0；只补蓝图，不调用带经验/研究加速的 ApplyTechprint。
            if (missing > 0) Find.ResearchManager.AddTechprints(project, missing);
#if SSC_RW17
            // 研究或外部回调不能把前任人格的使用请求交给后来者。
            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || pawn.TryGetComp<CompSexSlaveTraining>() != training ||
                training.pawnIdentity != originalIdentity || training.masterQualificationUnlocked != qualified ||
                !CanUseQualificationItem(pawn, item).Accepted)
            { reason = "SSC_MasterQualification_InvalidPawn".Translate().ToString(); return false; }
#endif
            // 研究边界调用成功后写个人资格及当前格式版本，身份、绑定和培养进度保持原值。
            // 消耗只在此成功路径执行；不能另挂独立销毁效果，否则拒绝授予也可能损失道具。
            pawn.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
            item.SplitOff(1).Destroy();
            reason = null;
            return true;
        }
    }
}
