using System;
using Verse;

// 只提供需求/设置边界；完成快照、方向选择和真实 PNA 输入均链接生产代码。
namespace RimWorld
{
    public partial class Pawn_NeedsTracker
    {
        public SexSlaveCraft.Need_Corruption CorruptionNeed = new SexSlaveCraft.Need_Corruption();
        public T TryGetNeed<T>() where T : class => CorruptionNeed as T ?? chemicalNeed as T;
    }
}
namespace SexSlaveCraft
{
    public class Need_Corruption : RimWorld.Need
    {
        public float HighestCorruptionLevel;
        public void SetCorruption(float value)
        { CurLevel=Math.Clamp(value,0,MaxLevel);HighestCorruptionLevel=Math.Max(HighestCorruptionLevel,CurLevel); }
    }
    public static class SSCMod { public static BatchSettings settings = new BatchSettings(); }
    public class BatchSettings { public bool useOldScoring; }
    public static class TrainingOutcomeUtility { public static float GetChainCap(Pawn pawn)=>pawn.InfestationCap; }
}
namespace Verse { public partial class Pawn { public float InfestationCap=.12f; } }

// 身体分量的完整生产实现由 TrainingExperience 与 InteractionProtection 执行；本宿主专测 PNA。
namespace SexSlaveCraft
{
    public sealed class SSCOrdinarySexPartsSnapshot { }
    public static class SSCDesireInfestationBodyUtility
    {
        public sealed class Plan { public void Apply(Func<bool> current) { } }
        public static Plan Capture(Pawn source,Pawn target,SSCOrdinarySexPartsSnapshot parts)=>null;
    }
}
