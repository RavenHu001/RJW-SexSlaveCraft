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
        private long nextBrokenAt;
        private Hediff_SSCSlaveBreakdown effect;
        // 即使效果被外部移除，也保留原实例到退出结算结束。准入要再检查实例是否仍挂在当前身体上。
        private long Deadline => effect?.ExpiresAt ?? -1L;
        private bool initialized;
        // 记录入场分支。原链日后移除、换主或无法在读档中恢复时，仍需按有链发作退出。
        private bool boundEpisode;
        private Hediff_ChainOfSexSlave boundChain;
        private Pawn boundOwner;
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
            long deadline = (long)SSCSlaveBreakdown.Now + SSCSlaveBreakdown.DurationTicks;
            if (!TryInitialize(deadline))
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
            Scribe_Values.Look(ref nextBrokenAt, "sscSlaveBreakdownNextBrokenAt", 0L);
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
            Scribe_Values.Look(ref temporaryIdentity, "sscSlaveBreakdownTemporaryIdentity", false);
            Scribe_Values.Look(ref personalityInvalidated, "sscSlaveBreakdownPersonalityInvalidated", false);
            // 健康列表由原版单独深存，状态只恢复本场的实例引用。外部移除后立即存档时，
            // 写空引用避免读档寻找已经离体的对象。当前会话仍持有原实例，退出可读取它的截止点。
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Hediff_SSCSlaveBreakdown attachedEffect = HasEffect() ? effect : null;
                Scribe_References.Look(ref attachedEffect, "sscSlaveBreakdownEffect");
            }
            else Scribe_References.Look(ref effect, "sscSlaveBreakdownEffect");
            ExposeBehavior();
            ExposeInvitations();
            ExposeTrainingRequest();
        }

        public override void MentalStateTick(int delta)
        {
            CaptureCarrier();
            int now = SSCSlaveBreakdown.Now;
            // 到期先走退出结算，避免调教刷新把已用完的延长期误判为任务失效并退回原截止点。
            if (!initialized || !CanInitializeOrContinue() ||
                !SSCIdentityUtility.IsSexSlave(pawn) || !HasEffect() || now >= Deadline)
            {
                EndCurrentState();
                return;
            }

            // 先清理未能恢复的邀约，再处理批准，避免失效的邀请仍占着发起者或调教员。
            TickInvitation();
            if (pawn.MentalState != this) return;
            // 已开场的调教可延长 Hediff，撤销时恢复原截止点并于本轮复查。
            TickTrainingRequest(now, delta);
            if (pawn.MentalState != this) return;
            if (!HasEffect() || now >= Deadline)
            {
                EndCurrentState();
                return;
            }

            AdvanceBroken(now);
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

        /// <summary>本场入场分支，用于选择评分、成功心情和信件。</summary>
        internal bool BoundEpisode => boundEpisode;

        /// <summary>原绑定仍有效时返回当时的实际主人。主人临时忙碌或离图仍保有原绑定。</summary>
        internal Pawn BoundOwner => boundEpisode && HasCurrentBinding() ? boundOwner : null;

        private bool HasCurrentBinding() => (!boundEpisode || boundChain != null) &&
            SSCSlaveBreakdown.MatchesBinding(pawn, boundChain, boundOwner);

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

        private bool TryInitialize(long deadline)
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
                effect.ExpiresAt = deadline;
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
            int now = SSCSlaveBreakdown.Now;
            if (initialized && now >= 0 &&
                pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp)
                comp.RecordSlaveBreakdownCooldown((long)now + SSCSlaveBreakdown.CooldownTicks);
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
                // 到期补齐最后一个整小时。提前移除后仍可从原实例读取截止点，以中断刷新为上限。
                AdvanceBroken(Math.Min(SSCSlaveBreakdown.Now, Deadline));
            }
            finally
            {
                try
                {
                    // 成功发布需要原调教员和原批准的数据，先计算锁链进展并发放结果，再清除请求字段。
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
