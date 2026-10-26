// AI 辅助生成：第二批提交前复核，复现提交实际日志、恢复后置准入与原生尾部容量边界。
using HarmonyLib;
using System.Reflection.Emit;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action<Thought_Memory> compositeB2Before, compositeB2After;
    private static Action compositeB2Admission;
    private static Thought_Memory compositeB2Outer, compositeB2Restored;
    private static bool compositeB2Skip;

    /// <summary>在真实 native 方法上注入一次兼容回调，不重做生产合并或历史恢复。</summary>
    private static void RomanceCompositeBatch2ReviewTests()
    {
        Check("composite b2 review rollback restores actual commit predecessor",()=>
        {
            var p=SocialPawn();var h=p.needs.mood.thoughts.memories;var original=MoodMemory(-2);h.TryGainMemory(original);original.age=777;
            Thought_Memory inner=null;var boundary=CompositeB2Boundary();
            try
            {
                compositeB2Before=memory=>
                {
                    compositeB2Outer=memory;
                    Require(new SSCRomanceMoodRequest(p,null,CompositeSelection(-6,SSCRomanceMoodGroup.Short)).Execute().Applied);
                    inner=h.Memories.Single();inner.age=333;
                };
                compositeB2After=memory=>{if(memory==compositeB2Outer)throw new InvalidOperationException("outer failed after nested event");};
                var result=new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute();
                Require(!result.Completed);Equal(inner,h.Memories.Single());Equal(333,inner.age);Require(!h.Memories.Contains(original));
            }
            finally{CompositeB2Clear(boundary);}
        });
        Check("composite b2 review uncommitted failure never revives external cleanup",()=>
        {
            var p=SocialPawn();var h=p.needs.mood.thoughts.memories;var original=MoodMemory(-2);h.TryGainMemory(original);
            var boundary=CompositeB2Boundary();
            try
            {
                compositeB2Before=_=>h.Memories.Remove(original);compositeB2Skip=true;
                Require(!new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute().Completed);
                Equal(0,h.Memories.Count);
            }
            finally{CompositeB2Clear(boundary);}
        });
        foreach(string field in new[]{"value","age","definition"})Check("composite b2 review restoration post-admission freezes "+field,()=>
        {
            var memory=CompositeMemory(-15,SSCRomanceMoodGroup.Long);memory.age=54321;
            var saved=PersonalityMemoryUtility.Capture(memory);var host=SocialPawn();var boundary=CompositeB2Boundary();
            try
            {
                boundary.Patch(AccessTools.Method(typeof(ThoughtMaker),nameof(ThoughtMaker.MakeThought),new[]{typeof(ThoughtDef),typeof(int)}),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeB2CaptureFactory)));
                compositeB2Admission=()=>
                {
                    if(compositeB2Restored==null)return;
                    if(field=="value")((Thought_MemorySSCRomance)compositeB2Restored).Data.Value=15;
                    else if(field=="age")compositeB2Restored.age=0;
                    else compositeB2Restored.def=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);
                };
                PersonalityMemoryUtility.Restore(host,new(){saved});Require(compositeB2Restored!=null);
                Equal(0,host.needs.mood.thoughts.memories.Memories.Count);Equal(54321,saved.age);Equal(-15,saved.romanceMood.Value);
            }
            finally{CompositeB2Clear(boundary);}
        });
        Check("composite b2 review restoration rejects factory stage substitution",()=>
        {
            var memory=CompositeMemory(-15,SSCRomanceMoodGroup.Long);var saved=PersonalityMemoryUtility.Capture(memory);var host=SocialPawn();
            var boundary=CompositeFactoryBoundary();
            try
            {
                compositeReviewAfterFactory=thought=>((Thought_Memory)thought).SetForcedStage(30);
                PersonalityMemoryUtility.Restore(host,new(){saved});Require(compositeReviewAfterFactory==null);
                Equal(0,host.needs.mood.thoughts.memories.Memories.Count);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeReviewAfterFactory=null;}
        });
        Check("composite b2 review opaque history arriving during admission survives native tail",()=>
        {
            var p=SocialPawn();var h=p.needs.mood.thoughts.memories;var old=CompositeMemory(-13,SSCRomanceMoodGroup.Long);h.TryGainMemory(old);old.age=1;
            var opaque=CompositeMemory(-15,SSCRomanceMoodGroup.Long);opaque.Data.DataVersion=99;opaque.pawn=p;opaque.age=99999;
            var boundary=CompositeB2Boundary();
            try
            {
                compositeB2Admission=()=>{h.Memories.Add(opaque);compositeB2Admission=null;};
                var result=new SSCRomanceMoodRequest(p,null,CompositeSelection(-15,SSCRomanceMoodGroup.Long)).Execute();
                Require(!result.Completed);Require(h.Memories.Contains(opaque));Require(h.Memories.Contains(old));Equal(99999,opaque.age);Equal(1,old.age);
            }
            finally{CompositeB2Clear(boundary);}
        });
        Check("composite b2 review admission nested stronger event yields normal kept receipt",()=>
        {
            var p=SocialPawn();var h=p.needs.mood.thoughts.memories;h.TryGainMemory(MoodMemory(2));var boundary=CompositeB2Boundary();
            Thought_Memory inner=null;
            try
            {
                compositeB2Before=_=>{Require(new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute().Applied);inner=h.Memories.Single();inner.age=543;};
                var result=new SSCRomanceMoodRequest(p,null,CompositeSelection(-6,SSCRomanceMoodGroup.Short)).Execute();
                Equal(SSCRomanceMoodResultKind.Kept,result.Kind);Require(result.Completed&&!result.Applied);Equal(inner,h.Memories.Single());Equal(543,inner.age);
            }
            finally{CompositeB2Clear(boundary);}
        });
        Check("composite b2 review ordinary native parent still makes and renews child",()=>
        {
            var p=SocialPawn();var other=SocialPawn();var h=p.needs.mood.thoughts.memories;
            var parent=new ThoughtDef{defName="B2ReviewNativeParent",thoughtClass=typeof(Thought_Memory),stackLimit=1,durationDays=3,showBubble=false,
                thoughtToMake=moodNativeLoss,stages=new(){new ThoughtStage{label="Review parent",description="Test",baseMoodEffect=1}}};
            DefDatabase<ThoughtDef>.Add(parent);h.TryGainMemory(parent,other);
            foreach(var memory in h.Memories)memory.age=321;
            h.TryGainMemory(parent,other);Equal(2,h.Memories.Count);Require(h.Memories.All(m=>m.age==0));
            Equal(1,h.Memories.Count(m=>m.def==moodNativeLoss));
        });
        Check("composite b2 review ordinary inferred native memory class still restores",()=>
        {
            var def=new ThoughtDef{defName="B2ReviewInferredMemory",thoughtClass=null,durationDays=3,showBubble=false,stackLimit=1,
                stages=new(){new ThoughtStage{label="Inferred",description="Test",baseMoodEffect=2}}};DefDatabase<ThoughtDef>.Add(def);
            var source=ThoughtMaker.MakeThought(def,0);source.age=12345;source.otherPawn=SocialPawn();var host=SocialPawn();
            PersonalityMemoryUtility.Restore(host,new(){PersonalityMemoryUtility.Capture(source)});
            var actual=host.needs.mood.thoughts.memories.Memories.Single();Equal(typeof(Thought_Memory),actual.GetType());Equal(12345,actual.age);Equal(source.otherPawn,actual.otherPawn);
        });
        foreach(string change in new[]{"token","comp","exception"})Check("composite b2 review CopyNeeds scope exits on "+change,()=>
        {
            var source=SocialPawn();source.needs.mood.def.needClass=typeof(Need_Mood);source.needs.AllNeeds.Add(source.needs.mood);
            var original=CompositeMemory(-15,SSCRomanceMoodGroup.Long);original.Data.BranchId="Retired";original.pawn=source;
            source.needs.mood.thoughts.memories.Memories.Add(original);var clone=SocialPawn();var boundary=CompositeFactoryBoundary();
            boundary.Patch(AccessTools.Method(typeof(ThoughtMaker),nameof(ThoughtMaker.MakeThought),new[]{typeof(ThoughtDef)}),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeReviewFactoryPostfix)));
            boundary.CreateClassProcessor(production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodCopiedHistory")).Patch();
            try
            {
                compositeReviewAfterFactory=_=>
                {
                    if(change=="token"){Require(SSCPersonalityRuntimeUtility.TryBeginMutation(clone,out var scope));scope.Dispose();}
                    else if(change=="comp")AccessTools.Field(typeof(ThingWithComps),"comps").SetValue(clone,new List<ThingComp>{new CompSexSlaveTraining{parent=clone}});
                    else throw new InvalidOperationException("copy failure");
                };
                try{AccessTools.Method(typeof(GameComponent_PawnDuplicator),"CopyNeeds").Invoke(null,new object[]{source,clone});Require(change!="exception");}
                catch(System.Reflection.TargetInvocationException ex)when(change=="exception"&&ex.InnerException is InvalidOperationException){}
                Require(compositeReviewAfterFactory==null);
                if(change!="exception")
                {
                    var copy=clone.needs.mood.thoughts.memories.Memories.Single();clone.needs.mood.thoughts.memories.RemoveMemory(copy);
                    clone.needs.mood.thoughts.memories.TryGainMemory(copy);Equal(0,clone.needs.mood.thoughts.memories.Memories.Count);
                }
                // 即使复制抛异常，下一个普通单条 CopyFrom 仍能按既有兼容模式恢复未知历史。
                var standalone=CompositeMemory(-15,SSCRomanceMoodGroup.Long);standalone.CopyFrom(original);var host=SocialPawn();
                host.needs.mood.thoughts.memories.TryGainMemory(standalone);Equal(1,host.needs.mood.thoughts.memories.Memories.Count);
            }
            finally{boundary.UnpatchAll(boundary.Id);compositeReviewAfterFactory=null;}
        });
        CompositeB2GuardTests();
    }

    /// <summary>用实际 IL 校验唯一锚点、例外区、标签及卸载；失败时绝不发新记忆或重放旧事件。</summary>
    private static void CompositeB2GuardTests()
    {
        var hook=production.GetType("SexSlaveCraft.Harmony_SSCRomanceMoodAdmissionTail");var transform=AccessTools.Method(hook,"Transpiler");
        var target=AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)});
        var merge=AccessTools.Method(typeof(Thought_Memory),nameof(Thought_Memory.TryMergeWithExistingMemory));
        List<CodeInstruction> Rewrite(List<CodeInstruction> codes,ILGenerator generator=null)=>((IEnumerable<CodeInstruction>)transform.Invoke(null,new object[]{codes,generator??new DynamicMethod("b2-review-tail",typeof(void),Type.EmptyTypes).GetILGenerator()})).ToList();
        foreach(string kind in new[]{"missing","duplicate","exception-region","branch"})Check("composite b2 review unsupported tail anchor "+kind,()=>
        {
            var original=kind=="missing"?new List<CodeInstruction>{new(OpCodes.Ret)}:PatchProcessor.GetOriginalInstructions(target).ToList();
            if(kind=="duplicate")original.AddRange(PatchProcessor.GetOriginalInstructions(target));
            else if(kind=="exception-region")original[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            else if(kind=="branch")original[original.FindIndex(c=>c.Calls(merge))+1].opcode=OpCodes.Pop;
            try
            {
                var result=Rewrite(original);Equal(original.Count,result.Count);
                Require(original.Zip(result).All(p=>p.First.opcode==p.Second.opcode&&Equals(p.First.operand,p.Second.operand)));
                var p=SocialPawn();Equal(SSCRomanceMoodResultKind.InvalidData,new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute().Kind);
                Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
            }
            finally{Rewrite(PatchProcessor.GetOriginalInstructions(target).ToList());}
        });
        Check("composite b2 review tail moves branch label and balances bool stack",()=>
        {
            var original=PatchProcessor.GetOriginalInstructions(target).ToList();int at=original.FindIndex(c=>c.Calls(merge))+1;
            var generator=new DynamicMethod("b2-label",typeof(void),Type.EmptyTypes).GetILGenerator();var label=generator.DefineLabel();original[at].labels.Add(label);
            var result=Rewrite(original,generator);Equal(original.Count+5,result.Count);Equal(OpCodes.Ldarg_1,result[at].opcode);Require(result[at].labels.Contains(label));
            Equal(OpCodes.Pop,result[at+3].opcode);Equal(OpCodes.Ret,result[at+4].opcode);Require(!result[at+5].labels.Contains(label));
        });
        Check("composite b2 review removed tail guard blocks new requests",()=>
        {
            var owner=new Harmony("ssc.tests.romance-control.native");owner.Unpatch(target,transform);
            try
            {
                var p=SocialPawn();Equal(SSCRomanceMoodResultKind.InvalidData,new SSCRomanceMoodRequest(p,null,CompositeSelection(-9,SSCRomanceMoodGroup.Short)).Execute().Kind);
                Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
            }
            finally{owner.CreateClassProcessor(hook).Patch();}
        });
    }

    /// <summary>独立补丁只包围新综合实例；实际准入、原生尾部和生产分组仍运行。</summary>
    private static Harmony CompositeB2Boundary()
    {
        var boundary=new Harmony("ssc.tests.composite.b2-review");
        var target=AccessTools.Method(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)});
        boundary.Patch(target,prefix:new HarmonyMethod(typeof(Program),nameof(CompositeB2Prefix)),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeB2Postfix)));
        boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.CanGetThought)),postfix:new HarmonyMethod(typeof(Program),nameof(CompositeB2AdmissionAfter)));
        return boundary;
    }
    /// <summary>一次 Prefix 可在外层提交前完成内层事件；清委托后调用以避免测试本身重入。</summary>
    private static bool CompositeB2Prefix(Thought_Memory __0)
    {
        if(__0 is not Thought_MemorySSCCompositeRomance)return true;
        var action=compositeB2Before;compositeB2Before=null;action?.Invoke(__0);return !compositeB2Skip;
    }
    /// <summary>后置失败不影响已完成的内层事件；仅在外层实例命中时触发。</summary>
    private static void CompositeB2Postfix(Thought_Memory __0)
    {if(__0 is Thought_MemorySSCCompositeRomance)compositeB2After?.Invoke(__0);}
    /// <summary>恢复路径的第二次准入在已构造实例之后发生，允许检验快照后回调。</summary>
    private static void CompositeB2AdmissionAfter(ThoughtDef __1)
    {if(__1.defName is Thought_MemorySSCCompositeRomance.ShortDefName or Thought_MemorySSCCompositeRomance.LongDefName)compositeB2Admission?.Invoke();}
    /// <summary>只持有待恢复的测试实例，保存载荷与实际恢复仍由生产层完成。</summary>
    private static void CompositeB2CaptureFactory(Thought __result)
    {if(__result is Thought_MemorySSCCompositeRomance memory)compositeB2Restored=memory;}
    /// <summary>清理测试范围，防止回调或 skip 状态污染后续用例。</summary>
    private static void CompositeB2Clear(Harmony boundary)
    {boundary.UnpatchAll(boundary.Id);compositeB2Before=compositeB2After=null;compositeB2Admission=null;compositeB2Outer=compositeB2Restored=null;compositeB2Skip=false;}
}
