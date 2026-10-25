// AI 辅助生成：真实 Scribe、原生克隆记忆与实际人格存储；不读取玩家存档。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static LogEntry moodLastLog;
    private static ThoughtDef moodNativeLoss;
    /// <summary>第四批验证历史保留、日志作用域及关系损失去重，和冻结版本分开记录。</summary>
    private static void RomanceMoodLifecycleTests(Harmony harmony,string repo)
    {
        foreach (var field in typeof(RulePackDefOf).GetFields().Where(f => f.FieldType == typeof(RulePackDef)))
            if (field.GetValue(null) is RulePackDef pack && DefDatabase<RulePackDef>.GetNamedSilentFail(pack.defName) == null) DefDatabase<RulePackDef>.Add(pack);
        moodNativeLoss=new ThoughtDef{defName="BrokeUpWithMeMood",thoughtClass=typeof(Thought_Memory),durationDays=25,stackLimit=1,showBubble=false,
            stages=new(){new ThoughtStage{label="",baseMoodEffect=-15}}};DefDatabase<ThoughtDef>.Add(moodNativeLoss);
        foreach(bool addThisEvent in new[]{false,true})
            Check("mood native loss same cause vs preexisting "+addThisEvent,()=>
            {
                CommandReset();var(a,b)=CommandPair(SSCRomanceAction.Breakup,false);
                a.needs.mood.thoughts.memories.TryGainMemory(moodNativeLoss,b);
                var boundary=new Harmony("ssc.tests.mood.compat-loss");
                try
                {
                    if(addThisEvent)boundary.Patch(AccessTools.Method(typeof(InteractionWorker_Breakup),"Interacted"),postfix:new HarmonyMethod(typeof(Program),nameof(MoodCompatibilityLoss)));
                    SubmitAndRun(a,b,SSCRomanceAction.Breakup,out _);
                    Equal(addThisEvent?0:1,a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Count(m=>m.Data.Kind==SSCRomanceMoodEvent.RelationshipLoss));
                    Equal(1,a.needs.mood.thoughts.memories.Memories.Count(m=>m.def==moodNativeLoss));
                }
                finally{boundary.UnpatchAll(boundary.Id);}
            });
        Check("mood confirmed log is one entry with own perspective only",()=>
        {
            CommandReset(0f);var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt);
            Equal(1,playLogCalls);var log=moodLastLog as PlayLogEntry_SSCRomance;Require(log!=null);
            Require(log.BackgroundFromPOV(a)==null && log.BackgroundFromPOV(SocialPawn())==null);
            Equal(SSCRomanceMoodEvent.ForcedRefusal,log.BackgroundFromPOV(b).Kind);
            Equal("",log.BackgroundTextFromPOV(a));Require(log.BackgroundTextFromPOV(b).Contains("文案待定"));
            var m=b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();m.age=4321;
            _=log.BackgroundFromPOV(b);_=log.BackgroundFromPOV(b);Equal(4321,m.age);
        });
        Check("mood real Scribe preserves snapshot age duration object and log without replay",()=>
        {
            CommandReset(0f);var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt);
            var memory=b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();memory.age=9876;
            var scene=new RomanceMoodScene{Memory=memory,Stored=new(){PersonalityMemoryUtility.Capture(memory)},Log=(PlayLogEntry_SSCRomance)moodLastLog};
            string path=Path.Combine(saveDirectory,"mood-"+(++id)+".xml");
            Scribe.saver.InitSaving(path,"Mood");Scribe_Deep.Look(ref scene,"scene");Scribe.saver.FinalizeSaving();
            RomanceMoodScene loaded=null;Scribe.loader.InitLoading(path);
            var directory=(LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler),"loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
            directory.RegisterLoaded(a);directory.RegisterLoaded(b);
            directory.RegisterLoaded(SSCBondUtility.GetBoundMaster(b));
            Scribe_Deep.Look(ref loaded,"scene");Scribe.loader.FinalizeLoading();
            Equal(9876,loaded.Memory.age);Equal(180000,loaded.Memory.DurationTicks);Equal(a,loaded.Memory.otherPawn);
            Equal(-2,loaded.Memory.Data.Value);Equal(3,loaded.Stored.Single().romanceMood.Stage);
            Equal(SSCRomanceMoodEvent.ForcedRefusal,loaded.Log.BackgroundFromPOV(b).Kind);Equal(1,playLogCalls);
            var host=SocialPawn();PersonalityMemoryUtility.Restore(host,loaded.Stored);
            var restored=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();Equal(9876,restored.age);Equal(a,restored.otherPawn);
        });
        Check("mood real gel capture copy insertion retains historical data",()=>
        {
            CommandReset();var source=SocialSlave();var oldObject=SocialPawn();var memory=MoodMemory(-5);memory.age=777;
            source.needs.mood.thoughts.memories.TryGainMemory(memory,oldObject);
            var captured=new CompPersonalityStore();captured.StorePawnData(source);var copied=new CompPersonalityStore();copied.CopyFrom(captured);
            var host=SocialSlave();var boundary=new Harmony("ssc.tests.mood.transfer");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker),nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)),prefix:new HarmonyMethod(typeof(Program),nameof(Skip)));
                Require(ExcretionUtility.InheritEverything(host,copied));
                var restored=host.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(777,restored.age);Equal(-5,restored.Data.Value);Equal(oldObject,restored.otherPawn);
                Require(!ReferenceEquals(memory.Data,restored.Data));Equal(0,playLogCalls);
            }
            finally{boundary.UnpatchAll(boundary.Id);}
        });
        Check("mood native duplicator CopyNeeds plus real newborn reset retains memory but clears config",()=>
        {
            CommandReset();var source=SocialSlave();Comp(source).romanceConfig=Denied();var old=SocialPawn();var memory=MoodMemory(-4);memory.age=111;
            source.needs.mood.thoughts.memories.TryGainMemory(memory,old);source.needs.mood.def.needClass=typeof(Need_Mood);source.needs.AllNeeds.Add(source.needs.mood);
            var clone=SocialSlave();Comp(clone).romanceConfig=Denied();var boundary=new Harmony("ssc.tests.mood.clone");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker),nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)),prefix:new HarmonyMethod(typeof(Program),nameof(Skip)));
                // 运行本机构建的原生 CopyNeeds，验证实际调用虚方法 CopyFrom，而非手工复制字段。
                AccessTools.Method(typeof(GameComponent_PawnDuplicator),"CopyNeeds").Invoke(null,new object[]{source,clone});
                boundary.CreateClassProcessor(production.GetType("SexSlaveCraft.SSCRestrictionCloneHook")).Patch();
                AccessTools.Method(typeof(RabbitCloneUtility),"PrepareFreshRabbitCloneBody").Invoke(null,new object[]{source,clone});
                var actual=clone.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(111,actual.age);Equal(-4,actual.Data.Value);Equal(old,actual.otherPawn);Require(!ReferenceEquals(memory.Data,actual.Data));
                Require(Comp(clone).romanceConfig==null && Comp(source).romanceConfig!=null);Equal(0,playLogCalls);
            }
            finally{boundary.UnpatchAll(boundary.Id);}
        });
        Check("mood downgrade unbind and query preserve historical memory",()=>
        {
            var p=SocialSlave();var memory=MoodMemory(-4);p.needs.mood.thoughts.memories.TryGainMemory(memory);memory.age=321;
            Comp(p).romanceConfig=new(){allowInitiateRomance=false};Require(ThoughtWorker_SSCRomanceControl.TryStage(p,out _));
            SSCBondUtility.GetChain(p).Severity=.1f;Require(!ThoughtWorker_SSCRomanceControl.TryStage(p,out _));
            SSCBondUtility.Unbind(p);SSCRomanceMoodRefresh.Notify(p);Equal(321,memory.age);Equal(-4,memory.Data.Value);
        });
        Check("mood XML numeric stages match native fixture and authorized placeholder passes config errors",()=>
        {
            var xml=System.Xml.Linq.XDocument.Load(Path.Combine(repo,"1.7/Defs/ThoughtDefs/SSC_RomanceMood.xml"));
            foreach(var def in xml.Root.Elements())
            {
                var native=DefDatabase<ThoughtDef>.GetNamed(def.Element("defName").Value);
                Require(!native.ConfigErrors().Any(),string.Join(";",native.ConfigErrors()));
                var stages=def.Element("stages").Elements().ToList();Equal(native.stages.Count,stages.Count);
                for(int i=0;i<stages.Count;i++){Equal(native.stages[i].baseMoodEffect,float.Parse(stages[i].Element("baseMoodEffect").Value));Require(stages[i].Element("label").Value.Contains("文案待定"));Require(stages[i].Element("description").Value.Contains("文案待定"));}
            }
        });
        Check("mood effective Masochist respects Biotech suppression",()=>
        {
            var p=SocialSlave();Comp(p).romanceConfig=new(){allowInitiateRomance=false};
            var trait=new Trait(SSCRomanceMoodDefOf.Masochist,0);p.story.traits.allTraits.Add(trait);
            Require(ThoughtWorker_SSCRomanceControl.TryStage(p,out int stage));Equal(7,stage);
            var boundary=new Harmony("ssc.tests.mood.biotech");
            try
            {
                boundary.Patch(AccessTools.PropertyGetter(typeof(ModsConfig),nameof(ModsConfig.BiotechActive)),prefix:new HarmonyMethod(typeof(Program),nameof(MoodBiotech)));
                AccessTools.Field(typeof(Trait),"suppressedByTrait").SetValue(trait,true);
                Require(ThoughtWorker_SSCRomanceControl.TryStage(p,out stage));Equal(5,stage);
            }
            finally{boundary.UnpatchAll(boundary.Id);}
        });
        Check("mood original worker snapshot uses current corruption and effective trait",()=>
        {
            CommandReset(0f);socialOpinionValue=40;var(a,b)=CommandPair(SSCRomanceAction.Romance,false);
            var need=new Need_Corruption(a){def=new NeedDef{defName="MoodFixtureCorruption"}};need.CurLevel=1;a.needs.AllNeeds.Add(need);
            a.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist,0));
            SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
            var memory=a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Equal(6,memory.Data.Value);Equal(1f,memory.Data.Corruption.Value);Require(memory.Data.Masochist);
            need.CurLevel=0;a.story.traits.allTraits.Clear();SSCRomanceMoodRefresh.Notify(a);Equal(6,memory.Data.Value);
        });
    }
    /// <summary>仅打开本机构建的 Biotech 特质抑制分支，实际 HasTrait／Suppressed 均运行原版实现。</summary>
    private static bool MoodBiotech(ref bool __result){__result=true;return false;}
    /// <summary>只记录真实 tracker 生成的条目，不追加或伪造日志。</summary>
    private static void MoodCaptureLog(LogEntry entry)=>moodLastLog=entry;
    /// <summary>模拟明确的兼容流程为同次分手发起者结算原版损失，实际准入与合并仍由游戏处理。</summary>
    private static void MoodCompatibilityLoss(Pawn initiator,Pawn recipient)=>initiator.needs.mood.thoughts.memories.TryGainMemory(moodNativeLoss,recipient);
}

public sealed class RomanceMoodScene:IExposable
{
    public Thought_MemorySSCRomance Memory;
    public List<StoredMemoryData> Stored;
    public PlayLogEntry_SSCRomance Log;
    /// <summary>同时往返运行记忆、人格载荷和日志；不包含任务、授权或结算请求。</summary>
    public void ExposeData(){Scribe_Deep.Look(ref Memory,"memory");Scribe_Collections.Look(ref Stored,"stored",LookMode.Deep);Scribe_Deep.Look(ref Log,"log");}
}
