// 引擎/RJW 边界：模拟身体、健康通知和已选部位，不实现 RJW 部位选择算法。
using System;
using System.Collections.Generic;
using System.Linq;
namespace Verse
{
    public class BodyPartDef : Def { }
    public class BodyDef : Def { public List<BodyPartRecord> AllParts = new(); }
    public class BodyPartRecord { public BodyPartDef def; public bool Missing; }
    public class ResearchProjectDef : Def { public bool IsFinished = true; }
    public class HediffStage { public float minSeverity; }
    public class HediffDef : Def { public float maxSeverity = 1f, initialSeverity = .01f; public List<HediffStage> stages = new(); }
    public class Hediff
    {
        public Pawn pawn;
        public HediffDef def;
        public BodyPartRecord Part;
        public string Kind;
        public float Length, Girth;
        private float severity;
        public static Action<Hediff> OnSeverity;
        public float Severity { get=>severity; set { severity=Math.Clamp(value,0,def?.maxSeverity??1f); OnSeverity?.Invoke(this); } }
    }
    public class HediffSet
    {
        public List<Hediff> hediffs = new();
        public bool PartIsMissing(BodyPartRecord part)=>part.Missing;
        public Hediff GetFirstHediffOfDef(HediffDef def)=>hediffs.FirstOrDefault(h=>h.def==def);
    }
    public class InfestationHealth
    {
        public HediffSet hediffSet = new();
        public static Action<Hediff> OnAdded;
        public void AddHediff(Hediff hediff,BodyPartRecord part,object ignored)
        { hediff.Part=part;hediffSet.hediffs.Add(hediff);OnAdded?.Invoke(hediff); }
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def,Pawn pawn,BodyPartRecord part)=>
            new Hediff {def=def,pawn=pawn,Part=part,Severity=def.initialSeverity};
    }
    public static class Log { public static List<string> Warnings=new(); public static void Warning(string text)=>Warnings.Add(text); }
    public static class Scribe_BodyParts { public static void Look(ref BodyPartRecord part,string key)=>Scribe_References.Look(ref part,key); }
    public static class Scribe_Defs { public static void Look<T>(ref T def,string key) where T:class=>Scribe_References.Look(ref def,key); }
    public partial class Pawn
    {
        public SkillTracker skills=new();
        public RelationTracker relations=new();
        public GuestTracker guest=new();
        public BodyPartRecord Breasts;
    }
    public class SkillRecord { public int Level=20; }
    public class SkillTracker { public SkillRecord Social=new(); public SkillRecord GetSkill(object def)=>Social; }
    public class RelationTracker { public int Opinion; public int OpinionOf(Pawn pawn)=>Opinion; }
    public class GuestTracker { public bool IsSlave; }
    public static partial class Rand
    {
        public static int DrawState;
        private static readonly Stack<int> States=new();
        public static void PushState()=>States.Push(DrawState);
        public static void PopState()=>DrawState=States.Pop();
    }
}
namespace RimWorld { public class InteractionDef : Verse.Def { } public static class SkillDefOf {public static object Social=new();} }
namespace rjw
{
    #pragma warning disable CS8981 // 使用依赖真实类型名。
    public static partial class xxx
    {
        public enum rjwSextype { None,Handjob,MutualMasturbation,Footjob,Vaginal,Scissoring,Fingering,Fisting,Sixtynine,Anal,Rimming,Oral,Boobjob }
    }
    public partial class SexProps
    {
        public RimWorld.InteractionDef dictionaryKey;
        public Modules.Interactions.SexInteractionResolved _resolved;
        public static int ResolveCalls;
        public Modules.Interactions.SexInteractionResolved resolved { get {ResolveCalls++;throw new InvalidOperationException("Must not resolve");} }
    }
    public static class Genital_Helper
    {
        public static Verse.BodyPartRecord get_breastsBPR(Verse.Pawn pawn)=>pawn.Breasts;
        public static List<Verse.Hediff> get_AllPartsHediffList(Verse.Pawn pawn)=>pawn.health.hediffSet.hediffs;
        public static bool is_penis(Verse.Hediff h)=>h.Kind=="Penis";
        public static bool is_vagina(Verse.Hediff h)=>h.Kind=="Vagina";
    }
}
namespace rjw.Modules.Interactions
{
    public enum LewdablePartFamily { Undefined,Vagina,Penis,Breasts,Udders,Anus,FemaleOvipositor,MaleOvipositor,Hand,Foot,Tail,Mouth,Beak,Tongue }
    public interface ILewdablePart { Verse.Pawn Owner {get;} Verse.BodyPartRecord BodyPart {get;} LewdablePartFamily Family {get;} }
    public class VanillaLewdablePart : ILewdablePart
    { public Verse.Pawn Owner {get;set;} public Verse.BodyPartRecord BodyPart {get;set;} public LewdablePartFamily Family {get;set;} }
    public interface ISexPart { Verse.Hediff AsHediff {get;} }
    public class TestSexPart : ISexPart { public Verse.Hediff AsHediff {get;set;} }
    public class RJWLewdablePart : ILewdablePart
    {
        public ISexPart SexPart;
        public Verse.Pawn Owner=>SexPart.AsHediff.pawn;
        public Verse.BodyPartRecord BodyPart=>SexPart.AsHediff.Part;
        public LewdablePartFamily Family {get;set;}
    }
    public class SexInteraction { public RimWorld.InteractionDef Def; }
    public class SexInteractionResolved
    {
        public Verse.Pawn Initiator,Recipient;
        public SexInteraction Interaction;
        public List<ILewdablePart> InitiatorParts=new(),RecipientParts=new();
        public List<Tuple<ILewdablePart,ILewdablePart>> Penetrations=new();
    }
}
