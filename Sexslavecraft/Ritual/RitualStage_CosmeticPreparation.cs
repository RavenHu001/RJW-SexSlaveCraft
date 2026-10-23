// AI 辅助生成
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>集合／消费阶段不计入正式仪式的 5000 tick，其余阶段行为沿用原版。</summary>
    public class RitualStage_CosmeticPreparation : RitualStage
    {
        // 集合可持续任意长度，瞬时消费也不占正式时长；切换条件和保存仍使用原版阶段。
        public override float ProgressPerTick(LordJob_Ritual ritual) => 0f;
    }
}
