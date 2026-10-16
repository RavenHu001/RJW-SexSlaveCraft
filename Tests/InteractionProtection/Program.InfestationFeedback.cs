using System;
using System.IO;
using System.Linq;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>真实 Harmony 完成集成：共享生产 XML 与记忆边界，不另写反馈分档或替换算法。</summary>
    private static void ResetFeedback()
    {
        ResetSexAwards();InfestationHealth.OnAdded=null;Hediff.OnSeverity=null;
        ThoughtMaker.OnMade=null;ThoughtUtility.OnCheck=null;MemoryThoughtHandler.OnRemoved=null;MemoryThoughtHandler.OnGained=null;
        InfestationDefLoader.Load(SscTestPaths.RepositoryRoot());
    }
    private static (JobDriver_SexBaseInitiator d,Pawn master,Pawn target) FeedbackScene(bool reverse=false,bool forced=false)
    {
        ResetFeedback();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;
        // 反向强迫也须先取得原有发起许可；反馈测试不能绕过统一行为保护。
        if(reverse&&forced)p.b.Training.restrictionConfig.rules.allowForcedInitiation=true;
        p.b.needs.mood=new Need_Mood(p.b);var d=Driver(reverse?p.b:p.a,reverse?p.a:p.b,forced);Begin(d);return(d,p.a,p.b);
    }
    private static Thought_Memory[] Feedback(Pawn p)=>p.needs.mood.thoughts.memories.Memories
        .Where(x=>x.def==SSCDefOf.SSC_DesireInfestation_Mood||x.def==SSCDefOf.SSC_DesireInfestation_Opinion).ToArray();
    private static void RunInfestationFeedbackTests()
    {
        foreach(bool forced in new[]{false,true})foreach(bool reverse in new[]{false,true})foreach(int stage in new[]{0,1,2,3})
            Run($"第四批真实普通完成 forced={forced} reverse={reverse} stage={stage}",()=>{
                var s=FeedbackScene(reverse,forced);s.target.needs.corruption.CurLevel=stage>=2?.4f:.1f;s.target.relations.Opinion=stage%2==0?0:-1;
                s.d.ticks_left=0;SexUtility.ProcessSex(s.d.Sexprops);SexUtility.ProcessSex(s.d.Sexprops);
                var result=Feedback(s.target);Assert(result.Length==(stage==3?1:2)&&result.All(x=>x.CurStageIndex==stage&&x.otherPawn==s.master),"独立四档与真实施加者");
                Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"不增加第二次认领");
            });
        Run("第四批 RJW 原结算改变 C/O 与发记忆不重判本档",()=>{
            var s=FeedbackScene();s.target.needs.corruption.CurLevel=.295f;s.target.relations.Opinion=-1;s.d.ticks_left=0;
            var rjw=new ThoughtDef{defName="rjw-existing",durationDays=2,stages=new(){new(){baseMoodEffect=2}}};
            SexUtility.OnProcess=()=>{s.target.needs.corruption.CurLevel=.4f;s.target.relations.Opinion=1;s.target.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(rjw),s.master);};
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{SexUtility.OnProcess=null;}
            Assert(Feedback(s.target).All(x=>x.CurStageIndex==1),"在原方法前冻结");Assert(s.target.needs.mood.thoughts.memories.Memories.Any(x=>x.def==rjw),"保留 RJW 记忆");
        });
        Run("第四批身体健康回调换人格后停止反馈",()=>{
            ResetFeedback();var s=BodyScene();s.target.needs.mood=new Need_Mood(s.target);s.d.ticks_left=0;
            InfestationHealth.OnAdded=_=>{SSCPersonalityRuntimeUtility.TryBeginMutation(s.target,out var scope);scope.Dispose();};
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{InfestationHealth.OnAdded=null;}
            Assert(BodyExp(s.target,s.hand)>0&&Feedback(s.target).Length==0,"第三批写入后先复查人格");
        });
        Run("第四批无研究与可靠部位仍领取反馈",()=>{
            var s=FeedbackScene();SSCDefOf.SSC_BodyPartTraining.IsFinished=false;s.d.ticks_left=0;
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{SSCDefOf.SSC_BodyPartTraining.IsFinished=true;}
            Assert(Feedback(s.target).Length==2,"反馈不依赖部位计划");
        });
        foreach(bool completed in new[]{false,true})Run("第四批任务存档保留唯一认领 completed="+completed,()=>{
            var s=FeedbackScene();s.d.ticks_left=0;if(completed)SexUtility.ProcessSex(s.d.Sexprops);
            foreach(var memory in Feedback(s.target))memory.age=123;
            var restored=new JobDriver_SexBaseInitiator{pawn=s.d.pawn,job=s.d.job,Sexprops=new(){pawn=s.d.pawn,partner=s.d.Partner},PartnerPawn=s.d.Partner,ticks_left=0};
            s.d.pawn.jobs.curDriver=restored;Restore(s.d,restored);SexUtility.ProcessSex(restored.Sexprops);SexUtility.ProcessSex(restored.Sexprops);
            Assert(Feedback(s.target).Length==2&&Feedback(s.target).All(x=>x.age==(completed?123:0))&&SSCPnaSexSupplyUtility.StrongCalls==1,"恢复后只领取一次");
        });
        foreach(bool reverse in new[]{false,true})Run("第四批 Lovin 双端 Cleanup 合计一次 reverse="+reverse,()=>{
            var s=FeedbackScene(reverse);var d=Lovin(reverse?s.target:s.master,reverse?s.master:s.target);BeginLovin(d);d.FinishTimer();
            var peer=(JobDriver_Lovin)(reverse?s.master:s.target).jobs.curDriver;peer.FinishTimer();peer.Cleanup(JobCondition.Succeeded);d.Cleanup(JobCondition.Succeeded);d.Cleanup(JobCondition.Succeeded);
            Assert(Feedback(s.target).Length==2&&SSCPnaSexSupplyUtility.StrongCalls==1,"Lovin 唯一发起完成适配");
        });
        Run("第四批 Lovin 在 RJW Cleanup 前缀前冻结 C/O",()=>{
            var s=FeedbackScene();s.target.needs.corruption.CurLevel=.295f;s.target.relations.Opinion=-1;
            var d=Lovin(s.master,s.target);BeginLovin(d);d.FinishTimer();
            SexUtility.OnProcess=()=>{s.target.needs.corruption.CurLevel=.5f;s.target.relations.Opinion=1;};
            try{d.Cleanup(JobCondition.Succeeded);}finally{SexUtility.OnProcess=null;}
            Assert(Feedback(s.target).All(x=>x.CurStageIndex==1),"不在 Cleanup 后取输入");
        });
        Run("第四批记忆通知内重入 ProcessSex 不重领",()=>{
            var s=FeedbackScene();s.d.ticks_left=0;bool entered=false;
            MemoryThoughtHandler.OnGained=_=>{if(entered)return;entered=true;SexUtility.ProcessSex(s.d.Sexprops);};
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{MemoryThoughtHandler.OnGained=null;}
            Assert(entered&&Feedback(s.target).Length==2&&SSCPnaSexSupplyUtility.StrongCalls==1,"原认领封住记忆回调重入");
        });
        foreach(bool interrupted in new[]{false,true})Run("第四批未完成不发反馈 interrupted="+interrupted,()=>{
            var s=FeedbackScene();if(interrupted)s.d.EndJobWith(JobCondition.Incompletable);SexUtility.ProcessSex(s.d.Sexprops);Assert(Feedback(s.target).Length==0,"没有真实完成");
        });
        foreach(string kind in new[]{"daily","ritual","personality","solo"})Run("第四批排除专用任务 "+kind,()=>{
            ResetFeedback();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;p.b.needs.mood=new Need_Mood(p.b);
            JobDriver_SexBaseInitiator d=kind=="daily"?new JobDriver_Training():kind=="ritual"?new JobDriver_RitualTraining():kind=="personality"?new JobDriver_PE():new JobDriver_Masturbate();
            d.pawn=p.a;d.job=new Job{def=new JobDef{defName="Excluded",driverClass=d.GetType()},targetA=new LocalTargetInfo{Thing=kind=="solo"?p.a:p.b}};
            p.a.jobs.curDriver=d;d.MakeScenarioToils();Begin(d);d.ticks_left=0;
            SexUtility.ProcessSex(d.Sexprops??new SexProps{pawn=p.a,partner=kind=="solo"?p.a:p.b});Assert(Feedback(p.b).Length==0,"不能从专用流程领取侵染反馈");
        });
#if REAL_HARMONY
        Run("第四批跳过原结算不领取，后续真实完成可领取",()=>{
            var s=FeedbackScene();s.d.ticks_left=0;InfestationSkipProbe.Skip=true;
            try{SexUtility.ProcessSex(s.d.Sexprops);}finally{InfestationSkipProbe.Skip=false;}
            Assert(Feedback(s.target).Length==0,"跳过原结算");SexUtility.ProcessSex(s.d.Sexprops);Assert(Feedback(s.target).Length==2,"占用释放");
        });
#endif
        // 清除静态回调与可选反馈定义，防止后续无关保护用例受本专项配置影响。
        ResetFeedback();
    }
}
