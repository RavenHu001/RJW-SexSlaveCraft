// AI 辅助生成：执行真实生产分组、原生准入与结果回执，不复制生产比较公式。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action<Thought_Memory> compositeSubmitCallback;
    private static Action compositeAdmissionAction;

    /// <summary>同对象与语义组才共享容量；数值、期限和文案背景不再划分名额。</summary>
    private static void RomanceCompositeGroupTests()
    {
        Check("semantic grouping crosses carriers but separates event directions and targets",()=>
        {
            var other=SocialPawn();
            var shortCarrier=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,other,raw:0);
            var longCarrier=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,other,raw:1);
            Require(shortCarrier.GroupsWith(longCarrier)&&longCarrier.GroupsWith(shortCarrier));
            var passive=SemanticMemory(SSCRomanceCompositeEvent.ForcedProposalRejected,other,raw:60);
            Require(!shortCarrier.GroupsWith(passive)&&!passive.GroupsWith(shortCarrier));
            var different=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,SocialPawn());
            Require(!shortCarrier.GroupsWith(different));
        });
        // 同事件矩阵覆盖更弱、反号和同值；预期来自目录中本次分支，不在测试复制替换比较器。
        foreach (var (previous, next) in RepresentativeReplacements())
            Check($"semantic newest branch replaces {previous.Id}/{next.Id}",()=>
            {
                var p=SocialPawn();var other=SocialPawn();var h=p.needs.mood.thoughts.memories;
                var old=BranchMemory(previous,other);h.TryGainMemory(old,other);old.age=777;
                var selection=BranchSelection(next);var result=new SSCRomanceMoodRequest(p,other,selection).Execute();
                Require(result.Completed&&result.Applied);var actual=h.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(next.Id,actual.Data.BranchId);Equal(next.Value,actual.Data.Value);Equal(0,actual.age);
                Equal(selection.DurationTicks,actual.DurationTicks);Require(!h.Memories.Contains(old));
            });
        foreach(int oldRaw in new[]{0,1}) foreach(int newRaw in new[]{0,1})
            Check($"semantic new standard lifetime replaces old {oldRaw}/{newRaw}",()=>
            {
                var p=SocialPawn();var other=SocialPawn();var h=p.needs.mood.thoughts.memories;
                var old=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,other,raw:oldRaw);
                h.TryGainMemory(old,other);old.durationTicksOverride=30*60000;old.age=60000;
                var selection=SemanticSelection(SSCRomanceCompositeEvent.CommandProposalRejected,raw:newRaw);
                Require(new SSCRomanceMoodRequest(p,other,selection).Execute().Applied);
                var actual=h.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(selection.DurationTicks,actual.DurationTicks);Equal(0,actual.age);Equal(selection.Group,actual.Data.Group);
            });
        Check("semantic other directions targets vanilla and continuous remain independent",()=>
        {
            var p=SocialPawn();var other=SocialPawn();var third=SocialPawn();var h=p.needs.mood.thoughts.memories;
            h.TryGainMemory(SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceRejected,other),other);
            h.TryGainMemory(SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other),other);
            h.TryGainMemory(SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,third),third);
            h.TryGainMemory(moodNativeLoss,other);Equal(4,h.Memories.Count);
            var selection=SemanticSelection(SSCRomanceCompositeEvent.ForcedRomanceAccepted);
            Require(new SSCRomanceMoodRequest(p,other,selection).Execute().Applied);Equal(4,h.Memories.Count);
            Require(h.Memories.Any(m=>m.def==moodNativeLoss));
            Require(h.Memories.OfType<Thought_MemorySSCRomance>().All(m=>!m.GroupsWith(new Thought_Situational{def=SSCRomanceMoodDefOf.SSC_Romance_Control})));
        });
        foreach (var (branch, raw) in RepresentativeBranches(RomanceRepresentativeCases.Admissions))
            Check($"composite every branch admission receipt {branch.Id}/{raw}",()=>
            {
                SSCRomanceMoodCatalog.TryResolve(branch.Event,branch.Stage,branch.Masochist,raw,out var selection);
                var p=SocialPawn();var result=new SSCRomanceMoodRequest(p,SocialPawn(),selection).Execute();
                Equal(branch.Value==0?SSCRomanceMoodResultKind.Zero:SSCRomanceMoodResultKind.Added,result.Kind);Require(result.Completed);
                Equal(branch.Value==0?0:1,p.needs.mood.thoughts.memories.Memories.Count);
                if(branch.Value!=0)
                {
                    var memory=p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                    Equal(branch.Id,memory.Data.BranchId);Equal(branch.Value,memory.Data.Value);Equal(selection.Group,result.Group);
                    Equal(selection.DurationTicks,result.RemainingTicks);Equal(selection.DurationTicks,memory.Data.NominalDurationTicks);
                    Require(memory.Data.OccurrenceOrder>0);
                }
            });
        Check("composite request is consumed once but same branch may be new event",()=>
        {
            var p=SocialPawn();var other=SocialPawn();var selection=SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted);
            var request=new SSCRomanceMoodRequest(p,other,selection);
            Equal(SSCRomanceMoodResultKind.Added,request.Execute().Kind);var old=(Thought_MemorySSCRomance)p.needs.mood.thoughts.memories.Memories.Single();old.age=321;
            long order=old.Data.OccurrenceOrder;Equal(SSCRomanceMoodResultKind.Duplicate,request.Execute().Kind);Equal(321,old.age);
            Equal(SSCRomanceMoodResultKind.Refreshed,new SSCRomanceMoodRequest(p,other,selection).Execute().Kind);
            var actual=(Thought_MemorySSCRomance)p.needs.mood.thoughts.memories.Memories.Single();Equal(0,actual.age);Require(actual.Data.OccurrenceOrder>order);
        });
        Check("semantic valid zero settles only matching history and consumes request",()=>
        {
            var p=SocialPawn();var other=SocialPawn();var h=p.needs.mood.thoughts.memories;
            var old=SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceRejected,other,stage:3,raw:60);h.TryGainMemory(old,other);old.age=400;
            var unrelated=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other);h.TryGainMemory(unrelated,other);
            var request=new SSCRomanceMoodRequest(p,other,SemanticSelection(SSCRomanceCompositeEvent.ForcedRomanceRejected,stage:3,raw:-10));
            var result=request.Execute();Equal(SSCRomanceMoodResultKind.Zero,result.Kind);Require(result.Completed&&!result.Applied);
            Equal(unrelated,h.Memories.Single());Equal(SSCRomanceMoodResultKind.Duplicate,request.Execute().Kind);
        });
        foreach(int delta in new[]{-1,0,1}) Check("semantic expiration never blocks newer result "+delta,()=>
        {
            var p=SocialPawn();var other=SocialPawn();var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other);
            p.needs.mood.thoughts.memories.TryGainMemory(old,other);old.age=old.DurationTicks+delta;
            Require(new SSCRomanceMoodRequest(p,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted)).Execute().Applied);
            Equal(0,p.needs.mood.thoughts.memories.Memories.Single().age);
        });
        Check("semantic replacement does not compare displayed multiplier offset or decay",()=>
        {
            var p=SocialPawn();var other=SocialPawn();var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other,raw:60);
            p.needs.mood.thoughts.memories.TryGainMemory(old,other);old.age=170000;old.moodPowerFactor=0;old.moodOffset=100;
            Require(new SSCRomanceMoodRequest(p,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted)).Execute().Applied);
            var actual=(Thought_MemorySSCRomance)p.needs.mood.thoughts.memories.Memories.Single();Equal(0,actual.age);Equal(0,actual.moodOffset);
        });
        foreach(string change in new[]{"deny","token","comp","mood","handler","payload","exception","skip","remove-after","throw-after","token-after","payload-after","age-after","definition-after","owner-after","later-event"})
            Check("semantic admission receipt callback "+change,()=>
            {
                var p=SocialPawn();var other=SocialPawn();var h=p.needs.mood.thoughts.memories;
                var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);h.TryGainMemory(old,other);old.age=4321;
                var later=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other,raw:60);
                var boundary=new Harmony("ssc.tests.composite.submit");
                try
                {
                    if(change=="deny") boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),prefix:new HarmonyMethod(typeof(Program),nameof(CompositeDenyAdmission)));
                    else
                    {
                        compositeSubmitCallback=memory=>
                        {
                            if(change.StartsWith("token")){Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p,out var scope));scope.Dispose();}
                            else if(change=="comp")AccessTools.Field(typeof(ThingWithComps),"comps").SetValue(p,new List<ThingComp>{new CompSexSlaveTraining{parent=p}});
                            else if(change=="mood")p.needs.mood=new Need_Mood(p){def=new NeedDef{defName="Mood"}};
                            else if(change=="handler")AccessTools.Field(typeof(ThoughtHandler),"memories").SetValue(p.needs.mood.thoughts,new MemoryThoughtHandler(p));
                            else if(change=="payload")((Thought_MemorySSCRomance)memory).Data.Value=-6;
                            else if(change=="payload-after")((Thought_MemorySSCRomance)memory).Data.Value=15;
                            else if(change=="age-after")memory.age=99;
                            else if(change=="definition-after")memory.def=moodNativeLoss;
                            else if(change=="owner-after")memory.pawn=SocialPawn();
                            else if(change=="exception"||change=="throw-after")throw new InvalidOperationException("test callback");
                            else if(change=="remove-after")h.Memories.Remove(memory);
                            else if(change=="later-event"){h.Memories.Remove(memory);h.Memories.Remove(old);later.pawn=p;h.Memories.Add(later);}
                        };
                        var target=AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)});
                        if(change.EndsWith("after")||change=="later-event")boundary.Patch(target,postfix:new HarmonyMethod(typeof(Program),nameof(CompositeSubmitAfter)));
                        else boundary.Patch(target,prefix:new HarmonyMethod(typeof(Program),change=="skip"?nameof(CompositeSkipSubmit):nameof(CompositeSubmitBefore)));
                    }
                    var result=new SSCRomanceMoodRequest(p,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceRejected,raw:60)).Execute();
                    Require(!result.Applied&&!result.Completed,result.Kind.ToString());
                    if(change=="later-event")Equal(later,h.Memories.Single());
                    else {Equal(old,h.Memories.Single());Equal(4321,old.age);}
                    if(change=="payload")Equal(SSCRomanceMoodResultKind.InvalidData,result.Kind);
                }
                finally{boundary.UnpatchAll(boundary.Id);compositeSubmitCallback=null;}
            });
        Check("semantic equal values retain independent direction backgrounds",()=>
        {
            var p=SocialPawn();var other=SocialPawn();var first=SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted,masochist:true,raw:60);
            var next=SemanticSelection(SSCRomanceCompositeEvent.CommandProposalAccepted,masochist:true,raw:60);
            Require(new SSCRomanceMoodRequest(p,other,first).Execute().Applied);var old=p.needs.mood.thoughts.memories.Memories.Single();old.age=500;
            Equal(SSCRomanceMoodResultKind.Added,new SSCRomanceMoodRequest(p,other,next).Execute().Kind);Equal(2,p.needs.mood.thoughts.memories.Memories.Count);Equal(500,old.age);
        });
        foreach(string change in new[]{"token","comp","mood","handler"}) Check("composite native CanGetThought callback "+change,()=>
        {
            var p=SocialPawn();var other=SocialPawn();var handler=p.needs.mood.thoughts.memories;
            var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);handler.TryGainMemory(old,other);old.age=321;
            var boundary=new Harmony("ssc.tests.composite.native-admission");
            try
            {
                compositeAdmissionAction=()=>
                {
                    if(change=="token"){Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p,out var scope));scope.Dispose();}
                    else if(change=="comp")AccessTools.Field(typeof(ThingWithComps),"comps").SetValue(p,new List<ThingComp>{new CompSexSlaveTraining{parent=p}});
                    else if(change=="mood")p.needs.mood=new Need_Mood(p){def=new NeedDef{defName="Mood"}};
                    else AccessTools.Field(typeof(ThoughtHandler),"memories").SetValue(p.needs.mood.thoughts,new MemoryThoughtHandler(p));
                };
                boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeAdmissionMutation)));
                Equal(SSCRomanceMoodResultKind.InvalidContext,new SSCRomanceMoodRequest(p,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceRejected)).Execute().Kind);
                Require(compositeAdmissionAction==null);Equal(old,handler.Memories.Single());Equal(321,old.age);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeAdmissionAction=null;}
        });
        foreach(string field in new[]{"stack","same-target","child","class"}) Check("composite unsupported carrier metadata "+field,()=>
        {
            var def=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
            int stack=def.stackLimit,sameTarget=def.stackLimitForSameOtherPawn;var child=def.thoughtToMake;var type=def.thoughtClass;
            var p=SocialPawn();var other=SocialPawn();var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);p.needs.mood.thoughts.memories.TryGainMemory(old,other);old.age=321;
            try
            {
                if(field=="stack")def.stackLimit=0;else if(field=="same-target")def.stackLimitForSameOtherPawn=0;
                else if(field=="child")def.thoughtToMake=moodNativeLoss;else def.thoughtClass=typeof(Thought_Memory);
                Equal(SSCRomanceMoodResultKind.InvalidData,new SSCRomanceMoodRequest(p,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceRejected)).Execute().Kind);
                Equal(old,p.needs.mood.thoughts.memories.Memories.Single());Equal(321,old.age);
            }
            finally{def.stackLimit=stack;def.stackLimitForSameOtherPawn=sameTarget;def.thoughtToMake=child;def.thoughtClass=type;}
        });
        Check("composite request freezes context before later caller edits",()=>
        {
            var p=SocialPawn();var master=SocialPawn();var context=new SSCRomanceMoodData{Master=master,Relation=SSCRomanceOldRelation.Spouse,HasRule=true,Rule=SSCRomanceSetting.AlwaysAcceptMaster};
            var request=new SSCRomanceMoodRequest(p,SocialPawn(),SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted),context);
            context.Master=SocialPawn();context.Relation=SSCRomanceOldRelation.None;context.HasRule=false;
            Require(request.Execute().Applied);var actual=p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Equal(master,actual.Data.Master);Equal(SSCRomanceOldRelation.Spouse,actual.Data.Relation);Require(actual.Data.HasRule);
        });
    }

    /// <summary>按实际事件及人物分支构造目录输入，避免数值相同但语义不同的夹具误判。</summary>
    private static SSCRomanceMoodSelection SemanticSelection(SSCRomanceCompositeEvent kind,int stage=4,bool masochist=false,int raw=0)
    {Require(SSCRomanceMoodCatalog.TryResolve(kind,stage,masochist,raw,out var selection));return selection;}
    /// <summary>真实历史夹具显式携带发生顺序及对象；构造本身不代表已经准入。</summary>
    private static Thought_MemorySSCCompositeRomance SemanticMemory(SSCRomanceCompositeEvent kind,Pawn other,int stage=4,bool masochist=false,int raw=0)
    {Require(Thought_MemorySSCCompositeRomance.TryCreate(SemanticSelection(kind,stage,masochist,raw),out var memory));memory.otherPawn=other;memory.Data.OccurrenceOrder=SSCRomanceMoodOrder.Next();return memory;}
    /// <summary>完整终端分支转换为同一档位中的代表输入，不依赖当前人物的实时好感。</summary>
    private static SSCRomanceMoodSelection BranchSelection(SSCRomanceMoodBranch branch)
    =>SemanticSelection(branch.Event,branch.Stage,branch.Masochist,branch.Opinion==SSCRomanceOpinionBand.Disliked?-10:branch.Opinion==SSCRomanceOpinionBand.Close?60:0);
    /// <summary>只生成合法目录分支的保存夹具，后续用例必须走实际生产准入或恢复。</summary>
    private static Thought_MemorySSCCompositeRomance BranchMemory(SSCRomanceMoodBranch branch,Pawn other)
    {Require(Thought_MemorySSCCompositeRomance.TryCreate(BranchSelection(branch),out var memory));memory.otherPawn=other;memory.Data.OccurrenceOrder=SSCRomanceMoodOrder.Next();return memory;}

    /// <summary>从真实只读目录寻找测试所需合法值／组，不构造伪分支或修改冻结值凑数。</summary>
    private static SSCRomanceMoodSelection CompositeSelection(int value,SSCRomanceMoodGroup group)
    {
        foreach(var branch in SSCRomanceMoodCatalog.EventBranches.Where(b=>b.Value==value))
        foreach(int raw in new[]{-10,0,1,60})
            if(SSCRomanceMoodCatalog.TryResolve(branch.Event,branch.Stage,branch.Masochist,raw,out var selection)&&selection.Branch.Id==branch.Id&&selection.Group==group)return selection;
        throw new InvalidOperationException($"No actual branch for {value}/{group}");
    }
    /// <summary>只构造候选，后续必须进入真实原生处理器或生产历史入口。</summary>
    private static Thought_MemorySSCCompositeRomance CompositeMemory(int value,SSCRomanceMoodGroup group)
    {Require(Thought_MemorySSCCompositeRomance.TryCreate(CompositeSelection(value,group),out var memory));memory.Data.OccurrenceOrder=SSCRomanceMoodOrder.Next();return memory;}
    /// <summary>模拟准入拒绝，仅作用于新综合记忆，不改变旧 SSC 或原版思想。</summary>
    private static bool CompositeDenyAdmission(ThoughtDef __1,ref bool __result)
    {if(__1.defName!=Thought_MemorySSCCompositeRomance.ShortDefName)return true;__result=false;return false;}
    /// <summary>精确消费单个新记忆回调，其他原生发放继续执行。</summary>
    private static void CompositeSubmitBefore(Thought_Memory __0)
    {if(__0 is not Thought_MemorySSCCompositeRomance)return;var action=compositeSubmitCallback;compositeSubmitCallback=null;action?.Invoke(__0);}
    /// <summary>模拟后置补丁写入或抛异常，回执必须在完整调用返回后确认。</summary>
    private static void CompositeSubmitAfter(Thought_Memory __0)=>CompositeSubmitBefore(__0);
    /// <summary>模拟兼容补丁跳过原生主体，不能因无异常而报告已获得。</summary>
    private static bool CompositeSkipSubmit(Thought_Memory __0)=>__0 is not Thought_MemorySSCCompositeRomance;
    /// <summary>原生准入已计算后模拟生命周期变更，不改它的布尔结论。</summary>
    private static void CompositeAdmissionMutation(ThoughtDef __1)
    {
        if(__1.defName!=Thought_MemorySSCCompositeRomance.ShortDefName)return;
        var action=compositeAdmissionAction;compositeAdmissionAction=null;action?.Invoke();
    }
}
