// AI 辅助生成：SSC 已完成准入后局部合并时结束原生主体，防止二次容量清理或关联副作用。
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), typeof(Thought_Memory), typeof(Pawn))]
    internal static class Harmony_SSCRomanceMoodAdmissionTail
    {
        private static bool matched;
        private static readonly MethodInfo target = AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) });
        /// <summary>锚点和补丁登记必须同时有效；动态卸载后新请求技术拒绝，不把未生效的防护当作正常结算。</summary>
        internal static bool Ready => matched && Harmony.GetPatchInfo(target)?.Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceMoodAdmissionTail)) == true;

        /// <summary>只识别本系统两个综合载体且已执行生产合并的实例；其他思想完整执行原生尾部。</summary>
        private static bool Handled(Thought_Memory memory) => memory is Thought_MemorySSCRomance romance &&
            SSCRomanceMoodGroups.GroupOf(romance) != SSCRomanceMoodGroup.None && romance.MergeResult != null;

        /// <summary>保留原 CanGetThought、人物绑定及虚方法准入；只在唯一合并调用后插入带平衡栈的早退。</summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var merge = AccessTools.Method(typeof(Thought_Memory), nameof(Thought_Memory.TryMergeWithExistingMemory));
            var matches = codes.Select((code, index) => (code, index)).Where(x => x.code.Calls(merge)).Select(x => x.index).ToArray();
            matched = matches.Length == 1 && !codes.Any(c => c.blocks.Count != 0) && matches[0] + 1 < codes.Count &&
                (codes[matches[0] + 1].opcode == OpCodes.Brtrue || codes[matches[0] + 1].opcode == OpCodes.Brtrue_S);
            if (!matched) return codes;
            int next = matches[0] + 1;
            var resume = generator.DefineLabel();
            var start = new CodeInstruction(OpCodes.Ldarg_1);
            start.labels.AddRange(codes[next].labels); codes[next].labels.Clear(); codes[next].labels.Add(resume);
            // 原合并的 bool 仍在栈上；普通思想交给原 brtrue 消费，SSC 早退则先 pop 再 ret。
            codes.InsertRange(next, new[] { start, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony_SSCRomanceMoodAdmissionTail), nameof(Handled))),
                new CodeInstruction(OpCodes.Brfalse, resume), new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Ret) });
            return codes;
        }
    }
}
