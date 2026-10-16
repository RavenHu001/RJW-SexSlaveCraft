using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using rjw.Modules.Attraction;

internal static partial class Program
{
    private static Pawn InfestationTarget()
    { var pawn=NewPawn();pawn.Comp=new CompSexSlaveTraining();return pawn; }
    private static void FinishInfestation(Pawn a,Pawn b,bool allowProducer=true)
    {
        using var context=SSCOrdinarySexOutcomeUtility.Capture(a,b,allowProducer,()=>true,()=>true);
        Assert(context.TryClaim(),"合法完成认领");context.Dispatch();context.Dispatch();
    }

    // 从真实完成快照/来源选择一路执行真实 PNA 输入；引擎对象由宿主提供边界。
    private static void RunInfestationTests()
    {
        foreach(bool reverse in new[]{false,true}) foreach(bool producer in new[]{false,true})
            Run("侵染第二批真实分发8%且双重来源不叠加 reverse="+reverse+" producer="+producer,()=>
            {
                var master=ToggleMaster();if(producer)AddPnaProducer(master);var target=InfestationTarget();
                Raw(target,.4f);AddOldAddiction(target,.4f);target.needs.PnaNeed=.2f;
                FinishInfestation(reverse?target:master,reverse?master:target);
                Near(.28,SSCPnaExposureUtility.GetLoad(target));Near(1,target.needs.PnaNeed.Value);
                Near(.42,SSCPnaExposureUtility.GetAddiction(target).Severity);Near(.01,target.needs.CorruptionNeed.CurLevel);
                Near(0,SSCPnaExposureUtility.GetLoad(master));
            });
        foreach(float load in new[]{.89f,.90f,.97f,1f})
            Run("侵染第二批输入后末期强化且满值真实输入 load="+load,()=>
            {
                var master=ToggleMaster();var target=InfestationTarget();Raw(target,load*2);AddOldAddiction(target,.4f);
                FinishInfestation(master,target);Near(load+.08>=.97?.62:.42,SSCPnaExposureUtility.GetAddiction(target).Severity);
                Near(Math.Min(1,load+.08),SSCPnaExposureUtility.GetLoad(target));
            });
        Run("侵染第二批首次满值只建立一次完整成瘾",()=>
        { var m=ToggleMaster();var t=InfestationTarget();Raw(t,1.9f);FinishInfestation(m,t);Near(1,SSCPnaExposureUtility.GetAddiction(t).Severity);Near(1,t.needs.PnaNeed.Value); });
        Run("侵染第二批低负荷不创建需求或成瘾",()=>
        { var m=ToggleMaster();var t=InfestationTarget();FinishInfestation(m,t);Assert(SSCPnaExposureUtility.GetAddiction(t)==null&&SSCPnaExposureUtility.GetNeed(t)==null,"无凭空需求");Near(.08,SSCPnaExposureUtility.GetLoad(t)); });
        Run("侵染第二批有效链保护仍有恶堕而无普通成瘾",()=>
        { var m=ToggleMaster();var t=InfestationTarget();Hediff_ChainOfSexSlave.AddToPawn(t,NewPawn());Raw(t,1.9f);FinishInfestation(m,t);Assert(SSCPnaExposureUtility.GetRootedRecord(t)!=null&&SSCPnaExposureUtility.GetAddiction(t)==null,"保护分流");Near(.01,t.needs.CorruptionNeed.CurLevel); });
        foreach(string invalid in new[]{"closed","master","hollow","noComp","noNeed","nonhuman","dead"})
            Run("侵染第二批目标准入与开关 "+invalid,()=>
            {
                var m=ToggleMaster();var t=InfestationTarget();
                switch(invalid){case "closed":((CompSexSlaveTraining)m.Comp).abilityToggles.Set(InfestationToggle,false);break;
                case "master":t.IsMaster=true;((CompSexSlaveTraining)t.Comp).pawnIdentity=PawnIdentity.Master;break;
                case "hollow":t.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);break;
                case "noComp":t.Comp=null;break;case "noNeed":t.needs.CorruptionNeed=null;break;
                case "nonhuman":t.RaceProps.Humanlike=false;break;case "dead":t.Dead=true;break;}
                using var c=SSCOrdinarySexOutcomeUtility.Capture(m,t,true,()=>true,()=>true);
                if(c.TryClaim())c.Dispatch();Near(0,SSCPnaExposureUtility.GetLoad(t));
            });
        foreach(bool allowProducer in new[]{false,true})
            Run("侵染第二批原版仅产出者范围 allowProducer="+allowProducer,()=>
            { var s=InfestationTarget();AddPnaProducer(s);var t=InfestationTarget();FinishInfestation(s,t,allowProducer);Near(allowProducer?.02:0,SSCPnaExposureUtility.GetLoad(t));Near(0,t.needs.CorruptionNeed.CurLevel); });
        Run("侵染第二批Lovin强来源仍生效",()=>
        { var m=ToggleMaster();AddPnaProducer(m);var t=InfestationTarget();FinishInfestation(m,t,false);Near(.08,SSCPnaExposureUtility.GetLoad(t)); });
        foreach(float level in new[]{.10f,.115f,.12f,.5f})
            Run("侵染第二批新评分12%上限及超限不倒扣 "+level,()=>
            { var m=ToggleMaster();var t=InfestationTarget();t.needs.CorruptionNeed.CurLevel=level;FinishInfestation(m,t);Near(level>=.12f?level:Math.Min(.12,level+.01),t.needs.CorruptionNeed.CurLevel);Near(.08,SSCPnaExposureUtility.GetLoad(t)); });
        foreach(float cap in new[]{.3f,.5f,.9f,1f})
            Run("侵染第二批锁链上限部分增量 "+cap,()=>
            { var m=ToggleMaster();var t=InfestationTarget();t.InfestationCap=cap;t.needs.CorruptionNeed.CurLevel=cap-.005f;FinishInfestation(m,t);Near(cap,t.needs.CorruptionNeed.CurLevel); });
        Run("侵染第二批旧评分保留原上限与历史最高",()=>
        {
            SSCMod.settings.useOldScoring=true;
            try{var m=ToggleMaster();var t=InfestationTarget();t.needs.CorruptionNeed.CurLevel=.9f;FinishInfestation(m,t);Near(.91,t.needs.CorruptionNeed.CurLevel);Near(.91,t.needs.CorruptionNeed.HighestCorruptionLevel);}
            finally{SSCMod.settings.useOldScoring=false;}
        });
        foreach(string change in new[]{"sourcePersona","targetPersona","need","disable"})
            Run("侵染第二批健康回调失效停止补需求与恶堕 "+change,()=>
            {
                var m=ToggleMaster();AddPnaProducer(m);var t=InfestationTarget();AddOldAddiction(t,.4f);t.needs.PnaNeed=.2f;
                t.health.OnAdded=h=>{if(!SSCPnaExposureUtility.IsBodyLoadDef(h.def))return;
                    if(change=="need")t.needs.CorruptionNeed=new Need_Corruption();
                    else if(change=="disable")((CompSexSlaveTraining)m.Comp).abilityToggles.Set(InfestationToggle,false);
                    else {Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(change=="sourcePersona"?m:t,out var scope),"换人格");scope.Dispose();}};
                FinishInfestation(m,t);Near(.08,SSCPnaExposureUtility.GetLoad(t));Near(.2,t.needs.PnaNeed.Value);
                Near(.4,SSCPnaExposureUtility.GetAddiction(t).Severity);Near(0,t.needs.CorruptionNeed.CurLevel);
            });
        Run("侵染第二批原结算后同组件换人格不能领取且清占用",()=>
        {
            var m=ToggleMaster();var t=InfestationTarget();int claims=0,releases=0;
            using(var c=SSCOrdinarySexOutcomeUtility.Capture(m,t,true,()=>true,()=>{claims++;return true;},()=>releases++))
            {SSCPersonalityRuntimeUtility.TryBeginMutation(m,out var scope);scope.Dispose();Assert(!c.TryClaim(),"原人格失效");c.Dispatch();}
            Equal(1,claims);Equal(1,releases);Near(0,SSCPnaExposureUtility.GetLoad(t));
        });
        Run("侵染第二批分发本身重入及重复不双发",()=>
        {
            var m=ToggleMaster();var t=InfestationTarget();using var c=SSCOrdinarySexOutcomeUtility.Capture(m,t,true,()=>true,()=>true);
            t.health.OnAdded=_=>c.Dispatch();Assert(c.TryClaim(),"认领");c.Dispatch();c.Dispatch();Near(.08,SSCPnaExposureUtility.GetLoad(t));Near(.01,t.needs.CorruptionNeed.CurLevel);
        });
        Run("侵染第二批认领失败不得直接分发",()=>
        {var m=ToggleMaster();var t=InfestationTarget();using var c=SSCOrdinarySexOutcomeUtility.Capture(m,t,true,()=>true,()=>false);Assert(!c.TryClaim(),"拒绝");c.Dispatch();Near(0,SSCPnaExposureUtility.GetLoad(t));});
        foreach(var purpose in new[]{AttractionPurpose.General,AttractionPurpose.ForFucking,AttractionPurpose.ForRape})
            foreach(bool producer in new[]{false,true})
            Run("侵染第二批只读来源偏好且双来源不叠乘 "+purpose+" "+producer,()=>
            {
                var m=ToggleMaster();if(producer)AddPnaProducer(m);var t=InfestationTarget();AddOldAddiction(t,.4f);t.needs.PnaNeed=.4f;
                Near(1.25,PnaAttraction(t,m,purpose));t.needs.PnaNeed=0;Near(1.5,PnaAttraction(t,m,purpose));
                Near(0,SSCPnaExposureUtility.GetLoad(t));Near(0,t.needs.CorruptionNeed.CurLevel);
                ((CompSexSlaveTraining)m.Comp).abilityToggles.Set(InfestationToggle,false);Near(producer?1.5:1,PnaAttraction(t,m,purpose));
            });
        Run("侵染第二批无适用恶堕需求时纯侵染源不加权",()=>
        {var m=ToggleMaster();var t=InfestationTarget();AddOldAddiction(t,.4f);t.needs.PnaNeed=0;t.needs.CorruptionNeed=null;Near(1,PnaAttraction(t,m));AddPnaProducer(m);Near(1.5,PnaAttraction(t,m));});
        Run("侵染第二批载入阶段不认领或写恶堕",()=>
        {
            var m=ToggleMaster();var t=InfestationTarget();int claims=0;
            using var c=SSCOrdinarySexOutcomeUtility.Capture(m,t,true,()=>true,()=>{claims++;return true;});
            Scribe.mode=LoadSaveMode.LoadingVars;
            try{Assert(!c.TryClaim(),"加载不能消费凭据");c.Dispatch();SSCDesireInfestationUtility.ApplyCorruption(m,t,t.needs.CorruptionNeed,()=>true);}
            finally{Scribe.mode=LoadSaveMode.Inactive;}
            Equal(0,claims);Near(0,t.needs.CorruptionNeed.CurLevel);Near(0,SSCPnaExposureUtility.GetLoad(t));
        });
    }
}
