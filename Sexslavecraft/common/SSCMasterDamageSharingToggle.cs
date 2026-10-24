// AI 辅助生成：分伤偏好统一读取原缰绳字段，实际伤害资格仍由分伤模块判断。
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCAbilityToggleUtility
    {
        public const string MasterDamageSharing = "SSC_MasterDamageSharing";
        public static bool GetMasterDamageSharingEnabled(Pawn pawn)
            => SSCBondUtility.GetBridle(pawn)?.shareDamageEnabled ?? false;
    }
}
