// AI 辅助生成
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunAbilityToggleIdentityTests()
    {
        const string id=SSCAbilityToggleUtility.MasterDesireInfestation;
        var marker=new HediffDef{defName=SSCAbilityToggleUtility.InfestationMarker,hediffClass=typeof(Hediff_SSCToggleAbilityMarker),maxSeverity=1};
        DefDatabase<HediffDef>.AllDefsListForReading.Add(marker);
        Run("真实身份出口保持个人能力偏好，退出移除、返回恢复标记",()=>
        {
            var p=Pawn();Assert(SSCIdentityUtility.TrySetIdentity(p,PawnIdentity.Master));
            Assert(!SSCAbilityToggleUtility.GetEnabled(p,id));
            Assert(SSCAbilityToggleUtility.TrySetEnabled(p,id,true,p.Training,SSCPersonalityRuntimeUtility.Capture(p)));
            Assert(p.health.hediffSet.HasHediff(marker));
            Assert(SSCIdentityUtility.TrySetIdentity(p,PawnIdentity.Unset));
            Assert(SSCAbilityToggleUtility.GetEnabled(p,id) && !p.health.hediffSet.HasHediff(marker));
            Assert(SSCIdentityUtility.TrySetIdentity(p,PawnIdentity.Master) && p.health.hediffSet.HasHediff(marker));
        });
        Run("同身份恢复出口修复缺失能力标记，不重复实例",()=>
        {
            var p=Pawn();Assert(SSCIdentityUtility.TrySetIdentity(p,PawnIdentity.Master));p.Training.abilityToggles.Set(id,true);
            Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(p,PawnIdentity.Master,false));
            Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(p,PawnIdentity.Master,false));
            Assert(p.health.hediffSet.hediffs.Count(h=>h.def==marker)==1);
        });
    }
}
