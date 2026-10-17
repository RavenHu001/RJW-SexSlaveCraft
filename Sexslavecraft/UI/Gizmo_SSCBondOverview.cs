// AI 辅助生成。命令栏只读摘要；世界查询属于会话 Update，输入交给原版网格分派一次。
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class Gizmo_SSCBondOverview : Gizmo
    {
        private readonly SSCBondOverviewSession session;
        private SSCBondOverviewSnapshot pendingSnapshot;
        private SSCBondOverviewEntry pendingEntry;
        private Pawn pendingSource;
        private int pendingRevision;
        private bool pendingDetails;
        internal Gizmo_SSCBondOverview(SSCBondOverviewSession session) { this.session = session; }
        public override float GetWidth(float maxWidth) => Math.Min(300, Math.Max(1, maxWidth));
        public override bool GroupsWith(Gizmo other) => false;
        public override bool InheritInteractionsFrom(Gizmo other) => false;
        internal void ClearPending() { pendingSnapshot = null; pendingEntry = null; pendingSource = null; pendingDetails = false; }
        internal void Clear() => ClearPending();

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            ClearPending();
            SSCBondOverviewView view = session.Card;
            if (!ReferenceEquals(session.Owner, Current.Game) || !SSCBondOverviewAccess.IsCardSource(view?.Source) || !view.ShowCard)
                return new GizmoResult(GizmoState.Clear);
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75);
            Widgets.DrawBoxSolid(rect, new Color(.14f, .14f, .16f));
            bool leftClick = GUI.enabled && !Disabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
            if (rect.width < 100)
            {
                // 极窄命令栏只保留详情入口；命中区域仍完全位于卡片内。
                float inset = Math.Min(4, rect.width * .1f);
                Rect compact = new Rect(rect.x + inset, rect.y + 4, Math.Max(1, rect.width - 2 * inset), 65);
                Widgets.Label(compact, SSCBondOverviewText.T("ViewAll").Truncate(compact.width));
                if (leftClick && Mouse.IsOver(compact)) Remember(view, null, true);
                return pendingSource != null ? new GizmoResult(GizmoState.Interacted, Event.current)
                    : new GizmoResult(Mouse.IsOver(rect) ? GizmoState.Mouseover : GizmoState.Clear);
            }
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            SSCBondOverviewDisplay display = view.Display;
            float detailsWidth = Math.Min(88, Math.Max(1, rect.width * .34f));
            Rect detailsButton = new Rect(rect.xMax - detailsWidth - 4, rect.y + 4, detailsWidth, 22);
            string heading = snapshot?.Master != null ? snapshot.TotalSlaveCount == 0 ? SSCBondOverviewText.T("Master")
                : SSCBondOverviewText.Format("BothCounts", snapshot.LivingSlaveCount, snapshot.TotalSlaveCount)
                : display?.Count ?? SSCBondOverviewText.T("Title");
            float headingWidth = Math.Max(1, detailsButton.x - rect.x - 10);
            Widgets.Label(new Rect(rect.x + 6, rect.y + 4, headingWidth, 22), heading.Truncate(headingWidth));
            Widgets.DrawHighlight(detailsButton);
            Widgets.Label(detailsButton, SSCBondOverviewText.T("ViewAll").Truncate(detailsButton.width));
            if (leftClick && Mouse.IsOver(detailsButton)) Remember(view, null, true);

            // 头像固定尺寸并限制数量；窄卡片减少预览，不产生超出自身区域的命中框。
            int capacity = SSCBondOverviewLayout.CardCapacity(rect.width);
            int count = snapshot == null ? 0 : snapshot.TotalSlaveCount + (snapshot.Master != null ? 1 : 0);
            for (int i = 0; i < Math.Min(capacity, count); i++)
            {
                bool master = snapshot.Master != null && i == 0;
                int index = i - (snapshot.Master != null ? 1 : 0);
                SSCBondOverviewEntry entry = master ? snapshot.Master : snapshot.Slaves[index];
                Rect portrait = new Rect(rect.x + 6 + i * 44, rect.y + 29, 40, 40);
                bool current = SSCBondOverviewUtility.IsCurrent(snapshot, entry);
                if (!current) session.RequestRefresh(view);
                SSCBondOverviewWidgets.Portrait(portrait, entry, current);
                Widgets.Label(new Rect(portrait.x, portrait.y, 12, 16), master ? "↑" : "↓");
                if (current && Mouse.IsOver(portrait))
                {
                    string name = master ? display.MasterName : display.Names[index];
                    string info = master ? display.MasterDetails : display.Details[index];
                    SSCBondOverviewWidgets.Hover(portrait, entry,
                        SSCBondOverviewText.T(master ? "Master" : "Slave") + ": " + name, info);
                    if (SSCBondOverviewNavigation.CanPreview(entry.Location, out _) && leftClick) Remember(view, entry, false);
                }
            }
            if (capacity > 0 && snapshot?.Master != null && snapshot.TotalSlaveCount == 0)
            {
                float textWidth = Math.Max(1, rect.width - 62);
                Widgets.Label(new Rect(rect.x + 54, rect.y + 29, textWidth, 20), display.MasterName.Truncate(textWidth));
                Widgets.Label(new Rect(rect.x + 54, rect.y + 49, textWidth, 20), display.MasterDetails.Truncate(textWidth));
            }
            if (count == 0 || capacity == 0)
            {
                string status = view.Failed ? "QueryFailed" : snapshot == null || snapshot.State != SSCBondOverviewQueryState.Ready ? "Waiting"
                    : snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget ? "InvalidMaster"
                    : snapshot.Identity == PawnIdentity.Master ? "EmptySlaves" : "NoMaster";
                Widgets.Label(new Rect(rect.x + 6, rect.y + 32, Math.Max(1, rect.width - 12), 36),
                    SSCBondOverviewText.T(status).Truncate(Math.Max(1, rect.width - 12)));
            }
            if (view.Pending) TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("Waiting"));
            if (pendingSource != null) return new GizmoResult(GizmoState.Interacted, Event.current);
            return new GizmoResult(Mouse.IsOver(rect) ? GizmoState.Mouseover : GizmoState.Clear);
        }

        private void Remember(SSCBondOverviewView view, SSCBondOverviewEntry entry, bool details)
        { pendingSource = view.Source; pendingRevision = view.Revision; pendingSnapshot = view.Snapshot; pendingEntry = entry; pendingDetails = details; }

        public override void ProcessInput(Event ev)
        {
            try
            {
                SSCBondOverviewView view = session.Card;
                if (ev == null || ev.button != 0 || !GUI.enabled || Disabled || pendingSource == null ||
                    !ReferenceEquals(session.Owner, Current.Game) || !SSCBondOverviewAccess.IsCardSource(pendingSource) ||
                    !ReferenceEquals(view?.Source, pendingSource) || view.Revision != pendingRevision) return;
                if (pendingDetails) Dialog_SSCBondOverview.Open(session, pendingSource);
                else if (ReferenceEquals(view.Snapshot, pendingSnapshot) && pendingEntry != null &&
                    !SSCBondOverviewNavigation.TryNavigate(pendingSnapshot, pendingEntry, out SSCBondOverviewNavigationFailure failure))
                {
                    session.RequestRefresh(view);
                    Messages.Message(SSCBondOverviewText.T("Failure_" + failure), RimWorld.MessageTypeDefOf.RejectInput, false);
                }
            }
            finally { ClearPending(); }
        }
    }
}
