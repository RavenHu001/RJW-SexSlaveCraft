using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>准备窗口只预览候选；实际选角与启动按拟定的两人组合验证，查询不修改绑定或指派。</summary>
    internal static class BindingRitualSelectionUtility
    {
        private sealed class WindowState { public TargetInfo Target; }
        private sealed class Validation
        {
            public Pawn Master, Slave;
            public TargetInfo Target;
            public bool Allowed;
        }
        private sealed class Operation
        {
            public RitualRoleAssignments Assignments;
            public readonly List<Validation> Results = new List<Validation>();
        }
        private sealed class Scope : IDisposable
        {
            private Action end;
            public Scope(Action end) { this.end = end; }
            public void Dispose() { Action action = end; end = null; action?.Invoke(); }
        }

        private static readonly ConditionalWeakTable<RitualRoleAssignments, WindowState> windows =
            new ConditionalWeakTable<RitualRoleAssignments, WindowState>();
        private static readonly List<RitualRole> noAutoRoles = new List<RitualRole>();
        [ThreadStatic] private static int previewDepth;
        [ThreadStatic] private static Operation operation;

        /// <summary>按行为定义识别，包括原版 Festival 生成的仪式及旧存档。</summary>
        public static bool IsBinding(Precept_Ritual ritual)
            => ritual?.behavior?.def?.defName == "SSC_BindingRitualBehavior";

        /// <summary>仅登记本次准备窗口；弱引用不延长取消窗口或旧游戏的存活期。</summary>
        public static void Register(RitualRoleAssignments assignments, TargetInfo target)
        {
            if (assignments != null && IsBinding(assignments.Ritual))
                windows.GetValue(assignments, _ => new WindowState()).Target = target;
        }

        public static void Forget(RitualRoleAssignments assignments)
        {
            if (assignments != null) windows.Remove(assignments);
        }

        /// <summary>只在本 mod 的角色方法里使用；运行中的 Lord 始终绕过预览快捷路径。</summary>
        public static bool IsPreview(RitualRoleAssignments assignments)
            => previewDepth > 0 || IsWindow(assignments);

        public static bool IsWindow(RitualRoleAssignments assignments)
            => assignments != null && windows.TryGetValue(assignments, out _);

        /// <summary>开窗建表、开窗前的存在性检查及原版基础条件查询使用显式预览作用域。</summary>
        public static IDisposable Preview()
        {
            previewDepth++;
            return new Scope(() => previewDepth--);
        }

        /// <summary>仅在一次同步选人/启动操作内复用验证；下一次操作必定读取最新状态。</summary>
        public static IDisposable BeginOperation(RitualRoleAssignments assignments)
        {
            if (operation?.Assignments == assignments) return null;
            Operation previous = operation;
            operation = new Operation { Assignments = assignments };
            return new Scope(() => operation = previous);
        }

        /// <summary>只屏蔽 SSC 执行角色的自动填充，保留原版观众、必需参与者及清理流程。</summary>
        public static List<RitualRole> AutoFillRoles(RitualRoleAssignments assignments)
        {
            List<RitualRole> roles = assignments.AllRolesForReading;
            if (!IsWindow(assignments)) return roles;
            List<RitualRole> others = null;
            foreach (RitualRole role in roles)
                if (!IsExecutionRole(role)) (others ?? (others = new List<RitualRole>())).Add(role);
            return others ?? noAutoRoles;
        }

        public static bool IsExecutionRole(RitualRole role)
            => role is RitualRole_BindingMaster || role is RitualRole_BindingSlave;

        /// <summary>按即将形成的角色分配校验；拖到已有头像上时同时验证原版会交换过去的角色。</summary>
        public static bool ValidateAttempt(RitualRoleAssignments assignments, RitualRole role, Pawn candidate,
            Pawn replacing, out string reason)
            => ValidateAttempt(assignments, role, candidate, replacing, assignments.FirstAssignedPawn("master"),
                assignments.FirstAssignedPawn("slave"), out reason);

        public static bool ValidateAttempt(RitualRoleAssignments assignments, RitualRole role, Pawn candidate,
            Pawn replacing, Pawn master, Pawn slave, out string reason)
        {
            reason = null;
            if (!windows.TryGetValue(assignments, out WindowState state)) return true;
            if (role is RitualRole_BindingMaster)
            {
                if (slave == candidate) slave = replacing;
                master = candidate;
            }
            else if (role is RitualRole_BindingSlave)
            {
                if (master == candidate) master = replacing;
                slave = candidate;
            }
            else return true;
            if (candidate == null) { reason = InvalidReason(); return false; }
            return Validate(assignments, master, slave, state.Target, false, out reason);
        }

        /// <summary>启动前必须同时存在两个执行者；此入口也用于绕过窗口的直接启动。</summary>
        public static bool ValidateStart(RitualRoleAssignments assignments, TargetInfo target, out string reason)
        {
            return Validate(assignments, assignments?.FirstAssignedPawn("master"),
                assignments?.FirstAssignedPawn("slave"), target, true, out reason);
        }

        private static bool Validate(RitualRoleAssignments assignments, Pawn master, Pawn slave, TargetInfo target,
            bool requireBoth, out string reason)
        {
            reason = null;
            if (assignments == null || requireBoth && (master == null || slave == null))
            {
                reason = "SSC_RitualSelection_MissingRoles".Translate();
                return false;
            }
            if (master != null && master == slave) { reason = InvalidReason(); return false; }
            // UI 交换会经过暂时只有一个角色的中间状态；已验证的最终组合覆盖该操作内的子集。
            if (operation?.Assignments == assignments)
            {
                foreach (Validation cached in operation.Results)
                    if (cached.Target.Equals(target) && cached.Allowed &&
                        (master == null || cached.Master == master) && (slave == null || cached.Slave == slave))
                        return true;
            }

            bool allowed = ValidateUncached(assignments, master, slave, target, out reason);
            if (!allowed && string.IsNullOrEmpty(reason)) reason = InvalidReason();
            if (operation?.Assignments == assignments)
                operation.Results.Add(new Validation { Master = master, Slave = slave, Target = target, Allowed = allowed });
            return allowed;
        }

        /// <summary>先核对原版基础拒绝和低成本许可，再验证所选两人的可达性及目标身体条件。</summary>
        private static bool ValidateUncached(RitualRoleAssignments assignments, Pawn master, Pawn slave,
            TargetInfo target, out string reason)
        {
            reason = null;
            RitualRole_BindingMaster masterRole = null;
            RitualRole_BindingSlave slaveRole = null;
            foreach (RitualRole role in assignments.AllRolesForReading)
            {
                if (role is RitualRole_BindingMaster m) masterRole = m;
                if (role is RitualRole_BindingSlave s) slaveRole = s;
            }
            if (masterRole == null || slaveRole == null) { reason = InvalidReason(); return false; }
            using (Preview())
            {
                if (master != null)
                {
                    if (!masterRole.AppliesToCandidate(master, out reason, false)) return false;
                    reason = RitualRoleAssignments.PawnNotAssignableReason(master, masterRole, assignments.Ritual, assignments, target, out _);
                    if (reason != null) return false;
                }
                if (slave != null)
                {
                    if (!slaveRole.AppliesToCandidate(slave, out reason, false)) return false;
                    reason = RitualRoleAssignments.PawnNotAssignableReason(slave, slaveRole, assignments.Ritual, assignments, target, out _);
                    if (reason != null) return false;
                }
            }
            if (master != null && slave != null)
            {
                SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(master, slave, true), false);
                if (!admission.Allowed) { reason = admission.Reason; return false; }
            }
            if (master != null && !masterRole.ValidateIndividual(master, out reason, target)) return false;
            return slave == null || slaveRole.ValidateIndividual(slave, out reason, target);
        }

        private static string InvalidReason() => "SSC_RitualSelection_Invalid".Translate();

        /// <summary>普通选角拒绝只显示短提示，不构造详细报告、不写警告日志。</summary>
        public static void Reject(string reason)
            => Messages.Message(reason ?? InvalidReason(), MessageTypeDefOf.RejectInput, false);
    }
}
