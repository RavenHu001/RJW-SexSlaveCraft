// AI 辅助生成
using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>医美专属只读资格和一次结果效果；不接管任务、材料或异常恢复。</summary>
    public static class SSCCosmeticUtility
    {
        public const float SurgerySuccess = 0.05f, SurgeryFailure = 0.25f;
        public const float RitualSuccess = 0.15f, RitualFailure = 0.60f;
        public const string HostRole = "host", RecipientRole = "recipient";
        private static TraitDef Beauty => DefDatabase<TraitDef>.GetNamedSilentFail("Beauty");

        public static bool IsLiving(Pawn pawn) => pawn != null && !pawn.Dead && !pawn.Destroyed;

        private static AcceptanceReport Reject(string key) => key.Translate();

        /// <summary>普通档用 null 表示；从不构造 Beauty degree 0。</summary>
        public static AcceptanceReport NextBeauty(Pawn pawn, out Trait current, out int? nextDegree)
        {
            current = null;
            nextDegree = null;
            if (!IsLiving(pawn) || pawn.health?.hediffSet == null || pawn.story?.traits?.allTraits == null || !pawn.Spawned)
                return Reject("SSC_Cosmetic_InvalidTarget");
            if (!pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh || pawn.DevelopmentalStage != DevelopmentalStage.Adult)
                return Reject("SSC_Cosmetic_AdultFlesh");

            TraitDef beauty = Beauty;
            if (beauty?.degreeDatas == null || SSCDefOf.SSC_Hediff_NanoCosmeticReaction == null)
                return Reject("SSC_Cosmetic_UnsupportedBeauty");
            var traits = pawn.story.traits.allTraits;
            var owned = traits.Where(t => t?.def == beauty).ToList();
            if (owned.Any(t => t.Degree >= 2)) return Reject("SSC_Cosmetic_AtLimit");
            foreach (Trait trait in traits)
                if (trait?.def != null && trait.def != beauty && beauty.ConflictsWith(trait.def))
                    return "SSC_Cosmetic_Conflict".Translate(trait.LabelCap);
            if (owned.Count > 1) return Reject("SSC_Cosmetic_UnsupportedBeauty");
            current = owned.FirstOrDefault();
            if (current != null && (current.sourceGene != null || current.Suppressed || current.suppressedByTrait ||
                current.suppressedByGene != null || (current.Degree != -2 && current.Degree != -1 && current.Degree != 1)))
                return Reject("SSC_Cosmetic_UnsupportedBeauty");
            int? currentDegree = current?.Degree;
            if (currentDegree.HasValue && !beauty.degreeDatas.Any(d => d.degree == currentDegree.Value))
                return Reject("SSC_Cosmetic_UnsupportedBeauty");
            nextDegree = current == null ? 1 : current.Degree == -2 ? -1 : current.Degree == -1 ? (int?)null : 2;
            int? wantedDegree = nextDegree;
            if (wantedDegree.HasValue && !beauty.degreeDatas.Any(d => d.degree == wantedDegree.Value))
                return Reject("SSC_Cosmetic_UnsupportedBeauty");
            var reaction = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_NanoCosmeticReaction);
            if (reaction != null)
                return "SSC_Cosmetic_Recovering".Translate((reaction.Severity / 0.1f).ToString("0.0"));
            return true;
        }

        public static AcceptanceReport RecipientReport(Pawn pawn, bool surgery, Map map = null)
        {
            if (!IsLiving(pawn) || (map != null && pawn.Map != map)) return Reject("SSC_Cosmetic_InvalidTarget");
            if (!(pawn.IsColonistPlayerControlled || pawn.IsSlaveOfColony || (surgery && pawn.IsPrisonerOfColony)))
                return Reject(surgery ? "SSC_Cosmetic_SurgeryIdentity" : "SSC_Cosmetic_RitualIdentity");
            return NextBeauty(pawn, out _, out _);
        }

        public static AcceptanceReport DoctorReport(Pawn doctor, Pawn patient)
        {
            if (doctor == patient) return Reject("SSC_Cosmetic_DifferentPawns");
            var report = ActiveParticipantReport(doctor, patient == null ? TargetInfo.Invalid : new TargetInfo(patient));
            if (!report.Accepted) return report;
            if (!doctor.IsColonistPlayerControlled || doctor.IsSlave || doctor.IsPrisoner)
                return Reject("SSC_Cosmetic_FreeColonist");
            if (doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || doctor.skills?.GetSkill(SkillDefOf.Medicine) == null ||
                doctor.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled || doctor.skills.GetSkill(SkillDefOf.Medicine).Level < 8)
                return "SSC_Cosmetic_MedicineRequired".Translate(8);
            return true;
        }

        public static AcceptanceReport ActiveParticipantReport(Pawn pawn, TargetInfo target)
        {
            if (!IsLiving(pawn) || !pawn.Spawned || pawn.Downed || pawn.InMentalState ||
                pawn.health?.hediffSet?.HasHediff(HediffDefOf.Anesthetic) == true)
                return Reject("SSC_Cosmetic_CannotParticipate");
            if (!pawn.RaceProps.Humanlike || pawn.DevelopmentalStage != DevelopmentalStage.Adult)
                return Reject("SSC_Cosmetic_AdultHumanlike");
            if (target.IsValid && (pawn.Map != target.Map || !pawn.CanReach((LocalTargetInfo)target, PathEndMode.Touch, Danger.Deadly)))
                return "MessageRitualRoleCannotReach".Translate();
            return true;
        }

        public static bool HasRequiredMeme(Precept_Ritual ritual)
        {
            if (!ModsConfig.IdeologyActive || ritual?.ideo == null) return false;
            var meme = DefDatabase<MemeDef>.GetNamedSilentFail("SSC_Meme_MasterSlaveOrder");
            return meme != null && ritual.ideo.HasMeme(meme);
        }

        public static AcceptanceReport CultureReport(Pawn pawn, Precept_Ritual ritual)
        {
            // 原版无文化上下文的候选预检只验证个人条件；正式分配与开始时有完整上下文。
            if (ritual == null) return true;
            if (!HasRequiredMeme(ritual)) return Reject("SSC_Cosmetic_MemeRequired");
            if (pawn?.Ideo != ritual.ideo) return Reject("SSC_Cosmetic_SameIdeo");
            return true;
        }

        public static AcceptanceReport RitualRecipientReport(Pawn pawn, Precept_Ritual ritual, Map map = null)
        {
            var report = RecipientReport(pawn, false, map);
            return report.Accepted ? CultureReport(pawn, ritual) : report;
        }

        /// <summary>成功时只移除已确认的单个普通 Beauty；写入异常交给调用方记录，不回滚。</summary>
        public static bool ApplyResult(Pawn pawn, bool success, float severity)
        {
            if (!IsLiving(pawn)) return false;
            if (success)
            {
                var report = NextBeauty(pawn, out Trait current, out int? next);
                if (!report.Accepted) throw new InvalidOperationException("Cosmetic result became ineligible: " + report.Reason);
                var traits = pawn.story.traits;
                if (current != null) traits.RemoveTrait(current);
                if (next.HasValue) traits.GainTrait(new Trait(Beauty, next.Value));
                var actual = traits.allTraits.Where(t => t?.def == Beauty).ToList();
                if (next.HasValue ? actual.Count != 1 || actual[0].Degree != next.Value || actual[0].Suppressed : actual.Count != 0)
                    throw new InvalidOperationException("Beauty write did not produce the requested vanilla degree.");
            }
            var def = SSCDefOf.SSC_Hediff_NanoCosmeticReaction ?? throw new InvalidOperationException("Missing cosmetic reaction Def.");
            var existing = pawn.health.hediffSet.hediffs.Where(h => h.def == def).ToList();
            if (existing.Count == 0)
            {
                var hediff = HediffMaker.MakeHediff(def, pawn);
                hediff.Severity = severity;
                pawn.health.AddHediff(hediff);
            }
            else
            {
                existing[0].Severity = Math.Max(severity, existing.Max(h => h.Severity));
                foreach (var duplicate in existing.Skip(1)) pawn.health.RemoveHediff(duplicate);
            }
            return true;
        }
    }
}
