using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

// 生产反馈 + 完成分发 + 人格记忆；只将引擎 API 和其他收益分量替换为可观测边界。
internal static class Program
{
    private static string root;
    private static int passed,failed;
    private static ThoughtDef Mood=>SSCDefOf.SSC_DesireInfestation_Mood;
    private static ThoughtDef Social=>SSCDefOf.SSC_DesireInfestation_Opinion;
    private static List<Thought_Memory> Memories(Pawn p)=>p.needs.mood.thoughts.memories.Memories;
    private static void Check(bool value,string message="assertion"){if(!value)throw new Exception(message);}
    private static void Near(float actual,float expected)=>Check(Math.Abs(actual-expected)<.0001f,$"{actual} != {expected}");
    private static Pawn Master()=>new(){Training=new(){pawnIdentity=PawnIdentity.Master},LabelShort="master"};
    private static (Pawn m,Pawn t) Scene(float c=.1f,int o=0)
    {
        var t=new Pawn();t.needs.corruption.CurLevel=c;t.relations.Opinion=o;t.needs.mood=new Need_Mood(t);return(Master(),t);
    }
    private static void Complete(Pawn m,Pawn t,Action original=null,bool reverse=false,bool claim=true)
    {
        using var context=SSCOrdinarySexOutcomeUtility.Capture(reverse?t:m,reverse?m:t,true,()=>true,()=>claim);
        original?.Invoke();if(context.TryClaim())context.Dispatch();context.Dispatch();
    }
    private static void Seed(Pawn t,Pawn m,ThoughtDef def,int stage=0,int age=0)
    {var memory=ThoughtMaker.MakeThought(def,stage);memory.age=age;t.needs.mood.thoughts.memories.TryGainMemory(memory,m);}
    private static void Run(string name,Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        Scribe.mode=LoadSaveMode.Inactive;Scribe.node.Clear();Log.Warnings.Clear();
        ThoughtMaker.OnMade=null;ThoughtUtility.OnCheck=null;MemoryThoughtHandler.OnRemoved=null;MemoryThoughtHandler.OnGained=null;
        SSCPnaSexSupplyUtility.Calls=0;SSCPnaSexSupplyUtility.OnApply=null;CorruptionUtility.OnApply=null;
        SSCMod.settings=new();InfestationDefLoader.Load(root);
        try{action();passed++;Console.WriteLine("通过："+name);}catch(Exception ex){failed++;Console.WriteLine("失败："+name+"\n"+ex);}
    }
    private static int Main(string[] args)
    {
        root=SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        foreach(float c in new[]{.29999f,.30f,.30001f})foreach(int o in new[]{-1,0})foreach(bool reverse in new[]{false,true})
            Run($"边界 C={c} O={o} reverse={reverse}",()=>{
                var s=Scene(c,o);Complete(s.m,s.t,reverse:reverse);
                int stage=c<.30f?(o<0?1:0):(o<0?3:2);
                var mood=Memories(s.t).Single(x=>x.def==Mood);Check(mood.CurStageIndex==stage);Near(mood.MoodOffset(),new[]{4,-4,8,4}[stage]);
                var social=Memories(s.t).SingleOrDefault(x=>x.def==Social);
                if(stage==3)Check(social==null);else{Check(social.otherPawn==s.m);Near(((Thought_MemorySocial)social).OpinionOffset(),new[]{5,-5,10,0}[stage]);}
                Check(SSCPnaSexSupplyUtility.Calls==1,"完成分发不能重入");
            });
        foreach(float c in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})Run("非法 C 只跳反馈 "+c,()=>{
            var s=Scene(c);Complete(s.m,s.t);Check(Memories(s.t).Count==0);Check(SSCPnaSexSupplyUtility.Calls==1);
        });
        Run("原结算跨过 C/O 边界仍用旧档，下场重采",()=>{
            var s=Scene(.295f,-1);Complete(s.m,s.t,()=>{s.t.needs.corruption.CurLevel=.35f;s.t.relations.Opinion=1;});
            Check(Memories(s.t).All(x=>x.CurStageIndex==1));Complete(s.m,s.t);Check(Memories(s.t).All(x=>x.CurStageIndex==2));
        });
        Run("本场额外恶堕跨界不改变档位",()=>{
            SSCMod.settings.useOldScoring=true;var s=Scene(.295f);Complete(s.m,s.t);Near(s.t.needs.corruption.CurLevel,.305f);Check(Memories(s.t).All(x=>x.CurStageIndex==0));
        });
        Run("旧反馈参与下一场原始 OpinionOf，不先扣除",()=>{
            var s=Scene();Seed(s.t,s.m,Social,0);s.t.relations.Query=m=>-3+(int)Memories(s.t).OfType<Thought_MemorySocial>().Where(x=>x.otherPawn==m).Sum(x=>x.OpinionOffset());
            Complete(s.m,s.t);Check(Memories(s.t).All(x=>x.CurStageIndex==0));
            s.t.needs.mood.thoughts.memories.Advance(180001);Complete(s.m,s.t);Check(Memories(s.t).All(x=>x.CurStageIndex==1));
        });
        foreach(var (old,next) in new (int, int)[]
        {
            (0, 0),
            (0, 1),
            (1, 1),
            (1, 2),
            (1, 3),
            (2, 2),
            (2, 3),
            (3, 0),
            (3, 2),
            (3, 3),
        })Run($"跨档替换 {old}->{next}",()=>{
            var s=Scene(next>=2?.4f:.1f,next%2==0?0:-1);Seed(s.t,s.m,Mood,old,30000);Seed(s.t,s.m,Social,old,90000);
            Complete(s.m,s.t);Check(Memories(s.t).Count(x=>x.def==Mood)==1);Check(Memories(s.t).Where(x=>x.def==Mood).All(x=>x.CurStageIndex==next&&x.age==0));
            Check(Memories(s.t).Count(x=>x.def==Social)==(next==3?0:1));Check(Memories(s.t).Where(x=>x.def==Social).All(x=>x.CurStageIndex==next&&x.age==0));
        });
        Run("同主人重复十次无累积",()=>{
            var s=Scene(.5f);for(int i=0;i<10;i++)Complete(s.m,s.t);Check(Memories(s.t).Count==2);Near(Memories(s.t).OfType<Thought_MemorySocial>().Single().OpinionOffset(),10);
        });
        Run("不同主人共享一份心情，各自保留社交",()=>{
            var s=Scene();var other=Master();Complete(s.m,s.t);Complete(other,s.t);Check(Memories(s.t).Count(x=>x.def==Mood)==1);Check(Memories(s.t).Count(x=>x.def==Social)==2);
        });
        Run("三主人容量，刷新后按最老淘汰且同名不串组",()=>{
            var s=Scene();var masters=new[]{s.m,Master(),Master(),Master()};
            for(int i=0;i<3;i++){Complete(masters[i],s.t);Memories(s.t).Single(x=>x.def==Social&&x.otherPawn==masters[i]).age=300-i*100;}
            Complete(masters[0],s.t);Complete(masters[3],s.t);
            var social=Memories(s.t).Where(x=>x.def==Social).ToList();Check(social.Count==3);Check(social.All(x=>x.otherPawn!=masters[1]));
        });
        Run("零社交只移除本施加者，不清其他主人的记录",()=>{
            var s=Scene(.5f,-1);var other=Master();Seed(s.t,s.m,Social,1);Seed(s.t,other,Social,2);Complete(s.m,s.t);
            Check(Memories(s.t).Count(x=>x.def==Social)==1&&Memories(s.t).Single(x=>x.def==Social).otherPawn==other);
        });
        Run("期限、最后 30% 社交衰减及刷新",()=>{
            var s=Scene();Complete(s.m,s.t);var h=s.t.needs.mood.thoughts.memories;h.Advance(60000);Check(Memories(s.t).All(x=>x.def!=Mood));
            h.Advance(66000);Near(Memories(s.t).OfType<Thought_MemorySocial>().Single().OpinionOffset(),5);
            h.Advance(27000);Near(Memories(s.t).OfType<Thought_MemorySocial>().Single().OpinionOffset(),2.5f);
            Complete(s.m,s.t);Check(Memories(s.t).All(x=>x.age==0));h.Advance(180000);Check(Memories(s.t).Count==0);
        });
        Run("不改 RJW 或调教同对象记忆",()=>{
            var s=Scene();var original=new ThoughtDef{defName="RJW",durationDays=10,stackLimit=40,stages=new(){new(){baseMoodEffect=-9}}};
            Seed(s.t,s.m,original);var old=Memories(s.t).Single();Complete(s.m,s.t);Complete(s.m,s.t);Check(Memories(s.t).Contains(old));
        });
        foreach(string invalid in new[]{"off","master","dead","hollow","noNeed","noMood","noRelations","noClaim"})Run("准入/载体 "+invalid,()=>{
            var s=Scene();switch(invalid){case "off":s.m.Training.Infestation=false;break;case "master":s.t.Training.pawnIdentity=PawnIdentity.Master;break;case "dead":s.t.Dead=true;break;case "hollow":s.t.Hollow=true;break;case "noNeed":s.t.needs.corruption=null;break;case "noMood":s.t.needs.mood=null;break;case "noRelations":s.t.relations=null;break;}
            Complete(s.m,s.t,claim:invalid!="noClaim");Check(s.t.needs.mood==null||Memories(s.t).Count==0);
            if(invalid=="noMood"||invalid=="noRelations")Check(SSCPnaSexSupplyUtility.Calls==1);
        });
        foreach(string replaced in new[]{"newMood","newHandler","newNeed","addedMood","newThoughts"})Run("原结算替换载体 "+replaced,()=>{
            var s=Scene();if(replaced=="addedMood")s.t.needs.mood=null;
            Complete(s.m,s.t,()=>{switch(replaced){case "newMood":case "addedMood":s.t.needs.mood=new(s.t);break;case "newHandler":s.t.needs.mood.thoughts.memories=new(s.t);break;case "newThoughts":s.t.needs.mood.thoughts=new(){memories=new(s.t)};break;case "newNeed":s.t.needs.corruption=new();break;}});
            Check(Memories(s.t).Count==0);
        });
        foreach(bool source in new[]{false,true})foreach(string point in new[]{"original","PNA","corruption","check","make","remove","gain"})Run($"回调换人格 {point} source={source}",()=>{
            var s=Scene();Seed(s.t,s.m,Mood,1);bool changed=false;
            void Change(){if(changed)return;changed=true;SSCPersonalityRuntimeUtility.TryBeginMutation(source?s.m:s.t,out var scope);scope.Dispose();}
            switch(point){case "PNA":SSCPnaSexSupplyUtility.OnApply=Change;break;case "corruption":CorruptionUtility.OnApply=Change;break;case "check":ThoughtUtility.OnCheck=Change;break;case "make":ThoughtMaker.OnMade=_=>Change();break;case "remove":MemoryThoughtHandler.OnRemoved=_=>Change();break;case "gain":MemoryThoughtHandler.OnGained=_=>Change();break;}
            Complete(s.m,s.t,point=="original"?Change:null);Check(changed);Check(Memories(s.t).All(x=>x.def!=Social),"人格失效后不能继续发社交");
        });
        Run("删除旧心情的回调恢复新人格后，旧实例循环立即停止",()=>{
            var s=Scene();Seed(s.t,s.m,Mood);var duplicate=ThoughtMaker.MakeThought(Mood,1);Memories(s.t).Add(duplicate);
            MemoryThoughtHandler.OnRemoved=_=>{MemoryThoughtHandler.OnRemoved=null;SSCPersonalityRuntimeUtility.TryBeginMutation(s.t,out var scope);Memories(s.t).Clear();Memories(s.t).Add(duplicate);scope.Dispose();};
            Complete(s.m,s.t);Check(Memories(s.t).Count==1&&Memories(s.t)[0]==duplicate);
        });
        foreach(string bad in new[]{"missing","wrongType","noStages","nanOffset","blocked","throwMake"})Run("心情定义异常不阻断社交 "+bad,()=>{
            var s=Scene();Seed(s.t,s.m,Mood,1);var old=Memories(s.t).Single();
            switch(bad){case "missing":SSCDefOf.SSC_DesireInfestation_Mood=null;break;case "wrongType":Mood.thoughtClass=typeof(Thought_MemorySocial);break;case "noStages":Mood.stages.Clear();break;case "nanOffset":Mood.stages[0].baseMoodEffect=float.NaN;break;case "blocked":Mood.Blocked=true;break;case "throwMake":ThoughtMaker.OnMade=x=>{if(x.def==Mood)throw new Exception("third party");};break;}
            Complete(s.m,s.t);Check(Memories(s.t).Contains(old));Check(Memories(s.t).Any(x=>x.def==Social));
        });
        Run("社交源发育阶段预检失败保留旧记忆",()=>{
            var s=Scene();Seed(s.t,s.m,Social,1);var old=Memories(s.t).Single();Social.socialTargetDevelopmentalStageFilter=DevelopmentalStage.None;
            Complete(s.m,s.t);Check(Memories(s.t).Contains(old));Check(Memories(s.t).Any(x=>x.def==Mood));
        });
        Run("关系查询异常只取消反馈",()=>{
            var s=Scene();s.t.relations.Query=_=>throw new Exception("query");Complete(s.m,s.t);Check(Memories(s.t).Count==0&&SSCPnaSexSupplyUtility.Calls==1);
        });
        Run("没有新完成认领不能重复恢复旧场反馈",()=>{
            var s=Scene();Complete(s.m,s.t);foreach(var memory in Memories(s.t))memory.age=500;Complete(s.m,s.t,claim:false);Check(Memories(s.t).All(x=>x.age==500));
        });
        Run("直接计划单次执行与加载期保护",()=>{
            var s=Scene();var plan=SSCDesireInfestationFeedbackUtility.Capture(s.m,s.t,s.t.needs.corruption);plan.Apply(()=>true);
            foreach(var memory in Memories(s.t))memory.age=100;plan.Apply(()=>true);Check(Memories(s.t).All(x=>x.age==100));
            Scribe.mode=LoadSaveMode.LoadingVars;Check(SSCDesireInfestationFeedbackUtility.Capture(s.m,s.t,s.t.needs.corruption)==null);Scribe.mode=LoadSaveMode.Inactive;
        });
        Run("关闭能力后旧记忆仍按期限保留",()=>{
            var s=Scene();Complete(s.m,s.t);foreach(var memory in Memories(s.t))memory.age=900;s.m.Training.Infestation=false;Complete(s.m,s.t);Check(Memories(s.t).All(x=>x.age==900));
        });
        Run("标准实例存读档恢复阶段、年龄和实际主人",StandardPersistence);
        Run("人格采集、复制、序列化、植入保持剩余期限且可跨档替换",PersonalityPersistence);
        Console.WriteLine($"结果：{passed}/{passed+failed} 项通过。");return failed==0?0:1;
    }
    private static void StandardPersistence()
    {
        var s=Scene(.5f);Complete(s.m,s.t);
        foreach(var memory in Memories(s.t))
        {
            memory.age=54321;Scribe.node.Clear();Scribe.mode=LoadSaveMode.Saving;memory.ExposeData();
            var restored=ThoughtMaker.MakeThought(memory.def,0);Scribe.mode=LoadSaveMode.LoadingVars;restored.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
            Check(restored!=memory&&restored.age==54321&&restored.CurStageIndex==2&&restored.otherPawn==s.m);
            if(restored is Thought_MemorySocial social)Near(social.opinionOffset,10);
        }
    }
    private static void PersonalityPersistence()
    {
        var s=Scene(.5f);Complete(s.m,s.t);var data=new List<StoredMemoryData>();
        foreach(var memory in Memories(s.t))
        {
            memory.age=40000;var captured=PersonalityMemoryUtility.Capture(memory);var copied=PersonalityMemoryUtility.Copy(captured);Check(copied!=captured);
            Scribe.node.Clear();Scribe.mode=LoadSaveMode.Saving;copied.ExposeData();var loaded=new StoredMemoryData();Scribe.mode=LoadSaveMode.LoadingVars;loaded.ExposeData();Scribe.mode=LoadSaveMode.Inactive;data.Add(loaded);
        }
        var host=Scene(.1f,-1).t;PersonalityMemoryUtility.Restore(host,data);Check(Memories(host).Count==2&&Memories(host).All(x=>x.age==40000&&x.CurStageIndex==2&&x.otherPawn==s.m));
        Complete(s.m,host);Check(Memories(host).Count==2&&Memories(host).All(x=>x.age==0&&x.CurStageIndex==1));
    }
}
