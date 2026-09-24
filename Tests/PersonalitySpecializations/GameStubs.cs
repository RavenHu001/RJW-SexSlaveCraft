using Verse;

namespace Verse
{
    public class Thing { }
    public class Pawn : Thing { }
}

namespace SexSlaveCraft
{
    public enum RabbitReproductionMode { Offspring, Clone }

    // 仅提供生产特化模块所依赖的训练配置与游戏对象边界；进度算法全部来自生产源码。
    public partial class CompSexSlaveTraining
    {
        public Thing parent;
        public bool allowOthersForTrainingOrSex;
        public RabbitReproductionMode rabbitReproductionMode;
        public float savedCowReservoirCharge;
        public bool trainerInvalidExitBlocksAdoption;
        public int inactiveStateCleanupCalls;
        public SexSlaveSpecializationType lastKeptType;

        /// <summary>记录健康状态清理请求，不在无游戏宿主中模拟健康状态或训练进度。</summary>
        private static void RemoveInactiveSpecializationStates(Pawn pawn, CompSexSlaveTraining comp, SexSlaveSpecializationType typeToKeep)
        {
            comp.inactiveStateCleanupCalls++;
            comp.lastKeptType = typeToKeep;
        }
    }

    // 此套件只测试公共进度切换；状态维护的生产代码在 TrainerIdentity 中单独链接验证。
    public static class TrainerSpecializationLifecycle
    {
        public static void Notify(Pawn pawn) { }
    }
}
