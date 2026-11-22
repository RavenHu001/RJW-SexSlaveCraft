// AI 辅助生成：复用原版精神状态、开始信和清除入口。
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
        private int brokenHoursApplied;
        private long legacyNextBrokenAt;
        private Hediff_SSCSlaveBreakdown effect;
        // 即使效果被外部移除，也保留原实例到退出结算结束。准入要再检查实例是否仍挂在当前身体上。
        private int RemainingTicks => effect?.Timer?.ticksToDisappear ?? 0;
        private bool initialized;
        // 记录入场分支。原链日后移除、换主或无法在读档中恢复时，仍需按有链发作退出。
        private bool boundEpisode;
        private Hediff_ChainOfSexSlave boundChain;
        private Pawn boundOwner;
        // 无链场次同样持有入场进度的实际实例。单独保存是否曾有记录，读档时能区分无记录和原记录已丢失。
        private Hediff_SSCOwnerlessSlave startingOwnerless;
        private bool hadOwnerlessRecord;
        // 用于请求选项和额外成长资格。实际结算仍从该实例读取当前严重度。
        private int startingOwnerlessStage;
        // 起始阶段只限制额外成长。原链在发作期间由仪式升阶，可照常结算本场普通调教。
        private int startingChainStage;
        // 只有从 Unset 写入 Slave 时才认领回退权。已有 Slave 或同场印记分化属于原身份。
        private bool temporaryIdentity;
        private bool personalityInvalidated;
        // 这些引用只在本局有效，读档后重新捕获。人格替换会撤销本场对身份和严重度的写入权。
        private object personalityToken;
        private CompSexSlaveTraining training;
        private Pawn_HealthTracker health;
        private bool ending;
        private bool ended;

        protected override bool CanEndBeforeMaxDurationNow => false;

        /// <summary>读取原版仍持有的本状态。生成、复制或读档期间，精神状态追踪器可能尚未建立。</summary>
        internal static MentalState_SSCSlaveBreakdown CurrentFor(Pawn pawn)
            => pawn?.mindState?.mentalStateHandler != null ? pawn.MentalState as MentalState_SSCSlaveBreakdown : null;

        /// <summary>原版已安装状态。身份和健康效果就绪后，才由 SSC 确认本次启动。</summary>
        public override void PostStart(string reason)
        {
            // 原版安装精神状态后还会运行通知。再次核对资格与原绑定，避免将启动转交给回调建立的新关系。
            if (!SSCSlaveBreakdown.IsEligiblePawn(pawn) ||
                SSCSlaveBreakdownGameComponent.Current?.MatchesStartingBinding(pawn) == false)
            {
                EndCurrentState();
                return;
            }
            boundChain = SSCBondUtility.GetChain(pawn);
            boundEpisode = boundChain != null;
            boundOwner = boundChain?.LinkedPawn;
            startingChainStage = TrainingOutcomeUtility.GetSexSlaveProgressStage(boundChain).Stage;
            startingOwnerless = boundEpisode ? null : Hediff_SSCOwnerlessSlave.Find(pawn);
            hadOwnerlessRecord = startingOwnerless != null;
            startingOwnerlessStage = TrainingOutcomeUtility.GetSexSlaveProgressStage(startingOwnerless).Stage;
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

        // 原版可能在 SSC 初始化失败后仍取正文。通过初始化的发作统一使用 Def 原有开场信。
        public override TaggedString GetBeginLetterText() => initialized ? base.GetBeginLetterText() : default;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref brokenHoursApplied, "sscBreakdownBrokenHoursApplied", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyNextBrokenAt, "sscSlaveBreakdownNextBrokenAt", 0L);
            Scribe_Values.Look(ref initialized, "sscSlaveBreakdownInitialized", false);
            Scribe_Values.Look(ref boundEpisode, "sscBreakdownBoundEpisode", false);
            Scribe_Values.Look(ref startingChainStage, "sscBreakdownStartingChainStage", 0);
            // 原版健康列表负责深存锁链。这里只保存同一实例的交叉引用，连同独立的入场分支和起始阶段。
            // 链已离体时写空引用，读档后有链状态据此退出，避免把失去锁链误认成无链发作。
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Hediff_ChainOfSexSlave attachedChain = SSCBondUtility.GetChain(pawn) == boundChain ? boundChain : null;
                Scribe_References.Look(ref attachedChain, "sscBreakdownBoundChain");
            }
            else Scribe_References.Look(ref boundChain, "sscBreakdownBoundChain");
            Scribe_References.Look(ref boundOwner, "sscBreakdownBoundOwner");
            Scribe_Values.Look(ref hadOwnerlessRecord, "sscBreakdownHadOwnerlessRecord", false);
            Scribe_Values.Look(ref startingOwnerlessStage, "sscBreakdownStartingOwnerlessStage", 0);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var attachedOwnerless = Hediff_SSCOwnerlessSlave.Find(pawn) == startingOwnerless ? startingOwnerless : null;
                Scribe_References.Look(ref attachedOwnerless, "sscBreakdownStartingOwnerless");
            }
            else Scribe_References.Look(ref startingOwnerless, "sscBreakdownStartingOwnerless");
            Scribe_Values.Look(ref temporaryIdentity, "sscSlaveBreakdownTemporaryIdentity", false);
            Scribe_Values.Look(ref personalityInvalidated, "sscSlaveBreakdownPersonalityInvalidated", false);
            // 健康列表由原版单独深存，状态只恢复本场的实例引用。外部移除后立即存档时，
            // 写空引用避免读档寻找已经离体的对象。当前会话保留原实例以结算已经过的整小时。
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Hediff_SSCSlaveBreakdown attachedEffect = HasEffect() ? effect : null;
                Scribe_References.Look(ref attachedEffect, "sscSlaveBreakdownEffect");
            }
            else Scribe_References.Look(ref effect, "sscSlaveBreakdownEffect");
            ExposeBehavior();
            ExposeInvitations();
            ExposeTrainingRequest();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) RestoreLegacyTimer();
        }

        private void RestoreLegacyTimer()
        {
            if (effect?.Timer is not { } timer || effect.LegacyExpiresAt < 0 || SSCSlaveBreakdown.Now < 0) return;
            // 一次性换算旧绝对时限和已结算整小时，之后只保存原版剩余时长和结算次数。
            trainingExtended = legacyOriginalDeadline >= 0;
            int duration = timer.Props.disappearsAfterTicks.max + (trainingExtended ? GenDate.TicksPerHour : 0);
            long start = effect.LegacyExpiresAt - duration;
            timer.disappearsAfterTicks = duration;
            timer.ticksToDisappear = (int)Math.Clamp(effect.LegacyExpiresAt - SSCSlaveBreakdown.Now, 0L, duration);
            if (legacyNextBrokenAt > 0)
                brokenHoursApplied = (int)Math.Clamp((legacyNextBrokenAt - start) / GenDate.TicksPerHour - 1,
                    0L, duration / GenDate.TicksPerHour);
            effect.LegacyExpiresAt = -1;
        }

        public override void MentalStateTick(int delta)
        {
            CaptureCarrier();
            int now = SSCSlaveBreakdown.Now;
            // 先处理到期，避免调教刷新撤回已经用完的延长时段。
            if (!initialized || !CanInitializeOrContinue() ||
                !SSCIdentityUtility.IsSexSlave(pawn) || !HasEffect() || RemainingTicks <= 0)
            {
                EndCurrentState();
                return;
            }

            // 先清理未能恢复的邀约，再处理批准，避免失效的邀请仍占着发起者或调教员。
            TickInvitation();
            if (pawn.MentalState != this) return;
            // 已开场的调教可延长原版计时，撤销时减回延长量并于本轮复查。
            TickTrainingRequest(now, delta);
            if (pawn.MentalState != this) return;
            if (!HasEffect() || RemainingTicks <= 0)
            {
                EndCurrentState();
                return;
            }

            AdvanceBroken();
            // 添加或修改 FeelingBroken 会进入健康回调，其中可能清除或替换状态。
            if (pawn.MentalState != this) return;
            // 预约未成功安装的任务可能没有结束回调，按实际当前任务编号清理候选。
            RetireMissingBehaviorJob();
            if (pawn.MentalState == this) base.MentalStateTick(delta);
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

        /// <summary>是否以有主身份入场。训练权限、心情与信件以入场分支为依据。</summary>
        internal bool BoundEpisode => boundEpisode;

        /// <summary>原绑定仍有效时返回当时的实际主人。主人临时忙碌或离图仍保有原绑定。</summary>
        internal Pawn BoundOwner => boundEpisode && HasCurrentBinding() ? boundOwner : null;

        private bool HasCurrentBinding() => (!boundEpisode || boundChain != null) &&
            SSCSlaveBreakdown.MatchesBinding(pawn, boundChain, boundOwner) &&
            (boundEpisode || (hadOwnerlessRecord == (startingOwnerless != null) &&
                Hediff_SSCOwnerlessSlave.Find(pawn) == startingOwnerless));

        // 已开场只复查角色与原绑定。若外部动作推进了同一条锁链，调教仍可完成，
        // 阶段变化由成功结算单独判断，以免把当前发作或本场自己的升阶误判成失效。
        private bool CanInitializeOrContinue() => !ending && pawn.MentalState == this && SameCarrier() &&
            SSCSlaveBreakdown.IsEligibleSubject(pawn) && HasCurrentBinding();

        // 存档可能发生在读档后的首次状态刷新之前，此时还未捕获运行时载体。直接核对当前健康列表。
        private bool HasEffect() => effect != null && effect.pawn == pawn && pawn?.health?.hediffSet?.hediffs.Contains(effect) == true;

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
                if (RemainingTicks <= 0) return false;
                // 先保存实例引用，再交给原版健康系统。添加过程可因状态或健康回调重入而中止。
                health.AddHediff(effect);
                if (!CanInitializeOrContinue() || !SSCIdentityUtility.IsSexSlave(pawn) || !HasEffect()) return false;
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

        private void AdvanceBroken()
        {
            if (!initialized || effect?.Timer is not { } timer || !SameCarrier() || pawn.Dead || pawn.Destroyed) return;
            int elapsed = (int)Math.Clamp((long)timer.disappearsAfterTicks - timer.ticksToDisappear, 0L, timer.disappearsAfterTicks);
            int completedHours = elapsed / GenDate.TicksPerHour;
            if (brokenHoursApplied >= completedHours) return;
            HediffDef broken = DefDatabase<HediffDef>.GetNamedSilentFail("FeelingBroken");
            // 先推进存档游标，再用原版健康接口追加。可重入的健康回调只能结算之后的整小时。
            // RJW 原有严重度上限、恢复与其它来源的增量继续生效。
            while (brokenHoursApplied < completedHours && SameCarrier() && !pawn.Dead && !pawn.Destroyed)
            {
                brokenHoursApplied++;
                if (broken != null) HealthUtility.AdjustSeverity(pawn, broken, 0.03f);
            }
        }

        private void EndCurrentState()
        {
            // 冷却 Hediff 的添加可同步触发状态刷新。原版已在清除本状态时，由外层恢复调用继续完成。
            if (ending || ended) return;
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
            // 只有已确认启动的发作才开始冷却。之后移除 Hediff 仍按本场中断结算。
            if (initialized && SSCSlaveBreakdown.Now >= 0 &&
                pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp)
                comp.RecordSlaveBreakdownCooldown();
        }

        /// <summary>清除后结算一次。身体效果按实例移除，身份回退另行核对当前人格和永久锁。</summary>
        internal void FinishEnding()
        {
            if (ended || Scribe.mode != LoadSaveMode.Inactive) return;
            // 静默替换可直接进入这里。先认领清理，再让 BeginEnding 写冷却，避免健康回调重入结算。
            ended = true;
            // 先保留应计的 RJW 整小时增量，再清理任务、临时效果以及身份。前一阶段异常仍尝试后续清理。
            try
            {
                BeginEnding();
                // 到期或中断时按原实例补齐已过的整小时，即使原版健康更新已经移除了该实例。
                AdvanceBroken();
            }
            finally
            {
                try
                {
                    // 原版清除精神状态后，仍须持有原调教员和原批准来提交阶段或归属。
                    // 先提交并发布结果，再清除请求字段，避免撤销路径提前抹去本场决定。
                    // 无论健康、记忆或信件回调结果如何，都释放本场行为并移除本场效果。
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

    /// <summary>供 SSC 的恶堕判定和风险查询检查启动资格，随后仍由原版精神状态处理器执行。</summary>
    public sealed class MentalStateWorker_SSCSlaveBreakdown : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn) => SSCSlaveBreakdown.CanStart(pawn) && base.StateCanOccur(pawn);
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
