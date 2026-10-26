// AI 辅助生成：第一批提交前复核；只验证保存／显示／构造边界，不启用实际综合事件。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action compositeReviewBeforeFactory;
    private static Action<Thought> compositeReviewAfterFactory;

    /// <summary>真实工厂回调、历史恢复及文案兜底；补足首批正常路径之外的保存兼容边界。</summary>
    private static void RomanceCompositeReviewTests()
    {
        Check("composite review freezes context before factory callback",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
            var master=SocialPawn();var context=new SSCRomanceMoodData{Relation=SSCRomanceOldRelation.Spouse,Master=master,
                HasRule=true,Rule=SSCRomanceSetting.AlwaysAcceptMaster};
            var boundary=CompositeFactoryBoundary();
            try
            {
                compositeReviewBeforeFactory=()=>{context.Relation=SSCRomanceOldRelation.None;context.Master=null;context.HasRule=false;};
                Require(Thought_MemorySSCCompositeRomance.TryCreate(selection,context,out var memory));
                Require(compositeReviewBeforeFactory==null);Equal(SSCRomanceOldRelation.Spouse,memory.Data.Relation);
                Equal(master,memory.Data.Master);Require(memory.Data.HasRule);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeReviewBeforeFactory=null;}
        });
        foreach(string change in new[]{"definition","stage","name","class","stages","null-stage","value"})
        {
            string mode=change;
            Check("composite review validates factory result after callback " + mode,()=>
            {
                SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandRomanceAccepted,4,false,60,out var selection);
                var carrier=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
                var stages=carrier.stages;var stage=stages[25];float value=stage.baseMoodEffect;string name=carrier.defName;var type=carrier.thoughtClass;
                var boundary=CompositeFactoryBoundary();
                try
                {
                    compositeReviewAfterFactory=thought=>
                    {
                        if(mode=="definition")thought.def=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.LongDefName);
                        else if(mode=="stage")((Thought_Memory)thought).SetForcedStage(0);
                        else if(mode=="name")carrier.defName="ChangedCarrier";
                        else if(mode=="class")carrier.thoughtClass=typeof(Thought_Memory);
                        else if(mode=="stages")carrier.stages=null;
                        else if(mode=="null-stage")carrier.stages[25]=null;
                        else stage.baseMoodEffect=99;
                    };
                    Require(!Thought_MemorySSCCompositeRomance.TryCreate(selection,out var memory));Require(memory==null);
                    Require(compositeReviewAfterFactory==null);
                }
                finally
                {
                    boundary.UnpatchAll(boundary.Id);compositeReviewAfterFactory=null;
                    carrier.stages=stages;stages[25]=stage;stage.baseMoodEffect=value;carrier.defName=name;carrier.thoughtClass=type;
                }
            });
        }
        Check("composite review unknown text branch preserves frozen numeric base",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
            Thought_MemorySSCCompositeRomance.TryCreate(selection,out var memory);memory.pawn=SocialPawn();
            memory.Data.Value=-14;memory.Data.BranchId="RetiredBranch";
            Equal(-14f,memory.MoodOffset());Equal(SSCRomanceMoodText.Name(null),memory.LabelCap);
            Require(!SSCRomanceMoodCatalog.TryReadSnapshot(memory.Data,out _));
        });
        foreach(string kind in new[]{"branch","version","raw","group","payload"})
        {
            string mode=kind;
            Check("composite review unknown historical payload survives restore and clone " + mode,()=>
            {
                SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
                Thought_MemorySSCCompositeRomance.TryCreate(selection,out var original);original.pawn=SocialPawn();original.otherPawn=SocialPawn();
                original.age=12345;original.durationTicksOverride=29*60000+original.age;
                if(mode=="branch")original.Data.BranchId="RetiredBranch";
                else if(mode=="version")original.Data.DataVersion=99;
                else if(mode=="raw")original.Data.HasRawOpinion=false;
                else if(mode=="group"){original.Data.Group=SSCRomanceMoodGroup.None;original.Data.Value=-14;}
                else original.Data=null;
                var host=SocialPawn();var saved=PersonalityMemoryUtility.Capture(original);
                PersonalityMemoryUtility.Restore(host,new(){saved});
                var restored=(Thought_MemorySSCCompositeRomance)host.needs.mood.thoughts.memories.Memories.Single();
                Equal(original.age,restored.age);Equal(original.durationTicksOverride,restored.durationTicksOverride);
                Equal(original.otherPawn,restored.otherPawn);Equal(host,restored.pawn);Equal(original.CurStageIndex,restored.CurStageIndex);
                Equal(original.Data?.BranchId,restored.Data?.BranchId);Equal(original.Data?.DataVersion,restored.Data?.DataVersion);
                if(mode=="group")Equal(-14f,restored.MoodOffset());
                Equal(SSCRomanceMoodText.Name(null),restored.LabelCap);
                // 全新无效请求仍拒绝，不能借历史兼容路径伪装成合法新分支。
                Thought_MemorySSCCompositeRomance.TryCreate(selection,out var rejected);rejected.Data=original.Data?.Copy();
                var rejectedHost=SocialPawn();rejectedHost.needs.mood.thoughts.memories.TryGainMemory(rejected,original.otherPawn);
                Equal(0,rejectedHost.needs.mood.thoughts.memories.Memories.Count);
                var clone=(Thought_MemorySSCCompositeRomance)ThoughtMaker.MakeThought(original.def,original.CurStageIndex);
                clone.CopyFrom(original);var clonedHost=SocialPawn();clonedHost.needs.mood.thoughts.memories.TryGainMemory(clone,original.otherPawn);
                Equal(1,clonedHost.needs.mood.thoughts.memories.Memories.Count);Equal(12345,clone.age);
                // CopyFrom 的历史领取标记只消费一次，既不存档也不变成后续新请求的通行证。
                clonedHost.needs.mood.thoughts.memories.RemoveMemory(clone);
                clonedHost.needs.mood.thoughts.memories.TryGainMemory(clone,original.otherPawn);Equal(0,clonedHost.needs.mood.thoughts.memories.Memories.Count);
            });
        }
        Check("composite review empty translations use nonempty placeholders",()=>
        {
            var language=LanguageDatabase.activeLanguage;var before=language.keyedReplacements;
            SSCRomanceMoodCatalog.TryGet("E06-S4-M-C",out var branch);
            try
            {
                language.keyedReplacements=new(before);
                void Text(string key,string value)=>language.keyedReplacements[key]=new LoadedLanguage.KeyedReplacement{key=key,value=value};
                Text(branch.NameKey,"");Text(branch.DescriptionKey,"  ");
                Text("SSC_RomanceMood_Unknown_Name","Unknown history");Text("SSC_RomanceMood_Unknown_Description","Unknown details");
                Equal("Unknown history",SSCRomanceMoodText.Name(branch));Equal("Unknown details",SSCRomanceMoodText.Description(branch));
                Text("SSC_RomanceMood_Unknown_Name","");Text("SSC_RomanceMood_Unknown_Description","  ");
                Require(!string.IsNullOrWhiteSpace(SSCRomanceMoodText.Name(branch)));Require(!string.IsNullOrWhiteSpace(SSCRomanceMoodText.Description(branch)));
            }
            finally{language.keyedReplacements=before;}
        });
        Check("composite review historical copy marker is not serialized",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
            Thought_MemorySSCCompositeRomance.TryCreate(selection,out var source);source.Data.BranchId="RetiredBranch";
            var clone=(Thought_MemorySSCCompositeRomance)ThoughtMaker.MakeThought(source.def,source.CurStageIndex);clone.CopyFrom(source);
            var scene=new RomanceMoodScene{Memory=clone};string path=Path.Combine(saveDirectory,"composite-review-"+(++id)+".xml");
            Scribe.saver.InitSaving(path,"CompositeReview");Scribe_Deep.Look(ref scene,"scene");Scribe.saver.FinalizeSaving();
            Require(!File.ReadAllText(path).Contains("historicalRestore"));
            RomanceMoodScene loaded=null;Scribe.loader.InitLoading(path);Scribe_Deep.Look(ref loaded,"scene");Scribe.loader.FinalizeLoading();
            Equal("RetiredBranch",loaded.Memory.Data.BranchId);
            var host=SocialPawn();host.needs.mood.thoughts.memories.TryGainMemory(loaded.Memory);Equal(0,host.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("composite review new factory does not borrow callback historical marker",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandRomanceAccepted,4,false,60,out var selection);
            Thought_MemorySSCCompositeRomance.TryCreate(selection,out var history);history.Data.BranchId="RetiredBranch";
            var boundary=CompositeFactoryBoundary();
            try
            {
                compositeReviewAfterFactory=thought=>((Thought_MemorySSCCompositeRomance)thought).CopyFrom(history);
                Require(Thought_MemorySSCCompositeRomance.TryCreate(selection,out var created));Equal(selection.Branch.Id,created.Data.BranchId);
                created.Data.BranchId="UnknownNewRequest";
                var host=SocialPawn();host.needs.mood.thoughts.memories.TryGainMemory(created);Equal(0,host.needs.mood.thoughts.memories.Memories.Count);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeReviewAfterFactory=null;}
        });
    }

    /// <summary>只包围新载体构造；实际 ThoughtMaker／Init 继续执行，不改旧事件工厂。</summary>
    private static Harmony CompositeFactoryBoundary()
    {
        var boundary=new Harmony("ssc.tests.composite.review-factory");
        boundary.Patch(AccessTools.Method(typeof(ThoughtMaker),nameof(ThoughtMaker.MakeThought),new[]{typeof(ThoughtDef),typeof(int)}),
            prefix:new HarmonyMethod(typeof(Program),nameof(CompositeReviewFactoryPrefix)),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeReviewFactoryPostfix)));
        return boundary;
    }

    /// <summary>模拟兼容回调改变调用方快照；每次只消费一个操作。</summary>
    private static void CompositeReviewFactoryPrefix(ThoughtDef __0)
    {
        if(__0.defName!=Thought_MemorySSCCompositeRomance.ShortDefName && __0.defName!=Thought_MemorySSCCompositeRomance.LongDefName)return;
        var action=compositeReviewBeforeFactory;compositeReviewBeforeFactory=null;action?.Invoke();
    }

    /// <summary>模拟 Init 或工厂后置补丁改变实际构造结果。</summary>
    private static void CompositeReviewFactoryPostfix(Thought __result)
    {
        if(__result is not Thought_MemorySSCCompositeRomance)return;
        var action=compositeReviewAfterFactory;compositeReviewAfterFactory=null;action?.Invoke(__result);
    }
}
