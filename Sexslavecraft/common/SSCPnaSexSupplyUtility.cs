// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>为已经认领的普通性交完成事件处理一个供给方向；不拥有任务状态或结算次数。</summary>
    public static class SSCPnaSexSupplyUtility
    {
        public static SSCPnaExposureResult ApplyFromProducer(Pawn producer, Pawn recipient)
            => ApplyFromProducer(producer, recipient, null);

        public static SSCPnaExposureResult ApplyFromProducer(Pawn producer, Pawn recipient, Func<bool> continuationGuard)
        {
            // 只认当前身体的普通产出能力；不按性别、姿势、避孕套或生产进度筛选。
            // 不消费产出资源，主人身份本身也不构成来源；调用方在此前已完成每方向择强。
            if (producer == recipient || !SSCPnaSourceUtility.IsPnaProducer(producer)) return default;
            // 统一入口负责主人免疫、有效链分流、需求准入及末期成瘾强化，避免重复实现。
            return SSCPnaExposureUtility.ApplyExposure(recipient, SSCPnaMath.ProducerSexDose, SSCPnaExposureSource.ProducerSex,
                continuationGuard: continuationGuard);
        }

        /// <summary>完成前只读捕获；与原版 Lovin 共用当前身体产量，不增加参与部位或避孕套门槛。</summary>
        public static float CaptureInfestationDose(Pawn source) => SSCMasterInfestationFluidUtility.CaptureDose(source);

        public static SSCPnaExposureResult ApplyInfestation(Pawn source, Pawn recipient, Func<bool> continuationGuard)
        {
            // 保留既有调用签名；独立调用同样先检查资格，再读取产量，不消费或生成体液。
            if (continuationGuard?.Invoke() != true || !SSCDesireInfestationUtility.CanAffect(source, recipient)) return default;
            return ApplyInfestation(source, recipient, CaptureInfestationDose(source), continuationGuard);
        }

        internal static SSCPnaExposureResult ApplyInfestation(Pawn source, Pawn recipient, float dose, Func<bool> continuationGuard)
        {
            // 强来源复用共享输入的主人免疫、有效链与首次满值规则，不在此重复写需求或成瘾。
            // 动态量只传给负荷参数；需求补满、固定成瘾增量及末期判定仍由原来源规则处理。
            if (continuationGuard?.Invoke() != true || !SSCDesireInfestationUtility.CanAffect(source, recipient)) return default;
            return SSCPnaExposureUtility.ApplyExposure(recipient, dose,
                SSCPnaExposureSource.MasterInfestation, continuationGuard: continuationGuard);
        }
    }
}
