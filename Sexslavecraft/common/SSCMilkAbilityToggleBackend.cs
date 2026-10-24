// AI 辅助生成：泌乳旧字段适配，保留人格采集／植入及旧克隆例外。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>适配原 Comp 泌乳偏好；以泌乳 Hediff 绑定当前 UI 身体，不改人格采集／植入的保存契约。</summary>
    internal sealed class SSCMilkAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCMilkAbilityToggleBackend Instance = new SSCMilkAbilityToggleBackend();
        /// <summary>只读取得当前泌乳记录，兼作 UI 资格和原身体凭据，不补建状态。</summary>
        internal static Hediff Find(Pawn pawn)
            => pawn?.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState);
        /// <summary>保存选择仍来自原训练字段，与普通生产和牛特化消费者共用。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
            => SSCAbilityToggleUtility.GetMilkProductionEnabled(pawn);
        /// <summary>通用准入只判断当前泌乳记录；营养、容量和兼容接管仍在原业务入口判断。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => Find(pawn) == null ? SSCAbilityToggleUtility.Reason("Unavailable") : null;
        /// <summary>沿用身体栏的现存组件／泌乳记录条件，不添加 SSC 身份、研究或绑定限制。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => pawn?.TryGetComp<CompSexSlaveTraining>() == null ? SSCAbilityToggleUtility.Reason("Unavailable") : ActivationFailure(pawn, definition);
        /// <summary>偏好虽存在 Comp，点击仍绑定原泌乳 Hediff；组件及人格凭据由公共上下文另行检查。</summary>
        public override object CaptureOwner(Pawn pawn) => Find(pawn);
        /// <summary>移除或替换泌乳记录后旧点击不得作用于新身体。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner) => owner != null && ReferenceEquals(Find(pawn), owner);
        /// <summary>公共校验后只写原字段；不改奶量、子宫、人格字典或生产时钟，停产处理仍在下一次原生产调用中执行。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => pawn.TryGetComp<CompSexSlaveTraining>().milkProductionEnabled = enabled;
    }
}
