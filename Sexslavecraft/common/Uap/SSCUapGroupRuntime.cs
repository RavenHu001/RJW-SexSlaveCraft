// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using rjw;
using RimWorld;
using Verse;
using Verse.AI;

[assembly: InternalsVisibleTo("UapGroupNative")]

namespace SexSlaveCraft
{
    /// <summary>仅 1.7：把手动请求、真实配对和运行任务关联起来；本次许可不写入人格或存档。</summary>
    internal static class SSCUapGroupRuntime
    {
        /// <summary>记录下令时的身体载体、人格世代及绑定对象；集合后不能重新捕获来延长旧请求权限。</summary>
        internal sealed class Person
        {
            internal Pawn Pawn;
            internal CompSexSlaveTraining Comp;
            internal object Personality;
            internal Pawn Owner;
            internal Hediff_ChainOfSexSlave Chain;
            /// <summary>只读核对原 Comp、人格、锁链及主人；人格替换进行中也不能继续使用旧请求。</summary>
            internal bool Current => Pawn != null && Pawn.TryGetComp<CompSexSlaveTraining>() == Comp &&
                !SSCPersonalityRuntimeUtility.IsMutating(Pawn) && SSCPersonalityRuntimeUtility.IsCurrent(Pawn, Personality) &&
                SSCBondUtility.GetBoundMaster(Pawn) == Owner && SSCBondUtility.GetChain(Pawn) == Chain;
        }

        /// <summary>一份实际 Job 的归属记录；集合等待与真正行为分别登记，避免清理同类的新任务。</summary>
        internal sealed class Work
        {
            internal Batch Batch;
            internal Pawn Pawn, Partner;
            internal Job Job;
            internal int Id;
            internal JobDriver Driver;
            internal Toil Barrier;
            internal bool Gather, Started, Finalized, Attempted;
            /// <summary>比较 Job 引用和编号；运行驱动一旦登记，还必须由该驱动继续持有任务。</summary>
            internal bool Owned => Job != null && Job.loadID == Id && Pawn?.CurJob == Job &&
                (Driver == null || Pawn.jobs.curDriver == Driver);
        }

        /// <summary>一个请求到会话的完整运行记录；提交仅代表任务安装成功，不代表所有 RJW 初始化已执行。</summary>
        internal sealed class Batch
        {
            internal Game Game;
            internal Map Map;
            internal Thing Anchor;
            internal object Request, Match, Session;
            internal int SessionId;
            // Manual 证明窗口来源，Override 固定下令选择；Committed 只放行安装屏障。
            // Closed 先撤销新准入，Aborted 区分失败清场和正常完成后的旧任务收尾。
            internal bool Manual, Override, Committed, Closed, Aborted;
            internal readonly List<Person> People = new List<Person>();
            internal readonly List<Work> Gathering = new List<Work>();
            internal readonly List<Work> Jobs = new List<Work>();
            internal string Failure;
            // 只保留最近一次预检的拒绝诊断；每次 CheckPlan 都清空，不作为缓存许可。
            internal SSCRestrictionDecision Denial;
            /// <summary>核对原游戏、地图、发生点和参与者有效性；不在这里重新分配角色或授予豁免。</summary>
            internal bool Current => ReferenceEquals(Game, Verse.Current.Game) && Anchor != null && Anchor.Spawned &&
                !Anchor.Destroyed && Anchor.Map == Map && People.All(p => p.Current && p.Pawn.Spawned &&
                    p.Pawn.Map == Map && !p.Pawn.Dead && !p.Pawn.Drafted);
        }

        /// <summary>保存同步调用链的请求上下文；跨 tick 的关联由请求弱表承担，不依赖线程字段长期授权。</summary>
        internal sealed class Scope : IDisposable
        {
            private readonly Batch previous;
            /// <summary>进入嵌套请求时保存外层记录，允许未知请求以空上下文隔离外层许可。</summary>
            internal Scope(Batch value) { previous = Ambient; Ambient = value; }
            /// <summary>恢复进入前的同线程上下文，保留外层请求或回调已有的归属。</summary>
            public void Dispose() { Ambient = previous; }
        }

        /// <summary>原回调同步换任务后，取消／完成通知仍属于旧驱动，不能转交给同 Pawn 的新会话。</summary>
        internal sealed class CallbackScope : IDisposable
        {
            private readonly Work previous;
            /// <summary>在实际驱动回调期间固定原工作，避免同步换任务后把通知解释为新会话的通知。</summary>
            internal CallbackScope(Work value) { previous = Executing; Executing = value; }
            /// <summary>恢复外层实际回调归属；异常退出与正常返回使用同一路径。</summary>
            public void Dispose() { Executing = previous; }
        }

        /// <summary>仅在窗口下令的同步调用中保存原选择；必须再匹配发生点、参与者集合和动画才算手动来源。</summary>
        internal sealed class ManualCall
        {
            internal Thing Anchor;
            internal Pawn[] Pawns;
            internal string Animation;
        }

        /// <summary>登记加载时不能恢复的群体驱动；保留原游戏、Job 对象和编号，供 Scribe 结束后精确取消。</summary>
        internal sealed class Orphan
        {
            internal JobDriver Driver;
            internal Job Job;
            internal int Id;
            internal Game Game;
            /// <summary>只核对加载记录仍属于原游戏和 Job；是否仍为 Pawn 当前驱动由清理入口再次确认。</summary>
            internal bool Current => ReferenceEquals(Game, Verse.Current.Game) && Driver.job == Job && Job?.loadID == Id;
        }

        // 三个同步作用域分别承载请求、窗口来源和实际回调；真实跨 tick 身份保存在下方弱表。
        [ThreadStatic] internal static Batch Ambient;
        [ThreadStatic] internal static ManualCall Manual;
        [ThreadStatic] internal static Work Executing;
        private static readonly ConditionalWeakTable<object, Batch> requests = new ConditionalWeakTable<object, Batch>();
        private static readonly ConditionalWeakTable<Job, Work> work = new ConditionalWeakTable<Job, Work>();
        private static readonly ConditionalWeakTable<JobDriver, Orphan> orphans = new ConditionalWeakTable<JobDriver, Orphan>();
        // 即使 Job 已清理，仍由旧 SexProps 记住旧批次，避免结算回调落回新任务。
        private static readonly ConditionalWeakTable<SexProps, Work> outcomes = new ConditionalWeakTable<SexProps, Work>();
        private static readonly AccessTools.FieldRef<JobDriver, List<Toil>> toils =
            AccessTools.FieldRefAccess<JobDriver, List<Toil>>("toils");

        /// <summary>下令时固定原载体和设置；后来开启豁免不能给已存在的请求补发许可。</summary>
        internal static Batch Create(Thing anchor, IList<Pawn> pawns, object match, bool manual)
        {
            var batch = new Batch { Game = Current.Game, Map = anchor?.Map, Anchor = anchor, Match = match,
                Manual = manual, Override = manual && (SSCMod.settings?.allowUapManualGroupRestrictionOverride ?? false) };
            foreach (Pawn pawn in pawns.Where(p => p != null).Distinct())
                batch.People.Add(new Person { Pawn = pawn, Comp = pawn.TryGetComp<CompSexSlaveTraining>(),
                    Personality = SSCPersonalityRuntimeUtility.Capture(pawn), Owner = SSCBondUtility.GetBoundMaster(pawn),
                    Chain = SSCBondUtility.GetChain(pawn) });
            Current.Game?.GetComponent<SSCUapGroupGameComponent>()?.Batches.Add(batch);
            return batch;
        }

        /// <summary>按 Pawn 对象比较完整参与者集合，允许 UAP 调整顺序，但不接受换入另一名参与者。</summary>
        internal static bool SamePeople(Batch batch, IEnumerable<Pawn> pawns)
            => pawns != null && new HashSet<Pawn>(pawns).SetEquals(batch.People.Select(p => p.Pawn));

        /// <summary>只读查找原请求对象的批次，不按发生点或人物编号猜测另一个请求的归属。</summary>
        internal static bool FindRequest(object request, out Batch batch) => requests.TryGetValue(request, out batch);
        /// <summary>在下令作用域中绑定真实请求对象；后续集合 tick 沿该对象恢复原记录。</summary>
        internal static void BindRequest(object request, Batch batch)
        { batch.Request = request; requests.Add(request, batch); }

        /// <summary>整组预检不创造 Job、配置或开始凭据；所有机械配对都沿实际发起方向检查。</summary>
        internal static bool CheckPlan(Batch batch, object plan, out string reason)
        {
            reason = null;
            batch.Denial = null;
            if (plan == null)
                reason = "SSC_Uap_StartFailed".Translate();
            else if (!batch.Current || !SamePeople(batch, SSCUapGroupApi.Get<List<Pawn>>(plan,
                ReferenceEquals(plan, batch.Session) ? "Participants" : "OrderedParticipants")))
                reason = "SSC_Uap_RequestChanged".Translate();
            else if (batch.Override && !(SSCMod.settings?.allowUapManualGroupRestrictionOverride ?? false))
                reason = "SSC_Uap_RequestChanged".Translate();
            else
            {
                // 当前 UAP 使用共享接收者的星形配对；结构变化必须拒绝，不能按旧模型部分分配。
                var pairs = SSCUapGroupApi.Pairs(plan).ToList();
                Pawn hub = SSCUapGroupApi.Get<Pawn>(plan, "Receiver");
                Pawn primary = SSCUapGroupApi.Get<Pawn>(plan, "PrimaryInitiator");
                if (pairs.Count == 0 || hub == null || !pairs.Any(p => p.Actor == primary) ||
                    pairs.Select(p => p.Actor).Distinct().Count() != pairs.Count || pairs.Any(p => p.Target != hub))
                    reason = "SSC_Uap_StartFailed".Translate();
                foreach (var pair in pairs)
                {
                    if (reason != null) break;
                    if (pair.Actor == null || pair.Target == null || pair.Actor == pair.Target ||
                        !batch.People.Any(p => p.Pawn == pair.Actor) || !batch.People.Any(p => p.Pawn == pair.Target))
                        reason = "SSC_Uap_RequestChanged".Translate();
                    // 豁免只跳过本场普通行为许可，参与者归属和计划结构始终检查。
                    else if (!batch.Override)
                    {
                        var decision = SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(pair.Actor, pair.Target,
                            SSCInteractionKind.Consensual, true));
                        if (!decision.Allowed)
                        {
                            batch.Denial = decision;
                            reason = "SSC_Uap_PairRejected".Translate(pair.Actor.LabelShortCap, pair.Target.LabelShortCap,
                                ("SSC_Restrictions_Reason_" + decision.Reason).Translate());
                        }
                    }
                }
            }
            return reason == null;
        }

        /// <summary>登记 Job 对象、编号及方向；行为 Job 在调度前登记，集合 Job 在派发后认领，清理范围分别保存。</summary>
        internal static Work Register(Batch batch, Pawn pawn, Job job, Pawn partner, bool gather)
        {
            var slot = new Work { Batch = batch, Pawn = pawn, Partner = partner, Job = job, Id = job.loadID, Gather = gather };
            work.Remove(job); work.Add(job, slot);
            (gather ? batch.Gathering : batch.Jobs).Add(slot);
            return slot;
        }

        /// <summary>仅认原 Job 编号及人物／驱动；池化后的新任务不继承旧记录，迟到驱动不能擦掉新候选。</summary>
        internal static bool Find(JobDriver driver, out Work slot)
        {
            slot = null;
            if (driver?.job == null || !work.TryGetValue(driver.job, out Work candidate)) return false;
            if (candidate.Id != driver.job.loadID) { work.Remove(driver.job); return false; }
            if (candidate.Pawn != driver.pawn || (candidate.Driver != null && candidate.Driver != driver) ||
                (candidate.Driver == null && (driver.pawn?.jobs?.curDriver != driver || driver.pawn.CurJob != driver.job)))
                return false;
            slot = candidate;
            return true;
        }

        /// <summary>精确检查必须先于 Guard 的 Started 快速路径；过期票据不能回退主人例外重新取权。</summary>
        internal static bool Check(JobDriver_Sex driver, out bool allowed)
        {
            allowed = true;
            if (!Find(driver, out Work slot)) return false;
            Batch batch = slot.Batch;
            allowed = !slot.Gather && !batch.Aborted && batch.Current && slot.Id == driver.job.loadID &&
                slot.Pawn == driver.pawn && slot.Owned && driver.Target == slot.Partner &&
                (slot.Driver == null || slot.Driver == driver) &&
                ((!batch.Closed && SSCUapGroupApi.OwnsSession(batch)) || (batch.Closed && slot.Started));
            if (allowed) allowed = Context(driver, out var context) && context.DirectionKnown;
            if (allowed) slot.Driver = driver;
            else batch.Failure = "SSC_Uap_RequestChanged".Translate();
            return true;
        }

        /// <summary>只为已登记任务还原准备方向；不从接收者的旧 SexProps 猜测发起者。</summary>
        internal static bool Context(JobDriver_Sex driver, out SSCRestrictionRequest request)
        {
            request = null;
            if (!Find(driver, out Work slot) || slot.Gather) return false;
            bool receiver = driver is JobDriver_SexBaseReciever;
            Pawn actor = receiver ? slot.Partner : slot.Pawn, target = receiver ? slot.Pawn : slot.Partner;
            bool propsMatch = driver.Sexprops == null || (!driver.Sexprops.isRape &&
                driver.Sexprops.initiator == actor && driver.Sexprops.recipient == target);
            request = new SSCRestrictionRequest(actor, target, SSCInteractionKind.Consensual,
                slot.Id == driver.job.loadID && slot.Pawn == driver.pawn && driver.Target == slot.Partner &&
                driver.Partner == slot.Partner && propsMatch);
            return true;
        }

        /// <summary>统一处理已登记 UAP 任务的准备准入；无豁免时复用原 Policy，返回 false 表示不由本适配负责。</summary>
        internal static bool Evaluate(JobDriver_Sex driver, out SSCRestrictionDecision decision, out string reason)
        {
            decision = null; reason = null;
            if (!Find(driver, out Work slot) || slot.Gather) return false;
            bool allowed = Check(driver, out bool valid) && valid;
            // 安装期间整组复查，避免后一个发起者被拒绝却留下前一个已安装的任务。
            if (allowed && !slot.Batch.Committed) allowed = CheckPlan(slot.Batch, slot.Batch.Session, out reason);
            if (!allowed) decision = slot.Batch.Denial;
            if (allowed && !slot.Batch.Override)
            {
                Context(driver, out SSCRestrictionRequest request);
                decision = SSCRestrictionPolicy.Evaluate(request);
                allowed = decision.Allowed;
            }
            decision ??= new SSCRestrictionDecision { Allowed = allowed,
                Reason = allowed ? SSCRestrictionReason.Allowed : SSCRestrictionReason.IncompleteContext };
            if (!allowed) reason ??= slot.Batch.Failure ?? "SSC_Uap_StartFailed".Translate();
            return true;
        }

        /// <summary>由原生 Start 成功后的守卫回调登记真实开始，并把本次 SexProps 绑定到原工作以防迟到结算。</summary>
        internal static void MarkStarted(JobDriver_Sex driver)
        {
            if (!Find(driver, out Work slot) || !slot.Owned) return;
            slot.Started = true;
            if (driver.Sexprops != null) { outcomes.Remove(driver.Sexprops); outcomes.Add(driver.Sexprops, slot); }
        }

        /// <summary>接管任务必须完成整组安装并仍属原批次，才能进入真正 Start；普通任务不受此门槛影响。</summary>
        internal static bool CanStart(JobDriver driver) => !Find(driver, out Work slot) ||
            (!slot.Batch.Closed && slot.Batch.Committed && slot.Batch.Current && slot.Owned);

        /// <summary>接管任务只有真正开始且原角色、Job 和驱动仍匹配时才允许 End，阻止准备任务伪收尾。</summary>
        internal static bool CanEnd(JobDriver_Sex driver) => !Find(driver, out Work slot) ||
            (slot.Started && slot.Id == driver.job.loadID && slot.Driver == driver && slot.Pawn == driver.pawn &&
                driver.Target == slot.Partner && slot.Batch.Current);

        /// <summary>在 tick 或步骤推进前复核任务与会话；失效时先阻止推进，批次清理由安全更新完成。</summary>
        internal static bool CanAdvance(JobDriver driver)
        {
            if (IsOrphan(driver)) return false;
            if (!Find(driver, out Work slot) || slot.Gather) return true;
            // 正常完成后原驱动还需要退出等待步骤；取消批次则必须停止继续推进。
            if (slot.Batch.Closed) return !slot.Batch.Aborted;
            if (slot.Batch.Current && slot.Owned && SSCUapGroupApi.OwnsSession(slot.Batch) && slot.Batch.Failure == null) return true;
            slot.Batch.Failure = "SSC_Uap_RequestChanged".Translate();
            return false;
        }

        /// <summary>仅对接管驱动核对真实开始和归属，未登记驱动交回原流程；取消、关闭和读档孤儿不能取得完成许可。</summary>
        internal static bool Outcome(JobDriver driver)
            => !IsOrphan(driver) && (!Find(driver, out Work slot) || (!slot.Batch.Aborted && !slot.Batch.Closed && slot.Batch.Current &&
                SSCUapGroupApi.OwnsSession(slot.Batch) && slot.Started && slot.Owned && slot.Id == driver.job.loadID));

        /// <summary>沿原 SexProps 查找工作，防止旧任务清理后直接调用 ProcessSex 时改借新驱动的权限。</summary>
        internal static bool Outcome(SexProps props)
        {
            if (props == null) return true;
            if (!outcomes.TryGetValue(props, out Work slot)) return Outcome(props.initiator?.jobs?.curDriver);
            // 新会话可能已覆盖人物索引，而 StartJob 尚未清理旧驱动；Job 仍当前不能单独证明结算归属。
            return slot.Started && !slot.Batch.Closed && !slot.Batch.Aborted && slot.Batch.Current && slot.Owned &&
                SSCUapGroupApi.OwnsSession(slot.Batch) &&
                slot.Driver is JobDriver_Sex driver && driver.Sexprops == props && props.initiator == slot.Pawn &&
                props.recipient == slot.Partner;
        }

        /// <summary>只在接管的启动批次前置等待；全部任务安装后才运行第三方真正的初始化回调。</summary>
        internal static IEnumerable<Toil> Gate(JobDriver driver, IEnumerable<Toil> original)
        {
            if (!Find(driver, out Work slot) || slot.Gather) { foreach (Toil toil in original) yield return toil; yield break; }
            var gate = new Toil { defaultCompleteMode = ToilCompleteMode.Never, socialMode = RandomSocialMode.Off };
            slot.Barrier = gate;
            gate.tickAction = () =>
            {
                if (slot.Batch.Aborted || !slot.Batch.Current || slot.Batch.Failure != null)
                    Close(slot.Batch, true, "SSC_Uap_RequestChanged".Translate());
                else if (slot.Batch.Committed && slot.Owned) driver.ReadyForNextToil();
            };
            // SetupToils 会枚举后续步骤，但它们的 initAction 只有提交后离开此等待步骤才会执行。
            yield return gate;
            foreach (Toil toil in original)
            {
                // 固定每个原初始化回调的工作归属，防止同步 ProcessSex 或异常处理换任务后串到新会话。
                Action init = toil.initAction;
                if (init != null)
                    toil.initAction = () =>
                    {
                        if (!slot.Owned || slot.Batch.Aborted || slot.Batch.Closed || !slot.Batch.Current) return;
                        using (new CallbackScope(slot)) init();
                    };
                yield return toil;
            }
        }

        /// <summary>SSC 协调已登记请求的任务分配；匹配、实际驱动、动画、计时与结算继续使用 UAP 原实现。</summary>
        internal static bool Launch(Batch batch, out string reason)
        {
            reason = null;
            try
            {
                if (!batch.Current) { reason = "SSC_Uap_RequestChanged".Translate(); return false; }
                if (!SSCUapGroupApi.CreateSession(batch, out reason)) return false;
                if (!CheckPlan(batch, batch.Session, out reason)) return false;
                Pawn receiver = SSCUapGroupApi.Get<Pawn>(batch.Session, "Receiver");
                Pawn primary = SSCUapGroupApi.Get<Pawn>(batch.Session, "PrimaryInitiator");
                var assignments = new List<(Pawn Pawn, Pawn Partner, JobDef Def)> {
                    (receiver, primary, SSCUapGroupApi.ReceiverDef) };
                assignments.AddRange(SSCUapGroupApi.Pairs(batch.Session).Select(p => (p.Actor, p.Target, SSCUapGroupApi.InitiatorDef)));
                assignments.AddRange(SSCUapGroupApi.Get<List<Pawn>>(batch.Session, "PassiveParticipants").Select(p => (p, (Pawn)null, SSCUapGroupApi.PassiveDef)));
                if (assignments.Select(p => p.Pawn).Distinct().Count() != batch.People.Count ||
                    !SamePeople(batch, assignments.Select(p => p.Pawn))) throw new InvalidOperationException("UAP incomplete assignment");
                // 先创建并登记全组 Job；StartJob 会构造另一份驱动，不能把缓存驱动当作运行凭据。
                foreach (var item in assignments)
                {
                    Job job = item.Partner == null ? JobMaker.MakeJob(item.Def) : batch.Anchor is RimWorld.Building_Bed bed
                        ? JobMaker.MakeJob(item.Def, item.Partner, bed) : JobMaker.MakeJob(item.Def, item.Partner);
                    Register(batch, item.Pawn, job, item.Partner, false);
                }
                // StartJob 未抛异常仍可能预约失败；逐人核对当前任务以及真正安装的首个等待步骤。
                foreach (Work slot in batch.Jobs)
                {
                    slot.Attempted = true;
                    slot.Pawn.jobs.StartJob(slot.Job, JobCondition.InterruptForced, preToilReservationsCanFail: true);
                    if (!slot.Owned || slot.Pawn.jobs.curDriver?.GetType() != slot.Job.def.driverClass ||
                        slot.Barrier == null || toils(slot.Pawn.jobs.curDriver).FirstOrDefault() != slot.Barrier ||
                        slot.Pawn.jobs.curDriver.CurToilIndex != 0 ||
                        !SSCUapGroupApi.OwnsSession(batch)) throw new InvalidOperationException("UAP job installation rejected");
                    slot.Driver = slot.Pawn.jobs.curDriver;
                }
                if (!batch.Jobs.All(s => s.Owned) || !CheckPlan(batch, batch.Session, out reason)) return false;
                // 提交只允许后续 tick 离开屏障；各人的原初始化仍顺序执行，不承诺异常副作用可回滚。
                batch.Committed = true;
                return true;
            }
            catch (Exception error)
            {
                reason = "SSC_Uap_StartFailed".Translate();
                SSCLog.WarningImportant("[SSC UAP] Launch failed: " + error);
                return false;
            }
            // 原生调用、反射或中途准入失败统一撤销；已提交场景由正常生命周期接管。
            finally { if (!batch.Committed) Close(batch, true, reason); }
        }

        /// <summary>先封闭授权，再比较移除映射和精确结束旧任务；回调中新任务不属于旧批次。</summary>
        internal static void Close(Batch batch, bool abort, string reason = null)
        {
            if (batch.Closed) return;
            batch.Aborted = abort; batch.Closed = true; batch.Failure = reason;
            // 在任何第三方清理回调前固定仍属于本场的任务；映射比较移除后才调用外观和驱动清理。
            var owned = batch.Jobs.Where(s => s.Owned).ToList();
            var visible = owned.Where(s => SSCUapGroupApi.PawnSessionIs(batch, s.Pawn)).Select(s => s.Pawn).ToList();
            Safely(() => SSCUapGroupApi.Detach(batch));
            if (visible.Count > 0) Safely(() => SSCUapGroupApi.Restore(visible));
            if (abort)
                foreach (Work slot in owned)
                    if (slot.Owned) Safely(() => slot.Pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false));
            // 尚未交给 StartJob 的候选由本模块回池；已经尝试安装的任务交给原跟踪器，避免重复回池。
            foreach (Work slot in batch.Jobs)
                if (!slot.Attempted) { work.Remove(slot.Job); JobMaker.ReturnToPool(slot.Job); }
            ReleaseGathering(batch);
            if (abort && batch.Committed && !string.IsNullOrEmpty(reason))
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
        }

        /// <summary>隔离单个第三方清理异常并保留诊断，使其余本场任务仍有机会被清理。</summary>
        private static void Safely(Action action)
        { try { action(); } catch (Exception error) { SSCLog.WarningImportant("[SSC UAP] Cleanup failed: " + error); } }

        /// <summary>清理后的票据不能随 Job 回池；尚未正常结算便丢失任务时，下一安全更新撤销整场。</summary>
        internal static void Cleanup(JobDriver driver)
        {
            if (!Find(driver, out Work slot) || slot.Id != driver.job.loadID || slot.Driver != null && slot.Driver != driver) return;
            if (!slot.Gather && !slot.Batch.Closed && !slot.Finalized)
                slot.Batch.Failure = "SSC_Uap_RequestChanged".Translate();
            work.Remove(slot.Job);
        }

        /// <summary>仅结束仍由本请求持有的原集合 Job；同类型但编号或引用已变的新命令保持原样。</summary>
        internal static void ReleaseGathering(Batch batch)
        {
            foreach (Work slot in batch.Gathering)
                if (slot.Owned) Safely(() => slot.Pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false));
        }

        /// <summary>加载阶段只登记孤儿，不立即结束任务或执行 UAP 收尾；取消延迟到 Scribe 完成后的更新。</summary>
        internal static void Loaded(JobDriver driver)
        {
            var orphan = new Orphan { Driver = driver, Job = driver.job, Id = driver.job?.loadID ?? 0, Game = Current.Game };
            orphans.Remove(driver); orphans.Add(driver, orphan);
            Current.Game?.GetComponent<SSCUapGroupGameComponent>()?.Orphans.Add(orphan);
        }
        /// <summary>只把仍匹配原游戏和原 Job 编号的加载驱动视为孤儿，防止驱动复用后误拦新任务。</summary>
        internal static bool IsOrphan(JobDriver driver) => driver != null && orphans.TryGetValue(driver, out var orphan) && orphan.Current;
    }

    /// <summary>按当前游戏维护少量活动批次；读档清理等到 Scribe 结束，暂停时也执行。</summary>
    public sealed class SSCUapGroupGameComponent : GameComponent
    {
        internal readonly List<SSCUapGroupRuntime.Batch> Batches = new List<SSCUapGroupRuntime.Batch>();
        internal readonly List<SSCUapGroupRuntime.Orphan> Orphans = new List<SSCUapGroupRuntime.Orphan>();
        /// <summary>为新游戏清除 UAP 旧静态索引，不通过旧会话对上一游戏的 Pawn 执行 End 或 Abort。</summary>
        public SSCUapGroupGameComponent(Game game) { SSCUapGroupApi.ClearSessions(); }
        /// <summary>暂停时也处理加载孤儿和失效批次；只检查登记过的小集合，不扫描全地图 Pawn。</summary>
        public override void GameComponentUpdate()
        {
            if (Scribe.mode != LoadSaveMode.Inactive) return;
            // 读档期间不解释保存的步骤序号；引用恢复后仅取消原 driver/Job/loadID 仍匹配的任务。
            foreach (var orphan in Orphans.ToArray())
                if (orphan.Current && orphan.Driver.pawn?.jobs?.curDriver == orphan.Driver)
                    orphan.Driver.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            Orphans.Clear();
            // 使用快照容纳清理中的同步回调；关闭批次从当前游戏列表移除，旧对象仍由弱表隔离。
            foreach (var batch in Batches.ToArray())
            {
                if (!batch.Closed && (batch.Failure != null || !batch.Current || (batch.Committed &&
                    (!SSCUapGroupApi.OwnsSession(batch) || batch.Jobs.Any(s => !s.Owned)))))
                    SSCUapGroupRuntime.Close(batch, true, "SSC_Uap_RequestChanged".Translate());
                if (batch.Closed) Batches.Remove(batch);
            }
        }
    }
}
