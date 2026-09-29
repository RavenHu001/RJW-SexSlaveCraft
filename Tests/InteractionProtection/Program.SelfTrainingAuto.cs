using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void RunSelfTrainingAutoTests()
    {
        Run("自动分流四种可用性仅双分支掷骰", AutoChoiceUsesOneRollOnly);
        Run("自动候选只在 RJW 生成器内替换并保留目标与来源", AutoCandidateReplacement);
        Run("普通与自我调教均可用时按固定随机值选择", AutoCandidateBothAvailable);
        Run("仅普通可用保留候选；均不可用交给守卫回收", AutoCandidateOtherCombinations);
        Run("无候选、非 RJW 来源和队列结果不会被分流", AutoCandidateSourceBoundary);
    }

    private static Pawn AutoPawn()
    {
        Pawn pawn = People().b;
        pawn.Training.pawnIdentity = PawnIdentity.Slave;
        pawn.Training.restrictionConfig.rules.allowSelfTraining = true;
        return pawn;
    }

    private static Job AutoCandidate(Pawn pawn, Thing bed = null)
    {
        return new Job
        {
            def = xxx.Masturbate,
            targetA = new LocalTargetInfo { Thing = pawn },
            targetB = new LocalTargetInfo { Thing = bed },
            targetC = new LocalTargetInfo { Cell = new IntVec3 { X = 7 } }
        };
    }

    private static void AutoChoiceUsesOneRollOnly()
    {
        int rolls = 0;
        System.Func<float> roll = () => { rolls++; return 0.29f; };
        Assert(SSCSelfTrainingAutoJob.Select(false, false, 0.30f, roll) == SSCSelfTrainingAutoChoice.None,
            "两者均不可用应放弃");
        Assert(SSCSelfTrainingAutoJob.Select(true, false, 0.30f, roll) == SSCSelfTrainingAutoChoice.Masturbation,
            "仅普通可用应保留");
        Assert(SSCSelfTrainingAutoJob.Select(false, true, 0.30f, roll) == SSCSelfTrainingAutoChoice.SelfTraining,
            "仅自我调教可用应直接选择");
        Assert(rolls == 0, "单分支不能耗用随机值");
        foreach (float chance in new[] { 0.30f, 0.55f, 0.80f, 0.50f })
        {
            Assert(SSCSelfTrainingAutoJob.Select(true, true, chance, () => { rolls++; return chance - 0.01f; })
                == SSCSelfTrainingAutoChoice.SelfTraining, "概率内应分流");
            Assert(SSCSelfTrainingAutoJob.Select(true, true, chance, () => { rolls++; return chance; })
                == SSCSelfTrainingAutoChoice.Masturbation, "概率边界应保留普通候选");
        }
        Assert(rolls == 8, "每个双分支候选只能掷一次");
    }

    private static void AutoCandidateReplacement()
    {
        Pawn pawn = AutoPawn();
        Thing bed = new Thing();
        Job original = AutoCandidate(pawn, bed);
        var giver = new JobGiver_Masturbate { Candidate = original, Tag = JobTag.Misc };
        Rand.Calls = 0;
        ThinkResult result = giver.TryIssueJobPackage(pawn);
        Assert(result.Job != null && result.Job != original && result.Job.def == SSCDefOf.SelfTraining,
            "普通自慰关闭而自我调教开启应直接替换");
        Assert(result.Job.targetA.Pawn == pawn && result.Job.targetB.Thing == bed && result.Job.targetC.Cell.X == 7,
            "应保留本人、床和地点");
        Assert(result.SourceNode == giver && result.Tag == JobTag.Misc && !result.FromQueue,
            "应保留思考树结果元数据");
        Assert(Rand.Calls == 0 && JobMaker.Made == 1 && JobMaker.LastReturned == original &&
            pawn.ClearedReservations == 1, "直接选择无需掷骰，旧候选只清理一次");
    }

    private static void AutoCandidateBothAvailable()
    {
        Pawn pawn = AutoPawn();
        pawn.Training.restrictionConfig.rules.allowMasturbation = true;
        pawn.AutoSelfTrainingChance = 0.55f;
        Rand.Calls = 0; Rand.NextValue = 0.54f;
        Job first = AutoCandidate(pawn);
        ThinkResult diverted = new JobGiver_Masturbate { Candidate = first }.TryIssueJobPackage(pawn);
        Assert(diverted.Job.def == SSCDefOf.SelfTraining && Rand.Calls == 1 && JobMaker.LastReturned == first,
            "随机值低于当前阶段概率应替换一次");
        Rand.Calls = 0; Rand.NextValue = 0.55f;
        Job second = AutoCandidate(pawn);
        ThinkResult ordinary = new JobGiver_Masturbate { Candidate = second }.TryIssueJobPackage(pawn);
        Assert(ordinary.Job == second && Rand.Calls == 1 && JobMaker.LastReturned == first,
            "达到概率边界时应保留原任务，不清理新候选");
    }

    private static void AutoCandidateOtherCombinations()
    {
        Pawn pawn = AutoPawn();
        pawn.CanSelfTrain = false;
        pawn.Training.restrictionConfig.rules.allowMasturbation = true;
        Job ordinary = AutoCandidate(pawn);
        Rand.Calls = 0;
        Assert(new JobGiver_Masturbate { Candidate = ordinary }.TryIssueJobPackage(pawn).Job == ordinary &&
            Rand.Calls == 0, "仅普通可用时不掷骰、不替换");
        pawn.Training.restrictionConfig.rules.allowMasturbation = false;
        Job denied = AutoCandidate(pawn);
        ThinkResult none = new JobGiver_Masturbate { Candidate = denied }.TryIssueJobPackage(pawn);
        Assert(none.Job == null && Rand.Calls == 0 && JobMaker.LastReturned == denied &&
            pawn.ClearedReservations == 1, "均不可用时统一守卫只回收一次");
    }

    private static void AutoCandidateSourceBoundary()
    {
        Pawn pawn = AutoPawn();
        Rand.Calls = 0;
        Assert(new JobGiver_Masturbate().TryIssueJobPackage(pawn).Job == null && Rand.Calls == 0,
            "RJW 上游无候选时不补发任务");
        Job unrelated = AutoCandidate(pawn);
        Assert(new ThinkNode_JobGiver { Candidate = unrelated }.TryIssueJobPackage(pawn).Job == null &&
            Rand.Calls == 0 && JobMaker.Made == 0, "其他生成器的同名候选不分流");
        Job queued = AutoCandidate(pawn);
        Assert(new JobGiver_Masturbate { Candidate = queued, FromQueue = true }.TryIssueJobPackage(pawn).Job == null &&
            Rand.Calls == 0 && JobMaker.Made == 0, "队列结果不作为新自动候选分流");
    }
}
