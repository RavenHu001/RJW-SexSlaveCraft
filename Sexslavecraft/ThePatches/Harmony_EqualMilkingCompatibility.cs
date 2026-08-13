using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public static class EqualMilkingCompatibility
    {
        // 检测Equal Milking是否加载
        public static bool IsEqualMilkingLoaded()
        {
            return ModLister.GetActiveModWithIdentifier("EqualMilking") != null
                || Type.GetType("EqualMilking.Helpers.ExtensionHelper, EqualMilking") != null;
        }
    }

    // ==============================================================
    // 核心补丁：让IsLactating()识别SSC_Lactating_SubState
    // ==============================================================
    [HarmonyPatch("EqualMilking.Helpers.ExtensionHelper", "IsLactating")]
    public static class Patch_IsLactating
    {
        // 🔥 新增：条件性加载
        public static bool Prepare()
        {
            return EqualMilkingCompatibility.IsEqualMilkingLoaded();
        }
        
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            try
            {
                if (!__result && pawn != null && !HumanCattleBridgeUtility.IsLoaded)
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
    [HarmonyPatch("EqualMilking.Helpers.ExtensionHelper", "LactatingHediff")]
    public static class Patch_LactatingHediff
    {
        // 🔥 新增：条件性加载
        public static bool Prepare()
        {
            return EqualMilkingCompatibility.IsEqualMilkingLoaded();
        }
        
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Hediff __result)
        {
            try
            {
                if (__result == null && pawn != null && !HumanCattleBridgeUtility.IsLoaded)
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
    [HarmonyPatch("EqualMilking.Helpers.ExtensionHelper", "LactatingHediffComp")]
    public static class Patch_LactatingHediffComp
    {
        // 🔥 新增：条件性加载
        public static bool Prepare()
        {
            return EqualMilkingCompatibility.IsEqualMilkingLoaded();
        }
        
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref object __result)
        {
            try
            {
                if (__result == null && pawn != null && !HumanCattleBridgeUtility.IsLoaded)
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
    [HarmonyPatch(typeof(HediffComp_Chargeable), "GreedyConsume")]
    public static class Patch_GreedyConsume
    {
        // 🔥 新增：条件性加载（这个补丁也只在EqualMilking存在时应用）
        public static bool Prepare()
        {
            return EqualMilkingCompatibility.IsEqualMilkingLoaded();
        }
        
        [HarmonyPrefix]
        public static bool Prefix(HediffComp_Chargeable __instance, ref float __result, float desiredCharge)
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
