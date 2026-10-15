using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using rjw.Modules.Interactions;

// 仅模拟本机 RJW 已核对的字段/属性及解析边界；不重写 RJW 的部位搜索和评分算法。
namespace Verse
{
    public sealed class Pawn { }
    public static class DefDatabase<T> where T : class
    {
        public static readonly Dictionary<string, T> Values = new();
        public static T GetNamedSilentFail(string name) => Values.GetValueOrDefault(name);
    }
}
namespace RimWorld
{
    public sealed class InteractionDef
    {
        public string defName;
        public rjw.xxx.rjwSextype Type;
        public bool Reverse;
    }
}
namespace rjw.Modules.Interactions
{
    public enum SexInteractionTag { Reverse }
    public sealed class SexInteraction
    {
        public InteractionDef Def;
        public SexInteraction(InteractionDef def) { Def = def; }
        public rjw.xxx.rjwSextype Sextype => Def.Type;
        public bool HasInteractionTag(SexInteractionTag tag) => Def.Reverse;
    }
    public interface ILewdablePart { Pawn Owner { get; } }
    public sealed class Part : ILewdablePart { public Pawn Owner { get; set; } }
    public sealed class SexInteractionResolved
    {
        public Pawn Initiator, Recipient;
        public SexInteraction Interaction;
        public List<ILewdablePart> InitiatorParts, RecipientParts;
        public List<Tuple<ILewdablePart, ILewdablePart>> Penetrations;
    }
    public static class SexInteractionHelper
    {
        public static Func<rjw.SexProps, SexInteractionResolved> Resolver;
        public static int Calls;
        public static SexInteractionResolved ResolveInteraction(rjw.SexProps props)
        { Calls++; return Resolver(props); }
    }
}
namespace rjw
{
    #pragma warning disable CS8981 // 保留 RJW 的真实类型名。
    public static class xxx
    {
        public enum rjwSextype { None, Vaginal, Anal, Oral, Fellatio, Boobjob, Handjob, Footjob, Fingering,
            MutualMasturbation, Fisting, Rimming, Sixtynine }
    }
    public sealed class SexProps
    {
        public Pawn pawn, partner;
        public InteractionDef dictionaryKey;
        public bool isReceiver, isRevese, isRape, isWhoring, usedCondom;
        public int orgasms;
        public Pawn initiator => isReceiver ? partner : pawn;
        public Pawn recipient => isReceiver ? pawn : partner;
        private xxx.rjwSextype? _sexType;
        private SexInteraction _interaction;
        public SexInteractionResolved _resolved;
        public SexInteraction interaction
        {
            get => _interaction ??= new SexInteraction(dictionaryKey);
            set { _interaction = value; if (value != null) dictionaryKey = value.Def; }
        }
        public SexInteractionResolved resolved
        {
            get => _resolved ??= SexInteractionHelper.ResolveInteraction(this);
            set { _resolved = value; if (value != null) { _interaction = value.Interaction; dictionaryKey = value.Interaction.Def; } }
        }
        public xxx.rjwSextype sexType => _sexType ??= interaction.Sextype;
    }
}
namespace SexSlaveCraft
{
    public enum TrainingActType { Auto, Vaginal, Anal, Oral, Boobjob, Handjob, Footjob, Fingering,
        MutualMasturbation, Fisting, Rimming, Sixtynine }
}
