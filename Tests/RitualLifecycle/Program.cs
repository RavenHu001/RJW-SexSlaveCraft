using System;
using System.Linq;
using SexSlaveCraft;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

// 测试只构造角色、Lord 和存档恢复后的字段；所有状态判断、阶段推进与清理均调用生产源码。
internal static partial class Program
{
    private static int cases;
    private static int failures;

    /// <summary>重置测试世界后运行单个用例，累计结果并报告异常，使失败不会阻止后续用例执行。</summary>
    private static void Check(string name, Action test)
    {
        cases++;
        TestWorld.Reset();
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception error)
        {
            failures++;
            Console.WriteLine("FAIL " + name + ": " + error.Message);
        }
    }

    /// <summary>断言条件为真；失败时抛出包含指定原因的异常，由用例执行器记录。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    /// <summary>比较预期值与实际值；不相等时报告字段名称及两侧值。</summary>
    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!Equals(expected, actual)) throw new Exception($"{label}: expected {expected}, got {actual}");
    }

    /// <summary>断言仪式占用、训练占用、阶段、Lord 归属和结算资格均已复位。</summary>
    private static void Cleared(CompSexSlaveTraining comp)
    {
        Require(!comp.isRitualTraining, "ritual flag remains set");
        Require(!comp.isBeingTrained, "training flag remains set");
        Equal(0, comp.ritualPhase, "phase after cleanup");
        Equal<Lord>(null, comp.bindingRitualLord, "owner after cleanup");
        Require(!comp.bindingRitualOutcomeClaimed, "outcome claim remains set");
    }

    /// <summary>通过生产状态管理器模拟指定数量的完整阶段，每一步都要求成功开始并推进。</summary>
    private static void Advance(RitualFixture fixture, int count)
    {
        for (int phase = 0; phase < count; phase++)
        {
            Require(BindingRitualStateUtility.TryBeginPhase(fixture.Master, fixture.Slave, fixture.Lord),
                "phase should begin");
            Require(BindingRitualStateUtility.TryCompletePhase(fixture.Master, fixture.Slave, fixture.Lord),
                "finished phase should advance");
        }
    }

    /// <summary>运行全部仪式生命周期回归用例；全部通过返回 0，存在失败返回 1，供发行脚本判断。</summary>
    private static int Main()
    {
        RunStage3BTests();
        RunEducationCompatibilityTests();
        Check("cancelling while walking cleans state without executing the future scene finish", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            Require(ritual.Comp.isRitualTraining, "job giver did not acquire ritual occupancy");
            Equal(0, execution.CurrentToil.FinishActions.Count, "walking toil scene finish actions");
            ritual.End();
            execution.FinishCurrent(JobCondition.InterruptForced);
            Cleared(ritual.Comp);
            Equal(0, TestWorld.ProcessSexCalls, "scene processing while walking");
            Equal(0, ritual.Lord.ReceivedMemos.Count, "completion memos");
        });

        Check("global job finish repairs a removed lord during walking even without the cleanup patch", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            ritual.RemoveLordWithoutCleanup();
            execution.FinishCurrent(JobCondition.InterruptForced);
            Cleared(ritual.Comp);
        });

        Check("cancelling during the scene does not process or advance the interrupted phase", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            ritual.End();
            execution.FinishCurrent(JobCondition.InterruptForced);
            Cleared(ritual.Comp);
            Equal(0, TestWorld.ProcessSexCalls, "aborted scene processing");
            Equal(1, TestWorld.RjwEndCalls, "started scene cleanup");
            Equal(0, ritual.Lord.ReceivedMemos.Count, "aborted scene completion memos");
        });

        Check("interrupting one job in a still active ritual preserves the phase for retry", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 2);
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            execution.FinishCurrent(JobCondition.InterruptForced);
            Equal(2, ritual.Comp.ritualPhase, "interrupted phase count");
            Require(ritual.Comp.isRitualTraining, "active ritual occupancy was released");
            Equal(0, TestWorld.ProcessSexCalls, "incomplete scene processing");
        });

        Check("a production phase driver advances once and retains active ritual occupancy", () =>
        {
            var ritual = new RitualFixture();
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            execution.ReachPhaseEnd();
            execution.FinishCurrent(JobCondition.Succeeded);
            execution.FinishCurrent(JobCondition.Succeeded);
            Equal(1, ritual.Comp.ritualPhase, "phase after repeated finish");
            Require(ritual.Comp.isRitualTraining, "phase completion released ritual occupancy");
            Equal(1, TestWorld.ProcessSexCalls, "phase processing count");
            Equal(0, ritual.Lord.ReceivedMemos.Count, "premature completion memo");
        });

        Check("final memo can synchronously cleanup and reenter the phase finish without duplicate effects", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 5);
            var execution = new PhaseExecution(ritual);
            execution.StartScene();
            int claimedOutcomes = 0;
            ritual.Lord.MemoReceived = memo =>
            {
                Equal("SSC_Training_Finished", memo, "completion memo");
                if (BindingRitualStateUtility.TryClaimOutcome(ritual.Job)) claimedOutcomes++;
                ritual.End();
                execution.FinishCurrent(JobCondition.InterruptForced);
            };
            execution.ReachPhaseEnd();
            execution.FinishCurrent(JobCondition.Succeeded);
            Cleared(ritual.Comp);
            Equal(1, claimedOutcomes, "outcome eligibility claims");
            Equal(1, TestWorld.ProcessSexCalls, "final scene processing count");
            Equal(1, TestWorld.RjwEndCalls, "final scene cleanup count");
            Equal(1, ritual.Lord.ReceivedMemos.Count, "final memo count");
        });

        Check("a delayed old driver finish cannot clean the new ritual or its scene", () =>
        {
            var previous = new RitualFixture();
            var oldExecution = new PhaseExecution(previous);
            oldExecution.StartScene();
            oldExecution.ReachPhaseEnd();
            previous.End();
            var current = new RitualFixture(previous.Master, previous.Slave);
            Advance(current, 2);
            oldExecution.FinishCurrent(JobCondition.InterruptForced);
            Equal(2, current.Comp.ritualPhase, "new ritual phase");
            Equal(current.Lord, current.Comp.bindingRitualLord, "new ritual owner");
            Require(current.Comp.isRitualTraining, "new ritual occupancy was released");
            Equal(0, TestWorld.ProcessSexCalls, "old scene processing after new ritual starts");
            Equal(0, TestWorld.RjwEndCalls, "old driver cleanup against new scene");
        });

        Check("enumerating a loaded legacy driver leaves migration to its first runtime validity check", () =>
        {
            var ritual = new RitualFixture();
            ritual.Comp.isRitualTraining = true;
            ritual.Comp.isBeingTrained = true;
            ritual.Comp.ritualPhase = 3;
            var driver = new JobDriver_RitualTraining
            {
                pawn = ritual.Master,
                job = JobMaker.MakeJob(SSCDefOf.Training_Ritual, ritual.Slave)
            };
            driver.CreateToilsForTest().ToList();
            Equal<Lord>(null, ritual.Comp.bindingRitualLord, "owner during load-time toil enumeration");
            Equal(3, ritual.Comp.ritualPhase, "phase during load-time toil enumeration");
            Require(!driver.FailConditions.Any(condition => condition()), "valid restored driver was rejected");
            Equal(ritual.Lord, ritual.Comp.bindingRitualLord, "legacy owner after first runtime check");
            Equal(3, ritual.Comp.ritualPhase, "legacy phase after first runtime check");
        });

        Check("normal phase changes retain ownership and ritual occupancy", () =>
        {
            var ritual = new RitualFixture();
            for (int phase = 1; phase <= 5; phase++)
            {
                Advance(ritual, 1);
                Equal(phase, ritual.Comp.ritualPhase, "phase");
                Equal(ritual.Lord, ritual.Comp.bindingRitualLord, "owner");
                Require(ritual.Comp.isRitualTraining, "phase change released ritual occupancy");
                Require(!BindingRitualStateUtility.TryClaimOutcome(ritual.Job), "incomplete ritual received outcome");
            }
        });

        Check("six completed phases allow one outcome and then cleanup", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 6);
            Equal(6, ritual.Comp.ritualPhase, "completed phase count");
            Require(BindingRitualStateUtility.TryClaimOutcome(ritual.Job), "completed ritual cannot claim outcome");
            Require(!BindingRitualStateUtility.TryClaimOutcome(ritual.Job), "outcome claimed twice");
            ritual.End();
            Cleared(ritual.Comp);
            Require(!BindingRitualStateUtility.TryClaimOutcome(ritual.Job), "ended ritual regained outcome eligibility");
        });

        Check("another phase cannot restart a ritual after phase six", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 6);
            Require(!BindingRitualStateUtility.TryBeginPhase(ritual.Master, ritual.Slave, ritual.Lord),
                "completed ritual restarted");
            Require(!BindingRitualStateUtility.TryCompletePhase(ritual.Master, ritual.Slave, ritual.Lord),
                "completed ritual advanced beyond phase six");
            Equal(6, ritual.Comp.ritualPhase, "completed phase count");
            Equal<Job>(null, new JobGiver_RitualBinding().GiveJobForTest(ritual.Master),
                "job giver restarted completed ritual");
            Equal(6, ritual.Comp.ritualPhase, "phase after rejected extra job");
        });

        Check("missing ritual job definition does not arm occupancy", () =>
        {
            var ritual = new RitualFixture();
            SSCDefOf.Training_Ritual = null;
            Equal<Job>(null, new JobGiver_RitualBinding().GiveJobForTest(ritual.Master),
                "job issued without its definition");
            Cleared(ritual.Comp);
        });

        Check("an unreachable target does not arm ritual occupancy", () =>
        {
            var ritual = new RitualFixture();
            ritual.Master.Reachable = false;
            Equal<Job>(null, new JobGiver_RitualBinding().GiveJobForTest(ritual.Master),
                "job issued with unreachable target");
            Cleared(ritual.Comp);
        });

        Check("cancellation between phases releases occupancy without an outcome", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 3);
            ritual.End();
            Cleared(ritual.Comp);
            Require(!BindingRitualStateUtility.TryClaimOutcome(ritual.Job), "cancelled ritual received outcome");
        });

        Check("cleanup preserves configured trainer, progress and cooldown", () =>
        {
            var ritual = new RitualFixture();
            ritual.Comp.selectedTrainer = ritual.Master;
            ritual.Comp.specializationProgress = 0.73f;
            ritual.Comp.lastTrainingTick = 12345;
            Advance(ritual, 3);
            ritual.End();
            ritual.End();
            Cleared(ritual.Comp);
            Equal(ritual.Master, ritual.Comp.selectedTrainer, "configured trainer");
            Equal(0.73f, ritual.Comp.specializationProgress, "specialization progress");
            Equal(12345, ritual.Comp.lastTrainingTick, "daily cooldown");
        });

        Check("simultaneous rituals have independent completion gates", () =>
        {
            var complete = new RitualFixture();
            var incomplete = new RitualFixture();
            Advance(complete, 6);
            Advance(incomplete, 2);
            Require(!BindingRitualStateUtility.TryClaimOutcome(incomplete.Job), "other ritual borrowed success");
            incomplete.End();
            Require(BindingRitualStateUtility.TryClaimOutcome(complete.Job), "unrelated cleanup erased success");
            Equal(6, complete.Comp.ritualPhase, "unrelated ritual phase");
        });

        Check("a new ritual starts at phase zero after previous cancellation", () =>
        {
            var previous = new RitualFixture();
            Advance(previous, 3);
            previous.End();
            var current = new RitualFixture(previous.Master, previous.Slave);
            Require(BindingRitualStateUtility.TryBeginPhase(current.Master, current.Slave, current.Lord),
                "new ritual cannot begin");
            Equal(0, current.Comp.ritualPhase, "new ritual initial phase");
            Equal(current.Lord, current.Comp.bindingRitualLord, "new ritual owner");
        });

        Check("a new ritual discards stale previous progress even if its cleanup callback was missed", () =>
        {
            var previous = new RitualFixture();
            Advance(previous, 3);
            previous.RemoveLordWithoutCleanup();
            var current = new RitualFixture(previous.Master, previous.Slave);
            var execution = new PhaseExecution(current);
            Equal(0, current.Comp.ritualPhase, "new initial phase after missing old cleanup");
            Equal(current.Lord, current.Comp.bindingRitualLord, "new owner after missing old cleanup");
        });

        Check("late completion and cleanup from old ritual preserve the new ritual", () =>
        {
            var previous = new RitualFixture();
            Advance(previous, 3);
            previous.End();
            var current = new RitualFixture(previous.Master, previous.Slave);
            Advance(current, 2);
            Require(!BindingRitualStateUtility.TryCompletePhase(previous.Master, previous.Slave, previous.Lord),
                "old job advanced new ritual");
            previous.End();
            Equal(2, current.Comp.ritualPhase, "new ritual phase after stale callback");
            Equal(current.Lord, current.Comp.bindingRitualLord, "new ritual owner after stale callback");
            Require(current.Comp.isRitualTraining, "old cleanup released new ritual");
        });

        Check("legacy orphan flags are repaired even when a receiver job remains", () =>
        {
            var pawn = TestWorld.NewPawn();
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            comp.isRitualTraining = true;
            comp.isBeingTrained = true;
            comp.ritualPhase = 4;
            TestWorld.AssignReceiverJob(pawn);
            BindingRitualStateUtility.RecoverPawnState(pawn);
            Cleared(comp);
        });

        Check("valid legacy ritual is adopted without resetting its phase", () =>
        {
            var ritual = new RitualFixture();
            ritual.Comp.isRitualTraining = true;
            ritual.Comp.isBeingTrained = true;
            ritual.Comp.ritualPhase = 3;
            BindingRitualStateUtility.RecoverPawnState(ritual.Slave);
            Equal(3, ritual.Comp.ritualPhase, "legacy active phase");
            Equal(ritual.Lord, ritual.Comp.bindingRitualLord, "adopted owner");
            Require(ritual.Comp.isRitualTraining, "valid legacy occupancy cleared");
        });

        Check("loaded active ritual waiting between jobs keeps its phase", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 3);
            TestWorld.ClearCurrentJob(ritual.Master);
            TestWorld.ClearCurrentJob(ritual.Slave);
            BindingRitualStateUtility.RecoverPawnState(ritual.Slave);
            Equal(3, ritual.Comp.ritualPhase, "restored active phase");
            Equal(ritual.Lord, ritual.Comp.bindingRitualLord, "restored active owner");
            Require(ritual.Comp.isRitualTraining, "valid loaded ritual cleared while waiting");
        });

        Check("stale owner is repaired after its lord disappears", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 2);
            ritual.RemoveLordWithoutCleanup();
            BindingRitualStateUtility.RecoverPawnState(ritual.Slave);
            Cleared(ritual.Comp);
        });

        Check("ordinary daily training is not cleared by ritual recovery", () =>
        {
            var pawn = TestWorld.NewPawn();
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            comp.isBeingTrained = true;
            BindingRitualStateUtility.RecoverPawnState(pawn);
            Require(comp.isBeingTrained, "daily training was interrupted by ritual recovery");
        });

        Check("repairing an old completed phase counter preserves current daily training", () =>
        {
            var pawn = TestWorld.NewPawn();
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            comp.ritualPhase = 6;
            comp.isBeingTrained = true;
            BindingRitualStateUtility.RecoverPawnState(pawn);
            Equal(0, comp.ritualPhase, "old completed phase counter");
            Require(comp.isBeingTrained, "current daily training was cleared with old phase residue");
        });

        foreach (string role in new[] { "master", "slave" })
        {
            Check($"removing the {role} cleans the target before its role entry is lost", () =>
            {
                var ritual = new RitualFixture();
                Advance(ritual, 3);
                ritual.RemoveParticipant(ritual.Job.PawnWithRole(role));
                Cleared(ritual.Comp);
                Require(!BindingRitualStateUtility.TryClaimOutcome(ritual.Job), "lost participant received outcome");
            });
        }

        Check("a spectator leaving does not cancel the assigned participants", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 2);
            Pawn spectator = TestWorld.NewPawn();
            ritual.Lord.ownedPawns.Add(spectator);
            spectator.lord = ritual.Lord;
            ritual.RemoveParticipant(spectator);
            Equal(2, ritual.Comp.ritualPhase, "phase after spectator leaves");
            Require(ritual.Comp.isRitualTraining, "spectator departure cancelled participants");
        });

        Check("cleanup finds the owned target even if its role assignment was already removed", () =>
        {
            var ritual = new RitualFixture();
            Advance(ritual, 2);
            ritual.Job.Roles.Remove("slave");
            ritual.End();
            Cleared(ritual.Comp);
        });

        Check("cleanup of an unrelated ritual preserves binding state owned by another lord", () =>
        {
            var binding = new RitualFixture();
            Advance(binding, 2);
            var unrelated = new RitualFixture(binding.Master, binding.Slave, bindingRitual: false);
            unrelated.End();
            Equal(2, binding.Comp.ritualPhase, "binding phase after unrelated cleanup");
            Equal(binding.Lord, binding.Comp.bindingRitualLord, "binding owner after unrelated cleanup");
            Require(binding.Comp.isRitualTraining, "unrelated ritual cleared binding state");
        });

        Check("an unrelated ritual cannot acquire binding state", () =>
        {
            var ritual = new RitualFixture(bindingRitual: false);
            Require(!BindingRitualStateUtility.IsBindingRitual(ritual.Job), "unrelated ritual misidentified");
            Require(!BindingRitualStateUtility.TryBeginPhase(ritual.Master, ritual.Slave, ritual.Lord),
                "unrelated ritual acquired binding state");
            Cleared(ritual.Comp);
        });

        RunUapCompatibilityTests();
        Console.WriteLine($"{cases - failures}/{cases} lifecycle tests passed.");
        return failures == 0 ? 0 : 1;
    }
}
