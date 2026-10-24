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
        /// <summary>检查当前身体、研究与启动身份；未指定部位时供手术清单查询。</summary>
        public static AcceptanceReport RecipientReport(Pawn pawn, BodyPartRecord part = null)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null || pawn.Dead || pawn.Destroyed ||
                pawn.Discarded || pawn.health?.hediffSet == null)
                return "当前无法进行子宫开发手术。";
            // 复用项目成年身体判据，不带入文化、绑定或性行为许可；原版 Adult 单独不足以排除青少年。
            if (pawn.gender != Gender.Female || !SSCCultureGenderUtility.IsAdult(pawn))
                return "需要成年女性人形身体。";
            if (!SSCIdentityUtility.IsSexSlave(pawn)) return "需要将 SSC 身份设为性奴。";
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_RES_UterusDevelopment))
                return "尚未完成子宫开发研究。";
            // 资格查询不修复或创建记录；已有本项目健康条目时不允许用手术重置进度。
            if (pawn.health.hediffSet.hediffs.Any(h => h.def?.defName == SSCUterusDevelopmentUtility.DefName))
                return "已经进行过子宫开发。";
            if (pawn.IsPregnant(false)) return "妊娠期间不能进行子宫开发手术。";
            BodyPartRecord torso = SSCUterusDevelopmentUtility.FindTorso(pawn);
            if (torso == null || (part != null && part != torso)) return "缺少有效的目标躯干部位。";
            // 直接读取身体健康列表，避开 RJW 器官缓存；只认仍附着于当前有效部位的阴道类别。
            // 不要求 CanBeFertilized 标签，也不把衣物阻塞或其他来源的不育加入启动门槛。
            if (!pawn.health.hediffSet.hediffs.Any(h => h.pawn == pawn &&
                TrainingExpUtility.IsPresentPart(pawn, h.Part) && Genital_Helper.is_vagina(h)))
                return "缺少适用的生殖器官。";
            var definition = DefDatabase<HediffDef>.GetNamedSilentFail(SSCUterusDevelopmentUtility.DefName);
            if (definition?.hediffClass != typeof(Hediff_SSCUterusDevelopment))
                return "子宫开发配置不可用。";
            return true;
        }

        /// <summary>使用原版配方技能门槛，兼容普通医生与受控医护机械体；其他接单限制继续由原版负责。</summary>
        public static AcceptanceReport DoctorReport(Pawn doctor, Pawn patient)
        {
            if (doctor == null || doctor == patient || doctor.Dead || doctor.Destroyed || doctor.Discarded)
                return "缺少可执行手术的医生。";
            var recipe = SSCDefOf.SSC_Surgery_StartUterusDevelopment;
            if (recipe == null) return "子宫开发手术配置不可用。";
            // 原版同时处理普通技能与受控机械体的固定技能及启用工作类型。
            // 不直接要求 skills 跟踪器；技能禁用或未达配方门槛仍由原版检查拒绝。
            if (!recipe.PawnSatisfiesSkillRequirements(doctor))
                return "医生医术需要达到 10 级。";
            return true;
        }
    }
}
