using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>区分尚未安装、明确跳过、正常启用、沿用原行为和安装失败。成功登记 Transpiler 仍可能回退原指令。</summary>
    internal enum SSCPatchState { Waiting, Ready, Skipped, Fallback, Failed }

    /// <summary>启动过程写入的结果。诊断窗口只读取副本，不触发安装或反射解析。</summary>
    internal static class SSCPatchRegistration
    {
        internal const string Owner = "SexSlaveCraft.training.patch";
        internal const string DailyTraining = "Daily training lifecycle";
        // 每组保留最近一次结果。窗口刷新或复制时读取整个副本，避免在枚举时遇到延迟安装写入。
        private static readonly Dictionary<string, (SSCPatchState State, string Target, string Stage, string Detail)> results = new();

        internal static void Record(string group, SSCPatchState state, string target = null, string stage = null, string detail = null)
        {
            lock (results) results[group] = (state, target, stage, detail);
        }

        internal static KeyValuePair<string, (SSCPatchState State, string Target, string Stage, string Detail)>[] Snapshot()
        {
            lock (results) return results.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        }

        /// <summary>使用原 Harmony owner 和程序集类型顺序安装。故障边界落在补丁类或实际驱动，最后核对日常调教的整套必要钩子。</summary>
        internal static bool InstallAnnotated(Harmony harmony, Assembly assembly)
        {
            var lifecycle = new[]
            {
                typeof(Patch_JobDriver_Sex_ProtectChainOfSexSlave), typeof(SSCRestrictionSceneSaveHook)
            };
            bool complete = true;
            // 沿用 Harmony 的程序集扫描顺序。共享目标、优先级相同的补丁仍按原顺序登记。
            foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (!type.IsDefined(typeof(HarmonyAttribute), false)) continue;
                if (type == typeof(SSCRestrictionReservationHook))
                {
                    // 派生驱动可能来自其他模组。逐个检查自己的预约方法，让一个驱动失败时其余仍可注册。
                    Install(DerivedReservations, () =>
                    {
                        bool allInstalled = true;
                        foreach (Type driver in GenTypes.AllTypes)
                            if (driver != typeof(JobDriver_Sex) && typeof(JobDriver_Sex).IsAssignableFrom(driver))
                                allInstalled &= InstallReservation(harmony, driver);
                        return allInstalled ? SSCPatchState.Ready : SSCPatchState.Fallback;
                    });
                    continue;
                }
                if (type == typeof(SSCRestrictionQuickiePreparationHook))
                {
                    // 准备阶段只覆盖已知会创建等待或移动任务的两个驱动，分别登记和回退。
                    InstallPreparation(harmony, typeof(JobDriver_SexQuick));
                    InstallPreparation(harmony, typeof(JobDriver_BestialityForFemale));
                    continue;
                }
                complete &= Install(type.Name, () =>
                {
                    var applied = harmony.CreateClassProcessor(type).Patch();
                    return applied?.Count > 0 ? SSCPatchState.Ready : SSCPatchState.Skipped;
                }, () => Unpatch(harmony, new[] { type }), stage: "Resolve / install");
            }
            // 装完后核对实际挂载。成功调用安装器或找到目标签名都不足以证明 Start 凭据写入、保存和收尾可用。
            bool ready = Install(DailyTraining, () =>
            {
                VerifyTrainingHooks(harmony.Id);
                return SSCPatchState.Ready;
            }, () =>
            {
                Unpatch(harmony, lifecycle);
                // 玩家命令的前置检查仍可报告缺少核心钩子，并在原版清空队列前拒绝。
                lock (results)
                    foreach (Type type in lifecycle)
                        if (results.TryGetValue(type.Name, out var row) && row.State == SSCPatchState.Ready)
                            results[type.Name] = (SSCPatchState.Fallback, row.Target, "Rollback", DailyTraining);
            }, stage: "Verify");
            TrainerAssignmentUtility.DailyTrainingHooksReady = ready;
            if (!ready)
                Log.WarningOnce("[SSC Harmony] Daily training dispatch disabled because required lifecycle hooks are unavailable.", 0x53535452);
            return complete && ready;
        }

        private const string DerivedReservations = "RJW derived reservations";

        private static bool InstallReservation(Harmony harmony, Type type)
        {
            var hook = typeof(SSCRestrictionReservationHook);
            string target = type.FullName + ".TryMakePreToilReservations";
            MethodBase method = null;
            MethodInfo patch = AccessTools.DeclaredMethod(hook, nameof(SSCRestrictionReservationHook.Prefix));
            bool installed = Install(type.FullName + " / SSC reservation", () =>
            {
                method = AccessTools.DeclaredMethod(type, "TryMakePreToilReservations", new[] { typeof(bool) });
                // 继承已有预约实现的驱动无需再补一次。空结果表示这个类型无需独立的诊断行。
                if (method == null || method.IsAbstract) return null;
                if (method.IsStatic || ((MethodInfo)method).ReturnType != typeof(bool) || method.ContainsGenericParameters)
                    throw new MissingMethodException(target);
                harmony.Patch(method, prefix: new HarmonyMethod(patch));
                return SSCPatchState.Ready;
            }, () => { if (method != null) harmony.Unpatch(method, patch); }, target);
            // 只有实际失败的驱动才进入限制系统的拒绝名单，继承正常预约实现的驱动照常工作。
            if (!installed) SSCRestrictionJobGuard.UnavailableDriverHooks.Add(type);
            return installed;
        }

        private static void InstallPreparation(Harmony harmony, Type type)
        {
            string target = type.FullName + ".MakeNewToils";
            MethodInfo method = null;
            MethodInfo patch = AccessTools.DeclaredMethod(typeof(SSCRestrictionQuickiePreparationHook),
                nameof(SSCRestrictionQuickiePreparationHook.Postfix));
            if (!Install(type.FullName + " / SSC preparation", () =>
            {
                method = SSCOptionalPatchGroup.RequireTarget(type, "MakeNewToils", typeof(IEnumerable<Toil>), false);
                harmony.Patch(method, postfix: new HarmonyMethod(patch));
                return SSCPatchState.Ready;
            }, () => { if (method != null) harmony.Unpatch(method, patch); }, target))
                SSCRestrictionJobGuard.UnavailableDriverHooks.Add(type);
        }

        /// <summary>包住一次安装尝试及其回滚。返回值用于判断是否抛出故障，具体结果由诊断状态表示。</summary>
        internal static bool Install(string group, Func<SSCPatchState?> install, Action rollback = null, string target = null,
            string stage = "Install")
        {
            try
            {
                SSCPatchState? status = install();
                if (status.HasValue) Record(group, status.Value, target);
                return true;
            }
            catch (Exception error)
            {
                try { rollback?.Invoke(); }
                catch (Exception cleanup)
                {
                    Record(group, SSCPatchState.Failed, target, stage + " / rollback",
                        error.GetBaseException().GetType().Name + " / " + cleanup.GetBaseException().GetType().Name);
                    Log.Warning($"[SSC Harmony] {group} rollback failed: {cleanup}");
                    Log.Warning($"[SSC Harmony] {group} installation failed: {error}");
                    return false;
                }
                // Harmony 3 会把失败的原方法写进部分包装异常。复制报告保留可定位的信息，完整异常留在 Player.log。
                const string prefix = "Patching exception in method ";
                if (target == null && error is HarmonyException && error.Message.StartsWith(prefix, StringComparison.Ordinal))
                    target = error.Message.Substring(prefix.Length);
                string detail = error.GetBaseException().GetType().Name;
                if (error is MissingMethodException && error.Source == typeof(SSCPatchRegistration).Assembly.GetName().Name)
                    detail += ": " + error.Message;
                Record(group, SSCPatchState.Failed, target, stage, detail);
                Log.Warning($"[SSC Harmony] {group} installation failed: {error}");
                return false;
            }
        }

        /// <summary>先登记等待状态，再交给原长事件队列。等回调运行完，按安装器报告的真实就绪结果更新。</summary>
        internal static void Schedule(string group, Action install, Func<SSCPatchState> state)
        {
            Record(group, SSCPatchState.Waiting);
            LongEventHandler.ExecuteWhenFinished(() => Install(group, () =>
            {
                install();
                return state();
            }, stage: "Delayed install"));
        }

        /// <summary>核对预约、准备、原生 Start 与 End 和凭据存档。Start 前置检查资格，后置仅在原方法确实执行后记录凭据。</summary>
        private static void VerifyTrainingHooks(string owner)
        {
            Type lifecycle = typeof(Patch_JobDriver_Sex_ProtectChainOfSexSlave);
            if (SSCRestrictionJobGuard.UnavailableDriverHooks.Contains(typeof(JobDriver_Training)))
                throw new MissingMethodException(typeof(JobDriver_Training).FullName + ".TryMakePreToilReservations");
            RequireHook(owner, typeof(JobDriver_Sex), "TryMakePreToilReservations", lifecycle, "Prefix");
            RequireHook(owner, typeof(JobDriver), "TryActuallyStartNextToil", lifecycle, "NextToil_Prefix");
            RequireHook(owner, typeof(JobDriver_SexBaseInitiator), "Start", lifecycle, "Start_Prefix");
            RequireHook(owner, typeof(JobDriver_SexBaseInitiator), "Start", lifecycle, "Start_Postfix", true);
            RequireHook(owner, typeof(JobDriver_SexBaseInitiator), "End", lifecycle, "End_Prefix");
            RequireHook(owner, typeof(JobDriver_Sex), nameof(JobDriver_Sex.ExposeData), typeof(SSCRestrictionSceneSaveHook), "Postfix", true);
            RequireHook(owner, typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), typeof(SSCRestrictionOrderedJobHook), "Prefix");
        }

        // 同一宿主方法可能已被其他补丁占用，因此同时核对 owner、补丁声明类型、方法和前后置位置。
        private static void RequireHook(string owner, Type originalType, string originalName, Type hook, string name, bool postfix = false)
        {
            MethodBase original = AccessTools.Method(originalType, originalName);
            var info = original == null ? null : Harmony.GetPatchInfo(original);
            var patches = postfix ? info?.Postfixes : info?.Prefixes;
            if (patches?.Any(p => p.owner == owner && p.PatchMethod.DeclaringType == hook && p.PatchMethod.Name == name) != true)
                throw new MissingMethodException(originalType.FullName + "." + originalName + " / " + hook.Name + "." + name);
        }

        private static void Unpatch(Harmony harmony, Type[] types)
        {
            // owner 由所有注解补丁共用。只撤回失败组方法，即使某个目标无法重建也继续清理其余目标。
            var failures = new List<Exception>();
            foreach (MethodBase original in harmony.GetPatchedMethods().ToArray())
            {
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                var methods = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                    .Where(p => p.owner == harmony.Id && types.Contains(p.PatchMethod.DeclaringType))
                    .Select(p => p.PatchMethod).Distinct().ToArray();
                foreach (var method in methods)
                {
                    try { harmony.Unpatch(original, method); }
                    catch (Exception error) { failures.Add(error); }
                }
            }
            if (failures.Count != 0) throw new AggregateException("SSC rollback failed", failures);
        }
    }
}
