// AI 辅助生成。方形关系命令复用原版绘制与分派，鼠标请求绑定按下时的视图。
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class Command_SSCBondSelection : Command_Action
    {
        private readonly SSCBondOverviewSession session;
        private SSCBondOverviewView view;
        private readonly bool selectMaster;
        private (SSCBondOverviewSnapshot Snapshot, int Revision)? pressed;
        private (SSCBondOverviewSnapshot Snapshot, int Revision)? pending;
        private Event pendingEvent;

        internal Command_SSCBondSelection(SSCBondOverviewSession session, bool selectMaster)
        {
            this.session = session;
            view = session.Card;
            this.selectMaster = selectMaster;
            defaultLabel = Label;
            if (!selectMaster) icon = TexButton.SelectOverlappingNext;
            groupable = false;
            action = Select;
        }

        public override bool Visible => SSCBondOverviewAccess.IsCurrentCard(session, view);
        public override string Label => SSCBondOverviewText.T(selectMaster ? "SelectMaster" : "SelectAllSlaves");
        public override float GetWidth(float maxWidth) => Math.Min(base.GetWidth(maxWidth), Math.Max(1f, maxWidth));

        public override string Desc
        {
            get
            {
                if (!selectMaster) return SSCBondOverviewText.T("SelectAllSlavesDesc");
                var snapshot = view?.Snapshot;
                var master = snapshot?.Master;
                if (master == null) return SSCBondOverviewText.T(snapshot == null ? "Waiting" : "InvalidMaster");
                if (!SSCBondOverviewUtility.IsCurrent(snapshot, master)) return SSCBondOverviewText.T("RelationChanged");
                string tooltip = view.Display.Row(master).Tooltip;
                return SSCBondOverviewNavigation.CanPreview(master.Location, out _)
                    ? tooltip : tooltip + "\n" + SSCBondOverviewText.T("ResolveOnClick");
            }
        }

        protected override GizmoResult GizmoOnGUIInt(Rect rect, GizmoRenderParms parms)
        {
            ClearPending();
            if (!Visible)
            {
                pressed = null;
                return new GizmoResult(GizmoState.Clear);
            }

            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            rect.height = Math.Min(rect.height, rect.width);
            Event input = Event.current;
            EventType phase = input.type;
            if (phase == EventType.repaint) session.MarkCardRepainted(view, Time.frameCount);
            bool canSelect = GUI.enabled && !Disabled && (!selectMaster || view.Snapshot?.Master != null);
            if (phase == EventType.mouseDown)
            {
                pressed = canSelect && input.button == 0 && Mouse.IsOver(rect) ? (view.Snapshot, view.Revision) : null;
            }

            // Waylith 的按钮只按矩形及鼠标键匹配松开。原版成功还须对应这个控件自己的按下。
            GizmoResult result = base.GizmoOnGUIInt(rect, parms);
            if (phase == EventType.mouseDown && input.type != EventType.used) pressed = null;
            if (phase == EventType.mouseUp)
            {
                if (result.State == GizmoState.Interacted && canSelect && input.button == 0 &&
                    pressed is { } request && request.Revision == view.Revision)
                {
                    pending = request;
                    pendingEvent = result.InteractEvent;
                }
                pressed = null;
            }
            return result.State == GizmoState.Interacted && pendingEvent == null
                ? new GizmoResult(GizmoState.Mouseover) : result;
        }

        public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            if (!selectMaster)
            {
                base.DrawIcon(rect, buttonMat, parms);
                return;
            }

            var snapshot = view?.Snapshot;
            var master = snapshot?.Master;
            if (master == null)
            {
                Widgets.Label(new Rect(rect.x + 4f, rect.y + 27f, Math.Max(1f, rect.width - 8f), 24f), "!");
                return;
            }

            bool current = SSCBondOverviewUtility.IsCurrent(snapshot, master);
            if (!current) session.RequestRefresh(view);
            GUI.color = parms.lowLight ? LowLightIconColor : Color.white;
            SSCBondOverviewWidgets.Portrait(rect, master, current);
            GUI.color = Color.white;
            SSCBondOverviewWidgets.Badge(rect, master.Location, current);
            if (current && Mouse.IsOver(rect)) SSCBondOverviewNavigation.Highlight(master.Location);
        }

        public override void ProcessInput(Event ev)
        {
            if (ev == null || !ReferenceEquals(ev, pendingEvent)) return;
            try { base.ProcessInput(ev); }
            finally { ClearPending(); }
        }

        private void Select()
        {
            var request = pending;
            ClearPending();
            if (!request.HasValue || !Visible || !GUI.enabled || Disabled || request.Value.Revision != view.Revision) return;

            if (!selectMaster)
            {
                SelectAllCurrentMap(view.Source);
                return;
            }
            var snapshot = request.Value.Snapshot;
            if (!SSCBondOverviewNavigation.TryNavigate(snapshot, snapshot?.Master, out var failure))
            {
                session.RequestRefresh(view);
                Messages.Message(SSCBondOverviewText.T("Failure_" + failure), MessageTypeDefOf.RejectInput, false);
            }
        }

        internal void ClearPending()
        {
            pending = null;
            pendingEvent = null;
        }

        internal void Clear()
        {
            ClearPending();
            pressed = null;
            view = null;
        }

        private static bool CanSelect(Pawn pawn, Pawn owner, Map map)
            => SSCBondOverviewUtility.IsValidPawn(pawn) && !ReferenceEquals(pawn, owner) && !pawn.Dead && pawn.Spawned &&
                ReferenceEquals(pawn.Map, map) && pawn.def.selectable && ReferenceEquals(SSCBondUtility.GetBoundMaster(pawn), owner);

        private static void SelectAllCurrentMap(Pawn source)
        {
            // 用户点击时只枚举当前地图已生成角色，以现时锁链补齐尚未进入显示快照的漏索引成员。
            // 先物化结果，避免选择通知修改原版临时集合。
            Map map = Find.CurrentMap;
            Game game = Current.Game;
            var candidates = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (CanSelect(pawn, source, map)) candidates.Add(pawn);
            if (candidates.Count == 0)
            {
                Messages.Message(SSCBondOverviewText.T("NoSelectableSlaves"), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.Selector.ClearSelection();
            foreach (Pawn pawn in candidates)
            {
                if (!ReferenceEquals(game, Current.Game) || !ReferenceEquals(map, Find.CurrentMap)) break;
                if (SSCBondOverviewUtility.IsValidPawn(source) && CanSelect(pawn, source, map))
                    Find.Selector.Select(pawn, playSound: Find.Selector.NumSelected == 0, forceDesignatorDeselect: Find.Selector.NumSelected == 0);
            }
            // 原版 Select 负责自身的 200 上限。无对象成功时保留来源，避免留下意外的空选择。
            if (ReferenceEquals(game, Current.Game) && ReferenceEquals(map, Find.CurrentMap) && Find.Selector.NumSelected == 0 &&
                SSCBondOverviewUtility.IsValidPawn(source) && source.Spawned && ReferenceEquals(source.Map, map)) Find.Selector.Select(source);
        }
    }
}
