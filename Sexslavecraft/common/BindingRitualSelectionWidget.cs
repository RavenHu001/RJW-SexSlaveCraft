using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>
    /// 保留原版控件，只为 SSC 选人事件提供回滚边界。不能补丁泛型控件的私有方法：
    /// Mono/CoreCLR 可能共享其方法体，从而影响其他类型的仪式。
    /// </summary>
    internal sealed class BindingRitualSelectionWidget : IPawnRoleSelectionWidget
    {
        private readonly IPawnRoleSelectionWidget inner;
        private readonly RitualRoleAssignments assignments;
        [ThreadStatic] private static Edit current;
        private static readonly FieldInfo assignedRolesField = AccessTools.Field(typeof(RitualRoleAssignments), "assignedRoles");
        private static readonly MethodInfo notifyChanged = AccessTools.Method(typeof(PawnRitualRoleSelectionWidget), "Notify_AssignmentsChanged");

        private sealed class Edit
        {
            public RitualRoleAssignments Assignments;
            public BindingRitualSelectionWidget Widget;
            public Snapshot Before;
            public Pawn Replacing;
            public bool Attempted, Rejected;
            public string Reason;
        }

        /// <summary>只在事件第一次改变分配时复制；布局、重绘和高亮不复制候选列表。</summary>
        private sealed class Snapshot
        {
            private readonly RitualRoleAssignments assignments;
            private readonly Dictionary<string, List<Pawn>> roles;
            private readonly Dictionary<string, List<Pawn>> contents = new Dictionary<string, List<Pawn>>();
            private readonly Dictionary<string, List<Pawn>> originals = new Dictionary<string, List<Pawn>>();
            private readonly List<Pawn> spectators, candidates;
            public readonly Pawn Master, Slave;

            public Snapshot(RitualRoleAssignments assignments)
            {
                this.assignments = assignments;
                Master = assignments.FirstAssignedPawn("master");
                Slave = assignments.FirstAssignedPawn("slave");
                roles = (Dictionary<string, List<Pawn>>)assignedRolesField.GetValue(assignments);
                foreach (var entry in roles)
                {
                    originals.Add(entry.Key, entry.Value);
                    contents.Add(entry.Key, new List<Pawn>(entry.Value));
                }
                spectators = new List<Pawn>(assignments.SpectatorsForReading);
                candidates = new List<Pawn>(assignments.AllCandidatePawns);
            }

            public void Restore()
            {
                roles.Clear();
                foreach (var entry in originals)
                {
                    entry.Value.Clear();
                    entry.Value.AddRange(contents[entry.Key]);
                    roles.Add(entry.Key, entry.Value);
                }
                assignments.SpectatorsForReading.Clear();
                assignments.SpectatorsForReading.AddRange(spectators);
                assignments.AllCandidatePawns.Clear();
                assignments.AllCandidatePawns.AddRange(candidates);
            }
        }

        public BindingRitualSelectionWidget(IPawnRoleSelectionWidget inner, RitualRoleAssignments assignments)
        { this.inner = inner; this.assignments = assignments; }

        public void WindowUpdate() => inner.WindowUpdate();
        public void DrawPawnList(Rect rect) => Run(this, () => inner.DrawPawnList(rect));

        private static void Run(BindingRitualSelectionWidget widget, Action action)
        {
            RitualRoleAssignments assignments = widget.assignments;
            if (!BindingRitualSelectionUtility.IsWindow(assignments) || current?.Assignments == assignments)
            { action(); return; }
            Edit previous = current;
            var edit = new Edit { Assignments = assignments, Widget = widget };
            current = edit;
            try
            {
                using (BindingRitualSelectionUtility.BeginOperation(assignments)) action();
                if (edit.Rejected)
                {
                    Rollback(edit);
                    // 原版自有拒绝提示保留；仅 SSC 的完整验证失败添加短消息。
                    if (edit.Reason != null) BindingRitualSelectionUtility.Reject(edit.Reason);
                }
            }
            catch { Rollback(edit); throw; }
            finally { current = previous; }
        }

        private static void Rollback(Edit edit)
        {
            if (edit.Before == null) return;
            edit.Before.Restore();
            edit.Before = null;
            // 交换的前半步可能已通知品质组件；回滚后同步原版预览缓存。
            if (edit.Widget.inner is PawnRitualRoleSelectionWidget)
                notifyChanged.Invoke(edit.Widget.inner, null);
        }

        /// <summary>右键菜单回调在后续事件执行，必须拥有独立作用域，不能沿用打开菜单时的资格结果。</summary>
        public static void WrapMenu(List<FloatMenuOption> options)
        {
            BindingRitualSelectionWidget widget = current?.Widget;
            if (widget == null) return;
            foreach (FloatMenuOption option in options)
            {
                Action original = option.action;
                if (original != null) option.action = () => Run(widget, original);
            }
        }

        public static void BeforeMutation(RitualRoleAssignments assignments, Pawn pawn, bool removingParticipant)
        {
            if (current?.Assignments != assignments) return;
            if (current.Before == null) current.Before = new Snapshot(assignments);
            if (removingParticipant && !current.Attempted) current.Replacing = pawn;
        }

        public static bool ValidateAssignment(RitualRoleAssignments assignments, RitualRole role, Pawn pawn, out string reason)
        {
            if (current?.Assignments != assignments)
                return BindingRitualSelectionUtility.ValidateAttempt(assignments, role, pawn, null, out reason);
            BeforeMutation(assignments, pawn, false);
            if (current.Rejected) { reason = current.Reason; return false; }
            bool first = !current.Attempted;
            current.Attempted = true;
            // 原版可能已删除旧角色。第一次提交须依据删除前的分配检查最终交换组合。
            return first
                ? BindingRitualSelectionUtility.ValidateAttempt(assignments, role, pawn, current.Replacing,
                    current.Before.Master, current.Before.Slave, out reason)
                : BindingRitualSelectionUtility.ValidateAttempt(assignments, role, pawn, null, out reason);
        }

        public static bool MarkRejected(RitualRoleAssignments assignments)
        {
            if (current?.Assignments != assignments) return false;
            current.Rejected = true;
            return true;
        }

        public static void RejectionReason(RitualRoleAssignments assignments, string reason)
        {
            if (current?.Assignments == assignments && current.Reason == null) current.Reason = reason;
        }
    }
}
