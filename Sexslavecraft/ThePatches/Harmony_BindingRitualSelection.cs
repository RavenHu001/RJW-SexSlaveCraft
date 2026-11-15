using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(RitualBehaviorWorker), nameof(RitualBehaviorWorker.CanStartRitualNow))]
    internal static class Harmony_BindingRitualAvailability
    {
        /// <summary>开窗前的全体角色存在性扫描只做候选预览；真正启动另有两人复查。</summary>
        public static bool Prefix(Precept_Ritual __1, ref string __result, out IDisposable __state)
        {
            __state = null;
            if (!BindingRitualSelectionUtility.IsBinding(__1)) return true;
            if (!Harmony_BindingRitualStart.Installed)
            {
                __result = BindingRitualSelectionUtility.InvalidReason();
                return false;
            }
            if (Harmony_BindingRitualAutoFill.ManualSelectionReady)
                __state = BindingRitualSelectionUtility.Preview();
            return true;
        }
        public static Exception Finalizer(Exception __exception, IDisposable __state)
        { __state?.Dispose(); return __exception; }
    }

    [HarmonyPatch(typeof(Dialog_BeginRitual), nameof(Dialog_BeginRitual.CreateRitualRoleAssignments))]
    internal static class Harmony_BindingRitualCandidateList
    {
        /// <summary>建表期间保留原版参与条件，但不对全体候选执行 SSC 完整资格查询。</summary>
        public static void Prefix(Precept_Ritual __0, out IDisposable __state)
            => __state = BindingRitualSelectionUtility.IsBinding(__0) && Harmony_BindingRitualAutoFill.ManualSelectionReady
                ? BindingRitualSelectionUtility.Preview() : null;
        public static void Postfix(RitualRoleAssignments __result, TargetInfo __1)
        {
            if (BindingRitualSelectionUtility.IsBinding(__result?.Ritual) && Harmony_BindingRitualAutoFill.ManualSelectionReady)
                BindingRitualSelectionUtility.Register(__result, __1);
        }
        public static Exception Finalizer(Exception __exception, IDisposable __state)
        { __state?.Dispose(); return __exception; }
    }

    [HarmonyPatch]
    internal static class Harmony_BindingRitualWindow
    {
        private static readonly ConstructorInfo constructor = AccessTools.DeclaredConstructor(typeof(Dialog_BeginRitual), new[]
        {
            typeof(string), typeof(Precept_Ritual), typeof(TargetInfo), typeof(Map), typeof(Dialog_BeginRitual.ActionCallback),
            typeof(Pawn), typeof(RitualObligation), typeof(Dialog_BeginRitual.PawnFilter), typeof(string),
            typeof(List<Pawn>), typeof(Dictionary<string, Pawn>), typeof(RitualOutcomeEffectDef), typeof(List<string>), typeof(Pawn)
        });

        public static bool Prepare()
        {
            if (constructor?.IsPublic == true &&
                AccessTools.Field(typeof(Dialog_BeginRitual), "assignments")?.FieldType == typeof(RitualRoleAssignments) &&
                AccessTools.Field(typeof(Dialog_BeginRitual), "target")?.FieldType == typeof(TargetInfo) &&
                AccessTools.Field(typeof(Dialog_BeginRitual), "action")?.FieldType == typeof(Dialog_BeginRitual.ActionCallback) &&
                AccessTools.Field(typeof(Dialog_BeginRitual), "extraInfos")?.FieldType == typeof(List<string>) &&
                AccessTools.Field(typeof(Dialog_BeginRitual), "participantsDrawer")?.FieldType == typeof(IPawnRoleSelectionWidget))
                return true;
            Log.WarningOnce("[SSC Binding ritual] Unsupported preparation window. SSC manual role selection is disabled.", 0x53535257);
            return false;
        }

        public static MethodBase TargetMethod() => constructor;

        internal static bool Installed => constructor != null &&
            Harmony.GetPatchInfo(constructor)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_BindingRitualWindow)) == true;

        /// <summary>在确认回调前验证最终两人；失败返回 false 保持窗口开启，成功解除准备窗口状态。</summary>
        public static void Postfix(RitualRoleAssignments ___assignments, TargetInfo ___target,
            ref Dialog_BeginRitual.ActionCallback ___action, ref List<string> ___extraInfos,
            ref IPawnRoleSelectionWidget ___participantsDrawer)
        {
            if (!BindingRitualSelectionUtility.IsBinding(___assignments?.Ritual)) return;
            bool manualSelection = Harmony_BindingRitualAutoFill.ManualSelectionReady;
            if (manualSelection)
            {
                BindingRitualSelectionUtility.Register(___assignments, ___target);
                ___participantsDrawer = new BindingRitualSelectionWidget(___participantsDrawer, ___assignments);
            }
            Dialog_BeginRitual.ActionCallback original = ___action;
            TargetInfo target = ___target;
            ___action = assignments =>
            {
                using (BindingRitualSelectionUtility.BeginOperation(assignments))
                {
                    if (!BindingRitualSelectionUtility.ValidateStart(assignments, target, out string reason))
                    {
                        BindingRitualSelectionUtility.Reject(reason);
                        return false;
                    }
                    bool accepted = original != null && original(assignments);
                    if (accepted) BindingRitualSelectionUtility.Forget(assignments);
                    else if (manualSelection && Harmony_BindingRitualAutoFill.Matched)
                        BindingRitualSelectionUtility.Register(assignments, target);
                    return accepted;
                }
            };
            if (!manualSelection) return;
            // 复制调用方列表，避免改变其他窗口复用的说明集合。
            ___extraInfos = ___extraInfos == null ? new List<string>() : new List<string>(___extraInfos);
            ___extraInfos.Add("SSC_RitualSelection_ChooseParticipants".Translate());
            ___extraInfos.Add("SSC_RitualSelection_Legend".Translate());
        }
    }

    [HarmonyPatch]
    internal static class Harmony_BindingRitualAutoFill
    {
        private static readonly MethodInfo target = AccessTools.DeclaredMethod(typeof(RitualRoleAssignments),
            nameof(RitualRoleAssignments.FillPawns), new[] { typeof(Dialog_BeginRitual.PawnFilter), typeof(TargetInfo) });

        internal static bool Matched { get; private set; }
        internal static bool ManualSelectionReady => Matched && target != null &&
            Harmony.GetPatchInfo(target)?.Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_BindingRitualAutoFill)) == true &&
            Harmony_BindingRitualWindow.Installed && Harmony_BindingRitualBeforeMutation.Installed &&
            Harmony_BindingRitualAssignment.Installed && Harmony_BindingRitualStart.Installed;

        public static bool Prepare()
        {
            if (target?.IsStatic == false && target.ReturnType == typeof(void)) return true;
            Log.WarningOnce("[SSC Binding ritual] FillPawns signature changed. SSC manual role selection is disabled.", 0x53535246);
            return false;
        }

        public static MethodBase TargetMethod() => target;

        /// <summary>只替换自动填角色的两次读取；观众分配与原版后续清理仍按原流程执行。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            MethodInfo roles = AccessTools.PropertyGetter(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.AllRolesForReading));
            MethodInfo replacement = AccessTools.Method(typeof(BindingRitualSelectionUtility), nameof(BindingRitualSelectionUtility.AutoFillRoles));
            int matches = roles == null ? 0 : codes.Count(code => code.Calls(roles));
            Matched = roles?.IsStatic == false && replacement?.ReturnType == roles.ReturnType && matches == 2;
            if (!Matched)
            {
                Log.WarningOnce($"[SSC Binding ritual] Unsupported FillPawns layout (role reads: {matches}/2). SSC manual role selection is disabled.",
                    0x5353524C);
                return codes;
            }
            foreach (CodeInstruction code in codes)
                if (code.Calls(roles)) { code.opcode = OpCodes.Call; code.operand = replacement; }
            return codes;
        }
    }

    [HarmonyPatch(typeof(FloatMenu), MethodType.Constructor, new[] { typeof(List<FloatMenuOption>) })]
    internal static class Harmony_BindingRitualMenu
    {
        public static void Prefix(List<FloatMenuOption> __0) => BindingRitualSelectionWidget.WrapMenu(__0);
    }

    // 原版点击/拖拽在 AfterWindowStack 才执行；登记时捕获 SSC 窗口，执行时重新建立提交范围。
    [HarmonyPatch(typeof(DragAndDropWidget), nameof(DragAndDropWidget.Draggable))]
    internal static class Harmony_BindingRitualDeferredClick
    {
        public static void Prefix(ref Action __3, ref Action __4)
        { __3 = BindingRitualSelectionWidget.WrapDeferred(__3); __4 = BindingRitualSelectionWidget.WrapDeferred(__4); }
    }

    [HarmonyPatch(typeof(DragAndDropWidget), nameof(DragAndDropWidget.DropArea))]
    internal static class Harmony_BindingRitualDeferredDrop
    {
        public static void Prefix(ref Action<object> __2) => __2 = BindingRitualSelectionWidget.WrapDeferred(__2);
    }

    [HarmonyPatch(typeof(DragAndDropWidget), nameof(DragAndDropWidget.NewGroup))]
    internal static class Harmony_BindingRitualDeferredGroup
    {
        public static void Prefix(ref Action<object, UnityEngine.Vector2> __0)
            => __0 = BindingRitualSelectionWidget.WrapDeferred(__0);
    }

    [HarmonyPatch]
    internal static class Harmony_BindingRitualBeforeMutation
    {
        private static readonly MethodInfo[] targets =
        {
            AccessTools.DeclaredMethod(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.TryUnassignAnyRole), new[] { typeof(Pawn) }),
            AccessTools.DeclaredMethod(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.RemoveParticipant), new[] { typeof(Pawn) }),
            AccessTools.DeclaredMethod(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.TryAssignSpectate), new[] { typeof(Pawn), typeof(Pawn) })
        };

        public static bool Prepare()
        {
            if (targets.All(method => method?.IsStatic == false) && targets[0].ReturnType == typeof(bool) &&
                targets[1].ReturnType == typeof(void) && targets[2].ReturnType == typeof(bool))
                return true;
            Log.WarningOnce("[SSC Binding ritual] Role mutation signatures changed. SSC manual role selection is disabled.", 0x5353524D);
            return false;
        }

        public static IEnumerable<MethodBase> TargetMethods() => targets;

        internal static bool Installed => targets.All(method => method != null &&
            Harmony.GetPatchInfo(method)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_BindingRitualBeforeMutation)) == true);

        public static bool Prefix(RitualRoleAssignments __instance, Pawn __0, MethodBase __originalMethod)
        {
            // TryAssignAnyRole 的替换回退允许 replacing=null。它既不能作为观众，也不能写入 allPawns。
            if (__0 == null && BindingRitualSelectionUtility.IsWindow(__instance)) return false;
            BindingRitualSelectionWidget.BeforeMutation(__instance, __0,
                __originalMethod.Name == nameof(RitualRoleAssignments.RemoveParticipant));
            return true;
        }
    }

    [HarmonyPatch]
    internal static class Harmony_BindingRitualAssignment
    {
        private static readonly MethodInfo target = AccessTools.DeclaredMethod(typeof(RitualRoleAssignments),
            nameof(RitualRoleAssignments.TryAssign), new[]
            {
                typeof(Pawn), typeof(RitualRole), typeof(PsychicRitualRoleDef.Reason).MakeByRefType(),
                typeof(PsychicRitualRoleDef.Context), typeof(Pawn)
            });

        public static bool Prepare()
        {
            if (target?.IsStatic == false && target.ReturnType == typeof(bool)) return true;
            Log.WarningOnce("[SSC Binding ritual] TryAssign signature changed. SSC manual role selection is disabled.", 0x53535241);
            return false;
        }

        public static MethodBase TargetMethod() => target;

        internal static bool Installed => target != null &&
            Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_BindingRitualAssignment)) == true;

        /// <summary>覆盖直接提交入口；原版随后多次调用角色方法时只做预览，复用本次完整结果。</summary>
        public static bool Prefix(RitualRoleAssignments __instance, Pawn __0, RitualRole __1,
            ref PsychicRitualRoleDef.Reason __2, ref bool __result, out IDisposable __state)
        {
            __state = null;
            if (!BindingRitualSelectionUtility.IsWindow(__instance) || !BindingRitualSelectionUtility.IsExecutionRole(__1)) return true;
            __state = BindingRitualSelectionUtility.BeginOperation(__instance);
            if (BindingRitualSelectionWidget.ValidateAssignment(__instance, __1, __0, out string reason)) return true;
            __2 = PsychicRitualRoleDef.Reason.None;
            __result = false;
            if (!BindingRitualSelectionWidget.MarkRejected(__instance)) BindingRitualSelectionUtility.Reject(reason);
            else BindingRitualSelectionWidget.RejectionReason(__instance, reason);
            return false;
        }
        public static void Postfix(RitualRoleAssignments __instance, RitualRole __1, bool __result)
        {
            if (!__result && BindingRitualSelectionUtility.IsExecutionRole(__1))
                BindingRitualSelectionWidget.MarkRejected(__instance, true);
        }
        public static Exception Finalizer(Exception __exception, IDisposable __state)
        { __state?.Dispose(); return __exception; }
    }

    [HarmonyPatch]
    internal static class Harmony_BindingRitualStart
    {
        private static readonly MethodInfo target = AccessTools.DeclaredMethod(typeof(RitualBehaviorWorker),
            nameof(RitualBehaviorWorker.TryExecuteOn), new[]
            { typeof(TargetInfo), typeof(Pawn), typeof(Precept_Ritual), typeof(RitualObligation), typeof(RitualRoleAssignments), typeof(bool) });

        public static bool Prepare()
        {
            if (target?.IsStatic == false && target.ReturnType == typeof(void)) return true;
            Log.WarningOnce("[SSC Binding ritual] Execution guard target unavailable. SSC binding ritual is disabled.", 0x53535253);
            return false;
        }

        public static MethodBase TargetMethod() => target;

        internal static bool Installed => target != null &&
            Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_BindingRitualStart)) == true;

        /// <summary>在结束参与者原任务或创建 Lord 之前拦截失效组合，也覆盖直接启动和确认弹窗回调。</summary>
        public static bool Prefix(TargetInfo __0, Precept_Ritual __2, RitualRoleAssignments __4)
        {
            if (!BindingRitualSelectionUtility.IsBinding(__2)) return true;
            using (BindingRitualSelectionUtility.BeginOperation(__4))
            {
                if (!BindingRitualSelectionUtility.ValidateStart(__4, __0, out string reason))
                {
                    BindingRitualSelectionUtility.Reject(reason);
                    return false;
                }
            }
            BindingRitualSelectionUtility.Forget(__4);
            return true;
        }
    }

    /// <summary>窗口字段仅在低频关闭/按钮检查时读取，缓存反射元数据。</summary>
    internal static class BindingRitualWindowFields
    {
        private static readonly FieldInfo assignments = AccessTools.Field(typeof(Dialog_BeginRitual), "assignments");
        public static RitualRoleAssignments Get(object window)
            => window is Dialog_BeginRitual ? assignments?.GetValue(window) as RitualRoleAssignments : null;
    }

    [HarmonyPatch(typeof(Dialog_BeginLordJob), nameof(Dialog_BeginLordJob.CanBegin), MethodType.Getter)]
    internal static class Harmony_BindingRitualCanBegin
    {
        /// <summary>缺少执行者时禁用开始按钮；重绘只读取两个引用，完整复查留在提交边界。</summary>
        public static void Postfix(Dialog_BeginLordJob __instance, ref bool __result)
        {
            RitualRoleAssignments assignments = BindingRitualWindowFields.Get(__instance);
            if (BindingRitualSelectionUtility.IsWindow(assignments))
                __result &= assignments.FirstAssignedPawn("master") != null && assignments.FirstAssignedPawn("slave") != null;
        }
    }

    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class Harmony_BindingRitualWindowClosed
    {
        public static void Postfix(Window __instance)
            => BindingRitualSelectionUtility.Forget(BindingRitualWindowFields.Get(__instance));
    }
}
