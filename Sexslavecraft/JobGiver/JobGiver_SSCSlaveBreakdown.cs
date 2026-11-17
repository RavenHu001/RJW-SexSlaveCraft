// AI 辅助生成：只为 SSC 发作分支协调原版需求、RJW 候选和居住区游荡。
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>保留已派发的互动。空闲时按 XML 顺序查询需求、开场和游荡。</summary>
    public sealed class ThinkNode_SSCSlaveBreakdownBehavior : ThinkNode_Priority
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (state?.CanIssueBehavior != true) return ThinkResult.NoJob;
            // 原版覆写比较识别同一个 Job，因此不会重新启动此任务。更高层的安全响应仍可中断。
            // 获批接收器由训练驱动安装。行为树返回当前任务，让接收器继续运行。
            if (state.OwnsBehaviorJob(pawn.CurJob) || state.HoldsTrainingReceiver(pawn.CurJob))
                return new ThinkResult(pawn.CurJob, pawn.CurJob.jobGiver ?? this, JobTag.InMentalState, false);
            state.RetireMissingBehaviorJob();
            return state.CanIssueBehavior ? base.TryIssueJobPackage(pawn, jobParams) : ThinkResult.NoJob;
        }
    }

    public sealed class JobGiver_SSCSlaveBreakdownRest : JobGiver_GetRest
    {
        // 直接使用原版睡眠任务，避免 PrioritySorter 在 Work 时段把休息优先级设为零。
        protected override Job TryGiveJob(Pawn pawn)
            => SSCSlaveBreakdown.Now >= pawn.mindState.canSleepTick && RestUtility.CanFallAsleep(pawn)
                ? base.TryGiveJob(pawn) : null;
    }

    public sealed class ThinkNode_SSCSlaveBreakdownOpening : ThinkNode
    {
        // 只在调用本场的双人候选子节点时标记发起者，嵌套调用返回后恢复上一层。
        [ThreadStatic] internal static Pawn PairSearchActor;
        private const float SearchRadius = 15f;

        /// <summary>发作一侧额外要求近距离和可接管性。RJW 原有寻路、配对及身体条件仍由其候选器处理。</summary>
        internal static bool AvailablePartner(Pawn actor, Pawn target)
        {
            if (actor?.Map == null || target == null || target == actor || !target.Spawned || target.Map != actor.Map ||
                !target.RaceProps.Humanlike || target.Dead || target.Downed || target.Drafted || target.InMentalState ||
                target.IsFighting() || target.jobs == null || target.CurJob?.playerForced == true ||
                !target.Awake() || !actor.Position.InHorDistOf(target.Position, SearchRadius)) return false;
            Area area = actor.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            // 发作中原版会放宽 IsForbidden。这里显式使用玩家对该 Pawn 生效的允许区。
            return (area == null || area.TrueCount == 0 || area[target.Position]) && !target.Position.Fogged(actor.Map);
        }

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            // 原生 Quickie 只在没有当前任务时产生候选，覆写查询不应消耗开场。
            if (state == null || state.HasBehaviorJob || state.SelectingOpeningJob || pawn.CurJob != null)
                return ThinkResult.NoJob;
            state.SelectingOpeningJob = true;
            try { return IssueOpening(pawn, jobParams, state); }
            finally { state.SelectingOpeningJob = false; }
        }

        private ThinkResult IssueOpening(Pawn pawn, JobIssueParams jobParams, MentalState_SSCSlaveBreakdown state)
        {
            // XML 首项走 RJW 双人候选，次项是一次性单人后备。无候选可在本次选任务中直接推进。
            while (state.CanIssueBehavior && state.Phase != MentalState_SSCSlaveBreakdown.OpeningPhase.Wander)
            {
                bool pair = state.Phase == MentalState_SSCSlaveBreakdown.OpeningPhase.Pair;
                Pawn previous = PairSearchActor;
                ThinkResult candidate;
                try
                {
                    if (pair) PairSearchActor = pawn;
                    candidate = subNodes[pair ? 0 : 1].TryIssueJobPackage(pawn, jobParams);
                }
                finally { PairSearchActor = previous; }
                if (candidate.IsValid)
                {
                    // 候选生成已经执行 RJW 和 SSC 回调。离开前复核发作、空闲状态及接收方队列。
                    if (state.CanIssueBehavior && !state.HasBehaviorJob && pawn.CurJob == null &&
                        (!pair || AvailablePartner(pawn, candidate.Job.targetA.Pawn) &&
                        candidate.Job.targetA.Pawn.jobs.jobQueue.Count == 0))
                    {
                        state.SetBehaviorJob(candidate.Job);
                        return candidate;
                    }
                    JobMaker.ReturnToPool(candidate.Job);
                }
                state.SkipOpeningAttempt();
            }
            return ThinkResult.NoJob;
        }
    }

    [HarmonyPatch(typeof(CasualSex_Helper), nameof(CasualSex_Helper.PotentialPartnersFor))]
    internal static class Patch_SSCSlaveBreakdownPartners
    {
        internal static void Postfix(Pawn pawn, bool bedsex, ref IEnumerable<Pawn> __result)
        {
            // FindPartner 当场枚举。提前过滤本场范围及已有排队任务，保留原伴侣排序和双方意愿判断。
            if (!bedsex && pawn != null && ThinkNode_SSCSlaveBreakdownOpening.PairSearchActor == pawn)
                __result = __result.Where(target => ThinkNode_SSCSlaveBreakdownOpening.AvailablePartner(pawn, target) &&
                    target.jobs.jobQueue.Count == 0);
        }
    }

    public sealed class ThinkNode_SSCSlaveBreakdownWandering : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
            => MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.Phase == MentalState_SSCSlaveBreakdown.OpeningPhase.Wander;
    }

    /// <summary>沿用 RJW 原候选及当前自动分流，另为游荡期单人任务记录本场所有权。</summary>
    public sealed class JobGiver_SSCSlaveBreakdownMasturbate : JobGiver_Masturbate
    {
        [ThreadStatic] internal static Pawn CandidatePawn;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (state?.CanIssueBehavior != true || state.HasBehaviorJob) return ThinkResult.NoJob;
            Job previous = pawn.CurJob;
            Pawn previousCandidate = CandidatePawn;
            ThinkResult result;
            CandidatePawn = pawn;
            try { result = base.TryIssueJobPackage(pawn, jobParams); }
            finally { CandidatePawn = previousCandidate; }
            // 原始候选从基类返回时已通过 SSC 自动分流和许可 Postfix。
            if (!result.IsValid) return result;
            if (state.CanIssueBehavior && !state.HasBehaviorJob && pawn.CurJob == previous)
            {
                state.SetBehaviorJob(result.Job);
                return result;
            }
            JobMaker.ReturnToPool(result.Job);
            return ThinkResult.NoJob;
        }
    }

    // RJW 的公开地点开关与旁人可见性评分分别发生在候选期和执行期。
    // 仅放行本场候选或已认领任务。第三方普通任务即使涉及同一 Pawn 也不会继承例外。
    internal static class SSCBreakdownPublicActivity
    {
        internal static bool Permits(Pawn pawn, Pawn partner = null)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (state?.CanIssueBehavior != true) return false;
            if (partner == null && (state.SelectingOpeningJob || JobGiver_SSCSlaveBreakdownMasturbate.CandidatePawn == pawn))
                return true;
            return state.OwnsBehaviorJob(pawn.CurJob) && (partner == null
                ? pawn.CurJob.targetA.Pawn == pawn
                : pawn.CurJob.targetA.Pawn == partner);
        }
    }

    [HarmonyPatch(typeof(JobGiver_Masturbate), "WillingToMasturbateEverywhere")]
    internal static class Patch_SSCBreakdownMasturbateEverywhere
    {
        internal static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!SSCBreakdownPublicActivity.Permits(pawn)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CasualSex_Helper), nameof(CasualSex_Helper.MightBeSeen))]
    internal static class Patch_SSCBreakdownVisibility
    {
        internal static bool Prefix(Pawn pawn, Pawn partner, ref bool __result)
        {
            if (!SSCBreakdownPublicActivity.Permits(pawn, partner)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CasualSex_Helper), nameof(CasualSex_Helper.FindSexLocation))]
    internal static class Patch_SSCBreakdownActivityLocation
    {
        internal static bool Prefix(Pawn pawn, Pawn partner, ref IntVec3 __result)
        {
            // 单人任务优先用当前位置；仍由 RJW 判定格子能否安全使用。
            // 双人快速任务的原版进阶由 MightBeSeen 接入，保留其温度与走位流程。
            if (partner != null || !SSCBreakdownPublicActivity.Permits(pawn) ||
                !CasualSex_Helper.IsAcceptableSexSpot(pawn.Position, pawn, null)) return true;
            __result = pawn.Position;
            return false;
        }
    }

    public sealed class JobGiver_SSCSlaveBreakdownWander : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 cell = FindDestination(pawn);
            if (!cell.IsValid || cell == pawn.Position)
                return JobMaker.MakeJob(JobDefOf.Wait_Wander, 150);
            Job job = JobMaker.MakeJob(JobDefOf.GotoWander, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            // 路段到期后重新检查聚集点、房间及 Home，关闭或拆除的点不会被长期缓存。
            job.expiryInterval = 450;
            job.checkOverrideOnExpire = true;
            return job;
        }

        internal static IntVec3 FindDestination(Pawn pawn)
        {
            Map map = pawn.Map;
            Area home = map.areaManager.Home;
            Area allowed = pawn.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            // Home 和个人允许区均检查最终落点。所有退路都经过同一格子检查。
            bool Usable(IntVec3 cell) => cell.InBounds(map) && home[cell] &&
                (allowed == null || allowed.TrueCount == 0 || allowed[cell]) && !cell.Fogged(map) &&
                cell.Standable(map) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.None);

            var population = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(other => other != pawn && !other.Downed && other.Awake()).Select(other => other.Position).ToArray();
            IntVec3 Choose(IEnumerable<IntVec3> cells)
            {
                // 在同类地点中按附近清醒角色数排序，人数相同时偏向较近位置。
                return cells.Select(cell => (Cell: cell, Crowd: population.Count(p => p.InHorDistOf(cell, 8f))))
                    .OrderByDescending(item => item.Crowd)
                    .ThenBy(item => item.Cell.DistanceToSquared(pawn.Position))
                    .Select(item => item.Cell).FirstOrDefault(IntVec3.Invalid);
            }

            // 聚集设施通常占据自身格。附近可站立格用于选锚点，再在该房间就近走动。
            var gatherCells = ActiveGatherSpots(pawn)
                .SelectMany(spot => GenAdj.CellsAdjacent8Way(spot.parent)).Where(Usable).Distinct().ToArray();
            IntVec3 anchor = Choose(gatherCells);
            if (anchor.IsValid)
            {
                Room room = anchor.GetRoom(map);
                if (CellFinder.TryFindRandomCellNear(anchor, map, 4, cell =>
                    cell != pawn.Position && Usable(cell) && cell.GetRoom(map) == room, out IntVec3 nearby))
                    return nearby;
                if (anchor != pawn.Position) return anchor;
                // 当前格若是本聚集点唯一可用的落点，继续找另一聚集点或房间。
                anchor = Choose(gatherCells.Where(cell => cell != pawn.Position));
                if (anchor.IsValid) return anchor;
            }

            anchor = Choose(map.regionGrid.AllRooms.Where(room => room.Role?.defName is "DiningRoom" or "RecRoom")
                .SelectMany(room => room.Cells.Where(cell => cell != pawn.Position && Usable(cell)).Take(8)));
            if (anchor.IsValid) return anchor;
            if (CellFinder.TryFindRandomCellNear(pawn.Position, map, 8, cell => cell != pawn.Position && Usable(cell),
                out IntVec3 fallback)) return fallback;
            // 即使小人当前在居住区外，也可回到合法的 Home 落点。
            return home.ActiveCells.FirstOrDefault(cell => cell != pawn.Position && Usable(cell), IntVec3.Invalid);
        }

        internal static IEnumerable<CompGatherSpot> ActiveGatherSpots(Pawn pawn)
            => pawn.Map.gatherSpotLister.activeSpots.Where(spot => spot.Active && spot.parent.Spawned &&
                spot.parent.Map == pawn.Map && !spot.parent.IsForbidden(pawn.Faction));
    }
}
