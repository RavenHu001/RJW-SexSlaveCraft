using System;
using System.Threading.Tasks;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RunDogWorkEntryCases()
    {
        Run("真实训练互动在原动作结束后成长、掷骰及请求事件，尾停取消不返还", TrainingEntrySettlesAfterOriginalAction);
        Run("训练互动拒绝、提前返回、未执行动作及作用域外通知均不发奖", TrainingEntryRequiresInteraction);
        Run("随机训练失败仍按已发生的有效尝试成长", FailedTrainingAttemptStillGrows);
        Run("同一动作重复观察训练互动只结算一次", RepeatedTrainingObservationIsOneReward);
        Run("同 tick 独立训练动作各自成长，事件仍只掷骰一次", IndependentTrainingActionsInSameTick);
        Run("错误发起者、对象或互动定义不冒充本次训练", TrainingObservationMatchesParticipants);
        Run("使用传入目标索引与执行时发起者，并保留动作开始时对象", TrainingEntryCapturesActualParticipants);
        Run("嵌套训练动作分别结算，外层在自身结束后成长", NestedTrainingActionsAreIndependent);
        Run("嵌套动作异常恢复外层，未完成的内层不结算", NestedTrainingExceptionRestoresOuterAction);
        Run("未捕获训练异常不发奖、不泄漏互动，随后新动作正常成长", FailedTrainingActionDoesNotLeak);
        Run("空训练步骤及缺失原动作保持原样", EmptyTrainingToilIsUnchanged);
        Run("普通狗培养完成后只保留工作事件概率，不增加经验", CompletedOrdinaryDogEntryHasNoProgressGain);
        Run("终极狗跨方向及留空只使用自身完成概率，不污染培养历史", FinalDogEntryKeepsProgressIsolated);
        Run("动作执行及结算时重新读取狗方向，不奖励过期资格", TrainingEntryRechecksCurrentDirection);
        Run("驯服互动维持 0.006 基数与原时序，拒绝及其他互动无收益", TamingEntryKeepsExistingContract);
        Run("RJW 行为结算入口维持原收益且拒绝无效组合", AnimalInteractionEntryKeepsExistingContract);
        Run("其他线程互动不能冒充当前同步训练动作", TrainingObservationsStayOnCallingThread);
    }

    // 显式执行生产 Postfix 和它包裹的原动作；不模拟 Harmony 自动补丁或原版成功率算法。
    private static (Pawn handler, Pawn animal) DogWorkPair(float progress = .2f)
    {
        Pawn handler = Pawn(Pets[1], progress), animal = Pawn();
        animal.RaceProps.Animal = true;
        animal.Map = handler.Map;
        return (handler, animal);
    }

    private static Toil WrappedTrainingToil(Pawn handler, Pawn animal, Action action, TargetIndex index = TargetIndex.A)
    {
        handler.jobs.curJob = new Job { target = index == TargetIndex.A ? animal : null, targetB = index == TargetIndex.B ? animal : null };
        var toil = new Toil { actor = handler, initAction = action, defaultCompleteMode = ToilCompleteMode.Delay, duration = 100 };
        Patch_DogSpecialization_AnimalTraining.Postfix(toil, index);
        return toil;
    }

    private static void ObserveTrain(Pawn handler, Pawn animal, bool occurred = true) =>
        Patch_DogSpecialization_AnimalHandling.Postfix(handler, animal, InteractionDefOf.TrainAttempt, occurred);

    private static void AssertUnrewarded(Pawn handler, float progress = .2f)
    {
        Equal(progress, handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 0 && SSCRestrictionJobGuard.PrepareCalls == 0 &&
            handler.Training.lastDogAnimalInteractionTick == -999999, "unperformed work consumed reward or event cooldown");
    }

    private static void TrainingEntrySettlesAfterOriginalAction()
    {
        var p = DogWorkPair();
        Rand.ChanceResult = true;
        bool originalReturned = false;
        var toil = WrappedTrainingToil(p.handler, p.animal, () =>
        {
            ObserveTrain(p.handler, p.animal);
            // 对应互动后仍由原动作执行的随机训练判定及反馈；本次收益不能提前改变它们。
            AssertUnrewarded(p.handler);
            originalReturned = true;
        });
        toil.initAction();
        Assert(originalReturned && toil.defaultCompleteMode == ToilCompleteMode.Delay && toil.duration == 100,
            "original action or tail delay changed");
        Equal(.2044f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1 && SSCRestrictionJobGuard.PrepareCalls == 1, "completed work missed reward/event request");
        Equal(.06f + .2044f * .08f, Rand.Chances[0]);
        Assert(p.handler.Training.lastDogAnimalInteractionTick == Find.TickManager.TicksGame, "completed work missed event cooldown");
        // 模拟剩余尾停未执行：真实互动已结束，不撤销先前收益。
        p.handler.jobs.curJob = null;
        Equal(.2044f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1 && SSCRestrictionJobGuard.PrepareCalls == 1, "tail cancellation altered completed reward");
    }

    private static void TrainingEntryRequiresInteraction()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            Reset(); var p = DogWorkPair();
            var toil = WrappedTrainingToil(p.handler, p.animal, () =>
            {
                if (mode == 0) ObserveTrain(p.handler, p.animal, false);
                // mode 1 为原动作未发出互动便返回，mode 2 为执行前取消。
            });
            if (mode < 2) toil.initAction();
            if (mode == 2) p.handler.jobs.curJob = null;
            if (mode == 3) ObserveTrain(p.handler, p.animal);
            AssertUnrewarded(p.handler);
        }
    }

    private static void FailedTrainingAttemptStillGrows()
    {
        var p = DogWorkPair();
        bool nativeTrainingSucceeded = true;
        WrappedTrainingToil(p.handler, p.animal, () =>
        {
            ObserveTrain(p.handler, p.animal);
            AssertUnrewarded(p.handler);
            nativeTrainingSucceeded = false;
        }).initAction();
        Assert(!nativeTrainingSucceeded, "host did not supply random training failure");
        Equal(.2044f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "failed but occurred interaction did not count as valid work");
    }

    private static void RepeatedTrainingObservationIsOneReward()
    {
        var p = DogWorkPair();
        WrappedTrainingToil(p.handler, p.animal, () =>
        {
            ObserveTrain(p.handler, p.animal);
            ObserveTrain(p.handler, p.animal, false);
            ObserveTrain(p.handler, p.animal);
        }).initAction();
        Equal(.2044f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "repeat observation duplicated event roll");
    }

    private static void IndependentTrainingActionsInSameTick()
    {
        var p = DogWorkPair();
        WrappedTrainingToil(p.handler, p.animal, () => ObserveTrain(p.handler, p.animal)).initAction();
        float first = p.handler.Training.specializationProgress;
        WrappedTrainingToil(p.handler, p.animal, () => ObserveTrain(p.handler, p.animal)).initAction();
        Equal(first + .004f * (1 + first * .5f), p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "independent work bypassed existing daily event cooldown");
    }

    private static void TrainingObservationMatchesParticipants()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            Reset(); var p = DogWorkPair(); var wrong = DogWorkPair();
            WrappedTrainingToil(p.handler, p.animal, () =>
            {
                if (mode == 0) ObserveTrain(wrong.handler, p.animal);
                if (mode == 1) ObserveTrain(p.handler, wrong.animal);
                if (mode == 2) Patch_DogSpecialization_AnimalHandling.Postfix(p.handler, p.animal,
                    new InteractionDef { defName = "TrainAttempt" }, true);
            }).initAction();
            AssertUnrewarded(p.handler); Equal(.2f, wrong.handler.Training.specializationProgress);
        }
    }

    private static void TrainingEntryCapturesActualParticipants()
    {
        var initial = DogWorkPair(); var actual = DogWorkPair();
        var toil = WrappedTrainingToil(initial.handler, initial.animal, () =>
        {
            ObserveTrain(actual.handler, actual.animal);
            actual.handler.jobs.curJob = new Job { targetB = initial.animal };
        }, TargetIndex.B);
        toil.actor = actual.handler;
        actual.handler.jobs.curJob = new Job { target = initial.animal, targetB = actual.animal };
        toil.initAction();
        Equal(.2f, initial.handler.Training.specializationProgress);
        Equal(.2044f, actual.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "wrong actor or index used for completed work");
    }

    private static void NestedTrainingActionsAreIndependent()
    {
        var outer = DogWorkPair(); var inner = DogWorkPair();
        var innerToil = WrappedTrainingToil(inner.handler, inner.animal, () => ObserveTrain(inner.handler, inner.animal));
        WrappedTrainingToil(outer.handler, outer.animal, () =>
        {
            ObserveTrain(outer.handler, outer.animal);
            innerToil.initAction();
            Equal(.2044f, inner.handler.Training.specializationProgress);
            Equal(.2f, outer.handler.Training.specializationProgress);
            ObserveTrain(outer.handler, outer.animal);
        }).initAction();
        Equal(.2044f, outer.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 2, "nested independent actors did not each complete one work reward");
    }

    private static void NestedTrainingExceptionRestoresOuterAction()
    {
        var outer = DogWorkPair(); var inner = DogWorkPair();
        var innerToil = WrappedTrainingToil(inner.handler, inner.animal, () =>
        {
            ObserveTrain(inner.handler, inner.animal);
            throw new InvalidOperationException("host inner action failure");
        });
        WrappedTrainingToil(outer.handler, outer.animal, () =>
        {
            try { innerToil.initAction(); } catch (InvalidOperationException) { }
            ObserveTrain(outer.handler, outer.animal);
        }).initAction();
        Equal(.2f, inner.handler.Training.specializationProgress);
        Equal(.2044f, outer.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1 && inner.handler.Training.lastDogAnimalInteractionTick == -999999,
            "failed inner action rewarded or damaged outer scope");
    }

    private static void FailedTrainingActionDoesNotLeak()
    {
        var p = DogWorkPair();
        var toil = WrappedTrainingToil(p.handler, p.animal, () =>
        {
            ObserveTrain(p.handler, p.animal);
            throw new InvalidOperationException("host action failure");
        });
        bool threw = false;
        try { toil.initAction(); } catch (InvalidOperationException) { threw = true; }
        Assert(threw, "wrapper swallowed original failure");
        ObserveTrain(p.handler, p.animal);
        WrappedTrainingToil(p.handler, p.animal, () => { }).initAction();
        AssertUnrewarded(p.handler);
        WrappedTrainingToil(p.handler, p.animal, () => ObserveTrain(p.handler, p.animal)).initAction();
        Equal(.2044f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "subsequent normal action cannot complete or used leaked observation");
    }

    private static void EmptyTrainingToilIsUnchanged()
    {
        Patch_DogSpecialization_AnimalTraining.Postfix(null, TargetIndex.A);
        var toil = new Toil { defaultCompleteMode = ToilCompleteMode.Delay, duration = 100 };
        Patch_DogSpecialization_AnimalTraining.Postfix(toil, TargetIndex.A);
        Assert(toil.initAction == null && toil.duration == 100 && toil.defaultCompleteMode == ToilCompleteMode.Delay,
            "empty original action became active work");
    }

    private static void CompletedOrdinaryDogEntryHasNoProgressGain()
    {
        foreach (float completed in new[] { .999f, 1f })
        {
            Reset(); var p = DogWorkPair(completed);
            WrappedTrainingToil(p.handler, p.animal, () => ObserveTrain(p.handler, p.animal)).initAction();
            Equal(completed, p.handler.Training.specializationProgress);
            Assert(Rand.Chances.Count == 1, "completed ordinary dog lost work event");
            Equal(.06f + completed * .08f, Rand.Chances[0]);
        }
    }

    private static void FinalDogEntryKeepsProgressIsolated()
    {
        foreach (var direction in new[] { Pets[1], SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.Combatant })
        {
            Reset(); var p = DogWorkPair(.7f); Final(p.handler, Pets[1]);
            p.handler.Training.SetSpecialization(direction);
            p.handler.Training.specializationProgress = .3f;
            var snapshot = new Snapshot(p.handler);
            WrappedTrainingToil(p.handler, p.animal, () => ObserveTrain(p.handler, p.animal)).initAction();
            snapshot.Unchanged(p.handler);
            Assert(Rand.Chances.Count == 1, "final dog entry lost event"); Equal(.18f, Rand.Chances[0]);
        }
    }

    private static void TrainingEntryRechecksCurrentDirection()
    {
        foreach (bool changeAfterInteraction in new[] { false, true })
        {
            Reset(); var p = DogWorkPair();
            var toil = WrappedTrainingToil(p.handler, p.animal, () =>
            {
                ObserveTrain(p.handler, p.animal);
                if (changeAfterInteraction) p.handler.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            });
            if (!changeAfterInteraction) p.handler.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            toil.initAction();
            Equal(0, p.handler.Training.specializationProgress);
            Equal(.2f, p.handler.Training.ExportSpecializationProgress()[Pets[1].ToString()]);
            Assert(Rand.Chances.Count == 0 && p.handler.Training.lastDogAnimalInteractionTick == -999999,
                "expired dog direction gained work or consumed event roll");
        }
    }

    private static void TamingEntryKeepsExistingContract()
    {
        var p = DogWorkPair();
        Patch_DogSpecialization_AnimalHandling.Postfix(p.handler, p.animal, InteractionDefOf.TameAttempt, false);
        Patch_DogSpecialization_AnimalHandling.Postfix(p.handler, p.animal, new InteractionDef(), true);
        AssertUnrewarded(p.handler);
        Patch_DogSpecialization_AnimalHandling.Postfix(p.handler, p.animal, InteractionDefOf.TameAttempt, true);
        Equal(.2066f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "valid tame attempt missed old event timing");
        Equal(.06f + .2066f * .08f, Rand.Chances[0]);
        ObserveTrain(p.handler, p.animal);
        Equal(.2066f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 1, "standalone TrainAttempt claimed tame reward");
    }

    private static void AnimalInteractionEntryKeepsExistingContract()
    {
        var p = DogWorkPair();
        Patch_DogSpecialization_AnimalInteractionProgress.Postfix(new rjw.SexProps { pawn = p.handler, partner = p.animal });
        Equal(.222f, p.handler.Training.specializationProgress);
        Assert(Rand.Chances.Count == 0, "behavior settlement incorrectly rolled work event");
        Patch_DogSpecialization_AnimalInteractionProgress.Postfix(null);
        Patch_DogSpecialization_AnimalInteractionProgress.Postfix(new rjw.SexProps { pawn = p.handler, partner = Pawn() });
        Equal(.222f, p.handler.Training.specializationProgress);
    }

    private static void TrainingObservationsStayOnCallingThread()
    {
        var p = DogWorkPair();
        WrappedTrainingToil(p.handler, p.animal, () =>
            Task.Run(() => ObserveTrain(p.handler, p.animal)).GetAwaiter().GetResult()).initAction();
        AssertUnrewarded(p.handler);
        WrappedTrainingToil(p.handler, p.animal, () => ObserveTrain(p.handler, p.animal)).initAction();
        Equal(.2044f, p.handler.Training.specializationProgress);
    }
}
