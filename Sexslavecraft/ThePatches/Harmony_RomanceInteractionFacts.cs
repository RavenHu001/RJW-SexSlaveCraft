// AI 辅助生成：求婚结果见证独立于原版心情变更账本。
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(InteractionWorker_MarriageProposal), "Interacted")]
    public static class Harmony_SSCRomanceProposalFacts
    {
        private static bool matched;
        /// <summary>同时检查锚点与实际补丁；被卸载后不得拿旧的安装标志确认拒婚分支。</summary>
        internal static bool Ready => matched && Harmony.GetPatchInfo(AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "Interacted"))?
            .Transpilers.Count(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceProposalFacts)) == 1;
        /// <summary>等栈包装三个原生语句包写入；整体失配保留原版，不增加抽样或重写最终关系。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            var add = AccessTools.Method(typeof(List<RulePackDef>), "Add");
            matched = codes.Count(c => c.Calls(add)) == 3 && codes.Count(c => c.LoadsField(AccessTools.Field(typeof(RulePackDefOf),
                nameof(RulePackDefOf.Sentence_MarriageProposalRejectedBrokeUp)))) == 1;
            if (!matched)
            { SSCLog.WarningImportant("[SSC Romance] 求婚事实锚点不匹配，保留原版并停用该类受控结果确认。"); return codes; }
            foreach (var code in codes.Where(c => c.Calls(add)))
            { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(SSCRomanceMoodEvents), nameof(SSCRomanceMoodEvents.ObserveProposalPack)); }
            return codes;
        }
    }
}
