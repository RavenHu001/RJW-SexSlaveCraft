using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    public class StageEndTrigger_PawnJobFinished : StageEndTrigger
    {
        public string roleId;
        public JobDef jobDef;

        // 【关键修复】把 protected 换成了 public，完全对齐原版底层的要求
        public override Trigger MakeTrigger(LordJob_Ritual ritual, TargetInfo spot, IEnumerable<TargetInfo> pawns, RitualStage stage)
        {
            // 监听到信号就结束当前阶段
            return new Trigger_Memo("SSC_Training_Finished");
        }
    }
}