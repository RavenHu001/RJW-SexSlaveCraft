using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    public class RitualStage_InteractWithSlave : RitualStage
    {
        public override TargetInfo GetSecondFocus(LordJob_Ritual ritual)
        {
            // 只要找到被分配为 slave 的小人即可
            return ritual.assignments.AssignedPawns("slave").FirstOrDefault();
        }
    }
}