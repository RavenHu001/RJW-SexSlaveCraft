// 只提供 RJW 器官、缓存与读取边界；聚合、饱和和 PNA 写入均执行生产源码。
using System;
using System.Collections.Generic;
using Verse;

namespace Verse
{
    public partial class Pawn
    {
        public List<Hediff> FluidOrgans = new();
        public bool ThrowFluidList;
    }
}
namespace rjw
{
    public class HediffDef_SexPart : HediffDef { public bool produceFluidOnOrgasm = true; }
    public interface ISexPartHediff { HediffDef_SexPart Def { get; } HediffComp_SexPart Comp { get; } }
    public class HediffComp_SexPart
    {
        public object Fluid = new();
        public float Amount;
        public int Reads;
        public bool ThrowRead;
        public float FluidAmount { get { Reads++; if (ThrowRead) throw new InvalidOperationException(); return Amount; } }
    }
    public class FluidOrgan : Hediff, ISexPartHediff
    {
        public HediffDef_SexPart Def => (HediffDef_SexPart)def;
        public HediffComp_SexPart Comp { get; set; } = new();
    }
    public static class FluidPartExtensions
    {
        public static HediffComp_SexPart GetPartComp(this ISexPartHediff part) => part.Comp;
        public static List<Hediff> GetGenitalsList(this Pawn pawn)
        { if (pawn.ThrowFluidList) throw new InvalidOperationException(); return pawn.FluidOrgans; }
    }
}

internal static partial class Program
{
    private static rjw.FluidOrgan AddFluidOrgan(Pawn pawn, float amount)
    {
        var organ = new rjw.FluidOrgan { pawn = pawn, Part = new BodyPartRecord(),
            def = new rjw.HediffDef_SexPart { defName = "TestFluidOrgan", maxSeverity = 2f } };
        organ.Comp.Amount = amount;
        pawn.FluidOrgans.Add(organ); pawn.health.hediffSet.hediffs.Add(organ);
        return organ;
    }
}
