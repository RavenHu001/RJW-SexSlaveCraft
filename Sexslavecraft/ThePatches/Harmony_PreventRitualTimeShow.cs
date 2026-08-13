using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(LordJob_Ritual), "GetReport")]
    public static class Patch_LordJob_Ritual_GetReport
    {
        public static void Postfix(LordJob_Ritual __instance, ref string __result)
        {
            if (__instance.Ritual != null &&
                __instance.Ritual.behavior != null &&
                __instance.Ritual.behavior.def == SSCDefOf.SSC_BindingRitualBehavior)
            {
                // 移除括号里的时间，只显示仪式名称
                __result = "Ritual".Translate() + ": " + __instance.Ritual.LabelCap;
            }
        }
    }
}
