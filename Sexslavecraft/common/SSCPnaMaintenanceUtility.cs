// AI 辅助生成：锁链维持选择、原上下文提交与原版补药任务的候选交接。
using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Verse.AI;

[assembly: InternalsVisibleTo("PnaMaintenanceNative")]

namespace SexSlaveCraft
{
    // 保存值稳定；零表示关闭，其余值对应现有PNA活化Def的五阶段。
    public enum SSCPnaMaintenanceStage { Off, First, Second, Third, Fourth, Fifth }

    /// <summary>原请求凭据；存档只保存实际角色／锁链／主人及选择世代，不保存第二份配置。</summary>
    public sealed class SSCPnaMaintenanceContext : IExposable
    {
        internal Pawn Pawn, Master;
        internal Hediff_ChainOfSexSlave Chain;
        internal SSCPnaMaintenanceStage Stage;
        internal int Revision, JobId;
        internal CompSexSlaveTraining Comp;
        internal object PersonalityToken;

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref Master, "master", saveDestroyedThings: true);
            Scribe_References.Look(ref Chain, "chain");
            Scribe_Values.Look(ref Stage, "stage", SSCPnaMaintenanceStage.Off);
            Scribe_Values.Look(ref Revision, "revision", 0);
            Scribe_Values.Look(ref JobId, "jobId", 0);
            // 先恢复并验证原保存owner；进程内凭据只建立新进程的基线，不改绑新锁链。
            if (Scribe.mode == LoadSaveMode.PostLoadInit && SSCPnaMaintenanceUtility.IsOwnerCurrent(this))
            {
                Comp = Pawn.TryGetComp<CompSexSlaveTraining>();
                PersonalityToken = SSCPersonalityRuntimeUtility.Capture(Pawn);
            }
        }
    }

    public static class SSCPnaMaintenanceUtility
    {
        public const string JobDefName = "SSC_PnaMaintenance";
        private const string RawDefName = "Purple_Aphrodisiac_Nanofluid";
        private static readonly ConditionalWeakTable<Job, SSCPnaMaintenanceContext> pending = new();
        internal static JobDef JobDefinition => DefDatabase<JobDef>.GetNamedSilentFail(JobDefName);
        private static HediffDef LoadDefinition => DefDatabase<HediffDef>.GetNamedSilentFail(SSCPnaExposureUtility.RootedLoadDefName);
        public static bool IsLegalStage(SSCPnaMaintenanceStage stage) => stage >= SSCPnaMaintenanceStage.Off && stage <= SSCPnaMaintenanceStage.Fifth;

        /// <summary>与PNA保护一致，零锁链阶段也有效；返回真正有效的owner，跳过无效残链。</summary>
        public static Hediff_ChainOfSexSlave FindChain(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            foreach (Hediff record in pawn.health.hediffSet.hediffs)
                if (record is Hediff_ChainOfSexSlave chain && chain.pawn == pawn &&
                    chain.LinkedPawn != null && !chain.LinkedPawn.Destroyed && !chain.LinkedPawn.Discarded)
                    return chain;
            return null;
        }

        /// <summary>只捕获既有引用，不创建锁链、负荷或保存默认设置。</summary>
        public static SSCPnaMaintenanceContext Capture(Pawn pawn)
        {
            var chain = FindChain(pawn);
            return new SSCPnaMaintenanceContext
            {
                Pawn = pawn, Chain = chain, Master = chain?.LinkedPawn,
                Stage = chain?.PnaMaintenanceStage ?? SSCPnaMaintenanceStage.Off,
                Revision = chain?.PnaMaintenanceRevision ?? 0,
                Comp = pawn?.TryGetComp<CompSexSlaveTraining>(),
                PersonalityToken = SSCPersonalityRuntimeUtility.Capture(pawn)
            };
        }

        internal static bool IsOwnerCurrent(SSCPnaMaintenanceContext context)
            => context?.Pawn?.health?.hediffSet != null && context.Chain != null &&
                ReferenceEquals(FindChain(context.Pawn), context.Chain) &&
                ReferenceEquals(context.Chain.LinkedPawn, context.Master) &&
                context.Chain.PnaMaintenanceRevision == context.Revision && context.Chain.PnaMaintenanceStage == context.Stage;

        public static bool IsCurrent(SSCPnaMaintenanceContext context)
            => IsOwnerCurrent(context) && context.Pawn.TryGetComp<CompSexSlaveTraining>() == context.Comp &&
                SSCPersonalityRuntimeUtility.IsCurrent(context.Pawn, context.PersonalityToken);

        private static bool Eligible(Pawn pawn)
            => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && !pawn.Suspended &&
                pawn.RaceProps?.Humanlike == true && pawn.RaceProps.IsFlesh && SSCIdentityUtility.IsSexSlave(pawn) &&
                !SSCPersonalityRuntimeUtility.IsMutating(pawn);

        /// <summary>名称直接取当前Def阶段；仅关闭和无效配置提示由本功能提供。</summary>
        public static string StageLabel(SSCPnaMaintenanceStage stage)
        {
            if (stage == SSCPnaMaintenanceStage.Off) return "SSC_PnaMaintenance_Off".Translate();
            var stages = LoadDefinition?.stages;
            int index = (int)stage - 1;
            return IsLegalStage(stage) && stages != null && index >= 0 && index < stages.Count && stages[index] != null && !stages[index].label.NullOrEmpty()
                ? stages[index].label : "SSC_PnaMaintenance_Unavailable".Translate().ToString();
        }

        /// <summary>按Def原始严重度比较，第一档下限零单独解释为活化消失后补给。</summary>
        public static bool NeedsDose(Pawn pawn, SSCPnaMaintenanceStage stage)
        {
            if (!IsLegalStage(stage) || stage == SSCPnaMaintenanceStage.Off) return false;
            var stages = LoadDefinition?.stages;
            int index = (int)stage - 1;
            if (stages == null || index >= stages.Count || stages[index] == null) return false;
            var record = SSCPnaExposureUtility.GetBodyRecord(pawn);
            float severity = SSCPnaMath.NormalizeSeverity(record?.Severity ?? 0f);
            if (stage == SSCPnaMaintenanceStage.First) return record == null || severity <= SSCPnaMath.RemovalSeverity;
            float minimum = stages[index].minSeverity;
            return SSCPnaMath.IsFinite(minimum) && minimum > 0f && minimum <= SSCPnaMath.MaximumSeverity && severity < minimum;
        }

        /// <summary>阶段菜单的提交准入；资格、原owner和原选择世代同时校验，UI禁用不替代此入口。</summary>
        public static bool CanSet(SSCPnaMaintenanceContext context, SSCPnaMaintenanceStage stage, out string reason)
        {
            string key = null;
            if (!IsCurrent(context)) key = "SSC_BodyManagement_BodyChanged";
            else if (!IsLegalStage(stage)) key = "SSC_PnaMaintenance_Unavailable";
            else if (Scribe.mode != LoadSaveMode.Inactive || !Eligible(context.Pawn) || context.Comp == null ||
                !SSCIdentityUtility.IsSupportedVanillaStatus(context.Pawn)) key = "SSC_PnaMaintenance_Unavailable";
            else if (stage != SSCPnaMaintenanceStage.Off && (LoadDefinition?.stages == null || (int)stage > LoadDefinition.stages.Count))
                key = "SSC_PnaMaintenance_Unavailable";
            reason = key?.Translate().ToString();
            return key == null;
        }

        /// <summary>只有显式提交写链；同值幂等，修改时撤销旧补给且回调后不重新授权。</summary>
        public static bool TrySet(SSCPnaMaintenanceContext context, SSCPnaMaintenanceStage stage, out string reason)
        {
            if (!CanSet(context, stage, out reason)) return false;
            context.Chain.SetPnaMaintenanceStage(stage);
            // 取消旧任务的清理可触发其他模块；写入后只复查原引用，不为回调后的新owner签发权限。
            if (FindChain(context.Pawn) != context.Chain || context.Chain.LinkedPawn != context.Master ||
                context.Pawn.TryGetComp<CompSexSlaveTraining>() != context.Comp ||
                !SSCPersonalityRuntimeUtility.IsCurrent(context.Pawn, context.PersonalityToken) ||
                !Eligible(context.Pawn) || !SSCIdentityUtility.IsSupportedVanillaStatus(context.Pawn) || context.Chain.PnaMaintenanceStage != stage)
            {
                reason = "SSC_BodyManagement_BodyChanged".Translate();
                return false;
            }
            return true;
        }

        /// <summary>只允许原生基础闲逛／等待；准备等待、命令、队列以及任何实际工作都不抢断。</summary>
        public static bool IsBetweenTasks(Pawn pawn)
        {
            if (pawn?.jobs == null || pawn.CurJob?.playerForced == true) return false;
            foreach (QueuedJob queued in pawn.jobs.jobQueue) if (queued.job != null) return false;
            Job current = pawn.CurJob;
            return current == null || current.def == JobDefOf.Wait_Wander || current.def?.defName == "GotoWander" ||
                (current.def == JobDefOf.Wait && current.expiryInterval <= 0);
        }

        /// <summary>地图自主补药的廉价准入；不找药、不预留，也不为不满足者创建待执行任务。</summary>
        public static bool CanStart(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || !Eligible(pawn) || !pawn.Spawned || pawn.Map == null ||
                pawn.Drafted || pawn.Downed || pawn.InMentalState || pawn.health.hediffSet.InLabor() || !IsBetweenTasks(pawn)) return false;
            var chain = FindChain(pawn);
            return chain != null && NeedsDose(pawn, chain.PnaMaintenanceStage) && JobDefinition != null;
        }

        private static bool DrugValid(Pawn pawn, Thing drug)
            => drug != null && !drug.Destroyed && drug.IngestibleNow && drug.def.defName == RawDefName &&
                (!drug.Spawned || (!drug.IsForbidden(pawn) && pawn.CanReserve(drug, 10, 1) && drug.IsSociallyProper(pawn)));

        /// <summary>背包优先；地图只用原生可达／预留查询，不遍历其他角色库存或另建补货任务。</summary>
        private static Thing FindDrug(Pawn pawn)
        {
            var raw = DefDatabase<ThingDef>.GetNamedSilentFail(RawDefName);
            if (raw == null) return null;
            var inventory = pawn.inventory?.innerContainer;
            if (inventory != null)
                foreach (Thing thing in inventory)
                    if (thing.def == raw && DrugValid(pawn, thing)) return thing;
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(raw), PathEndMode.ClosestTouch,
                TraverseParms.For(pawn), 9999f, thing => DrugValid(pawn, thing));
        }

        /// <summary>只在原生选任务入口找一份原液，找到后登记原凭据；缺药直接交还普通工作。</summary>
        public static Job TryMakeJob(Pawn pawn)
        {
            if (!CanStart(pawn)) return null;
            var context = Capture(pawn);
            Thing drug = FindDrug(pawn);
            // 查找可能经过外部查询回调；仍只接受原链与原选择。
            if (drug == null || !IsCurrent(context) || !CanStart(pawn) || !DrugValid(pawn, drug)) return null;
            Job job = JobMaker.MakeJob(JobDefinition, drug);
            job.count = 1;
            context.JobId = job.loadID;
            pending.Remove(job);
            pending.Add(job, context);
            return job;
        }

        /// <summary>实际运行驱动领取候选原凭据；loadID避免Job对象池复用取得旧授权。</summary>
        internal static SSCPnaMaintenanceContext Claim(JobDriver driver)
        {
            if (driver?.job == null || driver.pawn?.jobs?.curDriver != driver ||
                !pending.TryGetValue(driver.job, out var context)) return null;
            pending.Remove(driver.job);
            return context.JobId == driver.job.loadID && context.Pawn == driver.pawn ? context : null;
        }

        internal static bool CanContinue(SSCPnaMaintenanceContext context, JobDriver driver, bool checkNeed = true)
            => context != null && driver?.job != null && context.JobId == driver.job.loadID && context.Pawn == driver.pawn &&
                driver.pawn?.jobs?.curDriver == driver && driver.pawn.CurJob == driver.job &&
                IsCurrent(context) && Eligible(driver.pawn) && driver.pawn.Spawned && driver.pawn.Map != null && !driver.pawn.Drafted &&
                !driver.pawn.Downed && !driver.pawn.InMentalState && !driver.pawn.health.hediffSet.InLabor() &&
                (!checkNeed || NeedsDose(driver.pawn, context.Stage));

        internal static bool IsMaintenance(Job job) => job?.def?.defName == JobDefName;

        internal static void CancelForChain(Pawn pawn, Hediff_ChainOfSexSlave chain)
        {
            if (pawn?.jobs?.curDriver is JobDriver_SSCPnaMaintenance driver && driver.Owns(chain))
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        }
    }
}
