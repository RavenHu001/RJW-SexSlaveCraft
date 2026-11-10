// AI 辅助生成
using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只读显示原版地图当前单选角色的调教概况。</summary>
    internal sealed class Dialog_SSCTrainingManagement : Window
    {
        private const float ScreenPadding = 16f;
        private const float PanelPadding = 12f;
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
        private Pawn source;
        private CompSexSlaveTraining comp;
        private Vector2 scroll;

        /// <summary>按当前逻辑屏幕尺寸限制初始窗口大小。</summary>
        public override Vector2 InitialSize
            => new Vector2(Mathf.Max(1f, Mathf.Min(800f, UI.screenWidth - 2f * ScreenPadding)),
                Mathf.Max(1f, Mathf.Min(680f, UI.screenHeight - 2f * ScreenPadding)));

        /// <summary>记录所属游戏，允许原版界面处理窗口以外的点击。</summary>
        private Dialog_SSCTrainingManagement()
        {
            game = Current.Game;
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
        internal static void OpenSelected()
        {
            if (!GameReady() || SelectedPawn() == null) return;
            var previous = Find.WindowStack.WindowOfType<Dialog_SSCTrainingManagement>();
            if (previous != null && ReferenceEquals(previous.game, Current.Game))
            {
                Find.WindowStack.Notify_ManuallySetFocus(previous);
                return;
            }
            Find.WindowStack.Add(new Dialog_SSCTrainingManagement());
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

        /// <summary>更新只读视图对象。空选、多选和不可管理的选中显示同一提示。</summary>
        private void RefreshSelection()
        {
            Pawn pawn = SelectedPawn();
            var training = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (ReferenceEquals(source, pawn) && ReferenceEquals(comp, training)) return;
            source = pawn;
            comp = training;
            scroll = Vector2.zero;
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
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Text.WordWrap = true;
            float y = DrawHeader(inRect.width);
            y += DrawRelationStrip(inRect.width, y);
            y += PanelPadding;
            float available = Mathf.Max(1f, inRect.height - y);

            // 内容区变窄或高度不足时把六页导航移到顶部，避免挤压右侧内容。
            bool sidebar = inRect.width >= 600f && available >= 250f;
            float navWidth = sidebar ? Mathf.Clamp(inRect.width * .24f, 154f, 190f) : inRect.width;
            int columns = sidebar ? 1 : inRect.width >= 420f ? 3 : 2;
            float navHeight = DrawNavigation(new Rect(0f, y, navWidth, available), columns, sidebar);
            Rect page = sidebar
                ? new Rect(navWidth + PanelPadding, y, inRect.width - navWidth - PanelPadding, available)
                : new Rect(0f, y + navHeight + PanelPadding, inRect.width,
                    Mathf.Max(1f, available - navHeight - PanelPadding));
            DrawOverview(page);
        }

        /// <summary>标题、头像和身份使用同一个原版单选，空选时仍保留窗口标题。</summary>
        private float DrawHeader(float width)
        {
            const float portraitSize = 56f;
            bool showPortrait = comp != null && width >= 160f;
            float textX = PanelPadding + (showPortrait ? portraitSize + PanelPadding : 0f);
            float textWidth = Mathf.Max(1f, width - textX - PanelPadding);
            string title = comp == null ? "SSC_TrainingManagement_Title".Translate().ToString()
                : "SSC_TrainingManagement_TitlePawn".Translate(source.LabelShort).ToString();
            string status = comp == null ? null : IdentityLabel(comp.pawnIdentity);
            if (comp != null && comp.pawnIdentity != PawnIdentity.Unset)
                status += "  ·  " + "SSC_TrainerIdentity_Label".Translate() + TrainerState();

            Text.Font = GameFont.Medium;
            float titleHeight = Text.CalcHeight(title, textWidth);
            Text.Font = GameFont.Small;
            float statusHeight = status == null ? 0f : 4f + Text.CalcHeight(status, textWidth);
            float height = 2f * PanelPadding + Mathf.Max(showPortrait ? portraitSize : 0f, titleHeight + statusHeight);
            Widgets.DrawBoxSolid(new Rect(0f, 0f, width, height), HeaderColor);
            Widgets.DrawLineHorizontal(0f, height - 1f, width, DividerColor);
            if (showPortrait)
            {
                var portrait = new Rect(PanelPadding, PanelPadding, portraitSize, portraitSize);
                Widgets.DrawBoxSolid(portrait, PanelColor);
                SSCBondOverviewWidgets.Portrait(portrait, source, false, true);
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(textX, PanelPadding, textWidth, titleHeight), title);
            Text.Font = GameFont.Small;
            if (status != null) Widgets.Label(new Rect(textX, PanelPadding + titleHeight + 4f, textWidth, statusHeight - 4f), status);
            return height;
        }

        /// <summary>在关系详情页接入前，摘要已按身份读取实际锁链和指派。</summary>
        private float DrawRelationStrip(float width, float y)
        {
            if (comp == null || comp.pawnIdentity == PawnIdentity.Unset) return 0f;
            const float rowHeight = 42f;
            bool slave = comp.pawnIdentity == PawnIdentity.Slave;
            bool stacked = slave && width < 460f;
            float height = stacked ? 2f * rowHeight : rowHeight;
            Widgets.DrawBoxSolid(new Rect(0f, y, width, height), PanelColor);
            Widgets.DrawLineHorizontal(0f, y + height - 1f, width, DividerColor);
            float groupWidth = Mathf.Max(1f, (slave && !stacked ? width / 2f : width) - 2f * PanelPadding);
            var first = new Rect(PanelPadding, y, groupWidth, rowHeight);
            if (!slave)
            {
                var owned = SSCBondOverviewUtility.SummarizeLocalSlaves(source);
                DrawRelationGroup(first, "SSC_BondOverview_OwnedCount".Translate(owned.Count), owned.First, owned.Second);
                return height;
            }

            var chain = SSCBondUtility.GetChain(source);
            Pawn owner = chain?.LinkedPawn;
            string ownerLabel;
            if (chain == null) ownerLabel = "SSC_BondOverview_NoMaster".Translate();
            else if (!SSCBondOverviewUtility.IsValidPawn(owner) || ReferenceEquals(owner, source))
                ownerLabel = "SSC_BondOverview_InvalidMaster".Translate();
            else ownerLabel = "SSC_TrainingManagement_Owner".Translate(owner.LabelShort);
            DrawRelationGroup(first, ownerLabel, ReferenceEquals(owner, source) ? null : owner);

            // 使用保存的指派和现行状态说明，暂时停工的现任仍可见。
            Pawn trainer = comp.selectedTrainer;
            string trainerName = trainer == null ? "SSC_BondOverview_Trainer_None".Translate().ToString()
                : TrainerAssignmentUtility.GetAssignedTrainerLabel(source);
            Rect second = stacked ? new Rect(first.x, first.yMax, groupWidth, rowHeight)
                : new Rect(width / 2f + PanelPadding, y, groupWidth, rowHeight);
            DrawRelationGroup(second, "SSC_TrainingManagement_AssignedTrainer".Translate(trainerName), trainer);
            return height;
        }

        /// <summary>摘要肖像只显示关系，截断的完整状态可悬停查看。</summary>
        private static void DrawRelationGroup(Rect rect, string label, Pawn first = null, Pawn second = null)
        {
            const float cell = 32f;
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            float x = rect.x;
            DrawPortrait(first);
            DrawPortrait(second);
            var textRect = new Rect(x, rect.y, Mathf.Max(1f, rect.xMax - x), rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(textRect, label.Truncate(textRect.width));
            TooltipHandler.TipRegion(textRect, label);

            void DrawPortrait(Pawn pawn)
            {
                if (!SSCBondOverviewUtility.IsValidPawn(pawn) || rect.xMax - x < cell + 64f) return;
                var portrait = new Rect(x, rect.y + (rect.height - cell) / 2f, cell, cell);
                SSCBondOverviewWidgets.Portrait(portrait, pawn, pawn.Dead, true);
                TooltipHandler.TipRegion(portrait, pawn.LabelShort);
                x += cell + 4f;
            }
        }

        /// <summary>绘制原型的六个导航位置，概况可用，其余页在各批接入后启用。</summary>
        private static float DrawNavigation(Rect rect, int columns, bool sidebar)
        {
            const float gap = 4f;
            float width = Mathf.Max(1f, (rect.width - (columns - 1) * gap) / columns);
            var labels = new string[PageKeys.Length];
            float rowHeight = 32f;
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = PageKeys[i].Translate();
                rowHeight = Mathf.Max(rowHeight, Text.CalcHeight(labels[i], Mathf.Max(1f, width - 16f)) + 8f);
            }
            int rows = (labels.Length + columns - 1) / columns;
            float height = rows * (rowHeight + gap) - gap;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, sidebar ? Mathf.Max(height, rect.height) : height), PanelColor);
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Anchor = TextAnchor.MiddleLeft;
            for (int i = 0; i < labels.Length; i++)
            {
                var cell = new Rect(rect.x + i % columns * (width + gap), rect.y + i / columns * (rowHeight + gap), width, rowHeight);
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(cell, i == 0 ? SelectedColor : HeaderColor);
                GUI.color = i == 0 ? Color.white : new Color(.68f, .73f, .74f);
                Widgets.Label(new Rect(cell.x + 8f, cell.y, Mathf.Max(1f, cell.width - 16f), cell.height), labels[i]);
                if (i != 0) TooltipHandler.TipRegion(cell, "SSC_TrainingManagement_PagePending".Translate());
            }
            return height;
        }

        /// <summary>概况按标签和值排列，分别计算长译文的行高并独立滚动。</summary>
        private void DrawOverview(Rect page)
        {
            var rows = OverviewRows();
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            float width = Mathf.Max(1f, viewport.width - 16f);
            string title = PageKeys[0].Translate();
            Text.Font = GameFont.Medium;
            float titleHeight = Text.CalcHeight(title, width);
            Text.Font = GameFont.Small;
            float firstRow = titleHeight + 10f;
            float labelWidth = Mathf.Max(1f, width * .42f - 4f);
            float valueWidth = Mathf.Max(1f, width - labelWidth - 8f);
            var heights = new float[rows.Length];
            float totalHeight = firstRow;
            for (int i = 0; i < rows.Length; i++)
            {
                heights[i] = Mathf.Max(Text.CalcHeight(rows[i].Label, labelWidth), Text.CalcHeight(rows[i].Value, valueWidth)) + 12f;
                totalHeight += heights[i];
            }
            string hint = comp == null ? "SSC_TrainingManagement_SelectPawn".Translate().ToString() : null;
            if (hint != null) totalHeight += Text.CalcHeight(hint, width);
            scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, totalHeight - viewport.height));
            Widgets.BeginScrollView(viewport, ref scroll, new Rect(0f, 0f, width, Mathf.Max(viewport.height, totalHeight)));
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, width, titleHeight), title);
                Text.Font = GameFont.Small;
                if (hint != null) Widgets.Label(new Rect(0f, firstRow, width, totalHeight - firstRow), hint);
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
            return new (string, string)[] { ("SSC_TrainerIdentity_Label".Translate(), TrainerState()) };
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
