using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>将阶段 3A 的 RJW 任务转换为有方向的统一请求；这里只识别上下文，不决定许可。</summary>
    internal static class SSCRestrictionJobContext
    {
        /// <summary>仅为 RJW 任务获取缓存驱动，避免在全局 AI 和命令入口提前构造无关任务驱动。</summary>
        public static JobDriver_Sex GetDriver(Job job, Pawn pawn)
        {
            if (job?.def?.driverClass == null || !typeof(JobDriver_Sex).IsAssignableFrom(job.def.driverClass)) return null;
            return job.GetCachedDriver(pawn) as JobDriver_Sex;
        }

        /// <summary>标识尚未接管的任务，避免共用 RJW 基类导致日常、仪式和专项兼容同时受两套规则控制。</summary>
        private static bool IsDeferred(JobDriver_Sex driver)
        {
            string name = driver?.job?.def?.defName;
            return driver is JobDriver_Training || driver is JobDriver_RitualTraining ||
                name == "SSC_Training_SexSlave" || name == "SSC_Training_Ritual" ||
                name == "rjw_genes_lifeforce_randomrape" || name == "rjw_genes_lifeforce_seduced";
        }

        /// <summary>沿接收任务的实际 Partner 查找指回本人的发起任务，不从调教员身份或装备猜测用途。</summary>
        public static JobDriver_SexBaseInitiator FindInitiator(JobDriver_SexBaseReciever receiver)
        {
            var actor = receiver?.Partner?.jobs?.curDriver as JobDriver_SexBaseInitiator;
            return actor != null && actor.Partner == receiver.pawn ? actor : null;
        }

        /// <summary>识别本批任务及双方方向；返回 false 仅代表留给后续批次的适配器，不是许可放行。</summary>
        public static bool TryCreate(JobDriver_Sex driver, out SSCRestrictionRequest request)
        {
            request = null;
            if (driver == null || IsDeferred(driver)) return false;
            if (driver is JobDriver_SexBaseReciever receiver)
            {
                JobDriver_SexBaseInitiator actor = FindInitiator(receiver);
                if (actor != null) return TryCreate(actor, out request);
                if (receiver.GetType().Assembly != typeof(JobDriver_Sex).Assembly &&
                    receiver.job?.def != SSCDefOf.SSC_TrainingReceiver) return false;
                // 接收方持有的数据可能属于已离开的第一位参与者，不能用它冒充当前发起任务。
                request = new SSCRestrictionRequest(receiver.Partner, receiver.pawn, SSCInteractionKind.Unknown, false);
                return true;
            }

            Pawn initiator = driver.pawn;
            Pawn partner = driver.Partner;
            bool known = driver is JobDriver_SexBaseInitiator && initiator != null;
            SSCInteractionKind kind;
            if (driver is JobDriver_Masturbate && (partner == null || partner == initiator))
                kind = SSCInteractionKind.Masturbation;
            else if (driver is JobDriver_PE)
                kind = SSCInteractionKind.PersonalityExcretion;
            else if (partner == null || partner == initiator)
                kind = SSCInteractionKind.Unknown;
            else
                kind = driver is JobDriver_Rape || partner.Dead || driver.Sexprops?.isRape == true ||
                    partner.jobs?.curDriver is JobDriver_SexBaseRecieverRaped
                    ? SSCInteractionKind.Forced : SSCInteractionKind.Consensual;

            SexProps props = driver.Sexprops;
            // 姿势反转 isRevese 不影响发起方向；已有数据必须与当前任务的有向参与者一致。
            if (props != null && (props.initiator != initiator ||
                (props.recipient != partner && !(kind == SSCInteractionKind.Masturbation && props.recipient == initiator))))
                known = false;
            request = new SSCRestrictionRequest(initiator, partner, kind, known);
            return true;
        }
    }
}
