using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅在 SSC 仪式阶段边界释放 UAP 遗留的位置锁，避免旧任务的延迟清理误停新动画。</summary>
    internal static class UapRitualCompatibilityUtility
    {
        private static readonly MethodInfo UnlockParticipantsMethod = FindUnlockParticipantsMethod();

        /// <summary>释放本场仪式双方的位置锁；不停止动画、不结束任务，缺少 UAP 或接口不兼容时安全跳过。</summary>
        internal static void ReleasePositionLocks(Pawn initiator, Pawn slave)
        {
            if (UnlockParticipantsMethod == null || (initiator == null && slave == null)) return;

            var participants = new List<Pawn>(2);
            if (initiator != null) participants.Add(initiator);
            if (slave != null && slave != initiator) participants.Add(slave);

            try
            {
                UnlockParticipantsMethod.Invoke(null, new object[] { participants });
            }
            catch (Exception ex)
            {
                // 外部模组接口异常只能使兼容清理失效，不能中断仪式的启动、计时和结算。
                Exception cause = ex is TargetInvocationException invocation ? invocation.InnerException ?? ex : ex;
                SSCLog.WarningImportant($"[SSC Ritual] UAP position-lock release failed: {cause}");
            }
        }

        /// <summary>从已加载程序集查找 UAP 的群体解锁接口并缓存；不引入 UAP 程序集的硬依赖。</summary>
        private static MethodInfo FindUnlockParticipantsMethod()
        {
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type manager = assembly.GetType("UAP_Animations.UAP_AnimationPositionLockManager", false);
                    if (manager == null) continue;

                    MethodInfo method = manager.GetMethod(
                        "UnlockParticipants",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(List<Pawn>) },
                        null);
                    return method?.ReturnType == typeof(void) ? method : null;
                }
            }
            catch (Exception ex)
            {
                SSCLog.WarningImportant($"[SSC Ritual] UAP position-lock API lookup failed: {ex}");
            }

            return null;
        }
    }
}
