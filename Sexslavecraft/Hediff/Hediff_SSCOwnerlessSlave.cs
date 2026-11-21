// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存和读取无主性奴化的连续进度。</summary>
    /// <remarks>
    /// <see cref="Severity"/> 是唯一进度保存值，沿用原版 Hediff 的 Scribe 流程。三阶 80% 读档后仍是 80%，阶段号按阈值重算。
    /// SSC 身份由调教组件保存，实际归属由绑定锁链保存。发作成形与仪式交接各自负责写入对应载体。
    /// </remarks>
    public sealed class Hediff_SSCOwnerlessSlave : Hediff
    {
        internal const string DefName = "SSC_Hediff_OwnerlessSlave";

        /// <summary>只查询当前健康列表中的无主记录，供身份锁与阶段解析共用。</summary>
        /// <remarks>手选或印记形成的零阶身份通常没有此记录。缺少定义或健康记录时返回空。</remarks>
        internal static Hediff_SSCOwnerlessSlave Find(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(DefName);
            return def == null ? null : pawn?.health?.hediffSet?.GetFirstHediffOfDef(def) as Hediff_SSCOwnerlessSlave;
        }

        /// <summary>供人格提取和植入使用的无主进度。有效锁链优先，零阶记录也保留身份锁。</summary>
        internal static Hediff_SSCOwnerlessSlave GetPersonalityProgress(Pawn pawn)
            => SSCIdentityUtility.IsSexSlave(pawn)
                ? TrainingOutcomeUtility.GetSexSlaveProgress(pawn) as Hediff_SSCOwnerlessSlave : null;

        /// <summary>返回向行为、心情和共享保护提供效果的无主阶段，零表示这些效果未启用。</summary>
        /// <remarks>
        /// 先检查 SSC 身份，再用第一批的统一进度入口选载体。交接期间锁链和无主记录可能同时存在，
        /// 此时以锁链为准，未被选中的无主记录保留其严重度。持有零阶记录仍会锁定身份。
        /// </remarks>
        internal static int GetActiveStage(Pawn pawn)
            => SSCIdentityUtility.IsSexSlave(pawn) &&
                TrainingOutcomeUtility.GetSexSlaveProgress(pawn) is Hediff_SSCOwnerlessSlave record
                ? TrainingOutcomeUtility.GetSexSlaveProgressStage(record).Stage : 0;

        /// <summary>原版能力与 Stat 系统直接读取此处。非当前载体返回 Def 中没有任何修正的中性阶段。</summary>
        public override HediffStage CurStage
            => SSCIdentityUtility.IsSexSlave(pawn) && ReferenceEquals(TrainingOutcomeUtility.GetSexSlaveProgress(pawn), this)
                ? base.CurStage : def?.stages?.Count > 0 ? def.stages[0] : null;

        /// <summary>沿用原版裁剪和健康缓存刷新，跨原版阶段索引时另行同步 SSC 规则和共享保护。</summary>
        public override float Severity
        {
            get => base.Severity;
            set
            {
                int previous = CurStageIndex;
                base.Severity = value;
                // 严重度在同一阶内变化只影响进度。判断在原版回调完成后进行，以最终仍在体内的记录为准。
                // 创建时可以先写严重度，附着的首次同步交给 PostAdd，读取已有存档由各模块的读档入口处理。
                int current = CurStageIndex;
                if (previous != current && ReferenceEquals(Find(pawn), this))
                    NotifyStageChanged(protectionChanged: previous == 0 || current == 0);
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // 添加载体即可能改变恶堕允许区间及持续心情，即使恶堕率和初始严重度没有再写入。
            NotifyStageChanged(protectionChanged: CurStageIndex > 0);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            // 原版已将本记录从健康列表移出。各消费者读取剩余载体，本记录阶段用于判断是否要重算免瘾。
            NotifyStageChanged(protectionChanged: CurStageIndex > 0);
        }

        private void NotifyStageChanged(bool protectionChanged)
        {
            // 失去或获得阶段都可能改变无主持续心情、恶堕落差和自我调教许可心情，共用原版思想失效通知。
            SSCSelfTrainingFeedback.NotifyStatusChanged(pawn);
            if (Scribe.mode != LoadSaveMode.Inactive) return;
            SSCOrgasmSensitivityUtility.SyncOwnerless(pawn);
            // 指派和训导官由各自的维护入口按当前身份重算，已有的绑定／人格事务继续负责延迟其内部通知。
            SSCRestrictionGameComponent.Notify(pawn);
            TrainerSpecializationLifecycle.Notify(pawn);
            // 免瘾边界只有零阶与正式阶段之间。跨正阶段不改变 PNA 负荷，也无须额外扫描 PNA。
            if (protectionChanged) SSCPnaExposureUtility.RefreshBodyState(pawn, forceRefresh: true);
        }
    }

    /// <summary>ThoughtDef 只存一至四阶，因此玩法阶段 1 对应思想索引 0。</summary>
    public sealed class ThoughtWorker_SSCOwnerlessSlave : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            int stage = Hediff_SSCOwnerlessSlave.GetActiveStage(p);
            return stage > 0 && stage <= def.stages.Count ? ThoughtState.ActiveAtStage(stage - 1) : ThoughtState.Inactive;
        }
    }
}
