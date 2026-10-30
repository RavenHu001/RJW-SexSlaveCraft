// AI 辅助生成
using System.Runtime.CompilerServices;
using HarmonyLib;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>按本场SexProps实例隔离原生受孕，弱引用防止迟到回调且不持有永久场景存储。</summary>
    internal static class FertilitySceneUtility
    {
        private static readonly ConditionalWeakTable<SexProps, object> protectedProps = new();

        /// <summary>登记本场实际使用的数据实例；只要迟到回调还持有它，取消后也不能触发普通受孕。</summary>
        internal static void Protect(SexProps props)
        { if (props != null) protectedProps.GetValue(props, _ => new object()); }

        /// <summary>只阻断已登记实例或专属当前驱动持有的数据；不按一对Pawn全局禁止后来的自然受孕。</summary>
        internal static bool BlocksNaturalPregnancy(SexProps props)
        {
            if (props == null) return false;
            if (protectedProps.TryGetValue(props, out _)) return true;
            // 原生Start回调及读档时尚未登记的数据，仅可按实际驱动自己的Sexprops识别。
            bool Own(JobDriver_Sex d) => (d is JobDriver_FertilityConception || d is JobDriver_FertilityReceiver) &&
                ReferenceEquals(d.Sexprops, props);
            if (!Own(props.pawn?.jobs?.curDriver as JobDriver_Sex) && !Own(props.partner?.jobs?.curDriver as JobDriver_Sex))
                return false;
            // 动作替换或读档数据首次命中后也登记，避免取消后持相同引用的迟到回调漏过。
            Protect(props);
            return true;
        }

        /// <summary>由驱动先核对当前任务归属；取消及读档取消也回收本人的动画和裸绘制状态。</summary>
        internal static void ClearAppearance(Pawn pawn)
        {
            if (pawn == null) return;
            pawn.TryGetComp<Rimworld_Animations.CompExtendedAnimator>()?.StopAnimating();
            var comp = pawn.GetCompRJW();
            if (comp == null) return;
            comp.drawNude = false;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
    }
    [HarmonyPatch(typeof(PregnancyHelper), nameof(PregnancyHelper.impregnate))]
    internal static class FertilityScenePregnancyHook
    {
        /// <summary>在普通受孕及月经前缀之前拦截本场；技能直接调用StartVanillaPregnancy不经过此处。</summary>
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(SexProps props) => !FertilitySceneUtility.BlocksNaturalPregnancy(props);
    }
}
