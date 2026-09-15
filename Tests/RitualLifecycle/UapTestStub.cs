#if SSC_TEST_WITH_UAP
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace UAP_Animations
{
    // 外部 API 替身：保留旧 Job 锁和误停行为；SSC 的反射查找、解锁及阶段调用均执行生产代码。
    internal static class UAP_AnimationPositionLockManager
    {
        private static readonly Dictionary<Pawn, Job> Locks = new Dictionary<Pawn, Job>();
        internal static readonly HashSet<Pawn> Animating = new HashSet<Pawn>();
        internal static bool ThrowOnUnlock;

        /// <summary>清除外部模组替身的全部状态，使每个用例独立运行。</summary>
        internal static void Reset()
        {
            Locks.Clear();
            Animating.Clear();
            ThrowOnUnlock = false;
        }

        /// <summary>模拟 UAP 启动动画时记录参与者当前任务并建立位置锁。</summary>
        internal static void LockParticipants(params Pawn[] participants)
        {
            foreach (Pawn pawn in participants) Locks[pawn] = pawn.CurJob;
        }

        /// <summary>提供与真实 UAP 一致的解锁签名；只删除指定参与者的锁，不改动动画或任务。</summary>
        public static void UnlockParticipants(List<Pawn> participants)
        {
            if (ThrowOnUnlock) throw new InvalidOperationException("simulated UAP API failure");
            foreach (Pawn pawn in participants) Locks.Remove(pawn);
        }

        /// <summary>查询指定参与者的位置锁是否仍存在。</summary>
        internal static bool IsLocked(Pawn pawn) => Locks.ContainsKey(pawn);

        /// <summary>模拟已确认的误停条件：旧锁发现任务更换后停止该组当前动画，但保留新任务。</summary>
        internal static void CheckStaleGroup(params Pawn[] participants)
        {
            if (!participants.Any(pawn => Locks.TryGetValue(pawn, out Job owner) && owner != pawn.CurJob)) return;
            UnlockParticipants(participants.ToList());
            foreach (Pawn pawn in participants) Animating.Remove(pawn);
        }
    }
}
#endif
