using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SexSlaveCraft
{
    /// <summary>识别可选入口的缺席与签名变化，避免静默漏接，也不因缺失可选模组阻断加载。</summary>
    internal static class SSCRestrictionCompatibilityDiagnostics
    {
        // Prepare 与 TargetMethods 都会发现同一入口，且 Lovin 每次建步骤都会核对回调。
        // 以稳定入口名称去重，只警告一次；不缓存成功的方法或最终许可。
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>寻找已核对的实例方法；类型缺席安静跳过，类型存在但继承关系或完整签名变化则警告一次。</summary>
        public static MethodInfo OptionalMethod(string typeName, Type expectedBase, string methodName,
            Type returnType, params Type[] parameterTypes)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null) return null;
            string key = typeName + "." + methodName;
            MethodInfo method = null;
            if (expectedBase.IsAssignableFrom(type))
            {
                try { method = AccessTools.DeclaredMethod(type, methodName, parameterTypes); }
                catch (AmbiguousMatchException) { /* 同参数不同泛型版本也视为不匹配，不能猜选补丁目标。 */ }
            }
            if (method != null && !method.IsStatic && !method.IsAbstract && !method.ContainsGenericParameters &&
                method.ReturnType == returnType) return method;
            WarnOnce(key, "已检测到兼容类型，但已核对的方法签名不匹配；该入口未安装限制补丁。请检查模组版本。入口=" + key);
            return null;
        }

        /// <summary>每个兼容入口仅提示一次，避免能力查询或重复建立任务时持续刷屏。</summary>
        public static void WarnOnce(string key, string explanation)
        {
            if (!Reported.Add(key)) return;
            SSCLog.WarningImportant("[SSC Restrictions] " + explanation);
        }
    }
}
