using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>预检可选适配的整组目标。安装失败时逐一撤销本次尝试的补丁方法。</summary>
    internal static class SSCOptionalPatchGroup
    {
        /// <summary>锁定声明类型、完整参数列表及返回类型。实例、静态和 ref 参数会影响 Harmony 的参数注入。</summary>
        internal static MethodInfo RequireTarget(Type type, string name, Type result, bool isStatic, params Type[] parameters)
        {
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null || method.ReturnType != result || method.IsStatic != isStatic || method.IsAbstract)
                throw new MissingMethodException(type?.FullName ?? "Optional interface", name);
            return method;
        }

        /// <summary>依赖缺席时解析函数返回 null。接口失配抛给本组处理，调用方只在整组安装成功后启用功能。</summary>
        internal static bool Install(Harmony harmony, string name,
            Func<(MethodInfo Target, Type Hook, HarmonyPatchType Kind)[]> resolve)
        {
            var attempted = new List<(MethodInfo Target, MethodInfo Hook)>();
            try
            {
                // 先解析完整目标数组，再调用 Harmony。目标预检失败时不会留下半组已装补丁。
                var targets = resolve();
                if (targets == null)
                {
#if SSC_RW17
                    SSCPatchRegistration.Record(name, SSCPatchState.Skipped, stage: "Resolve");
#endif
                    return false;
                }
                foreach (var (target, type, kind) in targets)
                {
                    MethodInfo hook = AccessTools.DeclaredMethod(type, kind.ToString())
                        ?? throw new MissingMethodException(type.FullName, kind.ToString());
                    // 在 Patch 前记录本次尝试，清理时也能处理注册中途抛异常的目标。
                    attempted.Add((target, hook));
                    var patch = new HarmonyMethod(hook);
                    harmony.Patch(target, prefix: kind == HarmonyPatchType.Prefix ? patch : null,
                        postfix: kind == HarmonyPatchType.Postfix ? patch : null,
                        transpiler: kind == HarmonyPatchType.Transpiler ? patch : null);
                }
#if SSC_RW17
                SSCPatchRegistration.Record(name, SSCPatchState.Ready);
#endif
                return true;
            }
            catch (Exception error)
            {
                string cleanupFailure = null;
                // 逆序撤回具体补丁方法。各组共用同一 owner，按 owner 撤回会移除其他正常功能。
                for (int i = attempted.Count - 1; i >= 0; i--)
                {
                    var (target, hook) = attempted[i];
                    try { harmony.Unpatch(target, hook); }
                    catch (Exception cleanup) { cleanupFailure = cleanup.GetBaseException().Message; }
                }
                // 清理失败也返回未就绪，让调用方停用本组功能，并保留实际注册错误供排查。
                string detail = cleanupFailure == null ? "" : " Cleanup failed: " + cleanupFailure;
#if SSC_RW17
                // 没有尝试过 Patch 时属于接口解析失败。有记录时报告最近尝试的目标，完整异常仍写入日志。
                MethodInfo failedTarget = attempted.Count == 0 ? null : attempted[attempted.Count - 1].Target;
                SSCPatchRegistration.Record(name, SSCPatchState.Failed,
                    failedTarget == null ? null : failedTarget.DeclaringType?.FullName + "." + failedTarget.Name,
                    cleanupFailure != null ? "Install / rollback" : attempted.Count == 0 ? "Resolve" : "Install",
                    error.GetBaseException().GetType().Name);
#endif
                Log.Warning($"[SSC {name}] Optional patch group skipped: {error.GetBaseException().Message}.{detail}");
                return false;
            }
        }
    }
}
