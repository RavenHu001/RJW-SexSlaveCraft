// AI 辅助生成
using System;
using System.Collections.Generic;
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
            optionalTitle = "SSC_TrainingManagement_Title".Translate();
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

        /// <summary>按原版实际选中绘制角色标题和只读概况。</summary>
        public override void DoWindowContents(Rect inRect)
        {
            if (!GameReady() || !ReferenceEquals(game, Current.Game)) return;
            RefreshSelection();
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Text.WordWrap = true;
            if (comp == null)
            {
                Widgets.Label(inRect, "SSC_TrainingManagement_SelectPawn".Translate());
                return;
            }

            float portraitSize = Mathf.Min(56f, inRect.width);
            SSCBondOverviewWidgets.Portrait(new Rect(0f, 0f, portraitSize, portraitSize), source, false, true);
            float nameX = portraitSize + 12f;
            float nameWidth = Mathf.Max(1f, inRect.width - nameX);
            float nameHeight = Mathf.Max(24f, Text.CalcHeight(source.LabelShort, nameWidth));
            Widgets.Label(new Rect(nameX, 0f, nameWidth, nameHeight), source.LabelShort);
            string identity = IdentityLabel(comp.pawnIdentity);
            float identityHeight = Text.CalcHeight(identity, nameWidth);
            Widgets.Label(new Rect(nameX, nameHeight + 4f, nameWidth, identityHeight), identity);
            float y = Mathf.Max(portraitSize, nameHeight + 4f + identityHeight) + 12f;

            // 按当前语言测量文本高度，长姓名和状态文案可在同一滚动区换行。
            var lines = OverviewLines();
            float width = Mathf.Max(1f, inRect.width - 20f);
            float totalHeight = 0f;
            foreach (string line in lines) totalHeight += Text.CalcHeight(line, width) + 10f;
            Rect viewport = new Rect(0f, y, inRect.width, Mathf.Max(1f, inRect.height - y));
            scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, totalHeight - viewport.height));
            Widgets.BeginScrollView(viewport, ref scroll, new Rect(0f, 0f, width, Mathf.Max(viewport.height, totalHeight)));
            try
            {
                float lineY = 0f;
                foreach (string line in lines)
                {
                    float height = Text.CalcHeight(line, width);
                    Widgets.Label(new Rect(0f, lineY, width, height), line);
                    lineY += height + 10f;
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        /// <summary>读取原组件与关系查询的即时状态，生成概况文本。</summary>
        private List<string> OverviewLines()
        {
            var lines = new List<string> { "SSC_TrainingManagement_Overview".Translate() };
            bool trainer = SSCIdentityUtility.IsTrainer(source);
            string trainerState = trainer ? "SSC_TrainerIdentity_Active" :
                comp.slaveTrainerEnabled && comp.pawnIdentity == PawnIdentity.Slave
                    ? "SSC_TrainerIdentity_SavedInactive" : "SSC_TrainerIdentity_Off";
            lines.Add("SSC_TrainerIdentity_Label".Translate() + trainerState.Translate());
            Pawn owner = SSCBondUtility.GetBoundMaster(source);
            if (owner != null)
                lines.Add("SSC_BondOverview_Master".Translate() + " · " + owner.LabelShort);
            if (comp.pawnIdentity == PawnIdentity.Slave)
            {
                lines.Add("SSC_TrainingManagement_AssignedTrainer".Translate(TrainerAssignmentUtility.GetAssignedTrainerLabel(source)));
                // 保存的调教选择与即时冷却同时显示，便于看出暂时不可调教的原因。
                lines.Add((comp.IsEnabled ? "SSC_TrainingManagement_TrainingEnabled" : "SSC_TrainingManagement_TrainingDisabled").Translate());
                if (comp.IsOnCooldown)
                {
                    int ticks = CompSexSlaveTraining.CooldownTicks - (Find.TickManager.TicksGame - comp.lastTrainingTick);
                    lines.Add(Strings.ITab_CooldownStatus(ticks.ToStringTicksToPeriod()));
                }
            }
            return lines;
        }
    }
}
