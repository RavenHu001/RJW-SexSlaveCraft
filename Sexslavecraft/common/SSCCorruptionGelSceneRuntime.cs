// AI 辅助生成：任务侧保存生产认领及两条增长途径的独立额度。
using System;
using System.Runtime.CompilerServices;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>独立深存节点；只保存本人本任务的认领，不保存可以重新执行的事件。</summary>
    public sealed class SSCCorruptionGelSceneSaveState : IExposable
    {
        internal const int CurrentVersion = 3;
        internal int Version = CurrentVersion, PawnId, JobId;
        internal bool Claimed;
        internal double Granted, PartnerGranted;
        internal bool ConsumptionClosed;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Version, "version", 0, forceSave: true);
            Scribe_Values.Look(ref PawnId, "pawnId", 0);
            Scribe_Values.Look(ref JobId, "jobId", 0);
            Scribe_Values.Look(ref Claimed, "claimed", false);
            // 旧节点名称和凝胶字段继续使用，传道追加自己的累计量。
            Scribe_Values.Look(ref Granted, "granted", double.NaN, forceSave: true);
            Scribe_Values.Look(ref ConsumptionClosed, "consumptionClosed", false, forceSave: true);
            Scribe_Values.Look(ref PartnerGranted, "partnerGranted", double.NaN, forceSave: true);
        }
    }

    /// <summary>一个当前RJW任务是一个场次；高潮周期重设、身体开关与owner更换均不重置认领。</summary>
    internal static class SSCCorruptionGelSceneRuntime
    {
        internal sealed class State
        {
            internal Pawn Pawn;
            internal Job Job;
            internal SSCCorruptionGelSceneSaveState Saved;
            internal int Depth;
            internal bool Ended, Tainted, Settling;
        }
        internal sealed class Frame
        {
            internal State State;
            internal Pawn Pawn;
            internal Job Job;
            internal Pawn_HealthTracker Health;
            internal SexProps Props;
            internal CompSexSlaveTraining Training;
            internal object Personality;
            internal HediffComp_SSCCorruptionGelProduction Producer;
            internal Hediff_SSCCorruptionGelEffect Effect;
            internal Need_Corruption Need;
            internal Game Game;
            internal SSCCulturePartnerCorruption.Observation Culture;
            internal int Before;
            internal bool Root, Closed, CanBoost, CanConsume;
        }
        private static readonly ConditionalWeakTable<JobDriver_Sex, State> states = new ConditionalWeakTable<JobDriver_Sex, State>();
        private static readonly ConditionalWeakTable<Job, State> scenes = new ConditionalWeakTable<Job, State>();

        /// <summary>拒绝离体驱动和加载中的半恢复引用；不以伴侣或最终汇总提供本人次数。</summary>
        private static bool IsAttached(JobDriver_Sex driver) => driver?.pawn?.jobs != null &&
            driver.job != null && ReferenceEquals(driver.pawn.jobs.curDriver, driver) && ReferenceEquals(driver.pawn.CurJob, driver.job);
        private static bool IsCurrent(JobDriver_Sex driver) => Scribe.mode == LoadSaveMode.Inactive && IsAttached(driver);

        /// <summary>交叉引用恢复后、原生SetupToils前关联原任务账本，加载回调换驱动也不能刷新机会。</summary>
        internal static void RestoreJobBinding(JobDriver_Sex driver)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit && IsAttached(driver)) Bind(driver);
        }

        /// <summary>加载节点只在当前任务引用就绪后校验；坏节点禁止补领，缺节点兼容旧档。</summary>
        private static State Bind(JobDriver_Sex driver)
        {
            State state = states.GetOrCreateValue(driver);
            if (state.Pawn == null && state.Job == null)
            {
                // 同一Job被外部重建驱动仍共用账本；加载恢复到另一个Job实例则由其深存节点绑定。
                if (scenes.TryGetValue(driver.job, out State bound) && !ReferenceEquals(bound, state))
                {
                    if (state.Saved != null) { state.Ended = bound.Ended = true; return state; }
                    states.Remove(driver); states.Add(driver, bound); state = bound;
                }
                if (state.Pawn != null && (!ReferenceEquals(state.Pawn, driver.pawn) || !ReferenceEquals(state.Job, driver.job)))
                { state.Ended = true; return state; }
                state.Pawn = driver.pawn; state.Job = driver.job;
                if (state.Saved == null)
                    state.Saved = new SSCCorruptionGelSceneSaveState { PawnId = driver.pawn.thingIDNumber, JobId = driver.job.loadID };
                else if (state.Saved.Version < 1 || state.Saved.Version > SSCCorruptionGelSceneSaveState.CurrentVersion ||
                    state.Saved.PawnId != driver.pawn.thingIDNumber || state.Saved.JobId != driver.job.loadID)
                    state.Ended = true;
                else if (state.Saved.Version < SSCCorruptionGelSceneSaveState.CurrentVersion)
                {
                    // V1 只有生产认领，V2 已保存凝胶额度。旧场没有传道累计量，从恢复后的新事件才开始计。
                    if (state.Saved.Version == 1)
                    {
                        state.Saved.Granted = 0d;
                        state.Saved.ConsumptionClosed = false;
                    }
                    state.Saved.PartnerGranted = 0d;
                    state.Saved.Version = SSCCorruptionGelSceneSaveState.CurrentVersion;
                }

                // 只拒绝超出整个需求范围或不可读的累计值。调低 XML 限额后的旧账本仍合法。
                if (!(state.Saved.Granted >= 0d && state.Saved.Granted <= 1d)) state.Saved.ConsumptionClosed = true;
                if (!(state.Saved.PartnerGranted >= 0d && state.Saved.PartnerGranted <= 1d)) state.Saved.PartnerGranted = double.NaN;
                if (!scenes.TryGetValue(driver.job, out _)) scenes.Add(driver.job, state);
            }
            else if (!ReferenceEquals(state.Pawn, driver.pawn) || !ReferenceEquals(state.Job, driver.job)) state.Ended = true;
            return state;
        }

        /// <summary>所有本人事件共用嵌套边界；生产和消费分别捕获准入，普通服用者不要求生产组件。</summary>
        internal static Frame Begin(JobDriver_Sex driver)
        {
            // RJW的Orgasm每tick被询问，计时未到时原方法立即返回；不要为无事件检查创建场次帧。
            if (!IsCurrent(driver) || driver.Sexprops == null || driver.sex_ticks > driver.orgasmstick) return null;
            State state = Bind(driver);
            if (state.Ended || state.Settling) return null;
            bool root = state.Depth++ == 0;
            if (root) state.Tainted = false;
            var frame = new Frame { State = state, Pawn = driver.pawn, Job = driver.job, Health = driver.pawn.health,
                Props = driver.Sexprops, Training = driver.pawn.TryGetComp<CompSexSlaveTraining>(),
                Personality = SSCPersonalityRuntimeUtility.Capture(driver.pawn),
                Producer = SSCCorruptionGelProductionUtility.Find(driver.pawn),
                Need = driver.pawn.needs?.TryGetNeed<Need_Corruption>(), Game = Current.Game,
                Before = driver.orgasms, Root = root };
            frame.CanBoost = !state.Saved.Claimed && frame.Producer?.CanReceiveSceneBoost == true;
            // 冷却或余额不足时无需取药效；捕获后才替换进来的药效不能消费本次事件。
            if (root && !state.Saved.ConsumptionClosed &&
                SSCCorruptionGainUtility.CanApply(frame.Pawn, frame.Need, SSCCorruptionGainSource.Gel, state.Saved.Granted))
            {
                frame.Effect = SSCCorruptionGelEffectUtility.Find(driver.pawn);
                frame.CanConsume = SSCCorruptionGelEffectUtility.IsActive(frame.Effect);
            }
            // 这里只是达到 RJW 阈值的候选，尚未证明原方法真的增加了本人计数。
            // 外层提前固定文化施加者；嵌套调用沿用同一结算入口，文化资格与凝胶额度仍各自独立。
            if (root) frame.Culture = SSCCulturePartnerCorruption.Capture(driver, frame.Need, state.Saved.PartnerGranted);
            return frame;
        }

        /// <summary>复查同一事件的身体和人格；不在旧请求结算时重新捕获药效、需求或生产owner。</summary>
        private static bool IsCurrent(JobDriver_Sex driver, Frame frame) => !frame.State.Ended && IsCurrent(driver) &&
            ReferenceEquals(Current.Game, frame.Game) &&
            ReferenceEquals(driver.pawn, frame.Pawn) && ReferenceEquals(driver.job, frame.Job) &&
            ReferenceEquals(driver.Sexprops, frame.Props) && ReferenceEquals(frame.Pawn.health, frame.Health) &&
            ReferenceEquals(frame.Pawn.TryGetComp<CompSexSlaveTraining>(), frame.Training) &&
            SSCPersonalityRuntimeUtility.IsCurrent(frame.Pawn, frame.Personality);

        /// <summary>正常返回的外层统一结算真实本人增量；先占额度再写入，生产回调后独立复查消费归属。</summary>
        internal static void End(JobDriver_Sex driver, Frame frame, Exception error)
        {
            if (frame == null || frame.Closed) return;
            EndObserved(driver, frame, error);
        }

        /// <summary>仅真实观察帧进入带回调闭包的结算体，空调用不分配编译器生成的捕获对象。</summary>
        private static void EndObserved(JobDriver_Sex driver, Frame frame, Exception error)
        {
            frame.Closed = true;
            State state = frame.State;
            if (!states.TryGetValue(driver, out State current) || !ReferenceEquals(state, current)) return;
            if (error != null) state.Tainted = true;
            if (state.Depth > 0) state.Depth--;
            if (!frame.Root || state.Depth != 0 || state.Tainted || !IsCurrent(driver, frame) ||
                frame.Before < 0 || driver.orgasms <= frame.Before) return;
            long count = (long)driver.orgasms - frame.Before;
            state.Settling = true;
            try
            {
                if (frame.CanBoost && !state.Saved.Claimed &&
                    ReferenceEquals(SSCCorruptionGelProductionUtility.Find(frame.Pawn), frame.Producer) && frame.Producer.CanReceiveSceneBoost &&
                    IsCurrent(driver, frame))
                {
                    state.Saved.Claimed = true;
                    frame.Producer.ApplySceneBoost();
                }
                // 生产回调可能换掉身体、药效或当前任务，复核通过才提交凝胶这笔账。
                if (frame.CanConsume && !state.Saved.ConsumptionClosed && IsCurrent(driver, frame) &&
                    ReferenceEquals(SSCCorruptionGelEffectUtility.Find(frame.Pawn), frame.Effect) &&
                    SSCCorruptionGelEffectUtility.IsActive(frame.Effect))
                    SSCCorruptionGainUtility.Apply(frame.Pawn, frame.Need, SSCCorruptionGainSource.Gel, count, ref state.Saved.Granted);
                // 凝胶沿原顺序写入；其回调若换掉本次归属，文化收益不能再领取。
                // 文化规则只取「这次观察确有本人高潮」，不乘凝胶用于累计的 count。
                if (frame.Culture != null && IsCurrent(driver, frame))
                    SSCCulturePartnerCorruption.Apply(frame.Pawn, frame.Culture, ref state.Saved.PartnerGranted);
            }
            finally { state.Settling = false; }
        }

        /// <summary>结束的旧驱动不可被清理回调再次激活；弱表不延长任务或身体的生命周期。</summary>
        internal static void Finish(JobDriver driver)
        {
            // 即使此前没有记录也先封闭任务，阻止Cleanup内首次触发的迟到事件新建可领奖状态。
            if (driver is JobDriver_Sex sex)
            {
                State state = states.GetOrCreateValue(sex);
                state.Ended = true;
                if (sex.job != null)
                {
                    if (scenes.TryGetValue(sex.job, out State bound)) bound.Ended = true;
                    else scenes.Add(sex.job, state);
                }
            }
        }

        /// <summary>沿真实RJW ExposeData深存一次；恢复中保留节点，读取不发奖，不主动调用子节点生命周期。</summary>
        internal static void Expose(JobDriver_Sex driver)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) states.Remove(driver);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                SSCCorruptionGelSceneSaveState saved = null;
                // 新驱动尚未触发事件就保存，也必须带上同一Job的旧账本，不能输出缺节点刷新额度。
                if (states.TryGetValue(driver, out State state) || (driver.job != null && scenes.TryGetValue(driver.job, out state)))
                {
                    // 清理或中途保存时同时封住两条额度与生产认领，避免旧事件从快照再次取得机会。
                    saved = state.Saved;
                    if (state.Ended || state.Depth != 0 || state.Settling ||
                        (state.Pawn != null && (!ReferenceEquals(state.Pawn, driver.pawn) || !ReferenceEquals(state.Job, driver.job))))
                        saved = new SSCCorruptionGelSceneSaveState { PawnId = driver.pawn?.thingIDNumber ?? 0, JobId = driver.job?.loadID ?? 0,
                            Claimed = true, ConsumptionClosed = true, PartnerGranted = double.NaN };
                }
                Scribe_Deep.Look(ref saved, "sscCorruptionGelScene");
            }
            else if (Scribe.mode != LoadSaveMode.Inactive)
            {
                State state = states.GetOrCreateValue(driver);
                Scribe_Deep.Look(ref state.Saved, "sscCorruptionGelScene");
            }
        }
    }
}
