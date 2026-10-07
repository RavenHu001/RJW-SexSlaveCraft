using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Attraction;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>成功亲昵后的固定配对事件；这里只尝试宠物向真实主人发起自愿 Quickie。</summary>
    public static class PetAffectionFollowupUtility
    {
        public const float FollowupChance = 0.20f;
        private const int HandoffWaitTicks = 600;

        /// <summary>准备期间重新检查双方资格；地点和准备任务的归属由专用驱动检查。</summary>
        public static bool CanContinue(Pawn pet, Pawn master)
        {
            // 兔的既有亲昵仍可用，但本轮后续只开放生效的猫、狗；普通残留标签不授予资格。
            if (pet?.TryGetComp<CompSexSlaveTraining>() == null || master == null || pet == master ||
                SSCBondUtility.GetBoundMaster(pet) != master ||
                (!PetSpecializationUtility.HasActivePetEffects(pet, SexSlaveSpecializationType.PetCat) &&
                 !PetSpecializationUtility.HasActivePetEffects(pet, SexSlaveSpecializationType.PetDog)) ||
                !CanParticipate(pet) || !CanParticipate(master) || pet.Map != master.Map) return false;

            // 全局自愿互动开关、双方身体资格与原版冷却均保留；不借主人身份交换发起方向。
            if (!RJWHookupSettings.HookupsEnabled || !RJWHookupSettings.QuickHookupsEnabled ||
                !CasualSex_Helper.CanHaveSex(pet) || !CasualSex_Helper.CanHaveSex(master) ||
                !SexUtility.ReadyForLovin(pet) || !SexUtility.ReadyForLovin(master) ||
                !SexUtility.ReadyForHookup(pet) || !SexUtility.ReadyForHookup(master) ||
                !CasualSex_Helper.CanTargetHookup(pet, master) ||
                !CasualSex_Helper.CanTargetHookup(master, pet)) return false;

            bool lovers = LovePartnerRelationUtility.LovePartnerRelationExists(pet, master);
            if (!HasDesire(pet, lovers) || !HasDesire(master, lovers)) return false;
            if (!lovers && !RJWSettings.WildMode && !RJWSettings.HippieMode &&
                (!AllowsCasualPair(pet) || !AllowsCasualPair(master))) return false;

            // 只评估既定双方，不调用原版 FindPartner/RNG 搜索、随机换人或再叠加一次触发概率。
            // 吸引力内部仍使用 RJW 自己的确定性种子评估；双方阈值和玩家配对设置照常生效。
            var settings = new AppraisalSettings(AttractionPurpose.ForFucking);
            var appraisal = new AppraisalResult(pet, settings, master, settings);
            return SexAppraiser.FindBestResults(new[] { appraisal }).Any() &&
                CasualSex_Helper.CasualHookupAllowedViaSettings(appraisal);
        }

        private static bool CanParticipate(Pawn pawn)
        {
            // 明确限定成年人；Teenager 的 developmentalStage 可能同为 Adult，故另查18岁及RJW成年类别。
            return pawn != null && !pawn.Destroyed && !pawn.Dead && pawn.Spawned &&
                pawn.RaceProps?.Humanlike == true && pawn.ageTracker != null &&
                pawn.ageTracker.AgeBiologicalYears >= 18 && pawn.DevelopmentalStage == DevelopmentalStage.Adult &&
                pawn.GetAgeCategory() == AgeCategory.Adult && !pawn.Downed && !pawn.Drafted &&
                !pawn.InMentalState && pawn.health?.capacities?.CanBeAwake == true && pawn.Awake() &&
                !pawn.IsFighting();
        }

        private static bool HasDesire(Pawn pawn, bool lovers)
            => lovers || RJWSettings.WildMode || RJWSettings.HippieMode ||
                xxx.is_nympho(pawn) || xxx.is_frustrated(pawn) || xxx.is_horny(pawn);

        private static bool AllowsCasualPair(Pawn pawn)
            => AttractionUtility.CanFoolAround(pawn, true) ||
                (RJWHookupSettings.NymphosCanCheat && xxx.is_nympho(pawn) && xxx.is_frustrated(pawn)) ||
                pawn.HasBeerGoggles();

        /// <summary>先核对固定配对与有向许可，再消耗本次20%判定；失败时不安装任何任务。</summary>
        public static Job TryPrepare(Pawn pet, Pawn master, Job waiting)
        {
            Job source = pet?.CurJob;
            int sourceId = source?.loadID ?? -1;
            JobDriver sourceDriver = pet?.jobs?.curDriver;
            int waitId = waiting?.loadID ?? -1;
            if (!HasWaiting(master, waiting, waitId) || !CanContinue(pet, master) ||
                !HasWaiting(master, waiting, waitId) || !HasSource(pet, source, sourceId, sourceDriver)) return null;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("SSC_Job_PetAffectionFollowup");
            if (def?.driverClass != typeof(JobDriver_PetAffectionFollowup)) return null;
            Job candidate = JobMaker.MakeJob(def, master);
            bool prepared = false;
            try
            {
                // PrepareEvent 按实际宠物→主人查询 Consensual 许可；不产生 ordered/playerForced 豁免。
                prepared = SSCRestrictionJobGuard.PrepareEvent(pet, candidate, SSCRestrictionEvent.PetAffection) &&
                    HasWaiting(master, waiting, waitId) && HasSource(pet, source, sourceId, sourceDriver) &&
                    Rand.Chance(FollowupChance);
                return prepared ? candidate : null;
            }
            finally
            {
                if (!prepared) CancelPrepared(candidate);
            }
        }

        /// <summary>接管亲昵已经拥有的等待；安装失败时只释放这一个等待，不清空双方其他任务。</summary>
        public static bool TryStart(Pawn pet, Pawn master, Job followup, Job waiting)
        {
            bool invoked = false;
            bool accepted = false;
            int waitId = waiting?.loadID ?? -1;
            int followupId = followup?.loadID ?? -1;
            JobDef followupDef = followup?.def;
            Job source = pet?.CurJob;
            int sourceId = source?.loadID ?? -1;
            JobDriver sourceDriver = pet?.jobs?.curDriver;
            try
            {
                if (followup == null || followup.GetTarget(TargetIndex.A).Pawn != master ||
                    !HasWaiting(master, waiting, waitId) || !CanContinue(pet, master) ||
                    !HasWaiting(master, waiting, waitId) || !HasSource(pet, source, sourceId, sourceDriver) ||
                    followup.loadID != followupId || followup.def != followupDef) return false;
                SSCRestrictionJobGuard.RegisterEventWait(pet, followup, master, waiting);
                // 资格与登记都可能经第三方补丁同步替换工作；移交前最后核对原任务实例、编号和驱动。
                if (!HasWaiting(master, waiting, waitId) || !HasSource(pet, source, sourceId, sourceDriver) ||
                    followup.loadID != followupId || followup.def != followupDef) return false;
                // expiryInterval 从开始 tick 计算，保留已过去的亲昵时间后再增加600 tick准备窗口。
                waiting.expiryInterval = Find.TickManager.TicksGame - waiting.startTick + HandoffWaitTicks;
                invoked = true;
                pet.jobs.StartJob(followup, JobCondition.InterruptForced, tag: JobTag.Misc,
                    preToilReservationsCanFail: true);
                accepted = pet.CurJob == followup && followup.loadID == followupId && followup.def == followupDef &&
                    pet.jobs.curDriver is JobDriver_PetAffectionFollowup active && active.job == followup && active.pawn == pet;
                return accepted;
            }
            finally
            {
                if (!accepted)
                {
                    // StartJob 已调用时由原版管理对象池；不能把可能已经回收的任务再归池。
                    if (followup?.loadID == followupId && followup.def == followupDef)
                    {
                        if (invoked)
                        {
                            SSCRestrictionJobGuard.CancelPendingEvent(followup);
                            pet?.jobs?.jobQueue.RemoveAll(pet, queued => queued == followup &&
                                queued.loadID == followupId && queued.def == followupDef);
                        }
                        else CancelPrepared(followup);
                    }
                    Job current = master?.CurJob;
                    if (current == waiting && current != null && current.loadID == waitId && current.def == JobDefOf.Wait)
                        master.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }

        /// <summary>仅用于尚未安装的候选；移除待交接来源，避免对象池保留旧参与者。</summary>
        public static void CancelPrepared(Job followup)
        {
            if (followup == null) return;
            SSCRestrictionJobGuard.CancelPendingEvent(followup);
            JobMaker.ReturnToPool(followup);
        }

        private static bool HasWaiting(Pawn master, Job waiting, int id)
            => waiting != null && master?.CurJob == waiting && waiting.loadID == id &&
                waiting.def == JobDefOf.Wait && !waiting.playerForced;

        // 新排队命令优先于自动后续；同一原任务实例、编号和驱动也不能掩盖玩家刚追加的工作。
        private static bool HasSource(Pawn pet, Job source, int id, JobDriver driver)
            => pet?.jobs != null && pet.CurJob == source && pet.jobs.curDriver == driver &&
                (source?.loadID ?? -1) == id && !pet.jobs.jobQueue.Any(queued => queued.job != null);
    }
}
