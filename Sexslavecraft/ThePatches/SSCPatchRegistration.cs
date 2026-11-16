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
    /// <summary>保留启动时能确认的跳过原因。只有已跳过的行会把此分类译成提示。</summary>
    internal enum SSCPatchSkipReason { None, MissingDependency, UnsupportedTarget, ConditionsNotMet }

    /// <summary>启动过程写入的结果。诊断窗口只读取副本，不触发安装或反射解析。</summary>
    internal static class SSCPatchRegistration
    {
        internal const string Owner = "SexSlaveCraft.training.patch";
        internal const string DailyTraining = "Daily training lifecycle";
        internal const string WhipCooperation = "PNA whip cooperation";
        internal const string SharedLifecycle = "RJW shared scene lifecycle";
        // 每组保留最近一次结果。窗口刷新或复制时读取整个副本，避免在枚举时遇到延迟安装写入。
        private static readonly Dictionary<string, (SSCPatchState State, string Target, string Stage, string Detail, SSCPatchSkipReason SkipReason)> results = new();

        internal static void Record(string group, SSCPatchState state, string target = null, string stage = null,
            string detail = null, SSCPatchSkipReason skipReason = SSCPatchSkipReason.None)
        {
            lock (results) results[group] = (state, target, stage, detail, skipReason);
        }

        internal static KeyValuePair<string, (SSCPatchState State, string Target, string Stage, string Detail, SSCPatchSkipReason SkipReason)>[] Snapshot()
        {
            lock (results) return results.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        }

        /// <summary>使用原 Harmony owner 和程序集类型顺序安装。故障边界落在补丁类或实际驱动，最后核对日常调教的整套必要钩子。</summary>
        internal static bool InstallAnnotated(Harmony harmony, Assembly assembly)
        {
            // 绑定于扫描之前。缓存解析出的目标和补丁方法，每次准入读当前挂载，覆盖启动后发生的包装重建或卸载。
            bool sharedInstalled = false;
            SSCSceneHookAvailability.SharedHooks = () => sharedInstalled && MissingSharedHook(harmony.Id) == null;
            SSCSceneHookAvailability.DailyReservation = () => DailyReservationHook.Installed(harmony.Id);
            SSCSceneHookAvailability.OrderedJob = () => OrderedJobHook.Installed(harmony.Id);
            SSCSceneHookAvailability.DriverHooks = DriverHooksAvailable;
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
                    if (applied?.Count > 0) return SSCPatchState.Ready;
                    // 空结果按跳过登记。记录原因后返回 null，让通用安装器保留这条带原因的状态。
                    Record(type.Name, SSCPatchState.Skipped, stage: "Prepare", skipReason: ResolveSkipReason(type));
                    return null;
                }, () => Unpatch(harmony, new[] { type }), stage: "Resolve / install");
            }
            // 装完后核对实际挂载。成功调用安装器或找到目标签名都不足以证明 Start 凭据写入、保存和收尾可用。
            sharedInstalled = Install(SharedLifecycle, () =>
            {
                foreach (RequiredHook hook in SharedHooks) hook.Require(harmony.Id);
                return SSCPatchState.Ready;
            }, () =>
            {
                Unpatch(harmony, lifecycle);
                // 玩家命令的前置检查仍可报告缺少核心钩子，并在原版清空队列前拒绝。
                lock (results)
                    foreach (Type type in lifecycle)
                        if (results.TryGetValue(type.Name, out var row) && row.State == SSCPatchState.Ready)
                            results[type.Name] = (SSCPatchState.Fallback, row.Target, "Rollback", SharedLifecycle, SSCPatchSkipReason.None);
            }, stage: "Verify");
            // 自动任务只依赖共享生命周期和自身预约。命令前缀单独登记，失败时手动入口关闭，共享组仍保留。
            bool ready = Install(DailyTraining, () =>
            {
                if (!sharedInstalled) throw new MissingMethodException(SharedLifecycle);
                DailyReservationHook.Require(harmony.Id);
                return SSCPatchState.Ready;
            }, stage: "Verify");
            if (!ready)
                Log.WarningOnce("[SSC Harmony] Daily training dispatch disabled because required lifecycle hooks are unavailable.", 0x53535452);
            return complete && ready;
        }

        private const string DerivedReservations = "RJW derived reservations";
        private static readonly HashSet<Type> failedReservations = new();
        private static readonly HashSet<Type> failedPreparation = new();

        private static SSCPatchSkipReason ResolveSkipReason(Type patchType)
        {
            // Mint 补丁的已知准备条件可以区分两种情况。窗口类型存在时，剩下的检查是 DoRow 与组件字段的接口匹配。
            if (patchType == typeof(Harmony_SSC_MintSharedBedAssignmentUI))
                return AccessTools.TypeByName("DubsMintMenus.Dialog_AssignBuildingOwner") == null
                    ? SSCPatchSkipReason.MissingDependency : SSCPatchSkipReason.UnsupportedTarget;
            // Harmony 返回空列表时没有提供 Prepare 的详细拒绝原因。
            return SSCPatchSkipReason.ConditionsNotMet;
        }

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
            if (!installed) failedReservations.Add(type);
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
                failedPreparation.Add(type);
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
        internal static void Schedule(string group, Action install, Func<SSCPatchState> state,
            SSCPatchSkipReason skipReason = SSCPatchSkipReason.ConditionsNotMet)
        {
            Record(group, SSCPatchState.Waiting);
            LongEventHandler.ExecuteWhenFinished(() => Install(group, () =>
            {
                install();
                var result = state();
                if (result != SSCPatchState.Skipped) return result;
                // 延迟组如果主动跳过，由调度方提供它能确认的原因，覆盖先前的等待记录。
                Record(group, result, stage: "Delayed install", skipReason: skipReason);
                return null;
            }, stage: "Delayed install"));
        }

        private static bool DriverHooksAvailable(Type driver)
            => !InheritsFailedHook(driver, failedReservations, ReservationSlot, ReservationParameters) &&
               !InheritsFailedHook(driver, failedPreparation, PreparationSlot, Type.EmptyTypes);

        private static readonly Type[] ReservationParameters = { typeof(bool) };
        private static readonly MethodInfo ReservationSlot = AccessTools.DeclaredMethod(typeof(JobDriver_Sex), "TryMakePreToilReservations", ReservationParameters);
        private static readonly MethodInfo PreparationSlot = AccessTools.DeclaredMethod(typeof(JobDriver), "MakeNewToils", Type.EmptyTypes);

        private static bool InheritsFailedHook(Type driver, HashSet<Type> failed, MethodInfo slot, Type[] parameters)
        {
            if (failed.Count == 0) return false;
            if (slot == null) return failed.Any(type => type.IsAssignableFrom(driver));
            MethodInfo root = slot.GetBaseDefinition();
            // 每类钩子单独沿虚方法槽定位实际实现。子类继承失配实现时同样拒绝，覆盖另一个钩子不能解除拒绝。
            for (Type type = driver; type != null; type = type.BaseType)
            {
                MethodInfo implementation = AccessTools.DeclaredMethod(type, slot.Name, parameters);
                if (implementation?.GetBaseDefinition() == root) return failed.Contains(type);
                if (failed.Contains(type)) return true;
            }
            return false;
        }

        private static readonly Type SceneHook = typeof(Patch_JobDriver_Sex_ProtectChainOfSexSlave);
        private static readonly RequiredHook[] SharedHooks =
        {
            new(typeof(JobDriver_Sex), "TryMakePreToilReservations", SceneHook, "Prefix"),
            new(typeof(JobDriver), "TryActuallyStartNextToil", SceneHook, "NextToil_Prefix"),
            new(typeof(JobDriver_SexBaseInitiator), "Start", SceneHook, "Start_Prefix"),
            new(typeof(JobDriver_SexBaseInitiator), "Start", SceneHook, "Start_Postfix", true),
            new(typeof(JobDriver_SexBaseInitiator), "End", SceneHook, "End_Prefix"),
            new(typeof(JobDriver_Sex), nameof(JobDriver_Sex.ExposeData), typeof(SSCRestrictionSceneSaveHook), "Postfix", true)
        };
        private static readonly RequiredHook DailyReservationHook =
            new(typeof(JobDriver_Training), "TryMakePreToilReservations", typeof(SSCRestrictionReservationHook), "Prefix");
        private static readonly RequiredHook OrderedJobHook =
            new(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), typeof(SSCRestrictionOrderedJobHook), "Prefix");

        internal static string MissingSharedHook(string owner = Owner)
        {
            foreach (RequiredHook hook in SharedHooks)
                if (!hook.Installed(owner)) return hook.Description;
            return null;
        }

        // 核对实际挂载的方法、owner 和前后置位置，其他模组在同一目标上的补丁不能替代 SSC 钩子。
        private sealed class RequiredHook
        {
            private readonly MethodBase target;
            private readonly MethodInfo patch;
            private readonly bool postfix;
            internal string Description { get; }

            internal RequiredHook(Type targetType, string targetName, Type patchType, string patchName, bool postfix = false)
            {
                // 这些 Harmony 注解按方法名挂载。上游新增同名重载时视为目标不明，留给安装器报告。
                MethodInfo[] matches = AccessTools.GetDeclaredMethods(targetType).Where(method => method.Name == targetName).Take(2).ToArray();
                target = matches.Length == 1 ? matches[0] : null;
                patch = AccessTools.DeclaredMethod(patchType, patchName);
                this.postfix = postfix;
                Description = targetType.FullName + "." + targetName + " / " + patchType.Name + "." + patchName;
            }

            internal bool Installed(string owner)
            {
                var info = target == null ? null : Harmony.GetPatchInfo(target);
                var patches = postfix ? info?.Postfixes : info?.Prefixes;
                return patch != null && patches?.Any(p => p.owner == owner && p.PatchMethod == patch) == true;
            }

            internal void Require(string owner)
            {
                if (!Installed(owner)) throw new MissingMethodException(Description);
            }
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
