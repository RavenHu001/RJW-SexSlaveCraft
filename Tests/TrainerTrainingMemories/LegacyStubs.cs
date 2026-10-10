using System.Collections.Generic;
using RimWorld;
using Verse;

// 旧模式记忆分配所需的游戏契约替身；等级判定仍走生产 LegacyTrainingUtility。
namespace RimWorld
{
    /// <summary>社交关系的最小模型，只提供旧模式评分使用的单向好感查询。</summary>
    public sealed class Pawn_RelationsTracker
    {
        public int OpinionOf(Verse.Pawn other) => 0;
    }

    public sealed class GuestTracker
    {
        public bool IsSlave;
    }

    public static class SkillDefOf
    {
        public static readonly SkillDef Social = new SkillDef { defName = "Social" };
    }

    /// <summary>固定随机源：使旧模式评分与等级判定在测试中完全可复现。</summary>
    public static class Rand
    {
        public static float RangeValue;
        public static bool ChanceResult;

        public static float Range(float min, float max) => RangeValue;

        public static bool Chance(float probability) => ChanceResult;
    }

    public sealed class Hediff
    {
        public bool IsPenis;
        public bool IsVagina;
    }

    public static class Genital_Helper
    {
        public static List<Hediff> get_AllPartsHediffList(Verse.Pawn pawn) => new List<Hediff>();

        public static bool is_penis(Hediff hediff) => hediff.IsPenis;

        public static bool is_vagina(Hediff hediff) => hediff.IsVagina;
    }

    public static class PartSizeCalculator
    {
        public static bool TryGetLength(Hediff hediff, out float length) { length = 0f; return false; }

        public static bool TryGetGirth(Hediff hediff, out float girth) { girth = 0f; return false; }
    }
}

namespace SexSlaveCraft
{
    /// <summary>旧模式评分只读取锁链阶段；本套件用用例直接设定的阶段值。</summary>
    public static partial class SSCIdentityUtility
    {
        /// <summary>由用例设定的当前性奴阶段，默认未持有锁链。</summary>
        public static int SexSlaveStage;

        public static int GetSexSlaveStage(Pawn pawn) => SexSlaveStage;
    }
}
