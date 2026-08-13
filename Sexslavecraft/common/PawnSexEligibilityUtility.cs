using System.Linq;
using System.Text;
using RimWorld;
using rjw;
using Verse;

// EN: This file wraps the project's sex-target eligibility logic.
// EN: It bridges SSC legacy checks, RJW checks, and player-facing diagnostics.
// CN: 这个文件封装了项目里的性行为目标资格判定。
// CN: 它桥接 SSC 旧判定、RJW 判定和面向玩家的诊断信息。
namespace SexSlaveCraft
{
    public static class Trainjudge
    {
        public static bool TryCanBeFuckedWithReason(Pawn pawn, out string shortReason, out string detailedReport)
        {
            bool useRJWOriginalEligibility = SSCMod.settings?.useRJWOriginalEligibility ?? true;
            bool result = useRJWOriginalEligibility ? LegacyCanBeFucked(pawn, out _) : xxx.can_be_fucked(pawn);
            shortReason = BuildCanBeFuckedShortReason(pawn, useRJWOriginalEligibility, result);
            detailedReport = BuildCanBeFuckedReport(pawn);
            return result;
        }

        public static bool TryCanBeFuckedWithReport(Pawn pawn, out string report)
        {
            bool result = TryCanBeFuckedWithReason(pawn, out _, out string detailedReport);
            report = detailedReport;
            return result;
        }

        private static string BuildCanBeFuckedShortReason(Pawn pawn, bool useRJWOriginalEligibility, bool result)
        {
            if (pawn == null)
            {
                return Strings.RJW_Short_TargetNull;
            }

            if (useRJWOriginalEligibility)
            {
                LegacyCanBeFucked(pawn, out string legacyReason);
                return legacyReason;
            }

            if (result)
            {
                return Strings.RJW_Short_Pass;
            }

            if (xxx.is_mechanoid(pawn))
            {
                return Strings.RJW_Short_Fail_Mechanoid;
            }

            if (!xxx.can_do_loving(pawn))
            {
                if (xxx.is_human(pawn) && pawn.GetAgeCategory() == AgeCategory.Child)
                {
                    return Strings.RJW_Short_Fail_Age;
                }

                bool hasWarcasket = pawn.apparel?.WornApparel != null &&
                                   pawn.apparel.WornApparel.Any(x => x.def?.defName != null && x.def.defName.ToLower().Contains("warcasket"));
                if (hasWarcasket)
                {
                    return Strings.RJW_Short_Fail_Warcasket;
                }

                return Strings.RJW_Short_Fail_CanDoLoving;
            }

            bool anusUsable = Genital_Helper.has_anus(pawn) && !Genital_Helper.anus_blocked(pawn);
            bool vaginaUsable = Genital_Helper.has_vagina(pawn) && !Genital_Helper.vagina_blocked(pawn);
            bool oralUsable = Genital_Helper.has_mouth(pawn) && !Genital_Helper.oral_blocked(pawn);

            if (!anusUsable && !vaginaUsable && !oralUsable)
            {
                return Strings.RJW_Short_Fail_NoUsableOrifice;
            }

            return Strings.RJW_Short_Fail_CanBeFucked;
        }

        public static string BuildCanBeFuckedReport(Pawn pawn)
        {
            if (pawn == null) return "SSC_RJW_TargetNull".Translate();

            bool isHuman = xxx.is_human(pawn);
            bool isAnimal = xxx.is_animal(pawn);
            bool isMechanoid = xxx.is_mechanoid(pawn);

            AgeCategory ageCategory = pawn.GetAgeCategory();
            bool allowYouthSex = RJWSettings.AllowYouthSex;
            int bioYears = pawn.ageTracker?.AgeBiologicalYears ?? -1;
            float growth = pawn.ageTracker?.Growth ?? -1f;
            bool reproductive = pawn.ageTracker?.CurLifeStage?.reproductive ?? false;
            string lifeStage = pawn.ageTracker?.CurLifeStage?.defName ?? "null";

            bool canDoLoving = xxx.can_do_loving(pawn);
            bool hasRJWPESexConfig = RJWPECompatibility.TryGetSexAgeConfigForPawn(pawn, out RJWPECompatibility.SexAgeConfigSnapshot rjwpeConfig);

            bool hasAnus = Genital_Helper.has_anus(pawn);
            bool anusBlocked = Genital_Helper.anus_blocked(pawn);
            bool anusUsable = hasAnus && !anusBlocked;

            bool hasVagina = Genital_Helper.has_vagina(pawn);
            bool vaginaBlocked = Genital_Helper.vagina_blocked(pawn);
            bool vaginaUsable = hasVagina && !vaginaBlocked;

            bool hasMouth = Genital_Helper.has_mouth(pawn);
            bool oralBlocked = Genital_Helper.oral_blocked(pawn);
            bool oralUsable = hasMouth && !oralBlocked;

            bool hasWarcasket = pawn.apparel?.WornApparel != null &&
                               pawn.apparel.WornApparel.Any(x => x.def?.defName != null && x.def.defName.ToLower().Contains("warcasket"));

            bool useRJWOriginalEligibility = SSCMod.settings?.useRJWOriginalEligibility ?? true;
            bool canBeFucked = xxx.can_be_fucked(pawn);
            bool rapeEnabled = RJWSettings.rape_enabled;
            bool legacyCanBeFucked = LegacyCanBeFucked(pawn, out string legacyReason);
            bool legacyGrowthGatePass = true;

            string reason;
            if (useRJWOriginalEligibility)
            {
                reason = legacyReason;
            }
            else if (canBeFucked)
            {
                reason = "通过：RJW 判定该 Pawn 可作为被进入目标。";
            }
            else if (isMechanoid)
            {
                reason = "失败：机械体未被 RJW 兼容逻辑判定为可性行为目标。";
            }
            else if (!canDoLoving)
            {
                if (isHuman && ageCategory == AgeCategory.Child)
                    reason = "失败：AgeCategory=Child，RJW 的 can_do_loving 返回 false。";
                else if (hasWarcasket)
                    reason = "失败：检测到 warcasket 装备，RJW 的 can_do_loving 返回 false。";
                else
                    reason = "失败：RJW 的 can_do_loving 返回 false。";

                if (hasRJWPESexConfig && rjwpeConfig.OverrideAgeCheck)
                {
                    if (bioYears < rjwpeConfig.MinimumAgeSex)
                    {
                        reason += $" RJW-PE 最低性交年龄={rjwpeConfig.MinimumAgeSex:F1}，当前生理年龄={bioYears}。";
                    }
                    else
                    {
                        reason += $" RJW-PE 年龄阈值已满足（minSex={rjwpeConfig.MinimumAgeSex:F1}），失败更可能由其他条件导致（如装备阻挡或位点不可用）。";
                    }
                }
            }
            else
            {
                reason = "失败：没有任何可用受位（anus/vagina/oral 均不可用或被阻挡）。";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SSC_RJW_ReportHeader".Translate());
            sb.AppendLine($"- eligibilityMode: {(useRJWOriginalEligibility ? "ssc-loose" : "rjw-original")}");
            sb.AppendLine($"- pawn: {pawn.LabelShort}");
            sb.AppendLine($"- raceFlags: human={isHuman}, animal={isAnimal}, mechanoid={isMechanoid}");
            sb.AppendLine($"- age: category={ageCategory}, allowYouthSex={allowYouthSex}, bioYears={bioYears}, growth={growth:F3}, lifeStage={lifeStage}, reproductive={reproductive}");
            if (hasRJWPESexConfig)
            {
                sb.AppendLine($"- rjwpe: detected={rjwpeConfig.Detected}, source={rjwpeConfig.Source}, overrideAgeCheck={rjwpeConfig.OverrideAgeCheck}, minSex={rjwpeConfig.MinimumAgeSex:F1}, minRape={rjwpeConfig.MinimumAgeRape:F1}, note={rjwpeConfig.Note}");
            }
            else
            {
                bool rjwpeLoaded = RJWPECompatibility.IsRJWPELoaded();
                string note = (rjwpeConfig != null && !string.IsNullOrEmpty(rjwpeConfig.Note)) ? rjwpeConfig.Note : "not available";
                sb.AppendLine($"- rjwpe: detected={rjwpeLoaded}, sexConfig=unavailable, note={note}");
            }
            sb.AppendLine($"- can_do_loving: {canDoLoving}");
            sb.AppendLine($"- slot.anus: has={hasAnus}, blocked={anusBlocked}, usable={anusUsable}");
            sb.AppendLine($"- slot.vagina: has={hasVagina}, blocked={vaginaBlocked}, usable={vaginaUsable}");
            sb.AppendLine($"- slot.oral: has={hasMouth}, blocked={oralBlocked}, usable={oralUsable}");
            sb.AppendLine($"- hasWarcasket: {hasWarcasket}");
            sb.AppendLine($"- can_be_fucked: {canBeFucked}");
            sb.AppendLine($"- legacyCheck: rapeEnabled={rapeEnabled}, growthGatePass={legacyGrowthGatePass}, result={legacyCanBeFucked}");
            sb.Append($"- reason: {reason}");

            return sb.ToString();
        }

        public static bool SexSlaveTrainJudge(Pawn pawn)
        {
            return TryCanBeFuckedWithReport(pawn, out _);
        }

        private static bool LegacyCanBeFucked(Pawn pawn, out string reason)
        {
            if (!RJWSettings.rape_enabled)
            {
                reason = Strings.Legacy_Short_RequireRapeEnabled;
                return false;
            }

            if (!xxx.can_be_fucked(pawn))
            {
                reason = Strings.Legacy_Short_FailCanBeFucked;
                return false;
            }

            reason = Strings.Legacy_Short_Pass;
            return true;
        }
    }
}
