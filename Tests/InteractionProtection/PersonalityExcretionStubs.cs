using System.Linq;
using System.Runtime.CompilerServices;
using SexSlaveCraft;

namespace Verse.AI
{
    public partial class Pawn_JobTracker
    {
        public int AutomaticReplacements;

        // RimWorld 1.6 的两个自动入口均先调用同一个私有判断，然后替换或回收候选。
        // 此模型只使用不同 JobDef 的候选及当前 Job，不模拟 IsContinuation 分支。
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ShouldStartJobFromThinkTree(ThinkResult thinkResult)
        {
            bool result = curDriver == null || curDriver.job != thinkResult.Job;
#if !REAL_HARMONY
            PersonalityExcretionJobProtection.Postfix(pawn, ref result);
#endif
            return result;
        }

        public bool CheckForJobOverride(Job candidate) => ApplyThinkTreeCandidate(candidate);
        public bool ConstantThinkTreeTick(Job candidate) => ApplyThinkTreeCandidate(candidate);

        private bool ApplyThinkTreeCandidate(Job candidate)
        {
            if (ShouldStartJobFromThinkTree(new ThinkResult(candidate, null)))
            {
                EndCurrentJob(JobCondition.Incompletable, false);
                curDriver = new JobDriver { pawn = pawn, job = candidate };
                AutomaticReplacements++;
                return true;
            }
            if (candidate != curDriver?.job && !jobQueue.Any(q => q.job == candidate))
                JobMaker.ReturnToPool(candidate);
            return false;
        }
    }
}
