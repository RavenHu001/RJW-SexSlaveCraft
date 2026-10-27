// AI 辅助生成：已加载／已复制的历史局部收尾，不创建新事件、不恢复命令。
using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原生 CopyNeeds 直接恢复列表；该调用栈内的 CopyFrom 无需留下单条历史恢复标记。</summary>
    internal static class SSCRomanceMoodCopyScope
    {
        [ThreadStatic] private static int depth;
        internal static bool Active => depth != 0;
        /// <summary>支持复制回调内嵌套其他复制；只影响临时模式，不授予事件权限。</summary>
        internal static void Enter() => depth++;
        /// <summary>由 Finalizer 对应释放，原生复制抛异常也不能污染下一次普通 CopyFrom。</summary>
        internal static void Exit() => depth--;
    }
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.ExposeData))]
    internal static class Harmony_SSCRomanceMoodLoadedHistory
    {
        /// <summary>等待引用及原生 owner 重绑完成；保存和 LoadingVars 阶段不整理半成品。</summary>
        private static void Postfix(MemoryThoughtHandler __instance, Pawn ___pawn)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 强制存档回调可能捕获暂入列候选；恢复只丢弃标记实例，不执行待提交计划或补发事件。
                __instance.Memories.RemoveAll(m => m is Thought_MemorySSCRomance pending && pending.CopiedPendingEvent);
                SSCRomanceMoodGroups.NormalizeHistory(__instance, () => ___pawn?.needs?.mood?.thoughts?.memories == __instance);
            }
        }
    }

    [HarmonyPatch(typeof(GameComponent_PawnDuplicator), "CopyNeeds")]
    internal static class Harmony_SSCRomanceMoodCopiedHistory
    {
        internal sealed class State
        {
            internal object Token;
            internal CompSexSlaveTraining Comp;
            internal bool Entered;
        }
        /// <summary>复制前捕获新身体的原上下文，不让兼容回调替换人格后整理接管者的记忆。</summary>
        private static void Prefix(Pawn newPawn, out State __state)
        {
            __state = new State { Token = SSCPersonalityRuntimeUtility.Capture(newPawn), Comp = newPawn?.TryGetComp<CompSexSlaveTraining>() };
            SSCRomanceMoodCopyScope.Enter(); __state.Entered = true;
        }
        /// <summary>原版直接 Add 而不经过 TryGainMemory；复制完成清临时标记并整理整个副本，不改来源。</summary>
        private static void Postfix(Pawn newPawn, State __state)
        {
            var handler = newPawn?.needs?.mood?.thoughts?.memories;
            bool Current() => newPawn != null && SSCPersonalityRuntimeUtility.IsCurrent(newPawn, __state.Token) &&
                newPawn.TryGetComp<CompSexSlaveTraining>() == __state.Comp && newPawn.needs?.mood?.thoughts?.memories == handler;
            if (handler == null || __state == null || !Current()) return;
            // 原生直接复制列表；先删除来源尚待结算的候选，不能先ResetRuntime丢掉临时来源证据。
            handler.Memories.RemoveAll(m => m is Thought_MemorySSCRomance pending && pending.CopiedPendingEvent);
            foreach (var memory in handler.Memories.OfType<Thought_MemorySSCRomance>()) memory.ResetRuntime();
            SSCRomanceMoodGroups.NormalizeHistory(handler, Current);
        }
        /// <summary>复制被跳过、正常完成及任意后置异常均结束本次临时模式。</summary>
        private static void Finalizer(State __state)
        { if (__state?.Entered == true) { __state.Entered = false; SSCRomanceMoodCopyScope.Exit(); } }
    }
}
