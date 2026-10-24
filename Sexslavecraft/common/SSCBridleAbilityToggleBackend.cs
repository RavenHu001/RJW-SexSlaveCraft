// AI 辅助生成：缰绳开关沿原字段保存，保持配置资格与某次伤害能否分摊独立。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class SSCBridleAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCBridleAbilityToggleBackend Instance = new SSCBridleAbilityToggleBackend();
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCAbilityToggleUtility.GetMasterDamageSharingEnabled(pawn);
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCBondUtility.GetBridle(pawn) == null ? "SSC_MasterProgression_ShareNoBond".Translate().ToString() : null;
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        {
            string reason = ActivationFailure(pawn, definition);
            if (reason != null) return reason;
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.pawnIdentity != PawnIdentity.Master || Faction.OfPlayer == null || pawn.Faction != Faction.OfPlayer)
                return "SSC_MasterProgression_ShareReadOnly".Translate().ToString();
            return SSCPersonalityRuntimeUtility.IsMutating(pawn) ? SSCAbilityToggleUtility.Reason("Mutating") : null;
        }
        public override object CaptureOwner(Pawn pawn) => SSCBondUtility.GetBridle(pawn);
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(SSCBondUtility.GetBridle(pawn), owner);
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCBondUtility.GetBridle(pawn).shareDamageEnabled = enabled;
    }
}
