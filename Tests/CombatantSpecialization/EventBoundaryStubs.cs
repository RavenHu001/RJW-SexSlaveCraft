// 游戏事件模型只提供可被真实 Harmony 包装的边界，不实现奖励或去重。
using System;
using System.Runtime.CompilerServices;
using Verse;

namespace Verse
{
    public static class Log { public static void ErrorOnce(string text, int key) { } }
    public class DamageDef { public bool harmsHealth = true; }
    public struct DamageInfo
    {
        public Thing Instigator;
        public DamageDef Def;
        public DamageInfo(DamageDef def, Thing instigator) { Def = def; Instigator = instigator; }
    }
    public class ThingDef { }
    public struct IntVec3 { }
    public struct LocalTargetInfo { public Thing Thing; }
    public partial class Pawn
    {
        public bool Dead, Downed, PreventDeath;
        public string Kind, Faction;
        public Pawn BoundMaster, AssignedTrainer;
        public float ActualBodySize = 1f;
        public Func<float> ReadBodySize;
        public float BodySize => ReadBodySize != null ? ReadBodySize() : ActualBodySize;
        public Action OnKill;
        public AI.Pawn_JobTracker jobs = new AI.Pawn_JobTracker();
        public AI.Job CurJob => jobs.curJob;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Kill(DamageInfo? damage = null, Hediff cause = null)
        {
            OnKill?.Invoke();
            if (!PreventDeath) Dead = true;
        }
    }
    public class Explosion
    {
        public ThingDef projectile, weapon;
        public Action OnDamage;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AffectCell(IntVec3 cell) => OnDamage?.Invoke();
    }
}
namespace Verse.AI
{
    public class JobDriver { }
    public class Job { public LocalTargetInfo targetA; }
    public class Pawn_JobTracker { public JobDriver curDriver; public Job curJob; }
}
namespace RimWorld
{
    public static class DamageDefOf
    {
        public static readonly DamageDef ExecutionCut = new DamageDef();
        public static readonly DamageDef Bullet = new DamageDef();
        public static readonly DamageDef Flame = new DamageDef();
    }
    public class JobDriver_Slaughter : Verse.AI.JobDriver { }
    public class Fire
    {
        public Action OnDamage;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DoFireDamage(Thing target) => OnDamage?.Invoke();
    }
}
namespace SexSlaveCraft
{
    public static class SSCBondUtility
    {
        public static Pawn GetBoundMaster(Pawn p) => p?.BoundMaster;
    }
    public static class SSCMasterBondUtility
    {
        public static bool IsRedirectingDamage;
    }
}
