// 仅补齐共用宠物工具的普通亲昵编译接口；该套件不以这些替身验证亲昵调度。
using System.Collections.Generic;
using Verse.AI;

namespace Verse
{
    public partial class Pawn
    {
        public bool IsFighting() => false;
        public bool Awake() => !Sleeping;
    }
}

namespace Verse.AI
{
    public static class ReachabilityImmediate
    {
        public static bool CanReachImmediate(Verse.Pawn pawn, Verse.LocalTargetInfo target, PathEndMode mode) => true;
    }
    public class QueuedJob { public Job job; }
    public partial class PawnJobTracker { public readonly List<QueuedJob> jobQueue = new(); }
    public partial class Job { public bool playerForced; }
}
