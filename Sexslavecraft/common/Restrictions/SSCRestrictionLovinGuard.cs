using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>为原版双端 Lovin 保留唯一发起方向和实际开始凭据，不干预床位及睡眠许可。</summary>
    internal static class SSCRestrictionLovinGuard
    {
        private sealed class Scene
        {
            // 弱表按驱动隔离，Job引用及编号再防止外部复用驱动或池化任务导致串场。
            public Job Job;
            public int JobId;
            public bool HasRecord = true;
            public Pawn Actor, Target;
            public int PeerJobId;
            public bool Started, Rejected;
            // 只在原版同步创建另一端的调用栈中有效；不是可保存的许可或“已开始”标记。
            public bool CreatingReceiver;
        }

        private static readonly ConditionalWeakTable<JobDriver_Lovin, Scene> Scenes = new ConditionalWeakTable<JobDriver_Lovin, Scene>();
        // 原版接收任务从此哨兵开始倒计时，发起方初始最多5000 ticks。
        // 按数量级区分，不能用精确相等：已运行一个tick的接收者也必须恢复原发起方向。
        private const int ReceiverTicks = 9999999;

        /// <summary>获取当前任务状态，加载期间允许尚未接回的引用继续恢复，正常运行时严防复用旧状态。</summary>
        private static Scene State(JobDriver_Lovin driver)
        {
            Scene state = Scenes.GetOrCreateValue(driver);
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.Saving)
            {
                state.Job = driver.job;
                state.JobId = driver.job?.loadID ?? 0;
            }
            else if (state.Job != driver.job || state.JobId != (driver.job?.loadID ?? 0))
            {
                Scenes.Remove(driver);
                state = new Scene { Job = driver.job, JobId = driver.job?.loadID ?? 0 };
                Scenes.Add(driver, state);
            }
            return state;
        }

        /// <summary>原版1.6的私有Partner属性读取TargetA；读取同一目标，避免依赖公开化游戏程序集。</summary>
        private static Pawn Partner(JobDriver_Lovin driver) => driver.job?.targetA.Thing as Pawn;

        /// <summary>只返回互相指向的当前原版另一端；同床但目标不同的工作不能借用当前场景。</summary>
        private static JobDriver_Lovin Peer(JobDriver_Lovin driver)
        {
            var peer = Partner(driver)?.jobs?.curDriver as JobDriver_Lovin;
            return peer != null && Partner(peer) == driver.pawn ? peer : null;
        }

        /// <summary>建立有向请求；仅同步创建接收端时继承方向，后来新任务不得借用已开始场景的许可。</summary>
        private static SSCRestrictionRequest Request(JobDriver_Lovin driver)
        {
            Scene state = State(driver);
            if (state.Actor == null)
            {
                JobDriver_Lovin peer = Peer(driver);
                Scene other = peer == null ? null : State(peer);
                if (other?.CreatingReceiver == true && other.Actor == peer.pawn && other.Target == driver.pawn)
                {
                    state.Actor = other.Actor;
                    state.Target = other.Target;
                    state.PeerJobId = peer.job.loadID;
                    other.PeerJobId = driver.job.loadID;
                }
                else { state.Actor = driver.pawn; state.Target = Partner(driver); }
            }
            bool matches = (state.Actor == driver.pawn && state.Target == Partner(driver)) ||
                (state.Target == driver.pawn && state.Actor == Partner(driver));
            return new SSCRestrictionRequest(state.Actor, state.Target, SSCInteractionKind.Consensual, matches);
        }

        /// <summary>预约只返回结果，步骤前拒绝则精确终止本次关联任务；开始后的原场景继续正常收尾。</summary>
        public static bool Check(JobDriver_Lovin driver, bool endOnFailure)
        {
            if (endOnFailure && driver.pawn?.jobs?.curDriver != driver) return false;
            SSCRestrictionRequest request = Request(driver);
            Scene state = State(driver);
            if (state.Started && request.DirectionKnown) return true;
            SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(request);
            state.Rejected = !decision.Allowed;
            if (!decision.Allowed && endOnFailure) Reject(driver, SSCRestrictionExternalJobs.Rejection(decision));
            return decision.Allowed;
        }

        /// <summary>手动拒绝提示使用已经建立的真实方向，不将接收任务当作反向发起。</summary>
        public static string RejectionReason(JobDriver_Lovin driver)
            => SSCRestrictionExternalJobs.Rejection(SSCRestrictionPolicy.Evaluate(Request(driver)));

        /// <summary>终止未开始的当前请求和它确实创建的另一端；不结束后来替换的新工作，不伪造成功结算。</summary>
        private static void Reject(JobDriver_Lovin driver, string reason)
        {
            Scene state = State(driver);
            state.Rejected = true;
            JobDriver_Lovin peer = Peer(driver);
            if (peer != null && peer.job.loadID == state.PeerJobId)
            {
                Scene other = State(peer);
                if (!other.Started && other.PeerJobId == driver.job.loadID)
                    peer.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
            }
            if (driver.pawn?.jobs?.curDriver != driver) return;
            if (driver.job.playerForced && reason != null)
                Messages.Message(reason, driver.pawn, MessageTypeDefOf.RejectInput, false);
            driver.pawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }

        /// <summary>包装原版配对初始化回调，在原生同步创建接收任务期间传递方向，双方成功配对后才记录开始。</summary>
        /// <remarks>
        /// 已核对 RimWorld 1.6：只有配对初始化的 initAction 直接声明在 JobDriver_Lovin 中；走床和躺下使用其他类的回调。
        /// 不插入或删除 Toil，因此旧存档步骤索引不变。RJW 替换模式会先换成普通 RJW 任务，迟到回调在当前驱动检查处退出。
        /// </remarks>
        public static IEnumerable<Toil> Wrap(JobDriver_Lovin driver, IEnumerable<Toil> toils)
        {
            foreach (Toil toil in toils)
            {
                Action original = toil.initAction;
                if (original?.Method.DeclaringType == typeof(JobDriver_Lovin))
                    toil.initAction = () => InitializePair(driver, original);
                yield return toil;
            }
        }

        /// <summary>执行唯一原版配对回调；嵌套接收端只沿用方向，由最外层发起者确认双方仍在同一任务后发布开始凭据。</summary>
        private static void InitializePair(JobDriver_Lovin driver, Action original)
        {
            if (!Check(driver, true)) return;
            Scene state = State(driver);
            state.CreatingReceiver = state.Actor == driver.pawn;
            try { original(); }
            finally { state.CreatingReceiver = false; }
            if (driver.pawn?.jobs?.curDriver != driver || state.Actor != driver.pawn) return;
            JobDriver_Lovin peer = Peer(driver);
            Scene other = peer == null ? null : State(peer);
            if (peer == null || peer.job.loadID != state.PeerJobId || other.PeerJobId != driver.job.loadID ||
                other.Actor != state.Actor || other.Target != state.Target || other.Rejected)
            {
                Reject(driver, null);
                return;
            }
            state.Started = other.Started = true;
        }

        /// <summary>保存方向、配对任务编号及开始凭据；旧存档仅在真正的躺卧场景中用原版计时恢复，不把走床当开始。</summary>
        public static void ExposeData(JobDriver_Lovin driver, int ticksLeft)
        {
            Scene state = State(driver);
            Scribe_Values.Look(ref state.HasRecord, "sscLovinRecord", false);
            Scribe_Values.Look(ref state.Started, "sscLovinStarted", false);
            Scribe_Values.Look(ref state.PeerJobId, "sscLovinPeerJob", 0);
            Scribe_References.Look(ref state.Actor, "sscLovinActor");
            Scribe_References.Look(ref state.Target, "sscLovinTarget");
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            // 到达躺卧步骤证明配对初始化已运行；倒计时恰好为0的待收尾场景同样属于已开始。
            if (!state.HasRecord && driver.CurToilIndex >= 3)
            {
                JobDriver_Lovin peer = Peer(driver);
                if (peer != null)
                {
                    state.Actor = ticksLeft > ReceiverTicks / 2 ? peer.pawn : driver.pawn;
                    state.Target = ticksLeft > ReceiverTicks / 2 ? driver.pawn : peer.pawn;
                    state.PeerJobId = peer.job.loadID;
                    state.Started = true;
                }
            }
            state.HasRecord = true;
        }

        /// <summary>未真正开始的 Lovin 不允许以成功条件进入 Cleanup，防止 RJW 的成功前缀为被拒绝任务结算。</summary>
        public static void BeforeCleanup(JobDriver_Lovin driver, ref JobCondition condition)
        {
            if (condition == JobCondition.Succeeded && !State(driver).Started) condition = JobCondition.Incompletable;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Lovin), "TryMakePreToilReservations")]
    internal static class SSCRestrictionLovinReservationHook
    {
        /// <summary>在原版预约前检查有向普通双人许可，身体、床位及可达性仍由原任务校验。</summary>
        public static bool Prefix(JobDriver_Lovin __instance, ref bool __result)
        {
            if (SSCRestrictionLovinGuard.Check(__instance, false)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Lovin), "MakeNewToils")]
    internal static class SSCRestrictionLovinToilsHook
    {
        /// <summary>保留原版步骤顺序，只包装其原生配对回调以区分准备与实际开始。</summary>
        public static void Postfix(JobDriver_Lovin __instance, ref IEnumerable<Toil> __result)
        {
            if (__result != null) __result = SSCRestrictionLovinGuard.Wrap(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(JobDriver_Lovin), "ExposeData")]
    internal static class SSCRestrictionLovinSaveHook
    {
        /// <summary>在原版 ticksLeft 已恢复后保存或恢复 SSC 凭据；不改动游戏自己的倒计时。</summary>
        public static void Postfix(JobDriver_Lovin __instance, int ___ticksLeft)
            => SSCRestrictionLovinGuard.ExposeData(__instance, ___ticksLeft);
    }

    [HarmonyPatch(typeof(JobDriver), "Cleanup")]
    internal static class SSCRestrictionJobCleanupHook
    {
        /// <summary>先于 RJW 成功结算前缀修正未开始的 Lovin 结束条件，仍执行原生资源清理。</summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix(JobDriver __instance, ref JobCondition condition)
        {
            if (__instance is JobDriver_Lovin lovin) SSCRestrictionLovinGuard.BeforeCleanup(lovin, ref condition);
        }

        /// <summary>任何原因结束准备中的 RJW 事件任务时撤销自身等待，覆盖寻路失败和外部取消。</summary>
        public static void Postfix(JobDriver __instance)
        {
            if (__instance is rjw.JobDriver_Sex sex) SSCRestrictionJobGuard.CleanupAbortedPreparation(sex);
        }
    }
}
