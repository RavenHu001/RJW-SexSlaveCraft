using System;
using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>直接执行生产效果查询、狗工作入口和亲昵驱动；断言可观察结果与数据隔离。</summary>
    private static void RunEffectAndBehaviorCases()
    {
        Run("效果矩阵区分当前普通与独立终极，组外方向不授予宠物效果", EffectMatrix);
        Run("效果和有效进度查询只读，不创建状态或规范化存档字段", EffectQueriesAreReadOnly);
        Run("历史和残留普通标签不授予亲昵或狗工作资格", StaleMarkersHaveNoEffects);
        Run("普通进度安全归一，终极进度独立于当前方向和标签严重度", EffectiveProgress);
        Run("缺失组件、定义、健康及死亡销毁角色的效果查询安全", MissingEffectBoundaries);
        Run("终极清理全部重复普通宠物状态，保留终极、历史及组外状态", FinalStateCleanup);
        Run("普通同步仅保留当前方向且初始显示标记不赠送经验", OrdinaryStateCleanup);
        Run("明确留空及主人提前返回仍清理普通状态，旧档普通认领保留", ReconciliationEarlyReturns);
        Run("恢复方向时不导入宿主普通进度，标签恢复后再正确同步", RestoreDoesNotImportHostProgress);
        Run("终极猫狗兔转练或留空后仍可亲昵且不改变培养进度", FinalAffectionAcrossDirections);
        Run("普通宠物从零进度至完成均保持既有亲昵资格", OrdinaryAffectionAcrossProgress);
        Run("亲昵驱动和完成入口复查方向、终极及实际绑定主人", AffectionRechecksChanges);
        Run("亲昵保留冷却、重复结算保护、空闲调度及真实组件检查", AffectionSchedulingAndCooldown);
        Run("亲昵保持距离、同图、存活、倒地与征召条件", AffectionPhysicalConditions);
        Run("真实狗训练和驯服尝试保留成长公式与完成上限", OrdinaryDogWork);
        Run("终极狗各当前方向和进度下概率稳定，不发普通经验或污染其他方向", FinalDogWork);
        Run("非当前狗、冲突终极、死亡或无效工作对象不计成长或抽签", InvalidDogWork);
    }

    /// <summary>穷举当前方向与成果组合；多终极只查询现有事实，不迁移或选择保留对象。</summary>
    private static void EffectMatrix()
    {
        var types = Enum.GetValues<SexSlaveSpecializationType>().Append((SexSlaveSpecializationType)999).ToArray();
        for (int mask = 0; mask < 8; mask++)
        foreach (var current in types)
        foreach (var target in types)
        {
            int index = Array.IndexOf(Pets, target);
            bool expected = index >= 0 && ((mask & (1 << index)) != 0 || (mask == 0 && current == target));
            Assert(PetSpecializationRules.HasEffects(target, current, (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0) == expected,
                $"effect matrix {mask}/{current}/{target}");
        }
    }

    /// <summary>用原有深度快照验证查询不会写字段、历史、健康状态或通知。</summary>
    private static void EffectQueriesAreReadOnly()
    {
        foreach (var current in Enum.GetValues<SexSlaveSpecializationType>())
        foreach (float progress in new[] { .01f, .5f, 1f, -1f, 2f, float.NaN, float.PositiveInfinity })
        {
            var pawn = Pawn(current, progress);
            pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Pets[1])).Severity = .8f;
            var snapshot = new Snapshot(pawn);
            foreach (var type in Pets)
            {
                PetSpecializationUtility.HasActivePetEffects(pawn, type);
                PetSpecializationUtility.GetEffectivePetProgress(pawn, type);
            }
            PetSpecializationUtility.HasPetAffectionQualification(pawn);
            DogSpecializationUtility.IsDogSpecialized(pawn);
            snapshot.Unchanged(pawn);
        }
    }

    /// <summary>保留原始标签事实查询供迁移使用，但该标签不能绕过当前方向的行为门槛。</summary>
    private static void StaleMarkersHaveNoEffects()
    {
        foreach (var current in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow, Pets[0] })
        {
            var pawn = Pawn(Pets[1], .8f); pawn.Training.SetSpecialization(current);
            pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Pets[1])).Severity = .9f;
            Assert(PetSpecializationUtility.HasAnyPetState(pawn, Pets[1]), "raw state query changed");
            Assert(!DogSpecializationUtility.IsDogSpecialized(pawn), "stale dog marker grants effects");
            Equal(0, PetSpecializationUtility.GetEffectivePetProgress(pawn, Pets[1]));
            Assert(PetSpecializationUtility.HasPetAffectionQualification(pawn) == (current == Pets[0]), "history grants affection");
        }
    }

    /// <summary>效果读数安全归一且不反写；终极状态与其他培养进度、严重度解耦。</summary>
    private static void EffectiveProgress()
    {
        foreach (var type in Pets)
        {
            foreach (float progress in new[] { -.2f, 0, .01f, .999f, 1, 3, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                var pawn = Pawn(type, progress);
                float expected = float.IsNaN(progress) || float.IsInfinity(progress) ? 0 : Math.Clamp(progress, 0, 1);
                Equal(expected, PetSpecializationUtility.GetEffectivePetProgress(pawn, type));
                Assert(progress.Equals(pawn.Training.specializationProgress), "query normalized source field");
                Final(pawn, type).Severity = .01f;
                Equal(1, PetSpecializationUtility.GetEffectivePetProgress(pawn, type));
            }
            var switched = Pawn(type, .9f); Final(switched, type);
            switched.Training.SetSpecialization(SexSlaveSpecializationType.Cow); switched.Training.specializationProgress = .13f;
            Equal(1, PetSpecializationUtility.GetEffectivePetProgress(switched, type)); Equal(.13f, switched.Training.specializationProgress);
        }
    }

    /// <summary>资格不会依赖错误组件或缺失定义；无组件终极保留效果事实但不能启动组件驱动的亲昵。</summary>
    private static void MissingEffectBoundaries()
    {
        Assert(!PetSpecializationUtility.HasActivePetEffects(null, Pets[1]), "null has effects");
        foreach (int state in Enumerable.Range(0, 4))
        {
            var pawn = Pawn(Pets[1], .5f); Final(pawn, Pets[1]);
            if (state == 0) pawn.health = null;
            if (state == 1) pawn.Dead = true;
            if (state == 2) pawn.Destroyed = true;
            if (state == 3) pawn.Training = null;
            Assert(PetSpecializationUtility.HasActivePetEffects(pawn, Pets[1]) == (state == 3), "wrong boundary qualification");
            Assert(!PetSpecializationUtility.HasPetAffectionQualification(pawn), "invalid affection host accepted");
            Equal(state == 3 ? 1 : 0, PetSpecializationUtility.GetEffectivePetProgress(pawn, Pets[1]));
        }
        var missing = Pawn(SexSlaveSpecializationType.Cow); Final(missing, Pets[1]);
        DefDatabase<HediffDef>.Definitions.Remove("SSC_Hediff_PetDog_Final");
        Assert(!PetSpecializationUtility.HasActivePetEffects(missing, Pets[1]), "missing final definition grants effects");
        PetSpecializationUtility.SyncPetStates(missing);
    }

    /// <summary>消除普通与终极属性叠加，终极及组外状态原样保留，培养历史不被删除。</summary>
    private static void FinalStateCleanup()
    {
        foreach (var finalType in Pets)
        {
            var pawn = Pawn(Pets[1], .7f); var final = Final(pawn, finalType);
            var unrelated = pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Cow_Final);
            foreach (var type in Pets)
            for (int duplicate = 0; duplicate < 2; duplicate++) pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(type)).Severity = .8f;
            var history = pawn.Training.ExportSpecializationProgress();
            PetSpecializationUtility.SyncPetStates(pawn);
            Assert(Pets.All(t => !pawn.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(t))), "ordinary pet duplicate remains");
            Assert(pawn.health.hediffSet.hediffs.SequenceEqual(new[] { final, unrelated }), "final or unrelated marker changed");
            Equal(.7f, pawn.Training.specializationProgress);
            Assert(history.OrderBy(e => e.Key).SequenceEqual(pawn.Training.ExportSpecializationProgress().OrderBy(e => e.Key)), "history lost");
            var snapshot = new Snapshot(pawn); PetSpecializationUtility.SyncPetStates(pawn); snapshot.Unchanged(pawn);
        }
    }

    /// <summary>新建的 0.01 显示标记不增加经验；其他普通方向清理后不会重新生效。</summary>
    private static void OrdinaryStateCleanup()
    {
        foreach (var current in Pets)
        {
            var pawn = Pawn(current);
            foreach (var type in Pets.Where(t => t != current)) pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(type)).Severity = .8f;
            PetSpecializationUtility.SyncPetStates(pawn); PetSpecializationUtility.SyncPetStates(pawn);
            Assert(pawn.health.hediffSet.hediffs.Count == 1 && pawn.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(current)), "wrong ordinary tag kept");
            Equal(0, pawn.Training.specializationProgress);
            Equal(.01f, pawn.health.hediffSet.hediffs[0].Severity);
        }
    }

    /// <summary>提前返回路径同样清理；正常旧档的普通状态必须先认领，不能先删再恢复。</summary>
    private static void ReconciliationEarlyReturns()
    {
        foreach (bool master in new[] { false, true })
        {
            var pawn = Pawn(); pawn.Training.SetSpecialization(SexSlaveSpecializationType.None);
            if (master) pawn.Training.pawnIdentity = PawnIdentity.Master;
            var final = Final(pawn, Pets[1]);
            pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Pets[1]));
            CompSexSlaveTraining.ReconcileSpecialization(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None, "explicit none re-adopted");
            Assert(pawn.health.hediffSet.hediffs.SequenceEqual(new[] { final }), "early return retained ordinary marker");
        }
        foreach (var type in Pets)
        {
            var legacy = Pawn(); legacy.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(type)).Severity = .42f;
            CompSexSlaveTraining.ReconcileSpecialization(legacy);
            Assert(legacy.Training.specializationType == type, "legacy marker deleted before adoption"); Equal(.42f, legacy.Training.specializationProgress);
        }
    }

    /// <summary>模拟正式植入顺序中的方向恢复与标签恢复，验证宿主更高严重度不会提前反写。</summary>
    private static void RestoreDoesNotImportHostProgress()
    {
        foreach (var type in Pets)
        {
            var pawn = Pawn(type, .9f); pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(type)).Severity = .9f;
            pawn.Training.RestoreSpecializationProgress(type, .3f, null);
            Equal(.3f, pawn.Training.specializationProgress);
            var data = new CompPersonalityStore(); data.SetTag(PetSpecializationUtility.GetBaseHediffDef(type), .3f);
            PetSpecializationUtility.ApplyExclusivePetTags(pawn, data); CompSexSlaveTraining.ReconcileSpecialization(pawn);
            Equal(.3f, pawn.Training.specializationProgress);
            Equal(.3f, pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(type)).Severity);
        }
    }

    /// <summary>建立实际绑定且同图近距离的对象，记忆请求由可观察容器记录。</summary>
    private static (Pawn pet, Pawn master) AffectionPair(SexSlaveSpecializationType type = SexSlaveSpecializationType.PetDog, float progress = .3f)
    {
        var pet = Pawn(type, progress); var master = Pawn(); pet.BoundMaster = master; master.Map = pet.Map;
        pet.Training.lastPetAffectionTick = -999999;
        DefDatabase<ThoughtDef>.Add(new ThoughtDef { defName = "SSC_PetAffection_Mood" });
        return (pet, master);
    }

    /// <summary>终极成果跨方向亲昵完成只产生既有记忆和冷却，不发放任何培养经验。</summary>
    private static void FinalAffectionAcrossDirections()
    {
        foreach (var type in Pets)
        foreach (var direction in new[] { SexSlaveSpecializationType.None, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.Combatant, SexSlaveSpecializationType.TrainerOfficer })
        {
            var p = AffectionPair(type); var final = Final(p.pet, type); p.pet.Training.SetSpecialization(direction);
            p.pet.Training.specializationProgress = .17f;
            Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "final affection not scheduled");
            Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "final affection did not complete");
            Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "missing affection memory");
            Equal(.17f, p.pet.Training.specializationProgress);
            Assert(p.pet.Training.specializationType == direction && p.pet.health.hediffSet.hediffs.Contains(final), "affection changed direction or final");
        }
    }

    /// <summary>本步不新增 20% 亲昵门槛，也不把普通完成误判为不再有普通效果。</summary>
    private static void OrdinaryAffectionAcrossProgress()
    {
        foreach (var type in Pets)
        foreach (float progress in new[] { 0, .01f, .2f, .999f, 1 })
        {
            var p = AffectionPair(type, progress);
            Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "ordinary affection rejected");
            Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "ordinary affection completion rejected");
            Equal(type == Pets[0] && progress < .999f ? Math.Min(1, progress + .01f) : progress,
                p.pet.Training.specializationProgress);
        }
    }

    /// <summary>执行真实驱动注册的条件与动作，验证启动后的资格变化和直接完成调用同样受保护。</summary>
    private static void AffectionRechecksChanges()
    {
        for (int scenario = 0; scenario < 6; scenario++)
        {
            var p = AffectionPair();
            if (scenario == 1 || scenario == 4) { Final(p.pet, Pets[1]); p.pet.Training.SetSpecialization(SexSlaveSpecializationType.Cow); }
            var driver = new JobDriver_PetAffection { pawn = p.pet, job = JobMaker.MakeJob(SSCDefOf.SSC_Job_PetAffection, p.master) };
            var toils = driver.BuildToils().ToArray();
            Assert(toils.Length == 3 && toils[1].duration == 120 && !driver.FailureConditions.Any(f => f()), "initial job invalid");
            BeginAffection(driver, toils, p.master);
            toils[1].tickAction(); Assert(p.master.rotationTracker.LastTarget == p.pet, "master facing lost");
            if (scenario == 0) p.pet.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            if (scenario == 1) p.pet.health.RemoveHediff(p.pet.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetFinalHediffDef(Pets[1])));
            if (scenario == 2) p.pet.BoundMaster = Pawn();
            if (scenario == 3) p.pet.BoundMaster = null;
            if (scenario == 4) p.pet.Training.SetSpecialization(SexSlaveSpecializationType.None);
            if (scenario == 5)
            {
                p.pet.BoundMaster = null; p.pet.AssignedTrainer = p.master;
                Assert(SSCBondUtility.GetResolvedMaster(p.pet) == p.master, "test must cover resolved trainer fallback");
                Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "assigned trainer used as bound master");
            }
            bool expectedValid = scenario == 4;
            Assert(driver.FailureConditions.Any(f => f()) != expectedValid, "running job did not recheck");
            toils[2].initAction();
            Assert(p.master.needs.mood.thoughts.memories.Requests.Count == (expectedValid ? 1 : 0), "invalid job produced memory");
            Assert(p.pet.Training.lastPetAffectionTick == (expectedValid ? Find.TickManager.TicksGame : -999999), "invalid job consumed cooldown");
        }
    }

    /// <summary>结算冷却阻止同 tick 重复完成；调度只接受真实组件及原有空闲任务。</summary>
    private static void AffectionSchedulingAndCooldown()
    {
        var p = AffectionPair();
        var stale = new CompSexSlaveTraining { parent = p.pet, lastPetAffectionTick = -999999 };
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet, stale), "stale comp accepted");
        p.pet.CurJobDef = new JobDef { defName = "Build" };
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "important work interrupted");
        foreach (var def in new[] { RimWorld.JobDefOf.Wait, RimWorld.JobDefOf.Wait_Wander, new JobDef { defName = "GotoWander" } })
        {
            p.pet.CurJobDef = def; Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "idle job rejected");
        }
        p.pet.CurJobDef = SSCDefOf.SSC_Job_PetAffection;
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "existing affection replaced");
        p.pet.CurJobDef = null; p.pet.jobs.AcceptJobs = false;
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "job refusal ignored");
        Assert(p.pet.Training.lastPetAffectionTick == -999999, "schedule failure consumed completion cooldown");
        Assert(PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "first completion failed");
        Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "duplicate completion accepted");
        Find.TickManager.TicksGame += CompSexSlaveTraining.PetAffectionCooldownTicks - 1;
        Assert(!PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "cooldown shortened");
        Find.TickManager.TicksGame++; p.pet.jobs.AcceptJobs = true;
        Assert(PetSpecializationUtility.TryStartAutomaticPetAffectionJob(p.pet), "cooldown boundary rejected");
        Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 1, "memory duplicated");
    }

    /// <summary>公共资格扩展不放宽既有物理条件，完成拒绝不产生记忆或冷却。</summary>
    private static void AffectionPhysicalConditions()
    {
        for (int scenario = 0; scenario < 11; scenario++)
        {
            var p = AffectionPair(); Final(p.pet, Pets[1]); p.pet.Training.SetSpecialization(SexSlaveSpecializationType.None);
            if (scenario == 0) p.pet.Position = new IntVec3(3, 0);
            if (scenario == 1) p.master.Map = new object();
            if (scenario == 2) p.pet.Spawned = false;
            if (scenario == 3) p.master.Spawned = false;
            if (scenario == 4) p.pet.Downed = true;
            if (scenario == 5) p.master.Downed = true;
            if (scenario == 6) p.pet.Drafted = true;
            if (scenario == 7) p.master.Drafted = true;
            if (scenario == 8) p.pet.Dead = true;
            if (scenario == 9) p.master.Dead = true;
            if (scenario == 10) p.pet.Destroyed = true;
            Assert(!PetSpecializationUtility.CanDoPetAffectionNow(p.pet, p.master), "physical gate bypassed " + scenario);
            Assert(!PetSpecializationUtility.CompletePetAffection(p.pet, p.master), "invalid completion " + scenario);
            Assert(p.master.needs.mood.thoughts.memories.Requests.Count == 0 && p.pet.Training.lastPetAffectionTick == -999999, "rejected completion had effects");
        }
    }

    /// <summary>只观察原工作完成回调的成长、事件概率和冷却，不模拟原版动物工作是否成功。</summary>
    private static void OrdinaryDogWork()
    {
        foreach (bool tame in new[] { false, true })
        foreach (float progress in new[] { 0, .5f, .998f, .999f, 1 })
        {
            var dog = Pawn(Pets[1], progress); var animal = Pawn(); animal.RaceProps.Animal = true;
            float expected = progress >= .999f ? progress : Math.Min(1, progress + (tame ? .006f : .004f) * (1 + progress * .5f));
            Rand.Chances.Clear();
            if (tame) DogSpecializationUtility.NotifyAnimalTamingAttempted(dog, animal);
            else DogSpecializationUtility.NotifyAnimalTrainingCompleted(dog, animal);
            Equal(expected, dog.Training.specializationProgress);
            Assert(Rand.Chances.Count == 1, "work event missing or duplicate"); Equal(.06f + expected * .08f, Rand.Chances.Single());
        }
    }

    /// <summary>终极狗概率固定使用完成成果；工作既不增加狗历史，也不增加当前非宠物进度。</summary>
    private static void FinalDogWork()
    {
        foreach (var current in Enum.GetValues<SexSlaveSpecializationType>())
        foreach (float progress in new[] { 0, .2f, .8f, 1, float.NaN, float.PositiveInfinity })
        {
            var dog = Pawn(current, progress); Final(dog, Pets[1]); var animal = Pawn(); animal.RaceProps.Animal = true;
            var snapshot = new Snapshot(dog); Rand.Chances.Clear();
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(dog, animal);
            DogSpecializationUtility.NotifyAnimalTamingAttempted(dog, animal);
            Assert(Rand.Chances.Count == 1, "final work repeated roll"); Equal(.18f, Rand.Chances.Single());
            snapshot.Unchanged(dog);
        }
    }

    /// <summary>无效或过期方向不进入工作结算，任意其他宠物终极同样阻断普通狗行为。</summary>
    private static void InvalidDogWork()
    {
        for (int scenario = 0; scenario < 7; scenario++)
        {
            var dog = Pawn(Pets[1], .5f); var animal = Pawn(); animal.RaceProps.Animal = true;
            if (scenario == 0) { dog.Training.SetSpecialization(SexSlaveSpecializationType.Cow); dog.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Pets[1])); }
            if (scenario == 1) Final(dog, Pets[0]);
            if (scenario == 2) dog.Dead = true;
            if (scenario == 3) dog.Destroyed = true;
            if (scenario == 4) animal.Dead = true;
            if (scenario == 5) animal.RaceProps.Animal = false;
            if (scenario == 6) animal = null;
            var snapshot = new Snapshot(dog); Rand.Chances.Clear();
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(dog, animal);
            DogSpecializationUtility.NotifyAnimalTamingAttempted(dog, animal);
            snapshot.Unchanged(dog); Assert(Rand.Chances.Count == 0 && dog.Training.lastDogAnimalInteractionTick == -999999, "invalid work entered event");
        }
    }
}
