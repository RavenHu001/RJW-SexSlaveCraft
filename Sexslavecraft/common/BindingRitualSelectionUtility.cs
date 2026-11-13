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
#if SSC_RW17
            public CompSexSlaveTraining SlaveComp;
            public int TrainerRevision;
            public Pawn Owner;
#endif
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
            => ValidateAttempt(assignments, role, candidate, replacing, assignments?.FirstAssignedPawn("master"),
                assignments?.FirstAssignedPawn("slave"), out reason);

        public static bool ValidateAttempt(RitualRoleAssignments assignments, RitualRole role, Pawn candidate,
            Pawn replacing, Pawn master, Pawn slave, out string reason)
        {
            reason = null;
            if (assignments == null || !windows.TryGetValue(assignments, out WindowState state)) return true;
            if (!IsExecutionRole(role)) return true;
            if (!ProposePair(role, candidate, replacing, ref master, ref slave))
            { reason = InvalidReason(); return false; }
            return Validate(assignments, master, slave, state.Target, false, out reason);
        }

        /// <summary>悬停检查使用与提交相同的拟定组合及完整条件，但不读写一次操作的验证缓存。</summary>
        public static bool InspectAttempt(RitualRoleAssignments assignments, RitualRole role, Pawn candidate,
            Pawn replacing, out string reason)
        {
            if (!TryGetProposedPair(assignments, role, candidate, replacing,
                out Pawn master, out Pawn slave, out TargetInfo target, out reason)) return false;
            if (!CanPreviewAssignment(assignments, role, candidate, out reason, replacing)) return false;
            if (master != null && master == slave) { reason = InvalidReason(); return false; }
            bool allowed = ValidateUncached(assignments, master, slave, target, out reason);
            if (allowed) reason = null;
            else if (string.IsNullOrEmpty(reason)) reason = InvalidReason();
            return allowed;
        }

        /// <summary>原版强制分配及满槽移动另有拒绝；只为显示说明，不改变提交或启动规则。</summary>
        internal static bool CanPreviewAssignment(RitualRoleAssignments assignments, RitualRole role, Pawn candidate,
            out string reason, Pawn replacing = null)
        {
            reason = null;
            if (assignments == null || candidate == null || role == null || string.IsNullOrEmpty(role.id))
            { reason = InvalidReason(); return false; }
            RitualRole lockedRole = null;
            if (assignments.ForcedRolesForReading?.ContainsValue(candidate) == true)
                lockedRole = assignments.ForcedRole(candidate) ?? role;
            else if (assignments.ForcedRolesForReading != null &&
                assignments.ForcedRolesForReading.TryGetValue(role.id, out Pawn fixedPawn) && fixedPawn != null)
                lockedRole = role;
            if (lockedRole != null)
            {
                reason = "RoleIsLocked".Translate(lockedRole.LabelCap);
                return false;
            }
            if (role.maxCount > 0)
            {
                int remaining = 0;
                bool alreadyAssigned = false;
                foreach (Pawn assigned in assignments.AssignedPawns(role))
                {
                    alreadyAssigned |= assigned == candidate;
                    if (assigned != replacing) remaining++;
                }
                // 已选人物的提示描述当前状态；新人物必须有空位或明确替换当前头像。
                if (!alreadyAssigned && remaining >= role.maxCount)
                {
                    reason = "MaxPawnsPerRole".Translate(role.LabelCap, role.maxCount);
                    return false;
                }
            }
            return true;
        }

        /// <summary>只供已登记的 SSC 窗口查询；角色必须是该窗口实际拥有的执行角色。</summary>
        internal static bool TryGetProposedPair(RitualRoleAssignments assignments, RitualRole role, Pawn candidate,
            Pawn replacing, out Pawn master, out Pawn slave, out TargetInfo target, out string reason)
        {
            master = slave = null;
            target = default(TargetInfo);
            reason = null;
            if (assignments == null || !IsBinding(assignments.Ritual) || !IsExecutionRole(role) ||
                assignments.AllRolesForReading == null || !assignments.AllRolesForReading.Contains(role) ||
                !windows.TryGetValue(assignments, out WindowState state))
            { reason = InvalidReason(); return false; }
            bool hasMaster = false, hasSlave = false;
            foreach (RitualRole definedRole in assignments.AllRolesForReading)
            {
                hasMaster |= definedRole is RitualRole_BindingMaster;
                hasSlave |= definedRole is RitualRole_BindingSlave;
            }
            if (!hasMaster || !hasSlave) { reason = InvalidReason(); return false; }
            master = assignments.FirstAssignedPawn("master");
            slave = assignments.FirstAssignedPawn("slave");
            target = state.Target;
            if (!ProposePair(role, candidate, replacing, ref master, ref slave))
            { reason = InvalidReason(); return false; }
            return true;
        }

        /// <summary>交换对方已选角色时，原槽位人物成为对方的新候选；空槽移动则清空原角色。</summary>
        private static bool ProposePair(RitualRole role, Pawn candidate, Pawn replacing, ref Pawn master, ref Pawn slave)
        {
            if (candidate == null) return false;
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
            else return false;
            return true;
        }

        /// <summary>启动前必须同时存在两个执行者；此入口也用于绕过窗口的直接启动。</summary>
        public static bool ValidateStart(RitualRoleAssignments assignments, TargetInfo target, out string reason)
        {
#if SSC_RW17
            Pawn slave = assignments?.FirstAssignedPawn("slave");
            Pawn master = assignments?.FirstAssignedPawn("master");
            if (SSCDailyTrainingOccupancy.IsBusy(slave))
            {
                reason = Strings.Train_Reason_AlreadyBeingTrained;
                return false;
            }
            if (!Validate(assignments, master, slave, target, true, out reason)) return false;
            // 原版角色和第三方身体检查都可同步触发任务或重新选角。
            if (!ReferenceEquals(master, assignments.FirstAssignedPawn("master")) ||
                !ReferenceEquals(slave, assignments.FirstAssignedPawn("slave")))
            {
                reason = InvalidReason();
                return false;
            }
            if (!SSCDailyTrainingOccupancy.IsBusy(slave)) return true;
            reason = Strings.Train_Reason_AlreadyBeingTrained;
            return false;
#else
            return Validate(assignments, assignments?.FirstAssignedPawn("master"),
                assignments?.FirstAssignedPawn("slave"), target, true, out reason);
#endif
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
            if (slave != null && !SSCIdentityUtility.IsSexSlave(slave))
            {
                reason = "SSC_Training_TargetIdentityRequired".Translate();
                return false;
            }
#if SSC_RW17
            var slaveComp = slave?.TryGetComp<CompSexSlaveTraining>();
            int trainerRevision = slaveComp?.TrainerAssignmentRevision ?? 0;
            Pawn owner = SSCBondUtility.GetBoundMaster(slave);
#endif
            // UI 交换会经过暂时只有一个角色的中间状态；已验证的最终组合覆盖该操作内的子集。
            if (operation?.Assignments == assignments)
            {
                foreach (Validation cached in operation.Results)
                    if (cached.Target.Equals(target) && cached.Allowed &&
#if SSC_RW17
                        (slave == null || ReferenceEquals(cached.SlaveComp, slaveComp) &&
                            cached.TrainerRevision == trainerRevision && ReferenceEquals(cached.Owner, owner)) &&
#endif
                        (master == null || cached.Master == master) && (slave == null || cached.Slave == slave))
                        return true;
            }

            bool allowed = ValidateUncached(assignments, master, slave, target, out reason);
            if (!allowed && string.IsNullOrEmpty(reason)) reason = InvalidReason();
            if (operation?.Assignments == assignments)
                operation.Results.Add(new Validation
                {
                    Master = master, Slave = slave, Target = target, Allowed = allowed,
#if SSC_RW17
                    SlaveComp = slaveComp, TrainerRevision = trainerRevision, Owner = owner
#endif
                });
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
#if SSC_RW17
            else if (slave != null && !HasEligibleHost(slave, target, out reason, assignments, masterRole))
                return false;
#endif
            if (master != null && !masterRole.ValidateIndividual(master, out reason, target)) return false;
            if (slave == null) return true;
            if (!slaveRole.ValidateIndividual(slave, out reason, target)) return false;
#if SSC_RW17
            // 原版寻路与 RJW 查询可能触发外部回调，选角或启动前再次核对当前配对许可。
            if (master == null)
                return HasEligibleHost(slave, target, out reason, assignments, masterRole);
            var currentAdmission = SSCRestrictionTrainingUtility.Evaluate(
                SSCRestrictionTrainingUtility.CreateRequest(master, slave, true), false);
            if (!currentAdmission.Allowed) { reason = currentAdmission.Reason; return false; }
#endif
            return true;
        }

#if SSC_RW17
        // 空主持槽只判断是否存在配对；确定主持人后走原版完整角色、身体与寻路检查。
        internal static bool HasEligibleHost(Pawn slave, TargetInfo target, out string reason,
            RitualRoleAssignments assignments = null, RitualRole_BindingMaster masterRole = null, bool skipReason = false)
        {
            Pawn owner = SSCBondUtility.GetBoundMaster(slave);
            masterRole ??= new RitualRole_BindingMaster();
            Map map = target.Map ?? slave.Map;
            reason = null;
            // 原版强制主持槽只允许固定角色，不能以其他名单成员证明受训者可入槽。
            if (masterRole.id != null && assignments?.ForcedRolesForReading != null &&
                assignments.ForcedRolesForReading.TryGetValue(masterRole.id, out Pawn fixedHost) && fixedHost != null)
            {
                if (CanHost(fixedHost, out reason)) return true;
                if (!skipReason) reason ??= InvalidReason();
                return false;
            }
            if (CanHost(owner, out reason)) { reason = null; return true; }
            foreach (Pawn trainer in TrainerAssignmentUtility.GetActiveAssignedTrainers(slave))
            {
                if (ReferenceEquals(trainer, owner)) continue;
                if (CanHost(trainer, out string failure)) { reason = null; return true; }
                reason ??= failure;
            }
            if (!skipReason) reason ??= InvalidReason();
            return false;

            bool CanHost(Pawn pawn, out string failure)
            {
                failure = null;
                if (pawn == null || !ReferenceEquals(pawn.Map, map) ||
                    !masterRole.AppliesToCandidate(pawn, out failure, skipReason)) return false;
                if (assignments != null)
                {
                    var forcedRoles = assignments.ForcedRolesForReading;
                    if (forcedRoles != null && forcedRoles.ContainsValue(pawn) &&
                        (masterRole.id == null || !forcedRoles.TryGetValue(masterRole.id, out Pawn assignedHost) ||
                            !ReferenceEquals(assignedHost, pawn)))
                        return false;
                    using (Preview())
                        failure = RitualRoleAssignments.PawnNotAssignableReason(pawn, masterRole, assignments.Ritual,
                            assignments, target, out _);
                    if (failure != null) return false;
                    if (!masterRole.ValidateIndividual(pawn, out failure, target, skipReason)) return false;
                }
                var admission = SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(pawn, slave, true), false);
                if (!skipReason && !admission.Allowed) failure = admission.Reason;
                return admission.Allowed;
            }
        }
#endif

        internal static string InvalidReason() => "SSC_RitualSelection_Invalid".Translate();

        /// <summary>普通选角拒绝只显示短提示，不构造详细报告、不写警告日志。</summary>
        public static void Reject(string reason)
            => Messages.Message(reason ?? InvalidReason(), MessageTypeDefOf.RejectInput, false);
    }
}
