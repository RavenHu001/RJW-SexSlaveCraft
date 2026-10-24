// AI 辅助生成：个人渲染读写原外观工具，保留身体归属、主线程和不可变快照。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>适配原身体外观组件的个人渲染值；通用查询不取代原版／FA／SA 的渲染热路径。</summary>
    internal sealed class SSCGelRenderingAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCGelRenderingAbilityToggleBackend Instance = new SSCGelRenderingAbilityToggleBackend();
        /// <summary>与实际头身／五官／SA 渲染共用原准入；渲染热路径本身不改为注册表查询。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition) => FullGelAppearanceUtility.ShouldRender(pawn);
        /// <summary>有现存凝胶身体时才提供渲染能力，不重判身份或人格是否为空壳。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => FullGelAppearanceUtility.GetOwner(pawn) == null ? SSCAbilityToggleUtility.Reason("Unavailable") : null;
        /// <summary>沿原 setter 条件确认主线程及外观组件；缺组件的读取回退仍保持开启。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => !UnityData.IsInMainThread || FullGelAppearanceUtility.GetOwner(pawn)?.TryGetComp<HediffComp_FullGelAppearance>() == null
                ? SSCAbilityToggleUtility.Reason("Unavailable") : null;
        /// <summary>捕获当前凝胶身体实例，不将外观选择保存到人格字典。</summary>
        public override object CaptureOwner(Pawn pawn) => FullGelAppearanceUtility.GetOwner(pawn);
        /// <summary>凝胶身体替换后旧界面不能操作新实例。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(FullGelAppearanceUtility.GetOwner(pawn), owner);
        /// <summary>原外观工具保持局部刷新、Revision 与快照发布；同值不重建图形。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
            => FullGelAppearanceUtility.SetRenderingEnabled(pawn, FullGelAppearanceUtility.GetOwner(pawn), enabled);
        /// <summary>保留原 setter 的失败返回，公共框架不得把被拒绝的渲染写入报告成成功。</summary>
        public override bool TrySetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled, out string reason)
        {
            // 原工具同时核对 owner、线程和组件，并在真实变化时发布快照／刷新目标；同值也可成功。
            bool changed = FullGelAppearanceUtility.SetRenderingEnabled(pawn, FullGelAppearanceUtility.GetOwner(pawn), enabled);
            reason = changed ? null : SSCAbilityToggleUtility.Reason("Changed");
            return changed;
        }
    }
}
