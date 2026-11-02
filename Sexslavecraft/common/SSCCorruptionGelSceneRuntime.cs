// AI 辅助生成：任务侧催熟认领；不复制身体选择、库存或敏感度推进余数。
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
        internal int Version = 1, PawnId, JobId;
        internal bool Claimed;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Version, "version", 0, forceSave: true);
            Scribe_Values.Look(ref PawnId, "pawnId", 0);
            Scribe_Values.Look(ref JobId, "jobId", 0);
            Scribe_Values.Look(ref Claimed, "claimed", false);
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
            internal bool Ended, Tainted;
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
            internal int Before;
            internal bool Root, Closed;
        }
        private static readonly ConditionalWeakTable<JobDriver_Sex, State> states = new ConditionalWeakTable<JobDriver_Sex, State>();

        /// <summary>拒绝离体驱动和加载中的半恢复引用；不以伴侣或最终汇总提供本人次数。</summary>
        private static bool IsCurrent(JobDriver_Sex driver) => Scribe.mode == LoadSaveMode.Inactive && driver?.pawn?.jobs != null &&
            driver.job != null && ReferenceEquals(driver.pawn.jobs.curDriver, driver) && ReferenceEquals(driver.pawn.CurJob, driver.job);

        /// <summary>加载节点只在当前任务引用就绪后校验；坏节点禁止补领，缺节点兼容旧档。</summary>
        private static State Bind(JobDriver_Sex driver)
        {
            State state = states.GetOrCreateValue(driver);
            if (state.Pawn == null && state.Job == null)
            {
                state.Pawn = driver.pawn; state.Job = driver.job;
                if (state.Saved == null)
                    state.Saved = new SSCCorruptionGelSceneSaveState { PawnId = driver.pawn.thingIDNumber, JobId = driver.job.loadID };
                else if (state.Saved.Version != 1 || state.Saved.PawnId != driver.pawn.thingIDNumber || state.Saved.JobId != driver.job.loadID)
                    state.Ended = true;
            }
            else if (!ReferenceEquals(state.Pawn, driver.pawn) || !ReferenceEquals(state.Job, driver.job)) state.Ended = true;
            return state;
        }

        /// <summary>在事件前捕获原身体／人格；满批次也打开认领，未符合生产条件的事件不消耗机会。</summary>
        internal static Frame Begin(JobDriver_Sex driver)
        {
            if (!IsCurrent(driver) || driver.Sexprops == null) return null;
            var producer = SSCCorruptionGelProductionUtility.Find(driver.pawn);
            if (producer?.CanReceiveSceneBoost != true) return null;
            State state = Bind(driver);
            if (state.Ended || state.Saved.Claimed) return null;
            bool root = state.Depth++ == 0;
            if (root) state.Tainted = false;
            return new Frame { State = state, Pawn = driver.pawn, Job = driver.job, Health = driver.pawn.health,
                Props = driver.Sexprops, Training = driver.pawn.TryGetComp<CompSexSlaveTraining>(),
                Personality = SSCPersonalityRuntimeUtility.Capture(driver.pawn), Producer = producer,
                Before = driver.orgasms, Root = root };
        }

        /// <summary>只在正常完成且本人真实计数增加后认领；先记账后催熟，嵌套调用与重复Finalizer不重发。</summary>
        internal static void End(JobDriver_Sex driver, Frame frame, Exception error)
        {
            if (frame == null || frame.Closed) return;
            frame.Closed = true;
            State state = frame.State;
            if (!states.TryGetValue(driver, out State current) || !ReferenceEquals(state, current)) return;
            if (error != null) state.Tainted = true;
            if (state.Depth > 0) state.Depth--;
            if (!frame.Root || state.Depth != 0 || state.Ended || state.Tainted || state.Saved.Claimed || !IsCurrent(driver) ||
                !ReferenceEquals(driver.pawn, frame.Pawn) || !ReferenceEquals(driver.job, frame.Job) ||
                !ReferenceEquals(driver.Sexprops, frame.Props) || !ReferenceEquals(frame.Pawn.health, frame.Health) ||
                !ReferenceEquals(frame.Pawn.TryGetComp<CompSexSlaveTraining>(), frame.Training) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(frame.Pawn, frame.Personality) ||
                !ReferenceEquals(SSCCorruptionGelProductionUtility.Find(frame.Pawn), frame.Producer) ||
                frame.Before < 0 || driver.orgasms <= frame.Before || !frame.Producer.CanReceiveSceneBoost) return;
            state.Saved.Claimed = true;
            frame.Producer.ApplySceneBoost();
        }

        /// <summary>结束的旧驱动不可被清理回调再次激活；弱表不延长任务或身体的生命周期。</summary>
        internal static void Finish(JobDriver driver)
        {
            // 即使此前没有记录也先封闭任务，阻止Cleanup内首次触发的迟到事件新建可领奖状态。
            if (driver is JobDriver_Sex sex) states.GetOrCreateValue(sex).Ended = true;
        }

        /// <summary>沿真实RJW ExposeData深存一次；恢复中保留节点，读取不发奖，不主动调用子节点生命周期。</summary>
        internal static void Expose(JobDriver_Sex driver)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) states.Remove(driver);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                SSCCorruptionGelSceneSaveState saved = null;
                if (states.TryGetValue(driver, out State state))
                {
                    // 清理或原驱动换Job后只保存已耗尽，不能把旧请求恢复成新机会。
                    saved = state.Saved;
                    if (state.Ended || state.Depth != 0 ||
                        (state.Pawn != null && (!ReferenceEquals(state.Pawn, driver.pawn) || !ReferenceEquals(state.Job, driver.job))))
                        saved = new SSCCorruptionGelSceneSaveState { PawnId = driver.pawn?.thingIDNumber ?? 0, JobId = driver.job?.loadID ?? 0, Claimed = true };
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
