using System;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RunOwnedTrainingCases()
    {
        Run("己方动物训练项目按原版顺序筛选勾选、未完成及公开训练资格", OrderedTrainingQualification);
        Run("无待训项目或缺训练Tracker拒绝，野生不依赖训练项目", NoEligibleTraining);
        Run("树精和非动物不能从残留的训练Tracker获取项目", NonOrdinaryTrainingTargets);
        Run("己方训练目标仍限定同地图动物意识和精神资格", OwnedTargetQualification);
        Run("原版GetJob字段保留，玩家与野生分派不同任务报告", JobBranchDispatch);
        Run("训练目标仍地图选择同格施放，界面显示动物及当前项目", TrainingVisuals);
        Run("到达后完整两秒预热才训练一次，并正常完成任务", TrainingWarmup);
        Run("单次完成一个勾选项目，保留主人且不给普通工作经验", OneProjectAndNoExtraRewards);
        Run("已学会但步数衰退的项目可重新完整训练", LearnedMaintenanceTraining);
        Run("行走与预热取消最后待训项目不训练不消耗冷却", TrainingCancellation);
        Run("有后继项目时按生效时当前项目选择，不锁最初项目", DynamicTrainingProject);
        Run("原版最终训练资格变化可选择后继合格项目或拒绝", DynamicNativeQualification);
        Run("野生被别人驯服后不自动转训练，玩家变野生不自动驯服", FactionBranchIsolation);
        Run("任务分支守卫只约束当前技能的相同目标，选取不被旧任务绑定", BranchGuardOwnership);
        Run("两人竞争最后项目只结算一次，剩余项目允许另一施放者完成", CompetingTraining);
        Run("第三方训练无实效恢复本次冷却且不更新互动时间", NativeTrainingFailure);
        Run("训练期间Tracker替换或阵营变化视为失败，无MindState仍可成功", NativeTrainingPostconditions);
        Run("训练任务、拒绝提示与双参数目标和成功提示四语镜像完整", TrainingResources);
    }

    /// <summary>边界输入显式给出原版排序和资格；生产工具决定实际候选，不重写原版资格算法。</summary>
    private static TrainableDef Project(Pawn target, string name, bool wanted = true, bool trainable = true, bool assignable = true)
    {
        var def = new TrainableDef { defName = name, label = name + " label" };
        TrainableUtility.TrainableDefsInListOrder.Add(def);
        var state = target.training.State(def); state.Wanted = wanted; state.CanBeTrained = trainable; state.CanAssignToTrain = assignable;
        return def;
    }
    private static (Pawn caster, Pawn target, Ability_PetDogTame ability) OwnedPair()
    {
        var p = Pair(); p.target.Faction = Faction.OfPlayer; Project(p.target, "Obedience"); return p;
    }
    private static void NoTraining((Pawn caster, Pawn target, Ability_PetDogTame ability) p)
    {
        NoSettlement(p);
        Check(p.target.training?.Requests.Count is null or 0, "invalid cast trained animal");
        Check(p.target.mindState?.lastAssignedInteractTime is null or -100 && p.target.mindState?.interactionsToday is null or 0, "invalid cast changed ordinary interaction scheduling");
    }
    private static void OrderedTrainingQualification()
    {
        var p = Pair(); p.target.Faction = Faction.OfPlayer;
        Project(p.target, "Unwanted", wanted: false);
        Project(p.target, "Completed", trainable: false);
        Project(p.target, "Unsupported", assignable: false);
        var first = Project(p.target, "Z-first"); var second = Project(p.target, "A-second");
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == first, "native list order or qualification filtering ignored");
        Check(PetDogAbilityUtility.CanSelectTarget(p.caster, p.target, out _), "eligible owned target rejected");
        p.target.NativeTamingAllowed = false;
        Check(PetDogAbilityUtility.CanTarget(p.caster, p.target, out _), "owned training incorrectly requires wild CanTame");
        p.target.training.State(first).Wanted = false;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == second, "dynamic wanted state ignored");
        p.target.training.State(second).CanAssignToTrain = false;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == null, "unsupported projects offered"); NoTraining(p);
    }
    private static void NoEligibleTraining()
    {
        var p = Pair(); p.target.Faction = Faction.OfPlayer;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == null && !PetDogAbilityUtility.CanTarget(p.caster, p.target, out string reason) && reason == "SSC_PetDogTameNoTrainingTarget", "no-project rejection missing");
        Project(p.target, "NeverSelected", wanted: false); Project(p.target, "Unavailable", assignable: false);
        Check(!Verb(p.ability).ValidateTarget(p.target) && !p.ability.Activate(p.target, default), "empty/unsupported training cast allowed"); NoTraining(p);
        p.target.training = null;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == null && !PetDogAbilityUtility.CanTarget(p.caster, p.target, out _), "missing tracker offered project");
        p.target.Faction = null;
        Check(PetDogAbilityUtility.CanTarget(p.caster, p.target, out _), "wild taming unexpectedly needs training tracker");
        Check(PetDogAbilityUtility.GetNextTraining(null) == null, "null target manufactured project");
    }
    private static void OwnedTargetQualification()
    {
        for (int scenario = 0; scenario < 8; scenario++)
        {
            Reset(); var p = OwnedPair();
            if (scenario == 0) p.target.Faction = new Faction();
            if (scenario == 1) p.target.Map = new Map();
            if (scenario == 2) p.target.RaceProps.Animal = false;
            if (scenario == 3) p.target.Conscious = false;
            if (scenario == 4) p.target.MentalState = new MentalState();
            if (scenario == 5) p.target.Dead = true;
            if (scenario == 6) p.target.Spawned = false;
            if (scenario == 7) p.target.Destroyed = true;
            Check(!PetDogAbilityUtility.CanTarget(p.caster, p.target, out _) && !Verb(p.ability).TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default), "owned base qualification omitted " + scenario); NoTraining(p);
        }
        Reset(); var sleeping = OwnedPair(); sleeping.target.Sleeping = true; sleeping.target.Downed = true;
        Check(Verb(sleeping.ability).ValidateTarget(sleeping.target) && sleeping.target.Sleeping && sleeping.target.NativeWakeCalls == 0, "conscious sleeping/downed selection changed body"); NoTraining(sleeping);
    }
    private static void NonOrdinaryTrainingTargets()
    {
        var p = OwnedPair(); p.target.RaceProps.animalType = AnimalType.Dryad;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == null && !PetDogAbilityUtility.CanTarget(p.caster, p.target, out _), "dryad offered ordinary animal training"); NoTraining(p);
        p.target.RaceProps.animalType = AnimalType.Normal; p.target.RaceProps.Animal = false;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == null, "non-animal stale tracker offered training");
        p.target.RaceProps = null; Check(PetDogAbilityUtility.GetNextTraining(p.target) == null, "missing race props offered training");
    }
    private static void JobBranchDispatch()
    {
        var p = OwnedPair(); var destination = new Thing();
        var train = p.ability.GetJob(p.target, destination);
        Check(p.ability.BaseGetJobCalls == 1 && train.def == SSCDefOf.SSC_Job_PetDogTrain && train.targetA.Pawn == p.target && train.targetB.Thing == destination && train.ability == p.ability && train.verbToUse == p.ability.verb, "owned dispatch replaced native refs");
        p.target.Faction = null; var tame = p.ability.GetJob(p.target, destination);
        Check(p.ability.BaseGetJobCalls == 2 && tame.def == SSCDefOf.SSC_PetDogTame.jobDef && tame.targetA.Pawn == p.target && tame.targetB.Thing == destination && tame.ability == p.ability && tame.verbToUse == p.ability.verb, "wild dispatch changed native job fields"); NoTraining(p);
    }
    private static void TrainingVisuals()
    {
        var p = OwnedPair(); p.target.LabelName = "owned animal"; p.target.Position = new IntVec3(120, 60);
        var project = PetDogAbilityUtility.GetNextTraining(p.target); var verb = Verb(p.ability); verb.DrawHighlight(p.target); verb.OnGUI(p.target);
        Check(GenDraw.Highlights.Single().Pawn == p.target && GenUI.Attachments.Single() == p.ability.def.uiIcon, "owned remote native highlighting missing");
        string hint = Widgets.AttachedLabels.Single();
        Check(hint.StartsWith("SSC_PetDogTameTrainingTargetHint(") && hint.Contains(p.target.LabelShort) && hint.Contains(project.LabelCap), "owned hint lacks animal/project");
        Check(!verb.TryStartCastOn(p.target, default) && !p.ability.Activate(p.target, default) && !verb.CanHitTargetFrom(p.caster.Position, p.target), "training UI bypassed same-cell execution"); NoTraining(p);
        p.target.training.State(project).Wanted = false; verb.DrawHighlight(p.target); verb.OnGUI(p.target);
        Check(GenDraw.Highlights.Count == 1 && GenUI.Attachments.Last() == TexCommand.CannotShoot && Widgets.AttachedLabels.Count == 1, "unwanted hover retained eligibility");
    }
    private static void TrainingWarmup()
    {
        var p = OwnedPair(); p.target.Position = new IntVec3(90, 20); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
        Check(runner.Driver.job.def == SSCDefOf.SSC_Job_PetDogTrain, "runner omitted real owned dispatch"); Tick(runner, 40); NoTraining(p);
        Check(p.caster.pather.MovingNow && p.target.stances.stunner.StunCalls == 0, "training approach paused animal");
        runner.NotifyArrival(); Check(Verb(p.ability).WarmupRemaining == 120 && p.target.stances.stunner.StunCalls == 1, "native training warmup missing");
        Tick(runner, 119); NoTraining(p); runner.Tick();
        Check(p.target.training.Requests.Count == 1 && p.ability.CooldownTicksRemaining == 60000 && InteractionWorker_RecruitAttempt.Requests.Count == 0, "warmup did not complete training exactly once");
        runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Succeeded && p.ability.CooldownStartCalls == 1, "training target completion aborted successful job");
    }
    private static void OneProjectAndNoExtraRewards()
    {
        var p = OwnedPair(); var first = PetDogAbilityUtility.GetNextTraining(p.target); var second = Project(p.target, "Rescue");
        var master = Pawn(); p.target.playerSettings.Master = master; var before = p.caster.Training.ExportSpecializationProgress();
        Check(p.ability.Activate(p.target, default), "valid owned train rejected");
        var request = p.target.training.Requests.Single();
        Check(request.project == first && request.trainer == null && request.complete && p.target.training.HasLearned(first) && !p.target.training.CanBeTrained(first), "native complete/null trainer contract changed");
        Check(!p.target.training.HasLearned(second) && p.target.playerSettings.Master == master && p.target.Faction == Faction.OfPlayer && InteractionWorker_RecruitAttempt.Requests.Count == 0, "single cast trained another project/recruited/changed master");
        Check(p.target.mindState.lastAssignedInteractTime == Find.TickManager.TicksGame && p.target.mindState.interactionsToday == 1 && p.ability.CooldownTicksRemaining == 60000, "ordinary training scheduling or CD not updated");
        Check(Messages.Requests.Single().StartsWith("SSC_PetDogTameTrainingSuccess(") && Messages.Requests.Single().Contains(p.target.LabelShort) && Messages.Requests.Single().Contains(first.LabelCap), "training success message lacks animal/project");
        Check(p.caster.Training.ExportSpecializationProgress().OrderBy(e => e.Key).SequenceEqual(before.OrderBy(e => e.Key)) && p.caster.Training.lastPetAffectionTick == -999999 && p.target.Training.specializationProgress == 0, "active train awarded ordinary SSC work growth");
        Check(!p.ability.Activate(p.target, default) && p.target.training.Requests.Count == 1 && p.target.mindState.interactionsToday == 1, "cooldown allowed repeated training");
        p.ability.ResetCooldown(); Check(p.ability.Activate(p.target, default) && p.target.training.Requests.Count == 2 && p.target.training.Requests.Last().project == second && p.target.mindState.interactionsToday == 2, "subsequent ready cast did not complete next project");
    }
    private static void LearnedMaintenanceTraining()
    {
        var p = OwnedPair(); var project = PetDogAbilityUtility.GetNextTraining(p.target); var state = p.target.training.State(project); state.Learned = true; state.Steps = 2;
        Check(PetDogAbilityUtility.GetNextTraining(p.target) == project && p.ability.Activate(p.target, default), "learned but trainable maintenance incorrectly rejected");
        Check(p.target.training.Requests.Single().complete && state.Steps == project.steps && !state.CanBeTrained && state.Learned, "maintenance did not request full restoration");
    }
    private static void TrainingCancellation()
    {
        foreach (bool warmup in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            Reset(); var p = OwnedPair(); var project = PetDogAbilityUtility.GetNextTraining(p.target); p.target.Position = new IntVec3(10, 10); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
            if (warmup) { runner.NotifyArrival(); Tick(runner, 119); }
            if (cancel) runner.Cancel(); else { p.target.training.State(project).Wanted = false; runner.Tick(); }
            Check(runner.Ended && !Verb(p.ability).WarmingUp, "training cancellation retained warmup"); NoTraining(p);
        }
    }
    private static void DynamicTrainingProject()
    {
        foreach (bool completed in new[] { false, true })
        foreach (bool warmup in new[] { false, true })
        {
            Reset(); var p = OwnedPair(); var first = PetDogAbilityUtility.GetNextTraining(p.target); var second = Project(p.target, "Release"); p.target.Position = new IntVec3(10, 10); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability);
            if (warmup) { runner.NotifyArrival(); Tick(runner, 119); }
            if (completed) { p.target.training.State(first).Learned = true; p.target.training.State(first).CanBeTrained = false; }
            else p.target.training.State(first).Wanted = false;
            if (!warmup) runner.NotifyArrival(); Tick(runner, warmup ? 1 : 120);
            Check(p.target.training.Requests.Single().project == second && p.ability.CooldownTicksRemaining == 60000, "cast locked initial project despite current valid successor");
        }
    }
    private static void DynamicNativeQualification()
    {
        foreach (bool successor in new[] { false, true })
        {
            Reset(); var p = OwnedPair(); var first = PetDogAbilityUtility.GetNextTraining(p.target); TrainableDef second = successor ? Project(p.target, "Haul") : null;
            var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); runner.NotifyArrival(); Tick(runner, 119); p.target.training.State(first).CanAssignToTrain = false; runner.Tick();
            if (successor) Check(p.target.training.Requests.Single().project == second && p.ability.CooldownTicksRemaining == 60000, "late qualification ignored next candidate");
            else { Check(runner.Ended, "late unsupported last project did not cancel"); NoTraining(p); }
        }
    }
    private static void FactionBranchIsolation()
    {
        foreach (bool beganOwned in new[] { false, true })
        foreach (bool warmup in new[] { false, true })
        {
            Reset(); var p = beganOwned ? OwnedPair() : Pair(); if (!beganOwned) Project(p.target, "Rescue");
            p.target.Position = new IntVec3(10, 10); var runner = new CastBoundaryRunner(p.caster, p.target, p.ability); if (warmup) { runner.NotifyArrival(); Tick(runner, 119); }
            p.target.Faction = beganOwned ? null : Faction.OfPlayer;
            Check(PetDogAbilityUtility.CanSelectTarget(p.caster, p.target, out _), "current job branch incorrectly blocks UI selection");
            if (warmup) Check(!Verb(p.ability).CanHitTargetFrom(p.caster.Position, p.target), "From allowed branch conversion");
            Check(!PetDogAbilityUtility.MatchesJobBranch(p.caster, p.target), "changed faction still matches persisted branch");
            runner.Tick(); Check(runner.Ended && p.caster.jobs.EndCondition == JobCondition.Incompletable, "job automatically changed faction branch"); NoTraining(p);
        }
    }
    private static void BranchGuardOwnership()
    {
        var p = OwnedPair(); p.caster.jobs.curJob = p.ability.GetJob(p.target, default); p.target.Faction = null;
        Check(!PetDogAbilityUtility.CanApply(p.caster, p.target, out _), "same target branch guard ignored");
        var other = Pawn(); other.RaceProps.Animal = true; other.RaceProps.Humanlike = false; other.Faction = null; other.Map = p.caster.Map;
        Check(PetDogAbilityUtility.MatchesJobBranch(p.caster, other) && PetDogAbilityUtility.CanApply(p.caster, other, out _), "old target job blocks another target");
        p.caster.jobs.curJob.ability = new Ability(p.caster, new AbilityDef { defName = "unrelated" });
        Check(PetDogAbilityUtility.MatchesJobBranch(p.caster, p.target), "unrelated ability imposes dog job faction guard");
        p.caster.jobs.curJob = null; Check(PetDogAbilityUtility.MatchesJobBranch(p.caster, p.target), "no job fails direct execution guard"); NoTraining(p);
    }
    private static void CompetingTraining()
    {
        foreach (bool nextProject in new[] { false, true })
        {
            Reset(); var p = OwnedPair(); var first = PetDogAbilityUtility.GetNextTraining(p.target); TrainableDef second = nextProject ? Project(p.target, "Rescue") : null;
            var rival = Pawn(); rival.Map = p.caster.Map; rival.Position = p.target.Position; rival.health.AddHediff(DogFinal); PetDogAbilityUtility.Maintain(rival);
            var rivalAbility = (Ability_PetDogTame)rival.abilities.GetAbility(SSCDefOf.SSC_PetDogTame); var a = new CastBoundaryRunner(p.caster, p.target, p.ability); var b = new CastBoundaryRunner(rival, p.target, rivalAbility);
            a.NotifyArrival(); b.NotifyArrival(); Tick(a, 119); Tick(b, 119); a.Tick(); b.Tick();
            Check(p.target.training.Requests[0].project == first && p.ability.CooldownStartCalls == 1, "first training competitor failed");
            if (nextProject) Check(p.target.training.Requests.Count == 2 && p.target.training.Requests[1].project == second && rivalAbility.CooldownStartCalls == 1 && p.target.mindState.interactionsToday == 2, "next project not selected at competitor effect");
            else Check(p.target.training.Requests.Count == 1 && b.Ended && rivalAbility.CooldownStartCalls == 0 && rivalAbility.ActivationCalls == 0 && p.target.mindState.interactionsToday == 1, "last-project competition double settled");
        }
    }
    private static void NativeTrainingFailure()
    {
        for (int scenario = 0; scenario < 3; scenario++)
        {
            Reset(); var p = OwnedPair(); var project = PetDogAbilityUtility.GetNextTraining(p.target); p.target.training.NativeTrainEffective = false;
            if (scenario == 1) p.target.training.OnTrain = (d, _, _) => p.target.training.State(d).Learned = true;
            if (scenario == 2) p.target.training.OnTrain = (d, _, _) => p.target.training.State(d).CanBeTrained = false;
            Check(!p.ability.Activate(p.target, default) && !Effect(p.ability).Succeeded && p.ability.CooldownTicksRemaining == 0, "third-party partial/no training consumed CD " + scenario);
            Check(p.target.training.Requests.Count == 1 && p.ability.ActivationCalls == 1 && p.ability.CooldownStartCalls == 1 && p.target.mindState.lastAssignedInteractTime == -100 && p.target.mindState.interactionsToday == 0, "failed train updated scheduling or attempted twice");
            p.target.training.OnTrain = null; p.target.training.NativeTrainEffective = true; p.target.training.State(project).CanBeTrained = true;
            Check(p.ability.Activate(p.target, default) && p.target.training.Requests.Count == 2 && p.ability.CooldownTicksRemaining == 60000 && p.target.mindState.interactionsToday == 1, "no-effect training cannot retry immediately");
        }
    }
    private static void TrainingResources()
    {
        var job = Definition("Defs/JobDefs/SSC_PetDogTameJobDefs.xml", "SSC_Job_PetDogTrain");
        Check((string)job.Element("driverClass") == typeof(JobDriver_PetDogTame).FullName && (string)job.Element("abilityCasting") == "false" && (string)job.Element("collideWithPawns") == "false", "owned job lost same driver/native-cooldown safeguards");
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            string keyed = Path.Combine(root, "Languages", language, "Keyed"); var files = Directory.EnumerateFiles(keyed, "*.xml").Where(f => XDocument.Load(f).Descendants().Any(e => e.Name.LocalName == "SSC_PetDogTameTrainingTargetHint")).ToArray();
            var keys = files.SelectMany(f => XDocument.Load(f).Root.Elements()).ToArray();
            foreach (string suffix in new[] { "NoTrainingTarget", "TrainingTargetHint", "TrainingSuccess" })
            {
                var value = keys.Single(e => e.Name.LocalName == "SSC_PetDogTame" + suffix).Value; Check(!string.IsNullOrWhiteSpace(value), "empty training key " + language + suffix);
                if (suffix != "NoTrainingTarget") Check(value.Contains("{0}") && value.Contains("{1}"), "training animal/project placeholders missing " + language + suffix);
            }
            var jobs = Directory.EnumerateFiles(Path.Combine(root, "Languages", language, "DefInjected/JobDef"), "*.xml").Where(f => XDocument.Load(f).Descendants().Any(e => e.Name.LocalName == "SSC_Job_PetDogTrain.reportString")).ToArray();
            Check(jobs.Length == 1 && !string.IsNullOrWhiteSpace(XDocument.Load(jobs.Single()).Descendants("SSC_Job_PetDogTrain.reportString").Single().Value), "training job report missing " + language);
            foreach (string file in files.Concat(jobs)) Check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Sexslavecraft", Path.GetRelativePath(root, file)))), "training language mirror differs " + file);
        }
    }
    private static void NativeTrainingPostconditions()
    {
        foreach (bool replaceTracker in new[] { false, true })
        {
            Reset(); var p = OwnedPair(); var original = p.target.training;
            original.OnTrain = (_, _, _) => { if (replaceTracker) p.target.training = new Pawn_TrainingTracker(); else p.target.Faction = new Faction(); };
            Check(!p.ability.Activate(p.target, default) && !Effect(p.ability).Succeeded && p.ability.CooldownTicksRemaining == 0 && original.Requests.Count == 1, "post-train identity/faction change consumed CD");
            Check(p.target.mindState.lastAssignedInteractTime == -100 && p.target.mindState.interactionsToday == 0 && Messages.Requests.Count == 0, "post-train identity/faction failure emitted success or advanced scheduling");
        }
        Reset(); var noMind = OwnedPair(); noMind.target.mindState = null;
        Check(noMind.ability.Activate(noMind.target, default) && noMind.target.training.Requests.Count == 1 && noMind.ability.CooldownTicksRemaining == 60000, "missing optional MindState blocks successful native train");
    }
}
