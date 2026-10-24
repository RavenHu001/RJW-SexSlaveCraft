// AI 辅助生成
using System.Linq;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>胃部开发启动手术的只读资格检查；不参与已有开发记录的身份或开关管理。</summary>
    public static class SSCStomachSurgeryUtility
    {
        /// <summary>检查当前身体、研究与启动身份，返回本地化拒绝原因；未指定部位时供手术清单查询。</summary>
        public static AcceptanceReport RecipientReport(Pawn pawn, BodyPartRecord part = null)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null || pawn.Dead || pawn.Destroyed ||
                pawn.Discarded || pawn.health?.hediffSet == null)
                return "SSC_StomachDevelopment_SurgeryUnavailable".Translate();
            // 复用项目成年身体判据，不带入文化、绑定或性行为许可；原版 Adult 单独不足以排除青少年。
            if (!SSCCultureGenderUtility.IsAdult(pawn))
                return "SSC_StomachDevelopment_AdultRequired".Translate();
            if (!SSCIdentityUtility.IsSexSlave(pawn)) return "SSC_UterusDevelopment_SlaveIdentityRequired".Translate();
            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_RES_UterusDevelopment))
                return "SSC_UterusDevelopment_ResearchRequired".Translate();
            // 资格查询不修复或创建记录；已有本项目健康条目时不允许用手术重置进度。
            if (pawn.health.hediffSet.hediffs.Any(h => h.def?.defName == SSCStomachDevelopmentUtility.DefName))
                return "SSC_StomachDevelopment_AlreadyStarted".Translate();
            BodyPartRecord stomach = SSCStomachDevelopmentUtility.FindStomach(pawn);
            if (stomach == null || (part != null && part != stomach)) return "SSC_StomachDevelopment_MissingStomach".Translate();
            // 口部仅作身体存在检查；衣物阻塞、性行为许可及妊娠不构成启动限制。
            if (!Genital_Helper.has_mouth(pawn))
                return "SSC_StomachDevelopment_MissingMouth".Translate();
            var definition = DefDatabase<HediffDef>.GetNamedSilentFail(SSCStomachDevelopmentUtility.DefName);
            if (definition?.hediffClass != typeof(Hediff_SSCStomachDevelopment))
                return "SSC_StomachDevelopment_ConfigUnavailable".Translate();
            return true;
        }

        /// <summary>使用原版技能门槛，兼容医护机械体；已有医术提示复用原键，其他接单限制由原版负责。</summary>
        public static AcceptanceReport DoctorReport(Pawn doctor, Pawn patient)
        {
            // 使用实际配方执行共用检查，不借用另一项身体开发的配方引用。
            return SSCDevelopmentSurgeryUtility.DoctorReport(doctor, patient,
                SSCDefOf.SSC_Surgery_StartStomachDevelopment,
                "SSC_StomachDevelopment_SurgeryConfigUnavailable");
        }
    }
}
