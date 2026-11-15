// AI 辅助生成：两代 PNA 鞭束对非敌对有效链目标免闪避，且不新增本次近战威胁。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal static class SSCPnaWhipCooperationUtility
    {
        /// <summary>即时检查本次实际工作端和目标；不以持鞭、身份标签或玩家阵营代替真实关系。</summary>
        internal static bool Allows(Verb_MeleeAttack verb, Pawn target)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || verb == null) return false;
            ThingWithComps weapon = verb.EquipmentSource;
            Pawn attacker = verb.CasterPawn;
            // 先筛选武器，普通近战不查询健康列表。普通柄击、拳击和离手动作也不享受此规则。
            if (weapon == null || weapon.Destroyed || weapon.def?.IsMeleeWeapon != true ||
                attacker == null || attacker == target || attacker.equipment?.Primary != weapon ||
                !IsWorkingEnd(verb, weapon)) return false;
            // 零阶段有效链沿用共享判定；双方任一方向敌对都保留战斗防御。
            // 此规则不要求挥鞭者就是绑定主人，也不清除目标此前受到其他攻击留下的威胁。
            return SSCPnaExposureUtility.IsValidTarget(target) &&
                !target.HostileTo(attacker) && !attacker.HostileTo(target) &&
                SSCPnaExposureUtility.IsRooted(target);
        }

        private static bool IsWorkingEnd(Verb_MeleeAttack verb, Thing weapon)
        {
            DamageDef damage = verb.verbProps?.meleeDamageDef;
            string capacity;
            if (weapon.def.defName == "SSC_Weapon_PnaShortWhip" && verb is Verb_MeleeAttackSSCPnaWhip &&
                damage?.defName == "SSC_Damage_PnaWhipCut" && damage.workerClass != null &&
                typeof(DamageWorker_SSCPnaCoated).IsAssignableFrom(damage.workerClass))
            {
                var coating = damage.GetModExtension<SSCPnaCoatingExtension>();
                if (coating?.recordHediff == null || !SSCPnaMath.IsFinite(coating.baseLoad) || coating.baseLoad <= 0f)
                    return false;
                capacity = "SSC_ToolCapacity_PnaWhip";
            }
            else if (weapon.def.defName == "SSC_Weapon_PnaSilkWhip" &&
                damage?.defName == "SSC_Damage_PnaSilkWhip" && SSCPnaSpacerMeleeUtility.IsWorkingEnd(verb))
                capacity = "SSC_ToolCapacity_PnaSilkWhip";
            else return false;
            // 核对实际工具能力，避免只因装备或伤害定义名称相同就将普通攻击变为配合受药。
            if (verb.tool?.capacities == null) return false;
            foreach (ToolCapacityDef candidate in verb.tool.capacities)
                if (candidate?.defName == capacity) return true;
            return false;
        }
    }

    internal static class Harmony_SSCPnaWhipCooperativeDodge
    {
        internal static bool Prefix(Verb_MeleeAttack __instance, LocalTargetInfo __0, ref float __result)
        {
            if (!Harmony_SSCPnaWhipCooperativeThreat.Ready || !SSCPnaWhipCooperationUtility.Allows(__instance, __0.Thing as Pawn)) return true;
            // 只跳过目标这一层闪避；GetNonMissChance 和攻击者的随机失手完全沿用原版。
            __result = 0f;
            return false;
        }
    }

    internal static class Harmony_SSCPnaWhipCooperativeThreat
    {
        private static bool installed;
        private static bool matched;
        internal static bool Ready => installed && matched;

        // 两处钩子共同启用；回退时已生成的威胁包装器也执行原版写入。
        internal static void Register(Harmony harmony)
        {
            installed = false;
            MethodInfo cast = AccessTools.DeclaredMethod(typeof(Verb_MeleeAttack), "TryCastShot", Type.EmptyTypes);
            MethodInfo dodge = AccessTools.DeclaredMethod(typeof(Verb_MeleeAttack), "GetDodgeChance", new[] { typeof(LocalTargetInfo) });
            if (cast?.IsStatic != false || cast.ReturnType != typeof(bool) ||
                dodge?.IsStatic != false || dodge.ReturnType != typeof(float))
            {
                Log.WarningOnce("[SSC PNA whip] Unsupported melee method signature. SSC whip cooperation is disabled.", 0x53535753);
                return;
            }

            MethodInfo transpiler = AccessTools.DeclaredMethod(typeof(Harmony_SSCPnaWhipCooperativeThreat), nameof(Transpiler));
            MethodInfo prefix = AccessTools.DeclaredMethod(typeof(Harmony_SSCPnaWhipCooperativeDodge), nameof(Harmony_SSCPnaWhipCooperativeDodge.Prefix));
            try
            {
                harmony.Patch(cast, transpiler: new HarmonyMethod(transpiler));
                harmony.Patch(dodge, prefix: new HarmonyMethod(prefix));
                installed = true;
            }
            catch (Exception error)
            {
                Log.WarningOnce($"[SSC PNA whip] Could not install melee hooks. SSC whip cooperation is disabled. {error.GetBaseException().Message}",
                    0x53535749);
                foreach (var (target, patch) in new[] { (cast, transpiler), (dodge, prefix) })
                {
                    try { harmony.Unpatch(target, patch); }
                    catch (Exception cleanup)
                    {
                        Log.WarningOnce($"[SSC PNA whip] Could not remove {patch.Name}. {cleanup.GetBaseException().Message}",
                            0x53535752);
                    }
                }
            }
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            FieldInfo threat = AccessTools.Field(typeof(Pawn_MindState), nameof(Pawn_MindState.meleeThreat));
            FieldInfo tick = AccessTools.Field(typeof(Pawn_MindState), nameof(Pawn_MindState.lastMeleeThreatHarmTick));
            MethodInfo setThreat = AccessTools.Method(typeof(Harmony_SSCPnaWhipCooperativeThreat), nameof(SetThreat));
            MethodInfo setTick = AccessTools.Method(typeof(Harmony_SSCPnaWhipCooperativeThreat), nameof(SetThreatTick));
            int threatWrites = threat == null ? 0 : codes.Count(c => c.StoresField(threat));
            int tickWrites = tick == null ? 0 : codes.Count(c => c.StoresField(tick));
            matched = threat?.FieldType == typeof(Pawn) && tick?.FieldType == typeof(int) &&
                setThreat != null && setTick != null && threatWrites == 1 && tickWrites == 1;
            if (!matched)
            {
                Log.WarningOnce($"[SSC PNA whip] Unsupported TryCastShot layout (threat writes: {threatWrites}/1, tick writes: {tickWrites}/1). SSC whip cooperation is disabled.",
                    0x5353574C);
                foreach (CodeInstruction code in codes) yield return code;
                yield break;
            }
            foreach (CodeInstruction code in codes)
            {
                MethodInfo setter = code.StoresField(threat) ? setThreat : code.StoresField(tick) ? setTick : null;
                if (setter == null) { yield return code; continue; }
                // 保留栈上的 mindState 和原值，只追加本次 Verb。标签／异常边界随首条指令移动。
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code).MoveBlocksFrom(code);
                yield return new CodeInstruction(OpCodes.Call, setter);
            }
        }

        internal static void SetThreat(Pawn_MindState state, Pawn attacker, Verb_MeleeAttack verb)
        {
            if (!Ready || !SSCPnaWhipCooperationUtility.Allows(verb, state.pawn)) state.meleeThreat = attacker;
        }

        internal static void SetThreatTick(Pawn_MindState state, int tick, Verb_MeleeAttack verb)
        {
            // 不在命中后恢复旧值：伤害回调可在出手返回前查询威胁，必须直接阻止本次登记。
            if (!Ready || !SSCPnaWhipCooperationUtility.Allows(verb, state.pawn)) state.lastMeleeThreatHarmTick = tick;
        }
    }
}
