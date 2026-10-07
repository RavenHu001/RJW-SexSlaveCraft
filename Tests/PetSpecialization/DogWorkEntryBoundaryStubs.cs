// 仅提供编译宿主；Harmony 属性不执行自动补丁，原版 TryTrain 与互动通过测试动作显式驱动。
using System;
using Verse;
using Verse.AI;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string methodName) { }
    }
}

namespace RimWorld
{
    public class InteractionDef : Def { }
    public class Pawn_InteractionsTracker
    {
        public bool TryInteractWith(Pawn recipient, InteractionDef intDef) => false;
    }
}

namespace RimWorld
{
    public static class Toils_Interpersonal
    {
        public static Toil TryTrain(TargetIndex traineeInd) => new();
    }
}

namespace RimWorld
{
    public static class InteractionDefOf
    {
        public static readonly InteractionDef TrainAttempt = new() { defName = "TrainAttempt" };
        public static readonly InteractionDef TameAttempt = new() { defName = "TameAttempt" };
    }
}

namespace rjw
{
    public static class SexUtility
    {
        public static void ProcessSex(SexProps props) { }
    }
}
