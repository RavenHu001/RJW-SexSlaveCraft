// AI 辅助生成：只为装备本代丝鞭的肉体目标选择可用纳米工作端，不修改全局动作权重。
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCPnaSpacerMeleeUtility
    {
        // 同时核对专用 Verb、近战装备、处理器类型和有效剂量配置；不依赖武器名称
        // 或已经废弃的品质标记。品质完全走原版属性，工作端识别只关心投送机制。
        internal static bool IsWorkingEnd(Verb verb) => verb is Verb_MeleeAttackSSCPnaSpacer &&
            verb.EquipmentSource != null && verb.EquipmentSource.def.IsMeleeWeapon &&
            verb.verbProps?.meleeDamageDef?.workerClass != null &&
            typeof(DamageWorker_SSCPnaSpacer).IsAssignableFrom(verb.verbProps.meleeDamageDef.workerClass) &&
            verb.verbProps.meleeDamageDef.GetModExtension<SSCPnaSpacerExtension>()?.IsValid == true;

        internal static Verb Preferred(Pawn pawn, Thing target)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || !SSCPnaExposureUtility.IsValidTarget(target as Pawn)) return null;
            // 只检查当前主手，不持久缓存动作或武器；卸装、换武器和失效配置即时放行原版。
            var weapon = pawn?.equipment?.Primary;
            if (weapon?.def.IsMeleeWeapon != true) return null;
            var verbs = weapon.TryGetComp<CompEquippable>()?.AllVerbs;
            if (verbs == null) return null;
            // 原版可用性和目标准入仍有效；工作端不可用时不强行制造一个可执行动作。
            foreach (Verb verb in verbs)
                if (IsWorkingEnd(verb) && verb.EquipmentSource == weapon &&
                    verb.IsStillUsableBy(pawn) && verb.IsUsableOn(target)) return verb;
            return null;
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    internal static class Harmony_SSCPnaSpacerExplicitMelee
    {
        // 有些任务直接传入拳击／柄击而跳过正常选择；仍优先当前主手的可用纳米端。
        // 只替换该次普通近战参数，不把射击等非近战动作转换为鞭击；null 走正常选择入口。
        internal static void Prefix(Pawn_MeleeVerbs __instance, Thing target, ref Verb verbToUse)
        {
            if (verbToUse?.verbProps == null || !verbToUse.IsMeleeAttack) return;
            Verb preferred = SSCPnaSpacerMeleeUtility.Preferred(__instance.Pawn, target);
            if (preferred != null) verbToUse = preferred;
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
    internal static class Harmony_SSCPnaSpacerMeleeSelection
    {
        // 在原版缓存和随机选择前给出本次专用结果；不把拳击缓存改写为全局优先级。
        // 有合格工作端才短路原版；打空、冷却与真正伤害仍由返回的 Verb 执行。
        // 非敌对有效链目标的工作端免闪避由独立 Cooperation 补丁处理，其他目标沿用原版闪避。
        internal static bool Prefix(Pawn_MeleeVerbs __instance, Thing target, ref Verb __result)
        {
            Verb preferred = SSCPnaSpacerMeleeUtility.Preferred(__instance.Pawn, target);
            if (preferred == null) return true;
            __result = preferred;
            return false;
        }
    }
}
