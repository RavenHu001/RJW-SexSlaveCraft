// AI 辅助生成：身体与外观页的孕前选项文案。
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>提供孕前枚举文案，按当前语言显示自然选项及对应来源。</summary>
        internal static string PreconceptionOptionLabel(bool species, int selected)
        {
            string suffix = selected == 1 ? species ? "SpeciesSelf" : "GenderMale"
                : selected == 2 ? species ? "SpeciesPartner" : "GenderFemale"
                : species ? "SpeciesNatural" : "GenderNatural";
            return ("SSC_Preconception_" + suffix).Translate();
        }
    }
}
