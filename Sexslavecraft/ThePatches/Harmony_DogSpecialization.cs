using System;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    // 原版训练步骤也会在睡眠或互动拒绝时正常返回，不能只凭任务走到末尾发奖。
    // 在原版同步动作内记录实际 TrainAttempt，并等成功率判定及反馈结束后结算，
    // 避免本次成长的阶段属性影响同次训练，或工作后事件抢占尚未结束的原动作。
    [HarmonyPatch(typeof(Toils_Interpersonal), nameof(Toils_Interpersonal.TryTrain))]
    public static class Patch_DogSpecialization_AnimalTraining
    {
        private sealed class TrainingAttemptScope
        {
            public Pawn Handler;
            public Pawn Animal;
            public bool InteractionOccurred;
        }

        // 只覆盖一次同步 initAction 的调用栈，不跨 tick 缓存，不增加任务或存档字段。
        // 嵌套动作各自持有作用域；异常也恢复外层，避免后续动作误认先前的互动。
        [ThreadStatic]
        private static TrainingAttemptScope currentTrainingAttempt;

        public static void Postfix(Toil __result, TargetIndex traineeInd)
        {
            if (__result?.initAction == null) return;
            Action originalAction = __result.initAction;
            __result.initAction = delegate
            {
                Pawn handler = __result.actor;
                TrainingAttemptScope scope = new TrainingAttemptScope
                {
                    Handler = handler,
                    Animal = handler?.CurJob?.GetTarget(traineeInd).Pawn
                };
                TrainingAttemptScope previous = currentTrainingAttempt;
                currentTrainingAttempt = scope;
                try
                {
                    originalAction();
                }
                finally
                {
                    currentTrainingAttempt = previous;
                }

                // 正常返回且互动确实发生才认领一次；训练随机失败仍属于有效尝试。
                // 使用动作开始时的参与者，不借用后续事件或第三方改换后的当前任务目标。
                if (scope.InteractionOccurred)
                    DogSpecializationUtility.NotifyAnimalTrainingCompleted(scope.Handler, scope.Animal);
            };
        }

        internal static void ObserveTrainingAttempt(Pawn handler, Pawn animal)
        {
            TrainingAttemptScope scope = currentTrainingAttempt;
            if (scope != null && handler != null && animal != null &&
                scope.Handler == handler && scope.Animal == animal)
                scope.InteractionOccurred = true;
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Patch_DogSpecialization_AnimalHandling
    {
        public static void Postfix(Pawn ___pawn, Pawn recipient, InteractionDef intDef, bool __result)
        {
            if (!__result) return;

            // Harmony 按字段名注入真实互动发起者；bool 表示互动发生，不是动物训练成功率。
            // 训练只记录，由原动作收尾发奖；驯服保留既有互动结束后的结算时序与收益。
            if (intDef == InteractionDefOf.TrainAttempt)
                Patch_DogSpecialization_AnimalTraining.ObserveTrainingAttempt(___pawn, recipient);
            else if (intDef == InteractionDefOf.TameAttempt)
                DogSpecializationUtility.NotifyAnimalTamingAttempted(___pawn, recipient);
        }
    }

    [HarmonyPatch(typeof(SexUtility), nameof(SexUtility.ProcessSex))]
    public static class Patch_DogSpecialization_AnimalInteractionProgress
    {
        public static void Postfix(SexProps props)
        {
            DogSpecializationUtility.NotifyAnimalInteractionProcessed(props);
        }
    }
}
