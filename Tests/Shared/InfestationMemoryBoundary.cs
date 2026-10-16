// 引擎边界：依据本机 RimWorld 1.6 记忆 API 模拟分组、期限、容量和可控回调，不实现侵染业务。
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Verse
{
    [Flags] public enum DevelopmentalStage { None=0, Child=2, Adult=4 }
    public static class DevelopmentalStageExtensions
    { public static bool Has(this DevelopmentalStage filter, DevelopmentalStage stage) => (filter & stage) != 0; }
    public sealed class Precept { }
    public partial class Pawn { public DevelopmentalStage DevelopmentalStage=DevelopmentalStage.Adult; }
}
namespace RimWorld
{
    public sealed class ThoughtStage
    { public string label,description,labelSocial; public float baseMoodEffect,baseOpinionOffset; }
    public sealed class ThoughtDef : Def
    {
        public Type thoughtClass=typeof(Thought_Memory);
        public Type ThoughtClass=>thoughtClass;
        public bool IsMemory=>typeof(Thought_Memory).IsAssignableFrom(thoughtClass);
        public List<ThoughtStage> stages=new();
        public int stackLimit=1,stackLimitForSameOtherPawn=-1;
        public float durationDays,lerpOpinionToZeroAfterDurationPct=.7f;
        public bool stagesStack,lerpMoodToZero,Blocked,BlockSource;
        public DevelopmentalStage socialTargetDevelopmentalStageFilter=DevelopmentalStage.Child|DevelopmentalStage.Adult;
    }
    public sealed class Need_Mood
    {
        public ThoughtHandler thoughts;
        public Need_Mood(Pawn owner=null) { thoughts=new ThoughtHandler { memories=new MemoryThoughtHandler(owner) }; }
    }
    public sealed class ThoughtHandler { public MemoryThoughtHandler memories; }
    public class Thought_Memory : IExposable
    {
        public ThoughtDef def;
        public Pawn pawn,otherPawn;
        public int age,moodOffset,durationTicksOverride=-1;
        public float moodPowerFactor=1;
        public bool permanent;
        public Precept sourcePrecept;
        private int stage;
        public int CurStageIndex=>stage;
        public int DurationTicks=>durationTicksOverride>=0?durationTicksOverride:(int)(def.durationDays*60000);
        public ThoughtStage CurStage=>def.stages[stage];
        public string LabelCap=>CurStage.label;
        public void SetForcedStage(int value)=>stage=value;
        public virtual void Init() { }
        public float MoodOffset()=>CurStage.baseMoodEffect*moodPowerFactor+moodOffset;
        public virtual bool GroupsWith(Thought_Memory other)=>def==other.def &&
            (stage==other.stage || def.stagesStack) && (otherPawn==other.otherPawn || LabelCap==other.LabelCap);
        public virtual bool TryMergeWithExistingMemory()
        {
            var grouped=pawn.needs.mood.thoughts.memories.Memories.Where(m=>m.GroupsWith(this)).ToList();
            if(grouped.Count<def.stackLimit)return false;
            grouped.OrderByDescending(m=>m.age).First().age=0;return true;
        }
        // 保存实例字段而非对象本身；加载测试必须重建新的记忆，不能只复用原引用。
        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref stage,"stageIndex",0);Scribe_Values.Look(ref age,"age",0);
            Scribe_Values.Look(ref moodPowerFactor,"moodPowerFactor",1f);Scribe_Values.Look(ref moodOffset,"moodOffset",0);
            Scribe_Values.Look(ref otherPawn,"otherPawn",(Pawn)null);Scribe_Values.Look(ref permanent,"permanent",false);
            Scribe_Values.Look(ref durationTicksOverride,"durationTicksOverride",-1);
        }
    }
    public class Thought_MemorySocial : Thought_Memory
    {
        public float opinionOffset;
        public override void Init()=>opinionOffset=CurStage.baseOpinionOffset;
        public override bool GroupsWith(Thought_Memory other)=>other is Thought_MemorySocial &&
            base.GroupsWith(other) && otherPawn==other.otherPawn;
        public override bool TryMergeWithExistingMemory()=>false;
        public float OpinionOffset()
        {
            float fraction=(float)age/DurationTicks;
            return opinionOffset*Math.Clamp((1-fraction)/(1-def.lerpOpinionToZeroAfterDurationPct),0,1);
        }
        public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref opinionOffset,"opinionOffset",0f);}
    }
    public static class ThoughtMaker
    {
        public static Action<Thought_Memory> OnMade;
        public static Thought_Memory MakeThought(ThoughtDef def,int forcedStage=0)
        {
            var memory=(Thought_Memory)Activator.CreateInstance(def.ThoughtClass);
            memory.def=def;memory.SetForcedStage(forcedStage);memory.Init();OnMade?.Invoke(memory);return memory;
        }
    }
    public static class ThoughtUtility
    {
        public static Action OnCheck;
        public static bool CanGetThought(Pawn pawn,ThoughtDef def){OnCheck?.Invoke();return !def.Blocked;}
    }
    public sealed class MemoryThoughtHandler
    {
        public readonly List<Thought_Memory> Memories=new();
        public readonly Pawn owner;
        public static Action<Thought_Memory> OnRemoved,OnGained;
        public MemoryThoughtHandler(Pawn pawn=null){owner=pawn;}
        public void RemoveMemory(Thought_Memory memory){Memories.Remove(memory);OnRemoved?.Invoke(memory);}
        public void TryGainMemory(Thought_Memory memory,Pawn otherPawn=null)
        {
            if(!ThoughtUtility.CanGetThought(owner,memory.def))return;
            if(memory is Thought_MemorySocial && (otherPawn==null || memory.def.BlockSource ||
                !memory.def.socialTargetDevelopmentalStageFilter.Has(otherPawn.DevelopmentalStage)))return;
            memory.pawn=owner;memory.otherPawn=otherPawn;
            if(!memory.TryMergeWithExistingMemory())Memories.Add(memory);
            // 本机原版先限制同组数量，再限制同 Def 总数，均移除年龄最大的实例。
            if(memory.def.stackLimitForSameOtherPawn>=0)
                while(Memories.Count(m=>m.GroupsWith(memory))>memory.def.stackLimitForSameOtherPawn)
                    RemoveMemory(Memories.Where(m=>m.GroupsWith(memory)).OrderByDescending(m=>m.age).First());
            if(memory.def.stackLimit>=0)
                while(Memories.Count(m=>m.def==memory.def)>memory.def.stackLimit)
                    RemoveMemory(Memories.Where(m=>m.def==memory.def).OrderByDescending(m=>m.age).First());
            OnGained?.Invoke(memory);
        }
        public void Advance(int ticks)
        {
            foreach(var memory in Memories.ToArray()){memory.age+=ticks;if(!memory.permanent&&memory.age>=memory.DurationTicks)RemoveMemory(memory);}
        }
    }
}
