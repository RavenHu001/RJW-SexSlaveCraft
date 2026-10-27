using System;
using System.Runtime.CompilerServices;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>适配层持有周期／操作凭据；公共 getter 不在此创建记录。本批没有效果消费者。</summary>
    internal static class SSCOrgasmProgressSampling
    {
        /// <summary>仅属于一个驱动实例的瞬时记录；不进入 Scribe，也不承担任何玩家选择的保存。</summary>
        private sealed class State
        {
            internal ulong Cycle = 1, Operation;
            internal int ResetDepth;
            internal bool HasLast, Exhausted;
            internal SSCOrgasmAdvance Last;
        }

        /// <summary>一次初始化观察的关闭凭据；同时绑定驱动与原记录，防止清理后误关新记录。</summary>
        internal readonly struct ResetScope
        {
            internal readonly JobDriver_Sex Driver;
            internal readonly object StateOwner;
            internal readonly bool Open;
            /// <summary>记录真正打开范围的那个 State；只记 Driver 不足以抵抗同驱动记录重建。</summary>
            internal ResetScope(JobDriver_Sex driver, object stateOwner)
            { Driver = driver; StateOwner = stateOwner; Open = true; }
        }

        /// <summary>Orgasm 入口的记录实例、计数与周期，不预判该调用必然触发高潮。</summary>
        internal readonly struct OrgasmScope
        {
            internal readonly object StateOwner;
            internal readonly int Count;
            internal readonly ulong Cycle;
            internal readonly bool Open;
            /// <summary>保存事件进入时的端点；完成时只允许失效同一个 State 的旧数据。</summary>
            internal OrgasmScope(object stateOwner, int count, ulong cycle)
            { StateOwner = stateOwner; Count = count; Cycle = cycle; Open = true; }
        }

        // 弱表允许任务驱动自然回收；工厂委托缓存一次，稳定推进只读写值结构，不逐次分配。
        private static readonly ConditionalWeakTable<JobDriver_Sex, State> states = new ConditionalWeakTable<JobDriver_Sex, State>();
        private static readonly ConditionalWeakTable<JobDriver_Sex, State>.CreateValueCallback createState = _ => new State();

        /// <summary>只隔离 SSC 自身可恢复的检查错误，不把资源耗尽或损坏运行时当作正常采样失败。</summary>
        internal static bool Recoverable(Exception error) => !(error is OutOfMemoryException)
            && !(error is StackOverflowException) && !(error is AccessViolationException);

        /// <summary>RJW 自身／受方写回入口，沿用原表达式已经算出的 value。</summary>
        internal static void WriteRjw(JobDriver_Sex driver, int value) => WriteOriginal(driver, value, SSCOrgasmAdvanceSource.RjwSexTick);
        /// <summary>动画替代 Prefix 内的写回入口，不再次执行基础 SexTick。</summary>
        internal static void WriteAnimation(JobDriver_Sex driver, int value) => WriteOriginal(driver, value, SSCOrgasmAdvanceSource.AnimationSexTick);
        /// <summary>UAP 发起方独立计时入口，不替它额外推进接收者。</summary>
        internal static void WriteUapInitiator(JobDriver_Sex driver, int value) => WriteOriginal(driver, value, SSCOrgasmAdvanceSource.UapInitiator);
        /// <summary>UAP 主发起者已经完成去重之后的共享接收者写回入口。</summary>
        internal static void WriteUapReceiver(JobDriver_Sex driver, int value) => WriteOriginal(driver, value, SSCOrgasmAdvanceSource.UapReceiver);

        /// <summary>观察一次原写回并发布结果。无效／关闭时仍写原值一次，本批没有效果补充或事件补发。</summary>
        private static void WriteOriginal(JobDriver_Sex driver, int value, SSCOrgasmAdvanceSource source)
        {
            State state = null;
            SSCOrgasmAdvanceSnapshot before = default;
            try
            {
                // 只有可信入口才建立记录。先失效不再匹配的旧结果，再发行该驱动的新操作号。
                if (driver != null && Scribe.mode == LoadSaveMode.Inactive
                    && SSCOrgasmProgressPatches.IsReady(source) && SSCOrgasmProgressUtility.IsCurrentDriver(driver))
                {
                    state = states.GetValue(driver, createState);
                    if (state.HasLast && !SSCOrgasmProgressUtility.IsStillCurrent(state.Last, state.Last.After.Context))
                        Invalidate(state);
                    state.HasLast = false;
                    if (state.ResetDepth == 0 && !state.Exhausted && state.Operation != ulong.MaxValue)
                    {
                        state.Operation++;
                        before = SSCOrgasmProgressUtility.CaptureBeforeAdvance(driver,
                            new SSCOrgasmObservationContext(source, state, state.Cycle, state.Operation));
                    }
                    else if (state.Operation == ulong.MaxValue) state.Exhausted = true;
                }
                else NotifyReset(driver);
            }
            catch (Exception error) when (Recoverable(error)) { NotifyReset(driver); state = null; }

            // 原字段访问刻意在 SSC 的 catch 之外：无效样本不能吞原推进，也不能吞原 NullReferenceException。
            driver.sex_ticks = value;

            // 写回后的验证不修改原计时。失败只影响 SSC 记录，不回滚值或再次执行写入。
            try
            {
                if (state != null && before.IsValid && state.ResetDepth == 0 && SSCOrgasmProgressPatches.IsReady(source)
                    && SSCOrgasmProgressUtility.TryGetOriginalAdvance(before, driver,
                        new SSCOrgasmObservationContext(source, state, state.Cycle, state.Operation), out var result))
                {
                    state.Last = result;
                    state.HasLast = true;
                    // 将来效果消费者只在此之后接入；本批不修改已写回的计数，不调用 Orgasm。
                }
            }
            catch (Exception error) when (Recoverable(error)) { NotifyReset(driver); }
        }

        /// <summary>纯查询最近有效结果；不创建 State、不消费结果，也不把旧 Context 当现行凭据。</summary>
        internal static bool TryGetLast(JobDriver_Sex driver, out SSCOrgasmAdvance advance)
        {
            advance = default;
            if (driver == null || !states.TryGetValue(driver, out var state) || !state.HasLast || state.Exhausted
                || state.ResetDepth != 0 || !SSCOrgasmProgressPatches.IsReady(state.Last.Source)) return false;
            var context = new SSCOrgasmObservationContext(state.Last.Source, state, state.Cycle, state.Operation);
            if (!SSCOrgasmProgressUtility.IsStillCurrent(state.Last, context)) return false;
            advance = state.Last;
            return true;
        }

        /// <summary>将传入结果与弱表当前持有的同一次操作比对，包含原始端点而非仅比较差值。</summary>
        internal static bool IsCurrent(SSCOrgasmAdvance advance)
            => TryGetLast(advance.Driver, out var latest) && advance.Before.Context.Matches(latest.Before.Context)
                && advance.Before.Counter == latest.Before.Counter && advance.Before.Threshold == latest.Before.Threshold
                && advance.After.Counter == latest.After.Counter && advance.After.Threshold == latest.After.Threshold;

        /// <summary>废弃旧结果并更新周期代号；代号耗尽时停止该记录采样，避免回绕后旧凭据重新有效。</summary>
        private static void Invalidate(State state)
        {
            state.HasLast = false;
            if (state.Cycle == ulong.MaxValue) state.Exhausted = true;
            else state.Cycle++;
        }

        /// <summary>已知初始化／直接计时重设通知。没有旧记录时不为清理而分配新记录。</summary>
        internal static void NotifyReset(JobDriver_Sex driver)
        {
            if (driver != null && states.TryGetValue(driver, out var state)) Invalidate(state);
        }

        /// <summary>任务清理时解除弱表关联；尚未返回的旧回调不得据此操作未来新建的 State。</summary>
        internal static void Forget(JobDriver_Sex driver) { if (driver != null) states.Remove(driver); }

        /// <summary>打开初始化范围；最外层只失效一次，嵌套 base／override 共享当前记录的深度。</summary>
        internal static ResetScope BeginReset(JobDriver_Sex driver)
        {
            if (driver == null || !states.TryGetValue(driver, out var state)) return default;
            if (state.ResetDepth++ == 0) Invalidate(state);
            return new ResetScope(driver, state);
        }

        /// <summary>仅关闭原记录中真正打开的范围；同驱动已重建记录时忽略这个旧关闭请求。</summary>
        internal static void EndReset(ResetScope scope)
        {
            if (scope.Open && states.TryGetValue(scope.Driver, out var state)
                && ReferenceEquals(scope.StateOwner, state) && state.ResetDepth > 0) state.ResetDepth--;
        }

        /// <summary>读取事件入口的计数／周期；没有采样记录时不为每次 Orgasm 调用创建记录。</summary>
        internal static OrgasmScope BeginOrgasm(JobDriver_Sex driver)
            => driver != null && states.TryGetValue(driver, out var state)
                ? new OrgasmScope(state, driver.orgasms, state.Cycle) : default;

        /// <summary>同记录发生真实计数变化或异常才失效；Setup 已换周期时避免对成功事件再次换代。</summary>
        internal static void EndOrgasm(JobDriver_Sex driver, OrgasmScope scope, Exception error)
        {
            if (!scope.Open || driver == null || !states.TryGetValue(driver, out var state)
                || !ReferenceEquals(scope.StateOwner, state)) return;
            if (error != null || (driver.orgasms != scope.Count && state.Cycle == scope.Cycle)) Invalidate(state);
        }
    }
}
