// AI 辅助生成。独立关系卡片、只读头像分页；输入经原版分派后再执行导航或显式指派。
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal enum SSCBondOverviewPanel { Slaves, Master, Trainer }
    internal sealed class Gizmo_SSCBondOverview : Gizmo
    {
        private readonly SSCBondOverviewSession session;
        internal readonly SSCBondOverviewPanel Panel;
        private SSCBondOverviewView pendingView;
        private SSCBondOverviewSnapshot pendingSnapshot;
        private SSCBondOverviewEntry pendingEntry;
        private int pendingRevision, pendingAction, pendingCapacity;
        internal Gizmo_SSCBondOverview(SSCBondOverviewSession session, SSCBondOverviewPanel panel = SSCBondOverviewPanel.Slaves)
        { this.session = session; Panel = panel; }
        public override float GetWidth(float maxWidth) => Math.Min(
            Panel == SSCBondOverviewPanel.Master ? 180 : Panel == SSCBondOverviewPanel.Trainer ? 220 : 300, Math.Max(1, maxWidth));
        public override bool GroupsWith(Gizmo other) => false;
        public override bool InheritInteractionsFrom(Gizmo other) => false;
        internal void ClearPending() { pendingView = null; pendingSnapshot = null; pendingEntry = null; pendingAction = 0; }
        internal void Clear() => ClearPending();

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            ClearPending();
            SSCBondOverviewView view = session.Card;
            if (!ReferenceEquals(session.Owner, Current.Game) || !SSCBondOverviewAccess.IsCardSource(view?.Source) || !view.ShowCard)
                return new GizmoResult(GizmoState.Clear);
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75);
            // 与原版机械师从属卡片共用命令背景纹理、边框及低亮度材质；不另画实心底框。
            Color oldColor = GUI.color;
            GUI.color = parms.lowLight ? Command.LowLightBgColor : Color.white;
            GenUI.DrawTextureWithMaterial(rect, parms.shrunk ? Command.BGTexShrunk : Command.BGTex,
                Disabled || parms.lowLight ? TexUI.GrayscaleGUI : null);
            GUI.color = oldColor;
            bool left = GUI.enabled && !Disabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
            if (rect.width < 100)
            {
                float inset = Math.Min(1, rect.width * .1f);
                Rect compact = new Rect(rect.x + inset, rect.y + 4, Math.Max(0, rect.width - 2 * inset), 65);
                Button(compact, SSCBondOverviewText.T("ViewAll"), 1);
                return Result();
            }
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            SSCBondOverviewDisplay display = view.Display;
            Rect all = new Rect(rect.xMax - 88, rect.y + 4, 84, 22);
            string heading = Panel == SSCBondOverviewPanel.Master ? SSCBondOverviewText.T("Master")
                : Panel == SSCBondOverviewPanel.Trainer ? SSCBondOverviewText.T("TrainerPanel")
                : display?.SlaveHeading ?? SSCBondOverviewText.T("Slave");
            Rect title = new Rect(rect.x + 6, rect.y + 4, Math.Max(1, all.x - rect.x - 12), 22);
            Widgets.Label(title, view.Truncate(heading, title.width));
            if (display != null && Mouse.IsOver(title)) TooltipHandler.TipRegion(title,
                Panel == SSCBondOverviewPanel.Slaves ? display.Count : heading);
            Button(all, SSCBondOverviewText.T("ViewAll"), 1);

            if (Panel == SSCBondOverviewPanel.Slaves)
            {
                int count = snapshot?.TotalSlaveCount ?? 0;
                int capacity = SSCBondOverviewLayout.CardCapacity(rect.width);
                if (count > capacity) capacity = SSCBondOverviewLayout.CardCapacity(Math.Max(0, rect.width - 64));
                int last = SSCBondOverviewLayout.LastPage(count, capacity);
                // 页号属于来源视图，不属于快照代次；刷新保留页，数量减少或窄栏改变时仅夹到有效页。
                view.Page = Math.Max(0, Math.Min(view.Page, last));
                if (last > 0)
                {
                    pendingCapacity = capacity;
                    Button(new Rect(rect.xMax - 62, rect.y + 30, 26, 20), "‹", 2, view.Page > 0);
                    Button(new Rect(rect.xMax - 30, rect.y + 30, 26, 20), "›", 3, view.Page < last);
                    Widgets.Label(new Rect(rect.xMax - 62, rect.y + 52, 58, 18), display.PageLabel(view.Page, last + 1));
                }
                int start = view.Page * capacity;
                for (int i = 0; i < capacity && start + i < count; i++)
                {
                    SSCBondOverviewEntry entry = snapshot.Slaves[start + i];
                    if (SSCBondOverviewWidgets.Avatar(new Rect(rect.x + 6 + i * 44, rect.y + 29, 40, 40), snapshot, entry, session, view) && left)
                        Remember(4, entry);
                }
                if (count == 0 || capacity == 0) Status(snapshot == null ? "Waiting" : count > 0 ? "ViewAll"
                    : snapshot.CoverageComplete ? "EmptySlaves" : "SupplementPending");
            }
            else
            {
                SSCBondOverviewEntry master = snapshot?.Master;
                Pawn trainer = snapshot?.AssignedTrainer;
                bool hasPerson = Panel == SSCBondOverviewPanel.Master ? master != null : trainer != null;
                if (hasPerson)
                {
                    Rect portrait = new Rect(rect.x + 6, rect.y + 29, 40, 40);
                    bool clicked = Panel == SSCBondOverviewPanel.Master
                        ? SSCBondOverviewWidgets.Avatar(portrait, snapshot, master, session, view)
                        : SSCBondOverviewWidgets.TrainerAvatar(portrait, snapshot, session, view);
                    if (clicked && left) Remember(Panel == SSCBondOverviewPanel.Master ? 4 : 5, master);
                    float width = Math.Max(1, rect.width - (Panel == SSCBondOverviewPanel.Trainer ? 136 : 62));
                    string name = Panel == SSCBondOverviewPanel.Master ? display.Row(master).Name : display.TrainerName;
                    Widgets.Label(new Rect(rect.x + 54, rect.y + 38, width, 24), view.Truncate(name, width));
                }
                else Status(snapshot == null ? "Waiting" : Panel == SSCBondOverviewPanel.Trainer ? "None"
                    : snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget ? "InvalidMaster" : "NoMaster");
                if (Panel == SSCBondOverviewPanel.Trainer)
                    Button(new Rect(rect.xMax - 76, rect.y + 36, 70, 28), SSCBondOverviewText.T("ChangeTrainer"), 6,
                        SSCIdentityUtility.IsSexSlave(view.Source));
            }
            if (Panel == SSCBondOverviewPanel.Slaves)
            {
                if (view.Failed) TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("QueryFailed"));
                else if (view.Pending) TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("SupplementPending"));
            }
            return Result();

            void Remember(int action, SSCBondOverviewEntry entry = null)
            { pendingView = view; pendingRevision = view.Revision; pendingSnapshot = view.Snapshot; pendingEntry = entry; pendingAction = action; }
            void Button(Rect area, string text, int action, bool enabled = true)
            {
                Color old = GUI.color;
                if (!enabled) GUI.color = new Color(.5f, .5f, .5f);
                Widgets.Label(area, view.Truncate(text, area.width));
                GUI.color = old;
                if (enabled && Mouse.IsOver(area)) { Widgets.DrawHighlight(area); if (left) Remember(action); }
            }
            void Status(string key)
            {
                float width = Math.Max(1, rect.width - (Panel == SSCBondOverviewPanel.Trainer ? 90 : 12));
                Widgets.Label(new Rect(rect.x + 6, rect.y + 36, width, 28), view.Truncate(SSCBondOverviewText.T(key), width));
            }
            GizmoResult Result() => pendingView != null ? new GizmoResult(GizmoState.Interacted, Event.current)
                : new GizmoResult(Mouse.IsOver(rect) ? GizmoState.Mouseover : GizmoState.Clear);
        }

        public override void ProcessInput(Event ev)
        {
            try
            {
                SSCBondOverviewView view = session.Card;
                if (ev == null || ev.button != 0 || !GUI.enabled || Disabled || pendingView == null ||
                    !ReferenceEquals(session.Owner, Current.Game) || !ReferenceEquals(view, pendingView) ||
                    !SSCBondOverviewAccess.IsCardSource(view.Source) || view.Revision != pendingRevision) return;
                // 翻页不 Build、不导航；其余动作在原版网格分派后执行一次，仍拒绝旧代次输入。
                if (pendingAction == 1) Dialog_SSCBondOverview.Open(session, view.Source);
                else if (pendingAction == 2 || pendingAction == 3)
                    view.Page = Math.Max(0, Math.Min(view.Page + (pendingAction == 2 ? -1 : 1),
                        SSCBondOverviewLayout.LastPage(view.Snapshot?.TotalSlaveCount ?? 0, pendingCapacity)));
                else if (pendingAction == 6) OpenTrainerMenu(session, view);
                else if (ReferenceEquals(view.Snapshot, pendingSnapshot))
                {
                    SSCBondOverviewNavigationFailure failure;
                    bool success = pendingAction == 5 ? SSCBondOverviewNavigation.TryNavigateTrainer(pendingSnapshot, out failure)
                        : SSCBondOverviewNavigation.TryNavigate(pendingSnapshot, pendingEntry, out failure);
                    if (!success) { session.RequestRefresh(view); Messages.Message(SSCBondOverviewText.T("Failure_" + failure), RimWorld.MessageTypeDefOf.RejectInput, false); }
                }
            }
            finally { ClearPending(); }
        }

        internal static void OpenTrainerMenu(SSCBondOverviewSession session, SSCBondOverviewView view)
        {
            if (!SSCBondOverviewAccess.IsCardSource(view.Source) || !SSCIdentityUtility.IsSexSlave(view.Source)) return;
            Find.WindowStack.Add(new FloatMenu(SSCTrainerAssignmentMenu.Options(view.Source, () =>
            {
                // 显式指派后只重读直接字段，其他有效关系继续显示；全局补漏仍不能绕过共享预算。
                session.RefreshLocal(view, true); session.RequestRefresh(view);
            }, () => ReferenceEquals(session.Owner, Current.Game) && (ReferenceEquals(session.Card, view) || ReferenceEquals(session.Details, view)) &&
                SSCBondOverviewAccess.IsCardSource(view.Source))));
        }
    }
}
