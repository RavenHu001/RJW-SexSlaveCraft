// AI 辅助生成。独立关系卡片、只读头像网格；输入经原版分派后再执行导航或显式指派。
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal enum SSCBondOverviewPanel { Slaves, Master, Trainer, SelectAll }
    internal sealed class Gizmo_SSCBondOverview : Gizmo
    {
        private readonly SSCBondOverviewSession session;
        internal readonly SSCBondOverviewPanel Panel;
        private SSCBondOverviewView pendingView;
        private SSCBondOverviewSnapshot pendingSnapshot;
        private SSCBondOverviewEntry pendingEntry;
        private int pendingRevision, pendingAction;
        // 跨按下／松开只保存动作和当前游戏内的角色 ID，不额外持有来源 Pawn 或旧视图。
        private int pressedAction, pressedSourceId;
        internal Gizmo_SSCBondOverview(SSCBondOverviewSession session, SSCBondOverviewPanel panel = SSCBondOverviewPanel.Slaves)
        { this.session = session; Panel = panel; }
        public override float GetWidth(float maxWidth) => Math.Min(
            Panel == SSCBondOverviewPanel.Master || Panel == SSCBondOverviewPanel.SelectAll ? 75 : 130, Math.Max(1, maxWidth));
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
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Panel == SSCBondOverviewPanel.Master || Panel == SSCBondOverviewPanel.SelectAll ? GetWidth(maxWidth) : 75);
            // 与原版机械师从属卡片共用命令背景纹理、边框及低亮度材质；不另画实心底框。
            Color oldColor = GUI.color;
            GUI.color = parms.lowLight ? Command.LowLightBgColor : Color.white;
            GenUI.DrawTextureWithMaterial(rect, parms.shrunk ? Command.BGTexShrunk : Command.BGTex,
                Disabled || parms.lowLight ? TexUI.GrayscaleGUI : null);
            GUI.color = oldColor;
            bool left = GUI.enabled && !Disabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            SSCBondOverviewDisplay display = view.Display;
            if (Panel == SSCBondOverviewPanel.SelectAll)
            {
                // 独立命令紧邻性奴概览；沿原版按钮的按下捕获／松开成功时序，不在绘制时枚举候选。
                Press(rect, 7, true);
                bool leftUp = GUI.enabled && !Disabled && Event.current.type == EventType.mouseUp && Event.current.button == 0;
                if (GUI.enabled && !Disabled && Widgets.ButtonInvisible(rect) && leftUp && SamePress(7)) Remember(7);
                float icon = Math.Min(52, Math.Max(0, rect.width - 12));
                GUI.DrawTexture(new Rect(rect.x + (rect.width - icon) * .5f, rect.y + 5, icon, icon), TexButton.SelectOverlappingNext);
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleCenter; Text.WordWrap = true;
                Widgets.Label(new Rect(rect.x + 3, rect.yMax - 29, Math.Max(1, rect.width - 6), 27), SSCBondOverviewText.T("SelectAllSlaves"));
                if (Mouse.IsOver(rect)) { Widgets.DrawHighlight(rect); TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("SelectAllSlavesDesc")); }
                return Result();
            }
            if (Panel == SSCBondOverviewPanel.Master)
            {
                // 唯一主人改为方形头像命令；整块矩形共享头像的现时关系校验和定位／选中。
                // 不显示姓名，不添加总览或改主入口，完整姓名与位置仍可悬停查看。
                if (snapshot?.Master != null)
                {
                    if (rect.height >= 68)
                    {
                        if (SSCBondOverviewWidgets.Avatar(new Rect(rect.x + (rect.width - 56) * .5f, rect.y + 5, 56, 56),
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
                            SSCBondOverviewWidgets.Hover(rect, snapshot.Master, text.Tooltip);
                            if (left) Remember(4, snapshot.Master);
                        }
                    }
                }
                else
                {
                    Widgets.Label(new Rect(rect.x + 4, rect.y + 27, Math.Max(1, rect.width - 8), 24), "!");
                    TooltipHandler.TipRegion(rect, SSCBondOverviewText.T(snapshot == null ? "Waiting" : "InvalidMaster"));
                }
                Text.Anchor = TextAnchor.MiddleCenter;
                // 底部命令文字在头像之后绘制，允许覆盖人物下半部，保持示例中的命令按钮构图。
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x + 3, rect.yMax - 23, Math.Max(1, rect.width - 6), 22),
                    view.Truncate(SSCBondOverviewText.T("SelectMaster"), Math.Max(1, rect.width - 6)));
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
            // 顶部与原版机械师一致使用 Small；完整内容保留在悬停，不再以更小字号换取空间。
            Text.Font = GameFont.Small;
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
                // 一次容纳全部已知成员，不分页；布局按快照／卡片尺寸缓存，不在重绘中分配格子列表。
                Rect area = new Rect(rect.x + 6, rect.y + 36, Math.Max(0, rect.width - 12), 33);
                var grid = display?.Grid(area.width, area.height) ?? default;
                float firstX = area.x + (area.width - grid.Columns * grid.Side) * .5f;
                float firstY = area.y + (area.height - grid.Rows * grid.Side) * .5f;
                for (int i = 0; i < count && grid.Columns > 0; i++)
                {
                    SSCBondOverviewEntry entry = snapshot.Slaves[i];
                    Rect portrait = new Rect(firstX + (i % grid.Columns) * grid.Side, firstY + (i / grid.Columns) * grid.Side, grid.Side, grid.Side);
                    if (SSCBondOverviewWidgets.Avatar(portrait, snapshot, entry, session, view) && left) Remember(4, entry);
                }
                if (count == 0) Status(snapshot == null ? "Waiting" : "EmptySlaves");
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
            }
            return Result();

            void Remember(int action, SSCBondOverviewEntry entry = null)
            { pendingView = view; pendingRevision = view.Revision; pendingSnapshot = view.Snapshot; pendingEntry = entry; pendingAction = action; }
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
                // 所有动作在原版网格分派后执行一次，仍拒绝旧来源／代次；批选不调用世界补漏。
                if (pendingAction == 1) Dialog_SSCBondOverview.Open(session, view.Source);
                else if (pendingAction == 7) SelectAllCurrentMap(view.Source);
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

        private static bool CanSelect(Pawn pawn, Pawn owner, Map map)
            => SSCBondOverviewUtility.IsValidPawn(pawn) && !ReferenceEquals(pawn, owner) && !pawn.Dead && pawn.Spawned &&
                ReferenceEquals(pawn.Map, map) && pawn.def.selectable && ReferenceEquals(SSCBondUtility.GetBoundMaster(pawn), owner);

        private static void SelectAllCurrentMap(Pawn source)
        {
            // 用户点击时只枚举当前地图已生成角色，以现时锁链补齐尚未进入显示快照的漏索引成员。
            // 不扫描世界／货物，不清理绑定；先物化结果，避免选择通知修改原版临时集合。
            Map map = Find.CurrentMap; Game game = Current.Game;
            var candidates = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned) if (CanSelect(pawn, source, map)) candidates.Add(pawn);
            if (candidates.Count == 0)
            { Messages.Message(SSCBondOverviewText.T("NoSelectableSlaves"), RimWorld.MessageTypeDefOf.RejectInput, false); return; }
            Find.Selector.ClearSelection();
            foreach (Pawn pawn in candidates)
            {
                // 原版 Select 的通知可能触发其他 mod；逐人复查，且不允许选择过程跨游戏或切地图。
                if (!ReferenceEquals(game, Current.Game) || !ReferenceEquals(map, Find.CurrentMap)) break;
                if (SSCBondOverviewUtility.IsValidPawn(source) && CanSelect(pawn, source, map))
                    Find.Selector.Select(pawn, playSound: Find.Selector.NumSelected == 0, forceDesignatorDeselect: Find.Selector.NumSelected == 0);
            }
            // 原版 Select 负责自身的 200 上限。无对象成功时保留来源，避免留下意外的空选择。
            if (ReferenceEquals(game, Current.Game) && ReferenceEquals(map, Find.CurrentMap) && Find.Selector.NumSelected == 0 &&
                SSCBondOverviewUtility.IsValidPawn(source) && source.Spawned && ReferenceEquals(source.Map, map)) Find.Selector.Select(source);
        }

        internal static void OpenTrainerMenu(SSCBondOverviewSession session, SSCBondOverviewView view)
        {
            if (!SSCBondOverviewAccess.IsCardSource(view.Source) || !SSCIdentityUtility.IsSexSlave(view.Source)) return;
            Find.WindowStack.Add(new FloatMenu(SSCTrainerAssignmentMenu.Options(view.Source, () =>
            {
                // 显式指派后只重读直接字段，其他有效关系继续显示；只提交局部刷新请求，不枚举全局候选。
                session.RefreshLocal(view, true); session.RequestRefresh(view);
            }, () => ReferenceEquals(session.Owner, Current.Game) && (ReferenceEquals(session.Card, view) || ReferenceEquals(session.Details, view)) &&
                SSCBondOverviewAccess.IsCardSource(view.Source))));
        }
    }
}
