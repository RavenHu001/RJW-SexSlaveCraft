using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>仅 1.7，手动安装；没有 HarmonyPatch 特性，避免随旧 PatchAll 重复注册。</summary>
    internal static class SSCOrgasmProgressPatches
    {
        internal const string Owner = "SexSlaveCraft.orgasm.progress";

        /// <summary>一个自动采样入口的静态描述和已验证安装凭据；不包含任何 Pawn 执行状态。</summary>
        internal sealed class AdvanceTarget
        {
            internal readonly MethodInfo Method, Bridge;
            internal readonly SSCOrgasmAdvanceSource Source;
            internal readonly int Writes;
            internal bool Ready;
            internal Patch[] InstalledTranspilers;
            internal string Reason = "not installed";
            /// <summary>绑定原方法、预期写回数量与对应桥；Ready 由完整安装检查最后提交。</summary>
            internal AdvanceTarget(MethodInfo method, SSCOrgasmAdvanceSource source, int writes, MethodInfo bridge)
            { Method = method; Source = source; Writes = writes; Bridge = bridge; }
        }

        /// <summary>只在同步安装期间收集本次转换报告；诊断重放没有该对象，不能改变运行开关。</summary>
        private sealed class InstallAttempt
        {
            internal MethodBase Method;
            internal bool Matched;
            internal string Reason;
        }

        private static readonly Dictionary<MethodBase, AdvanceTarget> targets = new Dictionary<MethodBase, AdvanceTarget>();
        private static readonly List<MethodBase> observerMethods = new List<MethodBase>();
        private static readonly List<MethodBase> lifetimeMethods = new List<MethodBase>();
        // 仅自动适配的四种来源可发布结果；External 走独立公共接口，不借自动入口取得资格。
        private static readonly bool[] readySources = new bool[5];
        private static readonly FieldInfo counter = AccessTools.Field(typeof(JobDriver_Sex), "sex_ticks");
        private static readonly MethodInfo orgasm = AccessTools.Method(typeof(JobDriver_Sex), "Orgasm");
        [ThreadStatic] private static InstallAttempt attempt;
        private static bool installed, observersReady;
        private static string observerFailure = "";

        /// <summary>低频输出已登记入口及失效原因，供测试／诊断使用，不在逐次推进中组装字符串。</summary>
        internal static string Describe() => "observers=" + observersReady + " (" + observerFailure + "); "
            + string.Join("; ", targets.Values.Select(x => x.Source + "=" + x.Ready + " (" + x.Reason + ")"));

        /// <summary>热路径只读缓存开关；未登记、越界和尚未通过完整检查的来源均返回 false。</summary>
        internal static bool IsReady(SSCOrgasmAdvanceSource source)
            => (uint)source < readySources.Length && readySources[(int)source];

        /// <summary>启动阶段的容错外壳；本模块可恢复安装失败只关闭采样，不停止原有 Job。</summary>
        internal static void Install()
        {
            try { InstallCore(); }
            catch (Exception error) when (SSCOrgasmProgressSampling.Recoverable(error))
            {
                DisableObservers(error);
                Log.Warning("[SSC OrgasmProgress] Sampling installation failed; original jobs remain enabled: " + error.Message);
            }
        }

        /// <summary>一次性登记周期观察与主路径写回桥，再统一验证顺序；不依靠 PatchAll 或加载先后作证明。</summary>
        private static void InstallCore()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony(Owner);
            try
            {
                RegisterObservers(harmony);
                observersReady = true;
            }
            catch (Exception error) when (SSCOrgasmProgressSampling.Recoverable(error))
            {
                observerFailure = error.ToString();
                Log.Warning("[SSC OrgasmProgress] Cycle observers unavailable; sampling disabled: " + error.Message);
                return;
            }

            // 先包装动画 Prefix，再安装 SexTick 的 Transpiler，使调用 Prefix 的入口重新生成。
            var animation = AccessTools.TypeByName("Rimworld_Animations.HarmonyPatch_SexTick");
            if (animation != null)
                InstallAdvance(harmony, AccessTools.DeclaredMethod(animation, "Prefix"), SSCOrgasmAdvanceSource.AnimationSexTick, 1);
            InstallAdvance(harmony, AccessTools.Method(typeof(JobDriver_Sex), "SexTick"), SSCOrgasmAdvanceSource.RjwSexTick, 2);

            var uap = AccessTools.TypeByName("UAP_Animations.JobDriver_UAPGroupSexInitiator");
            if (uap != null)
            {
                // UAP 为软依赖。只有它自己的时长同步入口也能观察，才登记两条多人推进路径。
                var clock = AccessTools.TypeByName("UAP_Animations.UAPGroupRJWLauncher");
                try
                {
                    var sync = clock == null ? null : AccessTools.DeclaredMethod(clock, "SynchronizeRJWClock");
                    if (sync == null) throw new MissingMethodException("UAP SynchronizeRJWClock");
                    PatchObserver(harmony, sync, nameof(StaticResetPrefix), nameof(ResetFinalizer));
                    InstallAdvance(harmony, AccessTools.DeclaredMethod(uap, "TickInitiatorOnly"), SSCOrgasmAdvanceSource.UapInitiator, 1);
                    InstallAdvance(harmony, AccessTools.DeclaredMethod(uap, "TickSharedReceiverOnce"), SSCOrgasmAdvanceSource.UapReceiver, 1);
                }
                catch (Exception error) when (SSCOrgasmProgressSampling.Recoverable(error))
                { Log.Warning("[SSC OrgasmProgress] UAP sampling disabled: " + error.Message); }
            }
            ValidateInstalledOrder();
        }

        /// <summary>定位本适配类的静态补丁方法；反射只发生在安装和低频验证阶段。</summary>
        private static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(SSCOrgasmProgressPatches), name);

        /// <summary>让观察范围包住已知回调，或让转换位于已知转换之后；最终仍须核对实际排序。</summary>
        private static HarmonyMethod Ordered(string name, MethodBase original, bool first)
        {
            var patch = new HarmonyMethod(Method(name)) { priority = first ? Priority.First : Priority.Last };
            var info = Harmony.GetPatchInfo(original);
            if (info != null)
            {
                var owners = info.Owners.Where(x => x != Owner).Distinct().ToArray();
                if (first) patch.before = owners;
                else patch.after = owners;
            }
            return patch;
        }

        /// <summary>成对安装入口／完成观察并登记门槛依赖；即使原方法被跳过也须正确关闭已打开范围。</summary>
        private static void PatchObserver(Harmony harmony, MethodInfo original, string prefix, string finalizer)
        {
            if (original == null) throw new MissingMethodException(prefix);
            harmony.Patch(original, prefix: Ordered(prefix, original, true), finalizer: Ordered(finalizer, original, false));
            observerMethods.Add(original);
        }

        /// <summary>登记普通／自慰周期、真实事件、生命周期及动画开始后的失效通知，不重算任何周期。</summary>
        private static void RegisterObservers(Harmony harmony)
        {
            PatchObserver(harmony, AccessTools.DeclaredMethod(typeof(JobDriver_Sex), "SetupOrgasmTicks"), nameof(ResetPrefix), nameof(ResetFinalizer));
            PatchObserver(harmony, AccessTools.DeclaredMethod(typeof(JobDriver_Masturbate), "SetupOrgasmTicks"), nameof(ResetPrefix), nameof(ResetFinalizer));
            PatchObserver(harmony, orgasm, nameof(OrgasmPrefix), nameof(OrgasmFinalizer));
            PatchLifetime(harmony, AccessTools.DeclaredMethod(typeof(JobDriver), "Cleanup"), nameof(CleanupPrefix));
            PatchLifetime(harmony, AccessTools.DeclaredMethod(typeof(JobDriver_Sex), "ExposeData"), nameof(LoadPrefix));
            PatchLifetime(harmony, AccessTools.DeclaredMethod(typeof(JobDriver), "SetupToils"), nameof(SetupToilsPrefix));

            var start = AccessTools.DeclaredMethod(typeof(JobDriver_SexBaseInitiator), "Start");
            if (start == null) throw new MissingMethodException("JobDriver_SexBaseInitiator.Start");
            harmony.Patch(start, postfix: Ordered(nameof(StartPostfix), start, false));
            observerMethods.Add(start);
        }

        /// <summary>登记只需单次通知的清理／加载入口，并要求对应前置补丁持续存在。</summary>
        private static void PatchLifetime(Harmony harmony, MethodInfo original, string prefix)
        {
            if (original == null) throw new MissingMethodException(prefix);
            harmony.Patch(original, prefix: new HarmonyMethod(Method(prefix)));
            lifetimeMethods.Add(original);
        }

        /// <summary>幂等安装一个已知来源；保存本次匹配结果与转换集合，不把部分成功视为可运行。</summary>
        internal static void InstallAdvance(Harmony harmony, MethodInfo original, SSCOrgasmAdvanceSource source, int writes)
        {
            if (original == null) { Log.Warning("[SSC OrgasmProgress] Missing advancement method: " + source); return; }
            if (targets.ContainsKey(original)) return;
            string bridgeName = source == SSCOrgasmAdvanceSource.RjwSexTick ? "WriteRjw"
                : source == SSCOrgasmAdvanceSource.AnimationSexTick ? "WriteAnimation"
                : source == SSCOrgasmAdvanceSource.UapInitiator ? "WriteUapInitiator" : "WriteUapReceiver";
            var bridge = AccessTools.DeclaredMethod(typeof(SSCOrgasmProgressSampling), bridgeName);
            var target = new AdvanceTarget(original, source, writes, bridge);
            targets.Add(original, target);
            var currentAttempt = new InstallAttempt { Method = original, Reason = "transpiler not executed" };
            // 只有这个安装调用能接收 Transpiler 的匹配报告；finally 防止凭据泄漏到之后的诊断重放。
            attempt = currentAttempt;
            try
            {
                harmony.Patch(original, transpiler: Ordered(nameof(Transpiler), original, false));
                target.Ready = currentAttempt.Matched;
                target.Reason = currentAttempt.Reason;
                target.InstalledTranspilers = Harmony.GetPatchInfo(original)?.Transpilers.ToArray();
            }
            catch (Exception error) when (SSCOrgasmProgressSampling.Recoverable(error))
            { target.Ready = false; target.Reason = error.Message; }
            finally { attempt = null; }
            if (!target.Ready) Log.Warning("[SSC OrgasmProgress] " + source + " sampling disabled: " + target.Reason);
        }

        /// <summary>转换已登记方法并向当前安装调用交报告；单独重跑此函数没有运行状态副作用。</summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var input = instructions.ToList();
            if (!targets.TryGetValue(__originalMethod, out var target)) return input;
            bool ok = TryRewrite(input, counter, orgasm, target.Bridge, target.Writes, out var output, out string reason);
            // 只向本轮安装协调者交报告；GetCurrentInstructions 等诊断重放不会提交运行开关。
            if (attempt != null && attempt.Method == __originalMethod)
            { attempt.Matched = ok; attempt.Reason = reason; }
            return output;
        }

        /// <summary>原子验证局部 IL：同实例减法、完整控制流与后续 Orgasm 均成立才替换所有写回。</summary>
        /// <returns>失败时 output 保留收到的原指令对象，不修改其标签或留下半段桥。</returns>
        internal static bool TryRewrite(IList<CodeInstruction> input, FieldInfo counterField, MethodInfo orgasmMethod,
            MethodInfo bridge, int expectedWrites, out List<CodeInstruction> output, out string reason)
        {
            output = input.ToList(); reason = "unexpected write or call shape";
            var writes = new List<int>();
            var branchTargets = new HashSet<Label>();
            int calls = 0;
            // 先收集整段形状与真实跳转目标；局部看起来像减法，仍可能被别处的分支绕过。
            for (int i = 0; i < input.Count; i++)
            {
                var code = input[i];
                if (code.operand is Label label) branchTargets.Add(label);
                else if (code.operand is Label[] labels)
                    foreach (var destination in labels) branchTargets.Add(destination);
                if (code.opcode == OpCodes.Stfld && Equals(code.operand, counterField)) writes.Add(i);
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && Equals(code.operand, orgasmMethod)) calls++;
                if (code.opcode == OpCodes.Stfld && code.operand is FieldInfo field && field.DeclaringType == counterField.DeclaringType
                    && (field.Name == "orgasmstick" || field.Name == "orgasmStartTick" || field.Name == "duration" || field.Name == "orgasms"))
                    return false;
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && Equals(code.operand, bridge)) return false;
            }
            if (bridge == null || expectedWrites <= 0 || writes.Count != expectedWrites || calls != expectedWrites) return false;
            foreach (int write in writes)
            {
                // 这些窗口限制只用于当前已取证 IL 的识别，不是游戏等待下限，也不参与计算推进量。
                if (write < 4 || input[write - 1].opcode != OpCodes.Sub) return false;
                int read = write - 2;
                while (read > Math.Max(0, write - 16) && !(input[read].opcode == OpCodes.Ldfld && Equals(input[read].operand, counterField))) read--;
                if (read < 2 || input[read].opcode != OpCodes.Ldfld || !Equals(input[read].operand, counterField)) return false;
                // dup 或相同的参数／局部变量加载，证明原减法读取的正是写入目标。
                if (input[read - 1].opcode != OpCodes.Dup && !SameLoad(input[read - 2], input[read - 1])) return false;
                // 跳到生产式开头可正常执行完整减法；跳入中途或直接写回则来源未经证明。
                int producerStart = input[read - 1].opcode == OpCodes.Dup ? read - 1 : read - 2;
                for (int j = producerStart + 1; j <= write; j++)
                    if (input[j].labels.Any(branchTargets.Contains)) return false;
                for (int j = read + 1; j < write; j++)
                {
                    var code = input[j];
                    if (code.labels.Count != 0 || code.blocks.Count != 0 || !PureArithmetic(code.opcode)) return false;
                }
                bool found = false;
                for (int j = write + 1; j < Math.Min(input.Count, write + 17); j++)
                {
                    var code = input[j];
                    if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && Equals(code.operand, orgasmMethod)) { found = true; break; }
                    if (code.opcode.FlowControl != FlowControl.Next || code.opcode == OpCodes.Stfld || code.opcode == OpCodes.Stsfld) return false;
                }
                if (!found) return false;
            }
            // 全部锚点验证成功才产生替换；原指令对象、标签及异常块不作原地修改。
            foreach (int write in writes)
                output[write] = new CodeInstruction(input[write]) { opcode = OpCodes.Call, operand = bridge };
            reason = "matched " + writes.Count + " advancement writes";
            return true;
        }

        /// <summary>识别同一参数／局部变量的重复值加载；地址加载不能证明写回对象相同。</summary>
        private static bool SameLoad(CodeInstruction first, CodeInstruction second)
            => first.opcode == second.opcode && Equals(first.operand, second.operand)
                && (first.opcode.Name.StartsWith("ldarg", StringComparison.Ordinal) || first.opcode.Name.StartsWith("ldloc", StringComparison.Ordinal))
                && !first.opcode.Name.Contains("a.") && first.opcode != OpCodes.Ldarga && first.opcode != OpCodes.Ldloca;

        /// <summary>仅接受旧计数到新值之间可核对的纯取值／算术；调用、分支和写操作一律不猜测兼容。</summary>
        private static bool PureArithmetic(OpCode code) => code == OpCodes.Nop || code == OpCodes.Ldfld
            || code.Name.StartsWith("ldc.i4", StringComparison.Ordinal) || code.Name.StartsWith("ldarg", StringComparison.Ordinal)
            || code.Name.StartsWith("ldloc", StringComparison.Ordinal) || code == OpCodes.Add || code == OpCodes.Sub
            || code == OpCodes.Mul || code == OpCodes.Div || code == OpCodes.Rem || code == OpCodes.Neg;

        /// <summary>低频安装门槛；不通过重跑第三方 Transpiler 来诊断，不在采样热路径反射。</summary>
        internal static void ValidateInstalledOrder()
        {
            // 先关闭旧开关，再检查实际排序与安装集合；检查途中出错也不会保留上次的放行。
            Array.Clear(readySources, 0, readySources.Length);
            foreach (var target in targets.Values)
            {
                var info = Harmony.GetPatchInfo(target.Method);
                var ordered = info == null ? new List<MethodInfo>()
                    : PatchProcessor.GetSortedPatchMethods(target.Method, info.Transpilers.ToArray());
                if (ordered.Count == 0 || ordered[ordered.Count - 1] != Method(nameof(Transpiler))
                    || !DependenciesSatisfied(info.Transpilers.ToArray(), ordered)
                    || !SamePatchSet(target.InstalledTranspilers, info.Transpilers.ToArray()))
                { target.Ready = false; target.Reason = "sampling transpiler order or owner dependencies mismatch"; }
            }
            foreach (var original in observerMethods)
            {
                // 成对观察要求入口最先、完成观察最后；仅有正确特性声明不等于有效排序成立。
                var info = Harmony.GetPatchInfo(original);
                if (info == null) { observersReady = false; break; }
                if (info.Finalizers.Any(x => x.owner == Owner))
                {
                    var prefixes = PatchProcessor.GetSortedPatchMethods(original, info.Prefixes.ToArray());
                    var finalizers = PatchProcessor.GetSortedPatchMethods(original, info.Finalizers.ToArray());
                    if (prefixes.Count == 0 || finalizers.Count == 0
                        || !IsOwnMethod(prefixes[0]) || !IsOwnMethod(finalizers[finalizers.Count - 1])
                        || !DependenciesSatisfied(info.Prefixes.ToArray(), prefixes)
                        || !DependenciesSatisfied(info.Finalizers.ToArray(), finalizers)) observersReady = false;
                }
                else
                {
                    var postfixes = PatchProcessor.GetSortedPatchMethods(original, info.Postfixes.ToArray());
                    if (postfixes.Count == 0 || !IsOwnMethod(postfixes[postfixes.Count - 1])
                        || !DependenciesSatisfied(info.Postfixes.ToArray(), postfixes)) observersReady = false;
                }
            }
            foreach (var original in lifetimeMethods)
            {
                var info = Harmony.GetPatchInfo(original);
                if (info == null || !info.Prefixes.Any(x => x.owner == Owner && IsOwnMethod(x.PatchMethod)))
                    observersReady = false;
            }
            if (observersReady)
                // 全部公共依赖成立后才发布来源开关。失败状态不自动恢复，须重新证明安装。
                for (int i = 1; i < readySources.Length; i++)
                {
                    var matches = targets.Values.Where(x => (int)x.Source == i).ToArray();
                    readySources[i] = matches.Length > 0 && matches.All(x => x.Ready);
                }
        }

        /// <summary>确认排序端点属于本适配类，避免将第三方同名方法误当作自己的范围边界。</summary>
        private static bool IsOwnMethod(MethodInfo method) => method.DeclaringType == typeof(SSCOrgasmProgressPatches);

        /// <summary>检查有效方法序列是否真的满足每个已存在 owner 的 before／after，拒绝被打破的依赖环。</summary>
        internal static bool DependenciesSatisfied(Patch[] patches, IList<MethodInfo> ordered)
        {
            if (patches.Length != ordered.Count || ordered.Distinct().Count() != ordered.Count) return false;
            var positions = ordered.Select((method, index) => new { method, index }).ToDictionary(x => x.method, x => x.index);
            foreach (var patch in patches)
            {
                if (!positions.TryGetValue(patch.PatchMethod, out int position)) return false;
                foreach (var other in patches)
                {
                    if (!positions.TryGetValue(other.PatchMethod, out int otherPosition)) return false;
                    if (patch.before != null && patch.before.Contains(other.owner) && position >= otherPosition) return false;
                    if (patch.after != null && patch.after.Contains(other.owner) && position <= otherPosition) return false;
                }
            }
            return true;
        }

        /// <summary>比较实际转换方法、owner、优先级和依赖；末位相同也不能授权后来变化的转换集合。</summary>
        private static bool SamePatchSet(Patch[] installedPatches, Patch[] current)
        {
            if (installedPatches == null || installedPatches.Length != current.Length) return false;
            for (int i = 0; i < current.Length; i++)
            {
                var a = installedPatches[i]; var b = current[i];
                if (a.PatchMethod != b.PatchMethod || a.owner != b.owner || a.priority != b.priority || a.index != b.index
                    || !(a.before ?? Array.Empty<string>()).SequenceEqual(b.before ?? Array.Empty<string>())
                    || !(a.after ?? Array.Empty<string>()).SequenceEqual(b.after ?? Array.Empty<string>())) return false;
            }
            return true;
        }

        /// <summary>记录一次技术失效并关闭缓存门槛，保留原任务和原生计时，不作全局 Job 清理。</summary>
        private static void DisableObservers(Exception error)
        { observersReady = false; Array.Clear(readySources, 0, readySources.Length); observerFailure = error.Message; }

        /// <summary>实例周期初始化入口；只打开采样失效范围，不调用 Setup 或修改其结果。</summary>
        private static void ResetPrefix(JobDriver_Sex __instance, out SSCOrgasmProgressSampling.ResetScope __state)
            => __state = SSCOrgasmProgressSampling.BeginReset(__instance);
        /// <summary>UAP 静态时长同步入口，真实驱动来自原方法第一个参数。</summary>
        private static void StaticResetPrefix(JobDriver_Sex __0, out SSCOrgasmProgressSampling.ResetScope __state)
            => __state = SSCOrgasmProgressSampling.BeginReset(__0);
        /// <summary>完成或异常都关闭本次已打开范围；先清 __state，避免同回调重复关闭。</summary>
        private static void ResetFinalizer(ref SSCOrgasmProgressSampling.ResetScope __state)
        { var scope = __state; __state = default; SSCOrgasmProgressSampling.EndReset(scope); }
        /// <summary>在现有高潮处理前保存端点；不以阈值预判来伪造一次真实事件。</summary>
        private static void OrgasmPrefix(JobDriver_Sex __instance, out SSCOrgasmProgressSampling.OrgasmScope __state)
            => __state = SSCOrgasmProgressSampling.BeginOrgasm(__instance);
        /// <summary>观察真实计数／周期或异常，既不改变返回值，也不吞原事件异常。</summary>
        private static void OrgasmFinalizer(JobDriver_Sex __instance, Exception __exception, ref SSCOrgasmProgressSampling.OrgasmScope __state)
        { var scope = __state; __state = default; SSCOrgasmProgressSampling.EndOrgasm(__instance, scope, __exception); }
        /// <summary>任务结束解除该驱动记录，防止清理后的回调再使用旧样本。</summary>
        private static void CleanupPrefix(JobDriver __instance)
        { if (__instance is JobDriver_Sex driver) SSCOrgasmProgressSampling.Forget(driver); }
        /// <summary>加载阶段使瞬时样本失效；本批不写保存节点，Saving 本身不清理正常执行状态。</summary>
        private static void LoadPrefix(JobDriver_Sex __instance)
        { if (Scribe.mode != LoadSaveMode.Saving && Scribe.mode != LoadSaveMode.Inactive) SSCOrgasmProgressSampling.NotifyReset(__instance); }
        /// <summary>在新任务／读档的低频入口复核补丁门槛并丢弃旧样本，不声称已证明 Toil 构建成功。</summary>
        private static void SetupToilsPrefix(JobDriver __instance)
        {
            if (!(__instance is JobDriver_Sex driver)) return;
            // 新任务／读档的低频屏障：覆盖启动末尾其他模组追加的补丁，不在高潮或逐 tick 反射。
            try { ValidateInstalledOrder(); }
            catch (Exception error) when (SSCOrgasmProgressSampling.Recoverable(error))
            { DisableObservers(error); }
            SSCOrgasmProgressSampling.NotifyReset(driver);
        }
        /// <summary>在动画／Organic 等开始后计时改写之后，使实际参与驱动的旧样本失效，不覆盖新计时。</summary>
        private static void StartPostfix(JobDriver_SexBaseInitiator __instance)
        {
            try
            {
                SSCOrgasmProgressSampling.NotifyReset(__instance);
                if (SSCOrgasmProgressUtility.IsCurrentDriver(__instance)
                    && __instance.Partner?.jobs?.curDriver is JobDriver_SexBaseReciever receiver)
                {
                    SSCOrgasmProgressSampling.NotifyReset(receiver);
                    if (receiver.parteners != null)
                        foreach (var partner in receiver.parteners)
                            if (partner?.jobs?.curDriver is JobDriver_Sex driver) SSCOrgasmProgressSampling.NotifyReset(driver);
                }
            }
            catch (Exception error) when (SSCOrgasmProgressSampling.Recoverable(error)) { DisableObservers(error); }
        }
    }
}
