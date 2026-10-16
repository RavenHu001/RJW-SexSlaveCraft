// AI 辅助生成：真实 Harmony 生命周期及真实 SSC 克隆函数；复活引擎只提供结果边界。
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using SexSlaveCraft;
using Verse;

namespace Verse
{
    public partial class Pawn
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public void Kill(DamageInfo? dinfo = null,Hediff culprit = null) { Dead=true; }
    }
}
namespace RimWorld
{
    public class ResurrectionParams { }
    public static class ResurrectionUtility
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryResurrect(Pawn pawn,ResurrectionParams parms = null)
        {
            if(!pawn.Dead || pawn.Destroyed)return false;
            pawn.Dead=false;return true;
        }
    }
}
internal static partial class Program
{
    private const string InfestationToggle=SSCAbilityToggleUtility.MasterDesireInfestation;
    private static HediffDef RegisterToggleMarker()
    {
        SSCDefOf.SSC_PersonalityExcreted_Done = SSCDefOf.SSC_PersonalityExcreted_Done ??
            new HediffDef { defName="Hediff_SSC_PersonalityExcreted_Done" };
        var def=DefDatabase<HediffDef>.GetNamedSilentFail(SSCAbilityToggleUtility.InfestationMarker);
        if(def==null)
        {
            def=new HediffDef{defName=SSCAbilityToggleUtility.InfestationMarker,hediffClass=typeof(Hediff_SSCToggleAbilityMarker),maxSeverity=1};
            DefDatabase<HediffDef>.AllDefs.Add(def);
        }
        return def;
    }
    private static Pawn ToggleMaster()
    {
        RegisterToggleMarker();var p=NewPawn();p.Comp=new CompSexSlaveTraining{pawnIdentity=PawnIdentity.Master};p.IsMaster=true;
        p.Faction=Faction.OfPlayer;((CompSexSlaveTraining)p.Comp).abilityToggles.Set(InfestationToggle,true);
        SSCAbilityToggleUtility.SyncMarkers(p);return p;
    }
    private static int ToggleMarkers(Pawn p)=>p.health.hediffSet.hediffs.Count(h=>SSCAbilityToggleUtility.IsMarkerDef(h.def));
    private static void RunAbilityToggleLifecycleTests()
    {
        Run("完整读档初始化统一修复标记，不依赖地图或面板",()=>
        {
            var p=ToggleMaster();p.health.hediffSet.hediffs.RemoveAll(h=>SSCAbilityToggleUtility.IsMarkerDef(h.def));
            var off=NewPawn();off.Comp=new CompSexSlaveTraining{pawnIdentity=PawnIdentity.Master};
            off.health.hediffSet.hediffs.Add(new Hediff_SSCToggleAbilityMarker{pawn=off,def=RegisterToggleMarker()});
            SSCRestrictionGameComponent.Pawns.Add(p);SSCRestrictionGameComponent.Pawns.Add(off);
            new SSCAbilityToggleGameComponent(new Game()).FinalizeInit();
            Equal(1,ToggleMarkers(p));Equal(0,ToggleMarkers(off));
            Assert(SSCPnaExposureUtility.GetBodyRecord(p)==null,"不制造PNA输入");
        });
        Run("能力标记真实死亡/成功复活补丁清除并重建，偏好保留",()=>
        {
            var p=ToggleMaster();Equal(1,ToggleMarkers(p));p.Kill();Equal(0,ToggleMarkers(p));
            Assert(((CompSexSlaveTraining)p.Comp).abilityToggles.Get(InfestationToggle),"死亡保留偏好");
            Assert(ResurrectionUtility.TryResurrect(p),"复活成功");Equal(1,ToggleMarkers(p));
        });
        Run("能力标记失败复活不伪造同步",()=>
        {
            var p=ToggleMaster();p.health.hediffSet.hediffs.RemoveAll(h=>SSCAbilityToggleUtility.IsMarkerDef(h.def));
            Assert(!ResurrectionUtility.TryResurrect(p),"活人拒绝");Equal(0,ToggleMarkers(p));
        });
        Run("直接添加/移除空壳走真实健康补丁同步，不依赖打开面板",()=>
        {
            var p=ToggleMaster();var hollow=new Hediff{pawn=p,def=SSCDefOf.SSC_PersonalityExcreted_Done};
            p.health.AddHediff(hollow);Equal(0,ToggleMarkers(p));
            p.health.RemoveHediff(hollow);Equal(1,ToggleMarkers(p));
        });
        Run("外部移除有效标记后重建，自身同步不会递归",()=>
        {
            var p=ToggleMaster();p.health.RemoveHediff(p.health.hediffSet.hediffs.Single(h=>SSCAbilityToggleUtility.IsMarkerDef(h.def)));
            Equal(1,ToggleMarkers(p));
        });
        Run("新生克隆不继承人格开关或标记，来源保持开启",()=>
        {
            var source=CloneSource();var def=RegisterToggleMarker();
            ((CompSexSlaveTraining)source.Comp).abilityToggles.Set(InfestationToggle,true);
            var body=CopiedBody(source);((CompSexSlaveTraining)body.Comp).abilityToggles.Set(InfestationToggle,true);
            body.health.hediffSet.hediffs.Add(new Hediff_SSCToggleAbilityMarker{pawn=body,def=def});
            Assert(RabbitCloneUtility.TryCreateRabbitClone(source,out var clone,false),"真实新生路径");
            Assert(!((CompSexSlaveTraining)clone.Comp).abilityToggles.HasEntries && ToggleMarkers(clone)==0,"新生关闭");
            Assert(((CompSexSlaveTraining)source.Comp).abilityToggles.Get(InfestationToggle),"来源不变");
        });
        Run("成熟克隆过滤来源标记，不复制来源开启设置",()=>
        {
            var source=CloneSource();var def=RegisterToggleMarker();
            ((CompSexSlaveTraining)source.Comp).abilityToggles.Set(InfestationToggle,true);
            source.health.hediffSet.hediffs.Add(new Hediff_SSCToggleAbilityMarker{pawn=source,def=def});
            var clone=NewPawn();clone.Comp=new CompSexSlaveTraining();
            RabbitCloneUtility.MatureRabbitClone(clone,source,CloneLink(clone,source));
            Assert(!((CompSexSlaveTraining)clone.Comp).abilityToggles.HasEntries && ToggleMarkers(clone)==0,"不继承来源");
        });
        Run("成熟克隆保留自己后来开启的能力和原标记实例",()=>
        {
            var source=CloneSource();var clone=ToggleMaster();var own=clone.health.hediffSet.hediffs.Single(h=>SSCAbilityToggleUtility.IsMarkerDef(h.def));
            RabbitCloneUtility.MatureRabbitClone(clone,source,CloneLink(clone,source));
            Assert(SSCAbilityToggleUtility.IsActive(clone,InfestationToggle) && clone.health.hediffSet.hediffs.Contains(own),"保留自身");
        });
        Run("第一批开关不会创建PNA负荷或把主人身份当普通产出能力",()=>
        {
            var p=ToggleMaster();var target=NewPawn();
            Assert(SSCPnaExposureUtility.GetBodyRecord(p)==null && !SSCPnaSourceUtility.IsPnaProducer(p),"只保存设置");
            Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(p,target).Accepted && SSCPnaExposureUtility.GetBodyRecord(target)==null,"未接第二批收益");
        });
    }
}
