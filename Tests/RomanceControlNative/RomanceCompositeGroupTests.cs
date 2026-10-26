// AI 辅助生成：执行真实生产分组、原生准入与结果回执，不复制生产比较公式。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action<Thought_Memory> compositeSubmitCallback;
    private static Action compositeAdmissionAction;

    /// <summary>综合两个载体各自共享容量，比较冻结值，专项独立于尚未切换的互动分类。</summary>
    private static void RomanceCompositeGroupTests()
    {
        var shortCarrier=CompositeMemory(-2,SSCRomanceMoodGroup.Short);var longCarrier=CompositeMemory(-2,SSCRomanceMoodGroup.Long);
        Check("composite two carriers symmetric separate groups",()=>{Require(shortCarrier.GroupsWith(shortCarrier));Require(longCarrier.GroupsWith(longCarrier));Require(!shortCarrier.GroupsWith(longCarrier)&&!longCarrier.GroupsWith(shortCarrier));});
        foreach(int oldValue in new[]{-9,-6,-2,2,6,15}) foreach(int newValue in new[]{-9,-6,-2,2,6,15})
            Check($"composite new to new short {oldValue}/{newValue}",()=>
            {
                var p=SocialPawn();var old=CompositeMemory(oldValue,SSCRomanceMoodGroup.Short);p.needs.mood.thoughts.memories.TryGainMemory(old);old.age=777;
                int[] priority={-9,-6,-2,15,6,2};bool wins=Array.IndexOf(priority,newValue)<=Array.IndexOf(priority,oldValue);
                var result=new SSCRomanceMoodRequest(p,SocialPawn(),CompositeSelection(newValue,SSCRomanceMoodGroup.Short)).Execute();
                Equal(wins?newValue:oldValue,p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single().Data.Value);
                Equal(wins,result.Applied);Equal(wins?0:777,p.needs.mood.thoughts.memories.Memories.Single().age);
            });
        foreach(int oldValue in new[]{-5,-3,-1}) foreach(int newValue in new[]{-5,-3,-1})
            Check($"composite long replacement {oldValue}/{newValue}",()=>
            {
                var p=SocialPawn();var handler=p.needs.mood.thoughts.memories;
                var old=CompositeMemory(oldValue,SSCRomanceMoodGroup.Long);
                handler.TryGainMemory(old);old.durationTicksOverride=30*60000;old.age=60000;
                var next=CompositeMemory(newValue,SSCRomanceMoodGroup.Long);next.durationTicksOverride=25*60000;
                handler.TryGainMemory(next);var actual=handler.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(Math.Min(oldValue,newValue),actual.Data.Value);
                Equal(newValue<oldValue?25*60000:29*60000,actual.DurationTicks-actual.age);
                Require(newValue<=oldValue?ReferenceEquals(next,actual):ReferenceEquals(old,actual));
            });
        Check("composite group counts remain separate from vanilla and continuous",()=>
        {
            var p=SocialPawn();var h=p.needs.mood.thoughts.memories;
            h.TryGainMemory(MoodMemory(-2));h.TryGainMemory(MoodMemory(-3,true));h.TryGainMemory(moodNativeLoss,SocialPawn());
            var r=new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute();Require(r.Applied);
            Equal(3,h.Memories.Count);Equal(1,h.Memories.Count(m=>m.def==moodNativeLoss));Equal(1,h.Memories.Count(m=>SSCRomanceMoodGroups.GroupOf(m)==SSCRomanceMoodGroup.Long));
            Require(!h.Memories[0].GroupsWith(new Thought_Situational{def=SSCRomanceMoodDefOf.SSC_Romance_Control}));
        });
        foreach(var branch in SSCRomanceMoodCatalog.EventBranches)
        foreach(int raw in branch.Opinion==SSCRomanceOpinionBand.Disliked?new[]{-10}:branch.Opinion==SSCRomanceOpinionBand.Close?new[]{60}:
            branch.LongDays>0&&branch.Value<0?new[]{0,1}:new[]{0})
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
                }
            });
        Check("composite request is consumed once but same branch may be new event",()=>
        {
            var p=SocialPawn();var selection=CompositeSelection(15,SSCRomanceMoodGroup.Short);var request=new SSCRomanceMoodRequest(p,null,selection);
            Equal(SSCRomanceMoodResultKind.Added,request.Execute().Kind);var old=p.needs.mood.thoughts.memories.Memories.Single();old.age=321;
            Equal(SSCRomanceMoodResultKind.Duplicate,request.Execute().Kind);Equal(321,old.age);
            Equal(SSCRomanceMoodResultKind.Refreshed,new SSCRomanceMoodRequest(p,null,selection).Execute().Kind);Equal(0,p.needs.mood.thoughts.memories.Memories.Single().age);
        });
        Check("composite valid zero preserves old history and consumes request",()=>
        {
            var p=SocialPawn();var old=MoodMemory(-2);p.needs.mood.thoughts.memories.TryGainMemory(old);old.age=400;
            Require(SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.ForcedRomanceRejected,3,false,-10,out var selection));
            var request=new SSCRomanceMoodRequest(p,null,selection);var result=request.Execute();Equal(SSCRomanceMoodResultKind.Zero,result.Kind);Require(result.Completed&&!result.Applied);
            Equal(400,old.age);Equal(old,p.needs.mood.thoughts.memories.Memories.Single());Equal(SSCRomanceMoodResultKind.Duplicate,request.Execute().Kind);
        });
        foreach(int delta in new[]{-1,0,1}) Check("composite native expiration boundary "+delta,()=>
        {
            var p=SocialPawn();var old=MoodMemory(-6);p.needs.mood.thoughts.memories.TryGainMemory(old);old.age=old.DurationTicks+delta;
            var result=new SSCRomanceMoodRequest(p,null,CompositeSelection(15,SSCRomanceMoodGroup.Short)).Execute();
            Equal(delta==1?SSCRomanceMoodResultKind.Added:SSCRomanceMoodResultKind.Kept,result.Kind);
        });
        Check("composite frozen comparison ignores current display multiplier offset decay",()=>
        {
            var p=SocialPawn();var old=CompositeMemory(-9,SSCRomanceMoodGroup.Short);p.needs.mood.thoughts.memories.TryGainMemory(old);
            old.age=170000;old.moodPowerFactor=0;old.moodOffset=100;
            Equal(SSCRomanceMoodResultKind.Kept,new SSCRomanceMoodRequest(p,null,CompositeSelection(-6,SSCRomanceMoodGroup.Short)).Execute().Kind);
            Equal(170000,old.age);Equal(100,old.moodOffset);
        });
        foreach(string change in new[]{"deny","token","comp","mood","handler","payload","exception","skip","remove-after","throw-after","token-after","payload-after","age-after","definition-after","owner-after","later-event"})
            Check("composite admission receipt callback "+change,()=>
            {
                var p=SocialPawn();var h=p.needs.mood.thoughts.memories;var old=MoodMemory(-2);h.TryGainMemory(old);old.age=4321;
                var later=CompositeMemory(-9,SSCRomanceMoodGroup.Short);later.Data.BranchId="LaterHistory";
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
                            else if(change=="later-event"){h.Memories.Remove(memory);h.Memories.Add(later);}
                        };
                        var target=AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)});
                        if(change.EndsWith("after")||change=="later-event")boundary.Patch(target,postfix:new HarmonyMethod(typeof(Program),nameof(CompositeSubmitAfter)));
                        else boundary.Patch(target,prefix:new HarmonyMethod(typeof(Program),change=="skip"?nameof(CompositeSkipSubmit):nameof(CompositeSubmitBefore)));
                    }
                    var result=new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute();Require(!result.Applied&&!result.Completed,result.Kind.ToString());
                    if(change=="later-event"){Equal(later,h.Memories.Single());}
                    else if(change=="token-after"){Require(!h.Memories.OfType<Thought_MemorySSCCompositeRomance>().Any());}
                    else {Equal(old,h.Memories.Single());Equal(4321,old.age);}
                    if(change=="payload")Equal(SSCRomanceMoodResultKind.InvalidData,result.Kind);
                }
                finally{boundary.UnpatchAll(boundary.Id);compositeSubmitCallback=null;}
            });
        Check("composite same numeric value replaces independent branch background",()=>
        {
            var p=SocialPawn();SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandRomanceAccepted,4,true,60,out var first);
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandProposalAccepted,4,true,60,out var next);
            Require(new SSCRomanceMoodRequest(p,null,first).Execute().Applied);p.needs.mood.thoughts.memories.Memories.Single().age=500;
            Equal(SSCRomanceMoodResultKind.Refreshed,new SSCRomanceMoodRequest(p,null,next).Execute().Kind);
            Equal(next.Branch.Id,p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single().Data.BranchId);
        });
        foreach(string change in new[]{"token","comp","mood","handler"}) Check("composite native CanGetThought callback "+change,()=>
        {
            var p=SocialPawn();var handler=p.needs.mood.thoughts.memories;var old=MoodMemory(-2);handler.TryGainMemory(old);old.age=321;
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
                Equal(SSCRomanceMoodResultKind.InvalidContext,new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute().Kind);
                Require(compositeAdmissionAction==null);Equal(old,handler.Memories.Single());Equal(321,old.age);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeAdmissionAction=null;}
        });
        foreach(string field in new[]{"stack","same-target","child","class"}) Check("composite unsupported carrier metadata "+field,()=>
        {
            var def=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
            int stack=def.stackLimit,sameTarget=def.stackLimitForSameOtherPawn;var child=def.thoughtToMake;var type=def.thoughtClass;
            var p=SocialPawn();var old=MoodMemory(-2);p.needs.mood.thoughts.memories.TryGainMemory(old);old.age=321;
            try
            {
                if(field=="stack")def.stackLimit=0;else if(field=="same-target")def.stackLimitForSameOtherPawn=0;
                else if(field=="child")def.thoughtToMake=moodNativeLoss;else def.thoughtClass=typeof(Thought_Memory);
                Equal(SSCRomanceMoodResultKind.InvalidData,new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute().Kind);
                Equal(old,p.needs.mood.thoughts.memories.Memories.Single());Equal(321,old.age);
            }
            finally{def.stackLimit=stack;def.stackLimitForSameOtherPawn=sameTarget;def.thoughtToMake=child;def.thoughtClass=type;}
        });
        Check("composite request freezes context before later caller edits",()=>
        {
            var p=SocialPawn();var master=SocialPawn();var context=new SSCRomanceMoodData{Master=master,Relation=SSCRomanceOldRelation.Spouse,HasRule=true,Rule=SSCRomanceSetting.AlwaysAcceptMaster};
            var request=new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short),context);
            context.Master=SocialPawn();context.Relation=SSCRomanceOldRelation.None;context.HasRule=false;
            Require(request.Execute().Applied);var actual=p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Equal(master,actual.Data.Master);Equal(SSCRomanceOldRelation.Spouse,actual.Data.Relation);Require(actual.Data.HasRule);
        });
        foreach(int oldRemaining in new[]{10,29}) foreach(int newValue in new[]{-15,-13})
            Check($"composite table long refresh receipt {oldRemaining}/{newValue}",()=>
            {
                var p=SocialPawn();var old=CompositeMemory(-15,SSCRomanceMoodGroup.Long);p.needs.mood.thoughts.memories.TryGainMemory(old);old.durationTicksOverride=30*60000;old.age=(30-oldRemaining)*60000;
                var selection=CompositeSelection(newValue,SSCRomanceMoodGroup.Long);var result=new SSCRomanceMoodRequest(p,null,selection).Execute();
                var actual=p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(newValue==-15?SSCRomanceMoodResultKind.Refreshed:SSCRomanceMoodResultKind.Kept,result.Kind);
                Equal(newValue==-15?Math.Max(oldRemaining*60000,selection.DurationTicks):oldRemaining*60000,actual.DurationTicks-actual.age);
                if(newValue==-15)Equal(selection.DurationTicks,actual.Data.NominalDurationTicks);
            });
    }

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
    {Require(Thought_MemorySSCCompositeRomance.TryCreate(CompositeSelection(value,group),out var memory));return memory;}
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
