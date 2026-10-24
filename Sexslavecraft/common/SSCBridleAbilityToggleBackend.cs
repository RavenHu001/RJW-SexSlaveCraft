// AI 辅助生成：缰绳开关沿原字段保存，保持配置资格与某次伤害能否分摊独立。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>把主人面板接到原缰绳字段；不参与人格字典的提取清源、植入覆盖或克隆初始化。</summary>
    internal sealed class SSCBridleAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCBridleAbilityToggleBackend Instance = new SSCBridleAbilityToggleBackend();
        /// <summary>与分伤消费者共用同一原字段读取，缺缰绳视为关闭而不创建关系。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCAbilityToggleUtility.GetMasterDamageSharingEnabled(pawn);
        /// <summary>只判断偏好是否有有效载体；地图、候选和伤害类型仍由具体分伤入口处理。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCBondUtility.GetBridle(pawn) == null ? "SSC_MasterProgression_ShareNoBond".Translate().ToString() : null;
        /// <summary>保留玩家主人及原缰绳编辑条件，并在换人格期间暂停提交，不要求当前存在承伤候选。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        {
            // 缺载体优先显示无绑定提示；有缰绳后才区分只读身份与人格替换中的暂时拒绝。
            string reason = ActivationFailure(pawn, definition);
            if (reason != null) return reason;
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.pawnIdentity != PawnIdentity.Master || Faction.OfPlayer == null || pawn.Faction != Faction.OfPlayer)
                return "SSC_MasterProgression_ShareReadOnly".Translate().ToString();
            return SSCPersonalityRuntimeUtility.IsMutating(pawn) ? SSCAbilityToggleUtility.Reason("Mutating") : null;
        }
        /// <summary>捕获当前缰绳实例；随后解绑／换绑不能通过重新查询取得新请求凭据。</summary>
        public override object CaptureOwner(Pawn pawn) => SSCBondUtility.GetBridle(pawn);
        /// <summary>要求原缰绳仍属于当前查询结果，防止旧点击写入替换后的缰绳。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(SSCBondUtility.GetBridle(pawn), owner);
        /// <summary>公共入口完成引用与权限复查后，只写原 shareDamageEnabled 字段，不添加另一份保存状态。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCBondUtility.GetBridle(pawn).shareDamageEnabled = enabled;
    }
}
