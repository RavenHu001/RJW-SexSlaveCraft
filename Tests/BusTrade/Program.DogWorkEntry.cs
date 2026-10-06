using System;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using rjw;

internal static partial class Program
{
    /// <summary>链接真实工厂包装、工作成长、事件调度与许可核心，核对它们的执行顺序。</summary>
    private static void RunDogWorkEntryTests()
    {
        Run("狗训练事件仅在原版随机结算动作返回后调度并登记来源", () =>
        {
            var p = DogPair(true);
            Job training = SetAnimalWork(p.dog, p.animal);
            bool nativeActionCompleted = false;
            bool nativeTrainingSucceeded = true;
            p.dog.jobs.OnRequest = job =>
            {
                Assert(nativeActionCompleted && !nativeTrainingSucceeded, "随机训练失败也必须先完成原动作再调度事件");
                Assert(p.dog.CurJob == training, "原训练工作在事件 StartJob 前应仍被保留");
                Assert(PetSpecializationUtility.Gains.Count == 1 && Math.Abs(PetSpecializationUtility.Gains[0] - 0.004f) < 0.000001f,
                    "StartJob 前应先结算一次既有 0.004 工作成长");
                Assert(SSCRestrictionJobGuard.Events.TryGetValue(job, out var entry) &&
                    entry.Actor == p.dog && entry.Target == p.animal && entry.Source == SSCRestrictionEvent.Dog,
                    "事件来源应在直接启动前绑定至实际任务和参与者");
                return true;
            };
            Toil toil = WrapAnimalTraining(p.dog, () =>
            {
                ObserveInteraction(p.dog, p.animal, InteractionDefOf.TrainAttempt, true);
                AssertNoHandlingEffects(p.dog, training);
                // 原版互动成功发生后还会随机决定训练结果；模拟失败确保工作奖励不依赖随机成功。
                nativeTrainingSucceeded = false;
                AssertNoHandlingEffects(p.dog, training);
                nativeActionCompleted = true;
            });
            toil.initAction();
            Assert(nativeActionCompleted && Pawn_JobTracker.Requests.Count == 1 &&
                p.dog.CurJob?.def == xxx.bestialityForFemale, "动作收尾后应直接启动一次允许的床上事件");
            Assert(Rand.ChanceCalls == 1 && p.dog.Training.lastDogAnimalInteractionTick == Find.TickManager.TicksGame,
                "一次实际工作仅抽签一次并消耗既有冷却");
            Assert(Messages.Entries.Count == 0, "事件调度不冒充真实场景开始提示");
        });
        Run("狗训练事件许可拒绝保留原工作及工作成长和抽签冷却", () =>
        {
            var p = DogPair(true);
            Job training = SetAnimalWork(p.dog, p.animal);
            p.dog.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            Toil toil = WrapAnimalTraining(p.dog, () =>
            {
                ObserveInteraction(p.dog, p.animal, InteractionDefOf.TrainAttempt, true);
                AssertNoHandlingEffects(p.dog, training);
            });
            toil.initAction();
            Assert(p.dog.CurJob == training && Pawn_JobTracker.Requests.Count == 0 && SSCRestrictionJobGuard.Events.Count == 0,
                "许可拒绝不分配事件任务、不留下来源登记，也不打断训练工作");
            Assert(PetSpecializationUtility.Gains.Count == 1 && Math.Abs(PetSpecializationUtility.Gains[0] - 0.004f) < 0.000001f,
                "拒绝仅取消事件，应保留一次普通狗 0.004 工作成长");
            Assert(Rand.ChanceCalls == 1 && p.dog.Training.lastDogAnimalInteractionTick == Find.TickManager.TicksGame,
                "拒绝不退还抽签冷却");
            // 同一天的下一次实际工作仍成长；每日低频事件不得因许可拒绝而重新抽签。
            WrapAnimalTraining(p.dog, () => ObserveInteraction(p.dog, p.animal, InteractionDefOf.TrainAttempt, true)).initAction();
            Assert(PetSpecializationUtility.Gains.Count == 2 && Rand.ChanceCalls == 1 && p.dog.CurJob == training,
                "第二次工作仍结算但不重新抽签或改派工作");
        });
        Run("狗训练互动拒绝或原动作提前返回均不成长抽签或打断工作", () =>
        {
            foreach (bool callInteraction in new[] { true, false })
            {
                Reset();
                var p = DogPair(true);
                Job training = SetAnimalWork(p.dog, p.animal);
                Toil toil = WrapAnimalTraining(p.dog, () =>
                {
                    if (callInteraction)
                        ObserveInteraction(p.dog, p.animal, InteractionDefOf.TrainAttempt, false);
                });
                toil.initAction();
                AssertNoHandlingEffects(p.dog, training);
                Assert(SSCRestrictionJobGuard.Events.Count == 0 && Messages.Entries.Count == 0,
                    "互动未发生不登记事件或提示");
            }
        });
        Run("狗既有驯服互动入口保留普通 0.006 成长与一次抽签", () =>
        {
            var p = DogPair(true);
            Job taming = SetAnimalWork(p.dog, p.animal);
            p.dog.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            ObserveInteraction(p.dog, p.animal, InteractionDefOf.TameAttempt, true);
            Assert(PetSpecializationUtility.Gains.Count == 1 && Math.Abs(PetSpecializationUtility.Gains[0] - 0.006f) < 0.000001f,
                "实际驯服尝试仍使用既有 0.006 成长");
            Assert(Rand.ChanceCalls == 1 && p.dog.CurJob == taming && Pawn_JobTracker.Requests.Count == 0,
                "驯服入口保留原有抽签与许可拒绝规则");
        });
    }

    /// <summary>创建持有真实动物目标的当前工作，使生产包装捕获目标而非直接注入参与者。</summary>
    private static Job SetAnimalWork(Pawn handler, Pawn animal)
    {
        Job work = new Job { def = new JobDef { defName = "AnimalWork" }, targetA = animal };
        handler.jobs.curJob = work;
        return work;
    }

    /// <summary>显式调用生产工厂后缀执行真实包装；此套件不声称安装了 Harmony 补丁。</summary>
    private static Toil WrapAnimalTraining(Pawn handler, Action originalAction)
    {
        Toil toil = Toils_Interpersonal.TryTrain(TargetIndex.A);
        toil.actor = handler;
        toil.initAction = originalAction;
        Patch_DogSpecialization_AnimalTraining.Postfix(toil, TargetIndex.A);
        return toil;
    }

    /// <summary>让宿主返回真实 bool 边界并显式交给生产互动后缀；不直接调用内部 scope 辅助方法。</summary>
    private static void ObserveInteraction(Pawn handler, Pawn animal, InteractionDef definition, bool occurred)
    {
        var interactions = new Pawn_InteractionsTracker { Result = occurred };
        bool result = interactions.TryInteractWith(animal, definition);
        Patch_DogSpecialization_AnimalHandling.Postfix(handler, animal, definition, result);
    }

    /// <summary>断言原动作中尚未提前发奖、抽签、登记或调度，原训练工作仍完整保留。</summary>
    private static void AssertNoHandlingEffects(Pawn handler, Job work)
    {
        Assert(PetSpecializationUtility.Gains.Count == 0 && Rand.ChanceCalls == 0 && Pawn_JobTracker.Requests.Count == 0,
            "训练原动作未完成或互动未发生时不得成长、抽签或 StartJob");
        Assert(SSCRestrictionJobGuard.Events.Count == 0 && handler.CurJob == work && handler.Training.lastDogAnimalInteractionTick == 0,
            "原工作与冷却应在未结算前保留");
    }
}
