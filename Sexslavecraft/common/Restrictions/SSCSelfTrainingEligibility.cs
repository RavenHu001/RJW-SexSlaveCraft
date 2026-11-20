using Verse;

namespace SexSlaveCraft
{
    /// <summary>自我调教的不可绕过资格；许可和任务入口共用。</summary>
    public static class SSCSelfTrainingEligibility
    {
        /// <summary>判断进度载体是否提供自我调教资格。任务另查身体、目标地点和实际行为许可。</summary>
        public static bool IsEligible(Pawn pawn)
        {
#if SSC_RW17
            if (Hediff_SSCOwnerlessSlave.GetActiveStage(pawn) > 0) return true;
#endif
            return SSCIdentityUtility.IsSexSlave(pawn) && SSCBondUtility.GetChain(pawn) != null;
        }
    }
}
