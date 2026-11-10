// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>显示只读概况，并在地图单选确认后更新窗口当前角色。</summary>
    internal sealed class Dialog_SSCTrainingManagement : Window
    {
        private readonly Game game;
        private readonly Map map;
        private Pawn source;
        private CompSexSlaveTraining comp;
        private long revision;
        private bool switching;
        private Vector2 scroll;

        /// <summary>按当前逻辑屏幕尺寸限制初始窗口大小。</summary>
        public override Vector2 InitialSize
            => new Vector2(Mathf.Max(1f, Mathf.Min(800f, UI.screenWidth - 32f)),
                Mathf.Max(1f, Mathf.Min(680f, UI.screenHeight - 32f)));

        /// <summary>记录开窗时的游戏、地图和角色组件，配置原生窗口行为。</summary>
        private Dialog_SSCTrainingManagement(Pawn pawn)
        {
            game = Current.Game;
            map = Find.CurrentMap;
            source = pawn;
            comp = pawn.TryGetComp<CompSexSlaveTraining>();
            optionalTitle = "SSC_TrainingManagement_Title".Translate();
            doCloseX = true;
            closeOnCancel = true;
            closeOnAccept = false;
            forcePause = false;
            absorbInputAroundWindow = true;
        }

        /// <summary>只在可操作的地图会话中读取或改动选中状态。</summary>
        private static bool GameReady()
            => Current.Game != null && Current.ProgramState == ProgramState.Playing &&
                Scribe.mode == LoadSaveMode.Inactive && Find.CurrentMap != null &&
                !WorldRendererUtility.WorldSelected;

        /// <summary>为当前单选角色打开窗口，已有有效实例时将其置前。</summary>
        internal static void OpenSelected()
        {
            if (!GameReady()) return;
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (!ITab_SexSlaveTraining.CanInspect(pawn) || pawn.Dead || pawn.Destroyed || pawn.Discarded) return;
            var previous = Find.WindowStack.WindowOfType<Dialog_SSCTrainingManagement>();
            if (previous != null && previous.IsCurrent(previous.revision))
            {
                Find.WindowStack.Notify_ManuallySetFocus(previous);
                return;
            }
            if (previous != null) previous.Close(false);
            Find.WindowStack.Add(new Dialog_SSCTrainingManagement(pawn));
        }

        /// <summary>菜单和后续页面动作复查原窗口代次，A到B再回A也不能接收旧请求。</summary>
        internal bool IsCurrent(long expectedRevision)
            => !switching && GameReady() && ReferenceEquals(game, Current.Game) &&
                ReferenceEquals(map, Find.CurrentMap) && Find.WindowStack.IsOpen(this) &&
                revision == expectedRevision && source != null && !source.Dead &&
                !source.Destroyed && !source.Discarded &&
                ReferenceEquals(Find.Selector.SingleSelectedThing, source) &&
                ReferenceEquals(source.TryGetComp<CompSexSlaveTraining>(), comp) &&
                ITab_SexSlaveTraining.CanInspect(source);

        /// <summary>窗口入栈后再次确认打开期间角色上下文仍有效。</summary>
        public override void PostOpen()
        {
            base.PostOpen();
            if (!IsCurrent(revision)) Close(false);
        }

        /// <summary>外部选择或游戏上下文变化时关闭窗口。</summary>
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            // 窗内切换会暂时清空单选，由切换流程确认结果或恢复原选中。
            if (!switching && !IsCurrent(revision)) Close(false);
        }

        /// <summary>使遗留菜单失效并释放窗口持有的角色和组件。</summary>
        public override void PostClose()
        {
            revision++;
            source = null;
            comp = null;
            scroll = Vector2.zero;
            base.PostClose();
        }

        /// <summary>判断候选能否在原地图上直接选中，且仍具有菜单捕获的组件。</summary>
        private bool CanSelect(Pawn pawn, CompSexSlaveTraining expectedComp)
            => pawn != null && expectedComp != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded &&
                pawn.Spawned && ReferenceEquals(pawn.Map, map) && pawn.def.selectable &&
                pawn.TryGetComp<CompSelectProxy>()?.thingToSelect == null &&
                ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), expectedComp) &&
                ITab_SexSlaveTraining.CanInspect(pawn);

        /// <summary>原生选中调用期间，检查窗口仍处于原游戏和地图。</summary>
        private bool SameWindow()
            => GameReady() && ReferenceEquals(game, Current.Game) && ReferenceEquals(map, Find.CurrentMap) &&
                Find.WindowStack.IsOpen(this);

        /// <summary>复查旧菜单上下文，确认地图选中后再更新窗口角色及代次。</summary>
        internal bool TrySwitchTo(Pawn target, CompSexSlaveTraining expectedComp, long expectedRevision)
        {
            if (!IsCurrent(expectedRevision)) return false;
            if (!CanSelect(target, expectedComp)) return SelectionFailed();
            if (ReferenceEquals(target, source)) return true;
            switching = true;
            try
            {
                try
                {
                    if (TrySelectOnMap(target, expectedComp))
                    {
                        source = target;
                        comp = expectedComp;
                        revision++;
                        scroll = Vector2.zero;
                        return true;
                    }
                }
                catch (Exception error)
                {
                    Log.ErrorOnce("[SSC TrainingManagement] Selection failed: " + error, 1953727141);
                }
                // 此时窗口仍指向原角色，可以尝试恢复原单选。
                RestoreSelectionOrClose();
                return SelectionFailed();
            }
            finally { switching = false; }
        }

        /// <summary>执行原生单选，并在清空前后及最终选中后验证上下文。</summary>
        private bool TrySelectOnMap(Pawn pawn, CompSexSlaveTraining expectedComp)
        {
            if (!SameWindow() || !CanSelect(pawn, expectedComp)) return false;
            Find.Selector.ClearSelection();

            // 原生调用可以被其他模组接入。若清空后已换图或目标失效，Select 可能跳转镜头。
            if (!SameWindow() || !CanSelect(pawn, expectedComp)) return false;
            Find.Selector.Select(pawn);
            return SameWindow() && CanSelect(pawn, expectedComp) &&
                ReferenceEquals(Find.Selector.SingleSelectedThing, pawn);
        }

        /// <summary>在原上下文仍可操作时恢复原单选，无法恢复则关闭窗口。</summary>
        private void RestoreSelectionOrClose()
        {
            try
            {
                if (TrySelectOnMap(source, comp)) return;
            }
            catch (Exception error)
            {
                Log.ErrorOnce("[SSC TrainingManagement] Selection restore failed: " + error, 1953727142);
            }
            Close(false);
        }

        /// <summary>向原游戏会话反馈切换失败，并返回拒绝结果。</summary>
        private bool SelectionFailed()
        {
            if (ReferenceEquals(game, Current.Game) && Current.ProgramState == ProgramState.Playing)
                Messages.Message("SSC_TrainingManagement_SelectionFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        /// <summary>为当前地图生成选角菜单，每个操作保留当时的代次和候选组件。</summary>
        private void OpenPawnMenu()
        {
            long expectedRevision = revision;
            var options = new List<FloatMenuOption>();
            // 使用完整已生成角色集合，保留囚犯、原版奴隶及自我调教例外。
            var candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            candidates.Sort((left, right) => string.Compare(left.LabelShort, right.LabelShort, StringComparison.CurrentCulture));
            foreach (Pawn pawn in candidates)
            {
                var expectedComp = pawn.TryGetComp<CompSexSlaveTraining>();
                if (!CanSelect(pawn, expectedComp)) continue;
                Pawn target = pawn;
                string label = pawn.LabelShort + " · " + IdentityLabel(expectedComp.pawnIdentity);
                options.Add(new FloatMenuOption(label, () => TrySwitchTo(target, expectedComp, expectedRevision)));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("SSC_TrainingManagement_NoCandidates".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>返回概况和选角菜单共用的 SSC 身份名称。</summary>
        private static string IdentityLabel(PawnIdentity identity)
        {
            switch (identity)
            {
                case PawnIdentity.Master: return "SSC_BondOverview_Master".Translate();
                case PawnIdentity.Slave: return "SSC_BondOverview_Slave".Translate();
                default: return "SSC_TrainingManagement_Unset".Translate();
            }
        }

        /// <summary>绘制固定的角色标题及头像入口，下方概况使用独立滚动区。</summary>
        public override void DoWindowContents(Rect inRect)
        {
            if (!IsCurrent(revision)) return;
            using var scope = new SSCBondOverviewWidgets.GuiScope(true);
            Text.WordWrap = true;
            float portraitSize = Mathf.Min(56f, inRect.width);
            Rect portraitRect = new Rect(0f, 0f, portraitSize, portraitSize);

            // 肖像仍用原生缓存绘制，在同一矩形接收点击并显示切换提示。
            SSCBondOverviewWidgets.Portrait(portraitRect, source, false, true);
            Widgets.DrawHighlightIfMouseover(portraitRect);
            TooltipHandler.TipRegion(portraitRect, "SSC_TrainingManagement_SwitchPawn".Translate());
            if (Widgets.ButtonInvisible(portraitRect)) OpenPawnMenu();

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
            var lines = new List<string> { "SSC_TrainingManagement_Overview".Translate(), IdentityLabel(comp.pawnIdentity) };
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
