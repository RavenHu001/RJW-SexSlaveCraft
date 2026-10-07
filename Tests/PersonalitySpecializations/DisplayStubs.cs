using System.Globalization;
using Verse;

namespace Verse
{
    // 翻译边界只返回键；引导选择逻辑来自生产显示分部。
    public static class TestTranslator { public static string Translate(this string key) => key; }
    public static class TestPercentFormatting
    {
        /// <summary>仅提供固定文化的数值格式，不实现任何特化显示选择。</summary>
        public static string ToStringPercent(this float value) => value.ToString("P0", CultureInfo.InvariantCulture);
    }
}

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>直接调用生产界面文字方法，不加载 Unity 绘制宿主。</summary>
        public static string LabelForTest(Pawn pawn, CompSexSlaveTraining comp) => GetSpecializationLabel(pawn, comp);
        /// <summary>直接调用绘制进度行使用的生产方法。</summary>
        public static string ProgressForTest(Pawn pawn, CompSexSlaveTraining comp) => GetSpecializationProgressText(pawn, comp);
    }

    public static class SSCDefOf { public static readonly object SSC_BasicTraining = new object(); }
    public static class ResearchUtils { public static bool IsResearchFinished(object research) => true; }

    // 翻译和健康状态是外部边界；用固定词与显式集合支持各种完成记录组合。
    public static class Strings
    {
        public const string ITab_SpecializationNone = "未选择";
        public const string ITab_SpecializationBus = "公交车";
        public const string ITab_SpecializationCow = "奶牛";
        public const string ITab_SpecializationTrainerOfficer = "训导官";
        public const string ITab_SpecializationCombatant = "战斗员";
        public const string ITab_SpecializationOrdinaryComplete = "普通培养完成";
        public const string ITab_SpecializationFinalizedSuffix = "已终极化";
        public const string ITab_SpecializationUnfinishedSuffix = "未完成内容";
        public const string ITab_SpecializationComplete = "已完成";
    }
    public static class BusSpecializationUtility
    {
        public static bool HasFinalBusState(Pawn pawn) => pawn.Finalized.Contains(SexSlaveSpecializationType.Bus);
        public static bool HasFinalCowState(Pawn pawn) => pawn.Finalized.Contains(SexSlaveSpecializationType.Cow);
    }
    public static class PetSpecializationUtility
    {
        public static bool HasFinalPetState(Pawn pawn, SexSlaveSpecializationType type) => pawn.Finalized.Contains(type);
        public static bool HasAnyFinalPetState(Pawn pawn) => pawn.Finalized.Contains(SexSlaveSpecializationType.PetCat) ||
            pawn.Finalized.Contains(SexSlaveSpecializationType.PetDog) || pawn.Finalized.Contains(SexSlaveSpecializationType.PetRabbit);
        public static string GetSpecializationLabel(SexSlaveSpecializationType type) => type.ToString();
    }
    public static class CombatantSpecializationUtility
    {
        public static bool HasFinalState(Pawn pawn) => pawn.Finalized.Contains(SexSlaveSpecializationType.Combatant);
    }
    public static class TrainerSpecializationUtility
    {
        public static bool HasFinalRecord(Pawn pawn) => pawn.Finalized.Contains(SexSlaveSpecializationType.TrainerOfficer);
    }
}
