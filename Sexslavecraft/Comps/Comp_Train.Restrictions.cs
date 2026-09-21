using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 只在读取旧档时暂存个体例外；迁移快照捕获后不参与许可、指派或特化逻辑。
        internal bool allowOthersForTrainingOrSex;

        public SSCRestrictionConfig restrictionConfig;
        public bool restrictionLifecycleSeen = true;
        public SSCRestrictionLegacyPawn legacyRestrictionInput;
        internal int restrictionRestoreDepth;
        internal string restrictionLastError;

        /// <summary>读写个体配置和独立生命周期标记；在旧关系修复前捕获迁移输入，引用恢复后再转换。</summary>
        public void ExposeRestrictions()
        {
            // 新存档只保存新配置及必要的待迁移快照，不再写旧例外键。
            // LoadingVars 必须先读旧值，PostLoadInit 才能在关系修复前固定迁移输入。
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref allowOthersForTrainingOrSex, "allowOthersForTrainingOrSex", false);
            Scribe_Deep.Look(ref restrictionConfig, "sscRestrictionConfig");
            Scribe_Values.Look(ref restrictionLifecycleSeen, "sscRestrictionLifecycleSeen", false);
            Scribe_Deep.Look(ref legacyRestrictionInput, "sscRestrictionLegacyInput");
            if (Scribe.mode != LoadSaveMode.PostLoadInit || restrictionLifecycleSeen) return;
            restrictionLifecycleSeen = true;
            Pawn pawn = parent as Pawn;
            if (restrictionConfig == null && legacyRestrictionInput == null && SSCRestrictionResolver.IsApplicable(pawn))
                legacyRestrictionInput = new SSCRestrictionLegacyPawn
                {
                    open = allowOthersForTrainingOrSex,
                    bus = SSCRestrictionLifecycle.HasBus(pawn)
                };
        }
    }
}
