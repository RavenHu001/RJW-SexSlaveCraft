// AI 辅助生成。关系页绘制现有关系和调教员卡片，手势与提交由窗口单独管理。
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private const float MinCardWidth = 72f;
        private const float MinTrainerCardWidth = 100f;
        private const float TrainerHandleWidth = 30f;
        private static readonly Color TrainerGripColor = new Color(.68f, .76f, .78f);
        private const float CardGap = 6f;
        private const float CardHeight = 88f;
        private const float SectionPadding = 10f;
        private const float ScrollbarWidth = 18f;
        private const float OwnerColumnWidth = 144f;
        private const float OwnerPortraitSize = 92f;
        private const float CandidateRefreshSeconds = 2f;

        private Vector2 relationsScroll;
        private Vector2 candidateScroll;
        private readonly List<Pawn> candidates = new List<Pawn>();
        private Pawn[] assigned = Array.Empty<Pawn>();
        private int candidateRevision = -1;
        private float nextCandidateRefresh;

        /// <summary>来源或地图变化时，清掉原角色的滚动位置和候选。</summary>
        private void ResetRelationPage()
        {
            relationsScroll = candidateScroll = Vector2.zero;
            candidates.Clear();
            assigned = Array.Empty<Pawn>();
            candidateRevision = -1;
            nextCandidateRefresh = 0;
        }

        /// <summary>投影原指派字段，候选只在视图变化或定时刷新时重新枚举。</summary>
        private void RefreshRelationTrainers(SSCBondOverviewView view)
        {
            Pawn trainer = view.Snapshot.AssignedTrainer;
            if (!ReferenceEquals(assigned.Length == 0 ? null : assigned[0], trainer))
                assigned = trainer == null ? Array.Empty<Pawn>() : new[] { trainer };

            float now = Time.realtimeSinceStartup;
            if (candidateRevision == view.Revision && now < nextCandidateRefresh && now >= nextCandidateRefresh - CandidateRefreshSeconds) return;
            candidates.Clear();
            foreach (Pawn candidate in TrainerAssignmentUtility.GetTrainerCandidates(view.Source))
                if (!ReferenceEquals(candidate, trainer)) candidates.Add(candidate);
            candidates.Sort((left, right) =>
            {
                int result = StringComparer.CurrentCultureIgnoreCase.Compare(left.LabelShort, right.LabelShort);
                return result != 0 ? result : left.thingIDNumber.CompareTo(right.thingIDNumber);
            });
            candidateRevision = view.Revision;
            nextCandidateRefresh = now + CandidateRefreshSeconds;
        }

        /// <summary>绘制当前身份适用的关系区域，返回本帧点击以便在滚动区结束后跳转。</summary>
        private (Pawn Target, SummaryRelation Relation) DrawRelations(Rect page, SSCBondOverviewView view, SSCBondOverviewSnapshot snapshot)
        {
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            if (snapshot?.State != SSCBondOverviewQueryState.Ready || snapshot.Identity == PawnIdentity.Unset)
            {
                string text = comp == null ? "SSC_TrainingManagement_SelectPawn".Translate().ToString()
                    : comp.pawnIdentity == PawnIdentity.Unset ? IdentityLabel(PawnIdentity.Unset)
                    : SSCBondOverviewText.T(view?.Failed == true ? "QueryFailed" : "Waiting");
                Widgets.Label(viewport, text);
                return default;
            }

            if (snapshot.Identity == PawnIdentity.Master)
                return (DrawOwnedSlaves(viewport, view), SummaryRelation.OwnedSlave);

            RefreshRelationTrainers(view);
            return DrawSlaveRelations(viewport, view);
        }

        /// <summary>宽窗保持主人在左、调教员在右；窄窗三区纵排。</summary>
        private (Pawn Target, SummaryRelation Relation) DrawSlaveRelations(Rect viewport, SSCBondOverviewView view)
        {
            string assignedTitle = "SSC_TrainingManagement_AssignedSection".Translate();
            string candidateTitle = "SSC_TrainingManagement_CandidateSection".Translate();
            float rightWidth = viewport.width - OwnerColumnWidth - SectionPadding;
            if (rightWidth >= OwnerColumnWidth)
            {
                // 两个右区共用列宽，为候选滚动条留出同样的边距。
                int rightColumns = GridColumns(rightWidth - 2f * SectionPadding - ScrollbarWidth, MinTrainerCardWidth);
                float assignedHeight = TrainerSectionHeight(assignedTitle, rightWidth, assigned.Length, rightColumns);
                float candidateHeader = HeadingHeight(candidateTitle, rightWidth - 2f * SectionPadding);
                float candidateHeight = viewport.height - assignedHeight - SectionPadding;
                if (candidateHeight >= candidateHeader + 2f * SectionPadding + CardHeight &&
                    viewport.height >= OwnerSectionHeight(view.Snapshot, OwnerColumnWidth))
                {
                    var ownerRect = new Rect(viewport.x, viewport.y, OwnerColumnWidth, viewport.height);
                    var assignedRect = new Rect(ownerRect.xMax + SectionPadding, viewport.y, rightWidth, assignedHeight);
                    var candidateRect = new Rect(assignedRect.x, assignedRect.yMax + SectionPadding, rightWidth, candidateHeight);
                    Pawn owner = DrawOwnerSection(ownerRect, view);
                    Pawn trainer = DrawTrainerSection(assignedRect, assignedTitle, assigned, SummaryRelation.AssignedTrainer,
                        rightColumns, ScrollbarWidth, assignedRect.y, assignedRect.yMax);
                    Pawn candidate = DrawCandidateScroll(candidateRect, candidateTitle, rightColumns);
                    return RelationClick(owner, trainer, candidate);
                }
            }

            // 纵排三区共用外层纵向滚动条，头像卡按剩余宽度减少列数。
            float width = Mathf.Max(1f, viewport.width - ScrollbarWidth);
            int columns = GridColumns(width - 2f * SectionPadding, MinTrainerCardWidth);
            float ownerHeight = OwnerSectionHeight(view.Snapshot, width);
            float assignedY = ownerHeight + SectionPadding;
            float assignedSectionHeight = TrainerSectionHeight(assignedTitle, width, assigned.Length, columns);
            float candidateY = assignedY + assignedSectionHeight + SectionPadding;
            float candidateSectionHeight = TrainerSectionHeight(candidateTitle, width, candidates.Count, columns);
            float contentHeight = candidateY + candidateSectionHeight;
            // 先更新外层滚动，再以本帧可见的分区绘制和命中，避免松手使用上一帧坐标。
            ScrollTrainerDrag(viewport, contentHeight);
            Widgets.BeginScrollView(viewport, ref relationsScroll, new Rect(0f, 0f, width, Mathf.Max(viewport.height, contentHeight)));
            try
            {
                float top = relationsScroll.y;
                float bottom = top + viewport.height;
                Pawn owner = top < ownerHeight ? DrawOwnerSection(new Rect(0f, 0f, width, ownerHeight), view) : null;
                Pawn trainer = DrawTrainerSection(new Rect(0f, assignedY, width, assignedSectionHeight), assignedTitle,
                    assigned, SummaryRelation.AssignedTrainer, columns, 0f, top, bottom);
                Pawn candidate = DrawTrainerSection(new Rect(0f, candidateY, width, candidateSectionHeight), candidateTitle,
                    candidates, SummaryRelation.TrainerCandidate, columns, 0f, top, bottom);
                return RelationClick(owner, trainer, candidate);
            }
            finally { Widgets.EndScrollView(); }
        }

        /// <summary>宽窗中候选独立滚动，主人与已指派保持可见。</summary>
        private Pawn DrawCandidateScroll(Rect section, string title, int columns)
        {
            Rect body = DrawSectionHeading(section, title);
            float width = body.width - ScrollbarWidth;
            float total = Mathf.Max(CardHeight, GridRows(candidates.Count, columns) * CardHeight);
            Pawn clicked;
            Widgets.BeginScrollView(body, ref candidateScroll, new Rect(0f, 0f, width, Mathf.Max(body.height, total)));
            try
            {
                clicked = DrawTrainerCards(new Rect(0f, 0f, width, total), candidates, SummaryRelation.TrainerCandidate,
                    columns, candidateScroll.y, candidateScroll.y + body.height);
            }
            finally { Widgets.EndScrollView(); }
            // 在外层坐标中使用候选视口，扣掉滚动条边距后才是可高亮的落点。
            body.width = width;
            OfferTrainerDrop(body, SummaryRelation.TrainerCandidate);
            return clicked;
        }

        private static (Pawn Target, SummaryRelation Relation) RelationClick(Pawn owner, Pawn trainer, Pawn candidate)
        {
            if (owner != null) return (owner, SummaryRelation.Master);
            return trainer != null ? (trainer, SummaryRelation.AssignedTrainer) : (candidate, SummaryRelation.TrainerCandidate);
        }

        private static string MissingMasterLabel(SSCBondOverviewSnapshot snapshot)
            => SSCBondOverviewText.T(snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget ? "InvalidMaster" : "NoMaster");

        private static float OwnerSectionHeight(SSCBondOverviewSnapshot snapshot, float width)
        {
            float innerWidth = Mathf.Max(1f, width - 2f * SectionPadding);
            float body = snapshot.Master == null ? Text.CalcHeight(MissingMasterLabel(snapshot), innerWidth) : OwnerPortraitSize + 30f;
            return 2f * SectionPadding + HeadingHeight(SSCBondOverviewText.T("Master"), innerWidth) + body;
        }

        /// <summary>主人只占一张大头像，未绑定和损坏的锁链使用原空状态。</summary>
        private Pawn DrawOwnerSection(Rect section, SSCBondOverviewView view)
        {
            Rect body = DrawSectionHeading(section, SSCBondOverviewText.T("Master"));
            var master = view.Snapshot.Master;
            if (master == null)
            {
                Widgets.Label(body, MissingMasterLabel(view.Snapshot));
                return null;
            }

            // 纵排时仍保持主人头像靠左，避免随整个分区宽度扩张。
            var card = new Rect(body.x, body.y, Mathf.Min(body.width, OwnerColumnWidth - 2f * SectionPadding), OwnerPortraitSize + 30f);
            return DrawRelationCard(card, master.Pawn, SummaryRelation.Master, OwnerPortraitSize,
                view.Display.Row(master).Tooltip, view.Display.Row(master).Name) ? master.Pawn : null;
        }

        /// <summary>主人页复用原所属性奴快照、详情文字和按可见行绘制的网格。</summary>
        private Pawn DrawOwnedSlaves(Rect viewport, SSCBondOverviewView view)
        {
            Rect body = DrawSectionHeading(viewport, view.Display.OwnedHeading);
            var slaves = view.Snapshot.Slaves;
            if (slaves.Count == 0)
            {
                Widgets.Label(body, SSCBondOverviewText.T("EmptySlaves"));
                return null;
            }
            float width = Mathf.Max(1f, body.width - ScrollbarWidth);
            int columns = GridColumns(width);
            int rows = GridRows(slaves.Count, columns);
            float total = rows * CardHeight;
            Widgets.BeginScrollView(body, ref relationsScroll, new Rect(0f, 0f, width, Mathf.Max(body.height, total)));
            try
            {
                SSCBondOverviewLayout.VisibleRange(rows, 0f, relationsScroll.y, body.height, out int first, out int end, CardHeight);
                for (int i = first * columns; i < Math.Min(slaves.Count, end * columns); i++)
                {
                    var entry = slaves[i];
                    var text = view.Display.Row(entry);
                    if (DrawRelationCard(CardRect(0f, 0f, width, columns, i), entry.Pawn, SummaryRelation.OwnedSlave, 56f,
                        text.Tooltip, text.Name)) return entry.Pawn;
                }
                return null;
            }
            finally { Widgets.EndScrollView(); }
        }

        private static float TrainerSectionHeight(string title, float width, int count, int columns)
            => 2f * SectionPadding + HeadingHeight(title, width - 2f * SectionPadding) +
                Math.Max(1, GridRows(count, columns)) * CardHeight;

        /// <summary>绘制已指派区或纵排候选区。空区同样提供落点。</summary>
        private Pawn DrawTrainerSection(Rect section, string title, IReadOnlyList<Pawn> trainers, SummaryRelation relation,
            int columns, float scrollGutter, float top, float bottom)
        {
            if (section.yMax <= top || section.y >= bottom) return null;
            Rect body = DrawSectionHeading(section, title);
            body.width = Mathf.Max(1f, body.width - scrollGutter);
            Pawn clicked = DrawTrainerCards(body, trainers, relation, columns, top, bottom);
            // 标题只说明区域用途。即使当前没有卡片，也使用同一个内容矩形来高亮和命中。
            OfferTrainerDrop(body, relation);
            return clicked;
        }

        /// <summary>两组调教员按分区列数换行，绘制时只读取可见卡片的工作状态。</summary>
        private Pawn DrawTrainerCards(Rect body, IReadOnlyList<Pawn> trainers, SummaryRelation relation, int columns, float top, float bottom)
        {
            if (trainers.Count == 0)
            {
                Widgets.Label(body, SSCBondOverviewText.T(relation == SummaryRelation.AssignedTrainer ? "Trainer_None" : "None"));
                return null;
            }

            int rows = GridRows(trainers.Count, columns);
            SSCBondOverviewLayout.VisibleRange(rows, body.y, top, bottom - top, out int first, out int end, CardHeight);
            for (int i = first * columns; i < Math.Min(trainers.Count, end * columns); i++)
            {
                Pawn trainer = trainers[i];
                Rect card = CardRect(body.x, body.y, body.width, columns, i);
                string name = trainer.Dead ? SSCBondOverviewText.Format("DeadName", trainer.LabelShort) : trainer.LabelShort;
                if (DrawRelationCard(card, trainer, relation, 56f, TrainerTooltip(card, trainer, relation), name)) return trainer;
            }
            return null;
        }

        /// <summary>先说明保存的工作开关，再显示拖放操作或当前限制。</summary>
        private string TrainerTooltip(Rect rect, Pawn trainer, SummaryRelation relation)
        {
            if (trainerGesture != null || !Mouse.IsOver(rect)) return null;
            var state = SSCTrainerWorkUtility.GetState(trainer, source);
            int priority = SSCTrainerWorkUtility.SavedPriority(trainer.workSettings, SSCTrainerWorkUtility.WorkType);
            string work = SSCTrainerWorkUtility.Reason(priority > 0 ? SSCTrainerWorkState.Active
                : priority == 0 ? SSCTrainerWorkState.Off : SSCTrainerWorkState.Uninitialized);
            string detail;
            if (relation == SummaryRelation.AssignedTrainer)
                detail = (SSCRestrictionTrainerAssignment.CanAssign(source, null) ? "SSC_TrainingManagement_DragClear"
                    : "SSC_TrainingManagement_DragClearBlocked").Translate();
            else if (state == SSCTrainerWorkState.Active || state == SSCTrainerWorkState.Off)
                detail = (state == SSCTrainerWorkState.Off ? "SSC_TrainingManagement_DragAssignEnable"
                    : "SSC_TrainingManagement_DragAssign").Translate();
            else detail = SSCTrainerWorkUtility.Reason(state, trainer);
            return work + "\n" + detail;
        }

        /// <summary>排布头像、姓名和调教员把手，各自使用独立矩形。</summary>
        private bool DrawRelationCard(Rect card, Pawn pawn, SummaryRelation relation, float portraitSize, string tooltip, string name)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            bool trainerCard = relation == SummaryRelation.AssignedTrainer || relation == SummaryRelation.TrainerCandidate;
            // 调教员卡片预留更大的把手和间距，常规宽度保留完整的 56 像素肖像。
            float handleWidth = trainerCard ? Mathf.Min(TrainerHandleWidth, card.width * .35f) : 0f;
            float portraitWidth = trainerCard ? Mathf.Max(1f, card.width - handleWidth - 8f) : card.width;
            float size = Mathf.Min(portraitSize, portraitWidth);
            var portrait = new Rect(card.x + (portraitWidth - size) / 2f, card.y, size, size);
            bool clicked = DrawJumpPortrait(portrait, pawn, relation, true, trainerCard ? null : tooltip, portraitSize);
            if (trainerCard)
            {
                // 把手位于肖像之外。指派失效且没有可绘制的肖像时，仍可从把手发起撤销。
                // 把手与当前实际显示的头像等高，窄卡片缩放时也保持上下边缘一致。
                var handle = new Rect(card.xMax - handleWidth, card.y, handleWidth, size);
                DrawTrainerHandle(handle, pawn, relation, tooltip);
            }
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.UpperCenter;
            // 姓名使用较小字号，完整名称仍可悬停查看。GUI 作用域会恢复标题及正文的字号。
            Text.Font = GameFont.Small;
            float nameOffset = (trainerCard ? portraitSize : size) + 2f;
            var nameRect = new Rect(card.x, card.y + nameOffset, card.width, card.height - nameOffset);
            Widgets.Label(nameRect, name.Truncate(card.width));
            if (trainerGesture == null && Mouse.IsOver(nameRect)) TooltipHandler.TipRegion(nameRect, trainerCard ? name : tooltip ?? name);
            return clicked;
        }

        /// <summary>绘制独立把手并提供工作状态与拖放提示。</summary>
        private void DrawTrainerHandle(Rect rect, Pawn pawn, SummaryRelation relation, string tooltip)
        {
            Widgets.DrawBoxSolid(rect, PanelColor);
            float lineWidth = Mathf.Min(16f, rect.width * .60f);
            float x = rect.x + (rect.width - lineWidth) / 2f;
            float y = rect.y + rect.height / 2f;
            for (int i = -1; i <= 1; i++)
                Widgets.DrawBoxSolid(new Rect(x, y + i * 6f - 1f, lineWidth, 2f), TrainerGripColor);
            // 活跃拖放只呈现落区预览，经过其他把手时无需再弹出卡片提示。
            if (trainerGesture != null) return;
            Widgets.DrawHighlightIfMouseover(rect);
            if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);
            TrainerHandleInput(rect, pawn, relation);
        }

        /// <summary>按卡片最小宽度和间距计算分区能容纳的列数。</summary>
        private static int GridColumns(float width, float minimumCardWidth = MinCardWidth)
            => Math.Max(1, (int)((width + CardGap) / (minimumCardWidth + CardGap)));

        private static int GridRows(int count, int columns) => (count + columns - 1) / columns;

        private static Rect CardRect(float x, float y, float width, int columns, int index)
        {
            float cell = Mathf.Max(1f, (width - (columns - 1) * CardGap) / columns);
            return new Rect(x + index % columns * (cell + CardGap), y + index / columns * CardHeight, cell, CardHeight);
        }

        private static float HeadingHeight(string title, float width)
            => Text.CalcHeight("<b>" + title + "</b>", Mathf.Max(1f, width)) + 8f;

        /// <summary>绘制区域标题及边界，返回独立内容矩形。</summary>
        private static Rect DrawSectionHeading(Rect section, string title)
        {
            Widgets.DrawBoxSolid(section, HeaderColor);
            float x = section.x + SectionPadding;
            float y = section.y + SectionPadding;
            float width = Mathf.Max(1f, section.width - 2f * SectionPadding);
            float height = HeadingHeight(title, width);
            Widgets.Label(new Rect(x, y, width, height - 6f), "<b>" + title + "</b>");
            Widgets.DrawLineHorizontal(x, y + height - 4f, width, DividerColor);
            return new Rect(x, y + height, width, Mathf.Max(1f, section.yMax - y - height - SectionPadding));
        }
    }
}
