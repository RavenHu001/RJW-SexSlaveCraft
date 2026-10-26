// AI 辅助生成：本机原版已核对调用点的等栈包装；失配保留原流程并停用综合接管。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCRomanceNativeMoodHooks
    {
        internal static readonly MethodInfo DefGain = AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory", new[] { typeof(ThoughtDef), typeof(Pawn), typeof(Precept) });
        internal static readonly MethodInfo ObjectGain = AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory", new[] { typeof(Thought_Memory), typeof(Pawn) });
        internal static readonly MethodInfo MergeMethod = AccessTools.Method(typeof(Thought_Memory), "TryMergeWithExistingMemory");
        internal static readonly HashSet<MethodBase> Matched = new HashSet<MethodBase>();

        /// <summary>同时核对实际登记和锚点；外部卸载任何关键观察后不沿用旧能力标志。</summary>
        internal static bool Ready => SSCRomanceNativeMood.StageReady && Harmony_SSCRomanceMoodAdmissionTail.Ready &&
            Installed(DefGain, typeof(Harmony_SSCRomanceNativeDefinition), true) &&
            Installed(ObjectGain, typeof(Harmony_SSCRomanceNativeMemory), true) &&
            Installed(MergeMethod, typeof(Harmony_SSCRomanceNativeRenew), false) &&
            Harmony_SSCRomanceCommandWorker.TargetMethods().All(m => Installed(m, typeof(Harmony_SSCRomanceNativeWorker), false) &&
                Has(m, typeof(Harmony_SSCRomanceMoodWorker), "frame"));

        private static bool Installed(MethodBase method, Type hook, bool frames) => Matched.Contains(method) &&
            Has(method, hook, "il") && (!frames || Has(method, hook, "frame"));
        private static bool Has(MethodBase method, Type hook, string kind)
        {
            var patches = Harmony.GetPatchInfo(method);
            return kind == "il" ? patches?.Transpilers.Count(p => p.PatchMethod.DeclaringType == hook) == 1 :
                patches?.Prefixes.Count(p => p.PatchMethod.DeclaringType == hook) == 1 &&
                patches.Postfixes.Count(p => p.PatchMethod.DeclaringType == hook) == 1 && patches.Finalizers.Count(p => p.PatchMethod.DeclaringType == hook) == 1;
        }

        /// <summary>按实际调用次数整批替换，保留标签和异常标记；不允许安装部分变更账本。</summary>
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase target,
            params (MethodInfo From, string To, int Count)[] replacements)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            bool valid = !codes.Any(c => c.blocks.Count != 0) && replacements.All(r => codes.Count(c => c.Calls(r.From)) == r.Count);
            Matched.Remove(target);
            if (!valid) return codes;
            foreach (var replacement in replacements)
                foreach (var code in codes.Where(c => c.Calls(replacement.From)))
                { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(SSCRomanceNativeMood), replacement.To); }
            Matched.Add(target); return codes;
        }
    }

    [HarmonyPatch]
    internal static class Harmony_SSCRomanceNativeWorker
    {
        private static IEnumerable<MethodBase> TargetMethods() => Harmony_SSCRomanceCommandWorker.TargetMethods();
        /// <summary>仅包装三个原 worker 的已知调用点；求婚附带分手通过原生语句包观察，不重抽随机。</summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            bool proposal = __originalMethod.DeclaringType == typeof(InteractionWorker_MarriageProposal);
            int count = proposal ? 2 : __originalMethod.DeclaringType == typeof(InteractionWorker_RomanceAttempt) ? 3 : 1;
            var packAdd = AccessTools.Method(typeof(List<RulePackDef>), "Add");
            if (proposal && (codes.Count(c => c.Calls(packAdd)) != 3 || codes.Count(c => c.LoadsField(
                AccessTools.Field(typeof(RulePackDefOf), nameof(RulePackDefOf.Sentence_MarriageProposalRejectedBrokeUp)))) != 1))
            { SSCRomanceNativeMoodHooks.Matched.Remove(__originalMethod); return codes; }
            var rewritten = SSCRomanceNativeMoodHooks.Rewrite(codes, __originalMethod,
                (SSCRomanceNativeMoodHooks.DefGain, nameof(SSCRomanceNativeMood.GainFromWorker), count)).ToList();
            if (proposal && SSCRomanceNativeMoodHooks.Matched.Contains(__originalMethod))
                foreach (var code in rewritten.Where(c => c.Calls(packAdd)))
                { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(SSCRomanceMoodEvents), nameof(SSCRomanceMoodEvents.ObserveProposalPack)); }
            return rewritten;
        }
    }

    [HarmonyPatch]
    internal static class Harmony_SSCRomanceNativeDefinition
    {
        private static MethodBase TargetMethod() => SSCRomanceNativeMoodHooks.DefGain;
        /// <summary>工厂回调之前占用唯一调用点；每个 Def 调用都压帧屏蔽外层身份。</summary>
        private static void Prefix(MemoryThoughtHandler __instance, ThoughtDef __0, Pawn __1, out SSCRomanceNativeMood.DefinitionCall __state)
            => __state = SSCRomanceNativeMood.EnterDefinition(__instance, __0, __1);
        private static void Postfix() { }
        /// <summary>正常、跳过及异常均释放临时身份，不吞掉原异常。</summary>
        private static void Finalizer(SSCRomanceNativeMood.DefinitionCall __state) => SSCRomanceNativeMood.ExitDefinition(__state);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            => SSCRomanceNativeMoodHooks.Rewrite(instructions, __originalMethod,
                (SSCRomanceNativeMoodHooks.ObjectGain, nameof(SSCRomanceNativeMood.GainObject), 1));
    }

    [HarmonyPatch]
    internal static class Harmony_SSCRomanceNativeMemory
    {
        private static MethodBase TargetMethod() => SSCRomanceNativeMoodHooks.ObjectGain;
        private static void Prefix(MemoryThoughtHandler __instance, Thought_Memory __0, Pawn __1, out SSCRomanceNativeMood.MemoryCall __state)
            => __state = SSCRomanceNativeMood.EnterMemory(__instance, __0, __1);
        private static void Postfix(bool __runOriginal, SSCRomanceNativeMood.MemoryCall __state) { if (__state != null) __state.Ran = __runOriginal; }
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(Exception __exception, SSCRomanceNativeMood.MemoryCall __state)
            => SSCRomanceNativeMood.ExitMemory(__state, __state?.Ran == true, __exception);

        /// <summary>在 SSC 尾部守卫之后包装原调用，不破坏其唯一 merge 锚点和返回栈。</summary>
        [HarmonyPriority(Priority.Last)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            => SSCRomanceNativeMoodHooks.Rewrite(instructions, __originalMethod,
                (SSCRomanceNativeMoodHooks.MergeMethod, nameof(SSCRomanceNativeMood.Merge), 1),
                (AccessTools.Method(typeof(List<Thought_Memory>), "Add"), nameof(SSCRomanceNativeMood.Add), 1),
                (AccessTools.Method(typeof(MemoryThoughtHandler), "RemoveMemory"), nameof(SSCRomanceNativeMood.Remove), 3),
                (SSCRomanceNativeMoodHooks.DefGain, nameof(SSCRomanceNativeMood.GainChild), 1));
    }

    [HarmonyPatch(typeof(Thought_Memory), nameof(Thought_Memory.TryMergeWithExistingMemory))]
    internal static class Harmony_SSCRomanceNativeRenew
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            => SSCRomanceNativeMoodHooks.Rewrite(instructions, __originalMethod,
                (AccessTools.Method(typeof(Thought_Memory), "Renew"), nameof(SSCRomanceNativeMood.Renew), 1));
    }
}
