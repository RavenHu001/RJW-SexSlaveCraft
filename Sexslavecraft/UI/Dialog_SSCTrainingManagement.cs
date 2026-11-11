// AI 辅助生成
using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>管理原版地图当前单选角色的调教概况和关系。</summary>
    internal sealed partial class Dialog_SSCTrainingManagement : Window
    {
        private const float ScreenPadding = 16f;
        private const float PanelPadding = 14f;
        private const float NavigationGap = 5f;
        private const float StackedSummaryWidth = 460f;
        private enum Page { Overview, Relations }
        private enum SummaryRelation { Master, AssignedTrainer, OwnedSlave, TrainerCandidate }
        private static readonly Color HeaderColor = new Color(.20f, .24f, .26f);
        private static readonly Color PanelColor = new Color(.16f, .19f, .20f);
        private static readonly Color SelectedColor = new Color(.26f, .34f, .35f);
        private static readonly Color DividerColor = new Color(.36f, .43f, .45f);
        private static readonly string[] PageKeys =
        {
            "SSC_TrainingManagement_Overview", "SSC_TrainingManagement_Relations",
            "SSC_Restrictions_Kind_DailyTraining", "SSC_TrainingManagement_Specialization",
            "SSC_TrainingManagement_BodyAppearance", "SSC_TrainingManagement_BehaviorRules"
        };
        private readonly Game game;
        private readonly SSCBondOverviewSession relationSession;
        private Page currentPage;
        private Map viewedMap;
        private Pawn source;
        private CompSexSlaveTraining comp;
        private int contextRevision;
        private Vector2 scroll;
        private (Pawn Target, SummaryRelation Relation, bool Detail) pressedPortrait;

        /// <summary>按当前逻辑屏幕尺寸限制初始窗口大小。</summary>
        public override Vector2 InitialSize
            => new Vector2(Mathf.Max(1f, Mathf.Min(1000f, UI.screenWidth - 2f * ScreenPadding)),
                Mathf.Max(1f, Mathf.Min(560f, UI.screenHeight - 2f * ScreenPadding)));

        /// <summary>记录所属游戏，允许原版界面处理窗口以外的点击。</summary>
        private Dialog_SSCTrainingManagement()
        {
            game = Current.Game;
            relationSession = new SSCBondOverviewSession(game, () => Time.realtimeSinceStartup,
                report: error => Log.Error("[SSC] Training management relationship query: " + error));
            doCloseX = true;
            closeOnCancel = true;
            closeOnAccept = false;
            forcePause = false;
            absorbInputAroundWindow = false;
        }

        /// <summary>只在可操作的地图会话中读取选中状态。</summary>
        private static bool GameReady()
            => Current.Game != null && Current.ProgramState == ProgramState.Playing &&
                Scribe.mode == LoadSaveMode.Inactive && Find.CurrentMap != null &&
                !WorldRendererUtility.WorldSelected;

        /// <summary>为当前单选角色打开窗口，已有有效实例时将其置前。</summary>
        internal static void OpenSelected() => TryOpen(null, false);

        /// <summary>命令栏放大镜为同一个原版单选角色打开关系页。</summary>
        internal static bool TryOpenRelations(Pawn pawn) => pawn != null && TryOpen(pawn, true);

        private static bool TryOpen(Pawn expected, bool relations)
        {
            if (!GameReady()) return false;
            Pawn selected = SelectedPawn();
            if (selected == null || expected != null && !ReferenceEquals(selected, expected)) return false;
            var previous = Find.WindowStack.WindowOfType<Dialog_SSCTrainingManagement>();
            if (previous != null && ReferenceEquals(previous.game, Current.Game))
            {
                if (relations) previous.ChangePage(Page.Relations);
                Find.WindowStack.Notify_ManuallySetFocus(previous);
                return true;
            }
            Find.WindowStack.Add(new Dialog_SSCTrainingManagement { currentPage = relations ? Page.Relations : Page.Overview });
            return true;
        }

        /// <summary>读取原版单选，沿用调教标签的角色准入条件。</summary>
        private static Pawn SelectedPawn()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded ||
                !ReferenceEquals(pawn.MapHeld, Find.CurrentMap)) return null;
            return ITab_SexSlaveTraining.CanInspect(pawn) ? pawn : null;
        }

        /// <summary>离开原游戏的地图视图时关闭，殖民者栏扩展时让出新增行。</summary>
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (!GameReady() || !ReferenceEquals(game, Current.Game))
            {
                Close(false);
                return;
            }
            float top = MinimumWindowY();
            if (windowRect.y < top) PositionBelowBar(top);
            // 原版可通过取消或点击外部关闭弹窗，清理它捕获的旧上下文。
            if (overviewPopup != null && !Find.WindowStack.IsOpen(overviewPopup)) overviewPopup = null;
            RefreshSelection();
            // Alt-Tab 或身份变化可能没有对应的鼠标松手事件。
            if (trainerGesture != null && !TrainerGestureSourceCurrent()) EndTrainerGesture();
            relationSession.Update(Time.frameCount, false, viewedMap);
        }

        /// <summary>关闭时撤销编辑弹窗并释放鼠标捕获、关系视图和候选引用。</summary>
        public override void PostClose()
        {
            InvalidateWindowContext();
            relationSession.Clear();
            ResetRelationPage();
            source = null;
            comp = null;
            pressedPortrait = default;
            base.PostClose();
        }

        /// <summary>初次打开或分辨率变化时在原版殖民者栏下方定位。</summary>
        protected override void SetInitialSizeAndPosition()
        {
            PositionBelowBar(GameReady() ? MinimumWindowY() : ScreenPadding);
        }

        /// <summary>读取原版头像的实际行高，留出全部可点击头像。</summary>
        private static float MinimumWindowY()
        {
            ColonistBar bar = Find.ColonistBar;
            // Entries 会刷新原版布局缓存，DrawLocs 和 Size 随后对应同一批头像。
            if (bar.Entries.Count == 0) return ScreenPadding;
            float top = ScreenPadding;
            float portraitHeight = bar.Size.y;
            foreach (Vector2 position in bar.DrawLocs)
                top = Mathf.Max(top, position.y + portraitHeight + ScreenPadding);
            return Mathf.Ceil(top);
        }

        /// <summary>沿用居中布局，实际覆盖殖民者栏时下移并按剩余高度约束。</summary>
        private void PositionBelowBar(float top)
        {
            Vector2 size = InitialSize;
            float available = Mathf.Max(1f, UI.screenHeight - top - ScreenPadding);
            float height = Mathf.Min(size.y, available);
            float y = Mathf.Max(top, (UI.screenHeight - height) / 2f);
            windowRect = new Rect((UI.screenWidth - size.x) / 2f, y, size.x, height);
        }

        /// <summary>切换角色时释放原编辑与关系上下文，空选和不可管理对象显示同一提示。</summary>
        private void RefreshSelection()
        {
            Pawn pawn = SelectedPawn();
            var training = pawn?.TryGetComp<CompSexSlaveTraining>();
            Map map = Find.CurrentMap;
            if (ReferenceEquals(source, pawn) && ReferenceEquals(comp, training) && ReferenceEquals(viewedMap, map)) return;
            InvalidateWindowContext();
            relationSession.ReleaseDetails();
            ResetRelationPage();
            source = pawn;
            comp = training;
            viewedMap = map;
            scroll = Vector2.zero;
            pressedPortrait = default;
        }

        /// <summary>切页时撤销原页手势，进入关系页后重新查询调教员。</summary>
        private void ChangePage(Page page)
        {
            if (currentPage == page) return;
            InvalidateWindowContext();
            currentPage = page;
            pressedPortrait = default;
            if (page == Page.Relations) nextCandidateRefresh = 0;
        }

        /// <summary>撤销旧页面的提交凭据并释放本窗口拥有的输入。</summary>
        private void InvalidateWindowContext()
        {
            contextRevision++;
            EndTrainerGesture();
            CloseOverviewPopup();
        }

        /// <summary>返回当前角色的 SSC 身份名称。</summary>
        private static string IdentityLabel(PawnIdentity identity)
        {
            switch (identity)
            {
                case PawnIdentity.Master: return "SSC_BondOverview_Master".Translate();
                case PawnIdentity.Slave: return "SSC_BondOverview_Slave".Translate();
                default: return "SSC_TrainingManagement_Unset".Translate();
            }
        }

        /// <summary>保持标题和关系摘要在顶部，概况滚动独立于导航。</summary>
        public override void DoWindowContents(Rect inRect)
        {
            if (!GameReady() || !ReferenceEquals(game, Current.Game)) return;
            RefreshSelection();
            BeginTrainerInput();
            // 同一帧的摘要、详情与点击使用同一个本地视图。会话负责直接变化及周期刷新。
            SSCBondOverviewView view = source == null ? null : relationSession.OpenDetails(source, viewedMap);
            SSCBondOverviewSnapshot snapshot = view?.Snapshot;
            if (snapshot != null && snapshot.Identity != comp.pawnIdentity) snapshot = null;
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Text.Font = GameFont.Medium;
            Text.WordWrap = true;
            if (Event.current.type == EventType.mouseDown) pressedPortrait = default;
            float y = DrawHeader(inRect.width);
            y += DrawRelationStrip(inRect.width, y, snapshot, out var clicked);
            y += PanelPadding;
            float available = Mathf.Max(1f, inRect.height - y);

            // 内容区变窄或高度不足时把六页导航移到顶部，避免挤压右侧内容。
            var labels = Array.ConvertAll(PageKeys, key => key.Translate().ToString());
            float navWidth = Mathf.Clamp(inRect.width * .24f, 190f, 228f);
            float rowHeight = MeasureNavigationRow(labels, navWidth);
            bool sidebar = inRect.width >= 640f && available >= labels.Length * (rowHeight + NavigationGap) - NavigationGap;
            int columns = sidebar ? 1 : inRect.width >= 480f ? 3 : 2;
            if (!sidebar)
            {
                navWidth = inRect.width;
                rowHeight = MeasureNavigationRow(labels, NavigationCellWidth(navWidth, columns));
            }
            float navHeight = DrawNavigation(new Rect(0f, y, navWidth, available), labels, rowHeight, columns, sidebar);
            Rect page = sidebar
                ? new Rect(navWidth + PanelPadding, y, inRect.width - navWidth - PanelPadding, available)
                : new Rect(0f, y + navHeight + PanelPadding, inRect.width,
                    Mathf.Max(1f, available - navHeight - PanelPadding));
            if (currentPage == Page.Relations)
            {
                var detailClick = DrawRelations(page, view, snapshot);
                if (detailClick.Target != null) clicked = detailClick;
            }
            else DrawOverview(page);
            if (Event.current.rawType == EventType.mouseUp) pressedPortrait = default;
            // 在所有滚动区结束后分派输入，拖动松手不会再触发普通头像跳转。
            if (!FinishTrainerInput(inRect) && clicked.Target != null) NavigatePortrait(snapshot, clicked.Target, clicked.Relation);
        }

        /// <summary>按可用宽度预留固定标题行，角色和身份变化只更新内容。</summary>
        private float DrawHeader(float width)
        {
            const float portraitSize = 80f;
            bool reservePortrait = width >= 160f;
            bool stacked = width < StackedSummaryWidth;
            float textX = PanelPadding + (reservePortrait ? portraitSize + PanelPadding : 0f);
            float textWidth = Mathf.Max(1f, width - textX - PanelPadding);
            string title = comp == null ? "SSC_TrainingManagement_Title".Translate().ToString()
                : "SSC_TrainingManagement_TitlePawn".Translate(source.LabelShort).ToString();
            string status = comp == null ? null : IdentityLabel(comp.pawnIdentity);
            string trainer = comp != null && comp.pawnIdentity != PawnIdentity.Unset
                ? "SSC_TrainerIdentity_Label".Translate() + TrainerState() : null;

            float lineHeight = Text.LineHeight;
            float textHeight = (stacked ? 3f : 2f) * lineHeight + 4f;
            float height = 2f * PanelPadding + Mathf.Max(reservePortrait ? portraitSize : 0f, textHeight);
            Widgets.DrawBoxSolid(new Rect(0f, 0f, width, height), HeaderColor);
            Widgets.DrawLineHorizontal(0f, height - 1f, width, DividerColor);
            if (reservePortrait && comp != null)
            {
                var portrait = new Rect(PanelPadding, PanelPadding, portraitSize, portraitSize);
                Widgets.DrawBoxSolid(portrait, PanelColor);
                SSCBondOverviewWidgets.Portrait(portrait, source, false, true, portraitSize);
            }
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.UpperLeft;
            // 窄窗把身份与任职分行，超长文字通过悬停查看完整值。
            float statusY = PanelPadding + lineHeight + 4f;
            DrawHeaderLine(PanelPadding, title, true);
            if (status != null) DrawHeaderLine(statusY, !stacked && trainer != null ? status + "  ·  " + trainer : status);
            if (stacked && trainer != null) DrawHeaderLine(statusY + lineHeight, trainer);
            return height;

            void DrawHeaderLine(float y, string label, bool bold = false)
            {
                var rect = new Rect(textX, y, textWidth, lineHeight);
                string displayed = label.Truncate(textWidth);
                Widgets.Label(rect, bold ? "<b>" + displayed + "</b>" : displayed);
                if (trainerGesture == null && displayed != label) TooltipHandler.TipRegion(rect, label);
            }
        }

        /// <summary>用详情共用的快照绘制顶部关系摘要。</summary>
        private float DrawRelationStrip(float width, float y, SSCBondOverviewSnapshot snapshot,
            out (Pawn Target, SummaryRelation Relation) clicked)
        {
            clicked = default;
            const float rowHeight = 66f;
            bool stacked = width < StackedSummaryWidth;
            float height = stacked ? 2f * rowHeight : rowHeight;
            // 摘要是否有数据不参与布局。未设身份、空选或刷新失败时也保留底板。
            Widgets.DrawBoxSolid(new Rect(0f, y, width, height), PanelColor);
            Widgets.DrawLineHorizontal(0f, y + height - 1f, width, DividerColor);
            if (snapshot?.State != SSCBondOverviewQueryState.Ready || snapshot.Identity == PawnIdentity.Unset) return height;
            bool slave = snapshot.Identity == PawnIdentity.Slave;
            float groupWidth = Mathf.Max(1f, (slave && !stacked ? width / 2f : width) - 2f * PanelPadding);
            var first = new Rect(PanelPadding, y, groupWidth, rowHeight);
            if (!slave)
            {
                var owned = snapshot.Slaves;
                clicked = (DrawRelationGroup(first, "SSC_BondOverview_OwnedCount".Translate(owned.Count),
                    SummaryRelation.OwnedSlave, owned.Count > 0 ? owned[0].Pawn : null,
                    owned.Count > 1 ? owned[1].Pawn : null), SummaryRelation.OwnedSlave);
                return height;
            }

            Pawn ownerClicked = DrawRelationGroup(first, snapshot.Master == null ? MissingMasterLabel(snapshot)
                : "SSC_TrainingManagement_Owner".Translate(snapshot.Master.Name).ToString(), SummaryRelation.Master, snapshot.Master?.Pawn);

            // 使用保存的指派和现行状态说明，暂时停工的现任仍可见。
            Pawn trainer = snapshot.AssignedTrainer;
            string trainerName = trainer == null ? "SSC_BondOverview_Trainer_None".Translate().ToString()
                : TrainerAssignmentUtility.GetAssignedTrainerLabel(snapshot.Source, trainer);
            Rect second = stacked ? new Rect(first.x, first.yMax, groupWidth, rowHeight)
                : new Rect(width / 2f + PanelPadding, y, groupWidth, rowHeight);
            Pawn trainerClicked = DrawRelationGroup(second, "SSC_TrainingManagement_AssignedTrainer".Translate(trainerName),
                SummaryRelation.AssignedTrainer, trainer);
            clicked = ownerClicked != null ? (ownerClicked, SummaryRelation.Master) : (trainerClicked, SummaryRelation.AssignedTrainer);
            return height;
        }

        /// <summary>摘要肖像用原生按钮接收左键，截断的完整状态可悬停查看。</summary>
        private Pawn DrawRelationGroup(Rect rect, string label, SummaryRelation relation, Pawn first, Pawn second = null)
        {
            const float cell = 56f;
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Pawn clicked = null;
            float x = rect.x;
            DrawPortrait(first);
            DrawPortrait(second);
            var textRect = new Rect(x, rect.y, Mathf.Max(1f, rect.xMax - x), rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(textRect, label.Truncate(textRect.width));
            if (trainerGesture == null) TooltipHandler.TipRegion(textRect, label);
            return clicked;

            void DrawPortrait(Pawn pawn)
            {
                if (!SSCBondOverviewUtility.IsValidPawn(pawn) || ReferenceEquals(source, pawn) || rect.xMax - x < cell + 64f) return;
                var portrait = new Rect(x, rect.y + (rect.height - cell) / 2f, cell, cell);
                if (DrawJumpPortrait(portrait, pawn, relation, false)) clicked = pawn;
                x += cell + 8f;
            }
        }

        /// <summary>绘制可定位肖像并处理普通点击。拖放从独立把手开始。</summary>
        private bool DrawJumpPortrait(Rect rect, Pawn pawn, SummaryRelation relation, bool detail,
            string tooltip = null, float? captureSize = null)
        {
            if (!SSCBondOverviewUtility.IsValidPawn(pawn) || ReferenceEquals(source, pawn)) return false;
            SSCBondOverviewWidgets.Portrait(rect, pawn, pawn.Dead, true, captureSize);
            // 把手持有鼠标期间，肖像只绘制，避免普通定位提示盖住落区结果。
            if (trainerGesture != null) return false;
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(rect, tooltip ?? "SSC_TrainingManagement_JumpToPawn".Translate(pawn.LabelShort).ToString());
            Event input = Event.current;
            bool over = Mouse.IsOver(rect);
            bool leftUp = input.type == EventType.mouseUp && input.button == 0 && over;
            if (GUI.enabled && input.type == EventType.mouseDown && input.button == 0 && over)
                pressedPortrait = (pawn, relation, detail);
            return GUI.enabled && Widgets.ButtonInvisible(rect) && leftUp && ReferenceEquals(pressedPortrait.Target, pawn) &&
                pressedPortrait.Relation == relation && pressedPortrait.Detail == detail;
        }

        /// <summary>沿原关系总览复查当前关系、重新解析目的地，再执行跳转。</summary>
        private void NavigatePortrait(SSCBondOverviewSnapshot snapshot, Pawn target, SummaryRelation relation)
        {
            if (!SourceIsCurrent() || snapshot?.State != SSCBondOverviewQueryState.Ready) return;

            var failure = SSCBondOverviewNavigationFailure.RelationChanged;
            bool success = false;
            if (relation == SummaryRelation.AssignedTrainer && ReferenceEquals(snapshot.AssignedTrainer, target))
                success = SSCBondOverviewNavigation.TryNavigateTrainer(snapshot, out failure);
            else if (relation == SummaryRelation.Master && ReferenceEquals(snapshot.Master?.Pawn, target))
                success = SSCBondOverviewNavigation.TryNavigate(snapshot, snapshot.Master, out failure);
            else if (relation == SummaryRelation.TrainerCandidate)
                success = SSCBondOverviewNavigation.TryNavigatePawn(target,
                    () => SourceIsCurrent() && currentPage == Page.Relations && !ReferenceEquals(comp.selectedTrainer, target) &&
                        SSCTrainerWorkUtility.CanConsider(source, target), out failure);
            else if (relation == SummaryRelation.OwnedSlave)
                foreach (var entry in snapshot.Slaves)
                    if (ReferenceEquals(entry.Pawn, target))
                    {
                        success = SSCBondOverviewNavigation.TryNavigate(snapshot, entry, out failure);
                        break;
                    }

            if (!success)
            {
                relationSession.RequestRefresh(relationSession.Details);
                nextCandidateRefresh = 0;
                Messages.Message(SSCBondOverviewText.T("Failure_" + failure), MessageTypeDefOf.RejectInput, false);
            }
            else if (!GameReady() || !ReferenceEquals(SelectedPawn(), target)) Close(false);
        }

        private bool SourceIsCurrent()
            => GameReady() && ReferenceEquals(game, Current.Game) && ReferenceEquals(viewedMap, Find.CurrentMap) &&
                source != null && ReferenceEquals(SelectedPawn(), source) && ReferenceEquals(source.TryGetComp<CompSexSlaveTraining>(), comp);

        private static float NavigationCellWidth(float width, int columns)
            => Mathf.Max(1f, (width - (columns - 1) * NavigationGap) / columns);

        /// <summary>按译文测量同组按钮，避免较长的导航文字把侧栏撑出窗口。</summary>
        private static float MeasureNavigationRow(string[] labels, float width)
        {
            float height = 42f;
            foreach (string label in labels)
                height = Mathf.Max(height, Text.CalcHeight(label, Mathf.Max(1f, width - 20f)) + 10f);
            return height;
        }

        /// <summary>绘制六个导航位置，开放概况与只读关系。</summary>
        private float DrawNavigation(Rect rect, string[] labels, float rowHeight, int columns, bool sidebar)
        {
            float width = NavigationCellWidth(rect.width, columns);
            int rows = (labels.Length + columns - 1) / columns;
            float height = rows * (rowHeight + NavigationGap) - NavigationGap;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, sidebar ? Mathf.Max(height, rect.height) : height), PanelColor);
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Anchor = TextAnchor.MiddleLeft;
            for (int i = 0; i < labels.Length; i++)
            {
                var cell = new Rect(rect.x + i % columns * (width + NavigationGap), rect.y + i / columns * (rowHeight + NavigationGap), width, rowHeight);
                GUI.color = Color.white;
                bool available = i <= (int)Page.Relations;
                Widgets.DrawBoxSolid(cell, i == (int)currentPage ? SelectedColor : HeaderColor);
                GUI.color = available ? Color.white : new Color(.68f, .73f, .74f);
                Widgets.Label(new Rect(cell.x + 10f, cell.y, Mathf.Max(1f, cell.width - 20f), cell.height), labels[i]);
                if (available)
                {
                    Widgets.DrawHighlightIfMouseover(cell);
                    if (Widgets.ButtonInvisible(cell)) ChangePage((Page)i);
                }
                else TooltipHandler.TipRegion(cell, "SSC_TrainingManagement_PagePending".Translate());
            }
            return height;
        }

        /// <summary>概况的身份控件和只读状态共用独立滚动区，行高随译文换行。</summary>
        private void DrawOverview(Rect page)
        {
            var rows = OverviewRows();
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            float width = Mathf.Max(1f, viewport.width - 16f);
            string title = "<b>" + PageKeys[0].Translate() + "</b>";
            float titleHeight = Text.CalcHeight(title, width);
            float firstRow = titleHeight + 10f;
            // 先计入身份控件的高度，随后布局只读状态，使窄窗也能滚动到末行。
            var identity = comp == null ? null : BuildOverviewIdentity(width, firstRow);
            if (identity != null) firstRow = identity.Bottom;
            float labelWidth = Mathf.Max(1f, width * .42f - 4f);
            float valueWidth = Mathf.Max(1f, width - labelWidth - 8f);
            var heights = new float[rows.Length];
            float totalHeight = firstRow;
            for (int i = 0; i < rows.Length; i++)
            {
                heights[i] = Mathf.Max(Text.CalcHeight(rows[i].Label, labelWidth), Text.CalcHeight(rows[i].Value, valueWidth)) + 18f;
                totalHeight += heights[i];
            }
            string hint = comp == null ? "SSC_TrainingManagement_SelectPawn".Translate().ToString() : null;
            if (hint != null) totalHeight += Text.CalcHeight(hint, width);
            scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, totalHeight - viewport.height));
            Widgets.BeginScrollView(viewport, ref scroll, new Rect(0f, 0f, width, Mathf.Max(viewport.height, totalHeight)));
            try
            {
                Widgets.Label(new Rect(0f, 0f, width, titleHeight), title);
                if (hint != null) Widgets.Label(new Rect(0f, firstRow, width, totalHeight - firstRow), hint);
                if (identity != null) DrawOverviewIdentity(identity);
                float y = firstRow;
                for (int i = 0; i < rows.Length; i++)
                {
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(0f, y + 5f, labelWidth, heights[i] - 10f), rows[i].Label);
                    Text.Anchor = TextAnchor.UpperRight;
                    Widgets.Label(new Rect(labelWidth + 8f, y + 5f, valueWidth, heights[i] - 10f), rows[i].Value);
                    y += heights[i];
                    Widgets.DrawLineHorizontal(0f, y - 1f, width, DividerColor);
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        /// <summary>按身份展示原有可读状态，特化进度沿用旧页的规则。</summary>
        private (string Label, string Value)[] OverviewRows()
        {
            if (comp == null) return Array.Empty<(string, string)>();
            if (comp.pawnIdentity == PawnIdentity.Slave)
            {
                string training = (comp.IsEnabled ? "SSC_TrainingManagement_TrainingEnabled" : "SSC_TrainingManagement_TrainingDisabled").Translate();
                if (comp.IsOnCooldown)
                {
                    int ticks = CompSexSlaveTraining.CooldownTicks - (Find.TickManager.TicksGame - comp.lastTrainingTick);
                    training += " · " + Strings.ITab_CooldownStatus(ticks.ToStringTicksToPeriod());
                }
                return new (string, string)[]
                {
                    (PageKeys[2].Translate(), training),
                    ("SSC_TrainingManagement_CurrentSpecialization".Translate(), ITab_SexSlaveTraining.GetSpecializationOverviewLabel(source, comp))
                };
            }
            if (comp.pawnIdentity == PawnIdentity.Master)
            {
                string stage = SSCMasterProgressionUtility.GetRecord(source)?.LabelInBrackets;
                return new (string, string)[]
                {
                    ("SSC_TrainingManagement_MasterStage".Translate(), stage ?? "SSC_MasterProgression_NoRecord".Translate().ToString())
                };
            }
            return Array.Empty<(string, string)>();
        }

        /// <summary>保留原任职状态的显示语义，包括身份暂停时保留的启用选择。</summary>
        private string TrainerState()
        {
            string key = SSCIdentityUtility.IsTrainer(source) ? "SSC_TrainerIdentity_Active" :
                comp.slaveTrainerEnabled && comp.pawnIdentity == PawnIdentity.Slave
                    ? "SSC_TrainerIdentity_SavedInactive" : "SSC_TrainerIdentity_Off";
            return key.Translate();
        }

    }
}
