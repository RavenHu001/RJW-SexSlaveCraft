// AI 辅助生成：任务侧独立保存生产认领与消费额度，不保存可以重放的高潮事件。
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
        internal int Version = 2, PawnId, JobId;
        internal bool Claimed;
        internal double Granted;
        internal bool ConsumptionClosed;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Version, "version", 0, forceSave: true);
            Scribe_Values.Look(ref PawnId, "pawnId", 0);
            Scribe_Values.Look(ref JobId, "jobId", 0);
            Scribe_Values.Look(ref Claimed, "claimed", false);
            Scribe_Values.Look(ref Granted, "granted", double.NaN, forceSave: true);
            Scribe_Values.Look(ref ConsumptionClosed, "consumptionClosed", false, forceSave: true);
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
                else if ((state.Saved.Version != 1 && state.Saved.Version != 2) ||
                    state.Saved.PawnId != driver.pawn.thingIDNumber || state.Saved.JobId != driver.job.loadID)
                    state.Ended = true;
                else if (state.Saved.Version == 1)
                {
                    // 旧版仅有生产认领；不追算旧高潮，也不丢弃已用的生产机会。
                    state.Saved.Version = 2; state.Saved.Granted = 0d; state.Saved.ConsumptionClosed = false;
                }
                if (!double.IsFinite(state.Saved.Granted) || state.Saved.Granted < 0d ||
                    state.Saved.Granted > SSCCorruptionGelConsumptionUtility.SceneLimit)
                    state.Saved.ConsumptionClosed = true;
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
                Effect = SSCCorruptionGelEffectUtility.Find(driver.pawn), Need = driver.pawn.needs?.TryGetNeed<Need_Corruption>(),
                Before = driver.orgasms, Root = root };
            frame.CanBoost = !state.Saved.Claimed && frame.Producer?.CanReceiveSceneBoost == true;
            frame.CanConsume = !state.Saved.ConsumptionClosed && state.Saved.Granted < SSCCorruptionGelConsumptionUtility.SceneLimit &&
                frame.Need != null && SSCCorruptionGelEffectUtility.IsActive(frame.Effect);
            return frame;
        }

        /// <summary>复查同一事件的身体和人格；不在旧请求结算时重新捕获药效、需求或生产owner。</summary>
        private static bool IsCurrent(JobDriver_Sex driver, Frame frame) => !frame.State.Ended && IsCurrent(driver) &&
            ReferenceEquals(driver.pawn, frame.Pawn) && ReferenceEquals(driver.job, frame.Job) &&
            ReferenceEquals(driver.Sexprops, frame.Props) && ReferenceEquals(frame.Pawn.health, frame.Health) &&
            ReferenceEquals(frame.Pawn.TryGetComp<CompSexSlaveTraining>(), frame.Training) &&
            SSCPersonalityRuntimeUtility.IsCurrent(frame.Pawn, frame.Personality);

        /// <summary>正常返回的外层统一结算真实本人增量；先占额度再写入，生产回调后独立复查消费归属。</summary>
        internal static void End(JobDriver_Sex driver, Frame frame, Exception error)
        {
            if (frame == null || frame.Closed) return;
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
                if (frame.CanConsume && !state.Saved.ConsumptionClosed)
                    SSCCorruptionGelConsumptionUtility.Apply(frame.Pawn, frame.Effect, frame.Need, count,
                        SSCCorruptionGelConsumptionUtility.SceneLimit - state.Saved.Granted,
                        () => IsCurrent(driver, frame), gain => state.Saved.Granted += gain);
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
                    // 清理或原驱动换Job后只保存已耗尽，不能把旧请求恢复成新机会。
                    saved = state.Saved;
                    if (state.Ended || state.Depth != 0 || state.Settling ||
                        (state.Pawn != null && (!ReferenceEquals(state.Pawn, driver.pawn) || !ReferenceEquals(state.Job, driver.job))))
                        saved = new SSCCorruptionGelSceneSaveState { PawnId = driver.pawn?.thingIDNumber ?? 0, JobId = driver.job?.loadID ?? 0,
                            Claimed = true, ConsumptionClosed = true };
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
