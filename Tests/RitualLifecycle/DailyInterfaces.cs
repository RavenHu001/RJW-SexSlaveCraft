using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Verse
{
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
    public partial class Pawn
    {
        public bool Drafted;
        public int thingIDNumber;
        /// <summary>本流程模型默认不处于战斗状态。</summary>
        public bool IsFighting() => false;
        /// <summary>通过宿主预约存储登记生产驱动申请的目标，后续由生产交接工具释放。</summary>
        public bool Reserve(Pawn target, Job job, int count, int stack, object layer, bool error)
        {
            if (!Reservable) return false;
            Map.reservationManager.Reserve(target, this, job);
            return true;
        }
        /// <summary>日常持续预约检查沿用用例指定结果。</summary>
        public bool CanReserve(Pawn target, int count, int stack) => Reservable;
        /// <summary>用固定偏移支持无需渲染的周期效果调用。</summary>
        public int HashOffset() => thingIDNumber;
    }
}
namespace Verse.AI
{
    public enum RandomSocialMode { Off }
    public static class Toils_Reserve
    {
        /// <summary>接收预约使用无副作用步骤；具体预约守卫由交互套件验证。</summary>
        public static Toil Reserve(TargetIndex index, int count, int stack) => new Toil();
    }
}
namespace RimWorld
{
    public static class FleckDefOf { public static object Heart = new object(); }
    public static class PortraitsCache
    {
        /// <summary>无界面模型不维护肖像缓存。</summary>
        public static void SetDirty(Pawn pawn) { }
    }
}
namespace rjw
{
    public abstract class JobDriver_SexBaseReciever : JobDriver_Sex
    {
        public List<Pawn> parteners = new List<Pawn>();
    }
    public abstract class JobDriver_SexBaseRecieverLoved : JobDriver_SexBaseReciever
    {
        /// <summary>提供接收者准备签名，不复制RJW参与者算法。</summary>
        public void DoSetup() { }
    }
    public static class xxx
    {
        /// <summary>模型角色均为类人，允许执行生产接收收尾。</summary>
        public static bool is_human(Pawn pawn) => true;
    }
}
namespace SexSlaveCraft
{
    public enum TrainingActType { Auto }
    public partial class CompSexSlaveTraining { public TrainingActType selectedMode; }
    public static class RJWSexPropsUtility
    {
        /// <summary>测试不锁定特定动作，只提供生产方法签名。</summary>
        public static void ApplyTrainingAct(rjw.SexProps props, Pawn actor, Pawn target, TrainingActType mode) { }
    }
    public static class ConditioningUtility
    {
        /// <summary>返回固定分数以观察生产结算入口。</summary>
        public static float GetScore(Pawn actor, Pawn target) => 1;
        /// <summary>记录日常结算次数，不重写实际收益算法。</summary>
        public static void ExecuteOutcome(Pawn actor, Pawn target) => TestWorld.DailyOutcomes++;
    }
    public static class TrainingExpUtility
    {
        /// <summary>本流程套件不计算部位经验，只验证收益是否被调用。</summary>
        public static void ApplyExperienceFromScore(Pawn target, int kind, float score, float multiplier) { }
    }
}
