// AI 辅助生成
#if SSC_RW17
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using rjw;
using rjw.Modules.Attraction;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>手动安装并整体开放的1.7适配；锚点不符时所有新增豁免均关闭，RJW原行为继续执行。</summary>
    internal static class SSCMasterAttractionPatches
    {
        internal const string Owner = "SexSlaveCraft.master.attraction";
        internal static bool Ready { get; private set; }
        private static bool installed;
        private static readonly HashSet<string> warnings = new HashSet<string>();
        private sealed class InstallAttempt { internal MethodBase Method; internal bool Matched; }
        [ThreadStatic] private static InstallAttempt attempt;
        private static readonly MethodInfo preflight = AccessTools.Method(typeof(AttractionUtility), "PreflightChecks");
        private static readonly MethodInfo setup = AccessTools.Method(typeof(AttractionUtility), "SetupRequest");
        private static readonly MethodInfo finalize = AccessTools.DeclaredMethod(typeof(AttractionUtility), "Finalize",
            new[] { typeof(AttractionRequest).MakeByRefType(), typeof(float) });
        private static readonly MethodInfo evaluateMode = AccessTools.Method(typeof(PreferenceEvaluator), "Evaluate");
        private static readonly MethodInfo settings = AccessTools.Method(typeof(CasualSex_Helper), nameof(CasualSex_Helper.GetCasualHookupSettingsForPawn));
        private static readonly MethodInfo canFoolAround = AccessTools.Method(typeof(AttractionUtility), nameof(AttractionUtility.CanFoolAround), new[] { typeof(Pawn), typeof(bool) });
        private static readonly MethodInfo opinion = AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.OpinionOf), new[] { typeof(Pawn) });

        /// <summary>先安装全部必要入口，再一次性发布Ready；任何失败都撤销本owner已装补丁。</summary>
        internal static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony(Owner);
            var originals = new List<MethodBase>();
            try
            {
                PatchTranspiler(harmony, originals, AccessTools.Method(typeof(AttractionUtility), nameof(AttractionUtility.Evaluate),
                    new[] { typeof(AttractionRequest).MakeByRefType(), typeof(AttractionMode[]) }), nameof(EvaluateTranspiler));
                // 先包装getter再生成其消费者，避免小getter在消费者先JIT后绕过缓存撤销。
                PatchPrefix(harmony, originals, AccessTools.PropertyGetter(typeof(AppraisalResult), nameof(AppraisalResult.ObserverAttraction)), nameof(ObserverCachePrefix));
                PatchPrefix(harmony, originals, AccessTools.PropertyGetter(typeof(AppraisalResult), nameof(AppraisalResult.TargetAttraction)), nameof(TargetCachePrefix));
                PatchTranspiler(harmony, originals, AccessTools.Method(typeof(CasualSex_Helper), nameof(CasualSex_Helper.CasualHookupAllowedViaSettings)), nameof(SettingsTranspiler));
                PatchTranspiler(harmony, originals, AccessTools.Method(typeof(CasualSex_Helper), nameof(CasualSex_Helper.FindPartner)), nameof(FindPartnerTranspiler));
                Type rmb = typeof(CasualSex_Helper).Assembly.GetType("rjw.RMB.RMB_Sex", true);
                PatchTranspiler(harmony, originals, AccessTools.Method(rmb, "DoChecks", new[] { typeof(Pawn), typeof(Pawn) }), nameof(RmbTranspiler));
                PatchTranspiler(harmony, originals, AccessTools.Method(typeof(AttractionUtility), nameof(AttractionUtility.ExplainAttraction),
                    new[] { typeof(Pawn), typeof(Pawn), typeof(AttractionPurpose) }), nameof(ExplainTranspiler));
                if (warnings.Count != 0) throw new InvalidOperationException("a required transform was disabled during installation");
                Ready = true;
            }
            catch (Exception error)
            {
                Ready = false;
                foreach (MethodBase original in originals) harmony.Unpatch(original, HarmonyPatchType.All, Owner);
                Warn("installation: " + error.Message);
            }
            finally { attempt = null; }
        }

        /// <summary>同步安装凭据不参与游戏状态；诊断重放不能把已失效的适配重新标为可用。</summary>
        private static void PatchTranspiler(Harmony harmony, List<MethodBase> originals, MethodInfo original, string patch)
        {
            if (original == null) throw new MissingMethodException(patch);
            originals.Add(original);
            attempt = new InstallAttempt { Method = original };
            try
            {
                harmony.Patch(original, transpiler: new HarmonyMethod(Method(patch)) { priority = Priority.Last });
                if (!attempt.Matched) throw new InvalidOperationException(patch + " anchors do not match");
            }
            finally { attempt = null; }
        }

        /// <summary>缓存撤销和解释入口与评分一起安装；缺任一成员时不发布部分功能。</summary>
        private static void PatchPrefix(Harmony harmony, List<MethodBase> originals, MethodInfo original, string patch)
        {
            if (original == null) throw new MissingMethodException(patch);
            originals.Add(original);
            harmony.Patch(original, prefix: new HarmonyMethod(Method(patch)));
        }

        private static MethodInfo Method(string name) => AccessTools.DeclaredMethod(typeof(SSCMasterAttractionPatches), name);
        private static MethodInfo Utility(string name) => AccessTools.DeclaredMethod(typeof(SSCMasterAttractionUtility), name);

        /// <summary>失败后关闭全部桥接函数；即使Harmony以后重放某个转换成功，也不自动恢复权限。</summary>
        private static void Warn(string reason)
        {
            Ready = false;
            if (warnings.Add(reason)) Log.Warning("[SSC MasterAttraction] Disabled; original RJW rules remain active: " + reason);
        }

        /// <summary>全部锚点验证后才返回修改版；失败返回未经改变的原指令。</summary>
        private static IEnumerable<CodeInstruction> Transform(IEnumerable<CodeInstruction> instructions, MethodBase original,
            Func<List<CodeInstruction>, List<CodeInstruction>> transform)
        {
            List<CodeInstruction> source = instructions.ToList();
            try
            {
                List<CodeInstruction> changed = transform(source.Select(x => new CodeInstruction(x)).ToList());
                if (attempt?.Method == original) attempt.Matched = true;
                return changed;
            }
            catch (Exception error)
            {
                Warn(original.DeclaringType?.FullName + "." + original.Name + ": " + error.Message);
                return source;
            }
        }

        /// <summary>捕获原上下文、延后偏好零退出、原Finalize前整体保底；原硬资格和预约处理均保留。</summary>
        private static IEnumerable<CodeInstruction> EvaluateTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase __originalMethod)
            => Transform(instructions, __originalMethod, code =>
            {
                int pre = SingleCall(code, preflight), setupAt = SingleCall(code, setup), finalAt = SingleCall(code, finalize);
                int eval = SingleCall(code, evaluateMode);
                Require(pre + 4 < code.Count && IsTrueBranch(code[pre + 1]) && IsZero(code[pre + 2]) && code[pre + 3].opcode == OpCodes.Ret,
                    "Preflight failure return");
                Require(setupAt > pre + 3 && code[setupAt - 1].IsLdarg(0) && code[setupAt - 1].blocks.Count == 0, "SetupRequest entry");
                Require(eval + 9 < code.Count && code[eval + 1].opcode == OpCodes.Mul && IsStoreLocal(code[eval + 2]) &&
                    IsLoadLocal(code[eval + 3]) && SameLocal(code[eval + 2], code[eval + 3]) && IsZero(code[eval + 4]) &&
                    (code[eval + 5].opcode == OpCodes.Bgt_Un || code[eval + 5].opcode == OpCodes.Bgt_Un_S) &&
                    code[eval + 5].operand is Label && IsZero(code[eval + 6]) && IsStoreLocal(code[eval + 7]) &&
                    (code[eval + 8].opcode == OpCodes.Leave || code[eval + 8].opcode == OpCodes.Leave_S), "combined-zero exit");
                Require(finalAt > eval && code[finalAt - 2].IsLdarg(0) && IsLoadLocal(code[finalAt - 1]) &&
                    SameLocal(code[eval + 2], code[finalAt - 1]), "Finalize raw combined value");
                LocalBuilder context = generator.DeclareLocal(typeof(SSCMasterAttractionUtility.Context));
                Label continueLabel = (Label)code[eval + 5].operand;
                // 从后往前插入，不改变前面已验证的索引；保留原值，不对各模式分别加底。
                InsertBefore(code, finalAt + 1, new CodeInstruction(OpCodes.Ldloc, context),
                    new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.ValidateFinalizedAttraction))));
                InsertBefore(code, finalAt, new CodeInstruction(OpCodes.Ldloc, context), new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.ApplyAttractionFloor))));
                InsertBefore(code, eval + 6, new CodeInstruction(OpCodes.Ldloc, context), new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.ShouldContinueAfterZero))),
                    new CodeInstruction(OpCodes.Brtrue, continueLabel));
                InsertBefore(code, setupAt - 1, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.CaptureRequest))), new CodeInstruction(OpCodes.Stloc, context));
                return code;
            });

        /// <summary>只扩充当前方法内的设置查询上下文，原helper在桥接函数中照常运行。</summary>
        private static IEnumerable<CodeInstruction> SettingsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
            => Transform(instructions, __originalMethod, code =>
            {
                int[] calls = Calls(code, settings);
                Require(calls.Length == 2, "two directed settings calls");
                Require(code.Count(x => x.opcode == OpCodes.Ret) >= 2, "settings success exits");
                LocalBuilder context = generator.DeclareLocal(typeof(SSCMasterAttractionUtility.Context));
                // 每个出口复核同一个原上下文，另一侧查询回调不能延续前一侧已经放宽的局部阈值。
                for (int i = code.Count - 1; i >= 0; i--)
                    if (code[i].opcode == OpCodes.Ret)
                        InsertBefore(code, i, new CodeInstruction(OpCodes.Ldloc, context),
                            new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.ValidateSettingsResult))));
                calls = Calls(code, settings);
                for (int n = calls.Length - 1; n >= 0; n--)
                {
                    int index = calls[n];
                    InsertBefore(code, index, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldloc, context));
                    code[index + 2].opcode = OpCodes.Call;
                    code[index + 2].operand = Utility(nameof(SSCMasterAttractionUtility.GetDirectedHookupSettings));
                }
                InsertBefore(code, 0, new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.CaptureAppraisal))), new CodeInstruction(OpCodes.Stloc, context));
                return code;
            });

        /// <summary>只放宽当前候选里真实主人的婚恋意愿，不提高发起概率或突破外层任务准入。</summary>
        private static IEnumerable<CodeInstruction> FindPartnerTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
            => Transform(instructions, __originalMethod, code =>
            {
                int partners = SingleCall(code, AccessTools.Method(typeof(CasualSex_Helper), nameof(CasualSex_Helper.PotentialPartnersFor)));
                int around = SingleCall(code, canFoolAround);
                int nympho = SingleCall(code, AccessTools.Method(typeof(xxx), nameof(xxx.is_nympho)));
                Require(partners + 2 < around && IsStoreLocal(code[partners + 2]) && code[partners + 1].operand is MethodInfo toList &&
                    toList.DeclaringType == typeof(Enumerable) && toList.Name == nameof(Enumerable.ToList), "owned candidate list");
                Require(nympho < partners && IsStoreLocal(code[nympho + 1]), "original nympho snapshot");
                CodeInstruction load = LoadStoredLocal(code[partners + 2]);
                CodeInstruction loadNympho = LoadStoredLocal(code[nympho + 1]);
                Require(code.Any(x => x.opcode == OpCodes.Ret), "partner return");
                LocalBuilder context = generator.DeclareLocal(typeof(SSCMasterAttractionUtility.Context));
                for (int i = code.Count - 1; i >= 0; i--)
                    if (code[i].opcode == OpCodes.Ret)
                        InsertBefore(code, i, new CodeInstruction(OpCodes.Ldloc, context),
                            new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.ValidatePartnerResult))));
                around = SingleCall(code, canFoolAround);
                InsertBefore(code, around, load, loadNympho, new CodeInstruction(OpCodes.Ldloca, context));
                code[around + 3].opcode = OpCodes.Call;
                code[around + 3].operand = Utility(nameof(SSCMasterAttractionUtility.CanFoolAroundWithCandidates));
                InsertBefore(code, 0, new CodeInstruction(OpCodes.Ldnull), new CodeInstruction(OpCodes.Stloc, context));
                return code;
            });

        /// <summary>右键只在原DoChecks读意见的位置加底，原Hero分支、主人意见和评分判断都保留。</summary>
        private static IEnumerable<CodeInstruction> RmbTranspiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase __originalMethod)
            => Transform(instructions, __originalMethod, code =>
            {
                int[] calls = Calls(code, opinion);
                Require(calls.Length == 2, "two directed RMB opinions");
                Require(calls[0] >= 3 && code[calls[0] - 3].IsLdarg(0) && code[calls[0] - 1].IsLdarg(1) &&
                    calls[1] >= 3 && code[calls[1] - 3].IsLdarg(1) && code[calls[1] - 1].IsLdarg(0), "RMB opinion direction");
                LocalBuilder context = generator.DeclareLocal(typeof(SSCMasterAttractionUtility.Context));
                MethodInfo evaluation = AccessTools.Method(typeof(AttractionUtility), nameof(AttractionUtility.Evaluate),
                    new[] { typeof(Pawn), typeof(Pawn), typeof(AttractionPurpose) });
                Require(Calls(code, evaluation).Length == 2, "two RMB sex evaluations");
                for (int i = code.Count - 1; i >= 0; i--)
                {
                    if (code[i].opcode == OpCodes.Ret)
                        InsertBefore(code, i, new CodeInstruction(OpCodes.Ldloc, context),
                            new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.ValidateRmbResult))));
                    else if (code[i].Calls(evaluation))
                    {
                        InsertBefore(code, i, new CodeInstruction(OpCodes.Ldloc, context));
                        code[i + 1].opcode = OpCodes.Call;
                        code[i + 1].operand = Utility(nameof(SSCMasterAttractionUtility.EvaluateForRmb));
                    }
                }
                calls = Calls(code, opinion);
                for (int n = calls.Length - 1; n >= 0; n--)
                    InsertBefore(code, calls[n] + 1, new CodeInstruction(n == 0 ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1),
                        new CodeInstruction(n == 0 ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldloc, context),
                        new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.AdjustDirectedOpinion))));
                InsertBefore(code, 0, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, Utility(nameof(SSCMasterAttractionUtility.CaptureEitherDirection))), new CodeInstruction(OpCodes.Stloc, context));
                return code;
            });

        /// <summary>撤销旧关系取得的惰性缓存后由RJW原getter重算，不把原生高评分强行归零。</summary>
        private static void ObserverCachePrefix(AppraisalResult __instance, ref float? ___observerAttraction)
            => SSCMasterAttractionUtility.PrepareCachedScore(__instance, true, ref ___observerAttraction);
        private static void TargetCachePrefix(AppraisalResult __instance, ref float? ___targetAttraction)
            => SSCMasterAttractionUtility.PrepareCachedScore(__instance, false, ref ___targetAttraction);

        /// <summary>保留RJW原解释及窗口，仅在原文本末尾追加关系保底的实际结果。</summary>
        private static IEnumerable<CodeInstruction> ExplainTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
            => Transform(instructions, __originalMethod, code =>
            {
                MethodInfo trim = AccessTools.DeclaredMethod(typeof(string), nameof(string.Trim), Type.EmptyTypes);
                int text = SingleCall(code, trim);
                Require(text > 0 && code[text - 1].Calls(AccessTools.DeclaredMethod(typeof(object), nameof(object.ToString), Type.EmptyTypes)), "native explanation text");
                InsertBefore(code, text, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Call, Method(nameof(AppendExplanation))));
                return code;
            });

        /// <summary>原分项与原Finalized均保留；追加的实际评分单独标注，不把原解释值冒充保底后结果。</summary>
        private static string AppendExplanation(string original, Pawn observer, Pawn target, AttractionPurpose purpose)
        {
            string extra = BuildExplanation(observer, target, purpose);
            return extra == null ? original : original + "\n\n" + extra;
        }

        /// <summary>测试与实际窗口共用真实评分说明生成，不另建一套模拟偏好或假定通过的显示算法。</summary>
        internal static string BuildExplanation(Pawn observer, Pawn target, AttractionPurpose purpose)
        {
            if (!Ready || purpose != AttractionPurpose.ForFucking) return null;
            SSCMasterAttractionUtility.Context context = SSCMasterAttractionUtility.CaptureDirectedPair(observer, target);
            if (context == null) return null;
            float actual = AttractionUtility.Evaluate(observer, target, purpose);
            return SSCMasterAttractionUtility.IsCurrent(context)
                ? "SSC_MasterAttraction_ExplainBody".Translate(observer.LabelShortCap, target.LabelShortCap,
                    actual.ToStringByStyle(ToStringStyle.FloatMaxThree)).ToString()
                : "SSC_MasterAttraction_ExplainUnavailable".Translate().ToString();
        }

        /// <summary>插入指令继承入口标签；原异常块边界保留在原指令，防止跳过新检查或改变using释放。</summary>
        private static void InsertBefore(List<CodeInstruction> code, int index, params CodeInstruction[] added)
        {
            if (index < code.Count && code[index].labels.Count > 0)
            {
                added[0].labels.AddRange(code[index].labels);
                code[index].labels.Clear();
            }
            code.InsertRange(index, added);
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static int[] Calls(List<CodeInstruction> code, MethodInfo method)
        {
            Require(method != null, "missing target member");
            return code.Select((instruction, index) => new { instruction, index }).Where(x => x.instruction.Calls(method)).Select(x => x.index).ToArray();
        }
        private static int SingleCall(List<CodeInstruction> code, MethodInfo method)
        {
            int[] calls = Calls(code, method);
            Require(calls.Length == 1, "unique call " + method?.Name);
            return calls[0];
        }
        private static bool IsTrueBranch(CodeInstruction code) => code.opcode == OpCodes.Brtrue || code.opcode == OpCodes.Brtrue_S;
        private static bool IsZero(CodeInstruction code) => code.opcode == OpCodes.Ldc_R4 && code.operand is float value && value == 0f;
        private static bool IsStoreLocal(CodeInstruction code) => code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S ||
            code.opcode == OpCodes.Stloc_0 || code.opcode == OpCodes.Stloc_1 || code.opcode == OpCodes.Stloc_2 || code.opcode == OpCodes.Stloc_3;
        private static bool IsLoadLocal(CodeInstruction code) => code.opcode == OpCodes.Ldloc || code.opcode == OpCodes.Ldloc_S ||
            code.opcode == OpCodes.Ldloc_0 || code.opcode == OpCodes.Ldloc_1 || code.opcode == OpCodes.Ldloc_2 || code.opcode == OpCodes.Ldloc_3;
        private static int LocalIndex(CodeInstruction code)
        {
            if (code.opcode == OpCodes.Stloc_0 || code.opcode == OpCodes.Ldloc_0) return 0;
            if (code.opcode == OpCodes.Stloc_1 || code.opcode == OpCodes.Ldloc_1) return 1;
            if (code.opcode == OpCodes.Stloc_2 || code.opcode == OpCodes.Ldloc_2) return 2;
            if (code.opcode == OpCodes.Stloc_3 || code.opcode == OpCodes.Ldloc_3) return 3;
            if (code.operand is LocalBuilder builder) return builder.LocalIndex;
            if (code.operand is LocalVariableInfo local) return local.LocalIndex;
            return Convert.ToInt32(code.operand);
        }
        private static bool SameLocal(CodeInstruction left, CodeInstruction right) => LocalIndex(left) == LocalIndex(right);
        private static CodeInstruction LoadStoredLocal(CodeInstruction store)
            => new CodeInstruction(OpCodes.Ldloc, (short)LocalIndex(store));
    }
}
#endif
