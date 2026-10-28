// AI 辅助生成：普通 PNA 身体生产开关适配，不引入人格偏好副本。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>将框架查询与提交桥接到原 PNA 生产组件，owner 同时绑定原记录与保存选择。</summary>
    internal sealed class SSCPnaProductionAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCPnaProductionAbilityToggleBackend Instance = new SSCPnaProductionAbilityToggleBackend();

        /// <summary>读取原组件唯一保存值，缺记录默认 true 但不意味着当前可生产或可编辑。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCPnaProductionUtility.GetProductionEnabled(pawn);

        /// <summary>只描述身体与生产记录的通用准入，不重做某次营养扣除或产出判断。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCPnaProductionUtility.ActivationFailure(pawn);

        /// <summary>编辑资格由权威生产工具检查；当前资格不会改写身体中的原选择。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCPnaProductionUtility.CanSetProductionEnabled(pawn, enabled, out string reason) ? null : reason;

        /// <summary>捕获原生产组件引用，删除再添加相同 Def 后也必须重新取得界面凭据。</summary>
        public override object CaptureOwner(Pawn pawn) => SSCPnaProductionUtility.Find(pawn);

        /// <summary>必须仍是原身体中的同一生产组件，不能以新实例延续旧请求权限。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(SSCPnaProductionUtility.Find(pawn), owner);

        /// <summary>兼容基类签名；实际公共提交使用下方可返回拒绝原因的入口。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => SSCPnaProductionUtility.TrySetProductionEnabled(pawn, enabled, out _);

        /// <summary>提交时复查当前管理资格，只写原组件并保留权威拒绝原因。</summary>
        public override bool TrySetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled, out string reason)
            => SSCPnaProductionUtility.TrySetProductionEnabled(pawn, enabled, SSCPnaProductionUtility.Find(pawn), out reason);
    }
}
