// AI 辅助生成：真实记忆处理器与生产规则；不将本宿主计为实机思想显示。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>首批固定数字、边界、替换及人格快照；后续批次复用同一真实 Def 夹具。</summary>
    private static void RomanceMoodFoundationTests()
    {
        SetupMoodDefs();
        int[,] ordinary = { { -3, 0, -3 }, { -1, -2, -3 }, { 1, 0, 2 } };
        for (int stage = 2; stage <= 4; stage++)
        for (int kind = 0; kind < 3; kind++)
        foreach (bool maso in new[] { false, true })
        {
            int s = stage, k = kind;
            Check($"mood continuous table {s}/{k}/{maso}", () =>
            {
                int expected = s == 2 && k == 1 ? 0 : Math.Clamp(ordinary[s - 2, k] + (maso ? 2 : 0), -4, 4);
                Equal(expected, SSCRomanceMoodRules.Continuous(s, k != 1, k != 0, maso, null));
            });
        }
        foreach (var pair in new[] { (0f,-1), (.24999f,-1), (.25f,0), (.74999f,0), (.75f,1), (1f,1) })
            Check("mood corruption boundary " + pair.Item1, () => Equal(pair.Item2, SSCRomanceMoodRules.CorruptionModifier(pair.Item1)));
        foreach (var pair in new[] { (-100,0), (0,0), (1,1), (39,1), (40,2), (100,2) })
            Check("mood opinion boundary " + pair.Item1, () => Equal(pair.Item2, SSCRomanceMoodRules.OpinionCategory(pair.Item1)));
        foreach (int stage in new[] { 1, 2, 3, 4 })
            Check("mood defaults do not grant free mood " + stage, () =>
            {
                var p = SocialSlave(severity: new[] { 0f,.1f,.3f,.5f,.9f }[stage]);
                Require(!new ThoughtWorker_SSCRomanceControl { def = SSCRomanceMoodDefOf.SSC_Romance_Control }.CurrentState(p).Active);
            });
        Check("mood planned example low-opinion forced acceptance", () => Equal(-5, SSCRomanceMoodRules.Experience(SSCRomanceMoodEvent.ForcedAcceptance,3,false,.5f,0)));
        Check("mood planned example high-opinion masochist command clamp", () => Equal(6, SSCRomanceMoodRules.Experience(SSCRomanceMoodEvent.CommandRomance,4,true,1f,2)));
        var eventKinds=new[]{SSCRomanceMoodEvent.ForcedRefusal,SSCRomanceMoodEvent.ForcedAcceptance,SSCRomanceMoodEvent.CommandRomance,SSCRomanceMoodEvent.CommandProposal,SSCRomanceMoodEvent.CommandBreakup};
        int[,] eventBases={{-2,-3,0,0,0},{1,1,2,2,1}};
        for(int stage=3;stage<=4;stage++)
        for(int kind=0;kind<5;kind++)
        foreach(bool maso in new[]{false,true})
        foreach(float? corruption in new float?[]{null,0,.5f,1})
        for(int opinion=0;opinion<3;opinion++)
        {
            int s=stage,k=kind,o=opinion;
            Check($"mood event table {s}/{k}/{maso}/{corruption}/{o}",()=>
            {
                int modifier=k==0?(o==2?-2:0):k==4?0:o==0?-2:o==2?1:0;
                int expected=s==3&&k>=2?0:Math.Clamp(eventBases[s-3,k]+(maso?2:0)+(corruption==null||corruption==.5f?0:corruption==0?-1:1)+modifier,-6,6);
                Equal(expected,SSCRomanceMoodRules.Experience(eventKinds[k],s,maso,corruption,o));
            });
        }
        foreach(var relation in Enum.GetValues<SSCRomanceOldRelation>())
        for(int opinion=0;opinion<3;opinion++)
        {
            int o=opinion;
            Check($"mood loss table {relation}/{o}",()=>
            {
                int[,] expected={{0,0,0},{0,-2,-4},{0,-3,-5},{0,-3,-5}};
                Equal(expected[(int)relation,o],SSCRomanceMoodRules.Loss(relation,o));
            });
        }
        foreach (int old in new[] { -6,-2,2,6 })
        foreach (int value in new[] { -6,-2,0,2,6 })
            Check($"mood actual handler replacement {old}/{value}", () =>
            {
                var p=SocialPawn(); var handler=p.needs.mood.thoughts.memories;
                var before=MoodMemory(old); handler.TryGainMemory(before); before.age=123;
                if(value!=0) handler.TryGainMemory(MoodMemory(value));
                bool replace=value!=0 && (old<0 ? value<=old : value<0 || value>=old);
                var actual=handler.Memories.OfType<Thought_MemorySSCRomance>().Single();
                Equal(replace?value:old,actual.Data.Value); Equal(replace?0:123,actual.age);
            });
        Check("mood equal loss preserves 29 remaining days", () =>
        {
            var p=SocialPawn(); var handler=p.needs.mood.thoughts.memories;
            var old=MoodMemory(-3,true,SSCRomanceOldRelation.Spouse);handler.TryGainMemory(old);old.age=60000;
            handler.TryGainMemory(MoodMemory(-3,true,SSCRomanceOldRelation.Fiance));
            var actual=handler.Memories.OfType<Thought_MemorySSCRomance>().Single();Equal(29*60000,actual.DurationTicks-actual.age);
            handler.TryGainMemory(MoodMemory(-4,true,SSCRomanceOldRelation.Lover));
            actual=handler.Memories.OfType<Thought_MemorySSCRomance>().Single();Equal(25*60000,actual.DurationTicks-actual.age);
        });
        Check("mood snapshot copy and native CopyFrom keep independent history", () =>
        {
            var memory=MoodMemory(-5);memory.age=9123;memory.Data.Stage=3;memory.Data.Masochist=true;memory.Data.Corruption=.8f;
            var saved=PersonalityMemoryUtility.Capture(memory);var copy=PersonalityMemoryUtility.Copy(saved);
            Require(!ReferenceEquals(saved.romanceMood,copy.romanceMood));
            var cloned=(Thought_MemorySSCRomance)ThoughtMaker.MakeThought(memory.def);cloned.CopyFrom(memory);
            Equal(9123,cloned.age);Equal(-5,cloned.Data.Value);Require(!ReferenceEquals(cloned.Data,memory.Data));
            var p=SocialPawn();PersonalityMemoryUtility.Restore(p,new(){copy});
            var restored=p.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Single();
            Equal(9123,restored.age);Equal(.8f,restored.Data.Corruption.Value);Equal(3,restored.Data.Stage);Require(restored.Data.Masochist);
        });
    }
    /// <summary>仅构建与生产 XML 同结构的真实原生 Def；文本刻意不替维护者定稿。</summary>
    private static void SetupMoodDefs()
    {
        if (SSCRomanceMoodDefOf.SSC_Romance_Experience != null) return;
        SSCRomanceMoodDefOf.Masochist = new TraitDef { defName="Masochist", degreeDatas=new(){new TraitDegreeData{degree=0,label=""}} };
        foreach(var item in new[]{("Control",-4,4),("Experience",-6,6),("Loss",-5,-1)})
        {
            var def=new ThoughtDef{defName="SSC_Romance_"+item.Item1,thoughtClass=item.Item1=="Control"?typeof(Thought_Situational):typeof(Thought_MemorySSCRomance),
                durationDays=item.Item1=="Control"?0:item.Item1=="Loss"?25:3,stackLimit=1,showBubble=false,stages=new()};
            for(int i=item.Item2;i<=item.Item3;i++)def.stages.Add(new ThoughtStage{label="[文案待定]",description="开发占位文本，正式文案待定。",baseMoodEffect=i});
            if(item.Item1=="Control")AccessTools.Field(typeof(ThoughtDef),"workerClass").SetValue(def,typeof(ThoughtWorker_SSCRomanceControl));
            DefDatabase<ThoughtDef>.Add(def);AccessTools.Field(typeof(SSCRomanceMoodDefOf),def.defName).SetValue(null,def);
        }
    }
    /// <summary>用生产数值阶段创建新事件实例，后续仍走原生准入和生产合并。</summary>
    private static Thought_MemorySSCRomance MoodMemory(int value,bool loss=false,SSCRomanceOldRelation relation=SSCRomanceOldRelation.Lover)
    {
        var kind=loss?SSCRomanceMoodEvent.RelationshipLoss:SSCRomanceMoodEvent.ForcedAcceptance;
        var def=loss?SSCRomanceMoodDefOf.SSC_Romance_Loss:SSCRomanceMoodDefOf.SSC_Romance_Experience;
        var result=(Thought_MemorySSCRomance)ThoughtMaker.MakeThought(def,value+(loss?5:6));
        result.Data=new(){Kind=kind,Value=value,Relation=relation};result.durationTicksOverride=SSCRomanceMoodRules.Duration(kind,relation);return result;
    }
}
