using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只扩展 RJW 泄欲对象指派中的受虐狂资格，不改变其他受虐狂或奴隶判定。</summary>
    public static class SSCComfortDesignationCompatibility
    {
        public static bool IsMasochistOrSexSlave(Pawn pawn)
        {
            return xxx.is_masochist(pawn) ||
                (SSCIdentityUtility.IsSexSlave(pawn) && SSCIdentityUtility.GetSexSlaveStage(pawn) >= 2);
        }

        public static IEnumerable<CodeInstruction> ReplaceMasochistCheck(
            IEnumerable<CodeInstruction> instructions, string targetName)
        {
            List<CodeInstruction> codes = instructions.ToList();
            MethodInfo original = AccessTools.Method(typeof(xxx), nameof(xxx.is_masochist));
            MethodInfo replacement = AccessTools.Method(typeof(SSCComfortDesignationCompatibility),
                nameof(IsMasochistOrSexSlave));
            if (original == null || replacement == null || codes.Count(code => code.Calls(original)) != 1)
            {
                Log.Warning("[SSC] RJW comfort designation patch skipped: unexpected " + targetName + " layout.");
                return codes;
            }

            foreach (CodeInstruction code in codes)
                if (code.Calls(original)) code.operand = replacement;
            return codes;
        }
    }

    [HarmonyPatch(typeof(PawnDesignations_Comfort), nameof(PawnDesignations_Comfort.UpdateCanDesignateComfort))]
    public static class Harmony_RJWComfortDesignationEligibility
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SSCComfortDesignationCompatibility.ReplaceMasochistCheck(
                instructions, nameof(PawnDesignations_Comfort.UpdateCanDesignateComfort));
        }
    }

    [HarmonyPatch(typeof(PawnDesignations_Comfort), nameof(PawnDesignations_Comfort.IsDesignatedComfort))]
    public static class Harmony_RJWComfortDesignationRetention
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return SSCComfortDesignationCompatibility.ReplaceMasochistCheck(
                instructions, nameof(PawnDesignations_Comfort.IsDesignatedComfort));
        }
    }
}
