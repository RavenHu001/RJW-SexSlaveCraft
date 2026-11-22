// AI 辅助生成。使用原版结束任务后的同步找工及生产 WorkGiver，夹具提供地图、研究和身体资格。
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static ThinkTreeDef combatGateThinkTree;

    private static void DailyTrainingCombatChecks()
    {
        Check("drafting the trainer stops same-tick daily training reassignment", () => CheckCombatReassignment(true));
        Check("drafting the receiver stops same-tick daily training reassignment", () => CheckCombatReassignment(false));
    }

    private static void CheckCombatReassignment(bool draftTrainer)
    {
        var boundary = new Harmony(environment.Id + ".combat-gate");
        var trainingDef = SSCDefOf.TrainingSexSlave;
        var combatWaitDef = JobDefOf.Wait_Combat;
        bool protectionEnabled = SSCMod.settings.enableSexSlaveProtectionRules;
        try
        {
            boundary.Patch(AccessTools.PropertyGetter(typeof(Pawn), nameof(Verse.Pawn.IsColonist)),
                prefix: new HarmonyMethod(typeof(Program), nameof(True)));
            boundary.Patch(AccessTools.Method(typeof(ResearchUtils), nameof(ResearchUtils.IsResearchFinished),
                new[] { typeof(ResearchProjectDef) }), prefix: new HarmonyMethod(typeof(Program), nameof(True)));
            boundary.Patch(AccessTools.Method(typeof(Trainjudge), nameof(Trainjudge.TryCanBeFuckedWithReason),
                new[] { typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(Program), nameof(True)));
            boundary.Patch(AccessTools.Method(typeof(LifeForceConflictUtility), nameof(LifeForceConflictUtility.TryRemoveLifeForceGeneIfConflicting)),
                prefix: new HarmonyMethod(typeof(Program), nameof(False)));
            boundary.Patch(AccessTools.Method(typeof(Translator), nameof(Translator.Translate), new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(Program), nameof(OwnerlessRitualReason)));
            foreach (string name in new[] { nameof(Pawn_Thinker.MainThinkTree), nameof(Pawn_Thinker.ConstantThinkTree) })
                boundary.Patch(AccessTools.PropertyGetter(typeof(Pawn_Thinker), name),
                    prefix: new HarmonyMethod(typeof(Program), nameof(UseCombatGateThinkTree)));

            SSCMod.settings.enableSexSlaveProtectionRules = false;
            SSCDefOf.TrainingSexSlave = new JobDef { defName = "SSC_Training_SexSlave", driverClass = typeof(JobDriver_Training) };
            JobDefOf.Wait_Combat ??= new JobDef { defName = "Wait_Combat" };
            var (trainer, receiver, driver) = Pair(started: false);
            var faction = Bare<Faction>();
            trainer.SetFactionDirect(faction);
            receiver.SetFactionDirect(faction);
            receiver.stackCount = 1;
            receiver.jobs.curJob = null;
            receiver.jobs.curDriver = null;
            var comp = receiver.TryGetComp<CompSexSlaveTraining>();
            comp.mode = TrainingMode.Enabled;
            comp.ReplaceAssignedTrainers(new[] { trainer });
            map.reservationManager = new ReservationManager(map);
            map.physicalInteractionReservationManager = new PhysicalInteractionReservationManager();
            var work = new WorkGiver_Training();
            Require(work.HasJobOnThing(trainer, receiver), "the pair must be eligible before drafting");

            var scan = new CombatGateWorkScan(work, receiver);
            combatGateThinkTree = new ThinkTreeDef { thinkRoot = scan };
            trainer.thinker = new Pawn_Thinker(trainer);
            Pawn draftedPawn = draftTrainer ? trainer : receiver;
            draftedPawn.drafter = new Pawn_DraftController(draftedPawn);
            // 从 UAP 调用 EndCurrentJob 时的状态开始，征召按钮和动画终止留给实机复测。
            Set(draftedPawn.drafter, "draftedInt", true);
            int tick = Find.TickManager.TicksGame;
            trainer.jobs.EndCurrentJob(JobCondition.Incompletable);
            Require(cleanups == 1 && trainer.CurJob == null);
            Require(scan.Attempts == 1 && scan.Candidate == null && Find.TickManager.TicksGame == tick,
                "synchronous work search created a replacement training job");
            Require(work.JobOnThing(trainer, receiver, forced: true) == null);
            Require(!driver.TryMakePreToilReservations(false), "reservation accepted the drafted pair");

            Set(draftedPawn.drafter, "draftedInt", false);
            draftedPawn.jobs.curJob = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            Require(!work.HasJobOnThing(trainer, receiver) && work.JobOnThing(trainer, receiver, forced: true) == null,
                "combat job must also block dispatch");
            draftedPawn.jobs.curJob = null;
            Require(work.JobOnThing(trainer, receiver)?.def == SSCDefOf.TrainingSexSlave,
                "training must become eligible again after leaving combat");
            Require(!comp.IsOnCooldown && !comp.IsWaitingAfterFailedValidation);
        }
        finally
        {
            boundary.UnpatchAll(boundary.Id);
            combatGateThinkTree = null;
            SSCDefOf.TrainingSexSlave = trainingDef;
            JobDefOf.Wait_Combat = combatWaitDef;
            SSCMod.settings.enableSexSlaveProtectionRules = protectionEnabled;
        }
    }

    private static bool UseCombatGateThinkTree(MethodBase __originalMethod, ref ThinkTreeDef __result)
    {
        __result = __originalMethod.Name == "get_MainThinkTree" ? combatGateThinkTree : null;
        return false;
    }

    private sealed class CombatGateWorkScan(WorkGiver_Training work, Pawn receiver) : ThinkNode
    {
        public int Attempts { get; private set; }
        public Job Candidate { get; private set; }

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            Attempts++;
            Candidate = work.ShouldSkip(pawn) ? null : work.JobOnThing(pawn, receiver);
            // 记录结果后隔离安装终端。生成任何新调教 Job 即判为失败。
            return ThinkResult.NoJob;
        }
    }
}
