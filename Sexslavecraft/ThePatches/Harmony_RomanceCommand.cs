// AI 辅助生成
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
    /// <summary>记录必要 IL 适配是否成功；命令缺少任一关键守卫时拒绝执行，普通互动沿原回退契约。</summary>
    public static class SSCRomanceCommandHooks
    {
        internal static bool common, romance, proposal, willingness;
        public static bool Ready => common && romance && proposal && willingness &&
            TrackerReady() && Harmony_SSCRomanceAcceptance.TargetMethods().All(m => m != null &&
                Harmony.GetPatchInfo(m)?.Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceAcceptance)) == true) &&
            Harmony.GetPatchInfo(AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance"))?.Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceCommandWillingness)) == true &&
            Harmony_SSCRomanceCommandWorker.TargetMethods().All(m => m != null &&
                Harmony.GetPatchInfo(m)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceCommandWorker)) == true &&
                Harmony.GetPatchInfo(m)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceCommandWorker)) == true);
        /// <summary>能力标志之外也检查实际补丁仍在，外部卸载守卫后不能沿用旧的安装成功值。</summary>
        private static bool TrackerReady()
        {
            var patches = Harmony.GetPatchInfo(AccessTools.Method(typeof(Pawn_InteractionsTracker), "TryInteractWith"));
            return patches?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceInteraction)) == true &&
                patches.Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceInteraction)) &&
                patches.Finalizers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceInteraction));
        }
    }
    [HarmonyPatch]
    public static class Harmony_SSCRomanceCommandWorker
    {
        /// <summary>命令观察、接受判定和心情观察共用原版互动签名。四个 out 参数按引用类型匹配。</summary>
        internal static MethodInfo ResolveInteraction(Type type)
        {
            var method = AccessTools.DeclaredMethod(type, "Interacted", new[]
            {
                typeof(Pawn), typeof(Pawn), typeof(List<RulePackDef>), typeof(string).MakeByRefType(),
                typeof(string).MakeByRefType(), typeof(LetterDef).MakeByRefType(), typeof(LookTargets).MakeByRefType()
            });
            return method?.IsStatic == false && method.ReturnType == typeof(void) ? method : null;
        }

        /// <summary>保留每个预期目标的位置。缺少其中一个时跳过本组，运行时就绪检查也能识别缺项。</summary>
        internal static bool PrepareTargets(IEnumerable<MethodBase> targets)
        {
            if (targets.All(method => method != null)) return true;
            Log.WarningOnce("[SSC Romance] Required interaction signature unavailable. Related hooks skipped.", 0x53535249);
            return false;
        }

        public static bool Prepare() => PrepareTargets(TargetMethods());

        /// <summary>精确观察原生三个 worker，不把其他模组关系写入当作本次命令已结算。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(InteractionWorker_RomanceAttempt), typeof(InteractionWorker_MarriageProposal), typeof(InteractionWorker_Breakup) })
                yield return ResolveInteraction(type);
        }
        /// <summary>只记录当前命令帧的第一次 worker；普通社交没有持久记录。</summary>
        public static void Prefix(Pawn initiator, Pawn recipient, MethodBase __originalMethod, out SSCRomanceCommandRecord __state)
            => __state = SSCRomanceExecutionScope.WorkerEnter(initiator, recipient, Harmony_SSCRomanceRandomSelection.ActionFor(__originalMethod.DeclaringType));
        /// <summary>原方法正常执行完成才确认；被其他 Prefix 跳过不能报告完整原生结算。</summary>
        public static void Postfix(bool __runOriginal, SSCRomanceCommandRecord __state)
        { if (__runOriginal) SSCRomanceExecutionScope.WorkerExit(__state); }
    }
    [HarmonyPatch(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.AcceptanceChance))]
    public static class Harmony_SSCRomanceCommandWillingness
    {
        /// <summary>只包装求婚公式第一个（发起者）意愿，原版目标意愿及数值公式全部保留。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            var willing = AccessTools.Method(typeof(IdeoUtility), nameof(IdeoUtility.DoerWillingToDo), new[] { typeof(HistoryEvent) });
            var indices = codes.Select((c, i) => c.Calls(willing) ? i : -1).Where(i => i >= 0).ToArray();
            SSCRomanceCommandHooks.willingness = indices.Length == 2 && indices[0] + 3 < codes.Count &&
                (codes[indices[0] + 1].opcode == OpCodes.Brfalse || codes[indices[0] + 1].opcode == OpCodes.Brfalse_S) &&
                codes[indices[0] + 2].opcode == OpCodes.Ldloc_0 && indices[1] == indices[0] + 3;
            if (!SSCRomanceCommandHooks.willingness)
            { SSCLog.WarningImportant("[SSC Romance] 求婚发起者意愿锚点不匹配，点名命令已停用。"); return codes; }
            codes.InsertRange(indices[0] + 1, new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SSCRomanceExecutionScope), nameof(SSCRomanceExecutionScope.InitiatorWilling))) });
            return codes;
        }
    }
}
