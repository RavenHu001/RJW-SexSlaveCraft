// AI 辅助生成：分伤偏好统一读取原缰绳字段，实际伤害资格仍由分伤模块判断。
using Verse;

namespace SexSlaveCraft
{
    public static partial class SSCAbilityToggleUtility
    {
        public const string MasterDamageSharing = "SSC_MasterDamageSharing";
        /// <summary>UI 后端与高频受伤入口共用的只读偏好查询；不追加身份／人格条件，不遍历注册表。</summary>
        public static bool GetMasterDamageSharingEnabled(Pawn pawn)
            => SSCBondUtility.GetBridle(pawn)?.shareDamageEnabled ?? false;
    }
}
