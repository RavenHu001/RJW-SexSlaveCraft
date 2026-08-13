using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    // =========================================================
    // 🔥 拦截小人的主信息栏，把产出进度强行写进去
    // =========================================================
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Pawn_GetInspectString_Patch
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            // 安全检查：确保小人有健康状态追踪器
            if (__instance.health == null || __instance.health.hediffSet == null) return;

            // 遍历小人身上的所有 Hediff，寻找我们写的产出组件
            foreach (var hediff in __instance.health.hediffSet.hediffs)
            {
                var comp = hediff.TryGetComp<HediffComp_SeverityProduct>();
                if (comp != null && comp.Props.thingToSpawn != null)
                {
                    // 计算百分比
                    string progress = (hediff.Severity * 100f).ToString("F0") + "%";

                    // 获取产物的名字 (比如 "人格凝胶")
                    string productName = comp.Props.thingToSpawn.label;

                    // 换行拼接我们的自定义文本
                    if (!string.IsNullOrEmpty(__result))
                    {
                        __result += "\n"; // 如果原本已经有信息了，先打个回车换行
                    }

                    // 最终显示的格式： 人格凝胶 生产进度: 45%
                    __result += Strings.Inspect_ProductionProgress(productName, progress);
                }
            }
        }
    }
}