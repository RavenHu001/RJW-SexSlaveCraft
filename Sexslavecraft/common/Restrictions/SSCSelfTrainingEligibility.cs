using Verse;

namespace SexSlaveCraft
{
    /// <summary>自我调教的不可绕过资格；许可和任务入口共用。</summary>
    public static class SSCSelfTrainingEligibility
    {
        public static bool IsEligible(Pawn pawn)
        {
            return SSCIdentityUtility.IsSexSlave(pawn) && SSCBondUtility.GetChain(pawn) != null;
        }
    }
}
