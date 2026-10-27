// AI 辅助生成：全身微械指令；Severity 只保存一至四枚举档位。
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>身体配置的唯一保存载体；属性阶段从统一有效状态派生。</summary>
    public sealed class Hediff_SSCOrgasmSensitivity : Hediff
    {
        private static readonly HediffStage NeutralStage = new HediffStage();

        /// <summary>拒绝非法值经原版裁剪自动变成四档；合法档位沿用原版健康通知。</summary>
        public override float Severity
        {
            get => base.Severity;
            set
            {
                float previous = base.Severity;
                // 非法输入先变为关闭值，再交原版 setter；直接交给原版会把越界 5 裁成合法四档。
                base.Severity = SSCOrgasmSensitivityUtility.IsLegalLevel(value) ? value : 0f;
                // 保存档变化未必跨“有效”阶段，例如降阶时从保存三档切四档；仍需刷新派生显示／思想。
                if (previous != base.Severity) SSCOrgasmSensitivityUtility.NotifyStateChanged(pawn);
            }
        }

        /// <summary>索引随实际档位派生；关闭时保留非负索引约定，但 CurStage 单独返回中性阶段。</summary>
        public override int CurStageIndex => System.Math.Max(0, SSCOrgasmSensitivityUtility.GetState(pawn).EffectiveLevel - 1);

        /// <summary>只有当前唯一 owner 提供一次 RJW 脆弱度偏移；离体和重复实例返回中性阶段。</summary>
        public override HediffStage CurStage
        {
            get
            {
                var state = SSCOrgasmSensitivityUtility.GetState(pawn);
                // 保存着同一档位的离体或重复实例不能再次提供偏移；只读复用定义级派生阶段。
                return ReferenceEquals(state.Record, this) && state.EffectiveLevel > 0
                    ? SSCOrgasmSensitivityConfig.Get(SSCOrgasmSensitivityUtility.Definition).Stages[state.EffectiveLevel - 1] : NeutralStage;
            }
        }

        /// <summary>说明同时展示保存档位、实际档位和当前系数，不额外定义重复参与计算的 StatDef。</summary>
        public override string Description => base.Description + "\n\n" + SSCOrgasmSensitivityPresentation.Describe(pawn);

        /// <summary>重复加入保留当前原实例和原档位，不调用原版严重度相加。</summary>
        public override bool TryMergeWith(Hediff other)
            => ReferenceEquals(other, this) || (other is Hediff_SSCOrgasmSensitivity && other.def == def &&
                ReferenceEquals(SSCOrgasmSensitivityUtility.Find(pawn), this));

        /// <summary>加入后规范化非法／重复记录，未加入健康列表的临时实例不提供效果。</summary>
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            SSCOrgasmSensitivityUtility.Normalize(pawn);
        }

        /// <summary>移除后立即清理持续心情缓存，关闭不残留另存的思想状态。</summary>
        public override void PostRemoved()
        {
            base.PostRemoved();
            SSCOrgasmSensitivityUtility.NotifyStateChanged(pawn);
        }

        /// <summary>只使用原版严重度存档；所有引用恢复后规范化，不增加人格或 Comp 保存副本。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // LoadingVars 时 pawn／健康列表尚未恢复；只在引用恢复后的边界清理坏档和重复记录。
            if (Scribe.mode == LoadSaveMode.PostLoadInit) SSCOrgasmSensitivityUtility.Normalize(pawn);
        }
    }

    /// <summary>跟随当前身体状态的持续思想，不创建高潮事件记忆或按次数堆叠。</summary>
    public sealed class ThoughtWorker_SSCOrgasmSensitivity : ThoughtWorker
    {
        /// <summary>与属性共用有效档位，再查询独立 XML 矩阵；无心情角色不补建需求。</summary>
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var state = SSCOrgasmSensitivityUtility.GetState(p);
            return state.EffectiveLevel > 0 && SSCOrgasmSensitivityConfig.TryGetMood(def,
                SSCOrgasmSensitivityConfig.Get(SSCOrgasmSensitivityUtility.Definition), state.ChainStage, state.EffectiveLevel, out int index)
                ? ThoughtState.ActiveAtStage(index) : ThoughtState.Inactive;
        }
    }

    /// <summary>保留原版情境思想计算，在实际心情消费时复核当前身体，防止未通知的外部写入延续旧效果。</summary>
    public sealed class Thought_SituationalSSCOrgasmSensitivity : Thought_Situational
    {
        /// <summary>只重新计算这一项思想；正常空值／禁用返回零，其余倍率与屏蔽仍交给原版。</summary>
        public override float MoodOffset()
        {
            // 情境列表可能仍持有旧实例。消费前重算这一项，保留原版思想准入、屏蔽和倍率语义。
            RecalculateState();
            return Active ? base.MoodOffset() : 0f;
        }
    }
}
