using RimWorld;
using Verse;

// 训导官记忆套所需的最小游戏契约模型；身份资格、套别分流与发放逻辑链接生产源码。
namespace Verse
{
    public struct ThoughtState
    {
        public bool Active;
        public int StageIndex;

        public static ThoughtState Inactive => default;

        public static ThoughtState ActiveAtStage(int index) => new ThoughtState { Active = true, StageIndex = index };
    }
}

namespace Verse.Sound
{
    /// <summary>生产文件引用了该命名空间但本套件不调用其成员，这里只提供占位类型。</summary>
    public static class SoundStub
    {
    }
}

namespace rjw
{
    /// <summary>生产文件引用了该命名空间但本套件不调用其成员，这里只提供占位类型。</summary>
    public static class RjwStub
    {
    }
}

namespace RimWorld
{
    public class ThoughtWorker
    {
        protected virtual ThoughtState CurrentSocialStateInternal(Verse.Pawn pawn, Verse.Pawn otherPawn)
            => ThoughtState.Inactive;

        /// <summary>仅为运行真实 Worker 暴露原版 protected 查询入口。</summary>
        public ThoughtState EvaluateForTest(Verse.Pawn pawn, Verse.Pawn otherPawn)
            => CurrentSocialStateInternal(pawn, otherPawn);
    }

    /// <summary>健康与 Hediff 容器的最小模型；旧模式部位尺寸查询会在空列表上提前返回。</summary>
    public sealed class HediffSet
    {
        public readonly System.Collections.Generic.List<Hediff> hediffs = new System.Collections.Generic.List<Hediff>();
    }

    public sealed class HealthTracker
    {
        public readonly HediffSet hediffSet = new HediffSet();
    }

    /// <summary>绑定关系查询替身：主人评价不在本套件范围内，保持无绑定。</summary>
    public static class SSCBondUtility
    {
        public static Verse.Pawn GetBoundMaster(Verse.Pawn slave) => null;
    }
}

namespace SexSlaveCraft
{
    public enum PawnIdentity { Unset, Slave, Master }

    /// <summary>只保留训导官资格判定所需的组件状态。</summary>
    public class CompSexSlaveTraining
    {
        public PawnIdentity pawnIdentity = PawnIdentity.Unset;
        public bool slaveTrainerEnabled;
        public bool trainerIdentityInitialized = true;
        public float lastTrainingScore;

        /// <summary>由用例直接设定“是否持有训导官任职资格”，不复制特化进度算法。</summary>
        public bool HasTrainerQualification;
    }

    /// <summary>特化资格入口的最小替身；真实进度判定由训导官特化套件覆盖。</summary>
    public enum TrainerSpecializationFailure { None, WrongSpecialization }

    public static class TrainerSpecializationUtility
    {
        public static bool HasTrainerQualification(Pawn pawn, out TrainerSpecializationFailure failure)
        {
            failure = TrainerSpecializationFailure.None;
            return pawn?.TryGetComp<CompSexSlaveTraining>()?.HasTrainerQualification == true;
        }
    }

    /// <summary>复用生产 IsTrainer 所需的最小身份入口；阶段与绑定判定不在本套件范围内。</summary>
    public static partial class SSCIdentityUtility
    {
        public static PawnIdentity GetIdentity(Pawn pawn)
            => pawn?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity ?? PawnIdentity.Unset;

        public static bool IsSexSlave(Pawn pawn) => GetIdentity(pawn) == PawnIdentity.Slave;

        public static bool IsMaster(Pawn pawn) => GetIdentity(pawn) == PawnIdentity.Master;
    }

    public class Settings
    {
        public bool useOldScoring;
    }

    public static class SSCMod
    {
        public static Settings settings = new Settings();
    }

    public static class Strings
    {
        public static string DailyTrainingOutcome(string score, string corruption) => score + "/" + corruption;

        public static string DailyTrainingOutcome_Legacy(string score, int level, string corruption)
            => score + "/" + level + "/" + corruption;
    }

    /// <summary>收益路径的外部边界：本套件只记录调用，不重写恶堕、意志与经验算法。</summary>
    public static class CorruptionUtility
    {
        public static int AddCalls;
        public static float LastCorruptionGain;

        public static void AddCorruption(Pawn pawn, float amount)
        {
            AddCalls++;
            LastCorruptionGain = amount;
        }
    }

    public static class WillReductionUtility
    {
        public static int ApplyCalls;

        public static void ApplyWillReductionByCorruptionBand(Pawn master, Pawn slave, float corruptionGain, bool isRitual)
            => ApplyCalls++;
    }

    public static class CorruptionProgressionUtility
    {
        public static int ProcessCalls;

        public static string ProcessCorruptionProgression(Pawn master, Pawn sexSlave, bool isRitual, float score = 0f)
        {
            ProcessCalls++;
            return string.Empty;
        }
    }

    public static class SSCMasterBondUtility
    {
        /// <summary>旧模式恶堕倍率固定为 1，保持收益断言可复现。</summary>
        public static float GetTrainingCorruptionMultiplier(Pawn master) => 1f;
    }

    /// <summary>
    /// 评分工具替身：本套件只验证记忆套别选择与发放，
    /// 因此把评分、好感变化和上限交给用例直接驱动，不复制评分与其随机项。
    /// </summary>
    public static class TrainingOutcomeUtility
    {
        public static float Score;
        public static float OpinionChange;
        public static float ChainCap = 1f;

        public static float GetScore(Pawn master, Pawn sexSlave) => Score;

        public static float CalculateCorruptionGain(float score) => 0.05f;

        public static float GetChainCap(Pawn pawn) => ChainCap;

        public static float CalculateOpinionChange(float score, Pawn master, Pawn sexSlave) => OpinionChange;
    }
}
