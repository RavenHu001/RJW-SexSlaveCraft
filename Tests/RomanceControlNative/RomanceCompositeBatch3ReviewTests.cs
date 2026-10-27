// AI 辅助生成：提交前以原生回调和观察器故障复现因果边界，不在夹具中重写结算算法。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action<Thought_Memory> review3Candidate, review3Merge;
    private static bool review3Parent, review3RenewOffset;
    private static Thought_Memory review3Old;
    private static string review3Fault;
    private static int review3OldSnapshots;

    /// <summary>外部候选身份变化及记录器异常均不得变成可撤销的本次原版事实。</summary>
    private static void RomanceCompositeBatch3ReviewTests()
    {
        foreach(string change in new[]{"definition","other","owner","parent-other"})
            Check("composite b3 review native identity mutation "+change,()=>
            {
                CommandReset(1f);socialOpinionValue=0;var(a,b)=CommandPair(SSCRomanceAction.Romance);var outsider=SocialPawn();
                var h=a.needs.mood.thoughts.memories;var boundary=new Harmony("ssc.tests.composite.b3.identity");
                try
                {
                    review3Parent=change=="parent-other";
                    if(change=="definition")review3Candidate=m=>m.def=ThoughtDefOf.GotMarried;
                    else review3Merge=m=>{if(change=="owner")m.pawn=outsider;else m.otherPawn=outsider;};
                    boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)}),
                        prefix:new HarmonyMethod(typeof(Program),nameof(Review3NativeCandidate)){priority=Priority.Last});
                    boundary.Patch(AccessTools.Method(typeof(Thought_Memory),nameof(Thought_Memory.TryMergeWithExistingMemory)),
                        prefix:new HarmonyMethod(typeof(Program),nameof(Review3NativeMerge)){priority=Priority.Last});
                    boundary.Patch(AccessTools.Method(typeof(Thought_MemorySocial),nameof(Thought_MemorySocial.TryMergeWithExistingMemory)),
                        prefix:new HarmonyMethod(typeof(Program),nameof(Review3NativeMerge)){priority=Priority.Last});
                    SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                    Require(review3Candidate==null&&review3Merge==null,"mutation must actually execute");
                    Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any(),"changed source cannot authorize composite takeover");
                    Require(h.Memories.Any(m=>change=="definition"?m.def==ThoughtDefOf.GotMarried:
                        m.def.defName=="RebuffedMyRomanceAttemptMood"&&(change=="owner"?m.pawn==outsider:m.otherPawn==outsider)));
                }
                finally{boundary.UnpatchAll(boundary.Id);review3Candidate=null;review3Merge=null;review3Parent=false;}
            });
        Check("composite b3 review foreign Renew fields remain native without takeover",()=>
        {
            CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);var h=a.needs.mood.thoughts.memories;
            var def=DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood");int limit=def.stackLimit;var boundary=new Harmony("ssc.tests.composite.b3.renew");
            try
            {
                def.stackLimit=1;h.TryGainMemory(def,b);review3Old=h.Memories.Single();review3Old.age=54321;review3RenewOffset=true;
                boundary.Patch(AccessTools.Method(typeof(Thought_Memory),nameof(Thought_Memory.Renew)),postfix:new HarmonyMethod(typeof(Program),nameof(Review3RenewAfter)));
                SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                Require(!review3RenewOffset);Equal(4,review3Old.moodOffset);Equal(0,review3Old.age);
                Require(h.Memories.Contains(review3Old));Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
            }
            finally{def.stackLimit=limit;boundary.UnpatchAll(boundary.Id);review3Old=null;review3RenewOffset=false;}
        });
        foreach(string point in new[]{"entry","add","renew","remove"})
            Check("composite b3 review observer fault preserves native flow "+point,()=>
            {
                CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);var h=a.needs.mood.thoughts.memories;
                var def=DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood");int limit=def.stackLimit;var boundary=new Harmony("ssc.tests.composite.b3.observer");
                try
                {
                    if(point!="add")
                    {
                        def.stackLimit=point=="entry"?5:1;h.TryGainMemory(def,point=="remove"?SocialPawn():b);
                        review3Old=h.Memories.Single();review3Old.age=54321;
                    }
                    review3Fault=point;review3OldSnapshots=0;
                    var state=production.GetType("SexSlaveCraft.SSCRomanceNativeMood+State");
                    boundary.Patch(AccessTools.Constructor(state,new[]{typeof(Thought_Memory)}),prefix:new HarmonyMethod(typeof(Program),nameof(Review3StateFault)));
                    SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                    Require(review3Fault==null,"fault must execute once");
                    Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                    Equal(point=="entry"?2:1,h.Memories.Count(m=>m.def==def));
                    if(point=="renew")Equal(0,review3Old.age);
                    if(point=="remove")Require(!h.Memories.Contains(review3Old));
                }
                finally{def.stackLimit=limit;boundary.UnpatchAll(boundary.Id);review3Fault=null;review3Old=null;review3OldSnapshots=0;}
            });
    }

    /// <summary>在生产观察 Prefix 之后改候选，模拟其他补丁的 Def 替换。</summary>
    private static void Review3NativeCandidate(Thought_Memory __0)
    {
        if(__0.def.defName!=(review3Parent?"RebuffedMyRomanceAttempt":"RebuffedMyRomanceAttemptMood"))return;
        var action=review3Candidate;review3Candidate=null;action?.Invoke(__0);
    }
    /// <summary>原生已绑定 owner／other 后改归属，检验不能只靠最初参数确认原因。</summary>
    private static void Review3NativeMerge(Thought_Memory __instance)
    {
        if(__instance.def.defName!=(review3Parent?"RebuffedMyRomanceAttempt":"RebuffedMyRomanceAttemptMood"))return;
        var action=review3Merge;review3Merge=null;action?.Invoke(__instance);
    }
    /// <summary>外部 Renew 后置写入超出原生仅重置年龄的契约。</summary>
    private static void Review3RenewAfter(Thought_Memory __instance)
    {if(review3RenewOffset&&__instance==review3Old){review3RenewOffset=false;__instance.moodOffset=4;}}
    /// <summary>仅向观察器构造注入一次异常，原版实际思想／容量方法必须继续执行。</summary>
    private static void Review3StateFault(Thought_Memory __0)
    {
        if(review3Fault==null||__0.def.defName!="RebuffedMyRomanceAttemptMood")return;
        if(__0==review3Old)review3OldSnapshots++;
        bool hit=review3Fault=="entry"&&review3OldSnapshots==1||review3Fault=="add"&&__0!=review3Old||
            (review3Fault is "renew" or "remove")&&__0==review3Old&&review3OldSnapshots==2;
        if(hit){review3Fault=null;throw new InvalidOperationException("Injected native observer failure");}
    }
}
