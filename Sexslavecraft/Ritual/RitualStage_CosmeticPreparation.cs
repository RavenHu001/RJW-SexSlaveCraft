// AI 辅助生成
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>集合／消费阶段不计入正式仪式的 5000 tick，其余阶段行为沿用原版。</summary>
    public class RitualStage_CosmeticPreparation : RitualStage
    {
        public override float ProgressPerTick(LordJob_Ritual ritual) => 0f;
    }
}
