// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>为已经认领的普通性交完成事件处理一个供给方向；不拥有任务状态或结算次数。</summary>
    public static class SSCPnaSexSupplyUtility
    {
        public static SSCPnaExposureResult ApplyFromProducer(Pawn producer, Pawn recipient)
        {
            // 只认当前身体的普通产出能力；不按性别、姿势、避孕套或生产进度筛选。
            // 不消费产出资源，主人身份本身也不构成来源；未来实际侵染接入时在调用方择强。
            if (producer == recipient || !SSCPnaSourceUtility.IsPnaProducer(producer)) return default;
            // 统一入口负责主人免疫、有效链分流、需求准入及末期成瘾强化，避免重复实现。
            return SSCPnaExposureUtility.ApplyExposure(recipient, SSCPnaMath.ProducerSexDose, SSCPnaExposureSource.ProducerSex);
        }
    }
}
