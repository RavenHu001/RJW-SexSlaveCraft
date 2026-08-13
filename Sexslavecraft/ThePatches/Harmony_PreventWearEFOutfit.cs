using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) }, new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class Patch_SexSlaveApparelRestriction
    {
        [HarmonyPrefix]
        public static bool Prefix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            // 1. 类型安全检查：由于 CanEquip 也会检查武器，我们先确认这是不是我们要限制的那件衣服
            if (!(thing is Apparel apparel) || apparel.def != SSCDefOf.EFOutfit)
            {
                return true; // 不是目标衣服，放行
            }

            // 2. The final outfit follows the authoritative chain health stage.
            if (SSCIdentityUtility.GetSexSlaveStage(pawn) < 4)
            {
                // 3. 设置 UI 上的反馈信息（当玩家右键点击衣服时会显示这个原因）
                // 修改：使用本地化字符串
                cantReason = Strings.EquipRestriction_NotCorruptedEnough(pawn.LabelShort);

                // 4. 返回逻辑结果
                __result = false;

                // 5. 拦截原方法，不执行后续逻辑
                return false;
            }

            return true; // 符合条件，继续执行原版逻辑
        }
    }
}
