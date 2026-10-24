// AI 辅助生成
using System.Linq;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>子宫开发启动手术的只读资格检查；不参与已有开发记录的身份或开关管理。</summary>
    public static class SSCUterusSurgeryUtility
    {
        /// <summary>检查当前身体、研究与启动身份，返回本地化拒绝原因；未指定部位时供手术清单查询。</summary>
        public static AcceptanceReport RecipientReport(Pawn pawn, BodyPartRecord part = null)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null || pawn.Dead || pawn.Destroyed ||
                pawn.Discarded || pawn.health?.hediffSet == null)
                return "SSC_UterusDevelopment_SurgeryUnavailable".Translate();
            // 复用项目成年身体判据，不带入文化、绑定或性行为许可；原版 Adult 单独不足以排除青少年。
            if (pawn.gender != Gender.Female || !SSCCultureGenderUtility.IsAdult(pawn))
                return "SSC_UterusDevelopment_AdultFemaleRequired".Translate();
            if (!SSCIdentityUtility.IsSexSlave(pawn)) return "SSC_UterusDevelopment_SlaveIdentityRequired".Translate();
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_RES_UterusDevelopment))
                return "SSC_UterusDevelopment_ResearchRequired".Translate();
            // 资格查询不修复或创建记录；已有本项目健康条目时不允许用手术重置进度。
            if (pawn.health.hediffSet.hediffs.Any(h => h.def?.defName == SSCUterusDevelopmentUtility.DefName))
                return "SSC_UterusDevelopment_AlreadyStarted".Translate();
            if (pawn.IsPregnant(false)) return "SSC_UterusDevelopment_PregnancyBlocked".Translate();
            BodyPartRecord torso = SSCUterusDevelopmentUtility.FindTorso(pawn);
            if (torso == null || (part != null && part != torso)) return "SSC_UterusDevelopment_MissingTorso".Translate();
            // 直接读取身体健康列表，避开 RJW 器官缓存；只认仍附着于当前有效部位的阴道类别。
            // 不要求 CanBeFertilized 标签，也不把衣物阻塞或其他来源的不育加入启动门槛。
            if (!pawn.health.hediffSet.hediffs.Any(h => h.pawn == pawn &&
                TrainingExpUtility.IsPresentPart(pawn, h.Part) && Genital_Helper.is_vagina(h)))
                return "SSC_UterusDevelopment_MissingOrgan".Translate();
            var definition = DefDatabase<HediffDef>.GetNamedSilentFail(SSCUterusDevelopmentUtility.DefName);
            if (definition?.hediffClass != typeof(Hediff_SSCUterusDevelopment))
                return "SSC_UterusDevelopment_ConfigUnavailable".Translate();
            return true;
        }

        /// <summary>使用原版技能门槛，兼容医护机械体；已有医术提示复用原键，其他接单限制由原版负责。</summary>
        public static AcceptanceReport DoctorReport(Pawn doctor, Pawn patient)
        {
            if (doctor == null || doctor == patient || doctor.Dead || doctor.Destroyed || doctor.Discarded)
                return "SSC_UterusDevelopment_DoctorRequired".Translate();
            var recipe = SSCDefOf.SSC_Surgery_StartUterusDevelopment;
            if (recipe == null) return "SSC_UterusDevelopment_SurgeryConfigUnavailable".Translate();
            // 原版同时处理普通技能与受控机械体的固定技能及启用工作类型。
            // 不直接要求 skills 跟踪器；技能禁用或未达配方门槛仍由原版检查拒绝。
            if (!recipe.PawnSatisfiesSkillRequirements(doctor))
                // 医美已有相同语义的参数化行医／医术提示，复用四语键，不再新增一份医生文案。
                return "SSC_Cosmetic_MedicineRequired".Translate(10);
            return true;
        }
    }
}
