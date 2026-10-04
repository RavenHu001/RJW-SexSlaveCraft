// 亲昵、研究和界面仅提供编译边界；宠物定义映射、标签、资格及加工均链接生产源码。
using Verse;
using RimWorld;

namespace UnityEngine
{
    // 仅提供健康状态同步所需的数学接口；培养资格仍运行真实宠物源码。
    public static class Mathf { public static float Max(float a, float b) => System.Math.Max(a, b); }
}

namespace Verse
{
    // 亲昵调度与玩家选择的编译边界，本套件不以这些替身验证寻路或研究解锁。
    public class JobDef : Def { }
    public class ResearchProjectDef : Def { public bool IsFinished = true; }
    public partial class Pawn
    {
        public bool Downed, Drafted, Spawned = true;
        public JobDef CurJobDef => CurJob?.def;
    }
    public static class PetHostExtensions
    {
        public static bool DestroyedOrNull(this Thing thing) => thing == null || thing.Destroyed;
        public static bool InHorDistOf(this object position, object other, float distance) => true;
    }
    public static class GenTicks { public const int TickRareInterval = 250; }
    public static class Find { public static readonly PetTickManager TickManager = new PetTickManager(); }
    public class PetTickManager { public int TicksGame; }
}

namespace RimWorld
{
    // 保留生产宠物工具调用的等待任务与记忆接口，不运行真实任务或记忆合并。
    public static class JobDefOf
    {
        public static readonly JobDef Wait = new JobDef { defName = "Wait" };
        public static readonly JobDef Wait_Wander = new JobDef { defName = "Wait_Wander" };
    }
    public static class PetMemoryHost
    {
        public static void TryGainMemory(this MemoryHandler memories, ThoughtDef def, Pawn other) =>
            memories.TryGainMemory(ThoughtMaker.MakeThought(def), other);
    }
}

namespace Verse.AI
{
    // 这里的任务创建和接收仅用于编译亲昵分支；配方结算使用独立时序替身。
    public enum JobTag { Misc }
    public static class JobMaker { public static Job MakeJob(JobDef def, Pawn target) => new Job { def = def }; }
    public partial class Pawn_JobTracker { public bool TryTakeOrderedJob(Job job, JobTag tag) => true; }
}

namespace SexSlaveCraft
{
    // 补齐真实宠物工具依赖的冷却、绑定及文本字段，不替代宠物规则或标签迁移。
    public partial class CompSexSlaveTraining
    {
        public const int PetAffectionCooldownTicks = 60000;
        public int lastPetAffectionTick = -999999;
    }
    public static partial class SSCDefOf
    {
        public static readonly ResearchProjectDef SSC_BasicTraining = new ResearchProjectDef();
        public static readonly JobDef SSC_Job_PetAffection = new JobDef();
    }
    public static partial class SSCBondUtility
    {
        // 迁移套件不执行亲昵；实际绑定查询及对象变更由宠物专项观察验证。
        public static Pawn GetBoundMaster(Pawn pawn) => null;
        public static Pawn GetResolvedMaster(Pawn pawn) => null;
    }
    public static partial class Strings
    {
        public const string ITab_SpecializationPetCat = "cat", ITab_SpecializationPetDog = "dog", ITab_SpecializationPetRabbit = "rabbit";
        public const string ITab_SelectSpecializationPetCat = "select cat", ITab_SelectSpecializationPetDog = "select dog", ITab_SelectSpecializationPetRabbit = "select rabbit";
        public const string ITab_SpecializationNone = "none", ITab_SelectSpecializationNone = "select none";
        public const string ITab_SpecializationUnfinishedSuffix = "unfinished";
        public const string ITab_SpecializationPetDisabledResearch = "research", ITab_SpecializationPetDisabledMissingRequirements = "requirements";
        public const string ITab_SpecializationPetDisabledConflictingFinal = "conflicting final";
        public static string Message_PetSpecializationUnlocked(string name, string type) => name + type;
    }
}
