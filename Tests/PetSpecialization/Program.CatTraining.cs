using System;
using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>猫路线只通过实际选择、共享培养和亲昵完成入口验证，不重写生产资格或奖励逻辑。</summary>
    private static void RunCatTrainingCases()
    {
        Run("猫玩家入口无需绑定即可选择，显示标记及重复选择不赠送经验", CatPlayerSelection);
        Run("猫玩家入口检查SSC身份、基础和猫研究，缺失定义或组件不改状态", CatPlayerRequirements);
        Run("猫玩家入口拒绝全部宠物终极组合并保留原始状态", CatPlayerFinalMatrix);
        Run("猫玩家提交复查身份、两项研究、各宠物终极及真实组件", CatPlayerSubmissionRechecks);
        Run("猫亲昵实际驱动仅成功结束时给一次经验，调度和等待均不发奖", CatAffectionDriverCompletion);
        Run("猫亲昵固定奖励1个百分点，完成容差及上限保留原有心情效果", CatAffectionClamping);
        Run("猫亲昵同步真实进度，0.01显示严重度不冒充经验，普通完成标记不再领奖", CatAffectionMarkerProgress);
        Run("猫亲昵满一天才可再次结算，重复回调不重发记忆或经验", CatAffectionCooldown);
        Run("猫亲昵记忆回调重入只领奖一次，记忆定义或容器缺失仍可正常完成培养", CatAffectionMemoryBoundaries);
        Run("猫亲昵达到上限后次日仍有亲昵心情，但不继续增加经验", CatAffectionAfterCompletion);
        Run("猫亲昵中断或物理和绑定条件失败时不发经验、记忆或消耗冷却", CatAffectionFailures);
        Run("亲昵按成功完成时当前方向结算，跨方向和历史猫进度不误发奖", CatAffectionCurrentDirection);
        Run("任意宠物终极阻止猫亲昵经验，终极转练不污染当前方向或各历史", CatAffectionFinalIsolation);
        Run("主人或未指定身份的旧猫保持亲昵效果但不能领取特色经验", CatAffectionIdentity);
        Run("猫沿用共享日常及仪式分数公式，既有培养不重复检查选择研究", CatSharedTraining);
    }

    /// <summary>绑定只限制亲昵；方向选择本身依照已完成研究与SSC身份，不要求已有主人。</summary>
    private static void CatPlayerSelection()
    {
        var cat = Pawn();
        Assert(cat.BoundMaster == null && cat.AssignedTrainer == null, "test must start without any master");
        Assert(PetSpecializationUtility.CanSelectPetSpecialization(cat, Pets[0], out var failure), "cat menu rejected");
        Failure(PetSpecializationFailure.None, failure);
        Assert(PetSpecializationUtility.TrySelectPetSpecialization(cat, cat.Training, Pets[0], out failure), "cat submission rejected");
        Failure(PetSpecializationFailure.None, failure);
        Assert(cat.Training.specializationType == Pets[0], "cat direction not selected"); Equal(0, cat.Training.specializationProgress);
        Equal(.01f, cat.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(Pets[0])).Severity);
        Assert(PetSpecializationUtility.TrySelectPetSpecialization(cat, cat.Training, Pets[0], out _), "repeat cat selection rejected");
        Equal(0, cat.Training.specializationProgress);
        // 选择通知沿用已有流程；这里只检查重复同步没有额外健康状态或经验。
        PetSpecializationUtility.SyncPetStates(cat); PetSpecializationUtility.SyncPetStates(cat);
        Assert(cat.health.hediffSet.hediffs.Count == 1, "repeat selection created duplicate state");
        Equal(.01f, cat.health.hediffSet.hediffs.Single().Severity);
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(cat), "unbound cat started affection");
    }

    /// <summary>穷举未完成研究与身份，拒绝的选择不导入旧标记、不改历史或通知。</summary>
    private static void CatPlayerRequirements()
    {
        foreach (var identity in Enum.GetValues<PawnIdentity>())
        for (int completed = 0; completed < 4; completed++)
        {
            Reset(); var cat = Pawn(SexSlaveSpecializationType.Cow, .42f); cat.Training.pawnIdentity = identity;
            SSCDefOf.SSC_BasicTraining.IsFinished = (completed & 1) != 0;
            DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SSC_RES_PetCat").IsFinished = (completed & 2) != 0;
            var snapshot = new Snapshot(cat);
            bool expected = identity == PawnIdentity.Slave && completed == 3;
            Assert(PetSpecializationUtility.TrySelectPetSpecialization(cat, cat.Training, Pets[0], out var failure) == expected,
                $"cat requirements {identity}/{completed}");
            if (expected) Failure(PetSpecializationFailure.None, failure);
            else
            {
                Failure(identity != PawnIdentity.Slave ? PetSpecializationFailure.MissingRequirements : PetSpecializationFailure.ResearchRequired, failure);
                snapshot.Unchanged(cat);
            }
        }
        Reset(); var missing = Pawn(SexSlaveSpecializationType.Cow, .42f); var before = new Snapshot(missing);
        DefDatabase<ResearchProjectDef>.Definitions.Remove("SSC_RES_PetCat");
        Assert(!PetSpecializationUtility.TrySelectPetSpecialization(missing, missing.Training, Pets[0], out var missingFailure), "missing cat research accepted");
        Failure(PetSpecializationFailure.MissingRequirements, missingFailure); before.Unchanged(missing);
        Assert(!PetSpecializationUtility.CanSelectPetSpecialization(null, Pets[0], out _), "null cat accepted");
        Assert(!PetSpecializationUtility.CanSelectPetSpecialization(new Pawn(), Pets[0], out _), "cat without comp accepted");
    }

    /// <summary>同种终极已结束普通培养，跨种或多终极冲突同样拒绝玩家选择。</summary>
    private static void CatPlayerFinalMatrix()
    {
        for (int mask = 1; mask < 8; mask++)
        {
            var cat = Pawn(SexSlaveSpecializationType.Cow, .44f);
            for (int index = 0; index < Pets.Length; index++) if ((mask & (1 << index)) != 0) Final(cat, Pets[index]);
            var snapshot = new Snapshot(cat);
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(cat, cat.Training, Pets[0], out var failure), "cat final matrix accepted " + mask);
            Failure(mask == 1 ? PetSpecializationFailure.AlreadyFinalized : PetSpecializationFailure.ConflictingFinal, failure);
            snapshot.Unchanged(cat);
        }
    }

    /// <summary>模拟菜单展示后状态改变，实际提交必须重新读事实，而非复用菜单创建时的结果。</summary>
    private static void CatPlayerSubmissionRechecks()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            Reset(); var cat = Pawn(SexSlaveSpecializationType.Cow, .36f); var comp = cat.Training;
            Assert(PetSpecializationUtility.CanSelectPetSpecialization(cat, Pets[0], out _), "cat initially unavailable");
            if (scenario == 0) cat.Training.pawnIdentity = PawnIdentity.Master;
            if (scenario == 1) SSCDefOf.SSC_BasicTraining.IsFinished = false;
            if (scenario == 2) DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SSC_RES_PetCat").IsFinished = false;
            if (scenario >= 3 && scenario <= 5) Final(cat, Pets[scenario - 3]);
            if (scenario == 6) cat.Training = new CompSexSlaveTraining { parent = cat, pawnIdentity = PawnIdentity.Slave };
            var snapshot = new Snapshot(cat);
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(cat, comp, Pets[0], out _), "changed cat qualifications accepted " + scenario);
            snapshot.Unchanged(cat);
        }
    }

    /// <summary>真实亲昵驱动的等待与面向主人不奖励；完成动作更新普通进度与当前猫标记。</summary>
    private static void CatAffectionDriverCompletion()
    {
        var p = AffectionPair(Pets[0], 0);
        PetSpecializationUtility.EnsurePetHediffFromSpecialization(p.pet);
        Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "cat affection did not schedule");
        Equal(0, p.pet.Training.specializationProgress);
        var driver = new JobDriver_PetAffection { pawn = p.pet, job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, p.master) };
        var toils = driver.BuildToils().ToArray();
        Assert(toils.Length == 3 && toils[1].duration == 120 && !driver.FailureConditions.Any(f => f()), "cat driver invalid");
        BeginAffection(driver, toils, p.master);
        toils[1].tickAction(); Equal(0, p.pet.Training.specializationProgress);
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 0, "waiting generated memory");
        toils[2].initAction();
        Equal(.01f, p.pet.Training.specializationProgress);
        Equal(.01f, p.pet.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(Pets[0])).Severity);
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "completion missing memory");
        Assert(p.pet.Training.lastPetAffectionTick == Find.TickManager.TicksGame, "completion missing cooldown");
        toils[2].initAction(); Equal(.01f, p.pet.Training.specializationProgress);
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "repeat driver callback generated memory");
    }

    /// <summary>奖励固定为1个百分点，接近完成时按剩余空间裁剪；普通完成仍保留亲昵。</summary>
    private static void CatAffectionClamping()
    {
        foreach (float progress in new[] { 0, .01f, .195f, .495f, .98f, .989f, .995f, .998f, .999f, 1 })
        {
            var p = AffectionPair(Pets[0], progress); int beforeMessages = Messages.Calls;
            Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "cat completion denied " + progress);
            float expected = progress >= .999f ? progress : Math.Min(1, progress + .01f);
            Equal(expected, p.pet.Training.specializationProgress);
            Equal(expected, p.pet.Training.ExportSpecializationProgress()[Pets[0].ToString()]);
            Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "completed ordinary cat lost affection memory");
            Assert(Messages.Calls - beforeMessages == (progress < .2f && expected >= .2f ? 1 : 0), "cat threshold notification repeated or missing");
        }
    }

    /// <summary>初始显示值不参加XP；外部更高普通状态沿用导入规则，完成状态不会再次得到特色经验。</summary>
    private static void CatAffectionMarkerProgress()
    {
        foreach (float marker in new[] { .01f, .7f, .999f, 1 })
        {
            var p = AffectionPair(Pets[0], 0);
            p.pet.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Pets[0])).Severity = marker;
            Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "marker cat completion denied");
            float expected = marker == .01f ? .01f : marker >= .999f ? marker : marker + .01f;
            Equal(expected, p.pet.Training.specializationProgress);
            Equal(expected, p.pet.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(Pets[0])).Severity);
            PetSpecializationUtility.SyncPetStates(p.pet); PetSpecializationUtility.SyncPetStates(p.pet);
            Equal(expected, p.pet.Training.specializationProgress);
        }
    }

    /// <summary>直接完成与自动调度使用同一已存档冷却；边界前拒绝，60000tick整时再次结算。</summary>
    private static void CatAffectionCooldown()
    {
        var p = AffectionPair(Pets[0]);
        Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "first cat affection failed"); Equal(.31f, p.pet.Training.specializationProgress);
        var snapshot = new Snapshot(p.pet);
        Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "same-tick cat completion accepted"); snapshot.Unchanged(p.pet);
        Find.TickManager.TicksGame += 59999;
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "cat affection cooldown shortened");
        Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "direct cat completion bypassed cooldown"); snapshot.Unchanged(p.pet);
        Find.TickManager.TicksGame++;
        Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "cat cooldown boundary schedule rejected"); Equal(.31f, p.pet.Training.specializationProgress);
        Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "cat cooldown boundary complete rejected"); Equal(.32f, p.pet.Training.specializationProgress);
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 2, "cat cooldown memory count incorrect");
    }

    /// <summary>记忆只是一项原有副作用；重入必须被已认领冷却阻止，缺失记忆资源不阻止成功动作结算。</summary>
    private static void CatAffectionMemoryBoundaries()
    {
        var p = AffectionPair(Pets[0]); bool acceptedReentry = true;
        p.master.needs.mood.thoughts.memories.OnNextGainMemory = () =>
            acceptedReentry = PetSpecializationUtility.CompletePetAffection(p.pet, p.master);
        Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "outer affection completion denied");
        Assert(!acceptedReentry, "memory reentry accepted duplicate completion"); Equal(.31f, p.pet.Training.specializationProgress);
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "memory reentry duplicated memory");
        for (int scenario = 0; scenario < 5; scenario++)
        {
            var missing = AffectionPair(Pets[0]);
            if (scenario == 0) DefDatabase<ThoughtDef>.Definitions.Remove("SSC_PetAffection_Mood");
            if (scenario == 1) missing.master.needs = null;
            if (scenario == 2) missing.master.needs.mood = null;
            if (scenario == 3) missing.master.needs.mood.thoughts = null;
            if (scenario == 4) missing.master.needs.mood.thoughts.memories = null;
            Assert(PetSpecializationUtility.CompletePetAffection(missing.pet, missing.master), "missing memory blocked valid affection " + scenario);
            Equal(.31f, missing.pet.Training.specializationProgress);
            Assert(missing.pet.Training.lastPetAffectionTick == Find.TickManager.TicksGame, "missing memory lost completion cooldown");
        }
    }

    /// <summary>第一次动作补满剩余空间；完成后冷却到期仍保留亲昵，不重复触发普通培养奖项。</summary>
    private static void CatAffectionAfterCompletion()
    {
        var p = AffectionPair(Pets[0], .998f);
        Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "near-complete cat affection denied"); Equal(1, p.pet.Training.specializationProgress);
        Find.TickManager.TicksGame += 60000; var snapshot = new Snapshot(p.pet);
        Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "completed cat affection denied"); snapshot.Unchanged(p.pet);
        Equal(1, p.pet.Training.specializationProgress);
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 2, "completed cat lost next-day affection memory");
    }

    /// <summary>验证真实驱动注册的失败条件与强行调用完成动作都拒绝失效资格，断言XP和既有副作用。</summary>
    private static void CatAffectionFailures()
    {
        for (int scenario = 0; scenario < 15; scenario++)
        {
            var p = AffectionPair(Pets[0]);
            var driver = new JobDriver_PetAffection { pawn = p.pet, job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, p.master) };
            var toils = driver.BuildToils().ToArray();
            BeginAffection(driver, toils, p.master);
            if (scenario == 0) p.pet.BoundMaster = null;
            if (scenario == 1) { p.pet.BoundMaster = null; p.pet.AssignedTrainer = p.master; }
            if (scenario == 2) p.pet.BoundMaster = Pawn();
            if (scenario == 3) p.pet.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            if (scenario == 4) p.pet.Training.SetSpecialization(SexSlaveSpecializationType.None);
            if (scenario == 5) p.pet.Position = new IntVec3(3, 0);
            if (scenario == 6) p.master.Map = new object();
            if (scenario == 7) p.pet.Spawned = false;
            if (scenario == 8) p.master.Spawned = false;
            if (scenario == 9) p.pet.Downed = true;
            if (scenario == 10) p.master.Downed = true;
            if (scenario == 11) p.pet.Drafted = true;
            if (scenario == 12) p.master.Drafted = true;
            if (scenario == 13) p.pet.Dead = true;
            if (scenario == 14) p.master.Dead = true;
            var snapshot = new Snapshot(p.pet);
            Assert(driver.FailureConditions.Any(f => f()), "invalid cat driver accepted " + scenario);
            toils[2].initAction(); snapshot.Unchanged(p.pet);
            Assert(p.pet.Training.lastPetAffectionTick == -999999 && p.master.needs.mood.thoughts.memories.Requests.Count == 0,
                "failed cat affection produced effects " + scenario);
        }
    }

    /// <summary>共用动作允许猫狗兔互相切换；奖励只读完成时当前普通猫，不读取猫历史或给其他方向加经验。</summary>
    private static void CatAffectionCurrentDirection()
    {
        foreach (var start in Pets)
        foreach (var finish in Pets)
        {
            var p = AffectionPair(start, .63f);
            var driver = new JobDriver_PetAffection { pawn = p.pet, job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, p.master) };
            var toils = driver.BuildToils().ToArray();
            BeginAffection(driver, toils, p.master);
            p.pet.Training.SetSpecialization(finish); p.pet.Training.specializationProgress = .27f;
            var histories = p.pet.Training.ExportSpecializationProgress();
            Assert(!driver.FailureConditions.Any(f => f()), "pet direction switch invalidated common affection");
            toils[2].initAction(); Equal(finish == Pets[0] ? .28f : .27f, p.pet.Training.specializationProgress);
            var after = p.pet.Training.ExportSpecializationProgress();
            foreach (var entry in histories.Where(e => e.Key != finish.ToString())) Equal(entry.Value, after[entry.Key]);
            Assert(p.pet.Training.specializationType == finish && p.master.needs.mood.thoughts.memories.Requests.Count == 1,
                "current direction affection failed");
        }
    }

    /// <summary>任何终极都结束普通猫经验；现有终极照常亲昵，历史与当前方向所有数据只读保留。</summary>
    private static void CatAffectionFinalIsolation()
    {
        for (int mask = 1; mask < 8; mask++)
        foreach (var current in Enum.GetValues<SexSlaveSpecializationType>())
        {
            var p = AffectionPair(Pets[0], .62f);
            p.pet.Training.SetSpecialization(SexSlaveSpecializationType.Cow); p.pet.Training.specializationProgress = .46f;
            p.pet.Training.SetSpecialization(current); p.pet.Training.specializationProgress = .23f;
            for (int index = 0; index < Pets.Length; index++) if ((mask & (1 << index)) != 0) Final(p.pet, Pets[index]);
            var snapshot = new Snapshot(p.pet);
            Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "final cat affection denied " + mask + "/" + current);
            snapshot.Unchanged(p.pet);
            Assert(p.pet.Training.lastPetAffectionTick == Find.TickManager.TicksGame && p.master.needs.mood.thoughts.memories.Requests.Count == 1,
                "final cat lost normal affection effects");
        }
    }

    /// <summary>不收紧旧亲昵资格，但新增XP要求真实SSC性奴身份；失格身份的字段和健康数据不变。</summary>
    private static void CatAffectionIdentity()
    {
        foreach (var identity in new[] { PawnIdentity.Master, PawnIdentity.Unset })
        {
            var p = AffectionPair(Pets[0]); p.pet.Training.pawnIdentity = identity; var snapshot = new Snapshot(p.pet);
            Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "old cat identity lost affection");
            Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "old cat identity completion failed"); snapshot.Unchanged(p.pet);
            Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "old cat identity lost memory");
        }
    }

    /// <summary>猫普通培养使用已有共享公式；本组只验证公共分数入口，不模拟真实仪式调度或去重。</summary>
    private static void CatSharedTraining()
    {
        var p = AffectionPair(Pets[0], 0); var other = Pawn();
        SSCDefOf.SSC_BasicTraining.IsFinished = false;
        DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SSC_RES_PetCat").IsFinished = false;
        Equal(.03f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(p.master, p.pet, 40));
        Equal(.02f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(other, p.pet, 40));
        Equal(.05f, p.pet.Training.specializationProgress);
        float ritualScore = SpecializationTrainingProgressUtility.RitualScore(1);
        float amount = CombatantSpecializationProgressUtility.TrainingProgress(ritualScore, true);
        Equal(amount, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(p.master, p.pet, ritualScore));
        Equal(.05f + amount, p.pet.Training.specializationProgress);
    }
}
