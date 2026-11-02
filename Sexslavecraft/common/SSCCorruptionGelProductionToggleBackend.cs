// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>身体生产组件后端，不向人格字典复制保存选择。</summary>
    internal sealed class SSCCorruptionGelProductionToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCCorruptionGelProductionToggleBackend Instance = new SSCCorruptionGelProductionToggleBackend();
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCCorruptionGelProductionUtility.Find(pawn)?.ProductionEnabled ?? false;
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCCorruptionGelProductionUtility.ActivationFailure(pawn);
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCCorruptionGelProductionUtility.CanSet(pawn, enabled, out string reason) ? null : reason;
        public override object CaptureOwner(Pawn pawn) => SSCCorruptionGelProductionUtility.Find(pawn);
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(owner, SSCCorruptionGelProductionUtility.Find(pawn));
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => TrySetEnabled(pawn, definition, enabled, out _);
        public override bool TrySetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled, out string reason)
            => SSCCorruptionGelProductionUtility.TrySet(pawn, enabled, SSCCorruptionGelProductionUtility.Find(pawn), out reason);
    }
}
