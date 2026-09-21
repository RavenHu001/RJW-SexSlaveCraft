using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public SSCRestrictionConfig restrictionConfig;
        public bool restrictionLifecycleSeen = true;
        public SSCRestrictionLegacyPawn legacyRestrictionInput;
        internal int restrictionRestoreDepth;
        internal string restrictionLastError;

        /// <summary>读写个体配置和独立生命周期标记；在旧关系修复前捕获迁移输入，引用恢复后再转换。</summary>
        public void ExposeRestrictions()
        {
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
