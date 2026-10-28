// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>1.7 可选 UAP 接入；只接管可追溯的请求，未知调用保留原第三方路径。</summary>
    internal static class SSCUapGroupPatches
    {
        internal const string Owner = "SexSlaveCraft.uap.group";
        private static MethodInfo fail;
        private static bool installed;
        /// <summary>保存进入窗口前的外层手动来源，供异常或正常退出时恢复，避免嵌套命令串用。</summary>
        internal sealed class ManualState { internal SSCUapGroupRuntime.ManualCall Previous; }

        /// <summary>每进程只尝试安装一次；缺 UAP 时跳过，接口或注册失败时撤回本 owner 的全部补丁。</summary>
        internal static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony(Owner);
            try
            {
                if (!SSCUapGroupApi.Resolve()) return;
                Type match = SSCUapGroupApi.Dialog.Assembly.GetType("UAP_Animations.UAPGroupAnimationMatch", true);
                Type plan = SSCUapGroupApi.Dialog.Assembly.GetType("UAP_Animations.UAPGroupLaunchPlan", true);
                // 共用完整签名核对；仅验证元数据，此阶段还不授予任何运行请求许可。
                MethodInfo M(Type t, string name, Type result, params Type[] args) => SSCUapGroupApi.Method(t, name, result, args);
                // 先验证全部必需签名，随后才开始安装；失败时卸载本 owner 的整套补丁。
                var manual = M(SSCUapGroupApi.Dialog, "TryInitiate", typeof(void));
                var begin = M(SSCUapGroupApi.Component, "TryBeginRequest", typeof(bool), typeof(Thing), typeof(List<Pawn>), match, typeof(string).MakeByRefType());
                var build = M(SSCUapGroupApi.Matcher, "TryBuildLaunchPlan", typeof(bool), typeof(List<Pawn>), match, plan.MakeByRefType(), typeof(string).MakeByRefType());
                var startRequest = M(SSCUapGroupApi.Request, "TryStart", typeof(bool), typeof(string).MakeByRefType());
                var tick = M(SSCUapGroupApi.Request, "Tick", typeof(void));
                fail = M(SSCUapGroupApi.Request, "Fail", typeof(void), typeof(string));
                var release = M(SSCUapGroupApi.Request, "ReleaseGatheringPawns", typeof(void));
                var launch = M(SSCUapGroupApi.Launcher, "TryStartRJWBackedGroupSession", typeof(bool), typeof(Thing), typeof(List<Pawn>), match, typeof(string).MakeByRefType());
                var utility = SSCUapGroupApi.Dialog.Assembly.GetType("UAP_Animations.GroupTogetherUtility", true);
                var send = M(utility, "SendPawnToCell", typeof(void), typeof(Pawn), typeof(IntVec3));
                var abort = M(SSCUapGroupApi.Manager, "AbortSession", typeof(void), typeof(int), typeof(string));
                var abortPawn = M(SSCUapGroupApi.Manager, "AbortSessionForPawn", typeof(void), typeof(Pawn), typeof(string));
                var end = M(SSCUapGroupApi.Manager, "EndSession", typeof(void), typeof(int));
                var finalized = M(SSCUapGroupApi.Manager, "NotifyInitiatorFinalized", typeof(void), typeof(Pawn));
                var rafStartType = typeof(Rimworld_Animations.AnimationUtility).Assembly.GetType(
                    "Rimworld_Animations.HarmonyPatch_JobDriver_SexBaseInitiator_Start", true);
                var rafStart = M(rafStartType, "Postfix", typeof(void), typeof(JobDriver_SexBaseInitiator).MakeByRefType());
                if (!rafStart.IsStatic) throw new MissingMethodException(rafStartType.FullName, "static Postfix");
                MethodInfo[] toils = new[] { SSCUapGroupApi.Receiver, SSCUapGroupApi.Initiator, SSCUapGroupApi.Passive }
                    .Select(t => M(t, "MakeNewToils", typeof(IEnumerable<Toil>))).ToArray();
                // 统一安装 Prefix/Postfix/Finalizer；外层 catch 按独立 owner 回滚半途注册。
                void P(MethodBase original, string prefix = null, string postfix = null, string finalizer = null)
                {
                    // 未指定的回调保留 null，不为原方法引入空补丁。
                    HarmonyMethod H(string name) => name == null ? null : new HarmonyMethod(typeof(SSCUapGroupPatches), name);
                    harmony.Patch(original, H(prefix), H(postfix), finalizer: H(finalizer));
                }
                // 第一段追踪窗口来源及集合请求；跨 tick 只通过真实请求对象找回批次。
                P(manual, nameof(ManualPrefix), finalizer: nameof(ManualFinalizer));
                P(begin, nameof(BeginPrefix), finalizer: nameof(BeginFinalizer));
                P(build, postfix: nameof(PlanPostfix));
                P(startRequest, nameof(RequestPrefix));
                P(tick, nameof(TickPrefix), finalizer: nameof(ScopeFinalizer));
                P(release, nameof(ReleasePrefix));
                P(fail, postfix: nameof(FailPostfix));
                P(send, postfix: nameof(SendPostfix));
                P(launch, nameof(LaunchPrefix));
                // 第二段隔离安装与真正行为，并把取消／完成通知交给精确会话清理。
                foreach (MethodInfo method in toils) P(method, postfix: nameof(ToilsPostfix));
                P(abort, nameof(AbortPrefix)); P(abortPawn, nameof(AbortPawnPrefix));
                P(end, nameof(EndPrefix)); P(finalized, nameof(FinalizedPrefix));
                // UAP 按所选动画单独启动；阻止 RAF 在第一个 Start 后扫描仍未初始化的其他群体发起者。
                P(rafStart, nameof(AutomaticAnimationPrefix));
                // 第三段覆盖原版加载、推进、tick 和清理入口；具体范围在回调内按记录或精确类型筛选。
                P(AccessTools.DeclaredMethod(typeof(JobDriver), "SetupToils"), nameof(SetupPrefix));
                P(AccessTools.DeclaredMethod(typeof(JobDriver), "ReadyForNextToil"), nameof(AdvancePrefix));
                P(AccessTools.DeclaredMethod(typeof(JobDriver), "DriverTick"), nameof(DriverTickPrefix), finalizer: nameof(CallbackFinalizer));
                P(AccessTools.DeclaredMethod(typeof(JobDriver), "Cleanup"), finalizer: nameof(CleanupFinalizer));
                P(AccessTools.DeclaredMethod(typeof(SexUtility), "ProcessSex"), nameof(ProcessPrefix));
                // 所有补丁安装成功后才开放设置入口；任一前置步骤失败都不能留下可用豁免。
                SSCUapGroupApi.Ready = true;
            }
            catch (Exception error)
            {
                harmony.UnpatchAll(Owner); SSCUapGroupApi.Ready = false;
                SSCLog.WarningImportant("[SSC UAP] Compatibility unavailable; no manual override installed: " + error);
            }
        }

        /// <summary>进入 UAP 窗口下令方法时捕获原选择；此时仅证明来源，不直接批准任何具体配对。</summary>
        private static void ManualPrefix(object __instance, out ManualState __state)
        {
            __state = new ManualState { Previous = SSCUapGroupRuntime.Manual };
            SSCUapGroupRuntime.Manual = new SSCUapGroupRuntime.ManualCall {
                Anchor = SSCUapGroupApi.Get<Thing>(__instance, "anchor"),
                Pawns = SSCUapGroupApi.Get<List<Pawn>>(__instance, "readyPawns").ToArray(),
                Animation = SSCUapGroupApi.Get<string>(__instance, "selectedAnimationDefName") };
        }
        /// <summary>无论原窗口方法成功、提前退出或抛异常，都恢复外层手动来源，不吞掉原异常。</summary>
        private static void ManualFinalizer(ManualState __state) { if (__state != null) SSCUapGroupRuntime.Manual = __state.Previous; }

        /// <summary>仅接管同发生点、完整人物集合及动画的真实窗口请求，其他调用隔离外层作用域后沿原路径。</summary>
        private static void BeginPrefix(Thing __0, List<Pawn> __1, object __2, out SSCUapGroupRuntime.Scope __state)
        {
            __state = null;
            if (__0 == null || __1 == null || __2 == null) return;
            // public TryBeginRequest 也可能被其他代码调用，不能仅凭方法名或 playerForced 判为窗口命令。
            var manual = SSCUapGroupRuntime.Manual;
            bool trusted = manual != null && manual.Anchor == __0 && new HashSet<Pawn>(manual.Pawns).SetEquals(__1) &&
                manual.Animation == SSCUapGroupApi.Get<string>(__2, "DefName");
            __state = new SSCUapGroupRuntime.Scope(trusted ? SSCUapGroupRuntime.Create(__0, __1, __2, true) : null);
        }
        /// <summary>请求创建失败或抛异常时撤销当前批次，并在 finally 恢复外层请求作用域。</summary>
        private static void BeginFinalizer(SSCUapGroupRuntime.Scope __state, bool __result, Exception __exception)
        {
            if (__state == null) return;
            try
            {
                var batch = SSCUapGroupRuntime.Ambient;
                if (batch != null && (!__result || __exception != null)) SSCUapGroupRuntime.Close(batch, true);
            }
            finally { __state.Dispose(); }
        }
        /// <summary>在初次匹配及最终会话匹配后复查原批次；拒绝通过原 bool/out 原因返回给 UAP。</summary>
        private static void PlanPostfix(object __1, object __2, ref string __3, ref bool __result)
        {
            var batch = SSCUapGroupRuntime.Ambient;
            // 直接读取原方法写回的 out plan；当前 Harmony 的 __args 是执行前快照，使用它还会覆写原失败原因。
            if (__result && batch != null && ReferenceEquals(__1, batch.Match) &&
                !SSCUapGroupRuntime.CheckPlan(batch, __2, out string reason))
            { __result = false; __3 = reason; }
        }
        /// <summary>只把与当前下令完全匹配的真实请求对象登记到批次，使集合后的 tick 仍能取回原上下文。</summary>
        private static void RequestPrefix(object __instance)
        {
            var batch = SSCUapGroupRuntime.Ambient;
            if (batch != null && batch.Request == null && SSCUapGroupApi.Get<Thing>(__instance, "anchor") == batch.Anchor &&
                SSCUapGroupApi.Get<object>(__instance, "selectedMatch") == batch.Match &&
                SSCUapGroupRuntime.SamePeople(batch, SSCUapGroupApi.Get<List<Pawn>>(__instance, "participants")))
                SSCUapGroupRuntime.BindRequest(__instance, batch);
        }
        /// <summary>按原请求恢复跨 tick 关联；已取消或载体失效时调用原 Fail 路径，阻止继续启动。</summary>
        private static bool TickPrefix(object __instance, out SSCUapGroupRuntime.Scope __state)
        {
            SSCUapGroupRuntime.FindRequest(__instance, out var batch);
            __state = new SSCUapGroupRuntime.Scope(batch);
            if (batch == null) return true;
            if (!batch.Closed && batch.Current && batch.Failure == null) return true;
            fail.Invoke(__instance, new object[] { batch.Failure ?? "SSC_Uap_RequestChanged".Translate().ToString() });
            return false;
        }
        /// <summary>在原请求 tick 正常返回或异常退出后恢复同步作用域，不向下一次 tick 泄漏上下文。</summary>
        private static void ScopeFinalizer(SSCUapGroupRuntime.Scope __state) => __state?.Dispose();
        /// <summary>接管已登记请求的集合释放，按原 Job 精确清理；未登记请求继续使用 UAP 原实现。</summary>
        private static bool ReleasePrefix(object __instance)
        {
            if (!SSCUapGroupRuntime.FindRequest(__instance, out var batch)) return true;
            SSCUapGroupRuntime.ReleaseGathering(batch); return false;
        }
        /// <summary>原 UAP 请求失败后关闭对应批次；重复关闭由运行时处理为无副作用。</summary>
        private static void FailPostfix(object __instance)
        { if (SSCUapGroupRuntime.FindRequest(__instance, out var batch)) SSCUapGroupRuntime.Close(batch, true); }
        /// <summary>在原集合派发返回后登记实际接收的等待 Job，核对人物、任务类型、目标格和编号。</summary>
        private static void SendPostfix(Pawn pawn, IntVec3 cell)
        {
            var batch = SSCUapGroupRuntime.Ambient;
            Job job = pawn?.CurJob;
            if (batch != null && batch.People.Any(p => p.Pawn == pawn) && job?.def?.driverClass == SSCUapGroupApi.Passive &&
                job.targetA.Cell == cell && !batch.Gathering.Any(s => s.Job == job && s.Id == job.loadID))
                SSCUapGroupRuntime.Register(batch, pawn, job, null, true);
        }
        /// <summary>只替换当前请求内同发生点、动画和完整人物集合的启动调用，其他调用保持原路径。</summary>
        private static bool LaunchPrefix(Thing __0, List<Pawn> __1, object __2, ref string __3, ref bool __result)
        {
            var batch = SSCUapGroupRuntime.Ambient;
            if (batch?.Request == null || !ReferenceEquals(__0, batch.Anchor) || !ReferenceEquals(__2, batch.Match) ||
                !SSCUapGroupRuntime.SamePeople(batch, __1)) return true;
            // 同步改写返回值与 out 原因，使 UAP 原 Tick 正确进入 Complete 或 Fail 分支。
            __result = SSCUapGroupRuntime.Launch(batch, out __3);
            return false;
        }
        /// <summary>包装原步骤序列；运行时只为已登记的真正行为任务加入等待提交步骤。</summary>
        private static void ToilsPostfix(JobDriver __instance, ref IEnumerable<Toil> __result)
        { if (__result != null) __result = SSCUapGroupRuntime.Gate(__instance, __result); }

        /// <summary>只跳过接管任务重复的 RAF 自动选动画；RJW Start、其他后置和 UAP 命名动画启动继续执行。</summary>
        private static bool AutomaticAnimationPrefix(JobDriver_SexBaseInitiator __0)
            => !SSCUapGroupRuntime.Find(__0, out var slot) || slot.Gather;

        /// <summary>在当前游戏登记的活动批次中查会话编号，关闭后的批次不再接收新会话操作。</summary>
        private static SSCUapGroupRuntime.Batch Session(int id) => Current.Game?.GetComponent<SSCUapGroupGameComponent>()?
            .Batches.FirstOrDefault(b => !b.Closed && b.Session != null && b.SessionId == id);
        /// <summary>拦截已接管会话的取消，改用比较对象和 Job 编号的精确清场；其他会话沿原实现。</summary>
        private static bool AbortPrefix(int sessionId, string failureReason)
        {
            var batch = Session(sessionId); if (batch == null) return true;
            SSCUapGroupRuntime.Close(batch, true, failureReason);
            return false;
        }
        /// <summary>正常完成时关闭对应会话并保留原任务退出流程，不把正常完成处理成取消结算。</summary>
        private static bool EndPrefix(int sessionId)
        {
            var batch = Session(sessionId); if (batch == null) return true;
            SSCUapGroupRuntime.Close(batch, false); return false;
        }
        /// <summary>同步旧回调触发取消时使用捕获的原工作，不能根据 Pawn 此刻的索引误取消新会话。</summary>
        private static bool AbortPawnPrefix(Pawn pawn, string failureReason)
        {
            var slot = SSCUapGroupRuntime.Executing;
            if (slot == null || slot.Pawn != pawn) return true;
            SSCUapGroupRuntime.Close(slot.Batch, true, failureReason); return false;
        }
        /// <summary>只有原工作真实开始且仍匹配当前回调才接收完成通知，防止迟到通知提前结束替换场景。</summary>
        private static bool FinalizedPrefix(Pawn pawn)
        {
            var slot = SSCUapGroupRuntime.Executing;
            // 没有捕获回调的通知不能替已登记场景提交；未接管的普通 UAP 场景保留原行为。
            if (slot == null) return !SSCUapGroupRuntime.Find(pawn?.jobs?.curDriver, out _);
            // 原通知按 Pawn 索引查会话，必须先确认索引仍属于原场，避免旧 Job 未清理时通知到新场。
            if (slot.Pawn != pawn || !slot.Owned || !slot.Started || slot.Batch.Closed || !slot.Batch.Current ||
                !SSCUapGroupApi.OwnsSession(slot.Batch))
            { slot.Batch.Failure = "SSC_Uap_RequestChanged".Translate(); return false; }
            slot.Finalized = true; return true;
        }
        /// <summary>读档时不重建群体步骤；等待步骤改变了序号布局，故登记孤儿后延迟安全取消。</summary>
        private static bool SetupPrefix(JobDriver __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || !SSCUapGroupApi.GroupDriver(__instance.GetType())) return true;
            SSCUapGroupRuntime.Loaded(__instance); return false;
        }
        /// <summary>在步骤推进入口挡住读档孤儿和已失效的接管任务，防止跳入旧序号对应的结算步骤。</summary>
        private static bool AdvancePrefix(JobDriver __instance) => SSCUapGroupRuntime.CanAdvance(__instance);
        /// <summary>每次驱动 tick 前核对归属，并把整次同步执行绑定原工作，覆盖原 UAP 内部取消回调。</summary>
        private static bool DriverTickPrefix(JobDriver __instance, out SSCUapGroupRuntime.CallbackScope __state)
        {
            __state = null;
            if (!SSCUapGroupRuntime.CanAdvance(__instance)) return false;
            if (SSCUapGroupRuntime.Find(__instance, out var slot)) __state = new SSCUapGroupRuntime.CallbackScope(slot);
            return true;
        }
        /// <summary>异常与正常 tick 共用释放路径，恢复外层回调归属而不修改原异常。</summary>
        private static void CallbackFinalizer(SSCUapGroupRuntime.CallbackScope __state) => __state?.Dispose();
        /// <summary>原驱动清理结束后撤销 Job 关联；若尚未完成则登记批次失败，避免在 Cleanup 中递归清场。</summary>
        private static void CleanupFinalizer(JobDriver __instance)
        { SSCUapGroupRuntime.Cleanup(__instance); }
        /// <summary>原 RJW 结算之前验证捕获的 SexProps 归属；被取消或替换的旧场不能借新任务执行结算。</summary>
        private static bool ProcessPrefix(SexProps props) => SSCUapGroupRuntime.Outcome(props);
    }
}
