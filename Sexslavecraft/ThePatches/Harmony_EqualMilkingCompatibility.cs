using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class EqualMilkingCompatibility
    {
        /// <summary>外部查询和原生挤取兼容入口都挂接成功后，才允许查询后置补充 SSC 泌乳状态。</summary>
        internal static bool Installed { get; private set; }

        // 检测Equal Milking是否加载
        public static bool IsEqualMilkingLoaded()
        {
            return ModLister.GetActiveModWithIdentifier("akaster.equalmilking") != null
                || ModLister.GetActiveModWithIdentifier("EqualMilking") != null
                || Type.GetType("EqualMilking.Helpers.ExtensionHelper, EqualMilking") != null;
        }

        internal static void Register(Harmony harmony)
        {
            Installed = false;
            Installed = SSCOptionalPatchGroup.Install(harmony, "Equal Milking", () =>
            {
                var helper = SoftDependencyUtility.FindOptionalType("EqualMilking.Helpers.ExtensionHelper");
                // 模组已启用但 Helper 缺失时继续走签名检查，留下可辨认的失配日志。
                if (helper == null && !IsEqualMilkingLoaded()) return null;
                // 当前程序集使用全局类型名，同时接受旧命名空间。用真实组件类型核对返回签名。
                var comp = helper?.Assembly.GetType("HediffComp_EqualMilkingLactating")
                    ?? helper?.Assembly.GetType("EqualMilking.HediffComp_EqualMilkingLactating");
                if (comp == null || !typeof(HediffComp).IsAssignableFrom(comp))
                    throw new TypeLoadException("HediffComp_EqualMilkingLactating");
                return new[]
                {
                    (SSCOptionalPatchGroup.RequireTarget(helper, "IsLactating", typeof(bool), true, typeof(Pawn)), typeof(Patch_IsLactating), HarmonyPatchType.Postfix),
                    (SSCOptionalPatchGroup.RequireTarget(helper, "LactatingHediff", typeof(Hediff), true, typeof(Pawn)), typeof(Patch_LactatingHediff), HarmonyPatchType.Postfix),
                    (SSCOptionalPatchGroup.RequireTarget(helper, "LactatingHediffComp", comp, true, typeof(Pawn)), typeof(Patch_LactatingHediffComp), HarmonyPatchType.Postfix),
                    (SSCOptionalPatchGroup.RequireTarget(typeof(HediffComp_Chargeable), "GreedyConsume", typeof(float), false, typeof(float)), typeof(Patch_GreedyConsume), HarmonyPatchType.Prefix)
                };
            });
        }
    }

    // ==============================================================
    // 核心补丁：让IsLactating()识别SSC_Lactating_SubState
    // ==============================================================
    public static class Patch_IsLactating
    {
        public static void Postfix([HarmonyArgument(0)] Pawn pawn, ref bool __result)
        {
            try
            {
                if (EqualMilkingCompatibility.Installed && !__result && pawn != null && !HumanCattleBridgeUtility.IsLoaded)
                {
                    // 检查是否有SSC_Lactating_SubState hediff
                    __result = pawn.health?.hediffSet?.HasHediff(global::SexSlaveCraft.SSCDefOf.SSC_Lactating_SubState) ?? false;
                }
            }
            catch
            {
                // 静默失败，不影响原有功能
            }
        }
    }

    // ==============================================================
    // 扩展补丁：让LactatingHediff()能获取SSC_Lactating_SubState
    // ==============================================================
    public static class Patch_LactatingHediff
    {
        public static void Postfix([HarmonyArgument(0)] Pawn pawn, ref Hediff __result)
        {
            try
            {
                if (EqualMilkingCompatibility.Installed && __result == null && pawn != null && !HumanCattleBridgeUtility.IsLoaded)
                {
                    __result = pawn.health?.hediffSet?.GetFirstHediffOfDef(global::SexSlaveCraft.SSCDefOf.SSC_Lactating_SubState);
                }
            }
            catch
            {
                // 静默失败，不影响原有功能
            }
        }
    }

    // ==============================================================
    // 扩展补丁：让LactatingHediffComp()能获取SSC_Lactating_SubState的comp
    // 返回null不影响挤奶机核心功能，但可以避免某些UI错误
    // ==============================================================
    public static class Patch_LactatingHediffComp
    {
        public static void Postfix([HarmonyArgument(0)] Pawn pawn, ref object __result)
        {
            try
            {
                if (EqualMilkingCompatibility.Installed && __result == null && pawn != null && !HumanCattleBridgeUtility.IsLoaded)
                {
                    var hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(global::SexSlaveCraft.SSCDefOf.SSC_Lactating_SubState);
                    if (hediff != null)
                    {
                        // 返回null，避免类型不匹配导致的错误
                        // 挤奶机核心功能不依赖此comp，仅某些UI需要
                        __result = null;
                    }
                }
            }
            catch
            {
                // 静默失败，不影响原有功能
            }
        }
    }

    // ==============================================================
    // 可选补丁：处理GreedyConsume的兼容性（如果未来需要）
    // ==============================================================
    public static class Patch_GreedyConsume
    {
        public static bool Prefix(HediffComp_Chargeable __instance)
        {
            try
            {
                // 如果已经是Equal Milking的comp，让原补丁处理
                if (__instance.GetType().FullName == "EqualMilking.HediffComp_EqualMilkingLactating")
                    return true;

                // 如果是我们的comp，提供基本兼容逻辑
                if (__instance.GetType().Name == "HediffComp_PermanentLactating")
                {
                    // 简单实现：直接使用原版逻辑
                    // 因为我们不依赖Equal Milking的复杂转换，所以返回true使用原版方法
                    return true;
                }
            }
            catch
            {
                // 静默失败，回退到原版逻辑
            }
            return true;
        }
    }
}
