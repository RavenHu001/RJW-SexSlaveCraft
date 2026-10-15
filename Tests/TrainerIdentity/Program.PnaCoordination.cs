using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>核对真实身份提交时机；PNA 健康规则由独立 PnaLoad 套件覆盖。</summary>
    private static void RunPnaIdentityCoordinationTests()
    {
        Run("身份成功切换后按实际身份强制协调 PNA", () =>
        {
            Pawn pawn = Pawn(); int calls = 0; PawnIdentity expected = PawnIdentity.Master;
            SSCPnaExposureUtility.OnRefresh = (target, force) =>
            {
                calls++; Assert(target == pawn && force && pawn.Training.pawnIdentity == expected);
            };
            try
            {
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, expected)); Assert(calls == 1);
                expected = PawnIdentity.Unset;
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, expected)); Assert(calls == 2);
            }
            finally { SSCPnaExposureUtility.OnRefresh = null; }
        });
        Run("同主人身份及人格恢复同样核对身体 PNA", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); int calls = 0;
            SSCPnaExposureUtility.OnRefresh = (target, force) =>
            {
                calls++; Assert(target == pawn && force && SSCIdentityUtility.IsMaster(target));
            };
            try
            {
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master)); Assert(calls == 1);
                Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(pawn, PawnIdentity.Master, true)); Assert(calls == 2);
                Assert(pawn.Training.masterQualificationUnlocked);
            }
            finally { SSCPnaExposureUtility.OnRefresh = null; }
        });
        Run("拒绝身份切换不提前调用 PNA 清药", () =>
        {
            Pawn pawn = Pawn(); SSCMod.settings.allowDirectMasterSelection = false; int calls = 0;
            SSCPnaExposureUtility.OnRefresh = (target, force) => calls++;
            try
            {
                Assert(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
                Assert(pawn.Training.pawnIdentity == PawnIdentity.Unset && calls == 0);
            }
            finally { SSCPnaExposureUtility.OnRefresh = null; }
        });
    }
}
