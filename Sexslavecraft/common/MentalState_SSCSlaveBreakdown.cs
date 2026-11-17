// AI 辅助生成：复用原版精神状态、低心情候选、开始信和清除入口。
using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed class MentalState_SSCSlaveBreakdown : MentalState
    {
        private long expiresAt;
        private object personalityToken;
        private bool ended;

        protected override bool CanEndBeforeMaxDurationNow => false;

        public override void PostStart(string reason)
        {
            expiresAt = (long)SSCSlaveBreakdown.Now + SSCSlaveBreakdown.DurationTicks;
            personalityToken = SSCPersonalityRuntimeUtility.Capture(pawn);
            SSCSlaveBreakdownGameComponent.Current?.NotifyStarted(pawn);
            base.PostStart(reason);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expiresAt, "sscSlaveBreakdownExpiresAt", -1L);
        }

        public override void MentalStateTick(int delta)
        {
            // 人格 token 仅在进程内有意义。原版恢复状态后，再绑定到已加载的角色。
            personalityToken ??= SSCPersonalityRuntimeUtility.Capture(pawn);
            if (SSCSlaveBreakdown.Now >= expiresAt || !SSCSlaveBreakdown.IsEligiblePawn(pawn) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken))
                RecoverFromState();
            else base.MentalStateTick(delta);
        }

        public override void PostEnd()
        {
            NotifyEnded();
            base.PostEnd();
        }

        internal void NotifyEnded()
        {
            if (ended || Scribe.mode != LoadSaveMode.Inactive) return;
            ended = true;
            int now = SSCSlaveBreakdown.Now;
            if (now >= 0 && pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp)
                comp.SlaveBreakdownCooldownUntil = (long)now + SSCSlaveBreakdown.CooldownTicks;
        }
    }

    public sealed class MentalStateWorker_SSCSlaveBreakdown : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn) => SSCSlaveBreakdown.CanStart(pawn) && base.StateCanOccur(pawn);
    }

    public sealed class MentalBreakWorker_SSCSlaveBreakdown : MentalBreakWorker
    {
        public override bool BreakCanOccur(Pawn pawn)
            => Patch_SSCSlaveBreakdownMoodSelection.IsSelectingFor(pawn) &&
               SSCSlaveBreakdownGameComponent.Current is SSCSlaveBreakdownGameComponent runtime &&
               !runtime.HasFullRequest(pawn) && !SSCFullCorruptionImprint.IsSettling(pawn, SSCSlaveBreakdown.GetNeed(pawn)) &&
               base.BreakCanOccur(pawn);

        public override bool TryStart(Pawn pawn, string reason, bool causedByMood)
            => causedByMood && BreakCanOccur(pawn) &&
               SSCSlaveBreakdownGameComponent.Current.TryStart(pawn, reason, causedByMood: true);
    }

    // 原版读书和异常尸体也使用通用候选。仅在本人的实际低心情抽选中开放三个 Def。
    [HarmonyPatch(typeof(MentalBreaker), nameof(MentalBreaker.TryDoRandomMoodCausedMentalBreak))]
    internal static class Patch_SSCSlaveBreakdownMoodSelection
    {
        [ThreadStatic] private static Pawn selectingFor;

        internal static bool IsSelectingFor(Pawn pawn) => ReferenceEquals(selectingFor, pawn);

        internal static void Prefix(Pawn ___pawn, out Pawn __state)
        {
            __state = selectingFor;
            selectingFor = ___pawn;
        }

        internal static void Finalizer(Pawn __state) => selectingFor = __state;
    }

    // 普通 Recover 和倒地、离图等硬重置共用原版清除入口。在其回调前写好冷却。
    [HarmonyPatch(typeof(MentalStateHandler), "ClearMentalStateDirect")]
    internal static class Patch_SSCSlaveBreakdownClear
    {
        internal static void Prefix(MentalStateHandler __instance)
        {
            if (__instance.CurState is MentalState_SSCSlaveBreakdown state) state.NotifyEnded();
        }
    }

    // 静默转换替换状态时跳过 Recover，成功替换后仍为原发作记账。
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    internal static class Patch_SSCSlaveBreakdownTransition
    {
        internal static void Prefix(MentalStateHandler __instance, bool transitionSilently, out MentalState_SSCSlaveBreakdown __state)
            => __state = transitionSilently ? __instance.CurState as MentalState_SSCSlaveBreakdown : null;

        internal static void Finalizer(MentalStateHandler __instance, MentalState_SSCSlaveBreakdown __state)
        {
            if (__state != null && __instance.CurState != __state) __state.NotifyEnded();
        }
    }
}
