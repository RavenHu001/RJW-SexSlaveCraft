// AI 辅助生成。固定来源的只读窗口，按可见索引绘制，点击结束后统一执行导航。
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
        private string failureMessage;
        public override Vector2 InitialSize => new Vector2(Math.Max(1, Math.Min(880, UI.screenWidth - 32)),
            Math.Max(1, Math.Min(640, UI.screenHeight - 32)));

        internal Dialog_SSCBondOverview(SSCBondOverviewSession session, Pawn source)
        {
            this.session = session; Source = source;
            optionalTitle = SSCBondOverviewText.T("Title");
            forcePause = false; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; closeOnAccept = false;
        }

        internal static void Open(SSCBondOverviewSession session, Pawn source)
        {
            var previous = Find.WindowStack.WindowOfType<Dialog_SSCBondOverview>();
            if (previous != null && ReferenceEquals(previous.session, session) && ReferenceEquals(previous.Source, source)) return;
            Find.WindowStack.Add(new Dialog_SSCBondOverview(session, source));
        }

        public override void PostOpen()
        { base.PostOpen(); session.OpenDetails(Source, Find.CurrentMap); }
        public override void PostClose()
        {
            // 按来源退订，不能让被替换旧窗口的 PostClose 清掉后来打开的异源窗口。
            session.ReleaseDetails(Source); Source = null; failureMessage = null; scroll = Vector2.zero;
            base.PostClose();
        }
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (!ReferenceEquals(session.Owner, Current.Game)) { session.Clear(); Close(); return; }
            if (SSCBondOverviewUtility.IsValidPawn(Source)) session.OpenDetails(Source, Find.CurrentMap);
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            if (!ReferenceEquals(session.Owner, Current.Game) || Source == null) { session.Clear(); Close(); return; }
            SSCBondOverviewView view = session.Details;
            if (!ReferenceEquals(view?.Source, Source)) return;
            SSCBondOverviewDisplay display = view.Display;
            SSCBondOverviewSnapshot snapshot = display?.Snapshot;
            float refreshWidth = Math.Min(110, Math.Max(1, inRect.width * .24f));
            Rect refresh = new Rect(inRect.xMax - refreshWidth, inRect.y, refreshWidth, 30);
            string title = display?.Title ?? SSCBondOverviewText.Format("TitleSource", Source.LabelShort);
            Widgets.Label(new Rect(inRect.x, inRect.y, Math.Max(1, refresh.x - inRect.x - 8), 30), title);
            if (SSCUIWidgets.Button(refresh, SSCBondOverviewText.T("Refresh"), SSCBondOverviewUtility.IsValidPawn(Source)))
            { session.RequestRefresh(view); failureMessage = null; }
            string status = failureMessage ?? (!SSCBondOverviewUtility.IsValidPawn(Source) ? SSCBondOverviewText.T("Failure_SourceInvalid")
                : view.Failed ? SSCBondOverviewText.T("QueryFailed") : snapshot == null || snapshot.State != SSCBondOverviewQueryState.Ready
                    ? SSCBondOverviewText.T("Waiting") : display.Count + (view.Pending ? " · " + SSCBondOverviewText.T("Waiting") : ""));
            Rect statusRect = new Rect(inRect.x, inRect.y + 34, inRect.width, 26);
            Widgets.Label(statusRect, status.Truncate(statusRect.width));
            if (Mouse.IsOver(statusRect)) TooltipHandler.TipRegion(statusRect, status);
            if (!SSCBondOverviewUtility.IsValidPawn(Source) || snapshot == null || snapshot.State != SSCBondOverviewQueryState.Ready) return;

            Rect viewport = new Rect(inRect.x, inRect.y + 66, inRect.width, Math.Max(1, inRect.height - 118));
            float width = Math.Max(1, viewport.width - 20);
            const float row = SSCBondOverviewLayout.RowHeight, header = SSCBondOverviewLayout.HeaderHeight;
            float masterStart = header, trainerY = masterStart + (snapshot.Master == null ? header : row);
            float aliveHeader = trainerY + header, aliveStart = aliveHeader + header;
            int living = snapshot.LivingSlaveCount, dead = snapshot.TotalSlaveCount - living;
            float deadHeader = aliveStart + living * row + (snapshot.TotalSlaveCount == 0 ? header : 0), deadStart = deadHeader + header;
            float total = dead > 0 ? deadStart + dead * row : deadHeader;
            scroll.y = Math.Max(0, Math.Min(scroll.y, Math.Max(0, total - viewport.height)));
            SSCBondOverviewEntry clicked = null;
            Widgets.BeginScrollView(viewport, ref scroll, new Rect(0, 0, width, Math.Max(viewport.height, total)));
            try
            {
                Label(0, SSCBondOverviewText.T("Master"));
                if (snapshot.Master != null)
                {
                    if (Visible(masterStart, row) && SSCBondOverviewWidgets.Row(new Rect(0, masterStart, width, row), snapshot,
                        snapshot.Master, display.MasterName, display.MasterDetails, session, view)) clicked = snapshot.Master;
                }
                else Label(masterStart, SSCBondOverviewText.T(snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget ? "InvalidMaster" : "NoMaster"));
                Label(trainerY, display.Trainer);
                Label(aliveHeader, SSCBondOverviewText.T("LivingSlaves"));
                if (snapshot.TotalSlaveCount == 0) Label(aliveStart, SSCBondOverviewText.T("EmptySlaves"));
                DrawGroup(0, living, aliveStart);
                if (dead > 0) { Label(deadHeader, SSCBondOverviewText.T("DeadSlaves")); DrawGroup(living, dead, deadStart); }
            }
            finally { Widgets.EndScrollView(); }

            // 结束滚动区后才跳转或关闭，避免重入、切图或销毁窗口打断行遍历。
            if (clicked != null)
            {
                if (SSCBondOverviewNavigation.TryNavigate(snapshot, clicked, out SSCBondOverviewNavigationFailure failure)) Close();
                else { failureMessage = SSCBondOverviewText.T("Failure_" + failure); session.RequestRefresh(view); }
            }
            bool Visible(float y, float height) => y + height >= scroll.y && y <= scroll.y + viewport.height;
            void Label(float y, string text)
            {
                if (!Visible(y, header)) return;
                Rect rect = new Rect(0, y, width, header);
                Widgets.Label(rect, text.Truncate(width));
                if (Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, text);
            }
            void DrawGroup(int offset, int count, float start)
            {
                SSCBondOverviewLayout.VisibleRange(count, start, scroll.y, viewport.height, out int first, out int end);
                for (int i = first; i < end; i++)
                {
                    int index = offset + i;
                    if (SSCBondOverviewWidgets.Row(new Rect(0, start + i * row, width, row), snapshot,
                        snapshot.Slaves[index], display.Names[index], display.Details[index], session, view)) clicked = snapshot.Slaves[index];
                }
            }
        }
    }
}
