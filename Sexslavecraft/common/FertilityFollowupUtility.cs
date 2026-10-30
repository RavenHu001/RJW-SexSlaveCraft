// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using rjw;
using rjw.Modules.Attraction;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>成功建孕后的单次固定配对尝试；绝不参与妊娠和冷却的提交。</summary>
    internal static class FertilityFollowupUtility
    {
        internal sealed class Pair
        {
            internal Pawn Actor, Target;
            internal object ActorToken, TargetToken;
            internal CompSexSlaveTraining ActorComp, TargetComp;
            internal Job OriginalTargetJob;
            internal int OriginalTargetJobId, JobId;
            internal bool Current => SSCPersonalityRuntimeUtility.IsCurrent(Actor, ActorToken) &&
                SSCPersonalityRuntimeUtility.IsCurrent(Target, TargetToken) && !SSCPersonalityRuntimeUtility.IsMutating(Actor) &&
                !SSCPersonalityRuntimeUtility.IsMutating(Target) && Actor?.TryGetComp<CompSexSlaveTraining>() == ActorComp &&
                Target?.TryGetComp<CompSexSlaveTraining>() == TargetComp;
        }
        private static readonly ConditionalWeakTable<Job, Pair> pairs = new();
        internal static Pair Get(Job job) => pairs.TryGetValue(job, out var pair) && pair.JobId == job.loadID ? pair : null;
        /// <summary>互动保留原成人、自愿、意愿与身体条件；采样建孕不调用这里。</summary>
        internal static bool CanPair(Pawn a, Pawn b)
        {
            bool Ready(Pawn p) => p != null && p.Spawned && SSCCultureGenderUtility.IsAdult(p) && !p.Downed && !p.Drafted &&
                !p.InMentalState && p.Awake() && !p.IsFighting() && p.health.capacities.CanBeAwake && p.jobs != null;
            if (!Ready(a) || !Ready(b) || a == b || a.Map != b.Map || a.HostileTo(b) || b.HostileTo(a)) return false;
            if (!RJWHookupSettings.HookupsEnabled || !RJWHookupSettings.QuickHookupsEnabled ||
                !CasualSex_Helper.CanHaveSex(a) || !CasualSex_Helper.CanHaveSex(b) ||
                !SexUtility.ReadyForLovin(a) || !SexUtility.ReadyForLovin(b) ||
                !SexUtility.ReadyForHookup(a) || !SexUtility.ReadyForHookup(b) ||
                !CasualSex_Helper.CanTargetHookup(a, b) || !CasualSex_Helper.CanTargetHookup(b, a)) return false;
            bool lovers = LovePartnerRelationUtility.LovePartnerRelationExists(a, b);
            bool Desire(Pawn p) => lovers || RJWSettings.WildMode || RJWSettings.HippieMode || xxx.is_nympho(p) || xxx.is_frustrated(p) || xxx.is_horny(p);
            bool Allows(Pawn p) => AttractionUtility.CanFoolAround(p, true) ||
                (RJWHookupSettings.NymphosCanCheat && xxx.is_nympho(p) && xxx.is_frustrated(p)) || p.HasBeerGoggles();
            if (!Desire(a) || !Desire(b) || (!lovers && !RJWSettings.WildMode && !RJWSettings.HippieMode && (!Allows(a) || !Allows(b)))) return false;
            var settings = new AppraisalSettings(AttractionPurpose.ForFucking);
            var appraisal = new AppraisalResult(a, settings, b, settings);
            return SexAppraiser.FindBestResults(new[] { appraisal }).Any() && CasualSex_Helper.CasualHookupAllowedViaSettings(appraisal);
        }
        /// <summary>只把一个通过预检的Job放入施法者队首；不提前打断对象，不消费宠物概率或绑定。</summary>
        internal static bool TryQueue(Pawn a, Pawn b)
        {
            if (a == null || b == null) return false;
            // 固定原配对后再进入意愿、地图与许可回调，不能在回调结束后替新人格重新Capture。
            var pair = new Pair { Actor = a, Target = b, ActorToken = SSCPersonalityRuntimeUtility.Capture(a),
                TargetToken = SSCPersonalityRuntimeUtility.Capture(b), ActorComp = a.TryGetComp<CompSexSlaveTraining>(),
                TargetComp = b.TryGetComp<CompSexSlaveTraining>(), OriginalTargetJob = b.CurJob,
                OriginalTargetJobId = b.CurJob?.loadID ?? -1 };
            if (!CanPair(a, b) || !a.Position.InHorDistOf(b.Position, 15f) ||
                !a.CanReserveAndReach(b, PathEndMode.OnCell, Danger.Deadly) || !pair.Current ||
                b.CurJob?.playerForced == true || b.CurJob?.def.forceCompleteBeforeNextJob == true ||
                b.jobs.curDriver is JobDriver_Sex || b.jobs.jobQueue.Any() || !b.jobs.IsCurrentJobPlayerInterruptible()) return false;
            var def = DefDatabase<JobDef>.GetNamedSilentFail("SSC_Job_FertilityFollowup");
            if (def == null) return false;
            Job job = JobMaker.MakeJob(def, b);
            pair.JobId = job.loadID;
            bool queued = false;
            try
            {
                if (!SSCRestrictionJobGuard.PrepareEvent(a, job, SSCRestrictionEvent.None) || !pair.Current ||
                    b.CurJob != pair.OriginalTargetJob || (b.CurJob?.loadID ?? -1) != pair.OriginalTargetJobId ||
                    b.jobs.jobQueue.Any()) return false;
                pairs.Remove(job);
                pairs.Add(job, pair);
                a.jobs.jobQueue.EnqueueFirst(job);
                queued = true;
                return true;
            }
            finally
            {
                if (!queued && job.loadID == pair.JobId)
                {
                    pairs.Remove(job);
                    SSCRestrictionJobGuard.CancelPendingEvent(job);
                    JobMaker.ReturnToPool(job);
                }
            }
        }
    }
    /// <summary>沿RJW Quickie执行，开始前守住原配对、原任务与自愿许可；读档不重放未开始的联动。</summary>
    public class JobDriver_FertilityFollowup : JobDriver_SexQuick
    {
        private sealed class NativeInitializationAborted : Exception { }
        private bool inNativeInitialization;
        private Pawn receiverPawn;
        private Job receiverJob;
        private int receiverJobId = -1;
        internal bool Started => SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.Consensual);
        private bool IsCurrentJob => !ended && pawn?.jobs?.curDriver == this && pawn.CurJob == job;

        /// <summary>原地准备的配对须仍有效；开始之后由原RJW场景及持久化开始凭据负责正常收尾。</summary>
        internal bool CanPrepare(bool requireCurrent = true)
        {
            var pair = FertilityFollowupUtility.Get(job);
            if (pair == null || !pair.Current || pair.Actor != pawn || Partner != pair.Target ||
                !FertilityFollowupUtility.CanPair(pawn, Partner) || !pair.Current || (requireCurrent && !IsCurrentJob)) return false;
            bool Own(Job j) => j != null && SSCRestrictionJobGuard.OwnsPreparedJob(this, Partner, j);
            // 原版Quickie的StopAll仅可触及空队列或本场景自己的准备，不能清掉后来新增的命令。
            if (Partner.jobs.jobQueue.Any(q => !Own(q.job))) return false;
            return (Partner.CurJob == pair.OriginalTargetJob && (Partner.CurJob?.loadID ?? -1) == pair.OriginalTargetJobId) ||
                Own(Partner.CurJob) || (receiverPawn == Partner && OwnReceiver());
        }
        private bool OwnReceiver() => receiverJob != null && receiverPawn?.CurJob == receiverJob &&
            receiverJob.loadID == receiverJobId && receiverJob.def == xxx.getting_quickie &&
            receiverPawn.jobs.curDriver is JobDriver_SexBaseReciever r && r.Partner == pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => (Started || CanPrepare(false)) && base.TryMakePreToilReservations(errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Started && !CanPrepare());
            AddFinishAction(_ => ReleaseOwnUnstartedReceiver());
            foreach (Toil toil in base.MakeNewToils())
            {
                Action original = toil.initAction;
                toil.initAction = () =>
                {
                    if (!IsCurrentJob) return;
                    if (!Started && !CanPrepare()) { EndJobWith(JobCondition.Incompletable); return; }
                    InvokeNativeInitialization(original);
                };
                yield return toil;
            }
        }

        /// <summary>包住真实Quickie init；Start被拦截后只终止此回调，不执行后面的空Sexprops访问。</summary>
        internal void InvokeNativeInitialization(Action original)
        {
            Pawn target = Partner;
            Job previous = target?.CurJob;
            inNativeInitialization = true;
            try { original?.Invoke(); }
            catch (NativeInitializationAborted) { }
            finally
            {
                inNativeInitialization = false;
                // 只认领本回调实际新建且指回本人的接收任务，不能按相同类型接管别人的工作。
                Job current = target?.CurJob;
                if (current != null && current != previous && current.def == xxx.getting_quickie && !current.playerForced &&
                    target.jobs.curDriver is JobDriver_SexBaseReciever receiver && receiver.Partner == pawn)
                { receiverPawn = target; receiverJob = current; receiverJobId = current.loadID; }
                if (!IsCurrentJob) ReleaseOwnUnstartedReceiver();
            }
        }

        /// <summary>实际Start前再核对配对、意愿及本请求安装的接收者；迟到旧回调不结束新任务。</summary>
        internal bool CheckBeforeStart()
        {
            if (IsCurrentJob && (Started || (CanPrepare() && receiverPawn == Partner && OwnReceiver()))) return true;
            if (IsCurrentJob) EndJobWith(JobCondition.Incompletable);
            return false;
        }
        /// <summary>其他前缀同样可拒绝Start；在原init栈内抛本地中止信号，外部调用仅结束失败任务。</summary>
        internal void CheckAfterStart(bool ranOriginal)
        {
            if (ranOriginal || Started) return;
            if (IsCurrentJob) EndJobWith(JobCondition.Incompletable);
            if (inNativeInitialization) throw new NativeInitializationAborted();
        }
        internal bool CanEndScene() => Started;

        /// <summary>取消只释放本回调曾安装的未开始接收；已经开始或后来替换的工作保持原样。</summary>
        internal void ReleaseOwnUnstartedReceiver()
        {
            if (Started || !OwnReceiver()) return;
            var receiver = receiverPawn.jobs.curDriver as JobDriver_SexBaseReciever;
            receiverJobId = -1;
            receiver.parteners?.Remove(pawn);
            if (receiver.parteners == null || receiver.parteners.Count == 0)
                receiverPawn.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
        }
    }

    [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), nameof(JobDriver_SexBaseInitiator.Start))]
    internal static class FertilityFollowupStartHook
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(JobDriver_SexBaseInitiator __instance)
            => __instance is not JobDriver_FertilityFollowup f || f.CheckBeforeStart();
        public static void Postfix(JobDriver_SexBaseInitiator __instance, bool __runOriginal)
        { if (__instance is JobDriver_FertilityFollowup f) f.CheckAfterStart(__runOriginal); }
    }
    [HarmonyPatch(typeof(JobDriver_SexBaseInitiator), nameof(JobDriver_SexBaseInitiator.End))]
    internal static class FertilityFollowupEndHook
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(JobDriver_SexBaseInitiator __instance)
            => __instance is not JobDriver_FertilityFollowup f || f.CanEndScene();
    }
}
