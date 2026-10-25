// AI 辅助生成：真实 worker、关系写入与记忆准入；图形、完整世界及文字留实机验证。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>第二／三批覆盖原始比较、三类保证、命令与中断，不在宿主重写关系结算。</summary>
    private static void RomanceMoodInteractionTests(Harmony harmony)
    {
        for(int mask=0;mask<8;mask++)
        foreach(bool allow in new[]{false,true})
        for(int category=0;category<3;category++)
        foreach(bool original in new[]{false,true})
        foreach(var action in new[]{SSCRomanceAction.Romance,SSCRomanceAction.Proposal})
        {
            int choices=mask,cat=category;
            Check($"mood actual passive {choices}/{allow}/{cat}/{original}/{action}",()=>
            {
                CommandReset(original?0f:1f,1f);socialChance=.5f;
                var actor=cat==1?SocialSlave(severity:.1f):SocialPawn();
                var target=SocialSlave(cat==0?actor:null,.5f);
                Comp(target).romanceConfig=new(){allowAcceptRomance=allow,allowAcceptProposal=allow,
                    alwaysAcceptMaster=(choices&1)!=0,alwaysAcceptSexSlave=(choices&2)!=0,alwaysAcceptOthers=(choices&4)!=0};
                if(action==SSCRomanceAction.Proposal)actor.relations.AddDirectRelation(PawnRelationDefOf.Lover,target);
                Require(actor.interactions.TryInteractWith(target,SocialDef(action)));
                bool guarantee=(choices&(1<<cat))!=0, accepted=guarantee || allow&&original;
                var memories=target.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().ToList();
                Equal(original!=accepted?1:0,memories.Count);
                if(memories.Count>0){Equal(accepted?-3:-2,memories[0].Data.Value);Equal(3*60000,memories[0].DurationTicks);}
                Require(!actor.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
            });
        }
        foreach(var action in new[]{SSCRomanceAction.Romance,SSCRomanceAction.Proposal})
        foreach(bool accepted in new[]{false,true})
            Check($"mood command actor completed {action}/{accepted}",()=>
            {
                CommandReset(accepted?0f:1f,1f);var(a,b)=CommandPair(action,false);
                SubmitAndRun(a,b,action,out var record);
                var m=a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(2,m.Data.Value);Equal(3*60000,m.DurationTicks);Require(!m.Data.HasRule);
                Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
                m.age=123;((JobDriver_SSCRomanceCommand)a.jobs.curDriver).Cleanup(Verse.AI.JobCondition.InterruptForced);Equal(123,m.age);
            });
        foreach(var relation in new[]{PawnRelationDefOf.Lover,PawnRelationDefOf.Fiance,PawnRelationDefOf.Spouse})
            Check("mood command breakup confirmed loss "+relation.defName,()=>
            {
                CommandReset();var(a,b)=CommandPair(SSCRomanceAction.Breakup,false);
                a.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover,b);a.relations.AddDirectRelation(relation,b);
                SubmitAndRun(a,b,SSCRomanceAction.Breakup,out _);
                var memories=a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().ToList();Equal(2,memories.Count);
                var loss=memories.Single(m=>m.Data.Kind==SSCRomanceMoodEvent.RelationshipLoss);
                Equal(relation==PawnRelationDefOf.Lover?-2:-3,loss.Data.Value);Equal((relation==PawnRelationDefOf.Spouse?30:25)*60000,loss.DurationTicks);
                Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
            });
        foreach(bool skip in new[]{false,true})
            Check("mood skipped or throwing worker never awards "+skip,()=>
            {
                CommandReset(0f);var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
                var boundary=new Harmony("ssc.tests.mood.worker-boundary");
                try
                {
                    if(skip)boundary.Patch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt),"Interacted"),prefix:new HarmonyMethod(typeof(Program),nameof(Skip)));
                    else boundary.Patch(AccessTools.Method(typeof(SSCRomanceExecutionScope),nameof(SSCRomanceExecutionScope.ObserveDecision)),postfix:new HarmonyMethod(typeof(Program),nameof(FeedbackThrow)));
                    try{a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt);}catch(InvalidOperationException){}
                    Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
                }
                finally{boundary.UnpatchAll(boundary.Id);}
            });
        Check("mood old-personality token cannot receive completed interaction",()=>
        {
            CommandReset(0f);var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            moodMutationPawn=b;var boundary=new Harmony("ssc.tests.mood.personality");
            try{boundary.Patch(AccessTools.Method(typeof(SSCRomanceMoodEvents),nameof(SSCRomanceMoodEvents.Observe)),postfix:new HarmonyMethod(typeof(Program),nameof(MoodMutate)));a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt);}
            finally{boundary.UnpatchAll(boundary.Id);moodMutationPawn=null;}
            Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
        });
        Check("mood overlapping owner and SSC identity records actual peer guarantee",()=>
        {
            CommandReset(1f);var actor=SocialSlave();var target=SocialSlave(actor,.5f);
            Comp(target).romanceConfig=new(){alwaysAcceptSexSlave=true};actor.interactions.TryInteractWith(target,InteractionDefOf.RomanceAttempt);
            var memory=target.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Require(memory.Data.HasRule);Equal(SSCRomanceSetting.AlwaysAcceptSexSlave,memory.Data.Rule);
        });
        foreach(float severity in new[]{.1f,.3f})
            Check("mood dormant passive guarantee does not award "+severity,()=>
            {
                CommandReset(1f);var actor=SocialPawn();var target=SocialSlave(actor,severity);
                Comp(target).romanceConfig=new(){alwaysAcceptMaster=true,allowAcceptRomance=false};
                actor.interactions.TryInteractWith(target,InteractionDefOf.RomanceAttempt);
                Require(!target.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
            });
        Check("mood two controlled participants settle own experience once",()=>
        {
            CommandReset(0f);var(a,b)=CommandPair(SSCRomanceAction.Romance,true);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            SubmitAndRun(a,b,SSCRomanceAction.Romance,out _);
            Equal(2,a.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single().Data.Value);
            Equal(1,b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single().Data.Value);
        });
        Check("mood social fight tracker true without worker never awards romance memory",()=>
        {
            CommandReset(0f);var a=SocialPawn();var b=SocialSlave(severity:.5f);Comp(b).romanceConfig=new(){allowAcceptRomance=false};
            AccessTools.Field(typeof(Thing),"mapIndexOrState").SetValue(b,(sbyte)0);
            var mapsField=AccessTools.Field(typeof(Game),"maps");var oldMaps=mapsField.GetValue(Current.Game);
            mapsField.SetValue(Current.Game,new List<Map>{Bare<Map>()});
            var boundary=new Harmony("ssc.tests.mood.social-fight");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(Pawn_InteractionsTracker),"CheckSocialFightStart"),prefix:new HarmonyMethod(typeof(Program),nameof(MoodFight)));
                Require(a.interactions.TryInteractWith(b,InteractionDefOf.RomanceAttempt));
                Require(!b.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());Equal(1,playLogCalls);
            }
            finally{boundary.UnpatchAll(boundary.Id);mapsField.SetValue(Current.Game,oldMaps);}
        });
    }
    private static Pawn moodMutationPawn;
    /// <summary>只控制斗殴触发输入，tracker 是否跳过 worker 及返回 true 均运行原版代码。</summary>
    private static bool MoodFight(ref bool __result){__result=true;return false;}
    /// <summary>实际旋转人格 token，验证结算不能通过重捕获上下文续权。</summary>
    private static void MoodMutate(){if(SSCPersonalityRuntimeUtility.TryBeginMutation(moodMutationPawn,out var scope))scope.Dispose();}
}
