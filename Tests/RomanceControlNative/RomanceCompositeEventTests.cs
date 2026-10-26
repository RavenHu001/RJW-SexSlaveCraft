// AI 辅助生成：真实生产 tracker／worker／命令驱动与原版记忆调用专项，不复制事件分类实现。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static LogEntry moodLastLog;
    private static ThoughtDef moodNativeLoss;
    private static int review3CapacityRemovals;

    /// <summary>持续思想使用当前十四阶段映射；思想准入与显示仍执行生产类型。</summary>
    private static void SetupMoodDefs()
    {
        if(SSCRomanceMoodDefOf.SSC_Romance_Control!=null)return;
        SSCRomanceMoodDefOf.Masochist=new TraitDef{defName="Masochist",degreeDatas=new(){new TraitDegreeData{degree=0,label="Masochist"}}};
        var def=new ThoughtDef{defName="SSC_Romance_Control",thoughtClass=typeof(Thought_SituationalSSCRomanceControl),stages=new()};
        for(int index=0;index<14;index++)def.stages.Add(new ThoughtStage{label="[文案待定]",description="开发占位",baseMoodEffect=SSCRomanceContinuousMood.BranchAt(index).Value});
        AccessTools.Field(typeof(ThoughtDef),"workerClass").SetValue(def,typeof(ThoughtWorker_SSCRomanceControl));
        DefDatabase<ThoughtDef>.Add(def);SSCRomanceMoodDefOf.SSC_Romance_Control=def;
        moodNativeLoss=DefDatabase<ThoughtDef>.GetNamed("BrokeUpWithMeMood");moodNativeLoss.stackLimit=1;
    }

    /// <summary>创建本方案历史；测试数据必须来自真实目录，不再构造旧载体。</summary>
    private static Thought_MemorySSCCompositeRomance MoodMemory(int value,bool longTerm=false,SSCRomanceOldRelation relation=SSCRomanceOldRelation.Lover)
    {
        var memory=CompositeMemory(value,longTerm?SSCRomanceMoodGroup.Long:SSCRomanceMoodGroup.Short);
        memory.Data.Relation=relation;
        if(longTerm)memory.durationTicksOverride=(relation==SSCRomanceOldRelation.Spouse?30:25)*60000;
        return memory;
    }

    /// <summary>十三类事件全部终端分支经过实际互动；同时验证原版匹配心情被替换、社交父记忆仍存在。</summary>
    private static void RomanceCompositeEventTests(Harmony harmony,string repo)
    {
        Check("composite native takeover anchors and actual patches ready",()=>Require((bool)AccessTools.Property(
            production.GetType("SexSlaveCraft.SSCRomanceNativeMoodHooks"),"Ready").GetValue(null)));
        foreach(var branch in SSCRomanceMoodCatalog.EventBranches)
        foreach(int raw in branch.Opinion==SSCRomanceOpinionBand.Disliked?new[]{-10}:branch.Opinion==SSCRomanceOpinionBand.Close?new[]{60}:
            branch.LongDays>0&&branch.Value<0?new[]{0,1}:new[]{0})
            Check($"composite actual event {branch.Id}/{raw}",()=>RunCompositeEvent(branch,raw));
        CompositeNativeReplacementTests();
        CompositeEventBoundaryTests(harmony);
        RomanceCompositeBatch3ReviewTests();
        Check("composite old event defs and fields removed",()=>
        {
            var xml=XDocument.Load(Path.Combine(repo,"1.7/Defs/ThoughtDefs/SSC_RomanceMood.xml"));
            Require(xml.Root.Elements().All(n=>n.Element("defName").Value=="SSC_Romance_Control"));
            Require(typeof(SSCRomanceMoodData).GetField("Kind")==null&&typeof(SSCRomanceMoodData).GetField("Corruption")==null&&typeof(SSCRomanceMoodData).GetField("Opinion")==null);
            Require(production.GetType("SexSlaveCraft.SSCRomanceMoodEvent")==null);
        });
    }

    /// <summary>输入仅控制原随机、好感和引擎边界；结果分类、目录选择、关系及替换均运行生产代码。</summary>
    private static void RunCompositeEvent(SSCRomanceMoodBranch branch,int raw,bool samePassiveResult=false)
    {
        int ev=(int)branch.Event;
        bool passive=ev<=5, accepted=ev is 1 or 2 or 6 or 8, split=ev is 5 or 10;
        var action=ev is 1 or 3 or 6 or 7?SSCRomanceAction.Romance:ev<=10?SSCRomanceAction.Proposal:SSCRomanceAction.Breakup;
        // 同结果变体只调整原版取样边界；生效规则、结果分类、替换与分组仍运行生产代码。
        bool vanilla=passive?(samePassiveResult?accepted:!accepted):accepted;
        CommandReset(vanilla?0f:1f,split?0f:1f);socialOpinionValue=raw;
        Pawn actor,target,subject;
        if(passive)
        {
            actor=SocialPawn();target=SocialSlave(severity:branch.Stage==3?.5f:.9f);subject=target;
            Comp(target).romanceConfig=new(){allowAcceptRomance=accepted,allowAcceptProposal=accepted,alwaysAcceptOthers=accepted};
            if(action==SSCRomanceAction.Proposal)actor.relations.AddDirectRelation(PawnRelationDefOf.Lover,target);
        }
        else
        {
            (actor,target)=CommandPair(action);subject=actor;
            if(ev>=12){actor.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover,target);actor.relations.AddDirectRelation(ev==12?PawnRelationDefOf.Fiance:PawnRelationDefOf.Spouse,target);}
        }
        if(branch.Masochist)subject.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist,0));
        if(passive)Require(actor.interactions.TryInteractWith(target,SocialDef(action)));
        else{SubmitAndRun(actor,target,action,out var command);Equal(SSCRomanceCommandStatus.Completed,command.Status);}
        var records=subject.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCCompositeRomance>().ToList();
        Equal(branch.Value==0?0:1,records.Count);
        if(records.Count==1)
        {
            var memory=records.Single();Equal(branch.Id,memory.Data.BranchId);Equal(branch.Value,memory.Data.Value);Equal(raw,memory.Data.RawOpinion);
            Equal(branch.LongDays>0&&branch.Value<0&&raw>0?branch.LongDays*60000:3*60000,memory.DurationTicks);
            Equal(passive?actor:target,memory.otherPawn);Equal(subject,memory.pawn);
        }
        if(ev==7){Require(!subject.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName=="RebuffedMyRomanceAttemptMood"));Require(subject.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.RebuffedMyRomanceAttempt));}
        if(ev is 9 or 10){Require(!subject.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.RejectedMyProposalMood));Require(subject.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.RejectedMyProposal));}
        if(ev==3)Require(!subject.needs.mood.thoughts.memories.Memories.Any(m=>m.def==ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood));
        Equal(1,playLogCalls);
        Require(moodLastLog is PlayLogEntry_SSCRomance);Equal(branch.Event,((PlayLogEntry_SSCRomance)moodLastLog).BackgroundFromPOV(subject).Kind);
    }

    /// <summary>直接验证原版新增、续期及容量淘汰被精准撤销，技术失败保留原版且不复活外部历史。</summary>
    private static void CompositeNativeReplacementTests()
    {
        foreach(string mode in new[]{"new","renew","capacity","zero","updated","factory-failure"})
            Check("composite native replacement "+mode,()=>
            {
                CommandReset(1f);socialOpinionValue=mode=="zero"?-10:0;var(a,b)=CommandPair(SSCRomanceAction.Romance);
                var h=a.needs.mood.thoughts.memories;var def=DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood");
                var savedLimit=def.stackLimit;var old=new List<Thought_Memory>();var boundary=CompositeFactoryBoundary();
                try
                {
                    if(mode is "renew" or "capacity")
                    {
                        def.stackLimit=1;var memory=(Thought_Memory)ThoughtMaker.MakeThought(def);h.TryGainMemory(memory,mode=="renew"?b:SocialPawn());memory.age=54321;old.Add(memory);
                    }
                    if(mode=="capacity")
                    {
                        review3CapacityRemovals=0;
                        boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.RemoveMemory)),prefix:new HarmonyMethod(typeof(Program),nameof(Review3CountCapacityRemoval)));
                    }
                    if(mode=="updated"){var memory=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,b,raw:60);h.TryGainMemory(memory,b);memory.age=765;}
                    if(mode=="factory-failure")compositeReviewBeforeFactory=()=>throw new InvalidOperationException("composite factory failure");
                    SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
                    if(mode=="capacity")Equal(1,review3CapacityRemovals);
                    if(mode=="factory-failure"){Equal(1,h.Memories.Count(m=>m.def==def));Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());}
                    else
                    {
                        Equal(old.Count,h.Memories.Count(m=>m.def==def));foreach(var memory in old){Require(h.Memories.Contains(memory));Equal(54321,memory.age);}
                        var composite=h.Memories.OfType<Thought_MemorySSCCompositeRomance>().ToList();Equal(mode=="zero"?0:1,composite.Count);
                        if(mode=="updated"){Equal(-3,composite.Single().Data.Value);Equal(0,composite.Single().age);}
                    }
                }
                finally{def.stackLimit=savedLimit;boundary.UnpatchAll(boundary.Id);compositeReviewBeforeFactory=null;}
            });
    }
    /// <summary>确认真实原生 RemoveMemory 已执行，不能用最终相同状态冒充容量分支覆盖。</summary>
    private static void Review3CountCapacityRemoval(Thought_Memory __0)
    {if(__0.def.defName=="RebuffedMyRomanceAttemptMood")review3CapacityRemovals++;}
}

/// <summary>真实 Scribe 包装，保存综合记忆、人格快照及单条互动日志，不保存运行事务。</summary>
public sealed class RomanceMoodScene:IExposable
{
    public Thought_MemorySSCRomance Memory;
    public List<StoredMemoryData> Stored;
    public PlayLogEntry_SSCRomance Log;
    public void ExposeData(){Scribe_Deep.Look(ref Memory,"memory");Scribe_Collections.Look(ref Stored,"stored",LookMode.Deep);Scribe_Deep.Look(ref Log,"log");}
}
