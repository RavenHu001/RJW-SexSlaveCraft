// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>开启态独立配置，切换个人状态时不改写共享 Def。</summary>
    public sealed class SSCUterusDevelopmentExtension : DefModExtension
    {
        // 和常规阶段一样由 XML 提供；同一份只读配置可以被多个角色安全复用。
        public HediffStage fertileStage;
    }

    /// <summary>当前身体的唯一开发记录；Severity 是进度，开关随本实例保存。</summary>
    public sealed class Hediff_SSCUterusDevelopment : Hediff
    {
        // 离体、无效挂载或已归零的实例不再参与属性计算，等待正常清理。
        private static readonly HediffStage NeutralStage = new HediffStage();
        // 只保存玩家对当前身体的选择，不保存身份或绑定关系。
        private bool fertilityEnabled;

        /// <summary>完成资格只由有效进度判断，不根据旧六部位或角色身份推断。</summary>
        public bool IsComplete => float.IsFinite(Severity) && Severity >= 1f;
        /// <summary>未完成时不暴露开启态；真正切换还须经工具类检查妊娠。</summary>
        public bool FertilityEnabled => IsComplete && fertilityEnabled;

        /// <summary>按当前身体的培养进度与生育选择返回本地化正文，不保存或缓存描述状态。</summary>
        public override string Description
        {
            get
            {
                // 无效、离体或尚未加入身体的实例使用通用定义说明，不补建开发记录。
                if (!SSCUterusDevelopmentUtility.IsCurrentRecord(this)) return base.Description;
                // 培养期的各属性阶段共用一套正文；满级再按现有开关区分两套正文。
                // 每次读取都查当前语言，切换模式或语言无需修改共享 Def 或另存字段。
                string key = !IsComplete ? "SSC_UterusDevelopment_DescriptionDeveloping"
                    : FertilityEnabled ? "SSC_UterusDevelopment_DescriptionFertile"
                    : "SSC_UterusDevelopment_DescriptionInfertile";
                return key.Translate();
            }
        }

        /// <summary>统一约束运行时进度写入，并清除未完成状态下失效的开启选择。</summary>
        public override float Severity
        {
            get => base.Severity;
            set
            {
                // 0 仍由原版正常移除；不把异常输入恢复成初始 1%。
                float normalized = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
                if (normalized < 1f) fertilityEnabled = false;
                // 继续走原版阶段变更与健康通知；不直接改写底层存档字段。
                base.Severity = normalized;
            }
        }

        /// <summary>向原版属性与健康说明提供当前生效阶段；切换时不修改共享阶段。</summary>
        public override HediffStage CurStage
        {
            get
            {
                if (!SSCUterusDevelopmentUtility.IsCurrentRecord(this)) return NeutralStage;
                // 配置缺失时保留常规关闭阶段；正常开启入口也会拒绝缺失配置。
                return FertilityEnabled ? def.GetModExtension<SSCUterusDevelopmentExtension>()?.fertileStage ?? base.CurStage
                    : base.CurStage;
            }
        }

        /// <summary>在原版初始严重度初始化前，为新实例选择当前身体的躯干。</summary>
        public override void PostMake()
        {
            // 后续添加入口仍应传正确躯干；无躯干时不伪造全身挂载记录。
            Part = SSCUterusDevelopmentUtility.FindTorso(pawn);
            base.PostMake();
        }

        /// <summary>保留原版加入回调，再清理当前身体中的无效或重复记录。</summary>
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            SSCUterusDevelopmentUtility.Normalize(pawn);
        }

        /// <summary>重复添加只取较大进度，保留既有实例的生育选择，不累计额外经验。</summary>
        public override bool TryMergeWith(Hediff other)
        {
            if (other == this) return true;
            if (other is not Hediff_SSCUterusDevelopment incoming || other.def != def ||
                !SSCUterusDevelopmentUtility.IsCurrentRecord(this)) return false;
            // 不调用原版的严重度相加；保留既有实例及玩家开关。
            float incomingProgress = float.IsFinite(incoming.Severity) ? incoming.Severity : 0f;
            Severity = Math.Max(Severity, incomingProgress);
            return true;
        }

        /// <summary>基类保存进度及部位，本类只保存开关；引用恢复后才规范化身体记录。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fertilityEnabled, "sscUterusFertilityEnabled", false);
            // 旧档缺少开关字段按关闭读取；不在 LoadingVars 提前使用尚未恢复的 pawn 引用。
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                SSCUterusDevelopmentUtility.Normalize(pawn);
        }

        /// <summary>让直接从存档读入的字段经过同一约束，并报告是否需要刷新健康缓存。</summary>
        internal bool NormalizeState()
        {
            float previous = Severity;
            bool previousToggle = fertilityEnabled;
            Severity = previous;
            return previous != Severity || previousToggle != fertilityEnabled;
        }

        /// <summary>只供已经完成资格、妊娠及配置检查的切换入口写入，不修改任何妊娠。</summary>
        internal void SetFertilityEnabled(bool enabled)
        {
            fertilityEnabled = enabled;
            // 开关不改变严重度，需主动使健康缓存失效。
            pawn.health.Notify_HediffChanged(this);
        }
    }
}
