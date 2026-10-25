// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Need_Corruption), nameof(Need_Corruption.NeedInterval))]
    public static class Harmony_SSCRomanceMoodCorruption
    {
        /// <summary>需求更新后的当前值与当前链阶段才是依据；冻结需求仍可检查特质变化。</summary>
        public static void Postfix(Pawn ___pawn) => SSCRomanceMoodRefresh.Notify(___pawn);
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Harmony_SSCRomanceMoodLog
    {
        /// <summary>唯一构造锚点替换为等栈工厂；缺锚点只保留原版日志，不影响浪漫机制。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            var constructor = AccessTools.Constructor(typeof(PlayLogEntry_Interaction), new[] { typeof(InteractionDef), typeof(Pawn), typeof(Pawn), typeof(List<RulePackDef>) });
            var matches = codes.Where(c => c.opcode == OpCodes.Newobj && Equals(c.operand, constructor)).ToList();
            if (matches.Count != 1) { SSCLog.WarningImportant("[SSC Romance] 心情日志构造锚点不匹配，保留原版日志。"); return codes; }
            matches[0].opcode = OpCodes.Call; matches[0].operand = AccessTools.Method(typeof(SSCRomanceMoodLog), nameof(SSCRomanceMoodLog.Create));
            return codes;
        }
    }

    [HarmonyPatch]
    public static class Harmony_SSCRomanceMoodWorker
    {
        /// <summary>与现有命令观察相同的三个原 worker；不扩大到模组未知恋爱行为。</summary>
        public static IEnumerable<MethodBase> TargetMethods() => Harmony_SSCRomanceCommandWorker.TargetMethods();
        public sealed class State { internal SSCRomanceMoodFrame Frame; internal bool OriginalRan; }
        /// <summary>每个 worker 建立独立帧；直接调用没有共同入口快照便不生成心情。</summary>
        public static void Prefix(Pawn initiator, Pawn recipient, MethodBase __originalMethod, out State __state)
            => __state = new State { Frame = SSCRomanceMoodEvents.EnterWorker(SSCRomanceExecutionScope.MoodFrame, initiator, recipient,
                Harmony_SSCRomanceRandomSelection.ActionFor(__originalMethod.DeclaringType)) };
        /// <summary>先记录原方法确实运行；最终异常状态交给 Finalizer 判断。</summary>
        public static void Postfix(bool __runOriginal, State __state) { if (__state != null) __state.OriginalRan = __runOriginal; }
        /// <summary>最终确认或无条件释放；不吞掉游戏／外部补丁异常。</summary>
        [HarmonyPriority(Priority.Last)]
        public static void Finalizer(Exception __exception, State __state)
        { if (__state != null) SSCRomanceMoodEvents.Finish(__state.Frame, __state.OriginalRan, __exception); }
    }

    [HarmonyPatch(typeof(Thought_Memory), nameof(Thought_Memory.TryMergeWithExistingMemory))]
    public static class Harmony_SSCRomanceMoodLoss
    {
        /// <summary>原版准入已通过且合并正常完成；同次新增和刷新均可识别，拒绝准入不能误去重。</summary>
        public static void Postfix(Thought_Memory __instance, bool __runOriginal)
        { if (__runOriginal) SSCRomanceMoodEvents.ObserveAdmittedLoss(__instance); }
    }
}
