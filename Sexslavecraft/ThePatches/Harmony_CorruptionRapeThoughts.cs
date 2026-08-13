using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;
using SexSlaveCraft;

namespace SexSlaveCraft
{
    [HarmonyPatch]
    public static class Patch_CorruptionRapeThoughts
    {
        // 获取堕落的辅助方法
        private static float GetCorruptionLevel(Pawn pawn)
        {
            var need = pawn.needs?.AllNeeds?.FirstOrDefault(n => n.GetType() == typeof(Need_Corruption)) as Need_Corruption;
            return need?.CurLevel ?? 0f;
        }

        // ================================================
        // 补丁1：修改think_about_sex_Victim的masochist和guilty参数
        // ================================================
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AfterSexUtility), nameof(AfterSexUtility.think_about_sex_Victim))]
        public static void think_about_sex_Victim_Prefix(
            Pawn pawn,
            ref bool masochist,
            ref bool guilty)
        {
            float corruption = GetCorruptionLevel(pawn);
            if (corruption >= 0.2f)
            {
                // 堕落值 ≥ 0.2 时，强制设为受虐狂模式
                masochist = true;
                guilty = false;
            }
        }

        // ================================================
        // 补丁2：替换think_about_sex_Victim返回的心情记忆
        // ================================================
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AfterSexUtility), nameof(AfterSexUtility.think_about_sex_Victim))]
        public static void think_about_sex_Victim_Postfix(
            Pawn pawn,
            Pawn partner,
            ref ThoughtDef __result)
        {
            // 如果原方法没有返回记忆（例如非强暴情况），直接返回
            if (__result == null)
                return;

            float corruption = GetCorruptionLevel(pawn);
            if (corruption < 0.2f)
                return;

            // 根据堕落阶段选择对应的心情记忆
            ThoughtDef corruptionThought = null;
            if (corruption >= 0.9f)
                corruptionThought = SexSlaveCraft.SSCDefOf.SSC_CorruptionRape_Stage4;        // +8 心情
            else if (corruption >= 0.7f)
                corruptionThought = SexSlaveCraft.SSCDefOf.SSC_CorruptionRape_Stage3;        // +5 心情
            else if (corruption >= 0.5f)
                corruptionThought = SexSlaveCraft.SSCDefOf.SSC_CorruptionRape_Stage2;        // +3 心情
            else // corruption >= 0.2f
                corruptionThought = SexSlaveCraft.SSCDefOf.SSC_CorruptionRape_Stage1;        // +1 心情

            // 替换返回的记忆
            __result = corruptionThought;
        }

        // ================================================
        // 补丁3：完全控制think_about_sex_Victim_Blame的旁观者逻辑
        // ================================================
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AfterSexUtility), nameof(AfterSexUtility.think_about_sex_Victim_Blame))]
        public static bool think_about_sex_Victim_Blame_Prefix(
            Pawn pawn,
            Pawn partner,
            bool masochist,
            bool zoophile)
        {
            float corruption = GetCorruptionLevel(pawn);

            // 堕落值 < 0.2：执行原版逻辑（正常责罚机制）
            if (corruption < 0.2f)
                return true;

            // 堕落值 0.2 ≤ corruption < 0.7：跳过原版逻辑，不添加任何旁观者记忆
            if (corruption < 0.7f)
                return false;

            // 堕落值 ≥ 0.7：跳过原版逻辑，添加正面旁观者记忆
            if (pawn.Faction != null && pawn.Map != null && !(xxx.is_animal(partner) && zoophile))
            {
                // 遍历符合条件的旁观者（同派系、可见、15格内）
                foreach (Pawn bystander in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
                    .Where(x => !xxx.is_animal(x) && x != pawn && x != partner && !x.Downed && !x.Suspended))
                {
                    // 检查可见性和距离（与原版条件一致）
                    if (pawn.CanSee(bystander) && pawn.Position.DistanceTo(bystander.Position) < 15)
                    {
                        // 为受害者对旁观者添加正面记忆
                        pawn.needs.mood.thoughts.memories.TryGainMemory(
                            SexSlaveCraft.SSCDefOf.SSC_CorruptionRape_VictimToBystanderPositive, 
                            bystander);
                    }
                }
            }

            // 跳过原版方法执行
            return false;
        }
    }
}