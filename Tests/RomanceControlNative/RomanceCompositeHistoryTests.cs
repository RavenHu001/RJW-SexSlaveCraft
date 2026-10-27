// AI 辅助生成：真实 Scribe、人格采集／加工／植入、原生 CopyNeeds 与 SSC 新生／成熟路径。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>历史恢复不刷新、不借新事件强弱规则续期；未知格式保存，完整生命周期不复制授权。</summary>
    private static void RomanceCompositeHistoryTests()
    {
        foreach(bool reverse in new[]{false,true}) foreach(bool longTerm in new[]{false,true})
            Check($"semantic history newest whole record independent of input order {reverse}/{longTerm}",()=>
            {
                var target=SocialPawn();
                var old=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:longTerm?1:0);old.age=50000;
                var newer=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,masochist:true,raw:longTerm?1:0);
                newer.age=70000;newer.durationTicksOverride=(longTerm?30:3)*60000;
                var source=SocialPawn();old.pawn=newer.pawn=source;
                var list=new List<StoredMemoryData>{PersonalityMemoryUtility.Capture(old),PersonalityMemoryUtility.Capture(newer)};
                if(reverse)list.Reverse();var host=SocialPawn();PersonalityMemoryUtility.Restore(host,list);
                var actual=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(newer.def,actual.def);Equal(70000,actual.age);Equal(newer.Data.Value,actual.Data.Value);
                Equal(newer.Data.OccurrenceOrder,actual.Data.OccurrenceOrder);Equal(host,actual.pawn);Equal(target,actual.otherPawn);
                Equal((longTerm?30:3)*60000,actual.durationTicksOverride);
            });
        Check("semantic history picks weaker latest record without merging lifetimes",()=>
        {
            var target=SocialPawn();
            var stronger=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:60);stronger.age=20*60000;stronger.durationTicksOverride=30*60000;
            var weaker=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,masochist:true,raw:60);weaker.age=60000;weaker.durationTicksOverride=25*60000;
            var host=SocialPawn();PersonalityMemoryUtility.Restore(host,new(){PersonalityMemoryUtility.Capture(weaker),PersonalityMemoryUtility.Capture(stronger)});
            var actual=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Equal(weaker.Data.BranchId,actual.Data.BranchId);Equal(24*60000,actual.DurationTicks-actual.age);Equal(60000,actual.age);
        });
        Check("semantic exact occurrence ties choose deterministic whole record",()=>
        {
            var target=SocialPawn();
            var first=SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceAccepted,target,masochist:true,raw:60);
            var second=SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceAccepted,target,masochist:true,raw:0);
            first.age=second.age=12345;second.Data.OccurrenceOrder=first.Data.OccurrenceOrder;
            string selected=null;
            foreach(bool reverse in new[]{false,true})
            {
                var list=new List<StoredMemoryData>{PersonalityMemoryUtility.Capture(first),PersonalityMemoryUtility.Capture(second)};if(reverse)list.Reverse();
                var host=SocialPawn();PersonalityMemoryUtility.Restore(host,list);var actual=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                selected??=actual.Data.BranchId;Equal(selected,actual.Data.BranchId);Equal(12345,actual.age);Equal(first.Data.OccurrenceOrder,actual.Data.OccurrenceOrder);
            }
        });
        foreach(string kind in new[]{"id","version","raw","group","payload"}) Check("semantic corrupt history preserves evidence "+kind,()=>
        {
            var target=SocialPawn();var known=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:60);known.age=123;
            var unknown=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,masochist:true,raw:60);unknown.age=456;
            if(kind=="id")unknown.Data.BranchId="Retired";
            else if(kind=="version")unknown.Data.DataVersion=99;
            else if(kind=="raw")unknown.Data.HasRawOpinion=false;
            else if(kind=="group")unknown.Data.Group=SSCRomanceMoodGroup.None;
            else unknown.Data=null;
            var host=SocialPawn();PersonalityMemoryUtility.Restore(host,new(){PersonalityMemoryUtility.Capture(known),PersonalityMemoryUtility.Capture(unknown)});
            var all=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().ToList();
            bool opaque=kind=="version"||kind=="payload";
            Equal(opaque?2:1,all.Count);Require(all.Any(m=>m.age==456));
            if(opaque)Equal(SSCRomanceMoodResultKind.Conflict,new SSCRomanceMoodRequest(host,target,SemanticSelection(SSCRomanceCompositeEvent.CommandProposalRejected,raw:60)).Execute().Kind);
            else Equal(unknown.Data.Value,all.Single().Data.Value);
            Require(all.All(m=>m.age==123||m.age==456));
        });
        Check("composite restore twice preserves age duration and instance modifiers",()=>
        {
            var original=CompositeMemory(-15,SSCRomanceMoodGroup.Long);original.age=12345;original.durationTicksOverride=29*60000+12345;
            original.moodPowerFactor=.5f;original.moodOffset=2;original.otherPawn=SocialPawn();original.Data.Master=SocialPawn();
            var saved=PersonalityMemoryUtility.Capture(original);var host=SocialPawn();
            for(int i=0;i<2;i++)PersonalityMemoryUtility.Restore(host,new(){saved});
            var actual=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Equal(12345,actual.age);Equal(29*60000,actual.DurationTicks-actual.age);Equal(.5f,actual.moodPowerFactor);Equal(2,actual.moodOffset);
            Equal(original.otherPawn,actual.otherPawn);Equal(original.Data.Master,actual.Data.Master);Equal(original.Data.OccurrenceOrder,actual.Data.OccurrenceOrder);
            Require(!ReferenceEquals(original.Data,actual.Data));
        });
        foreach(int[] order in new[]{new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}})
            Check("semantic opaque affected key postpones normalization "+string.Join(",",order),()=>
            {
                var target=SocialPawn();var weak=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:1);
                var strong=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:60);
                var opaque=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,masochist:true,raw:60);opaque.Data.DataVersion=99;
                weak.age=111;strong.age=222;opaque.age=333;
                var source=new[]{weak,strong,opaque};var host=SocialPawn();
                PersonalityMemoryUtility.Restore(host,order.Select(i=>PersonalityMemoryUtility.Capture(source[i])).ToList());
                Equal(3,host.needs.mood.thoughts.memories.Memories.Count);
                Require(host.needs.mood.thoughts.memories.Memories.Select(m=>m.age).OrderBy(a=>a).SequenceEqual(new[]{111,222,333}));
            });
        foreach(string change in new[]{"token","comp","mood","handler","source-data"}) Check("composite restoration callback original context "+change,()=>
        {
            var host=SocialPawn();var handler=host.needs.mood.thoughts.memories;
            var original=CompositeMemory(-15,SSCRomanceMoodGroup.Long);original.age=12345;
            var data=PersonalityMemoryUtility.Capture(original);var boundary=CompositeFactoryBoundary();
            try
            {
                compositeReviewBeforeFactory=()=>
                {
                    if(change=="token"){Require(SSCPersonalityRuntimeUtility.TryBeginMutation(host,out var scope));scope.Dispose();}
                    else if(change=="comp")AccessTools.Field(typeof(ThingWithComps),"comps").SetValue(host,new List<ThingComp>{new CompSexSlaveTraining{parent=host}});
                    else if(change=="mood")host.needs.mood=new Need_Mood(host){def=new NeedDef{defName="Mood"}};
                    else if(change=="handler")AccessTools.Field(typeof(ThoughtHandler),"memories").SetValue(host.needs.mood.thoughts,new MemoryThoughtHandler(host));
                    else {data.age=0;data.romanceMood.Value=15;}
                };
                PersonalityMemoryUtility.Restore(host,new(){data});Require(compositeReviewBeforeFactory==null);
                if(change=="source-data"){var actual=handler.Memories.OfType<Thought_MemorySSCRomance>().Single();Equal(12345,actual.age);Equal(-15,actual.Data.Value);}
                else {Equal(0,handler.Memories.Count);Equal(0,host.needs.mood.thoughts.memories.Memories.Count);}
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeReviewBeforeFactory=null;}
        });
        var lifecycle=new Harmony("ssc.tests.composite.history");
        lifecycle.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodLoadedHistory")).Patch();
        lifecycle.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodCopiedHistory")).Patch();
        lifecycle.CreateClassProcessor(production.GetType("SexSlaveCraft.SSCRestrictionCloneHook")).Patch();
        lifecycle.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker),nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)),prefix:new HarmonyMethod(typeof(Program),nameof(Skip)));
        try
        {
            Check("composite actual handler Scribe performs postload normalization without replay",()=>
            {
                var source=SocialPawn();var target=SocialPawn();var old=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:1);old.pawn=source;old.age=50000;
                var next=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,target,raw:60);next.pawn=source;next.age=12345;next.durationTicksOverride=29*60000+next.age;
                source.needs.mood.thoughts.memories.Memories.AddRange(new[]{old,next});
                var scene=new RomanceCompositeHistoryScene(source);string path=Path.Combine(saveDirectory,"composite-history-"+(++id)+".xml");
                Scribe.saver.InitSaving(path,"History");Scribe_Deep.Look(ref scene,"scene",source);Scribe.saver.FinalizeSaving();
                Equal(2,source.needs.mood.thoughts.memories.Memories.Count);
                var host=SocialPawn();RomanceCompositeHistoryScene loaded=null;Scribe.loader.InitLoading(path);
                var directory=(LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler),"loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);directory.RegisterLoaded(target);
                Scribe_Deep.Look(ref loaded,"scene",host);Scribe.loader.FinalizeLoading();
                var actual=loaded.Handler.Memories.OfType<Thought_MemorySSCRomance>().Single();Equal(host,actual.pawn);Equal(12345,actual.age);Equal(29*60000,actual.DurationTicks-actual.age);
                Equal(next.Data.BranchId,actual.Data.BranchId);Require(!File.ReadAllText(path).Contains("MergeResult"));
            });
            foreach(bool unknown in new[]{false,true}) Check("composite actual CopyNeeds newborn maturation history "+unknown,()=>
            {
                CommandReset();var(source,target)=CommandPair(SSCRomanceAction.Romance);Comp(source).romanceConfig=Denied();
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(source,target),out var command,out _));
                var memory=CompositeMemory(-15,SSCRomanceMoodGroup.Long);memory.age=12345;memory.durationTicksOverride=29*60000+memory.age;memory.Data.Master=SSCBondUtility.GetBoundMaster(source);
                if(unknown)memory.Data.BranchId="Retired";
                if(unknown){memory.pawn=source;source.needs.mood.thoughts.memories.Memories.Add(memory);}
                else source.needs.mood.thoughts.memories.TryGainMemory(memory,SocialPawn());
                // 加入一次新事件会使用完整新期限；此处在获得后再设置历史实例字段。
                memory.age=12345;memory.durationTicksOverride=29*60000+memory.age;
                source.needs.mood.def.needClass=typeof(Need_Mood);source.needs.AllNeeds.Add(source.needs.mood);
                var clone=SocialSlave();AccessTools.Method(typeof(GameComponent_PawnDuplicator),"CopyNeeds").Invoke(null,new object[]{source,clone});
                AccessTools.Method(typeof(RabbitCloneUtility),"PrepareFreshRabbitCloneBody").Invoke(null,new object[]{source,clone});
                var actual=clone.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Require(Comp(clone).romanceConfig==null&&Comp(source).romanceConfig!=null);Equal(clone,actual.pawn);Equal(12345,actual.age);Equal(29*60000,actual.DurationTicks-actual.age);
                Equal(memory.otherPawn,actual.otherPawn);Equal(memory.Data.Master,actual.Data.Master);Require(!ReferenceEquals(memory.Data,actual.Data));
                Equal(SSCRomanceCommandStatus.Pending,command.Status);Require(clone.CurJob==null);Equal(0,playLogCalls);
                Comp(clone).romanceConfig=new(){alwaysAcceptOthers=true};var own=Comp(clone).romanceConfig;
                RabbitCloneUtility.MatureRabbitClone(clone,source,new HediffComp_RabbitCloneLink());
                Equal(own,Comp(clone).romanceConfig);Equal(actual,clone.needs.mood.thoughts.memories.Memories.Single());Equal(12345,actual.age);
                if(unknown)
                {
                    // 真实 CopyNeeds 直接 Add；收尾必须清掉 CopyFrom 临时标记，不能给后续新请求借用。
                    clone.needs.mood.thoughts.memories.RemoveMemory(actual);clone.needs.mood.thoughts.memories.TryGainMemory(actual);
                    Equal(0,clone.needs.mood.thoughts.memories.Memories.Count);
                }
            });
            Check("composite actual clone normalizes composite source without modifying source",()=>
            {
                var source=SocialPawn();source.needs.mood.def.needClass=typeof(Need_Mood);source.needs.AllNeeds.Add(source.needs.mood);
                var other=SocialPawn();var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);old.pawn=source;old.age=321;
                var next=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other,raw:60);next.pawn=source;next.age=789;
                source.needs.mood.thoughts.memories.Memories.AddRange(new[]{old,next});var clone=SocialPawn();
                AccessTools.Method(typeof(GameComponent_PawnDuplicator),"CopyNeeds").Invoke(null,new object[]{source,clone});
                Equal(2,source.needs.mood.thoughts.memories.Memories.Count);Equal(321,old.age);Equal(789,clone.needs.mood.thoughts.memories.Memories.Single().age);
                Equal(next.Data.BranchId,clone.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single().Data.BranchId);
            });
            CompositeFullPersonalityTests(lifecycle);
        }
        finally{lifecycle.UnpatchAll(lifecycle.Id);}
    }

    /// <summary>复用既有完整提取／加工夹具的引擎边界；记忆、配置、命令及植入都执行真实生产代码。</summary>
    private static void CompositeFullPersonalityTests(Harmony boundary)
    {
        boundary.Patch(AccessTools.Method(typeof(ThingMaker),nameof(ThingMaker.MakeThing)),prefix:new HarmonyMethod(typeof(Program),nameof(TransferMakeThing)));
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(GenSpawn)).Where(m=>m.Name=="Spawn"&&m.GetParameters()[0].ParameterType==typeof(Thing)))
            boundary.Patch(method,prefix:new HarmonyMethod(typeof(Program),nameof(TransferSpawn)));
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(GenPlace)).Where(m=>m.Name=="TryPlaceThing"&&m.GetParameters()[0].ParameterType==typeof(Thing)))
            boundary.Patch(method,prefix:new HarmonyMethod(typeof(Program),nameof(TransferPlace)));
        boundary.Patch(AccessTools.Method(typeof(rjw.SexUtility),nameof(rjw.SexUtility.ProcessSex)),prefix:new HarmonyMethod(typeof(Program),nameof(Skip)));
        boundary.Patch(AccessTools.Method(typeof(Thing),"RemoveAllReservationsAndDesignationsOnThis"),prefix:new HarmonyMethod(typeof(Program),nameof(TransferReservationCleanup)));
        var field=AccessTools.Field(typeof(Game),"maps");var maps=field.GetValue(Current.Game);field.SetValue(Current.Game,new List<Map>{Bare<Map>()});
        try
        {
            foreach(bool scene in new[]{false,true}) Check("composite full extraction processed gel insertion preserves history and invalidates commands "+scene,()=>
            {
                CommandReset();transferProduct=null;var source=SocialSlave();PutOnTransferMap(source);Comp(source).romanceConfig=Denied();
                var memory=CompositeMemory(-15,SSCRomanceMoodGroup.Long);source.needs.mood.thoughts.memories.TryGainMemory(memory,SocialPawn());
                memory.age=54321;memory.durationTicksOverride=29*60000+memory.age;memory.Data.Master=SSCBondUtility.GetBoundMaster(source);
                source.jobs=new Pawn_JobTracker(source);Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(source,SocialPawn()),out var command,out _));
                ExtractForTransfer(source,scene);Require(Comp(source).romanceConfig==null);Equal(SSCRomanceCommandStatus.Cancelled,command.Status);
                var raw=transferProduct;new Recipe_PSTagWorker{recipe=new RecipeDef{defName="CompositeTransfer"}}.Notify_IterationCompleted(source,new List<Thing>{raw});
                var data=transferProduct.TryGetComp<CompPersonalityStore>();Require(!ReferenceEquals(data.storedMemories.Single().romanceMood,memory.Data));
                var host=SocialSlave();host.needs.mood.thoughts.memories.TryGainMemory(MoodMemory(6));Comp(host).romanceConfig=new(){alwaysAcceptOthers=true};
                Require(ExcretionUtility.InheritEverything(host,data));Require(!Comp(host).romanceConfig.allowInitiateRomance&&Comp(host).romanceConfig.alwaysAcceptMaster);
                var actual=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();Equal(host,actual.pawn);Equal(54321,actual.age);Equal(29*60000,actual.DurationTicks-actual.age);
                Equal(memory.Data.BranchId,actual.Data.BranchId);Equal(memory.otherPawn,actual.otherPawn);Equal(memory.Data.Master,actual.Data.Master);Equal(0,playLogCalls);
            });
        }
        finally{field.SetValue(Current.Game,maps);}
    }
}

/// <summary>包装实际处理器 ExposeData；让真实 Scribe 三阶段和生产 PostLoadInit 钩子执行。</summary>
public sealed class RomanceCompositeHistoryScene : IExposable
{
    public MemoryThoughtHandler Handler;
    /// <summary>使用该角色真实需求中的处理器，避免把空处理器当作保存来源。</summary>
    public RomanceCompositeHistoryScene(Pawn owner){Handler=owner.needs.mood.thoughts.memories;}
    public void ExposeData()=>Handler.ExposeData();
}
