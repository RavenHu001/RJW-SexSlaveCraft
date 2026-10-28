// AI 辅助生成：统一倍率、单操作提交和每驱动余数；不创建或补发高潮事件。
using System;
using System.Runtime.CompilerServices;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>只管理本模块的执行状态；原始采样和身体保存源保持独立。</summary>
    internal static class SSCOrgasmAdvanceRuntime
    {
        /// <summary>仅用于恢复诊断；Pending不代表已经接续，Rejected也不禁止当前周期重新从零推进。</summary>
        internal enum RestoreStatus { None, Pending, Restored, Rejected }
        /// <summary>当前人格、身体、实际系数及任务的进程内凭据，不跨读档保存对象地址。</summary>
        private readonly struct Binding
        {
            internal readonly Pawn Pawn, Master;
            internal readonly Job Job;
            internal readonly CompSexSlaveTraining Comp;
            internal readonly object Token;
            internal readonly SexProps Props;
            internal readonly Hediff_SSCOrgasmSensitivity Body;
            internal readonly Hediff_ChainOfSexSlave Chain;
            internal readonly int Level, Bits;
            internal readonly float Multiplier;

            /// <summary>绑定最终Stat及可选SSC载体，不要求每个RJW角色都有SSC身份或锁链。</summary>
            internal Binding(JobDriver_Sex d, SSCOrgasmSensitivityState sensitivity, float multiplier)
            {
                Pawn = d.pawn; Job = d.job; Props = d.Sexprops; Comp = Pawn.TryGetComp<CompSexSlaveTraining>();
                Token = SSCPersonalityRuntimeUtility.Capture(Pawn); Body = sensitivity.Record;
                Chain = SSCBondUtility.GetChain(Pawn); Master = Chain?.LinkedPawn;
                Level = sensitivity.EffectiveLevel; Multiplier = multiplier;
                Bits = BitConverter.SingleToInt32Bits(Multiplier);
            }

            /// <summary>实际数学和归属均相同时保留余数；仅锁链阶段或显示刷新不产生额外重置。</summary>
            internal bool Matches(Binding other) => ReferenceEquals(Pawn, other.Pawn) && ReferenceEquals(Job, other.Job)
                && ReferenceEquals(Props, other.Props) && ReferenceEquals(Comp, other.Comp) && ReferenceEquals(Token, other.Token)
                && ReferenceEquals(Body, other.Body) && ReferenceEquals(Chain, other.Chain) && ReferenceEquals(Master, other.Master)
                && Level == other.Level && Bits == other.Bits;
        }

        /// <summary>已知周期终态，必须先核对此端点才能生成新保存指纹，不能把未知写入洗成合法状态。</summary>
        private readonly struct Clock
        {
            internal readonly int Counter, Threshold, Start, Duration, Orgasms, Toil;
            /// <summary>读取当前原生周期与 Toil 阶段，不修改任何计时。</summary>
            internal Clock(JobDriver_Sex d)
            { Counter = d.sex_ticks; Threshold = d.orgasmstick; Start = d.orgasmStartTick; Duration = d.duration; Orgasms = d.orgasms; Toil = d.CurToilIndex; }
            /// <summary>读取载荷原始指纹，只用于比较，不能以它覆盖当前驱动。</summary>
            internal Clock(SSCOrgasmAdvanceSaveState p)
            { Counter = p.Counter; Threshold = p.Threshold; Start = p.StartCounter; Duration = p.Duration; Orgasms = p.Orgasms; Toil = p.ToilIndex; }
            /// <summary>所有周期字段与阶段均相同才是已知终态。</summary>
            internal bool Matches(Clock other) => Counter == other.Counter && Threshold == other.Threshold && Start == other.Start
                && Duration == other.Duration && Orgasms == other.Orgasms && Toil == other.Toil;
            /// <summary>原写回后用样本 Before 核对此前终态，不把原推进误判成外部重设。</summary>
            internal bool MatchesBefore(SSCOrgasmAdvance a) => Counter == a.Before.Counter && Threshold == a.Before.Threshold
                && Start == a.Before.StartCounter && Duration == a.Before.Duration && Orgasms == a.Before.Orgasms && Toil == a.Driver.CurToilIndex;
        }

        /// <summary>待恢复节点与运行余数分开；构建中的重设不能提前删除尚未校验的载荷。</summary>
        private sealed class State
        {
            // Payload只承载旧档事实，Running／Carry才是当前已提交状态；普通运行清理不能抹掉加载中的节点。
            internal SSCOrgasmAdvanceSaveState Payload;
            internal RestoreStatus Status;
            internal bool Pending, LoadStarted, LoadingReset, Tainted, BuildSeen, BuildSuccess, Candidate, Running;
            internal int BuildDepth;
            internal ulong BuildGeneration;
            internal Clock Terminal, Restored;
            internal Binding Binding;
            internal double Carry;
            // 去重绑定采样器发行者及周期／操作号，不使用全局tick，也不跨读档保存。
            internal object ConsumedOwner;
            internal ulong ConsumedCycle, ConsumedOperation;
        }

        /// <summary>每次构建分别记录成功与恢复候选，内层成功不能替外层被捕获的异常作证明。</summary>
        private sealed class BuildFrame
        {
            internal JobDriver_Sex Driver;
            internal State Owner;
            internal ulong Generation;
            internal BuildFrame Parent;
            internal bool Success, Restored, Closed;
            internal Clock Clock;
        }

        [ThreadStatic] private static BuildFrame currentBuild;

        /// <summary>绑定具体调用帧、记录和世代，防止 Cleanup 后旧 Finalizer 关闭新记录。</summary>
        internal readonly struct BuildScope
        {
            private readonly BuildFrame frame;
            /// <summary>捕获某一次调用帧，不能只凭驱动实例关闭后来重新打开的范围。</summary>
            private BuildScope(BuildFrame frame) { this.frame = frame; }
            /// <summary>标识本回调是否真正打开范围；被第三方跳过的Prefix不得关闭其他范围。</summary>
            internal bool Open => frame != null;
            /// <summary>只向本模块关闭方法传递不透明帧，不暴露可写的恢复状态。</summary>
            internal object Frame => frame;
            /// <summary>在嵌套类型的访问边界内封装原帧，不另建一个恢复候选。</summary>
            internal static BuildScope Create(object frame) => new BuildScope((BuildFrame)frame);
        }

        private static readonly ConditionalWeakTable<JobDriver_Sex, State> states = new ConditionalWeakTable<JobDriver_Sex, State>();
        private static readonly ConditionalWeakTable<JobDriver_Sex, State>.CreateValueCallback create = _ => new State();

        /// <summary>所有当前RJW驱动读取最终Stat；正常速度不创建余数记录，SSC档位仅是可选属性来源。</summary>
        private static bool TryBinding(JobDriver_Sex d, out Binding binding)
        {
            binding = default;
            if (!SSCOrgasmProgressUtility.IsCurrentDriver(d) || d.pawn.health?.hediffSet == null) return false;
            float multiplier = SSCOrgasmFrequencyUtility.GetMultiplier(d.pawn);
            if (multiplier <= 1f) return false;
            var sensitivity = SSCOrgasmSensitivityUtility.GetState(d.pawn);
            binding = new Binding(d, sensitivity, multiplier);
            return true;
        }

        /// <summary>清理已提交运行状态，不影响独立待恢复载荷。</summary>
        private static void ClearRun(State state)
        {
            if (state.Status == RestoreStatus.Restored) state.Status = RestoreStatus.Rejected;
            state.Running = false; state.Carry = 0; state.ConsumedOwner = null;
            state.ConsumedCycle = state.ConsumedOperation = 0;
        }

        /// <summary>读档载荷最终判为无效才释放；绝不把节点里的数据反写到原生计时或身体档位。</summary>
        private static void DropPending(State state)
        { if (state.Pending) state.Status = RestoreStatus.Rejected; state.Pending = false; state.Payload = null; }

        /// <summary>一次绑定当前周期，只接续小数余数，不恢复旧采样操作号。</summary>
        private static void Bind(State state, Binding binding, Clock clock, double carry)
        {
            ClearRun(state); state.Binding = binding; state.Terminal = clock; state.Carry = carry; state.Running = true;
        }

        /// <summary>首次推进前或安全保存边界验证待恢复节点；构建期只积累事实，不抢先消费。</summary>
        private static void ResolvePending(JobDriver_Sex d, State state, Binding binding)
        {
            if (!state.Pending) return;
            var p = state.Payload;
            var clock = new Clock(d);
            // 构建成功、引用归属、当前配置和原周期必须同时匹配；数字相同不能抵消一次真正的新周期。
            bool valid = SSCOrgasmProgressPatches.RecoveryReady && state.BuildSeen && state.BuildSuccess && state.BuildDepth == 0
                && !state.Tainted && (!state.LoadingReset || (state.Candidate && state.Restored.Matches(clock)))
                && p != null && p.SchemaVersion == 1 && double.IsFinite(p.Carry) && p.Carry >= 0 && p.Carry < 1
                && p.JobLoadId == d.job.loadID && p.DriverType == d.GetType().FullName
                && MatchesSavedBinding(p, binding) && new Clock(p).Matches(clock);
            double carry = valid ? p.Carry : 0;
            // 此处只接续余数并重绑本进程凭据，不改原生计时，也不恢复旧操作号供再次消费。
            DropPending(state);
            Bind(state, binding, clock, carry);
            state.Status = valid ? RestoreStatus.Restored : RestoreStatus.Rejected;
        }

        /// <summary>加载完成后的资格事件也核对待恢复归属，防止先换主／改档再恢复原值时复活旧余数。</summary>
        private static bool MatchesSavedBinding(SSCOrgasmAdvanceSaveState payload, Binding binding)
            => payload != null && ReferenceEquals(payload.Actor, binding.Pawn) && ReferenceEquals(payload.Body, binding.Body) && ReferenceEquals(payload.Chain, binding.Chain)
                && ReferenceEquals(payload.Master, binding.Master) && payload.Level == binding.Level && payload.MultiplierBits == binding.Bits;

        /// <summary>在原计数写回之前恢复／校验执行端点；加载期误推进只污染本模块恢复凭据。</summary>
        internal static void BeforeAdvance(JobDriver_Sex d, bool ready)
        {
            if (d == null) return;
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                var loading = states.GetValue(d, create); loading.Tainted = true; ClearRun(loading); return;
            }
            if (!ready || !TryBinding(d, out var binding))
            {
                if (states.TryGetValue(d, out var old)) { ClearRun(old); DropPending(old); }
                return;
            }
            var state = states.GetValue(d, create);
            if (state.BuildDepth != 0) { state.Tainted = true; ClearRun(state); DropPending(state); return; }
            ResolvePending(d, state, binding);
            var current = new Clock(d);
            // 原表达式尚未写回，端点必须仍等于上次接受值；外部未知改写按新边界从零开始。
            if (!state.Running || !state.Binding.Matches(binding) || !state.Terminal.Matches(current)) Bind(state, binding, current, 0);
        }

        /// <summary>统一饱和公式。无倍率分支、无最短等待，也不按倍率或高潮次数循环。</summary>
        /// <param name="raw">本次已完成的原始整数推进量；原流程已执行它，不能再完整乘一次倍率。</param>
        /// <param name="remaining">原写回之后距当前阈值的非负距离，额外整数推进不得超过它。</param>
        /// <param name="nextCarry">只保留理论量的小数部分；整数超额丢弃，真实事件或周期失效另行清理。</param>
        internal static bool TryCalculate(long raw, double multiplier, double carry, long remaining, out long extra, out double nextCarry)
        {
            extra = 0; nextCarry = 0;
            if (raw < 0 || remaining < 0 || !double.IsFinite(multiplier) || multiplier < 1 || !double.IsFinite(carry) || carry < 0 || carry >= 1) return false;
            if (raw == 0) { nextCarry = carry; return true; }
            double factor = multiplier - 1;
            // 先比较饱和界限，再相乘，极大有限倍率不会先溢出；只丢弃超过阈值的整数超额。
            if (remaining == 0 || factor >= ((double)remaining - carry) / raw)
            {
                extra = remaining;
                // raw是整数，factor的整数部分不影响小数余数。剥离后再乘，避免极值溢出或吞掉原有余数。
                // 到达阈值不等于发生真实高潮：第三方可能跳过原方法，故余数仍等真实事件／周期清理。
                double fractional = carry + raw * (factor - Math.Floor(factor));
                nextCarry = fractional - Math.Floor(fractional);
                return nextCarry >= 0 && nextCarry < 1;
            }
            double theoretical = carry + raw * factor;
            if (!double.IsFinite(theoretical) || theoretical < 0) return false;
            extra = (long)Math.Floor(theoretical);
            nextCarry = theoretical - extra;
            return extra <= remaining && nextCarry >= 0 && nextCarry < 1;
        }

        /// <summary>共享原始样本只提交一次；所有检查及算术在局部量完成后才写计数与余数。</summary>
        internal static void Apply(SSCOrgasmAdvance advance)
        {
            var d = advance.Driver;
            if (d == null || !states.TryGetValue(d, out var state) || !state.Running) return;
            var context = advance.Before.Context;
            // 已消费的同一原始样本只读复用；即使调用者再次交回，也不追加推进或重新累积余数。
            if (ReferenceEquals(state.ConsumedOwner, context.CycleOwner) && state.ConsumedCycle == context.Cycle && state.ConsumedOperation == context.Operation) return;
            if (Scribe.mode != LoadSaveMode.Inactive || !SSCOrgasmProgressUtility.IsStillCurrent(advance)
                || d.sex_ticks != advance.After.Counter || !state.Terminal.MatchesBefore(advance)
                || !TryBinding(d, out var binding) || !state.Binding.Matches(binding)) { ClearRun(state); return; }
            long remaining = Math.Max(0L, (long)d.sex_ticks - d.orgasmstick);
            if (!TryCalculate(advance.RawAdvance, binding.Multiplier, state.Carry, remaining, out long extra, out double carry)) { ClearRun(state); return; }
            int target = (int)((long)d.sex_ticks - extra);
            // 此处没有外部回调；以同一操作同时提交计时终态、余数和去重凭据，保留原 Orgasm 调用链。
            // 没有额外整数推进时只提交执行凭据，零原始量不产生一次多余的字段写回。
            if (extra != 0) d.sex_ticks = target;
            state.Carry = carry; state.Terminal = new Clock(d);
            state.ConsumedOwner = context.CycleOwner; state.ConsumedCycle = context.Cycle; state.ConsumedOperation = context.Operation;
            SSCOrgasmProgressSampling.AcceptSupplement(advance, target);
        }

        /// <summary>已知新周期／直接重设。加载中仅标记事实，不提前删除节点引用或待恢复余数。</summary>
        internal static void NotifyReset(JobDriver_Sex d)
        {
            if (d == null || !states.TryGetValue(d, out var state)) return;
            ClearRun(state);
            if (Scribe.mode != LoadSaveMode.Inactive || state.BuildDepth > 0) state.LoadingReset = true;
            else DropPending(state);
        }

        /// <summary>身体资格事件只清数学或归属已改变的执行状态，普通界面刷新和无关心情改动不清余数。</summary>
        internal static void NotifyBodyChanged(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || !(pawn?.jobs?.curDriver is JobDriver_Sex d) || !states.TryGetValue(d, out var state)) return;
            if (!TryBinding(d, out var binding) || (state.Running && !state.Binding.Matches(binding))
                || (state.Pending && !MatchesSavedBinding(state.Payload, binding))) { ClearRun(state); DropPending(state); }
        }

        /// <summary>同驱动 LoadingVars 使用同一记录；后续 Scribe 阶段不能重建或抹掉已恢复的节点。</summary>
        internal static void BeginLoading(JobDriver_Sex d)
        {
            if (d == null || Scribe.mode != LoadSaveMode.LoadingVars) return;
            var state = states.GetValue(d, create);
            if (state.LoadStarted) return;
            state.LoadStarted = true; ClearRun(state);
        }

        /// <summary>驱动基类只深存一个附加节点；子节点的引用与 PostLoadInit 由 Scribe 自己调度。</summary>
        internal static void Expose(JobDriver_Sex d)
        {
            if (d == null) return;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 使用新的局部保存节点，不把保存快照倒灌到运行记录；保存本身不能推进计时。
                SSCOrgasmAdvanceSaveState payload = CreateSave(d);
                Scribe_Deep.Look(ref payload, "sscOrgasmAdvanceState");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // 节点必须由同一State持有到加载结束；其自身引用回调由Scribe注册，不手动递归。
                var state = states.GetValue(d, create);
                Scribe_Deep.Look(ref state.Payload, "sscOrgasmAdvanceState");
                state.Pending = state.Payload != null;
                state.Status = state.Pending ? RestoreStatus.Pending : RestoreStatus.None;
            }
        }

        /// <summary>安全保存同样先核对已知终态；读档后尚未推进的合法余数可验证接续后再次保存。</summary>
        private static SSCOrgasmAdvanceSaveState CreateSave(JobDriver_Sex d)
        {
            if (!states.TryGetValue(d, out var state)) return null;
            if (!TryBinding(d, out var binding)) { ClearRun(state); DropPending(state); return null; }
            if (state.BuildDepth != 0 || !SSCOrgasmProgressPatches.RecoveryReady) { ClearRun(state); DropPending(state); return null; }
            // 读档后尚未推进就再次保存，也要先验证原载荷，再检查已知终态；不能无条件转存或丢弃。
            ResolvePending(d, state, binding);
            if (!state.Running || !state.Binding.Matches(binding) || !state.Terminal.Matches(new Clock(d))) { ClearRun(state); return null; }
            if (!double.IsFinite(state.Carry) || state.Carry < 0 || state.Carry >= 1) { ClearRun(state); return null; }
            if (state.Carry <= 0) return null;
            var c = state.Terminal;
            return new SSCOrgasmAdvanceSaveState
            {
                SchemaVersion = 1, Carry = state.Carry, JobLoadId = d.job.loadID, DriverType = d.GetType().FullName,
                Actor = binding.Pawn, Body = binding.Body, Chain = binding.Chain, Master = binding.Master, Level = binding.Level, MultiplierBits = binding.Bits,
                Counter = c.Counter, Threshold = c.Threshold, StartCounter = c.Start, Duration = c.Duration, Orgasms = c.Orgasms, ToilIndex = c.Toil
            };
        }

        /// <summary>外围构建范围记住原记录及世代；成功只由正常 try 离开处的 IL 标记确认。</summary>
        internal static BuildScope BeginBuild(JobDriver_Sex d)
        {
            var state = states.GetValue(d, create);
            if (state.BuildDepth++ == 0)
            {
                ClearRun(state); state.BuildSeen = true; state.BuildSuccess = false; state.Candidate = false;
                if (state.BuildGeneration == ulong.MaxValue) { state.Tainted = true; }
                else state.BuildGeneration++;
            }
            var frame = new BuildFrame { Driver = d, Owner = state, Generation = state.BuildGeneration, Parent = currentBuild };
            // 每次调用都持有独立帧；同驱动重入时，内层正常退出不能替外层被catch的失败作证明。
            currentBuild = frame;
            return BuildScope.Create(frame);
        }

        /// <summary>只在正常枚举及 Dispose 完成后由成功锚点调用，不使用共享 ret 或无异常 Finalizer 猜测。</summary>
        internal static void MarkBuildSuccess(JobDriver driver)
        {
            var frame = currentBuild;
            if (frame != null && !frame.Closed && ReferenceEquals(frame.Driver, driver)) frame.Success = true;
        }

        /// <summary>自我调教完整枚举到达末尾，原恢复值已经写完且所有必要 Toil 已检查；这里只记候选。</summary>
        internal static void MarkRestored(JobDriver_Sex d)
        {
            var frame = currentBuild;
            if (Scribe.mode == LoadSaveMode.PostLoadInit && frame != null && !frame.Closed && ReferenceEquals(frame.Driver, d))
            { frame.Restored = true; frame.Clock = new Clock(d); }
        }

        /// <summary>异常／内部捕获／被跳过的构建均不能接续余数，旧关闭请求不会触碰重建记录。</summary>
        internal static void EndBuild(BuildScope scope, Exception error)
        {
            if (!scope.Open) return;
            var frame = (BuildFrame)scope.Frame;
            if (frame.Closed) return;
            frame.Closed = true;
            bool top = ReferenceEquals(currentBuild, frame);
            if (top) currentBuild = frame.Parent;
            // 即使Cleanup重建了同一驱动的弱表记录，也只退栈旧帧，不关闭或污染新记录。
            if (!states.TryGetValue(frame.Driver, out var state) || !ReferenceEquals(frame.Owner, state)
                || frame.Generation != state.BuildGeneration || state.BuildDepth <= 0) return;
            if (!top || error != null || !frame.Success) state.Tainted = true;
            if (--state.BuildDepth == 0)
            {
                // 外层正常路径标记、当前任务及完整Toil共同决定成功；Finalizer没有异常本身不足为证。
                state.BuildSuccess = frame.Success && error == null;
                state.Candidate = frame.Restored; state.Restored = frame.Clock;
                if (!state.BuildSuccess || !SSCOrgasmProgressUtility.IsCurrentDriver(frame.Driver) || !SSCOrgasmProgressPatches.HasValidToils(frame.Driver)) state.Tainted = true;
                if (state.Candidate && !state.Restored.Matches(new Clock(frame.Driver))) state.Candidate = false;
            }
        }

        /// <summary>仅调试／原生专项只读查询已提交余数；无记录时不分配、不恢复载荷。</summary>
        internal static double GetCarry(JobDriver_Sex d) => d != null && states.TryGetValue(d, out var state) && state.Running ? state.Carry : 0;

        /// <summary>只读区分无载荷、待恢复、已接续及已失效；诊断不创建记录、不触发恢复。</summary>
        internal static RestoreStatus GetRestoreStatus(JobDriver_Sex d)
            => d != null && states.TryGetValue(d, out var state) ? state.Status : RestoreStatus.None;

        /// <summary>清理后移除索引，后续旧范围或旧样本不能把旧执行状态挂到新任务上。</summary>
        internal static void Forget(JobDriver_Sex d) { if (d != null) states.Remove(d); }
    }
}
