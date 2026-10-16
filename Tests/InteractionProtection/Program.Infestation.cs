using System;
using System.Linq;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    // 真实任务守卫和完成上下文负责认领；这里只观察 PNA 边界分发与真实恶堕入口。
    private static void RunInfestationCompletionTests()
    {
        foreach(bool reverse in new[]{false,true})
            Run("侵染完成双向主人和来源择强 reverse="+reverse,()=>
            {
                ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;
                var d=Driver(reverse?p.b:p.a,reverse?p.a:p.b,false);Begin(d);d.ticks_left=0;
                SexUtility.ProcessSex(d.Sexprops);SexUtility.ProcessSex(d.Sexprops);
                Assert(SSCPnaSexSupplyUtility.StrongCalls==1&&TrainerSpecializationProgressUtility.InitiatedSexAwards==1,"唯一完成");
                Assert(!SSCPnaSexSupplyUtility.Calls.Any(x=>x.Source==p.a),"强来源不再发弱来源");
                Assert(Math.Abs(p.b.needs.corruption.CurLevel-.01f)<.0001f,"目标恶堕");
            });
        Run("侵染原结算内重入只由外层认领",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Driver(p.a,p.b,false);Begin(d);d.ticks_left=0;
            bool nested=false;SexUtility.OnProcess=()=>{if(nested)return;nested=true;SexUtility.ProcessSex(d.Sexprops);};
            try{SexUtility.ProcessSex(d.Sexprops);}finally{SexUtility.OnProcess=null;}
            Assert(SSCPnaSexSupplyUtility.StrongCalls==1&&TrainerSpecializationProgressUtility.InitiatedSexAwards==1,"重入不抢认领");
        });
        foreach(bool target in new[]{false,true})
            Run("侵染原结算换人格同组件同身份也不补领 target="+target,()=>
            {
                ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Driver(p.a,p.b,false);Begin(d);d.ticks_left=0;
                SexUtility.OnProcess=()=>{SSCPersonalityRuntimeUtility.TryBeginMutation(target?p.b:p.a,out var scope);scope.Dispose();};
                try{SexUtility.ProcessSex(d.Sexprops);}finally{SexUtility.OnProcess=null;}
                SexUtility.ProcessSex(d.Sexprops);
                Assert(SSCPnaSexSupplyUtility.StrongCalls==0&&TrainerSpecializationProgressUtility.InitiatedSexAwards==0,"原场已消费且人格失效");
            });
        Run("侵染原方法异常释放占用且不提前认领",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Driver(p.a,p.b,false);Begin(d);d.ticks_left=0;
            SexUtility.OnProcess=()=>throw new InvalidOperationException("test original");
            try{SexUtility.ProcessSex(d.Sexprops);throw new Exception("异常未保留");}catch(InvalidOperationException){}finally{SexUtility.OnProcess=null;}
            Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"异常无收益");SexUtility.ProcessSex(d.Sexprops);
            Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"后续真实完成可认领");
        });
        Run("侵染经验回调换人格后停止所有供给",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Driver(p.a,p.b,false);Begin(d);d.ticks_left=0;
            TrainerSpecializationProgressUtility.OnAward=()=>{SSCPersonalityRuntimeUtility.TryBeginMutation(p.a,out var scope);scope.Dispose();};
            SexUtility.ProcessSex(d.Sexprops);Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards==1&&SSCPnaSexSupplyUtility.StrongCalls==0&&SSCPnaSexSupplyUtility.Calls.Count==0,"逐分量复查");
        });
        Run("侵染原结算后才开启不补领本场",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;var d=Driver(p.a,p.b,false);Begin(d);d.ticks_left=0;
            SexUtility.OnProcess=()=>p.a.Training.Infestation=true;
            try{SexUtility.ProcessSex(d.Sexprops);}finally{SexUtility.OnProcess=null;}
            Assert(SSCPnaSexSupplyUtility.StrongCalls==0&&SSCPnaSexSupplyUtility.Calls.Count==2,"旧普通来源保留");
        });
        foreach(bool started in new[]{false,true}) foreach(bool timerDone in new[]{false,true})
            Run("侵染Lovin真实开始与计时 started="+started+" timer="+timerDone,()=>
            {
                ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Lovin(p.a,p.b);
                if(started)BeginLovin(d);if(timerDone)d.FinishTimer();d.Cleanup(JobCondition.Succeeded);d.Cleanup(JobCondition.Succeeded);
                Assert(SSCPnaSexSupplyUtility.StrongCalls==(started&&timerDone?1:0),"只在真正完成时一次");
                Assert(TrainerSpecializationProgressUtility.InitiatedSexAwards==0&&SSCPnaSexSupplyUtility.Calls.Count==0,"原版适配器只分发侵染");
            });
        Run("侵染Lovin接收端伪成功不抢发起者认领",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Lovin(p.a,p.b);BeginLovin(d);
            var peer=(JobDriver_Lovin)p.b.jobs.curDriver;peer.FinishTimer();peer.Cleanup(JobCondition.Succeeded);
            Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"接收端不领取");d.FinishTimer();d.Cleanup(JobCondition.Succeeded);
            Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"原发起端仍可完成");
        });
        Run("侵染Lovin中断和替换任务无收益",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Lovin(p.a,p.b);BeginLovin(d);d.FinishTimer();
            d.Cleanup(JobCondition.Incompletable);Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"中断");
            p.a.jobs.curDriver=new JobDriver{pawn=p.a,job=new Job()};d.Cleanup(JobCondition.Succeeded);Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"迟到原版");
        });
        Run("侵染Lovin进行中与已认领存档分别恢复",()=>
        {
            foreach(bool completed in new[]{false,true})
            {
                ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Lovin(p.a,p.b);BeginLovin(d);d.FinishTimer();
                if(completed)d.Cleanup(JobCondition.Succeeded);
                Scribe.node.Clear();Scribe.mode=LoadSaveMode.Saving;d.ExposeData();
                var restored=new JobDriver_Lovin{pawn=p.a,job=d.job,Index=d.Index};p.a.jobs.curDriver=restored;
                Scribe.mode=LoadSaveMode.LoadingVars;restored.ExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;restored.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
                restored.Cleanup(JobCondition.Succeeded);restored.Cleanup(JobCondition.Succeeded);
                Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"中途档补唯一完成，已领取档不补发");
            }
        });
#if REAL_HARMONY
        Run("侵染真实Harmony跳过原结算不认领且释放占用",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;var d=Driver(p.a,p.b,false);Begin(d);d.ticks_left=0;
            InfestationSkipProbe.Skip=true;try{SexUtility.ProcessSex(d.Sexprops);}finally{InfestationSkipProbe.Skip=false;}
            Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"跳过原方法");SexUtility.ProcessSex(d.Sexprops);Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"释放占用");
        });
        Run("侵染Lovin显式排在本机rjw owner之前",()=>
        {
            var info=HarmonyLib.Harmony.GetPatchInfo(typeof(JobDriver).GetMethod("Cleanup"));
            Assert(info.Prefixes.Any(p=>p.PatchMethod.DeclaringType==typeof(SSCRestrictionJobCleanupHook)&&p.before.Contains("rjw")),"显式顺序元数据");
        });
        Run("侵染Lovin的RJW前缀回调换人格后不领取",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;
            var d=Lovin(p.a,p.b);BeginLovin(d);d.FinishTimer();
            SexUtility.OnProcess=()=>{SSCPersonalityRuntimeUtility.TryBeginMutation(p.a,out var scope);scope.Dispose();};
            try{d.Cleanup(JobCondition.Succeeded);}finally{SexUtility.OnProcess=null;}
            d.Cleanup(JobCondition.Succeeded);Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"Cleanup前快照不能被后来的同身份人格复用");
        });
        Run("侵染Lovin跳过原Cleanup不领取且异常释放占用",()=>
        {
            ResetSexAwards();var p=People();p.a.Training.pawnIdentity=PawnIdentity.Master;p.a.Training.Infestation=true;
            var d=Lovin(p.a,p.b);BeginLovin(d);d.FinishTimer();
            InfestationCleanupSkipProbe.Skip=true;try{d.Cleanup(JobCondition.Succeeded);}finally{InfestationCleanupSkipProbe.Skip=false;}
            Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"原Cleanup未执行");
            SexUtility.OnProcess=()=>throw new InvalidOperationException("Lovin original prefix");
            try{d.Cleanup(JobCondition.Succeeded);throw new Exception("异常未保留");}catch(InvalidOperationException){}finally{SexUtility.OnProcess=null;}
            Assert(SSCPnaSexSupplyUtility.StrongCalls==0,"异常无认领");d.Cleanup(JobCondition.Succeeded);
            Assert(SSCPnaSexSupplyUtility.StrongCalls==1,"临时占用释放");
        });
#endif
    }
}
#if REAL_HARMONY
[HarmonyLib.HarmonyPatch(typeof(SexUtility),nameof(SexUtility.ProcessSex))]
internal static class InfestationSkipProbe
{
    public static bool Skip;
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    public static bool Prefix()=>!Skip;
}
[HarmonyLib.HarmonyPatch(typeof(JobDriver),nameof(JobDriver.Cleanup))]
internal static class InfestationCleanupSkipProbe
{
    public static bool Skip;
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    public static bool Prefix()=>!Skip;
}
#endif
