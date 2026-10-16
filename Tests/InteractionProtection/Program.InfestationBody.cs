using System;
using System.Linq;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    // 真实 Harmony Start/ProcessSex/ExposeData + 真实部位、预算及写入生产代码；引擎健康通知仍为模型。
    private static (JobDriver_SexBaseInitiator d,Pawn master,Pawn target,BodyPartRecord hand,BodyPartRecord unused) BodyScene(bool reverse=false,bool begin=true,bool resolved=true)
    {
        ResetSexAwards();SSCDefOf.SSC_BodyPartTraining.IsFinished=true;InfestationHealth.OnAdded=null;Hediff.OnSeverity=null;
        DefDatabase<HediffDef>.AllDefsListForReading.Clear();
        foreach(var n in new[]{"Hand","Foot","Oral","Breast","Genitals","Anus"})
            DefDatabase<HediffDef>.AllDefsListForReading.Add(new(){defName="SSC_Exp_"+n});
        var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;
        var hand=new BodyPartRecord{def=new(){defName="LeftHand"}};var unused=new BodyPartRecord{def=new(){defName="RightHand"}};
        p.b.RaceProps.body.AllParts.AddRange(new[]{hand,unused});
        var d=Driver(reverse?p.b:p.a,reverse?p.a:p.b,false);
        d.Sexprops=new(){pawn=d.pawn,partner=d.Partner,dictionaryKey=new(){defName="Selected"},isRevese=reverse};
        if(resolved)
        {
            d.Sexprops._resolved=new(){Initiator=d.pawn,Recipient=d.Partner,Interaction=new(){Def=d.Sexprops.dictionaryKey}};
            var part=new VanillaLewdablePart{Owner=p.b,BodyPart=hand,Family=LewdablePartFamily.Hand};
            (reverse?d.Sexprops._resolved.InitiatorParts:d.Sexprops._resolved.RecipientParts).Add(part);
        }
        if(begin)Begin(d);return(d,p.a,p.b,hand,unused);
    }
    private static float BodyExp(Pawn p,BodyPartRecord part)=>p.health.hediffSet.hediffs.Where(h=>h.Part==part&&h.def.defName=="SSC_Exp_Hand").Sum(h=>h.Severity);
    private static void RunInfestationBodyTests()
    {
        foreach(bool reverse in new[]{false,true}) Run("第三批真实开始/完成精确部位且一次 reverse="+reverse,()=>{
            var s=BodyScene(reverse);s.d.ticks_left=0;SexUtility.ProcessSex(s.d.Sexprops);SexUtility.ProcessSex(s.d.Sexprops);
            Assert(Math.Abs(BodyExp(s.target,s.hand)-.01f)<.00001f&&BodyExp(s.target,s.unused)==0,"只开发真实部位");
            Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"保留一次强来源");
        });
        Run("第三批先采凭据，开始后开启仍按完成规则领取",()=>{
            var s=BodyScene(begin:false);s.master.Training.Infestation=false;Begin(s.d);s.master.Training.Infestation=true;
            s.d.ticks_left=0;SexUtility.ProcessSex(s.d.Sexprops);Assert(BodyExp(s.target,s.hand)>.009f,"采集不依赖开启状态");
        });
        Run("第三批缺少开始解析不在收尾补造",()=>{
            var s=BodyScene(resolved:false);s.d.ticks_left=0;
            s.d.Sexprops._resolved=new(){Initiator=s.d.pawn,Recipient=s.d.Partner,Interaction=new(){Def=s.d.Sexprops.dictionaryKey},
                RecipientParts=new(){new VanillaLewdablePart{Owner=s.target,BodyPart=s.hand,Family=LewdablePartFamily.Hand}}};
            SexUtility.ProcessSex(s.d.Sexprops);Assert(BodyExp(s.target,s.hand)==0&&SSCPnaSexSupplyUtility.StrongCalls==1,"仅身体无凭据");
        });
        Run("第三批重复Start不能替换首次部位凭据",()=>{
            var s=BodyScene();((VanillaLewdablePart)s.d.Sexprops._resolved.RecipientParts[0]).BodyPart=s.unused;s.d.Start();
            s.d.ticks_left=0;SexUtility.ProcessSex(s.d.Sexprops);Assert(BodyExp(s.target,s.unused)==0,"重复开始不改凭据");
        });
        foreach(bool completed in new[]{false,true}) Run("第三批进行中/已完成存档恢复 completed="+completed,()=>{
            var s=BodyScene();s.d.ticks_left=0;if(completed)SexUtility.ProcessSex(s.d.Sexprops);
            var restored=new JobDriver_SexBaseInitiator{pawn=s.d.pawn,job=s.d.job,Sexprops=new(){pawn=s.d.pawn,partner=s.d.Partner,dictionaryKey=s.d.Sexprops.dictionaryKey},PartnerPawn=s.d.Partner,ticks_left=0};
            s.d.pawn.jobs.curDriver=restored;Restore(s.d,restored);SexUtility.ProcessSex(restored.Sexprops);SexUtility.ProcessSex(restored.Sexprops);
            Assert(Math.Abs(BodyExp(s.target,s.hand)-.01f)<.00001f&&SSCPnaSexSupplyUtility.StrongCalls==1,"恢复后合计一次且不重新解析");
        });
        Run("第三批旧场景缺字段恢复不回填",()=>{
            var s=BodyScene();Scribe.node.Clear();Scribe.mode=LoadSaveMode.Saving;s.d.ExposeData();Scribe.node.Remove("sscOrdinarySexParts");
            var d=new JobDriver_SexBaseInitiator{pawn=s.d.pawn,job=s.d.job,Sexprops=s.d.Sexprops,PartnerPawn=s.d.Partner,ticks_left=0};s.d.pawn.jobs.curDriver=d;
            Scribe.mode=LoadSaveMode.LoadingVars;d.ExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;d.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
            d.Start();d.ticks_left=0;SexUtility.ProcessSex(d.Sexprops);Assert(BodyExp(s.target,s.hand)==0&&SSCPnaSexSupplyUtility.StrongCalls==1,"旧场只跳过开发");
        });
        Run("第三批任务复用不能继承旧身体凭据",()=>{
            var s=BodyScene();s.d.job.loadID++;s.d.ticks_left=0;SexUtility.ProcessSex(s.d.Sexprops);Assert(BodyExp(s.target,s.hand)==0,"旧Job身份失效");
        });
        Run("第三批原结算重入保持唯一开发",()=>{
            var s=BodyScene();s.d.ticks_left=0;bool nested=false;
            SexUtility.OnProcess=()=>{if(nested)return;nested=true;SexUtility.ProcessSex(s.d.Sexprops);};
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{SexUtility.OnProcess=null;}
            Assert(Math.Abs(BodyExp(s.target,s.hand)-.01f)<.00001f,"唯一开发");
        });
        Run("第三批原结算换人格停止开发",()=>{
            var s=BodyScene();s.d.ticks_left=0;SexUtility.OnProcess=()=>{SSCPersonalityRuntimeUtility.TryBeginMutation(s.target,out var scope);scope.Dispose();};
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{SexUtility.OnProcess=null;}
            Assert(BodyExp(s.target,s.hand)==0,"原人格凭据失效");
        });
        Run("第三批健康回调重入不能追加一次",()=>{
            var s=BodyScene();s.d.ticks_left=0;InfestationHealth.OnAdded=_=>SexUtility.ProcessSex(s.d.Sexprops);
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{InfestationHealth.OnAdded=null;}
            Assert(Math.Abs(BodyExp(s.target,s.hand)-.01f)<.00001f&&SSCPnaSexSupplyUtility.StrongCalls==1,"健康重入被挡");
        });
        Run("第三批原方法异常未提前开发且释放临时占用",()=>{
            var s=BodyScene();s.d.ticks_left=0;SexUtility.OnProcess=()=>throw new InvalidOperationException("test original");
            try{SexUtility.ProcessSex(s.d.Sexprops);}catch(InvalidOperationException){}finally{SexUtility.OnProcess=null;}
            Assert(BodyExp(s.target,s.hand)==0,"异常前不开发");SexUtility.ProcessSex(s.d.Sexprops);Assert(BodyExp(s.target,s.hand)>.009f,"后续真实完成可领");
        });
#if REAL_HARMONY
        Run("第三批跳过原方法不认领开发",()=>{
            var s=BodyScene();s.d.ticks_left=0;InfestationSkipProbe.Skip=true;
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{InfestationSkipProbe.Skip=false;}
            Assert(BodyExp(s.target,s.hand)==0,"跳过原方法");SexUtility.ProcessSex(s.d.Sexprops);Assert(BodyExp(s.target,s.hand)>.009f,"仍可真实完成");
        });
#endif
        Run("第三批原版Lovin仍只结算第二批分量",()=>{
            var s=BodyScene(begin:false);var d=Lovin(s.master,s.target);BeginLovin(d);d.FinishTimer();d.Cleanup(JobCondition.Succeeded);
            Assert(BodyExp(s.target,s.hand)==0&&SSCPnaSexSupplyUtility.StrongCalls==1,"Lovin无全过程部位凭据");
        });
    }
}
