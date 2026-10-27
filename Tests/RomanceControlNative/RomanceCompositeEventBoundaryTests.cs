// AI 辅助生成：原生真实事件的双边、准入、生命周期及已安装补丁失效边界。
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action eventWorkerCallback;
    private static ThoughtDef eventDeniedDef;

    /// <summary>三类对象／八种保证组合经过原 worker，按实际命中规则结算，包含原判定相同。</summary>
    private static void CompositeEventBoundaryTests(Harmony owner)
    {
        foreach (var (mask, category, allow, original, action) in RomanceRepresentativeCases.Guarantees)
        {
            int choices=mask,cat=category;
            Check($"composite actual guarantees {mask}/{category}/{allow}/{original}/{action}",()=>
            {
                CommandReset(original?0f:1f,1f);socialOpinionValue=0;
                var a=cat==1?SocialSlave(severity:.1f):SocialPawn();var b=SocialSlave(cat==0?a:null,.5f);
                Comp(b).romanceConfig=new(){allowAcceptRomance=allow,allowAcceptProposal=allow,
                    alwaysAcceptMaster=(choices&1)!=0,alwaysAcceptSexSlave=(choices&2)!=0,alwaysAcceptOthers=(choices&4)!=0};
                if(action==SSCRomanceAction.Proposal)a.relations.AddDirectRelation(PawnRelationDefOf.Lover,b);
                Require(a.interactions.TryInteractWith(b,SocialDef(action)));
                bool accepted=(choices&(1<<cat))!=0||allow&&original;
                var memories=b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().ToList();
                Equal((choices&(1<<cat))!=0||!allow?1:0,memories.Count);
                if(memories.Count>0)Equal(action==SSCRomanceAction.Romance?(accepted?SSCRomanceCompositeEvent.ForcedRomanceAccepted:SSCRomanceCompositeEvent.ForcedRomanceRejected):
                    accepted?SSCRomanceCompositeEvent.ForcedProposalAccepted:SSCRomanceCompositeEvent.ForcedProposalRejected,memories.Single().Data.CompositeEvent);
                Require(!a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
            });
        }
        foreach(string mode in new[]{"parent-denied","child-denied","ssc-denied","ssc-skipped","late-native-write","worker-throws"})
            Check("composite technical failure preserves native "+mode,()=>
            {
                CommandReset(1f);socialOpinionValue=0;var(a,b)=CommandPair(SSCRomanceAction.Romance);var h=a.needs.mood.thoughts.memories;
                var native=DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood");var boundary=new Harmony("ssc.tests.composite.event-failure");
                try
                {
                    if(mode.EndsWith("denied"))
                    {
                        eventDeniedDef=mode=="parent-denied"?ThoughtDefOf.RebuffedMyRomanceAttempt:mode=="child-denied"?native:DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
                        boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),prefix:new HarmonyMethod(typeof(Program),nameof(EventDenyThought)));
                    }
                    if(mode=="ssc-skipped")boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)}),prefix:new HarmonyMethod(typeof(Program),nameof(CompositeSkipSubmit)));
                    if(mode=="late-native-write")
                    {
                        var old=MoodMemory(-2);h.TryGainMemory(old);old.age=1234;
                        compositeSubmitCallback=_=>h.TryGainMemory(native,SocialPawn());
                        boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)}),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeSubmitAfter)));
                    }
                    if(mode=="worker-throws")
                    {
                        eventWorkerCallback=()=>throw new InvalidOperationException("after worker");
                        boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt),"Interacted"),postfix:new HarmonyMethod(typeof(Program),nameof(EventWorkerAfter)));
                    }
                    try{SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);}catch(InvalidOperationException)when(mode=="worker-throws"){}
                    Equal(mode is "parent-denied" or "child-denied"?0:mode=="late-native-write"?2:1,h.Memories.Count(m=>m.def==native));
                    if(mode=="late-native-write"){Equal(-2,h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().Data.Value);Equal(1234,h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().age);}
                    else Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                }
                finally{boundary.UnpatchAll(boundary.Id);eventDeniedDef=null;eventWorkerCallback=null;compositeSubmitCallback=null;}
            });
        foreach(string when in new[]{"decision","factory","admission"})foreach(string change in new[]{"token","comp","mood","handler"})
            Check($"composite original event holder {when}/{change}",()=>
            {
                CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);var old=a.needs.mood.thoughts.memories;
                Action mutation=()=>
                {
                    if(change=="token"){Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a,out var scope));scope.Dispose();}
                    else if(change=="comp")AccessTools.Field(typeof(ThingWithComps),"comps").SetValue(a,new List<ThingComp>{new CompSexSlaveTraining{parent=a,pawnIdentity=PawnIdentity.Slave}});
                    else if(change=="mood")a.needs.mood=new Need_Mood(a){def=new NeedDef{defName="Mood"}};
                    else AccessTools.Field(typeof(ThoughtHandler),"memories").SetValue(a.needs.mood.thoughts,new MemoryThoughtHandler(a));
                };
                var boundary=new Harmony("ssc.tests.composite.event-holder");
                try
                {
                    if(when=="decision")
                    {
                        eventWorkerCallback=mutation;
                        boundary.Patch(AccessTools.Method(typeof(SSCRomanceMoodEvents),nameof(SSCRomanceMoodEvents.Observe)),postfix:new HarmonyMethod(typeof(Program),nameof(EventWorkerAfter)));
                    }
                    else if(when=="factory")
                    {
                        compositeReviewBeforeFactory=mutation;
                        boundary.Patch(AccessTools.Method(typeof(ThoughtMaker),nameof(ThoughtMaker.MakeThought),new[]{typeof(ThoughtDef),typeof(int)}),prefix:new HarmonyMethod(typeof(Program),nameof(CompositeReviewFactoryPrefix)));
                    }
                    else
                    {
                        compositeAdmissionAction=mutation;
                        boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeAdmissionMutation)));
                    }
                    SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                    Require(eventWorkerCallback==null&&compositeReviewBeforeFactory==null&&compositeAdmissionAction==null);
                    Require(!old.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());Require(!a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                }
                finally{boundary.UnpatchAll(boundary.Id);eventWorkerCallback=null;compositeReviewBeforeFactory=null;compositeAdmissionAction=null;}
            });
        foreach(string name in new[]{"Worker","Definition","Memory","Renew"})
            Check("composite actual removed native hook "+name,()=>
            {
                var hook=production.GetType("SexSlaveCraft.Harmony_SSCRomanceNative"+name);var transform=AccessTools.Method(hook,"Transpiler");
                MethodBase target=name=="Worker"?AccessTools.Method(typeof(InteractionWorker_RomanceAttempt),"Interacted"):name=="Definition"?
                    AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(ThoughtDef),typeof(Pawn),typeof(Precept)}):name=="Memory"?
                    AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)}):AccessTools.Method(typeof(Thought_Memory),nameof(Thought_Memory.TryMergeWithExistingMemory));
                owner.Unpatch(target,transform);
                try
                {
                    CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                    Require(a.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName=="RebuffedMyRomanceAttemptMood"));
                    Require(!a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                }
                finally{owner.Patch(target,transpiler:new HarmonyMethod(transform));}
            });
        CompositeEventAdditionalTests();
        CompositeNativeAnchorTests();
    }

    /// <summary>只拒绝指定思想，实际原生方法自己处理准入后的父／子传播。</summary>
    private static bool EventDenyThought(ThoughtDef __1,ref bool __result){if(__1!=eventDeniedDef)return true;__result=false;return false;}
    /// <summary>一次性后置回调，异常及生命周期变更不干扰其他用例。</summary>
    private static void EventWorkerAfter(){var callback=eventWorkerCallback;eventWorkerCallback=null;callback?.Invoke();}

    /// <summary>补充双方独立结算、冻结分支、直接 worker 与不确定关系证据。</summary>
    private static void CompositeEventAdditionalTests()
    {
        foreach(bool denyActor in new[]{false,true})Check("composite two controlled sides independently settle "+denyActor,()=>
        {
            CommandReset(0f);socialOpinionValue=60;var(a,b)=CommandPair(SSCRomanceAction.Romance,true);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            var boundary=new Harmony("ssc.tests.composite.two-sides");
            try
            {
                if(denyActor){eventDeniedOwner=a;boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),prefix:new HarmonyMethod(typeof(Program),nameof(EventDenyOwner)));}
                SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                Equal(denyActor?0:1,a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Count());
                Equal("E03-S4-N-C",b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().Data.BranchId);
            }
            finally{boundary.UnpatchAll(boundary.Id);eventDeniedOwner=null;}
        });
        Check("composite event keeps preinteraction opinion and effective trait",()=>
        {
            CommandReset(1f);socialOpinionValue=60;var(a,b)=CommandPair(SSCRomanceAction.Romance);a.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist,0));
            var boundary=new Harmony("ssc.tests.composite.freeze-result");
            try
            {
                eventWorkerCallback=()=>{socialOpinionValue=-100;a.story.traits.allTraits.Clear();};
                boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt),"Interacted"),postfix:new HarmonyMethod(typeof(Program),nameof(EventWorkerAfter)));
                SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                Equal("E07-S4-M-C",a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().Data.BranchId);
            }
            finally{boundary.UnpatchAll(boundary.Id);eventWorkerCallback=null;}
        });
        Check("composite direct worker retains native without event context",()=>
        {
            CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);
            InteractionDefOf.RomanceAttempt.Worker.Interacted(a,b,new List<RulePackDef>(),out _,out _,out _,out _);
            Require(!a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
            Require(a.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName=="RebuffedMyRomanceAttemptMood"));
        });
    }
    private static Pawn eventDeniedOwner;
    private static Pawn eventOpinionPawn;
    /// <summary>仅拒绝一方 SSC 准入，验证另一方不会被整帧短路。</summary>
    private static bool EventDenyOwner(Pawn __0,ThoughtDef __1,ref bool __result)
    {if(__0!=eventDeniedOwner||__1.defName!=Thought_MemorySSCCompositeRomance.ShortDefName)return true;__result=false;return false;}

    /// <summary>在已固定的原生好感输入上模拟父社交记忆生效；判断顺序和低好感心情调用仍由原版执行。</summary>
    private static void EventOpinionAfter(Pawn_RelationsTracker __instance,ref int __result)
    {
        if(eventOpinionPawn?.relations==__instance)__result=eventOpinionPawn.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.FailedRomanceAttemptOnMe)?-14:1;
    }

    /// <summary>零值定义失配、真实拒绝后跨零及原生 IL 包装锚点的失败关闭专项。</summary>
    private static void CompositeNativeAnchorTests()
    {
        Check("composite unknown native memory subclass is preserved without takeover",()=>
        {
            CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);var h=a.needs.mood.thoughts.memories;
            var def=DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood");
            var custom=new CompositeUnprovenNativeMemory{def=def,pawn=a,otherPawn=b,age=1234};h.Memories.Add(custom);
            SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
            Require(h.Memories.Contains(custom));Equal(1234,custom.age);Equal(2,h.Memories.Count(m=>m.def==def));
            Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
        });
        Check("composite zero cannot consume native with malformed carrier",()=>
        {
            CommandReset(0f);socialOpinionValue=-10;var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            var carrier=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);var stage=carrier.stages[15];
            try
            {
                carrier.stages[15]=null;Require(a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt));
                Require(b.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood));
                Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
            }
            finally{carrier.stages[15]=stage;}
        });
        Check("composite recipient raw positive still replaces actually emitted native low-opinion mood",()=>
        {
            CommandReset(0f);var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            eventOpinionPawn=b;var boundary=new Harmony("ssc.tests.composite.opinion-cross-zero");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker),nameof(Pawn_RelationsTracker.OpinionOf)),postfix:new HarmonyMethod(typeof(Program),nameof(EventOpinionAfter)));
                Require(a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt));
                Equal(1,b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Single().Data.RawOpinion);
                Require(!b.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood));
            }
            finally{boundary.UnpatchAll(boundary.Id);eventOpinionPawn=null;}
        });
        var hook=production.GetType("SexSlaveCraft.Harmony_SSCRomanceNativeDefinition");var transform=AccessTools.Method(hook,"Transpiler");
        var target=AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(ThoughtDef),typeof(Pawn),typeof(Precept)});
        List<CodeInstruction> Rewrite(List<CodeInstruction> codes)=>((IEnumerable<CodeInstruction>)transform.Invoke(null,new object[]{codes,target})).ToList();
        foreach(string mode in new[]{"missing","duplicate","exception-region"})Check("composite native journal unsupported IL "+mode,()=>
        {
            var original=PatchProcessor.GetOriginalInstructions(target).ToList();var codes=mode=="missing"?new List<CodeInstruction>{new(OpCodes.Ret)}:original.Select(c=>new CodeInstruction(c)).ToList();
            if(mode=="duplicate")codes.AddRange(original);if(mode=="exception-region")codes[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            try
            {
                var output=Rewrite(codes);Equal(codes.Count,output.Count);Require(codes.Zip(output).All(p=>p.First.opcode==p.Second.opcode&&Equals(p.First.operand,p.Second.operand)));
                CommandReset(1f);var(a,b)=CommandPair(SSCRomanceAction.Romance);SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                Require(!a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());
                Require(a.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName=="RebuffedMyRomanceAttemptMood"));
            }
            finally{Rewrite(original);}
        });
        Check("composite native journal wrapper preserves incoming labels",()=>
        {
            var original=PatchProcessor.GetOriginalInstructions(target).ToList();var objectGain=AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)});
            int at=original.FindIndex(c=>c.Calls(objectGain));var generator=new DynamicMethod("native-label",typeof(void),Type.EmptyTypes).GetILGenerator();var label=generator.DefineLabel();original[at].labels.Add(label);
            var output=Rewrite(original);Equal(original.Count,output.Count);Require(output[at].labels.Contains(label));Equal(OpCodes.Call,output[at].opcode);
            Equal("GainObject",((MethodInfo)output[at].operand).Name);
        });
    }
}

/// <summary>未登记的外部记忆类型；其内部字段不属于 SSC 原版撤销契约。</summary>
public sealed class CompositeUnprovenNativeMemory:Thought_Memory { }
