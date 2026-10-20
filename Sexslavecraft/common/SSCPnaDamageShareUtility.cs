// AI 辅助生成：九把现役 PNA 武器共用的分摊策略与单次伤害上下文。
using System;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCPnaDamageShareUtility
    {
        // 不借用覆盖整个分摊循环的 redirectDepth。每个同步 TakeDamage 都有自己的帧，
        // 所以同一目标、同种伤害的嵌套真实攻击也不会继承外层“仅物理”的标记。
        [ThreadStatic] private static PendingHit pending;
        [ThreadStatic] private static HitFrame current;

        internal static bool IsPnaDamage(DamageDef damage)
        {
            Type worker = damage?.workerClass;
            return worker != null && (typeof(DamageWorker_SSCPnaCoated).IsAssignableFrom(worker) ||
                typeof(DamageWorker_SSCPnaSpacer).IsAssignableFrom(worker));
        }

        // 太空主攻击本来就对 SSC 主人无伤；不创建一次虚假的性奴命中。
        // 只检查处理器及主人身份，不依赖武器名，也不影响普通枪托／柄击或涂覆物理伤害。
        internal static bool CanRedirect(Pawn victim, DamageDef damage) =>
            !SSCIdentityUtility.IsMaster(victim) || damage?.workerClass == null ||
            !typeof(DamageWorker_SSCPnaSpacer).IsAssignableFrom(damage.workerClass);

        internal static bool IsRedirectedHit(Thing target) =>
            current != null && current.Redirected && ReferenceEquals(current.Target, target);

        // 只给这一次即将执行的 PNA 转移伤害发放标记；普通伤害直接走原入口。
        // 不复制药液、不修改 DamageDef、穿甲或忽略护甲标记，仍完整执行受击者的 TakeDamage。
        internal static DamageWorker.DamageResult ApplyRedirected(Pawn target, DamageInfo damage)
        {
            if (!IsPnaDamage(damage.Def)) return target.TakeDamage(damage);
            var token = new PendingHit(target, damage.Def, pending);
            pending = token;
            try { return target.TakeDamage(damage); }
            finally { pending = token.Previous; }
        }

        // 由 TakeDamage Prefix 建帧，Finalizer 释放。空闲时不分配对象；跟踪期间的
        // 独立嵌套调用也建一个非分摊帧，暂时遮蔽外层标记。令牌只能被目标调用认领一次。
        internal static IDisposable EnterTakeDamage(Thing target, DamageInfo damage)
        {
            if (pending == null && current == null) return null;
            bool redirected = pending != null && !pending.Claimed &&
                ReferenceEquals(pending.Target, target) && pending.Damage == damage.Def;
            if (redirected) pending.Claimed = true;
            var frame = new HitFrame(target, redirected, current);
            current = frame;
            return frame;
        }

        private sealed class PendingHit
        {
            internal readonly Pawn Target;
            internal readonly DamageDef Damage;
            internal readonly PendingHit Previous;
            internal bool Claimed;
            internal PendingHit(Pawn target, DamageDef damage, PendingHit previous)
            { Target = target; Damage = damage; Previous = previous; }
        }

        private sealed class HitFrame : IDisposable
        {
            internal readonly Thing Target;
            internal readonly bool Redirected;
            private readonly HitFrame previous;
            private bool disposed;
            internal HitFrame(Thing target, bool redirected, HitFrame previous)
            { Target = target; Redirected = redirected; this.previous = previous; }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                current = previous;
            }
        }
    }
}
