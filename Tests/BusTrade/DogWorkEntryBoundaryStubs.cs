using System;
using Verse;
using Verse.AI;

// 仅补齐生产 Harmony 宿主签名；用例显式调用生产 Postfix，不自动打补丁或模拟训练成功率。
namespace Verse.AI
{
    public enum TargetIndex { A, B, C }
    public class Toil { public Pawn actor; public Action initAction; }
    public partial class Job
    {
        /// <summary>返回训练任务 A 目标，其他索引保持空目标而不借用当前事件参与者。</summary>
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.A ? targetA : default;
    }
}
namespace Verse
{
    public partial struct LocalTargetInfo { public Pawn Pawn => Thing as Pawn; }
}
namespace RimWorld
{
    public static class Toils_Interpersonal
    {
        public static Toil TryTrain(TargetIndex traineeInd) => new Toil();
    }
    public class InteractionDef { }
    public static class InteractionDefOf
    {
        public static readonly InteractionDef TrainAttempt = new InteractionDef();
        public static readonly InteractionDef TameAttempt = new InteractionDef();
    }
    public class Pawn_InteractionsTracker
    {
        public bool Result = true;
        /// <summary>只提供互动是否发生的返回值；成功率判定由原动作边界明确模拟。</summary>
        public bool TryInteractWith(Pawn recipient, InteractionDef intDef) => Result;
    }
}
namespace rjw
{
    public static class SexUtility { public static void ProcessSex(SexProps props) { } }
}
