using System.Collections.Generic;
using Verse;

namespace Verse
{
    public static class DefDatabase<T> where T : Def { public static List<T> AllDefsListForReading = new List<T>(); }
    public static class Translation
    {
        /// <summary>无界面测试保留提示键，不加载语言系统。</summary>
        public static string Translate(this string key, params object[] args) => key;
    }
    public class LookTargets
    {
        /// <summary>提供提示目标签名，不模拟镜头跳转。</summary>
        public LookTargets(params Pawn[] pawns) { }
    }
    public static class Messages
    {
        /// <summary>接收生产取消提示，不在测试中显示界面。</summary>
        public static void Message(string reason, LookTargets targets, object type, bool historical) { }
    }
}
namespace RimWorld { public static class MessageTypeDefOf { public static object RejectInput = new object(); } }
namespace SexSlaveCraft
{
    public enum PawnIdentity { Unset, Slave, Master }
    public static class SSCBondUtility
    {
        /// <summary>提供真实策略所需的有向绑定引用。</summary>
        public static Pawn GetBoundMaster(Pawn pawn) => pawn?.BoundMaster;
        /// <summary>仅承认明确指向当前发起者的绑定。</summary>
        public static bool IsBoundTo(Pawn target, Pawn actor) => actor != null && target?.BoundMaster == actor;
    }
    public static class SSCIdentityUtility
    {
        /// <summary>读取模型显式主人身份。</summary>
        public static bool IsMaster(Pawn pawn) => pawn?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity == PawnIdentity.Master;
        /// <summary>按模型身份开关暴露资格，不修改身份或绑定。</summary>
        public static bool IsTrainer(Pawn pawn) => IsMaster(pawn) || (pawn?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity == PawnIdentity.Slave && pawn.TryGetComp<CompSexSlaveTraining>().slaveTrainerEnabled);
    }
    public static class TrainerAssignmentUtility
    {
        /// <summary>复用生产调教适配器，完整工作扫描由身份套件验证。</summary>
        public static bool IsAllowedTrainer(Pawn target, Pawn actor, bool forced = false) =>
            SSCRestrictionTrainingUtility.Evaluate(SSCRestrictionTrainingUtility.CreateRequest(actor, target, false), !forced).Allowed;
        /// <summary>提供有效指派边界，完整身份和指派代码由 TrainerIdentity 套件验证。</summary>
        public static Pawn GetActiveAssignedTrainer(Pawn target)
        {
            Pawn actor = target?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            return actor != null && actor != target && !actor.Dead && !actor.Destroyed && SSCIdentityUtility.IsTrainer(actor) ? actor : null;
        }
    }
    public static class BusSpecializationUtility
    {
        /// <summary>本仪式流程模型不额外模拟公交车状态。</summary>
        public static bool HasAnyBusState(Pawn pawn) => false;
    }
    public static class TrainerSpecializationUtility
    {
        /// <summary>仪式生命周期模型不设置训导官方向；完整资格由 TrainerIdentity 套件检查。</summary>
        public static bool HasActiveRestrictionEffect(Pawn pawn) => false;
    }
    public static class SSCRestrictionJobGuard
    {
        /// <summary>提供精确开始凭据的编译边界；真实守卫与加载顺序由 InteractionProtection 验证。</summary>
        public static void RestoreRitualScene(JobDriver_RitualTraining driver, bool started) { }
    }
}
