// AI 辅助生成：短鞭的临时视觉姿态；不修改冷却、命中、伤害或施药结算。
using System.Runtime.CompilerServices;
using Verse;

namespace SexSlaveCraft
{
    internal enum SSCPnaWhipPose { Idle, Attack, Recovery }

    internal static class SSCPnaWhipPoseUtility
    {
        internal const string WeaponDefName = "SSC_Weapon_PnaShortWhip";
        internal const int AttackTicks = 6;
        internal const int RecoveryTicks = 12;
        internal const int LifetimeTicks = AttackTicks + RecoveryTicks;

        private sealed class Swing
        {
            internal System.WeakReference<Pawn> Owner;
            internal System.WeakReference<Map> Map;
            internal System.WeakReference<TickManager> Clock;
            internal int StartedAt;
            internal bool Hit;
        }
        // 状态不写入存档，弱键和弱引用不使掉落武器、角色或旧地图常驻内存。
        private static readonly ConditionalWeakTable<Thing, Swing> Swings = new ConditionalWeakTable<Thing, Swing>();

        internal static bool IsWhip(Thing weapon) => weapon?.def?.defName == WeaponDefName;

        internal static void Clear(Thing weapon)
        {
            if (weapon != null) Swings.Remove(weapon);
        }

        /// <summary>由持鞭者的原版近战出手后缀调用；不要求本次动作由鞭束工具提供。</summary>
        internal static void Begin(Thing weapon, Pawn pawn, bool hit = false)
        {
            Clear(weapon);
            if (!IsWhip(weapon) || weapon.Destroyed || Scribe.mode != LoadSaveMode.Inactive ||
                pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.Map == null ||
                pawn.equipment?.Primary != weapon) return;
            TickManager clock = Find.TickManager;
            if (clock == null) return;
            Swings.Add(weapon, new Swing {
                Owner = new System.WeakReference<Pawn>(pawn), Map = new System.WeakReference<Map>(pawn.Map),
                Clock = new System.WeakReference<TickManager>(clock), StartedAt = clock.TicksGame, Hit = hit
            });
        }

        /// <summary>只查询游戏 tick，不按绘制帧推进；暂停保持，加速遵从游戏时间。</summary>
        internal static SSCPnaWhipPose GetPose(Thing weapon)
        {
            return TryGetSwing(weapon, out _, out int elapsed)
                ? (elapsed < AttackTicks ? SSCPnaWhipPose.Attack : SSCPnaWhipPose.Recovery)
                : SSCPnaWhipPose.Idle;
        }

        /// <summary>命中残影复用同一次姿态的时钟和失效检查；不另建逐帧计时器。</summary>
        internal static int GetHitAge(Thing weapon)
            => TryGetSwing(weapon, out Swing swing, out int elapsed) && swing.Hit &&
                swing.Map.TryGetTarget(out Map map) && map == Find.CurrentMap ? elapsed : -1;

        private static bool TryGetSwing(Thing weapon, out Swing swing, out int age)
        {
            swing = null;
            age = 0;
            if (weapon == null || !Swings.TryGetValue(weapon, out swing)) return false;
            if (!IsWhip(weapon) || weapon.Destroyed || Scribe.mode != LoadSaveMode.Inactive ||
                !swing.Owner.TryGetTarget(out Pawn pawn) || pawn.Dead || pawn.Destroyed || !pawn.Spawned ||
                pawn.equipment?.Primary != weapon || !swing.Map.TryGetTarget(out Map map) || pawn.Map != map ||
                !swing.Clock.TryGetTarget(out TickManager clock) || clock != Find.TickManager)
            { Clear(weapon); return false; }
            // 使用 long 相减，回退的时钟或跨越 int 边界不能让旧出手重新出现。
            long elapsed = (long)clock.TicksGame - swing.StartedAt;
            if (elapsed < 0 || elapsed >= LifetimeTicks)
            { Clear(weapon); return false; }
            age = (int)elapsed;
            return true;
        }
    }
}
