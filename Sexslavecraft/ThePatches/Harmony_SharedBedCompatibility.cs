using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // SSC 只接入床位查询；不修改 LovePartnerRelationUtility 的任何判断。
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
    public static class Harmony_SSC_CanUseBedNow
    {
        /// <summary>在原版床位检查前，仅调整符合 SSC 许可的奴隶身份参数，随后继续执行原版方法。</summary>
        /// <param name="guestStatusOverride">本次调用的身份覆盖，不会改变角色的实际身份。</param>
        public static void Prefix(Thing bedThing, Pawn sleeper, ref GuestStatus? guestStatusOverride)
        {
            SSCSharedBedUtility.AdjustGuestStatusForPartnerBed(bedThing, sleeper, ref guestStatusOverride);
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.BedOwnerWillShare))]
    public static class Harmony_SSC_BedOwnerWillShare
    {
        /// <summary>保留原版已允许的共享结果；原版拒绝时仅为合格 SSC 对象补充有空归属位的床位许可。</summary>
        /// <param name="__result">原版共享结论，包括原有恋人/配偶许可；本补丁只可能将 false 放宽为 true。</param>
        public static void Postfix(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus, ref bool __result)
        {
            if (!__result && !SSCSharedBedUtility.IsPrisoner(sleeper, guestStatus)
                && SSCSharedBedUtility.HasPartnerBedPermission(bed, sleeper))
                __result = bed.AnyUnownedSleepingSlot;
        }
    }

    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor), new[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) })]
    public static class Harmony_SSC_FindBedFor
    {
        /// <summary>保留医疗和死眠结果；普通休息时优先使用通过原版完整检查的主人或调教员床。</summary>
        /// <param name="__result">原版找到的床；指定对象的床不可用时保持原值。</param>
        /// <remarks>寻路角色、社交检查、预留和身份参数沿用原调用，不自行扫描其他普通床。</remarks>
        public static void Postfix(Pawn sleeper, Pawn traveler, bool checkSocialProperness,
            bool ignoreOtherReservations, GuestStatus? guestStatus, ref Building_Bed __result)
        {
            if (SSCSharedBedUtility.PreserveSpecialRest(sleeper, __result)) return;
            if (SSCSharedBedUtility.TryGetPreferredPartnerBed(sleeper, traveler, checkSocialProperness,
                ignoreOtherReservations, guestStatus, out Building_Bed bed))
                __result = bed;
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.CanAssignTo))]
    public static class Harmony_SSC_BedCanAssignTo
    {
        /// <summary>仅替换原版分配方法中的奴隶身份读取，让合格对象通过该床的身份分类分支。</summary>
        /// <param name="instructions">原版分配检查指令；体型等其他拒绝条件保持原样。</param>
        /// <remarks>额外压入当前床位组件，并转移跳转标签和异常块边界，维持指令流有效。</remarks>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var isSlave = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsSlave));
            var replacement = AccessTools.Method(typeof(SSCSharedBedUtility), nameof(SSCSharedBedUtility.IsSlaveForBedAssignment));
            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(isSlave))
                {
                    yield return instruction;
                    continue;
                }
                // 保留原版方法，唯独将这张床的奴隶分类判断替换为 SSC 的有限例外。
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "get_AssigningCandidates")]
    public static class Harmony_SSC_BedAssigningCandidates
    {
        /// <summary>将地图上符合本床 SSC 许可的殖民地奴隶补入候选列表，并去除重复角色。</summary>
        /// <param name="__instance">正在提供候选角色的床位分配组件。</param>
        /// <param name="__result">原版候选序列；无地图或原序列为空引用时不修改。</param>
        public static void Postfix(CompAssignableToPawn_Bed __instance, ref IEnumerable<Pawn> __result)
        {
            Building_Bed bed = __instance?.parent as Building_Bed;
            if (bed?.Map == null || __result == null) return;
            IEnumerable<Pawn> extra = bed.Map.mapPawns.SlavesOfColonySpawned
                .Where(pawn => SSCSharedBedUtility.HasPartnerBedPermission(bed, pawn));
            __result = __result.Concat(extra).Distinct();
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedRelatedEffects")]
    public static class Harmony_SSC_RecordSharedSleep
    {
        /// <summary>原版确实按睡眠恢复休息值时记录共同睡眠，清醒躺卧或其他床上效果不触发。</summary>
        /// <param name="p">原版效果接收者；保留原参数名供 Harmony 绑定。</param>
        public static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest)
        {
            if (asleep && gainRest) SSCSharedBedMemory.RecordSleep(p, bed);
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
    public static class Harmony_SSC_ApplyBedThoughts
    {
        /// <summary>在原版生成房间心情后，依据本次共同睡眠记录清理符合条件的负面记忆。</summary>
        public static void Postfix(Pawn actor, Building_Bed bed)
        {
            SSCSharedBedMemory.RemoveNegativeRoomMemories(actor, bed);
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "FinalizeLayingJob")]
    public static class Harmony_SSC_FinishSharedSleep
    {
        /// <summary>在原版结束躺卧处理后消费共同睡眠记录，为性奴生成一次睡后记忆。</summary>
        public static void Postfix(Pawn pawn, Building_Bed bed)
        {
            SSCSharedBedMemory.FinishSleep(pawn, bed);
        }
    }
}
