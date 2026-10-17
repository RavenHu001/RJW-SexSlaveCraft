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
        // 跨按下／松开只保存动作和当前游戏内的角色 ID，不额外持有来源 Pawn 或旧视图。
        private int pressedAction, pressedSourceId;
        internal Gizmo_SSCBondOverview(SSCBondOverviewSession session, SSCBondOverviewPanel panel = SSCBondOverviewPanel.Slaves)
        { this.session = session; Panel = panel; }
        public override float GetWidth(float maxWidth) => Math.Min(
            Panel == SSCBondOverviewPanel.Master ? 75 : 130, Math.Max(1, maxWidth));
        public override bool GroupsWith(Gizmo other) => false;
        public override bool InheritInteractionsFrom(Gizmo other) => false;
        internal void ClearPending() { pendingView = null; pendingSnapshot = null; pendingEntry = null; pendingAction = 0; }
        internal void Clear() { ClearPending(); pressedAction = pressedSourceId = 0; }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            ClearPending();
            if (Event.current.type == EventType.mouseDown) pressedAction = pressedSourceId = 0;
            bool released = Event.current.type == EventType.mouseUp;
            SSCBondOverviewView view = session.Card;
            if (!ReferenceEquals(session.Owner, Current.Game) || !SSCBondOverviewAccess.IsCardSource(view?.Source) || !view.ShowCard)
                return new GizmoResult(GizmoState.Clear);
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Panel == SSCBondOverviewPanel.Master ? GetWidth(maxWidth) : 75);
            // 与原版机械师从属卡片共用命令背景纹理、边框及低亮度材质；不另画实心底框。
            Color oldColor = GUI.color;
            GUI.color = parms.lowLight ? Command.LowLightBgColor : Color.white;
            GenUI.DrawTextureWithMaterial(rect, parms.shrunk ? Command.BGTexShrunk : Command.BGTex,
                Disabled || parms.lowLight ? TexUI.GrayscaleGUI : null);
            GUI.color = oldColor;
            bool left = GUI.enabled && !Disabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            SSCBondOverviewDisplay display = view.Display;
            if (Panel == SSCBondOverviewPanel.Master)
            {
                // 唯一主人改为方形头像命令；整块矩形共享头像的现时关系校验和定位／选中。
                // 不显示姓名，不添加总览或改主入口，完整姓名与位置仍可悬停查看。
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + 3, rect.y + 3, Math.Max(1, rect.width - 6), 20),
                    view.Truncate(SSCBondOverviewText.T("Master"), Math.Max(1, rect.width - 6)));
                Text.Anchor = TextAnchor.UpperLeft;
                if (snapshot?.Master != null)
                {
                    if (rect.height >= 68)
                    {
                        if (SSCBondOverviewWidgets.Avatar(new Rect(rect.x + (rect.width - 40) * .5f, rect.y + 27, 40, 40),
                            snapshot, snapshot.Master, session, view, interactionRect: rect) && left) Remember(4, snapshot.Master);
                    }
                    else
                    {
                        // 极窄方块只显示方向，不生成第三档肖像；仍保留整块悬停与安全导航。
                        bool current = SSCBondOverviewUtility.IsCurrent(snapshot, snapshot.Master);
                        if (!current) session.RequestRefresh(view);
                        if (current && Mouse.IsOver(rect))
                        {
                            var text = display.Row(snapshot.Master);
                            SSCBondOverviewWidgets.Hover(rect, snapshot.Master, text.Name, text.Details);
                            if (left) Remember(4, snapshot.Master);
                        }
                    }
                }
                else
                {
                    Widgets.Label(new Rect(rect.x + 4, rect.y + 27, Math.Max(1, rect.width - 8), 24), "!");
                    TooltipHandler.TipRegion(rect, SSCBondOverviewText.T(snapshot == null ? "Waiting" : "InvalidMaster"));
                }
                return Result();
            }
            if (rect.width < 100)
            {
                float size = Math.Max(0, Math.Min(28, rect.width - 2));
                Icon(new Rect(rect.x + 1, rect.y + 4, size, 28), TexButton.Search, "ViewAll", 1);
                if (Panel == SSCBondOverviewPanel.Trainer)
                    Icon(new Rect(rect.x + 1, rect.y + 39, size, 28), TexButton.Rename, "ChangeTrainer", 6, SSCIdentityUtility.IsSexSlave(view.Source));
                return Result();
            }

            Rect all = new Rect(rect.xMax - 28, rect.y + 4, 24, 24);
            bool slaves = Panel == SSCBondOverviewPanel.Slaves;
            string heading = slaves ? display?.OwnedHeading ?? SSCBondOverviewText.Format("OwnedCount", "?")
                : SSCBondOverviewText.T("TrainerPanel");
            // 数量与“拥有性奴”合成完整顶部文案；小字体为原版宽度内的图标入口留出空间。
            Text.Font = slaves ? GameFont.Tiny : GameFont.Small;
            Rect title = new Rect(rect.x + 6, rect.y + 4, Math.Max(1, rect.width - (slaves ? 38 : 66)), 22);
            Widgets.Label(title, view.Truncate(heading, title.width));
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(title)) TooltipHandler.TipRegion(title, slaves ? display?.OwnedDetails ?? heading : heading);
            if (!slaves) Icon(new Rect(rect.xMax - 56, rect.y + 4, 24, 24), TexButton.Rename, "ChangeTrainer", 6,
                SSCIdentityUtility.IsSexSlave(view.Source));
            Icon(all, TexButton.Search, "ViewAll", 1);

            if (slaves)
            {
                int count = snapshot?.TotalSlaveCount ?? 0;
                int capacity = SSCBondOverviewLayout.CardCapacity(rect.width);
                if (count > capacity) capacity = SSCBondOverviewLayout.CardCapacity(rect.width, true);
                int last = SSCBondOverviewLayout.LastPage(count, capacity);
                view.Page = Math.Max(0, Math.Min(view.Page, last));
                if (last > 0)
                {
                    pendingCapacity = capacity;
                    Rect previous = new Rect(rect.x + 2, rect.y + 36, 20, 28);
                    Rect next = new Rect(rect.xMax - 22, rect.y + 36, 20, 28);
                    Button(previous, "‹", 2, view.Page > 0);
                    Button(next, "›", 3, view.Page < last);
                    // 页码移到箭头悬停，避免 75 高卡片内覆盖头像；完整名单仍逐页可达。
                    string page = display.PageLabel(view.Page, last + 1);
                    TooltipHandler.TipRegion(previous, page); TooltipHandler.TipRegion(next, page);
                }
                int start = view.Page * capacity, shown = Math.Min(capacity, count - start);
                float first = rect.x + (rect.width - (shown * 40 + Math.Max(0, shown - 1) * 4)) * .5f;
                for (int i = 0; i < shown; i++)
                {
                    SSCBondOverviewEntry entry = snapshot.Slaves[start + i];
                    if (SSCBondOverviewWidgets.Avatar(new Rect(first + i * 44, rect.y + 29, 40, 40),
                        snapshot, entry, session, view) && left) Remember(4, entry);
                }
                if (count == 0 || capacity == 0) Status(snapshot == null ? "Waiting" : count > 0 ? "ViewAll"
                    : snapshot.CoverageComplete ? "EmptySlaves" : "SupplementPending");
            }
            else
            {
                // 单个调教员与原版少量机械体一样居中排列，不在头像旁常驻姓名。
                if (snapshot?.AssignedTrainer != null)
                {
                    if (SSCBondOverviewWidgets.TrainerAvatar(new Rect(rect.x + (rect.width - 40) * .5f, rect.y + 29, 40, 40),
                        snapshot, session, view) && left) Remember(5);
                }
                else Status(snapshot == null ? "Waiting" : "None");
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
                // 分页箭头使用原版按钮底框；不再把箭头作为无边界文本命中区。
                Press(area, action, enabled);
                bool leftUp = GUI.enabled && Event.current.type == EventType.mouseUp && Event.current.button == 0;
                if (SSCUIWidgets.Button(area, text, enabled && !Disabled) && leftUp && SamePress(action)) Remember(action);
            }
            void Icon(Rect area, Texture2D texture, string tip, int action, bool enabled = true)
            {
                Press(area, action, enabled);
                if (SSCBondOverviewWidgets.IconButton(area, texture, SSCBondOverviewText.T(tip), enabled && !Disabled) && SamePress(action)) Remember(action);
            }
            void Press(Rect area, int action, bool enabled)
            { if (left && enabled && Mouse.IsOver(area)) { pressedAction = action; pressedSourceId = view.Source.thingIDNumber; } }
            // 原版热按钮只认矩形；换人后即使新卡片出现在同一位置，也不能接收旧来源的松开。
            bool SamePress(int action) => pressedAction == action && pressedSourceId == view.Source.thingIDNumber;
            void Status(string key)
            {
                float width = Math.Max(1, rect.width - 12);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + 6, rect.y + 36, width, 28), view.Truncate(SSCBondOverviewText.T(key), width));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            GizmoResult Result()
            {
                if (released) pressedAction = pressedSourceId = 0;
                return pendingView != null ? new GizmoResult(GizmoState.Interacted, Event.current)
                    : new GizmoResult(Mouse.IsOver(rect) ? GizmoState.Mouseover : GizmoState.Clear);
            }
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
