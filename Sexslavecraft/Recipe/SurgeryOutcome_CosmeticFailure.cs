// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版成功项后的确定性兜底；不再抽签、不附加伤害。</summary>
    public class SurgeryOutcome_CosmeticFailure : SurgeryOutcome
    {
        public SurgeryOutcome_CosmeticFailure() { failure = true; }

        public override bool Apply(float quality, RecipeDef recipe, Pawn surgeon, Pawn patient, BodyPartRecord part)
        {
            // 配置在原版成功项之后；一旦执行即承接失败并终止结果尝试，不再套用外科损伤。
            Recipe_BeautyEnhancement.ApplyFailure(patient);
            return true;
        }
    }
}
