// AI 辅助生成：对象与五类语义组的生产入口专项；数值及文案保持目录占位。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>显式验收十三类事件映射、关系阶段清理、零值及保存顺序，不重写生产清理算法。</summary>
    private static void RomanceSemanticGroupTests()
    {
        var expected=new[]{SSCRomanceSemanticGroup.PassiveRomance,SSCRomanceSemanticGroup.PassiveProposal,
            SSCRomanceSemanticGroup.PassiveRomance,SSCRomanceSemanticGroup.PassiveProposal,SSCRomanceSemanticGroup.Separation,
            SSCRomanceSemanticGroup.CommandRomance,SSCRomanceSemanticGroup.CommandRomance,SSCRomanceSemanticGroup.CommandProposal,
            SSCRomanceSemanticGroup.CommandProposal,SSCRomanceSemanticGroup.Separation,SSCRomanceSemanticGroup.Separation,
            SSCRomanceSemanticGroup.Separation,SSCRomanceSemanticGroup.Separation};
        for(int index=0;index<expected.Length;index++)
        {
            int number=index+1;var group=expected[index];
            Check("semantic thirteen events map exactly once "+number,()=>Equal(group,SSCRomanceMoodSemantics.Of((SSCRomanceCompositeEvent)number)));
        }
        foreach(var branch in SSCRomanceMoodCatalog.EventBranches.Where(b=>b.Value==0))
            Check("semantic zero retains event group "+branch.Id,()=>
            {
                var selection=BranchSelection(branch);Require(!selection.HasMemory);
                Equal(expected[(int)branch.Event-1],selection.SemanticGroup);Require(selection.SemanticGroup!=SSCRomanceSemanticGroup.None);
            });
        Check("semantic game occurrence watermark survives actual Scribe",()=>
        {
            var components=(List<GameComponent>)AccessTools.Field(typeof(Game),"components").GetValue(Current.Game);
            var previous=components.OfType<SSCRomanceMoodOrderGameComponent>().ToArray();
            components.RemoveAll(c=>c is SSCRomanceMoodOrderGameComponent);
            var saved=new SSCRomanceMoodOrderGameComponent(Current.Game);components.Add(saved);
            try
            {
                long first=SSCRomanceMoodOrder.Next(),second=SSCRomanceMoodOrder.Next();Equal(first+1,second);
                string path=Path.Combine(saveDirectory,"semantic-order-"+(++id)+".xml");
                Scribe.saver.InitSaving(path,"SemanticOrder");Scribe_Deep.Look(ref saved,"order",Current.Game);Scribe.saver.FinalizeSaving();
                components.Remove(saved);SSCRomanceMoodOrderGameComponent loaded=null;
                Scribe.loader.InitLoading(path);Scribe_Deep.Look(ref loaded,"order",Current.Game);Scribe.loader.FinalizeLoading();components.Add(loaded);
                Equal(second+1,SSCRomanceMoodOrder.Next());
                SSCRomanceMoodOrder.Observe(second+100);Equal(second+101,SSCRomanceMoodOrder.Next());
            }
            finally{components.RemoveAll(c=>c is SSCRomanceMoodOrderGameComponent);components.AddRange(previous);}
        });
        foreach(var phase in new[]{SSCRomanceMoodPhase.None,SSCRomanceMoodPhase.RomanceEstablished,SSCRomanceMoodPhase.ProposalRejected,SSCRomanceMoodPhase.Engaged,SSCRomanceMoodPhase.Separated})
            Check("semantic confirmed phase targeted cleanup "+phase,()=>
            {
                var owner=SocialPawn();var other=SocialPawn();var unrelated=SocialPawn();var h=owner.needs.mood.thoughts.memories;
                var original=SeedFiveSemanticGroups(owner,other);
                var untouched=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,unrelated,raw:60);h.TryGainMemory(untouched,unrelated);untouched.age=9876;
                h.TryGainMemory(moodNativeLoss,other);var native=h.Memories.Single(m=>m.def==moodNativeLoss);native.age=7654;
                var kind=phase==SSCRomanceMoodPhase.RomanceEstablished?SSCRomanceCompositeEvent.CommandRomanceAccepted:
                    phase==SSCRomanceMoodPhase.Engaged?SSCRomanceCompositeEvent.CommandProposalAccepted:
                    phase==SSCRomanceMoodPhase.ProposalRejected?SSCRomanceCompositeEvent.CommandProposalRejected:
                    phase==SSCRomanceMoodPhase.Separated?SSCRomanceCompositeEvent.CommandEndLover:SSCRomanceCompositeEvent.CommandRomanceRejected;
                var selection=SemanticSelection(kind,raw:1);
                var result=new SSCRomanceMoodRequest(owner,other,selection,phase:phase).Execute();Require(result.Completed&&result.Applied);
                var same=h.Memories.OfType<Thought_MemorySSCRomance>().Where(m=>m.otherPawn==other).ToList();
                Equal(phase==SSCRomanceMoodPhase.None?5:phase==SSCRomanceMoodPhase.ProposalRejected?3:1,same.Count);
                Equal(1,same.Count(m=>m.Data.BranchId==selection.Branch.Id));Equal(9876,untouched.age);Require(h.Memories.Contains(untouched));
                Equal(7654,native.age);Require(h.Memories.Contains(native));
                if(phase==SSCRomanceMoodPhase.ProposalRejected)
                {
                    Require(same.Any(m=>m.Data.CompositeEvent==SSCRomanceCompositeEvent.ForcedProposalRejected));
                    Require(same.Any(m=>m.Data.CompositeEvent==SSCRomanceCompositeEvent.CommandEndLover));
                    Require(!same.Any(m=>m.Data.CompositeEvent==SSCRomanceCompositeEvent.ForcedRomanceRejected));
                }
            });
        foreach(var kind in new[]{SSCRomanceCompositeEvent.CommandRomanceRejected,SSCRomanceCompositeEvent.ForcedProposalRejected,SSCRomanceCompositeEvent.ForcedProposalAccepted})
            Check("semantic zero performs valid phase cleanup "+kind,()=>
            {
                var owner=SocialPawn();var other=SocialPawn();var original=SeedFiveSemanticGroups(owner,other);var h=owner.needs.mood.thoughts.memories;
                int stage=kind==SSCRomanceCompositeEvent.ForcedProposalAccepted?3:4;
                bool masochist=kind==SSCRomanceCompositeEvent.ForcedProposalAccepted;
                int raw=kind==SSCRomanceCompositeEvent.CommandRomanceRejected?-10:0;
                var phase=kind==SSCRomanceCompositeEvent.ForcedProposalAccepted?SSCRomanceMoodPhase.Engaged:
                    kind==SSCRomanceCompositeEvent.ForcedProposalRejected?SSCRomanceMoodPhase.ProposalRejected:SSCRomanceMoodPhase.None;
                var selection=SemanticSelection(kind,stage,masochist,raw);Equal(0,selection.Branch.Value);
                var result=new SSCRomanceMoodRequest(owner,other,selection,phase:phase).Execute();
                Require(result.Completed&&!result.Applied);Equal(SSCRomanceMoodResultKind.Zero,result.Kind);
                Equal(phase==SSCRomanceMoodPhase.Engaged?0:phase==SSCRomanceMoodPhase.ProposalRejected?2:4,h.Memories.Count);
                Require(h.Memories.OfType<Thought_MemorySSCRomance>().All(m=>m.Data.Value!=0));
            });
        foreach(var phase in new[]{SSCRomanceMoodPhase.RomanceEstablished,SSCRomanceMoodPhase.ProposalRejected,SSCRomanceMoodPhase.Engaged,SSCRomanceMoodPhase.Separated})
            Check("semantic denied new mood cannot delete phase history "+phase,()=>
            {
                var owner=SocialPawn();var other=SocialPawn();var original=SeedFiveSemanticGroups(owner,other);var h=owner.needs.mood.thoughts.memories;
                var boundary=new Harmony("ssc.tests.semantic.phase-denied");
                try
                {
                    boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),prefix:new HarmonyMethod(typeof(Program),nameof(CompositeDenyAdmission)));
                    var selection=SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted);
                    var result=new SSCRomanceMoodRequest(owner,other,selection,phase:phase).Execute();Require(!result.Completed);
                    Equal(5,h.Memories.Count);Require(original.All(h.Memories.Contains));
                }
                finally{boundary.UnpatchAll(boundary.Id);}
            });
        Check("semantic pending admission is excluded from personality and native clone history",()=>
        {
            var owner=SocialPawn();var other=SocialPawn();var h=owner.needs.mood.thoughts.memories;
            owner.needs.mood.def.needClass=typeof(Need_Mood);owner.needs.AllNeeds.Add(owner.needs.mood);
            var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);h.TryGainMemory(old,other);old.age=9876;
            var clone=SocialPawn();bool observed=false;var boundary=new Harmony("ssc.tests.semantic.pending-copy");
            try
            {
                boundary.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodCopiedHistory")).Patch();
                compositeSubmitCallback=memory=>
                {
                    observed=true;Require(h.Memories.Contains(old)&&h.Memories.Contains(memory));
                    Require(PersonalityMemoryUtility.Capture(memory)==null);Require(PersonalityMemoryUtility.Capture(old)!=null);
                    AccessTools.Method(typeof(GameComponent_PawnDuplicator),"CopyNeeds").Invoke(null,new object[]{owner,clone});
                    var copied=(Thought_MemorySSCRomance)clone.needs.mood.thoughts.memories.Memories.Single();
                    Equal(old.Data.BranchId,copied.Data.BranchId);Equal(old.Data.OccurrenceOrder,copied.Data.OccurrenceOrder);Equal(9876,copied.age);
                };
                boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)}),
                    postfix:new HarmonyMethod(typeof(Program),nameof(CompositeSubmitAfter)));
                Require(new SSCRomanceMoodRequest(owner,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceRejected,raw:60)).Execute().Applied);
                Require(observed);Equal(SSCRomanceCompositeEvent.CommandRomanceRejected,((Thought_MemorySSCRomance)h.Memories.Single()).Data.CompositeEvent);
                Equal(SSCRomanceCompositeEvent.CommandRomanceAccepted,((Thought_MemorySSCRomance)clone.needs.mood.thoughts.memories.Memories.Single()).Data.CompositeEvent);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeSubmitCallback=null;}
        });
        foreach(bool succeeds in new[]{false,true})Check("semantic pending Scribe restores only committed predecessor "+succeeds,()=>
        {
            var owner=SocialPawn();var other=SocialPawn();var h=owner.needs.mood.thoughts.memories;
            var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);h.TryGainMemory(old,other);old.age=9876;
            var loadedOwner=SocialPawn();RomanceCompositeHistoryScene loaded=null;bool observed=false;
            var boundary=new Harmony("ssc.tests.semantic.pending-save");
            try
            {
                boundary.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodLoadedHistory")).Patch();
                compositeSubmitCallback=memory=>
                {
                    observed=true;Require(h.Memories.Contains(old)&&h.Memories.Contains(memory));
                    var scene=new RomanceCompositeHistoryScene(owner);string path=Path.Combine(saveDirectory,"semantic-pending-"+(++id)+".xml");
                    Scribe.saver.InitSaving(path,"PendingHistory");Scribe_Deep.Look(ref scene,"scene",owner);Scribe.saver.FinalizeSaving();
                    Require(File.ReadAllText(path).Contains("sscRomancePendingAtSave"));
                    Scribe.loader.InitLoading(path);
                    var directory=(LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler),"loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);directory.RegisterLoaded(other);
                    Scribe_Deep.Look(ref loaded,"scene",loadedOwner);Scribe.loader.FinalizeLoading();
                    var restored=(Thought_MemorySSCRomance)loaded.Handler.Memories.Single();
                    Equal(old.Data.BranchId,restored.Data.BranchId);Equal(old.Data.OccurrenceOrder,restored.Data.OccurrenceOrder);Equal(9876,restored.age);
                    if(!succeeds)throw new InvalidOperationException("outer event fails after forced pending save");
                };
                boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)}),
                    postfix:new HarmonyMethod(typeof(Program),nameof(CompositeSubmitAfter)));
                var result=new SSCRomanceMoodRequest(owner,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceRejected,raw:60)).Execute();
                Equal(succeeds,result.Applied);Require(observed);Equal(1,loaded.Handler.Memories.Count);
                Equal(succeeds?SSCRomanceCompositeEvent.CommandRomanceRejected:SSCRomanceCompositeEvent.CommandRomanceAccepted,((Thought_MemorySSCRomance)h.Memories.Single()).Data.CompositeEvent);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeSubmitCallback=null;}
        });
        Check("semantic nested zero leaves watermark and prevents older outer write",()=>
        {
            var owner=SocialPawn();var other=SocialPawn();var h=owner.needs.mood.thoughts.memories;
            var old=SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceRejected,other,raw:60);h.TryGainMemory(old,other);
            var boundary=CompositeB2Boundary();bool zeroCompleted=false;
            try
            {
                compositeB2Before=_=>
                {
                    var zero=new SSCRomanceMoodRequest(owner,other,SemanticSelection(SSCRomanceCompositeEvent.ForcedRomanceRejected,stage:3,raw:-10)).Execute();
                    Equal(SSCRomanceMoodResultKind.Zero,zero.Kind);Require(zero.Completed);zeroCompleted=true;Equal(0,h.Memories.Count);
                };
                var result=new SSCRomanceMoodRequest(owner,other,SemanticSelection(SSCRomanceCompositeEvent.ForcedRomanceAccepted)).Execute();
                Require(!result.Completed);Require(zeroCompleted);Equal(0,h.Memories.Count);
            }
            finally{CompositeB2Clear(boundary);}
        });
        Check("semantic nested independent counterpart remains compatible with outer result",()=>
        {
            var owner=SocialPawn();var other=SocialPawn();var third=SocialPawn();var h=owner.needs.mood.thoughts.memories;
            var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other,raw:60);h.TryGainMemory(old,other);
            var boundary=CompositeB2Boundary();Thought_MemorySSCRomance inner=null;
            try
            {
                compositeB2Before=_=>
                {
                    Require(new SSCRomanceMoodRequest(owner,third,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted,raw:60)).Execute().Applied);
                    inner=h.Memories.OfType<Thought_MemorySSCRomance>().Single(m=>m.otherPawn==third);inner.age=345;
                };
                Require(new SSCRomanceMoodRequest(owner,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted)).Execute().Applied);
                Equal(2,h.Memories.Count);Require(h.Memories.Contains(inner));Equal(345,inner.age);
                Equal(SSCRomanceCompositeEvent.CommandRomanceAccepted,h.Memories.OfType<Thought_MemorySSCRomance>().Single(m=>m.otherPawn==other).Data.CompositeEvent);
            }
            finally{CompositeB2Clear(boundary);}
        });
        foreach(bool reverse in new[]{false,true})Check("semantic expired latest short history cannot revive older long loss "+reverse,()=>
        {
            var owner=SocialPawn();var other=SocialPawn();
            var old=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,other,raw:1);old.age=60000;
            var latest=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,other,raw:0);latest.age=latest.DurationTicks+1;
            var history=new List<StoredMemoryData>{PersonalityMemoryUtility.Capture(old),PersonalityMemoryUtility.Capture(latest)};if(reverse)history.Reverse();
            PersonalityMemoryUtility.Restore(owner,history);Equal(0,owner.needs.mood.thoughts.memories.Memories.Count);
        });
        foreach(bool reverse in new[]{false,true})Check("semantic history does not replay phase cleanup "+reverse,()=>
        {
            var owner=SocialPawn();var other=SocialPawn();
            var earlier=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,other,raw:60);earlier.age=4321;
            var later=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceAccepted,other);later.age=1234;
            var source=new List<StoredMemoryData>{PersonalityMemoryUtility.Capture(earlier),PersonalityMemoryUtility.Capture(later)};if(reverse)source.Reverse();
            PersonalityMemoryUtility.Restore(owner,source);Equal(2,owner.needs.mood.thoughts.memories.Memories.Count);
            Equal(4321,owner.needs.mood.thoughts.memories.Memories.Single(m=>(m as Thought_MemorySSCRomance)?.Data.CompositeEvent==SSCRomanceCompositeEvent.CommandProposalRejected).age);
        });
        Check("semantic missing target histories remain separate evidence",()=>
        {
            var owner=SocialPawn();var a=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,null,raw:1);
            var b=SemanticMemory(SSCRomanceCompositeEvent.CommandProposalRejected,null,raw:60);
            Require(!a.GroupsWith(b)&&!b.GroupsWith(a));
            PersonalityMemoryUtility.Restore(owner,new(){PersonalityMemoryUtility.Capture(a),PersonalityMemoryUtility.Capture(b)});
            Equal(2,owner.needs.mood.thoughts.memories.Memories.Count);Require(a.Data.OccurrenceOrder!=b.Data.OccurrenceOrder);
        });
        Check("semantic restored event order advances next real occurrence",()=>
        {
            var owner=SocialPawn();var other=SocialPawn();var old=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,other,raw:60);
            old.Data.OccurrenceOrder=SSCRomanceMoodOrder.Next()+10000;
            PersonalityMemoryUtility.Restore(owner,new(){PersonalityMemoryUtility.Capture(old)});
            var result=new SSCRomanceMoodRequest(owner,other,SemanticSelection(SSCRomanceCompositeEvent.CommandRomanceAccepted)).Execute();Require(result.Applied);
            var actual=(Thought_MemorySSCRomance)owner.needs.mood.thoughts.memories.Memories.Single();Require(actual.Data.OccurrenceOrder>old.Data.OccurrenceOrder);
        });
    }

    /// <summary>真实 tracker 与 worker 的关系推进；自然清理没有新的 SSC 记忆或原版接管。</summary>
    private static void RomanceSemanticInteractionTests()
    {
        foreach(var action in new[]{SSCRomanceAction.Romance,SSCRomanceAction.Proposal,SSCRomanceAction.Breakup})
        foreach(bool accepted in action==SSCRomanceAction.Breakup?new[]{false}:new[]{false,true})
            Check($"semantic natural interaction clears confirmed phase {action}/{accepted}",()=>
            {
                CommandReset(accepted?0f:1f,1f);socialOpinionValue=30;
                var actor=SocialPawn();var target=SocialSlave();Comp(target).romanceConfig=new(){allowAcceptRomance=true,allowAcceptProposal=true};
                if(action!=SSCRomanceAction.Romance)actor.relations.AddDirectRelation(PawnRelationDefOf.Lover,target);
                var actorOld=SeedFiveSemanticGroups(actor,target);var targetOld=SeedFiveSemanticGroups(target,actor);
                var unrelated=SocialPawn();var untouched=SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected,unrelated,raw:60);
                target.needs.mood.thoughts.memories.TryGainMemory(untouched,unrelated);untouched.age=1111;
                Require(actor.interactions.TryInteractWith(target,SocialDef(action)));
                int expected=action==SSCRomanceAction.Romance&&!accepted?5:action==SSCRomanceAction.Proposal&&!accepted?3:0;
                foreach(var pair in new[]{(actor,target),(target,actor)})
                {
                    var list=pair.Item1.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Where(m=>m.otherPawn==pair.Item2).ToList();
                    Equal(expected,list.Count);Require(list.All(m=>(pair.Item1==actor?actorOld:targetOld).Contains(m)));
                }
                Require(target.needs.mood.thoughts.memories.Memories.Contains(untouched));Equal(1111,untouched.age);
                if(!accepted&&action!=SSCRomanceAction.Breakup)
                    Require(actor.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName==(action==SSCRomanceAction.Romance?"RebuffedMyRomanceAttemptMood":"RejectedMyProposalMood")));
            });
        Check("semantic controlled admission failure does not fall back to natural phase cleanup",()=>
        {
            CommandReset(0f);socialOpinionValue=30;var actor=SocialPawn();var target=SocialSlave();
            Comp(target).romanceConfig=new(){alwaysAcceptOthers=true};
            var actorOld=SeedFiveSemanticGroups(actor,target);var targetOld=SeedFiveSemanticGroups(target,actor);
            var boundary=new Harmony("ssc.tests.semantic.controlled-phase-denied");
            try
            {
                eventDeniedDef=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
                boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),prefix:new HarmonyMethod(typeof(Program),nameof(EventDenyThought)));
                Require(actor.interactions.TryInteractWith(target,InteractionDefOf.RomanceAttempt));
                Require(actor.relations.DirectRelationExists(PawnRelationDefOf.Lover,target));
                Require(targetOld.All(target.needs.mood.thoughts.memories.Memories.Contains));
                Require(!actorOld.Any(actor.needs.mood.thoughts.memories.Memories.Contains));
            }
            finally{boundary.UnpatchAll(boundary.Id);eventDeniedDef=null;}
        });
        Check("semantic actual zero engagement clears old five groups after complete transition",()=>
        {
            CommandReset(0f);socialOpinionValue=0;var actor=SocialPawn();var target=SocialSlave(severity:.5f);
            target.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist,0));Comp(target).romanceConfig=new(){alwaysAcceptOthers=true};
            actor.relations.AddDirectRelation(PawnRelationDefOf.Lover,target);SeedFiveSemanticGroups(target,actor);
            Require(actor.interactions.TryInteractWith(target,InteractionDefOf.MarriageProposal));
            Require(actor.relations.DirectRelationExists(PawnRelationDefOf.Fiance,target));
            Equal(0,target.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Count());
        });
        Check("semantic actual relation sequence retains other counterpart and ends obsolete phases",()=>
        {
            CommandReset(1f);socialOpinionValue=60;var(actor,target)=CommandPair(SSCRomanceAction.Romance);var carr=SocialPawn();
            var carrHistory=SemanticMemory(SSCRomanceCompositeEvent.ForcedRomanceRejected,carr,raw:-10);
            actor.needs.mood.thoughts.memories.TryGainMemory(carrHistory,carr);carrHistory.age=4321;
            SubmitAndRun(actor,target,SSCRomanceAction.Romance,out _);
            Equal(2,actor.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Count());
            CommandReset(0f);SubmitAndRun(actor,target,SSCRomanceAction.Romance,out _);
            var success=actor.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single(m=>m.otherPawn==target);
            Equal(SSCRomanceCompositeEvent.CommandRomanceAccepted,success.Data.CompositeEvent);
            CommandReset(1f,0f);SubmitAndRun(actor,target,SSCRomanceAction.Proposal,out _);
            var ended=actor.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single(m=>m.otherPawn==target);
            Equal(SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated,ended.Data.CompositeEvent);
            Equal(SSCRomanceMoodGroup.Long,ended.Data.Group);
            CommandReset(0f);SubmitAndRun(actor,target,SSCRomanceAction.Romance,out _);
            var renewed=actor.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single(m=>m.otherPawn==target);
            Equal(SSCRomanceCompositeEvent.CommandRomanceAccepted,renewed.Data.CompositeEvent);
            Require(actor.needs.mood.thoughts.memories.Memories.Contains(carrHistory));Equal(4321,carrHistory.age);
        });
    }

    /// <summary>准备五个独立经历；只走生产准入，不通过直接列表删除伪造清理结果。</summary>
    private static List<Thought_MemorySSCRomance> SeedFiveSemanticGroups(Pawn owner,Pawn other)
    {
        var result=new List<Thought_MemorySSCRomance>();var h=owner.needs.mood.thoughts.memories;
        foreach(var kind in new[]{SSCRomanceCompositeEvent.ForcedRomanceRejected,SSCRomanceCompositeEvent.CommandRomanceRejected,
            SSCRomanceCompositeEvent.ForcedProposalRejected,SSCRomanceCompositeEvent.CommandProposalRejected,SSCRomanceCompositeEvent.CommandEndLover})
        {
            var memory=SemanticMemory(kind,other,raw:60);h.TryGainMemory(memory,other);memory.age=321;
            Require(h.Memories.Contains(memory));result.Add(memory);
        }
        Equal(5,h.Memories.OfType<Thought_MemorySSCRomance>().Count(m=>m.otherPawn==other));return result;
    }
}
