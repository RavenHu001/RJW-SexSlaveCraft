using System;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>原始写回所在的计时入口；External 由独立观察者提供可信边界，不代表已自动适配。</summary>
    public enum SSCOrgasmAdvanceSource
    {
        Unknown, RjwSexTick, AnimationSexTick, UapInitiator, UapReceiver, External
    }

    /// <summary>采样失败的技术原因。None 与有效 RawAdvance 为零可以同时成立，不以零代替失败。</summary>
    public enum SSCOrgasmAdvanceFailure
    {
        None, InvalidContext, Loading, InactiveDriver, ChangedContext,
        ChangedCycle, CounterRewound, UnexpectedThresholdChange, NegativeAdvance
    }

    /// <summary>由观察者提供的瞬时凭据；不是身体选择，也不是可存档的执行命令。</summary>
    public readonly struct SSCOrgasmObservationContext
    {
        public SSCOrgasmAdvanceSource Source { get; }
        /// <summary>发行该组周期／操作编号的实例身份；编号相同但实例不同也不能复用。</summary>
        public object CycleOwner { get; }
        public ulong Cycle { get; }
        public ulong Operation { get; }
        public bool AllowThresholdAdvance { get; }

        /// <summary>封装适配层已经发行的凭据；构造自身不会推进、分配编号或创建记录。</summary>
        /// <param name="allowThresholdAdvance">仅 External 可显式允许阈值参与推进；普通入口不能猜测阈值变化。</param>
        public SSCOrgasmObservationContext(SSCOrgasmAdvanceSource source, object cycleOwner,
            ulong cycle, ulong operation, bool allowThresholdAdvance = false)
        {
            Source = source; CycleOwner = cycleOwner; Cycle = cycle; Operation = operation;
            AllowThresholdAdvance = allowThresholdAdvance;
        }

        /// <summary>拒绝默认值、无发行者及不合法的来源许可；不查询任何 SSC 业务资格。</summary>
        internal bool Valid => Source > SSCOrgasmAdvanceSource.Unknown && Source <= SSCOrgasmAdvanceSource.External
            && CycleOwner != null && Cycle != 0 && Operation != 0
            && (!AllowThresholdAdvance || Source == SSCOrgasmAdvanceSource.External);

        /// <summary>同时比较发行者、周期、操作与来源，避免用同 tick 或相同整数误认同一次推进。</summary>
        internal bool Matches(SSCOrgasmObservationContext other) => Valid && other.Valid
            && ReferenceEquals(CycleOwner, other.CycleOwner) && Cycle == other.Cycle
            && Operation == other.Operation && Source == other.Source
            && AllowThresholdAdvance == other.AllowThresholdAdvance;
    }

    /// <summary>一个边界时刻的只读字段副本；引用用于复核归属，数值不随原驱动之后的重设变化。</summary>
    public readonly struct SSCOrgasmAdvanceSnapshot
    {
        public JobDriver_Sex Driver { get; }
        public Pawn Actor { get; }
        public Job Job { get; }
        public SexProps SexProps { get; }
        public SSCOrgasmObservationContext Context { get; }
        public int Counter { get; }
        public int Threshold { get; }
        public int StartCounter { get; }
        public int Duration { get; }
        public int Orgasms { get; }
        public SSCOrgasmAdvanceFailure Failure { get; }
        public bool IsValid => Driver != null && Failure == SSCOrgasmAdvanceFailure.None;

        /// <summary>读取原生字段而不补建 Pawn／Job／SexProps；即使采样无效也保留失败原因。</summary>
        internal SSCOrgasmAdvanceSnapshot(JobDriver_Sex driver, SSCOrgasmObservationContext context,
            SSCOrgasmAdvanceFailure failure)
        {
            Driver = driver; Actor = driver?.pawn; Job = driver?.job; SexProps = driver?.Sexprops;
            Context = context; Counter = driver?.sex_ticks ?? 0; Threshold = driver?.orgasmstick ?? 0;
            StartCounter = driver?.orgasmStartTick ?? 0; Duration = driver?.duration ?? 0;
            Orgasms = driver?.orgasms ?? 0; Failure = failure;
        }
    }

    /// <summary>一次已完成原始推进的不可变值；反复读取不会再次采样或应用效果。</summary>
    public readonly struct SSCOrgasmAdvance
    {
        public SSCOrgasmAdvanceSnapshot Before { get; }
        public SSCOrgasmAdvanceSnapshot After { get; }
        public JobDriver_Sex Driver => Before.Driver;
        public Pawn Actor => Before.Actor;
        public SSCOrgasmAdvanceSource Source => Before.Context.Source;
        public ulong Cycle => Before.Context.Cycle;
        public ulong Operation => Before.Context.Operation;
        // 先转换为 long 再相减，覆盖两个 int 端点之间超过 int 范围的距离。
        public long CounterAdvance => (long)Before.Counter - After.Counter;
        public long ThresholdAdvance => (long)After.Threshold - Before.Threshold;
        public long RawAdvance => CounterAdvance + ThresholdAdvance;

        /// <summary>只组合已经核验的前后端点，不重新读取当前计时或估算高潮次数。</summary>
        internal SSCOrgasmAdvance(SSCOrgasmAdvanceSnapshot before, SSCOrgasmAdvanceSnapshot after)
        { Before = before; After = after; }
    }

    /// <summary>在游戏线程的可信推进边界使用。只读，不查 SSC 身份／档位，不改计时或创建执行／存档数据。</summary>
    public static class SSCOrgasmProgressUtility
    {
        /// <summary>在原写回前捕获快照；先检查上下文、加载阶段和实际当前任务，不创建采样记录。</summary>
        /// <returns>通过 IsValid／Failure 区分可用快照与失败；无效快照不影响调用方继续原写入。</returns>
        public static SSCOrgasmAdvanceSnapshot CaptureBeforeAdvance(JobDriver_Sex driver,
            SSCOrgasmObservationContext observationContext)
        {
            SSCOrgasmAdvanceFailure failure = !observationContext.Valid ? SSCOrgasmAdvanceFailure.InvalidContext
                : Scribe.mode != LoadSaveMode.Inactive ? SSCOrgasmAdvanceFailure.Loading
                : !IsCurrentDriver(driver) ? SSCOrgasmAdvanceFailure.InactiveDriver : SSCOrgasmAdvanceFailure.None;
            return new SSCOrgasmAdvanceSnapshot(driver, observationContext, failure);
        }

        /// <summary>完成一次原始推进的只读差分，不需要失败细节时使用；有效零推进仍返回 true。</summary>
        public static bool TryGetOriginalAdvance(SSCOrgasmAdvanceSnapshot snapshot, JobDriver_Sex driver,
            SSCOrgasmObservationContext currentContext, out SSCOrgasmAdvance advance)
            => TryGetOriginalAdvance(snapshot, driver, currentContext, out advance, out _);

        /// <summary>在 SSC 效果或高潮判定前验证同一操作，并计算距阈值缩短的原始距离。</summary>
        /// <param name="currentContext">发行者的当前凭据；不得直接拿旧快照凭据证明它仍然有效。</param>
        /// <param name="advance">成功时为不可变结果；调用方应向消费者共享它，不在效果写入后重算。</param>
        /// <param name="failure">失败原因，不回退成固定减 1，也不调用初始化或 Orgasm。</param>
        public static bool TryGetOriginalAdvance(SSCOrgasmAdvanceSnapshot snapshot, JobDriver_Sex driver,
            SSCOrgasmObservationContext currentContext, out SSCOrgasmAdvance advance,
            out SSCOrgasmAdvanceFailure failure)
        {
            // 先验证归属和周期，随后才解释字段差值；初始化写小也可能产生看似正向的差值。
            advance = default;
            failure = snapshot.IsValid ? SSCOrgasmAdvanceFailure.None
                : snapshot.Driver == null ? SSCOrgasmAdvanceFailure.InactiveDriver : snapshot.Failure;
            if (failure != SSCOrgasmAdvanceFailure.None) return false;
            if (Scribe.mode != LoadSaveMode.Inactive) failure = SSCOrgasmAdvanceFailure.Loading;
            else if (!snapshot.Context.Matches(currentContext)) failure = SSCOrgasmAdvanceFailure.InvalidContext;
            else if (!SameOwner(snapshot, driver)) failure = SSCOrgasmAdvanceFailure.ChangedContext;
            else if (!SameCycleFields(snapshot, driver)) failure = SSCOrgasmAdvanceFailure.ChangedCycle;
            else if (driver.sex_ticks > snapshot.Counter) failure = SSCOrgasmAdvanceFailure.CounterRewound;
            else if (!currentContext.AllowThresholdAdvance && driver.orgasmstick != snapshot.Threshold)
                failure = SSCOrgasmAdvanceFailure.UnexpectedThresholdChange;
            if (failure != SSCOrgasmAdvanceFailure.None) return false;
            var after = new SSCOrgasmAdvanceSnapshot(driver, currentContext, SSCOrgasmAdvanceFailure.None);
            var result = new SSCOrgasmAdvance(snapshot, after);
            // External 也不能用整体后退的距离产生正向推进；零距离则是有效的未推进。
            if (result.RawAdvance < 0) { failure = SSCOrgasmAdvanceFailure.NegativeAdvance; return false; }
            advance = result;
            return true;
        }

        /// <summary>自动采样结果提交前使用；同时核对采样器当前凭据，不能拿旧 Context 自证有效。</summary>
        public static bool IsStillCurrent(SSCOrgasmAdvance advance)
            => SSCOrgasmProgressSampling.IsCurrent(advance);

        /// <summary>外部观察者使用，须传入其发行者的当前凭据，而不是复制旧结果的 Context。</summary>
        public static bool IsStillCurrent(SSCOrgasmAdvance advance, SSCOrgasmObservationContext currentContext)
            => advance.After.IsValid && Scribe.mode == LoadSaveMode.Inactive
                && advance.After.Context.Matches(currentContext) && SameOwner(advance.After, advance.Driver)
                && SameCycleFields(advance.After, advance.Driver)
                && advance.Driver.sex_ticks == advance.After.Counter
                && advance.Driver.orgasmstick == advance.After.Threshold;

        /// <summary>只读取得当前驱动最近一次仍有效的结果；不是历史队列，也不补采错过的操作。</summary>
        public static bool TryGetLastObservedAdvance(JobDriver_Sex driver, out SSCOrgasmAdvance advance)
            => SSCOrgasmProgressSampling.TryGetLast(driver, out advance);

        /// <summary>核对 Pawn 当前 Job 与当前驱动的实例归属；不把它当作身份、人格或效果权限检查。</summary>
        internal static bool IsCurrentDriver(JobDriver_Sex driver) => driver?.pawn?.jobs != null
            && driver.job != null && driver.Sexprops != null
            && ReferenceEquals(driver.pawn.jobs.curDriver, driver) && ReferenceEquals(driver.pawn.jobs.curJob, driver.job);

        /// <summary>拒绝同 Pawn 上后来换入的新 Job、驱动或 SexProps，即使它们的计数恰好一样。</summary>
        private static bool SameOwner(SSCOrgasmAdvanceSnapshot snapshot, JobDriver_Sex driver)
            => ReferenceEquals(snapshot.Driver, driver) && IsCurrentDriver(driver)
                && ReferenceEquals(snapshot.Actor, driver.pawn) && ReferenceEquals(snapshot.Job, driver.job)
                && ReferenceEquals(snapshot.SexProps, driver.Sexprops);

        /// <summary>验证推进范围没有跨过可见的周期起点／时长重设或真实高潮计数变化。</summary>
        private static bool SameCycleFields(SSCOrgasmAdvanceSnapshot snapshot, JobDriver_Sex driver)
            => snapshot.StartCounter == driver.orgasmStartTick && snapshot.Duration == driver.duration
                && snapshot.Orgasms == driver.orgasms;
    }
}
