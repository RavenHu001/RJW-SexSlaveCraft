using Verse;

namespace SexSlaveCraft
{
    /// <summary>训导官培养与任职资格的只读失败原因。</summary>
    /// <remarks>这些值只描述特化资格。工作开关、倒地、目标指派和目标许可由现有工作入口分别判断。</remarks>
    public enum TrainerSpecializationFailure
    {
        /// <summary>所有特化资格条件均已满足。</summary>
        None,
        /// <summary>角色或其 SSC 训练组件不存在，无法读取权威状态。</summary>
        MissingPawnOrTrainingComp,
        /// <summary>SSC 身份并非性奴；主人使用原有固定调教员规则。</summary>
        NotSexSlave,
        /// <summary>不是现有调教工作支持的自由殖民者。</summary>
        NotFreeColonist,
        /// <summary>没有锁链记录中的实际主人，指定调教员不能代替主人。</summary>
        NoBoundMaster,
        /// <summary>当前锁链未达到第 3 阶段“屈服性奴”。</summary>
        ChainStageTooLow,
        /// <summary>终极化记录存在，但禁用标记阻止其效果和任职资格。</summary>
        FinalDisabled,
        /// <summary>没有有效终极状态，当前培养方向也不是训导官。</summary>
        WrongSpecialization,
        /// <summary>当前培养训导官，但尚未取得 20% 基础资格。</summary>
        BasicStageNotReached
    }

    /// <summary>只读取训导官的持续条件、完成记录与当前任职资格。</summary>
    /// <remarks>菜单、候选、同床和限制解析都可能频繁查询；这里不添加或移除 Hediff，也不切换培养方向。</remarks>
    public static class TrainerSpecializationUtility
    {
        // 普通方向用组件当前进度解锁基础资格；同一数值供后续界面和状态同步引用。
        public const float BasicQualificationProgress = 0.20f;

        // 第 3 阶段从当前锁链严重度 0.5 开始，不从历史 Trait 或恶堕峰值推断。
        public const int RequiredSexSlaveStage = 3;

        /// <summary>培养和终极效果共同依赖的持续条件；离图本身不算失格。</summary>
        /// <remarks>本查询不检查研究、工作安排或暂时身体状态；研究仅属于以后选择方向时的入口条件。</remarks>
        public static bool MeetsContinuousConditions(Pawn pawn, out TrainerSpecializationFailure failure)
        {
            failure = TrainerSpecializationFailure.None;

            // 所有后续条件都依赖 Pawn 的 SSC 组件。缺失时给出确定的失败原因，
            // 避免读档恢复中的空引用被误判成尚未培养或没有主人。
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null)
            {
                failure = TrainerSpecializationFailure.MissingPawnOrTrainingComp;
                return false;
            }

            // 训导官只是性奴的职能特化，不会把主人或未选择身份转换为性奴。
            // 主人的固定调教员身份由 SSCIdentityUtility.IsTrainer 独立处理。
            if (comp.pawnIdentity != PawnIdentity.Slave)
            {
                failure = TrainerSpecializationFailure.NotSexSlave;
                return false;
            }

            // 现有执行者范围是自由殖民者。不能使用受训目标的
            // IsSupportedVanillaStatus：它还允许原版奴隶和囚犯。
            // 不查询 Map.FreeColonists，保证旅行中仍可保留培养和终极记录。
            if (!pawn.IsColonist || pawn.IsSlave || pawn.IsPrisonerOfColony)
            {
                failure = TrainerSpecializationFailure.NotFreeColonist;
                return false;
            }

            // 只认锁链指向的实际主人；GetResolvedMaster 可能回退到指定调教员，
            // 若在这里使用，会让无主性奴借他人指派取得“最臣服”的资格。
            if (SSCBondUtility.GetBoundMaster(pawn) == null)
            {
                failure = TrainerSpecializationFailure.NoBoundMaster;
                return false;
            }

            // 阶段取当前锁链严重度。历史达到过第 3 阶段但现已退阶时，
            // 普通培养与终极效果都必须立即失格，不等待低频 Hediff 维护。
            if (SSCIdentityUtility.GetSexSlaveStage(pawn) < RequiredSexSlaveStage)
            {
                failure = TrainerSpecializationFailure.ChainStageTooLow;
                return false;
            }

            // 到这里仅说明持续培养条件满足；尚未判断是否达到 20%、
            // 是否终极化以及玩家是否打开“是调教员”个人开关。
            return true;
        }

        /// <summary>普通完成进度不等于终极化；有效或禁用标记均保留完成事实。</summary>
        public static bool HasFinalRecord(Pawn pawn)
        {
            // 两种终极 Hediff 是同一个完成事实的不同状态。即使当前条件失效、
            // 方向切走或只剩禁用标记，配方和人格保存仍应能识别这段历史。
            return HasMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final)
                || HasMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
        }

        /// <summary>终极效果在切换培养方向后仍可生效；双标记及持续条件失效时保守拒绝。</summary>
        public static bool HasActiveFinalEffect(Pawn pawn)
        {
            // 有效标记只表示上次维护后的状态，还要实时核对持续条件。
            // 若有效和禁用标记同时残留，禁用优先；归并由后续生命周期阶段负责。
            // 本判断故意不依赖当前培养方向，终极化后允许培养其他方向。
            return MeetsContinuousConditions(pawn, out _)
                && HasMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final)
                && !HasMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
        }

        /// <summary>仅判断特化是否赋予手动任职的资格，不读取个人开关、工作或目标指派。</summary>
        public static bool HasTrainerQualification(Pawn pawn, out TrainerSpecializationFailure failure)
        {
            // 持续条件排在状态判断前：解绑、退阶和原版身份变化必须即时拒绝，
            // 同时向界面提供根本原因，而不是依赖低频维护先切换终极标记。
            if (!MeetsContinuousConditions(pawn, out failure)) return false;

            // 禁用标记优先于残留的有效标记，也阻止当前普通进度绕过终极禁用。
            // 这里不尝试删除重复标记；读取入口需要保持无副作用。
            if (HasMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled))
            {
                failure = TrainerSpecializationFailure.FinalDisabled;
                return false;
            }

            // 有效终极状态提供跨方向资格。个人开关仍由后续 IsTrainer 接入时
            // 单独判断；终极化本身不会自动任命该性奴为调教员。
            if (HasMarker(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer_Final)) return true;

            // 没有终极记录时只能走普通培养路径。每类特化的历史进度会保留，
            // 但离开训导官方向后，历史进度不能继续赋予当前任职资格。
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp.specializationType != SexSlaveSpecializationType.TrainerOfficer)
            {
                failure = TrainerSpecializationFailure.WrongSpecialization;
                return false;
            }

            // 20% 是普通方向的基础阶段。拒绝 NaN 和无穷值，避免坏档或
            // 外部写入的异常进度因比较结果而意外越过门槛。
            float progress = comp.specializationProgress;
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < BasicQualificationProgress)
            {
                failure = TrainerSpecializationFailure.BasicStageNotReached;
                return false;
            }

            // 返回的是“允许玩家开启职能”的资格，不代表工作、指派或目标许可通过。
            return true;
        }

        /// <summary>只读检查指定 Hediff 是否存在，定义或健康数据缺失时按未持有处理。</summary>
        private static bool HasMarker(Pawn pawn, HediffDef def)
        {
            // Def 加载失败时不把 null 交给 HasHediff；真正的 Def 缺失仍由
            // SSCDefOf 的启动日志暴露，资格查询在此保持安全失败。
            return def != null && (pawn?.health?.hediffSet?.HasHediff(def) ?? false);
        }
    }
}
