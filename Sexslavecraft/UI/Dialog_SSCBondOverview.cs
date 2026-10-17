// AI 辅助生成。紧凑头像总览；只绘制可见网格，结束遍历后再导航或打开显式指派菜单。
using System;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class Dialog_SSCBondOverview : Window
    {
        private readonly SSCBondOverviewSession session;
        internal Pawn Source { get; private set; }
        private Vector2 scroll;
        public override Vector2 InitialSize
        {
            get
            {
                SSCBondOverviewSnapshot snapshot = session.Card?.Snapshot;
                int rows = (snapshot?.TotalSlaveCount ?? 0) + 6;
                float height = 150 + Math.Min(4, rows / 7) * SSCBondOverviewLayout.RowHeight;
                if (snapshot?.Master != null) height += 108;
                if (ShowTrainer(snapshot)) height += 108;
                return new Vector2(Math.Max(1, Math.Min(680, UI.screenWidth - 32)),
                    Math.Max(1, Math.Min(Math.Max(240, height), UI.screenHeight - 32)));
            }
        }
        internal Dialog_SSCBondOverview(SSCBondOverviewSession session, Pawn source)
        {
            this.session = session; Source = source;
            optionalTitle = SSCBondOverviewText.Format("TitleSource", source.LabelShort);
            forcePause = false; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; closeOnAccept = false;
        }
        internal static void Open(SSCBondOverviewSession session, Pawn source)
        {
            if (!SSCBondOverviewAccess.IsCardSource(source)) return;
            var previous = Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            if (previous != null && ReferenceEquals(previous.session, session) && ReferenceEquals(previous.Source, source)) return;
            Find.WindowStack.Add(new Dialog_SSCBondOverview(session, source));
        }
        public override void PostOpen() { base.PostOpen(); session.OpenDetails(Source, Find.CurrentMap); }
        public override void PostClose()
        {
            session.ReleaseDetails(Source); Source = null; scroll = Vector2.zero;
            base.PostClose();
        }
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (!ReferenceEquals(session.Owner, Current.Game)) { session.Clear(); Close(); return; }
            // 总览跟随当前单选上下文；切换来源或来源失效就关闭，不再保留异源固定浏览器。
            if (!SSCBondOverviewAccess.IsCardSource(Source)) { Close(); return; }
            session.OpenDetails(Source, Find.CurrentMap);
        }
        private bool ShowTrainer(SSCBondOverviewSnapshot snapshot)
            => snapshot?.AssignedTrainer != null || SSCIdentityUtility.IsSexSlave(Source);

        public override void DoWindowContents(Rect inRect)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            if (!ReferenceEquals(session.Owner, Current.Game)) { session.Clear(); Close(); return; }
            if (!SSCBondOverviewAccess.IsCardSource(Source)) { Close(); return; }
            SSCBondOverviewView view = session.Details;
            if (!ReferenceEquals(view?.Source, Source)) return;
            SSCBondOverviewDisplay display = view.Display;
            SSCBondOverviewSnapshot snapshot = view.Snapshot;
            if (snapshot == null || snapshot.State != SSCBondOverviewQueryState.Ready)
            { Widgets.Label(inRect, SSCBondOverviewText.T(view.Failed ? "QueryFailed" : "Waiting")); return; }
            Rect viewport = new Rect(inRect.x, inRect.y, inRect.width, Math.Max(1, inRect.height - 44));
            float width = Math.Max(1, viewport.width - 20);
            const float header = SSCBondOverviewLayout.HeaderHeight, row = SSCBondOverviewLayout.RowHeight;
            bool master = snapshot.Master != null || snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget;
            bool trainer = ShowTrainer(snapshot), slaves = snapshot.TotalSlaveCount > 0 || snapshot.Identity == PawnIdentity.Master;
            float masterStart = header, trainerHeader = master ? header + row : 0;
            float trainerStart = trainerHeader + header;
            float slavesHeader = trainerHeader + (trainer ? header + row : 0), slavesStart = slavesHeader + header;
            int columns = SSCBondOverviewLayout.Columns(width);
            int rows = (snapshot.TotalSlaveCount + columns - 1) / columns;
            float total = slaves ? slavesStart + Math.Max(header, rows * row) : slavesHeader;
            scroll.y = Math.Max(0, Math.Min(scroll.y, Math.Max(0, total - viewport.height)));
            SSCBondOverviewEntry clicked = null;
            bool clickedTrainer = false, changeTrainer = false;
            Widgets.BeginScrollView(viewport, ref scroll, new Rect(0, 0, width, Math.Max(total, viewport.height)));
            try
            {
                if (master)
                {
                    Label(0, SSCBondOverviewText.T("Master"));
                    if (snapshot.Master != null && Visible(masterStart, row))
                    {
                        if (SSCBondOverviewWidgets.Avatar(new Rect(4, masterStart, 56, 56), snapshot, snapshot.Master, session, view, true)) clicked = snapshot.Master;
                    }
                    else if (snapshot.Master == null) Label(masterStart, SSCBondOverviewText.T("InvalidMaster"));
                }
                if (trainer)
                {
                    Label(trainerHeader, SSCBondOverviewText.T("TrainerPanel"), Math.Max(1, width - Math.Min(width, 100) - 8));
                    if (Visible(trainerHeader, header) && SSCUIWidgets.Button(new Rect(Math.Max(0, width - 100), trainerHeader, Math.Min(width, 100), 24),
                        SSCBondOverviewText.T("ChangeTrainer"), SSCIdentityUtility.IsSexSlave(Source))) changeTrainer = true;
                    if (Visible(trainerStart, row))
                    {
                        if (snapshot.AssignedTrainer != null)
                            clickedTrainer = SSCBondOverviewWidgets.TrainerAvatar(new Rect(4, trainerStart, 56, 56), snapshot, session, view, true);
                        else Label(trainerStart, SSCBondOverviewText.T("None"));
                    }
                }
                if (slaves)
                {
                    Label(slavesHeader, display.SlaveHeading);
                    if (snapshot.TotalSlaveCount == 0) Label(slavesStart,
                        SSCBondOverviewText.T(snapshot.CoverageComplete ? "EmptySlaves" : "SupplementPending"));
                    // 先算可见网格行，再直接索引完整快照；不枚举或格式化离屏头像。
                    SSCBondOverviewLayout.VisibleRange(rows, slavesStart, scroll.y, viewport.height, out int first, out int end);
                    for (int r = first; r < end; r++)
                        for (int c = 0; c < columns; c++)
                        {
                            int index = r * columns + c;
                            if (index >= snapshot.TotalSlaveCount) break;
                            SSCBondOverviewEntry entry = snapshot.Slaves[index];
                            if (SSCBondOverviewWidgets.Avatar(new Rect(4 + c * SSCBondOverviewLayout.CellWidth, slavesStart + r * row, 56, 56),
                                snapshot, entry, session, view, true)) clicked = entry;
                        }
                }
            }
            finally { Widgets.EndScrollView(); }
            if (changeTrainer) Gizmo_SSCBondOverview.OpenTrainerMenu(session, view);
            else if (clicked != null || clickedTrainer)
            {
                SSCBondOverviewNavigationFailure failure;
                bool success = clickedTrainer ? SSCBondOverviewNavigation.TryNavigateTrainer(snapshot, out failure)
                    : SSCBondOverviewNavigation.TryNavigate(snapshot, clicked, out failure);
                if (success) Close();
                else { session.RequestRefresh(view); Messages.Message(SSCBondOverviewText.T("Failure_" + failure), RimWorld.MessageTypeDefOf.RejectInput, false); }
            }
            bool Visible(float y, float height) => y + height >= scroll.y && y <= scroll.y + viewport.height;
            void Label(float y, string text, float labelWidth = -1)
            {
                if (!Visible(y, header)) return;
                float available = labelWidth < 0 ? width : labelWidth;
                Rect rect = new Rect(0, y, available, header);
                Widgets.Label(rect, view.Truncate(text, available));
                if (Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, y == slavesHeader && slaves ? display.Count : text);
            }
        }
    }
}
