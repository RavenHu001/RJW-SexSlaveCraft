// AI 辅助生成：一次发作的冷却及满值入口回落记录随 Pawn 存档。
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        internal long SlaveBreakdownCooldownUntil;
        internal bool SlaveBreakdownFullTriggerUsed;

        private void ExposeSlaveBreakdown()
        {
            Scribe_Values.Look(ref SlaveBreakdownCooldownUntil, "sscSlaveBreakdownCooldownUntil", 0L);
            Scribe_Values.Look(ref SlaveBreakdownFullTriggerUsed, "sscSlaveBreakdownFullTriggerUsed", false);
        }
    }
}
