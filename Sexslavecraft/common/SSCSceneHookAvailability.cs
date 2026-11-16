using System;

namespace SexSlaveCraft
{
    /// <summary>1.7 启动时绑定实际 Harmony 检查。独立运行的业务模型没有启动安装器，使用原准入规则。</summary>
    internal static class SSCSceneHookAvailability
    {
        internal static Func<bool> SharedHooks;
        internal static Func<bool> DailyReservation;
        internal static Func<bool> OrderedJob;
        internal static Func<Type, bool> DriverHooks;

        internal static bool SharedReady => SharedHooks?.Invoke() != false;
        internal static bool OrderedReady => OrderedJob?.Invoke() != false;
        internal static bool CanStartDaily(bool ordered)
            => SharedReady && DailyReservation?.Invoke() != false && (!ordered || OrderedReady);
        internal static bool DriverReady(Type driver) => DriverHooks?.Invoke(driver) != false;
    }
}
