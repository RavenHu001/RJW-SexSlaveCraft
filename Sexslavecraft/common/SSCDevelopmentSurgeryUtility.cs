// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>身体开发手术共用的医生检查；接单和执行均保留原版配方技能语义。</summary>
    public static class SSCDevelopmentSurgeryUtility
    {
        /// <summary>检查实际医生与实际配方，兼容医护机械体，并复用已有本地化提示。</summary>
        public static AcceptanceReport DoctorReport(Pawn doctor, Pawn patient, RecipeDef recipe, string unavailableKey)
        {
            if (doctor == null || doctor == patient || doctor.Dead || doctor.Destroyed || doctor.Discarded)
                return "SSC_UterusDevelopment_DoctorRequired".Translate();
            if (recipe == null) return unavailableKey.Translate();
            // 不要求 skills 跟踪器；原版同时处理普通技能、机械体固定技能及禁用工作。
            if (!recipe.PawnSatisfiesSkillRequirements(doctor))
                return "SSC_Cosmetic_MedicineRequired".Translate(10);
            return true;
        }
    }
}
