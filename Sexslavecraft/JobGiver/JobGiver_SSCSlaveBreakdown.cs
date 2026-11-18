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
    /// <summary>维持已安装的本场任务。空闲时按 XML 顺序查询进食、休息、单次开场和游荡。</summary>
    public sealed class ThinkNode_SSCSlaveBreakdownBehavior : ThinkNode_Priority
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (state?.CanIssueBehavior != true || state.SelectingOpeningJob) return ThinkResult.NoJob;
            // 读档后编号匹配时返回追踪器里的同一个 Job。原版覆写比较会保留它，不会从头启动。
            // 主树的安全响应仍可中断这一分支。
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

    /// <summary>空闲后最多查询一次 XML 中的 RJW 自慰候选。开场只负责记录最终任务及其生命周期。</summary>
    public sealed class ThinkNode_SSCSlaveBreakdownOpening : ThinkNode
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            // 等空闲后才消费开场。读档中的原任务或准备中的候选都不会再次进入 RJW 查询。
            if (state?.CanIssueBehavior != true || state.Phase != MentalState_SSCSlaveBreakdown.OpeningPhase.Solo ||
                state.HasBehaviorJob || state.SelectingOpeningJob || pawn.CurJob != null)
                return ThinkResult.NoJob;
            state.SelectingOpeningJob = true;
            try
            {
                // 通过原版 TryIssueJobPackage 查询 RJW 节点。SSC 的通用后缀先分流，再核对最终任务的许可。
                // 直接返回处理后的 ThinkResult，让自我调教保留其任务、目标和来源。
                ThinkResult candidate = subNodes[0].TryIssueJobPackage(pawn, jobParams);
                if (!candidate.IsValid) return ThinkResult.NoJob;
                // 候选查询可能触发身份或任务回调。只有本场依然有效且角色仍空闲时才认领。
                if (state.CanIssueBehavior && !state.HasBehaviorJob && pawn.CurJob == null)
                {
                    state.SetBehaviorJob(candidate.Job);
                    return candidate;
                }
                JobMaker.ReturnToPool(candidate.Job);
                return ThinkResult.NoJob;
            }
            finally
            {
                // 无候选时继续同一棵树的游荡分支。已返回的候选由任务清理或缺失检测收束到游荡。
                if (!state.HasBehaviorJob) state.FinishOpening();
                state.SelectingOpeningJob = false;
            }
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

    // 公开地点例外只覆盖本场查询中的单人候选或已认领的本人任务。
    // 有搭档的请求继续走 RJW 常规选址，即使发起者刚好处于同一场发作。
    internal static class SSCBreakdownPublicActivity
    {
        internal static bool Permits(Pawn pawn, Pawn partner = null)
        {
            if (partner != null) return false;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (state?.CanIssueBehavior != true) return false;
            if (state.SelectingOpeningJob || JobGiver_SSCSlaveBreakdownMasturbate.CandidatePawn == pawn)
                return true;
            return state.OwnsBehaviorJob(pawn.CurJob) && pawn.CurJob.targetA.Pawn == pawn;
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
            // 单人任务优先用当前位置。原地不可用时仍调用 RJW 搜索，评分只免除本场单人的旁观限制。
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
