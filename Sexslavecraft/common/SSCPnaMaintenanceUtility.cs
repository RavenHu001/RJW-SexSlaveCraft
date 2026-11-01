// AI 辅助生成：锁链维持选择、原上下文提交与原版补药任务的候选交接。
using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Verse.AI;

[assembly: InternalsVisibleTo("PnaMaintenanceNative")]

namespace SexSlaveCraft
{
    /// <summary>稳定存档枚举；零表示关闭，其余值对应现有PNA活化Def的五阶段。</summary>
    public enum SSCPnaMaintenanceStage { Off, First, Second, Third, Fourth, Fifth }

    /// <summary>原请求凭据；存档只保存实际角色／锁链／主人及选择世代，不保存第二份配置。</summary>
    public sealed class SSCPnaMaintenanceContext : IExposable
    {
        internal Pawn Pawn, Master;
        internal Hediff_ChainOfSexSlave Chain;
        internal SSCPnaMaintenanceStage Stage;
        internal int Revision, JobId;
        // 组件与人格token只用于本进程防止原地换人格续权；它们不是存档中的授权副本。
        internal CompSexSlaveTraining Comp;
        internal object PersonalityToken;

        /// <summary>保存原请求的世界引用与世代；读档只为仍匹配原链的请求恢复进程内凭据。</summary>
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

    /// <summary>集中管理锁链配置的查询／提交和自主补药准入；实际摄入仍由原版驱动与既有PNA入口执行。</summary>
    public static class SSCPnaMaintenanceUtility
    {
        public const string JobDefName = "SSC_PnaMaintenance";
        private const string RawDefName = "Purple_Aphrodisiac_Nanofluid";
        // 候选和运行驱动并非同一个对象，故凭据暂附Job；弱键不额外延长候选Job的生命期。
        private static readonly ConditionalWeakTable<Job, SSCPnaMaintenanceContext> pending = new();
        /// <summary>读取已加载的专用任务定义；缺失时不临时创建替代任务。</summary>
        internal static JobDef JobDefinition => DefDatabase<JobDef>.GetNamedSilentFail(JobDefName);
        /// <summary>阶段名称与阈值共用当前环境最终Def，不维护第二份阶段数值表。</summary>
        private static HediffDef LoadDefinition => DefDatabase<HediffDef>.GetNamedSilentFail(SSCPnaExposureUtility.RootedLoadDefName);
        /// <summary>只接受已声明的六个保存值，拒绝未知整数而非裁剪成可用档位。</summary>
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

        /// <summary>比较原链实例、主人与设置世代；关闭后重开同档也不能恢复旧请求权限。</summary>
        internal static bool IsOwnerCurrent(SSCPnaMaintenanceContext context)
            => context?.Pawn?.health?.hediffSet != null && context.Chain != null &&
                ReferenceEquals(FindChain(context.Pawn), context.Chain) &&
                ReferenceEquals(context.Chain.LinkedPawn, context.Master) &&
                context.Chain.PnaMaintenanceRevision == context.Revision && context.Chain.PnaMaintenanceStage == context.Stage;

        /// <summary>在原载体校验之外复查组件和人格token，覆盖同一身体原地替换人格的情况。</summary>
        public static bool IsCurrent(SSCPnaMaintenanceContext context)
            => IsOwnerCurrent(context) && context.Pawn.TryGetComp<CompSexSlaveTraining>() == context.Comp &&
                SSCPersonalityRuntimeUtility.IsCurrent(context.Pawn, context.PersonalityToken);

        /// <summary>菜单与执行共用的身体／身份准入；绑定、地图和忙碌条件由各自调用入口追加。</summary>
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
            // 第一档下限是零，不能使用severity小于零来触发；濒临原生移除的记录也视为已消退。
            if (stage == SSCPnaMaintenanceStage.First) return record == null || severity <= SSCPnaMath.RemovalSeverity;
            float minimum = stages[index].minSeverity;
            return SSCPnaMath.IsFinite(minimum) && minimum > 0f && minimum <= SSCPnaMath.MaximumSeverity && severity < minimum;
        }

        /// <summary>阶段菜单的提交准入；资格、原owner和原选择世代同时校验，UI禁用不替代此入口。</summary>
        public static bool CanSet(SSCPnaMaintenanceContext context, SSCPnaMaintenanceStage stage, out string reason)
        {
            string key = null;
            // 先拒绝过期请求，再检查编辑与目标档位；关闭无需依赖阶段Def，但仍须有效原链及身份。
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
            // 排队命令同样代表已有安排；Wait_MaintainPosture及限时Wait可能属于其他互动的准备阶段。
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

        /// <summary>只接普通原液；地图物品沿原版禁用、预留及社会归属检查，随身物品无需地图预约。</summary>
        private static bool DrugValid(Pawn pawn, Thing drug)
            => drug != null && !drug.Destroyed && drug.IngestibleNow && drug.def.defName == RawDefName &&
                (!drug.Spawned || (!drug.IsForbidden(pawn) && pawn.CanReserve(drug, 10, 1) && drug.IsSociallyProper(pawn)));

        /// <summary>背包优先；地图只用原生可达／预留查询，不遍历其他角色库存或另建补货任务。</summary>
        private static Thing FindDrug(Pawn pawn)
        {
            var raw = DefDatabase<ThingDef>.GetNamedSilentFail(RawDefName);
            if (raw == null) return null;
            var inventory = pawn.inventory?.innerContainer;
            // 随身药物不触发地图寻路；找不到时才使用原版可达查询，并让同一验证器过滤候选。
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
            // 一次只申请一份；保存候选当时的loadID，防止对象池复用同一Job引用时取走旧凭据。
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

        /// <summary>执行检查不要求空闲，但必须仍是原运行任务；消费后仅跳过不足判断，保留其余撤权检查。</summary>
        internal static bool CanContinue(SSCPnaMaintenanceContext context, JobDriver driver, bool checkNeed = true)
            => context != null && driver?.job != null && context.JobId == driver.job.loadID && context.Pawn == driver.pawn &&
                driver.pawn?.jobs?.curDriver == driver && driver.pawn.CurJob == driver.job &&
                IsCurrent(context) && Eligible(driver.pawn) && driver.pawn.Spawned && driver.pawn.Map != null && !driver.pawn.Drafted &&
                !driver.pawn.Downed && !driver.pawn.InMentalState && !driver.pawn.health.hediffSet.InLabor() &&
                (!checkNeed || NeedsDose(driver.pawn, context.Stage));

        /// <summary>按专用任务标识识别本模块的服药，不接管手动摄入或原版定时服药。</summary>
        internal static bool IsMaintenance(Job job) => job?.def?.defName == JobDefName;

        /// <summary>设置变更只取消持有同一原链的当前补药；不立即选新任务，避免设置回调内重入调度。</summary>
        internal static void CancelForChain(Pawn pawn, Hediff_ChainOfSexSlave chain)
        {
            if (pawn?.jobs?.curDriver is JobDriver_SSCPnaMaintenance driver && driver.Owns(chain))
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        }
    }
}
