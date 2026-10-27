// AI 辅助生成：防止归档旧 Def／源码重新进入 1.7，同时保护现役九武器及其公共身体状态。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SexSlaveCraft;

internal static partial class Program
{
    private static void RunLegacyLauncherRemovalTests()
    {
        Run("退役伤害类型不进入当前测试程序集", () =>
        {
            foreach (string type in new[] { "PNA_DWExtension", "DamageWorker_SSCPna", "DamageWorker_LowTier", "DamageWorker_HighTier", "SSCPnaWeaponResistancePolicy" })
                Assert(typeof(SSCPnaWeaponDoseUtility).Assembly.GetType("SexSlaveCraft." + type) == null, "类型已退役 " + type);
            Assert(typeof(SSCPnaWeaponDoseUtility).GetMethod("FromSeverity", BindingFlags.NonPublic | BindingFlags.Static) == null, "旧严重度入口已退役");
        });
    }
}
