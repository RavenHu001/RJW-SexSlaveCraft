using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public class JobGiver_SlaveKeepJob : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // 如果 slave 当前正在执行 TrainingReceiver job，不要中断
            if (pawn.CurJobDef == SSCDefOf.SSC_TrainingReceiver)
                return pawn.CurJob;

            // 否则让 slave 原地等待
            return JobMaker.MakeJob(JobDefOf.Wait_WithSleeping, 300, true);
        }
    }
}