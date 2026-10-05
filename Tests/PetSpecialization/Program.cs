using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly SexSlaveSpecializationType[] Pets =
    {
        SexSlaveSpecializationType.PetCat, SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit
    };
    private static int passed, failed;

    private static int Main()
    {
        Run("宠物组仅包括猫狗兔，奶牛及未知方向不属于组", Membership);
        Run("八种终极组合的切换矩阵保留同种认领及非宠物方向", DirectionMatrix);
        Run("培养矩阵只认当前宠物方向，任何宠物终极均阻断", TrainingMatrix);
        Run("跨种终极拒绝切换且字段、历史和健康状态完全不变", RejectedSwitchIsAtomic);
        Run("拒绝切换不会规范化损坏当前进度", RejectedSwitchKeepsBadProgress);
        Run("旧 Set 包装同样拒绝跨种终极", LegacySetterIsProtected);
        Run("多个旧宠物终极保留原样并拒绝新增宠物方向", MultipleFinalsArePreserved);
        Run("普通宠物切换保存各自历史并清理非当前普通标记", OrdinaryHistoryRoundTrip);
        Run("同种终极底层认领允许而玩家选择拒绝", InternalFinalAdoptionAndPlayerGate);
        Run("明确留空保留终极及历史且对账不能重新认领", ExplicitNoneKeepsFinal);
        Run("宠物终极不阻止非宠物方向切换", NonPetDirectionsRemainAvailable);
        Run("旧档猫狗兔终极从健康状态认领仍可用", LegacyFinalAdoption);
        Run("猫兔人格进度恢复绕过玩家研究门槛及兔未开放限制", LegacyPetRestore);
        Run("旧档猫兔普通状态在未完成研究下继续培养", LegacyOrdinaryPetTraining);
        Run("狗玩家选择成功并创建显示标记，重复同步不赠送经验", PlayerDogSelection);
        Run("玩家选择需要 SSC 性奴身份", PlayerIdentityGate);
        Run("玩家选择同时检查基础与狗研究，缺失定义安全拒绝", PlayerResearchGate);
        Run("玩家提交重新检查身份、研究及终极事实", PlayerSubmissionRechecks);
        Run("替换后的训练组件不能由旧菜单引用提交", StaleComponentIsRejected);
        Run("兔玩家入口继续禁用且不改状态", UnfinishedPlayerOptions);
        Run("失败原因映射与旧 CanUse 包装一致", FailureMessages);
        Run("任意宠物终极阻止共享、特色和底层经验且不创建普通标记", AllExperienceEntrypointsRespectFinals);
        Run("历史宠物进度不授予非当前方向培养资格", HistoryDoesNotGrantTraining);
        Run("共享及特色培养遵守完成容差和剩余空间", CompletionAndClamping);
        Run("共享、特色与底层宠物入口拒绝非有限和非正收益", InvalidAmounts);
        Run("普通外部标记导入不冒充本次共享奖励", ExistingMarkerImport);
        Run("宠物终极不阻断非宠物共享与底层经验", NonPetExperienceRemainsAvailable);
        Run("实际绑定主人倍率沿用共享生产公式", BoundMasterFormula);
        Run("共享经验拒绝死亡、销毁、无组件及错误身份", InvalidSharedReceivers);
        RunEffectAndBehaviorCases();
        RunCatTrainingCases();
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        Reset();
        try { test(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception error) { failed++; Console.WriteLine("失败：" + name + "\n" + error); }
    }

    private static void Reset()
    {
        DefDatabase<HediffDef>.Definitions.Clear();
        DefDatabase<ResearchProjectDef>.Definitions.Clear();
        foreach (string name in new[] { "PetCat", "PetDog", "PetRabbit" })
        {
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + name });
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Hediff_" + name + "_Final" });
            DefDatabase<ResearchProjectDef>.Add(new ResearchProjectDef { defName = "SSC_RES_" + name });
        }
        SSCDefOf.SSC_BasicTraining.IsFinished = true;
        Scribe.mode = LoadSaveMode.Inactive;
        Find.TickManager.TicksGame = 100000;
        Rand.Chances.Clear(); Rand.ChanceResult = false;
        Messages.Calls = TrainerSpecializationLifecycle.NotifyCalls = CombatantSpecializationUtility.SyncCalls = 0;
    }

    private static Pawn Pawn(SexSlaveSpecializationType type = SexSlaveSpecializationType.None, float progress = 0)
    {
        var pawn = new Pawn();
        pawn.Training = new CompSexSlaveTraining { parent = pawn, pawnIdentity = PawnIdentity.Slave };
        if (type != SexSlaveSpecializationType.None) pawn.Training.SetSpecialization(type);
        pawn.Training.specializationProgress = progress;
        return pawn;
    }

    private static Hediff Final(Pawn pawn, SexSlaveSpecializationType type)
    {
        Hediff hediff = pawn.health.AddHediff(PetSpecializationUtility.GetFinalHediffDef(type));
        hediff.Severity = 1;
        return hediff;
    }
    private static ResearchProjectDef DogResearch => DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SSC_RES_PetDog");
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Equal(float expected, float actual)
    {
        Assert(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(expected - actual) < .00001f,
            $"expected {expected}, actual {actual}");
    }
    private static void Failure(PetSpecializationFailure expected, PetSpecializationFailure actual) => Assert(expected == actual, $"expected {expected}, actual {actual}");

    private sealed class Snapshot
    {
        private readonly SexSlaveSpecializationType type;
        private readonly float progress, cow;
        private readonly bool unset, invalidExit;
        private readonly RabbitReproductionMode rabbit;
        private readonly Dictionary<string, float> historyReference, history;
        private readonly Hediff[] hediffs;
        private readonly float[] severities;
        private readonly int adds, removes, notify, sync, messages;
        private static readonly FieldInfo HistoryField = typeof(CompSexSlaveTraining).GetField("perTypeProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        public Snapshot(Pawn pawn)
        {
            var comp = pawn.Training;
            type = comp.specializationType; progress = comp.specializationProgress;
            cow = comp.savedCowReservoirCharge; unset = comp.specializationExplicitlyUnset;
            invalidExit = comp.trainerInvalidExitBlocksAdoption; rabbit = comp.rabbitReproductionMode;
            historyReference = (Dictionary<string, float>)HistoryField.GetValue(comp);
            history = historyReference == null ? null : new Dictionary<string, float>(historyReference);
            hediffs = pawn.health.hediffSet.hediffs.ToArray(); severities = hediffs.Select(h => h.Severity).ToArray();
            adds = pawn.health.AddCalls; removes = pawn.health.RemoveCalls;
            notify = TrainerSpecializationLifecycle.NotifyCalls; sync = CombatantSpecializationUtility.SyncCalls; messages = Messages.Calls;
        }
        public void Unchanged(Pawn pawn)
        {
            var comp = pawn.Training;
            Assert(type == comp.specializationType && progress.Equals(comp.specializationProgress), "current fields changed");
            Assert(cow.Equals(comp.savedCowReservoirCharge) && unset == comp.specializationExplicitlyUnset &&
                invalidExit == comp.trainerInvalidExitBlocksAdoption && rabbit == comp.rabbitReproductionMode, "flags or body resource changed");
            var current = (Dictionary<string, float>)HistoryField.GetValue(comp);
            Assert(ReferenceEquals(historyReference, current), "history container changed");
            if (history != null) Assert(history.Count == current.Count && history.All(e => current.TryGetValue(e.Key, out float value) && e.Value.Equals(value)), "history contents changed");
            Assert(hediffs.SequenceEqual(pawn.health.hediffSet.hediffs) && severities.SequenceEqual(hediffs.Select(h => h.Severity)), "health changed");
            Assert(adds == pawn.health.AddCalls && removes == pawn.health.RemoveCalls, "health mutation attempted");
            Assert(notify == TrainerSpecializationLifecycle.NotifyCalls && sync == CombatantSpecializationUtility.SyncCalls && messages == Messages.Calls, "mutation notification attempted");
        }
    }

    private static void Membership()
    {
        foreach (var type in Enum.GetValues<SexSlaveSpecializationType>())
            Assert(PetSpecializationRules.IsPetSpecialization(type) == Pets.Contains(type), "wrong group member " + type);
        Assert(!PetSpecializationRules.IsPetSpecialization((SexSlaveSpecializationType)999), "unknown direction in group");
    }

    private static void DirectionMatrix()
    {
        SexSlaveSpecializationType[][] allowedPets = { Pets, new[] { Pets[0] }, new[] { Pets[1] }, Array.Empty<SexSlaveSpecializationType>(),
            new[] { Pets[2] }, Array.Empty<SexSlaveSpecializationType>(), Array.Empty<SexSlaveSpecializationType>(), Array.Empty<SexSlaveSpecializationType>() };
        for (int mask = 0; mask < 8; mask++)
        foreach (var target in Enum.GetValues<SexSlaveSpecializationType>())
        {
            bool expected = !Pets.Contains(target) || allowedPets[mask].Contains(target);
            bool actual = PetSpecializationRules.CanChangeDirection(target, (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, out var failure);
            Assert(actual == expected, $"mask {mask}, target {target}");
            Failure(expected ? PetSpecializationFailure.None : PetSpecializationFailure.ConflictingFinal, failure);
        }
    }

    private static void TrainingMatrix()
    {
        for (int mask = 0; mask < 8; mask++)
        foreach (var type in Enum.GetValues<SexSlaveSpecializationType>())
        foreach (var current in Enum.GetValues<SexSlaveSpecializationType>())
            Assert(PetSpecializationRules.CanTrain(type, current, (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0) ==
                (mask == 0 && Pets.Contains(type) && type == current), $"mask {mask}, type {type}, current {current}");
    }

    private static Pawn PreparedConflict(SexSlaveSpecializationType finalType)
    {
        var pawn = Pawn(SexSlaveSpecializationType.PetRabbit, .43f);
        pawn.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
        pawn.Training.specializationProgress = .62f;
        pawn.Training.specializationExplicitlyUnset = true;
        pawn.Training.trainerInvalidExitBlocksAdoption = true;
        pawn.Training.rabbitReproductionMode = RabbitReproductionMode.Clone;
        pawn.Training.savedCowReservoirCharge = .27f;
        pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Cow).Severity = .62f;
        Final(pawn, finalType);
        return pawn;
    }

    private static void RejectedSwitchIsAtomic()
    {
        foreach (var finalType in Pets)
        foreach (var target in Pets.Where(t => t != finalType))
        {
            var pawn = PreparedConflict(finalType); var snapshot = new Snapshot(pawn);
            Assert(!pawn.Training.TrySetSpecialization(target, out var failure), "conflicting switch accepted");
            Failure(PetSpecializationFailure.ConflictingFinal, failure); snapshot.Unchanged(pawn);
        }
    }

    private static void RejectedSwitchKeepsBadProgress()
    {
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -3f, 7f })
        {
            var pawn = PreparedConflict(Pets[0]); pawn.Training.specializationProgress = bad;
            var snapshot = new Snapshot(pawn);
            Assert(!pawn.Training.TrySetSpecialization(Pets[1], out _), "conflict accepted"); snapshot.Unchanged(pawn);
        }
    }

    private static void LegacySetterIsProtected()
    {
        var pawn = PreparedConflict(Pets[0]); var snapshot = new Snapshot(pawn);
        pawn.Training.SetSpecialization(Pets[1]); snapshot.Unchanged(pawn);
    }

    private static void MultipleFinalsArePreserved()
    {
        var pawn = Pawn(SexSlaveSpecializationType.Cow, .4f);
        foreach (var type in Pets) Final(pawn, type);
        var snapshot = new Snapshot(pawn);
        foreach (var type in Pets)
        {
            Assert(!pawn.Training.TrySetSpecialization(type, out var failure), "multi-final direction accepted");
            Failure(PetSpecializationFailure.ConflictingFinal, failure); snapshot.Unchanged(pawn);
        }
    }

    private static void OrdinaryHistoryRoundTrip()
    {
        foreach (var first in Pets)
        foreach (var second in Pets.Where(t => t != first))
        {
            var pawn = Pawn(first, .36f); PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
            Assert(pawn.Training.TrySetSpecialization(second, out _), "ordinary switch denied");
            Assert(!pawn.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(first)), "inactive base retained");
            Equal(.36f, pawn.Training.ExportSpecializationProgress()[first.ToString()]); Equal(0, pawn.Training.specializationProgress);
            pawn.Training.specializationProgress = .71f; PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
            pawn.Training.SetSpecialization(first); PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
            Equal(.36f, pawn.Training.specializationProgress);
            Equal(.36f, pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(first)).Severity);
            Equal(.71f, pawn.Training.ExportSpecializationProgress()[second.ToString()]);
            Assert(!pawn.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(second)), "second ordinary base retained");
        }
    }

    private static void InternalFinalAdoptionAndPlayerGate()
    {
        foreach (var type in Pets)
        {
            var pawn = Pawn(); Final(pawn, type);
            Assert(pawn.Training.TrySetSpecialization(type, out var failure), "same final internal adoption denied");
            Failure(PetSpecializationFailure.None, failure);
            Assert(!PetSpecializationUtility.CanSelectPetSpecialization(pawn, type, out failure), "final player selection accepted");
            Failure(type == Pets[2] ? PetSpecializationFailure.NotImplemented : PetSpecializationFailure.AlreadyFinalized, failure);
        }
    }

    private static void ExplicitNoneKeepsFinal()
    {
        foreach (var type in Pets)
        {
            var pawn = Pawn(type, 1); var final = Final(pawn, type);
            Assert(pawn.Training.TrySetSpecialization(SexSlaveSpecializationType.None, out _), "none denied");
            CompSexSlaveTraining.ReconcileSpecialization(pawn);
            Assert(pawn.Training.specializationType == SexSlaveSpecializationType.None && !pawn.Training.CanAdoptSpecializationFromHealth, "none re-adopted");
            Assert(pawn.health.hediffSet.hediffs.Contains(final), "final deleted");
            Equal(1, pawn.Training.ExportSpecializationProgress()[type.ToString()]); Equal(0, pawn.Training.specializationProgress);
        }
    }

    private static void NonPetDirectionsRemainAvailable()
    {
        foreach (var type in new[] { SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.TrainerOfficer, SexSlaveSpecializationType.Combatant })
        {
            var pawn = Pawn(Pets[1], .7f); var final = Final(pawn, Pets[1]);
            Assert(pawn.Training.TrySetSpecialization(type, out var failure), "nonpet denied");
            Failure(PetSpecializationFailure.None, failure);
            Assert(pawn.Training.specializationType == type && pawn.health.hediffSet.hediffs.Contains(final), "nonpet switch damaged final");
        }
    }

    private static void LegacyFinalAdoption()
    {
        foreach (var type in Pets)
        {
            var pawn = Pawn(); Final(pawn, type); CompSexSlaveTraining.ReconcileSpecialization(pawn);
            Assert(pawn.Training.specializationType == type, "legacy final not adopted " + type);
            Assert(!pawn.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(type)), "final adoption generated base");
        }
    }

    private static void LegacyPetRestore()
    {
        foreach (var type in new[] { Pets[0], Pets[2] })
        {
            var pawn = Pawn(SexSlaveSpecializationType.Cow, .8f);
            pawn.Training.pawnIdentity = PawnIdentity.Master; SSCDefOf.SSC_BasicTraining.IsFinished = false;
            DefDatabase<ResearchProjectDef>.GetNamedSilentFail(PetSpecializationUtility.GetResearchDefName(type)).IsFinished = false;
            pawn.Training.RestoreSpecializationProgress(type, .42f, new Dictionary<string, float> { [Pets[1].ToString()] = .16f });
            Assert(pawn.Training.specializationType == type, "legal pet restore blocked"); Equal(.42f, pawn.Training.specializationProgress);
            Equal(.16f, pawn.Training.ExportSpecializationProgress()[Pets[1].ToString()]);
            Assert(!pawn.Training.ExportSpecializationProgress().ContainsKey(SexSlaveSpecializationType.Cow.ToString()), "host history leaked");
        }
    }

    private static void LegacyOrdinaryPetTraining()
    {
        SSCDefOf.SSC_BasicTraining.IsFinished = false;
        foreach (var type in new[] { Pets[0], Pets[2] })
        {
            var pawn = Pawn(); DefDatabase<ResearchProjectDef>.GetNamedSilentFail(PetSpecializationUtility.GetResearchDefName(type)).IsFinished = false;
            pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(type)).Severity = .35f;
            CompSexSlaveTraining.ReconcileSpecialization(pawn);
            Assert(pawn.Training.specializationType == type, "legacy base not adopted");
            Assert(PetSpecializationUtility.TryGainPetProgress(pawn, type, .01f), "legacy specialty gain blocked"); Equal(.36f, pawn.Training.specializationProgress);
            Equal(.02f, SpecializationTrainingProgressUtility.TryGainProgress(pawn, .02f)); Equal(.38f, pawn.Training.specializationProgress);
        }
    }

    private static void PlayerDogSelection()
    {
        var pawn = Pawn();
        Assert(PetSpecializationUtility.TrySelectPetSpecialization(pawn, pawn.Training, Pets[1], out var failure), "dog selection denied");
        Failure(PetSpecializationFailure.None, failure); Equal(0, pawn.Training.specializationProgress);
        Equal(.01f, pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(Pets[1])).Severity);
        PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn); PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
        Equal(0, pawn.Training.specializationProgress);
    }

    private static void PlayerIdentityGate()
    {
        foreach (var identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
        {
            var pawn = Pawn(); pawn.Training.pawnIdentity = identity; var snapshot = new Snapshot(pawn);
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, pawn.Training, Pets[1], out var failure), "non-slave accepted");
            Failure(PetSpecializationFailure.MissingRequirements, failure); snapshot.Unchanged(pawn);
        }
        Assert(!PetSpecializationUtility.CanSelectPetSpecialization(null, Pets[1], out _), "null accepted");
        Assert(!PetSpecializationUtility.CanSelectPetSpecialization(new Pawn(), Pets[1], out _), "missing comp accepted");
    }

    private static void PlayerResearchGate()
    {
        foreach (int missing in new[] { 1, 2, 3 })
        {
            var pawn = Pawn(); SSCDefOf.SSC_BasicTraining.IsFinished = (missing & 1) == 0; DogResearch.IsFinished = (missing & 2) == 0;
            var snapshot = new Snapshot(pawn);
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, pawn.Training, Pets[1], out var failure), "unfinished research accepted");
            Failure(PetSpecializationFailure.ResearchRequired, failure); snapshot.Unchanged(pawn);
        }
        SSCDefOf.SSC_BasicTraining.IsFinished = true; DefDatabase<ResearchProjectDef>.Definitions.Remove("SSC_RES_PetDog");
        Assert(!PetSpecializationUtility.CanSelectPetSpecialization(Pawn(), Pets[1], out var missingFailure), "missing def accepted");
        Failure(PetSpecializationFailure.MissingRequirements, missingFailure);
    }

    private static void PlayerSubmissionRechecks()
    {
        for (int change = 0; change < 4; change++)
        {
            Reset(); var pawn = Pawn(SexSlaveSpecializationType.Cow, .3f);
            Assert(PetSpecializationUtility.CanSelectPetSpecialization(pawn, Pets[1], out _), "menu initially unavailable");
            if (change == 0) pawn.Training.pawnIdentity = PawnIdentity.Master;
            if (change == 1) DogResearch.IsFinished = false;
            if (change == 2) Final(pawn, Pets[0]);
            if (change == 3) Final(pawn, Pets[1]);
            var snapshot = new Snapshot(pawn);
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, pawn.Training, Pets[1], out _), "changed qualification accepted");
            snapshot.Unchanged(pawn);
        }
    }

    private static void StaleComponentIsRejected()
    {
        var pawn = Pawn(); var stale = pawn.Training;
        pawn.Training = new CompSexSlaveTraining { parent = pawn, pawnIdentity = PawnIdentity.Slave };
        var snapshot = new Snapshot(pawn);
        Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, stale, Pets[1], out var failure), "stale comp accepted");
        Failure(PetSpecializationFailure.MissingRequirements, failure); snapshot.Unchanged(pawn);
        Assert(stale.specializationType == SexSlaveSpecializationType.None, "stale comp changed");
    }

    private static void UnfinishedPlayerOptions()
    {
        foreach (var type in new[] { Pets[2] })
        {
            var pawn = Pawn(SexSlaveSpecializationType.Cow, .31f); var snapshot = new Snapshot(pawn);
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, pawn.Training, type, out var failure), "unfinished option accepted");
            Failure(PetSpecializationFailure.NotImplemented, failure); snapshot.Unchanged(pawn);
        }
    }

    private static void FailureMessages()
    {
        Assert(PetSpecializationUtility.GetSelectionFailureReason(PetSpecializationFailure.None) == null, "success has rejection text");
        foreach (var failure in Enum.GetValues<PetSpecializationFailure>().Where(f => f != PetSpecializationFailure.None))
            Assert(!string.IsNullOrEmpty(PetSpecializationUtility.GetSelectionFailureReason(failure)), "missing failure message " + failure);
        var pawn = Pawn(); Final(pawn, Pets[0]);
        Assert(!PetSpecializationUtility.CanUsePetSpecialization(pawn, Pets[1], out string reason), "legacy wrapper bypassed conflict");
        Assert(reason == Strings.ITab_SpecializationPetDisabledConflictingFinal, "legacy reason disagrees");
    }

    private static void AllExperienceEntrypointsRespectFinals()
    {
        foreach (var current in Pets)
        foreach (var finalType in Pets)
        {
            var pawn = Pawn(current, .24f); Final(pawn, finalType); var snapshot = new Snapshot(pawn);
            Assert(PetSpecializationUtility.HasAnyFinalPetState(pawn), "any final query missed " + finalType);
            Assert(!PetSpecializationUtility.CanTrainPetSpecialization(pawn, current), "final grants training");
            Equal(0, SpecializationTrainingProgressUtility.TryGainProgress(pawn, .03f));
            Assert(!PetSpecializationUtility.TryGainPetProgress(pawn, current, .03f), "specialty gained after final");
            Equal(.24f, pawn.Training.AddSpecializationProgress(.03f));
            PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn); snapshot.Unchanged(pawn);
            Assert(!pawn.health.hediffSet.HasHediff(PetSpecializationUtility.GetBaseHediffDef(current)), "conflict generated base");
        }
    }

    private static void HistoryDoesNotGrantTraining()
    {
        var pawn = Pawn(Pets[1], .67f); pawn.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
        var snapshot = new Snapshot(pawn);
        Assert(!PetSpecializationUtility.CanTrainPetSpecialization(pawn, Pets[1]), "history grants qualification");
        Assert(!PetSpecializationUtility.TryGainPetProgress(pawn, Pets[1], .02f), "inactive history gained"); snapshot.Unchanged(pawn);
        pawn.Training.SetSpecialization(Pets[0]);
        Assert(!PetSpecializationUtility.TryGainPetProgress(pawn, Pets[1], .02f), "different current pet gained");
    }

    private static void CompletionAndClamping()
    {
        foreach (var type in Pets)
        foreach (float progress in new[] { .19f, .98f, .999f, 1f })
        {
            var shared = Pawn(type, progress);
            float expected = progress >= .999f ? progress : Math.Min(1, progress + .04f);
            Equal(expected - progress, SpecializationTrainingProgressUtility.TryGainProgress(shared, .04f)); Equal(expected, shared.Training.specializationProgress);
            var specialty = Pawn(type, progress);
            Assert(PetSpecializationUtility.TryGainPetProgress(specialty, type, .04f) == (progress < .999f), "completion specialty result");
            Equal(expected, specialty.Training.specializationProgress);
            var raw = Pawn(type, progress);
            Equal(expected, raw.Training.AddSpecializationProgress(.04f)); Equal(expected, raw.Training.specializationProgress);
        }
    }

    private static void InvalidAmounts()
    {
        foreach (var type in Pets)
        foreach (float amount in new[] { 0, -.02f, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
        {
            var pawn = Pawn(type, .3f); var snapshot = new Snapshot(pawn);
            Equal(0, SpecializationTrainingProgressUtility.TryGainProgress(pawn, amount));
            Assert(!PetSpecializationUtility.TryGainPetProgress(pawn, type, amount), "invalid specialty amount accepted");
            Equal(.3f, pawn.Training.AddSpecializationProgress(amount)); snapshot.Unchanged(pawn);
        }
    }

    private static void ExistingMarkerImport()
    {
        var pawn = Pawn(Pets[1], .1f); pawn.health.AddHediff(PetSpecializationUtility.GetBaseHediffDef(Pets[1])).Severity = .5f;
        Equal(.02f, SpecializationTrainingProgressUtility.TryGainProgress(pawn, .02f)); Equal(.52f, pawn.Training.specializationProgress);
        Equal(.52f, pawn.health.hediffSet.GetFirstHediffOfDef(PetSpecializationUtility.GetBaseHediffDef(Pets[1])).Severity);
    }

    private static void NonPetExperienceRemainsAvailable()
    {
        foreach (var type in new[] { SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.Cow, SexSlaveSpecializationType.TrainerOfficer, SexSlaveSpecializationType.Combatant })
        {
            var pawn = Pawn(type, .2f); foreach (var pet in Pets) Final(pawn, pet);
            Equal(.03f, SpecializationTrainingProgressUtility.TryGainProgress(pawn, .03f)); Equal(.25f, pawn.Training.AddSpecializationProgress(.02f));
            Assert(Pets.All(pet => PetSpecializationUtility.HasFinalPetState(pawn, pet)), "unrelated final lost");
        }
    }

    private static void BoundMasterFormula()
    {
        Pawn master = Pawn(), other = Pawn(), pet = Pawn(Pets[1]); pet.BoundMaster = master;
        Equal(.03f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(master, pet, 40));
        Equal(.02f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(other, pet, 40)); Equal(.05f, pet.Training.specializationProgress);
    }

    private static void InvalidSharedReceivers()
    {
        var dead = Pawn(Pets[1], .3f); dead.Dead = true;
        var destroyed = Pawn(Pets[1], .3f); destroyed.Destroyed = true;
        var master = Pawn(Pets[1], .3f); master.Training.pawnIdentity = PawnIdentity.Master;
        foreach (var pawn in new[] { dead, destroyed, master })
        {
            var snapshot = new Snapshot(pawn); Equal(0, SpecializationTrainingProgressUtility.TryGainProgress(pawn, .03f)); snapshot.Unchanged(pawn);
        }
        Equal(0, SpecializationTrainingProgressUtility.TryGainProgress(null, .03f));
        Equal(0, SpecializationTrainingProgressUtility.TryGainProgress(new Pawn(), .03f));
    }
}
