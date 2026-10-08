using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只装饰当前 SSC 选人控件；保留原版布局、拖拽、头像和文化图标。</summary>
    internal sealed class BindingRitualSelectionVisuals
    {
        private const float RefreshSeconds = 0.35f;
        private static readonly Color HostColor = new Color(1f, 0.76f, 0.32f);
        private static readonly Color TargetColor = new Color(0.78f, 0.6f, 1f);
        private static readonly Color AllowedColor = new Color(0.45f, 0.9f, 0.5f);
        private static readonly Color RejectedColor = new Color(1f, 0.4f, 0.35f);
        private static readonly Color BadgeBackground = new Color(0.06f, 0.07f, 0.08f, 0.94f);
        private static readonly Color DimmedColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly FieldInfo groupField = AccessTools.Field(typeof(PawnRoleSelectionWidgetBase<RitualRole>), "dragAndDropGroup");
        [ThreadStatic] private static BindingRitualSelectionVisuals active;
        internal static BindingRitualSelectionVisuals Active => active;

        private readonly PawnRitualRoleSelectionWidget owner;
        private readonly RitualRoleAssignments assignments;
        private readonly RitualRole hostRole, targetRole;
        private readonly string hostBadge = "SSC_RitualSelection_HostBadge".Translate();
        private readonly string targetBadge = "SSC_RitualSelection_TargetBadge".Translate();
        private readonly Dictionary<Pawn, BindingRitualRolePreview[]> previews = new Dictionary<Pawn, BindingRitualRolePreview[]>();
        private readonly Dictionary<Pawn, Rect> portraits = new Dictionary<Pawn, Rect>();
        private readonly List<RoleSlot> slots = new List<RoleSlot>();
        private Pawn previousHost, previousTarget;
        private float refreshedAt = float.NegativeInfinity;
        private int scrollDepth;
        private readonly Dictionary<RitualRole, Detail> details = new Dictionary<RitualRole, Detail>();

        private sealed class RoleSlot { public Rect Rect; public RitualRole Role; }
        private sealed class Detail
        {
            public Pawn Pawn, Replacing;
            public RitualRole Role;
            public bool Allowed;
            public string Reason;
        }
        private sealed class DrawScope : IDisposable
        {
            private readonly BindingRitualSelectionVisuals previous;
            public DrawScope(BindingRitualSelectionVisuals value) { previous = active; active = value; }
            public void Dispose() { active = previous; }
        }

        public BindingRitualSelectionVisuals(IPawnRoleSelectionWidget inner, RitualRoleAssignments assignments)
        {
            owner = inner as PawnRitualRoleSelectionWidget;
            this.assignments = assignments;
            foreach (RitualRole role in assignments.AllRolesForReading)
            {
                if (role is RitualRole_BindingMaster) hostRole = role;
                if (role is RitualRole_BindingSlave) targetRole = role;
            }
        }

        public IDisposable BeginDraw()
        {
            if (owner == null || !BindingRitualSelectionUtility.IsWindow(assignments)) return new DrawScope(null);
            Pawn host = assignments.FirstAssignedPawn("master"), target = assignments.FirstAssignedPawn("slave");
            float now = Time.realtimeSinceStartup;
            // 短期缓存只服务显示。真实点击/拖拽和开始仍使用独立的最新提交检查。
            if (host != previousHost || target != previousTarget || now - refreshedAt >= RefreshSeconds || now < refreshedAt)
            {
                previousHost = host; previousTarget = target; refreshedAt = now;
                previews.Clear(); details.Clear();
            }
            portraits.Clear(); slots.Clear(); scrollDepth = 0;
            return new DrawScope(this);
        }

        public void BeginScroll() { scrollDepth++; }
        public void EndScroll() { scrollDepth--; }

        public void RecordSlot(int group, Rect rect, object context)
        {
            if (scrollDepth != 1 || group != Group || !(context is IGrouping<string, RitualRole> roles)) return;
            RitualRole role = roles.FirstOrDefault(BindingRitualSelectionUtility.IsExecutionRole);
            if (role != null) slots.Add(new RoleSlot { Rect = rect, Role = role });
        }

        private int Group => (int)groupField.GetValue(owner);
        private Pawn Replacing(RitualRole role) => assignments.FirstAssignedPawn(role.id);

        // 目标提示暂以 SSC 性奴身份为门槛；只限制显示，不替代实际选角或开始检查。
        private bool CanShowRoleHint(Pawn pawn, RitualRole role)
            => role != null && (role != targetRole || SSCIdentityUtility.IsSexSlave(pawn));

        private BindingRitualRolePreview Preview(Pawn pawn, RitualRole role)
        {
            if (!previews.TryGetValue(pawn, out BindingRitualRolePreview[] states))
            {
                states = new[]
                {
                    BindingRitualRolePreviewUtility.Inspect(assignments, hostRole, pawn, hostRole == null ? null : Replacing(hostRole)),
                    BindingRitualRolePreviewUtility.Inspect(assignments, targetRole, pawn, targetRole == null ? null : Replacing(targetRole))
                };
                previews.Add(pawn, states);
            }
            return states[role == hostRole ? 0 : 1];
        }

        private Detail Inspect(Pawn pawn, RitualRole role, Pawn replacing)
        {
            if (!details.TryGetValue(role, out Detail detail) || detail.Pawn != pawn || detail.Replacing != replacing)
            {
                bool allowed = BindingRitualSelectionUtility.InspectAttempt(assignments, role, pawn, replacing, out string reason);
                detail = new Detail { Pawn = pawn, Role = role, Replacing = replacing, Allowed = allowed, Reason = reason };
                details[role] = detail;
            }
            return detail;
        }

        public void DrawPortrait(Rect rect, Pawn pawn, ref bool tooltipActive)
        {
            if (scrollDepth != 1 || pawn == null) return;
            portraits[pawn] = rect;
            float y = rect.y + 1f;
            DrawBadge(rect, pawn, hostRole, hostBadge, HostColor, ref y, ref tooltipActive);
            DrawBadge(rect, pawn, targetRole, targetBadge, TargetColor, ref y, ref tooltipActive);
            // 只有当前悬停头像执行完整查询，不能因悬停角色栏而全量寻路/身体检查。
            if (!DragAndDropWidget.Dragging && Mouse.IsOver(rect) && !tooltipActive)
            {
                RitualRole focus = previousHost != null ? targetRole : previousTarget != null ? hostRole : null;
                if (!CanShowRoleHint(pawn, focus) || !Preview(pawn, focus).Candidate)
                    focus = Preview(pawn, hostRole).Assigned ? hostRole
                        : CanShowRoleHint(pawn, targetRole) && Preview(pawn, targetRole).Candidate ? targetRole : hostRole;
                if (CanShowRoleHint(pawn, focus) && Preview(pawn, focus).Candidate)
                {
                    Detail result = Inspect(pawn, focus, Replacing(focus));
                    DrawBorder(rect, result.Allowed);
                }
            }
        }

        private void DrawBadge(Rect portrait, Pawn pawn, RitualRole role, string label, Color color, ref float y, ref bool tooltipActive)
        {
            // 已分配状态也不能为非性奴补画目标标签；身份变化不等待预览缓存过期。
            if (!CanShowRoleHint(pawn, role)) return;
            BindingRitualRolePreview state = Preview(pawn, role);
            if (!state.Candidate && !state.Assigned) return;
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleCenter, false, Color.white))
            {
                // 第二行避开原版右下角的文化/身份图标；两种资格同时存在时仍保留文字。
                float available = y > portrait.y + 1f ? portrait.width - 24f : portrait.width - 4f;
                float width = Math.Min(available, Text.CalcSize(label).x + 8f);
                var badge = new Rect(portrait.x + 2f, y, width, 17f);
                bool blocked = !state.Candidate || !state.PairAllowed;
                Widgets.DrawBoxSolidWithOutline(badge, BadgeBackground, blocked ? DimmedColor : color);
                Widgets.Label(badge, label);
                if (state.Assigned)
                {
                    Color before = GUI.color;
                    try { GUI.color = color; GUI.DrawTexture(new Rect(portrait.xMax - 10f, portrait.y + 4f, 8f, 8f), Widgets.CheckboxOnTex); }
                    finally { GUI.color = before; }
                }
                if (blocked)
                    Widgets.DrawLine(new Vector2(badge.x + 2f, badge.yMax - 2f), new Vector2(badge.xMax - 2f, badge.y + 2f), RejectedColor, 1.5f);
                if (!DragAndDropWidget.Dragging && Mouse.IsOver(badge))
                {
                    // 原版在图标绘制后登记整张头像提示；此标志只替换当前角标的提示。
                    tooltipActive = true;
                    string tip = RoleTip(pawn, role);
                    TooltipHandler.TipRegion(badge, tip);
                    Detail result = Inspect(pawn, role, Replacing(role));
                    DrawBorder(portrait, result.Allowed);
                }
                y += 18f;
            }
        }

        private string RoleTip(Pawn pawn, RitualRole role)
        {
            BindingRitualRolePreview state = Preview(pawn, role);
            var text = new StringBuilder((role == hostRole ? "SSC_RitualSelection_HostCandidate" : "SSC_RitualSelection_TargetCandidate").Translate());
            text.AppendLine();
            if (state.Assigned) text.AppendLine("SSC_RitualSelection_Assigned".Translate());
            if (!state.Candidate) text.Append("SSC_RitualSelection_CannotAssign".Translate(state.Reason));
            else if (!state.PairAllowed) text.Append("SSC_RitualSelection_PairBlocked".Translate(state.Reason));
            else
            {
                Detail result = Inspect(pawn, role, Replacing(role));
                text.Append(result.Allowed ? "SSC_RitualSelection_Allowed".Translate()
                    : "SSC_RitualSelection_CannotAssign".Translate(result.Reason));
                if (result.Allowed && (role == hostRole ? previousTarget : previousHost) == null)
                    text.AppendLine().Append("SSC_RitualSelection_Candidate".Translate());
            }
            return text.ToString();
        }

        public string ExtraTip(Pawn pawn)
        {
            if (hostRole == null || targetRole == null || DragAndDropWidget.Dragging ||
                !portraits.TryGetValue(pawn, out Rect rect) || !Mouse.IsOver(rect)) return null;
            // 此原版方法会为所有头像构造提示，但完整资格只针对鼠标下的一人。
            string tip = RoleTip(pawn, hostRole);
            return CanShowRoleHint(pawn, targetRole) ? tip + "\n\n" + RoleTip(pawn, targetRole) : tip;
        }

        public void DrawDropFeedback()
        {
            if (scrollDepth != 1 || !(DragAndDropWidget.CurrentlyDraggedDraggable() is Pawn pawn)) return;
            foreach (RoleSlot slot in slots)
            {
                if (!Mouse.IsOver(slot.Rect)) continue;
                Pawn replacing = DragAndDropWidget.DraggableAt(Group, Event.current.mousePosition) as Pawn;
                if (replacing == pawn) replacing = null;
                Detail result = Inspect(pawn, slot.Role, replacing);
                DrawBorder(slot.Rect, result.Allowed);
                if (result.Allowed) TooltipHandler.TipRegion(slot.Rect, "SSC_RitualSelection_Allowed".Translate());
                else ShowDragReason("SSC_RitualSelection_CannotAssign".Translate(result.Reason));
                break;
            }
        }

        private static void ShowDragReason(string reason)
        {
            float width, height;
            using (new TextBlock(GameFont.Small))
            {
                width = Math.Min(400f, Text.CalcSize(reason).x);
                height = Text.CalcHeight(reason, width);
            }
            Vector2 mouse = UI.MousePositionOnUI;
            var rect = new Rect(Math.Max(0f, Math.Min(UI.screenWidth - width - 10f, mouse.x - width / 2f)),
                Math.Max(0f, UI.screenHeight - mouse.y - height - 20f), width + 10f, height + 10f);
            // 原版拖拽提示使用该 ID；后写入完整原因，避免同时显示相互矛盾的轻量提示。
            Find.WindowStack.ImmediateWindow(47839543, rect, WindowLayer.Super, () =>
            {
                using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft, RejectedColor))
                    Widgets.Label(new Rect(5f, 5f, width, height), reason);
            });
        }

        private static void DrawBorder(Rect rect, bool allowed)
        {
            Color before = GUI.color;
            try { GUI.color = allowed ? AllowedColor : RejectedColor; Widgets.DrawBox(rect, 2); }
            finally { GUI.color = before; }
        }
    }
}
