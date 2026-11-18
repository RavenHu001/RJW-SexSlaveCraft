// AI 辅助生成：复用原版精神状态、低心情候选、开始信和清除入口。
using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>
    /// 管理单次发作的身份写入、临时身体效果和结束清理。
    /// 原版精神状态负责启动、开始信和退出通知，SSC 在这些入口补齐本场资源的生命周期。
    /// </summary>
    public sealed partial class MentalState_SSCSlaveBreakdown : MentalState
    {
        // 使用绝对游戏 tick。首步存档保留了到期时间，尚未执行 PostStart 的新实例则没有时限。
        private long expiresAt = -1L;
        private long nextBrokenAt;
        private Hediff_SSCSlaveBreakdown effect;
        private bool initialized;
        // 只有从 Unset 写入 Slave 时才认领回退权。已有 Slave 或同场印记分化属于原身份。
        private bool temporaryIdentity;
        private bool personalityInvalidated;
        // 这些引用只在本局有效，读档后重新捕获。人格替换会撤销本场对身份和严重度的写入权。
        private object personalityToken;
        private CompSexSlaveTraining training;
        private Pawn_HealthTracker health;
        // attemptedStart 区分刚尝试且失败的新发作和仍待补齐效果的第一步存档。
        private bool attemptedStart;
        private bool ending;
        private bool ended;

        protected override bool CanEndBeforeMaxDurationNow => false;

        /// <summary>读取原版仍持有的本状态。生成、复制或读档期间，精神状态追踪器可能尚未建立。</summary>
        internal static MentalState_SSCSlaveBreakdown CurrentFor(Pawn pawn)
            => pawn?.mindState?.mentalStateHandler != null ? pawn.MentalState as MentalState_SSCSlaveBreakdown : null;

        /// <summary>原版已安装状态。身份和健康效果就绪后，才由 SSC 确认本次启动。</summary>
        public override void PostStart(string reason)
        {
            attemptedStart = true;
            expiresAt = (long)SSCSlaveBreakdown.Now + SSCSlaveBreakdown.DurationTicks;
            if (!TryInitialize())
            {
                EndCurrentState();
                return;
            }
            // 赋予新发作一次单人候选机会。等原版完成切换且小人空闲后再由行为树尝试。
            openingPhase = OpeningPhase.Solo;
            // Start 确认后才清空真实需求。读档恢复与启动被撤销的场次沿用当前需求值。
            if (pawn.needs?.TryGetNeed<rjw.Need_Sex>() is rjw.Need_Sex sexNeed) sexNeed.CurLevel = 0f;
            SSCSlaveBreakdownGameComponent.Current?.NotifyStarted(pawn);
            base.PostStart(reason);
        }

        // 原版即使在通知回调中已经清除状态，也会向先前构造的对象取信件正文。
        public override TaggedString GetBeginLetterText() => initialized ? base.GetBeginLetterText() : default;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expiresAt, "sscSlaveBreakdownExpiresAt", -1L);
            Scribe_Values.Look(ref nextBrokenAt, "sscSlaveBreakdownNextBrokenAt", 0L);
            Scribe_Values.Look(ref initialized, "sscSlaveBreakdownInitialized", false);
            Scribe_Values.Look(ref temporaryIdentity, "sscSlaveBreakdownTemporaryIdentity", false);
            Scribe_Values.Look(ref personalityInvalidated, "sscSlaveBreakdownPersonalityInvalidated", false);
            // 只恢复本场挂载的具体实例。健康列表由原版单独保存，引用在 Scribe 的交叉解析阶段接回。
            Scribe_References.Look(ref effect, "sscSlaveBreakdownEffect");
            ExposeBehavior();
            ExposeInvitations();
            ExposeTrainingRequest();
        }

        public override void MentalStateTick(int delta)
        {
            CaptureCarrier();
            int now = SSCSlaveBreakdown.Now;
            // 第一步的进行中存档只有到期时间。首次有效刷新补齐身份和效果，并从当前 tick 开始累计。
            if (!CanInitializeOrContinue() ||
                (!initialized && (now >= expiresAt || !TryInitialize())) ||
                !SSCIdentityUtility.IsSexSlave(pawn) || (now < expiresAt && !HasEffect()))
            {
                EndCurrentState();
                return;
            }

            // 先清理未能恢复的邀约，再处理批准，避免失效的邀请仍占着发起者或调教员。
            TickInvitation();
            if (pawn.MentalState != this) return;
            // 已开场的调教可以同步延长状态和 Hediff，用最新期限结算整小时增量。
            TickTrainingRequest(now, delta);
            if (pawn.MentalState != this) return;
            // 到期时健康追踪器可能已移除兜底 Hediff，最后一个完整小时仍由原发作结算。
            AdvanceBroken(Math.Min(now, expiresAt));
            // 添加或修改 FeelingBroken 会进入健康回调，回调可能已经结束或替换了状态。
            if (pawn.MentalState != this) return;
            if (now >= expiresAt) EndCurrentState();
            else
            {
                // 预约未成功安装的任务可能没有结束回调，按实际当前任务编号清理候选。
                RetireMissingBehaviorJob();
                if (pawn.MentalState == this) base.MentalStateTick(delta);
            }
        }

        /// <summary>常规恢复的兜底。原版硬重置或静默替换经各自的 Harmony 入口进入同一清理。</summary>
        public override void PostEnd()
        {
            FinishEnding();
            base.PostEnd();
        }

        // 游戏读档后重新认领同一精神状态的载体，原人格在此前被替换时会显式撤销此入口。
        private void CaptureCarrier()
        {
            // 身体上属于本场的 Hediff 仍需清理，即使原人格已失效。
            health ??= pawn.health;
            if (personalityToken != null || personalityInvalidated) return;
            personalityToken = SSCPersonalityRuntimeUtility.Capture(pawn);
            training = pawn.TryGetComp<CompSexSlaveTraining>();
        }

        private bool SameCarrier() => !personalityInvalidated && !SSCPersonalityRuntimeUtility.IsMutating(pawn) &&
            SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) && training != null &&
            pawn.TryGetComp<CompSexSlaveTraining>() == training && pawn.health == health;

        private bool CanInitializeOrContinue() => !ending && pawn.MentalState == this && SameCarrier() && SSCSlaveBreakdown.IsEligiblePawn(pawn);

        private bool HasEffect() => effect != null && effect.pawn == pawn && health?.hediffSet?.hediffs.Contains(effect) == true;

        // 交叉引用恢复完成后、首次精神状态 tick 之前，已保存的具体实例即可恢复属性。
        // 一旦捕获过凭据，每次检查同时核对人格和载体。
        internal bool OwnsEffect(Hediff_SSCSlaveBreakdown candidate) => !ending && !personalityInvalidated &&
            effect == candidate && (personalityToken == null || SameCarrier());

        internal bool EffectIsActive(Hediff_SSCSlaveBreakdown candidate) => initialized && OwnsEffect(candidate);

        /// <summary>撤销旧人格写入资格，保留对具体身体 Hediff 的清理责任。</summary>
        internal void InvalidatePersonality() => personalityInvalidated = true;

        /// <summary>独立副本继承来源的正式身份。临时赋予的身份只有建立永久身份锁后才随之保留。</summary>
        internal PawnIdentity IdentityForCopy(PawnIdentity current)
        {
            CaptureCarrier();
            return temporaryIdentity && current == PawnIdentity.Slave && SameCarrier() && !SSCIdentityUtility.IsIdentityLocked(pawn)
                ? PawnIdentity.Unset : current;
        }

        private bool TryInitialize()
        {
            CaptureCarrier();
            try
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Hediff_SSCSlaveBreakdown.DefName);
                if (!CanInitializeOrContinue() || def?.hediffClass != typeof(Hediff_SSCSlaveBreakdown)) return false;
                // 只认领本次真正写入的身份。先前的正式身份以及满值分化写入均由其原来源持有。
                if (training.pawnIdentity == PawnIdentity.Unset && !SSCIdentityUtility.TrySetBreakdownIdentity(pawn,
                    () => CanInitializeOrContinue(), () => temporaryIdentity = true)) return false;
                if (!CanInitializeOrContinue() || !SSCIdentityUtility.IsSexSlave(pawn)) return false;

                effect = (Hediff_SSCSlaveBreakdown)HediffMaker.MakeHediff(def, pawn);
                effect.ExpiresAt = expiresAt;
                // 先保存实例引用，再交给原版健康系统。添加过程可因状态或健康回调重入而中止。
                health.AddHediff(effect);
                if (!CanInitializeOrContinue() || !SSCIdentityUtility.IsSexSlave(pawn) || !HasEffect()) return false;
                nextBrokenAt = (long)SSCSlaveBreakdown.Now + GenDate.TicksPerHour;
                // AddHediff 已刷新健康缓存，属性读取会查询 CurStage。确认启动后即可生效。
                initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[SSC] Could not initialize breakdown for {pawn}: {ex}");
                return false;
            }
        }

        private void AdvanceBroken(long through)
        {
            if (!initialized || nextBrokenAt <= 0 || nextBrokenAt > through || !SameCarrier() || pawn.Dead || pawn.Destroyed) return;
            HediffDef broken = DefDatabase<HediffDef>.GetNamedSilentFail("FeelingBroken");
            // 先推进存档游标，再用原版健康接口追加。可重入的健康回调只能结算之后的整小时。
            // RJW 原有严重度上限、恢复与其它来源的增量继续生效。
            while (nextBrokenAt <= through && SameCarrier() && !pawn.Dead && !pawn.Destroyed)
            {
                nextBrokenAt += GenDate.TicksPerHour;
                if (broken != null) HealthUtility.AdjustSeverity(pawn, broken, 0.03f);
            }
        }

        private void EndCurrentState()
        {
            if (pawn.MentalState == this) RecoverFromState();
            else FinishEnding();
        }

        /// <summary>状态清除之前认领结束，为可能重入的原版通知预先写好冷却。</summary>
        internal void BeginEnding()
        {
            if (ending || Scribe.mode != LoadSaveMode.Inactive) return;
            CaptureCarrier();
            ending = true;
            // 只有确认开始过的发作才结算。完成出口先写入成功，原版恢复不会把成功改成中断。
            if (episodeResult == EpisodeResult.None && initialized) episodeResult = EpisodeResult.Failure;
            // 第一步存档里的已启动状态只有到期时间。原版启动通知在 PostStart 前撤销状态时没有到期时间。
            int now = SSCSlaveBreakdown.Now;
            if ((initialized || (!attemptedStart && expiresAt >= 0)) && now >= 0 &&
                pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp)
                comp.RecordSlaveBreakdownCooldown((long)now + SSCSlaveBreakdown.CooldownTicks);
        }

        /// <summary>清除后结算一次。身体效果按实例移除，身份回退另行核对当前人格和永久锁。</summary>
        internal void FinishEnding()
        {
            if (ended || Scribe.mode != LoadSaveMode.Inactive) return;
            BeginEnding();
            ended = true;
            // 先保留应计的 RJW 整小时增量，再清理任务、临时效果以及身份。前一阶段异常仍尝试后续清理。
            try
            {
                AdvanceBroken(Math.Min(SSCSlaveBreakdown.Now, expiresAt));
            }
            finally
            {
                try
                {
                    // 发布仍需原调教员作好感对象。无论回调结果，都释放信件、批准与本场行为。
                    try { PublishEpisodeOutcome(); }
                    finally
                    {
                        try { CancelTrainingRequest(); }
                        finally { StopBehaviorJob(); }
                    }
                }
                finally
                {
                    try { if (HasEffect() && pawn.health == health) health.RemoveHediff(effect); }
                    finally
                    {
                        // 同场分化、建链或换入新人格均可改变最终身份归属。已开始的新发作也拥有自己的身份。
                        if (temporaryIdentity && SameCarrier() && pawn.MentalState is not MentalState_SSCSlaveBreakdown &&
                            SSCIdentityUtility.IsSexSlave(pawn) && !SSCIdentityUtility.IsIdentityLocked(pawn))
                            SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset);
                    }
                }
            }
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

    // 普通 Recover 和硬重置共用原版清除入口。Prefix 在退出信号前写冷却，Finalizer 在实际清除后清理。
    [HarmonyPatch(typeof(MentalStateHandler), "ClearMentalStateDirect")]
    internal static class Patch_SSCSlaveBreakdownClear
    {
        internal static void Prefix(MentalStateHandler __instance, out MentalState_SSCSlaveBreakdown __state)
        {
            __state = __instance.CurState as MentalState_SSCSlaveBreakdown;
            __state?.BeginEnding();
        }

        internal static void Finalizer(MentalStateHandler __instance, MentalState_SSCSlaveBreakdown __state)
        {
            if (__state != null && __instance.CurState != __state) __state.FinishEnding();
        }
    }

    // Pawn.Kill 会在死亡后直接丢弃 mindState，绕过 ClearMind。趁追踪器仍在时通过原版 Reset 收口。
    [HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.RemoveComponentsOnKilled))]
    internal static class Patch_SSCSlaveBreakdownDeath
    {
        internal static void Prefix(Pawn pawn)
        {
            var handler = pawn.mindState?.mentalStateHandler;
            // Pawn.MentalState 会对死亡 Pawn 返回 null，此处必须查看仍未卸载的原始槽位。
            if (handler?.CurState is MentalState_SSCSlaveBreakdown) handler.Reset();
        }
    }

    // 静默转换直接替换原版状态。仅在旧实例确实被替换时为它结算，准入失败则保持原发作。
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    internal static class Patch_SSCSlaveBreakdownTransition
    {
        internal static void Prefix(MentalStateHandler __instance, bool transitionSilently, out MentalState_SSCSlaveBreakdown __state)
            => __state = transitionSilently ? __instance.CurState as MentalState_SSCSlaveBreakdown : null;

        internal static void Finalizer(MentalStateHandler __instance, MentalState_SSCSlaveBreakdown __state)
        {
            if (__state != null && __instance.CurState != __state) __state.FinishEnding();
        }
    }
}
