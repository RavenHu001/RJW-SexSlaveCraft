// AI 辅助生成：实际出生结果旁路结算，外围范围合并HAR递归与月经出生循环。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    internal static class Harmony_FertilityNativeBirth
    {
        /// <summary>原版真实出生参数提供临时亲代与原人格凭据，不固定受孕时资格。</summary>
        internal static void Prefix(Thing birtherThing, Pawn father, out SSCFertilityBirthUtility.Scope __state)
            => __state = SSCFertilityBirthUtility.Begin(birtherThing as Pawn, father, true);
        /// <summary>只认原出生实际执行后的活Pawn结果；Corpse及被替代跳过的调用不纳入。</summary>
        internal static void Postfix(Thing __result, bool __runOriginal, SSCFertilityBirthUtility.Scope __state)
        {
            if (__runOriginal) __state?.Observe(__result as Pawn);
        }
        /// <summary>正常与异常退出都释放；沿原异常返回，不吞掉出生失败。</summary>
        internal static Exception Finalizer(Exception __exception, SSCFertilityBirthUtility.Scope __state)
        { __state?.Finish(__exception); return __exception; }
    }

    [HarmonyPatch(typeof(Hediff_LaborPushing), nameof(Hediff_LaborPushing.PreRemoved))]
    internal static class Harmony_FertilityLaborBirthScope
    {
        /// <summary>包住完整自然分娩，月经在里面循环调用多个出生结果时仍只结算一胎次。</summary>
        internal static void Prefix(Hediff_LaborPushing __instance, out SSCFertilityBirthUtility.Scope __state)
            => __state = SSCFertilityBirthUtility.Begin(__instance.pawn);
        /// <summary>外围出生结束后统一结算并释放，异常不补造完成。</summary>
        internal static Exception Finalizer(Exception __exception, SSCFertilityBirthUtility.Scope __state)
        { __state?.Finish(__exception); return __exception; }
    }

    [HarmonyPatch(typeof(RitualOutcomeEffectWorker_ChildBirth), nameof(RitualOutcomeEffectWorker_ChildBirth.Apply))]
    internal static class Harmony_FertilityRitualBirthScope
    {
        /// <summary>仪式独立发起时也建立整次范围；嵌套于自然分娩时复用其根汇总。</summary>
        internal static void Prefix(LordJob_Ritual jobRitual, out SSCFertilityBirthUtility.Scope __state)
            => __state = SSCFertilityBirthUtility.Begin(jobRitual?.assignments?.FirstAssignedPawn("mother"));
        /// <summary>仪式零结果或异常不发培养；恢复进入前的临时上下文。</summary>
        internal static Exception Finalizer(Exception __exception, SSCFertilityBirthUtility.Scope __state)
        { __state?.Finish(__exception); return __exception; }
    }

    [HarmonyPatch(typeof(rjw.Hediff_HumanlikePregnancy), nameof(rjw.Hediff_HumanlikePregnancy.GiveBirth))]
    internal static class Harmony_FertilityRjwBirth
    {
        /// <summary>RJW预生成不是分娩，只有本次GiveBirth范围的实际放置回调可以标记活产。</summary>
        internal static void Prefix(rjw.Hediff_HumanlikePregnancy __instance, out SSCFertilityBirthUtility.Scope __state)
            => __state = SSCFertilityBirthUtility.Begin(__instance.pawn, __instance.father, true);
        /// <summary>克隆等前缀跳过原出生时没有活产记录；不据调用本身发放培养。</summary>
        internal static Exception Finalizer(Exception __exception, SSCFertilityBirthUtility.Scope __state)
        { __state?.Finish(__exception); return __exception; }

        /// <summary>原RJW忽略放置返回值；只在真实放置成功且婴儿存活后标记，不改原返回与分娩处理。</summary>
        internal static bool Spawn(Pawn baby, Thing mother, IntVec3? positionOverride)
        {
            bool success = PawnUtility.TrySpawnHatchedOrBornPawn(baby, mother, positionOverride);
            if (success) SSCFertilityBirthUtility.Observe(mother as Pawn, baby);
            return success;
        }

        /// <summary>按唯一放置调用替换，原真假返回及所有分娩副作用保持；失配则仅禁用新增奖励。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var original = AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.TrySpawnHatchedOrBornPawn));
            if (codes.Count(c => c.Calls(original)) != 1)
            {
                Log.Error("[SSC] RJW活产接入失败：出生放置调用不唯一；保留原分娩，不补发培养。");
                return codes;
            }
            foreach (var code in codes)
                if (code.Calls(original)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(Harmony_FertilityRjwBirth), nameof(Spawn)); }
            return codes;
        }
    }
}
