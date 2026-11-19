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
            if (state?.CanIssueBehavior != true || state.SelectingOpeningJob || state.SelectingInvitation)
                return ThinkResult.NoJob;
            // 读档后编号匹配时返回追踪器里的同一个 Job。原版覆写比较会保留它，不会从头启动。
            // 主树的安全响应仍可中断这一分支。
            // 获批接收器由训练驱动安装。行为树返回当前任务，让接收器继续运行。
            if (state.OwnsBehaviorJob(pawn.CurJob) || state.HoldsTrainingReceiver(pawn.CurJob) ||
                state.InvitedUseReceiverFor(pawn.CurJob) != null || state.WaitingInvitationFor(pawn.CurJob) != null)
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

    /// <summary>选择求用邀约的对象。实际配对交给 RJW，答复沿用伴侣优先和现有好感门槛。</summary>
    public sealed class ThinkNode_SSCSlaveBreakdownInvitation : ThinkNode
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (state?.TryBeginInvitationScan() != true) return ThinkResult.NoJob;
            // 候选查询可以同步重入原版的任务选择；只有最外层搜索负责作答、记冷却和派发。
            state.SelectingInvitation = true;
            try { return TryInvite(pawn, state); }
            finally { state.SelectingInvitation = false; }
        }

        private ThinkResult TryInvite(Pawn pawn, MentalState_SSCSlaveBreakdown state)
        {
            // 发作与 SSC 的冷却负责触发节奏。仍遵守 RJW 的启用、工作时段、身体条件，以及 SSC 生命周期钩子的可用性。
            if (pawn.Drafted || pawn.Downed || !pawn.Awake() || pawn.IsFighting() || pawn.Fogged() ||
                (pawn.IsDesignatedHero() && !RJWSettings.RPG_hero_control_AI_sex) || !xxx.can_do_loving(pawn) ||
                !pawn.HasParts() || !CasualSex_Helper.CanHaveSex(pawn) || pawn.mindState?.duty?.def == DutyDefOf.TravelOrLeave ||
                !SSCSceneHookAvailability.SharedReady)
                return ThinkResult.NoJob;

            if (xxx.gettin_raped == null || JobDriver_SSCBreakdownInvitedUse.Def == null ||
                !RJWSettings.rape_enabled || !SSCSceneHookAvailability.DriverReady(typeof(JobDriver_SSCBreakdownInvitedUse)))
                return ThinkResult.NoJob;

            // 实际参与资格仍按 RJW 身体条件和 SSC 许可。求用的询问和答复不使用普通约会的性取向及浪漫吸引力。
            bool prioritizePartners = !RJWSettings.HippieMode;
            var targets = pawn.Map.mapPawns.AllPawnsSpawned.Where(target => state.CanInvite(target) &&
                xxx.can_rape(target, forced: true) && MentalState_SSCSlaveBreakdown.CanReachInvitedUse(target, pawn))
                .OrderByDescending(target => prioritizePartners && IsPartner(pawn, target))
                .ThenBy(target => pawn.Position.DistanceToSquared(target.Position));
            foreach (Pawn partner in targets)
            {
                object personality = SSCPersonalityRuntimeUtility.Capture(partner);
                bool preferred = prioritizePartners && IsPartner(pawn, partner);
                if (!preferred && ((prioritizePartners && !SSCMasterAttractionUtility.AllowsCasualPair(pawn, partner)) ||
                    !MeetsOpinionThreshold(pawn, partner))) continue;

                // 保留好感导致的拒绝与对象冷却。TryRecordInvitation 再查实际动作是否兼容，成立才显示答复。
                bool accepted = preferred || MeetsOpinionThreshold(partner, pawn);
                if (!state.TryRecordInvitation(partner, personality, accepted, out Job job)) continue;
                // 接受提示在两端任务装好后由派发方显示。拒绝没有任务，仍由行为树当场显示。
                if (job != null) return new ThinkResult(job, this, JobTag.InMentalState, false);
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
                    "SSC_BreakdownInvitedUse_Refused".Translate(partner.LabelShort).ToString(), 3f);
                return ThinkResult.NoJob;
            }
            return ThinkResult.NoJob;
        }

        private static bool IsPartner(Pawn pawn, Pawn target)
            => pawn.GetRelations(target).Any(LovePartnerRelationUtility.IsLovePartnerRelation);

        private static bool MeetsOpinionThreshold(Pawn pawn, Pawn other)
        {
            CasualSex_Helper.GetCasualHookupSettingsForPawn(pawn, out _, out int minimumOpinion, out _);
            return pawn.relations.OpinionOf(other) >= minimumOpinion ||
                (SSCMasterAttractionPatches.Ready && SSCMasterAttractionUtility.IsEligibleDirectedPair(pawn, other));
        }
    }

    /// <summary>沿用 RJW 原候选及当前自动分流，另为游荡期单人任务记录本场所有权。</summary>
    public sealed class JobGiver_SSCSlaveBreakdownMasturbate : JobGiver_Masturbate
    {
        [ThreadStatic] internal static Pawn CandidatePawn;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            // 玩家已批准的调教要等当前任务结束；这段间隙可以进食、休息或游荡，但不再认领新的单人任务。
            if (state?.CanIssueBehavior != true || state.HasBehaviorJob || state.HasApprovedTraining)
                return ThinkResult.NoJob;
            Job previous = pawn.CurJob;
            Pawn previousCandidate = CandidatePawn;
            ThinkResult result;
            CandidatePawn = pawn;
            try { result = base.TryIssueJobPackage(pawn, jobParams); }
            finally { CandidatePawn = previousCandidate; }
            // 原始候选从基类返回时已通过 SSC 自动分流和许可 Postfix。
            if (!result.IsValid) return result;
            if (state.CanIssueBehavior && !state.HasBehaviorJob && !state.HasApprovedTraining && pawn.CurJob == previous)
            {
                state.SetBehaviorJob(result.Job);
                return result;
            }
            JobMaker.ReturnToPool(result.Job);
            return ThinkResult.NoJob;
        }
    }

    // 公开地点例外覆盖本场的单人候选、已认领的本人任务或指向原对象的邀约。
    internal static class SSCBreakdownPublicActivity
    {
        internal static bool Permits(Pawn pawn, Pawn partner = null)
        {
            if (pawn == null) return false;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(pawn);
            if (partner != null)
            {
                Job job = pawn.CurJob;
                if (job?.targetA.Pawn != partner) return false;
                // 求用的发作持有者是实际受方。只有持有原任务编号的准确方向才继承公开选址。
                if (!JobDriver_SSCBreakdownInvitedUse.IsInvitedJob(job)) return false;
                var subject = MentalState_SSCSlaveBreakdown.CurrentFor(partner);
                return subject?.CanIssueBehavior == true &&
                    subject.InvitationFor(job) is { } invited && invited.Partner == pawn &&
                    invited.ParticipantsAreCurrent(partner);
            }
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
            bool SafeToWander(IntVec3 cell) => cell.InBounds(map) &&
                (allowed == null || allowed.TrueCount == 0 || allowed[cell]) && !cell.Fogged(map) &&
                cell.Standable(map) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.None);
            // 只有聚集点和原游荡退路要求落在居住区。两种选址都遵守个人活动区与安全寻路。
            bool Usable(IntVec3 cell) => cell.InBounds(map) && home[cell] && SafeToWander(cell);

            // 每段游荡重新查原绑定主人和地图位置。主人可在居住区外活动，目标仍是附近可站立的合法格。
            // 征召、失去行动能力、离图或找不到合法格时，本轮继续走聚集点、房间与居住区选址。
            Pawn owner = MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.BoundOwner;
            if (owner?.Spawned == true && owner.Map == map && !owner.Drafted && !owner.Downed &&
                owner.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness) &&
                owner.health.capacities.CapableOf(PawnCapacityDefOf.Moving) &&
                CellFinder.TryFindRandomCellNear(owner.Position, map, 4, cell =>
                    cell != pawn.Position && cell != owner.Position && SafeToWander(cell), out IntVec3 nearOwner))
                return nearOwner;

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
