// AI 辅助生成。窗口持有一次头像手势，调教员事务仍由 SSCTrainerWorkUtility 负责。
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private const float TrainerDragThreshold = 6f;
        private const float TrainerScrollEdge = 28f;
        private const float TrainerScrollSpeed = 300f;
        private static readonly Color TrainerDropColor = new Color(.35f, .72f, .56f, .24f);

        /// <summary>一次左键手势的起点和原始业务请求，列表重绘时保留同一对象。</summary>
        private sealed class TrainerGesture
        {
            internal Pawn Pawn;
            internal SummaryRelation Origin;
            internal SSCTrainerWorkUtility.Request Request;
            internal Vector2 PressPosition;
            internal int ControlId;
            internal bool Dragging;
            internal float LastScrollTime;
        }

        private TrainerGesture trainerGesture;
        private int trainerControlId;
        private int trainerContextRevision;
        private bool trainerDropHovered;

        /// <summary>刷新窗口上下文时让捕获的旧请求永久失效。</summary>
        private void InvalidateTrainerGesture()
        {
            trainerContextRevision++;
            EndTrainerGesture();
        }

        /// <summary>释放本窗口持有的输入捕获，保留已交给同步提交的上下文代次。</summary>
        private void EndTrainerGesture()
        {
            if (trainerGesture != null && GUIUtility.hotControl == trainerGesture.ControlId) GUIUtility.hotControl = 0;
            trainerGesture = null;
            trainerDropHovered = false;
        }

        /// <summary>只允许当前游戏中仍打开的性奴关系页处理手势。</summary>
        private bool TrainerGestureSourceCurrent()
            => Application.isFocused && currentPage == Page.Relations && SourceIsCurrent() && comp.pawnIdentity == PawnIdentity.Slave &&
                Find.WindowStack.IsOpen(this);

        /// <summary>核对实时指派，旧快照上的卡片不能代表后来换入的指派。</summary>
        private bool TrainerCardCurrent(Pawn pawn, SummaryRelation origin)
            => ReferenceEquals(comp.selectedTrainer, pawn) == (origin == SummaryRelation.AssignedTrainer);

        /// <summary>先判定鼠标是否越过阈值，再绘制本次事件的可见落区。</summary>
        private void BeginTrainerInput()
        {
            trainerControlId = GUIUtility.GetControlID(FocusType.Passive);
            trainerDropHovered = false;
            if (trainerGesture == null) return;
            Event input = Event.current;
            if (!TrainerGestureSourceCurrent() || GUIUtility.hotControl != trainerGesture.ControlId ||
                input.type == EventType.mouseDown && input.button == 0)
            {
                EndTrainerGesture();
                return;
            }
            if (trainerGesture.Dragging || input.button != 0 ||
                input.rawType != EventType.mouseDrag && input.rawType != EventType.mouseUp) return;
            // 按下可能发生在滚动区内。统一比较屏幕坐标，再把阈值换算为相同的单位。
            Vector2 moved = UI.GUIToScreenPoint(input.mousePosition) - trainerGesture.PressPosition;
            float threshold = TrainerDragThreshold / Prefs.UIScale;
            trainerGesture.Dragging = moved.sqrMagnitude >= threshold * threshold;
        }

        /// <summary>头像按下即捕获事务；同一头像内松手的普通点击仍用于定位。</summary>
        private bool TrainerPortraitInput(Rect rect, Pawn pawn, SummaryRelation origin)
        {
            if (!GUI.enabled || !Mouse.IsOver(rect)) return false;
            Event input = Event.current;
            if (input.type == EventType.mouseDown && input.button == 0 && GUIUtility.hotControl == 0 &&
                TrainerGestureSourceCurrent() && TrainerCardCurrent(pawn, origin))
            {
                int revision = trainerContextRevision;
                Pawn originalSource = source;
                Pawn requestedTrainer = origin == SummaryRelation.AssignedTrainer ? null : pawn;
                // 事务在按下时捕获真实指派、人格和工作设置。松手阶段只验证该请求并尝试提交。
                trainerGesture = new TrainerGesture
                {
                    Pawn = pawn,
                    Origin = origin,
                    ControlId = trainerControlId,
                    PressPosition = UI.GUIToScreenPoint(input.mousePosition),
                    LastScrollTime = Time.realtimeSinceStartup,
                    Request = new SSCTrainerWorkUtility.Request(originalSource, requestedTrainer,
                        current: () => trainerContextRevision == revision && ReferenceEquals(source, originalSource) &&
                            TrainerGestureSourceCurrent() && TrainerCardCurrent(pawn, origin))
                };
                GUIUtility.hotControl = trainerControlId;
                input.Use();
                return false;
            }
            return input.rawType == EventType.mouseUp && input.button == 0 && trainerGesture?.Dragging == false &&
                trainerGesture.Origin == origin && ReferenceEquals(trainerGesture.Pawn, pawn) &&
                SSCBondOverviewUtility.IsValidPawn(pawn);
        }

        /// <summary>落区在其所属 GUI 坐标内命中，原版 Mouse.IsOver 同时检查滚动裁剪与遮挡。</summary>
        private void OfferTrainerDrop(Rect rect, SummaryRelation destination)
        {
            if (trainerGesture?.Dragging != true || trainerGesture.Origin == destination || !Mouse.IsOver(rect)) return;
            trainerDropHovered = true;
            if (Event.current.type == EventType.repaint && trainerGesture.Request.CanSubmit(out _))
                Widgets.DrawBoxSolid(rect, TrainerDropColor);
        }

        /// <summary>三区纵排时让手势在外层滚动区边缘继续移动到目标区。</summary>
        private void ScrollTrainerDrag(Rect viewport, float contentHeight)
        {
            if (trainerGesture?.Dragging != true || Event.current.type != EventType.repaint) return;
            float now = Time.realtimeSinceStartup;
            // 限制单帧滚动量，窗口掉帧或切回前台后不会突然跳过落区。
            float elapsed = Mathf.Clamp(now - trainerGesture.LastScrollTime, 0f, .05f);
            trainerGesture.LastScrollTime = now;
            if (!Mouse.IsOver(viewport)) return;
            float edge = Mathf.Min(TrainerScrollEdge, viewport.height / 3f);
            float y = Event.current.mousePosition.y;
            if (y < viewport.y + edge) elapsed = -elapsed;
            else if (y <= viewport.yMax - edge) return;
            relationsScroll.y = Mathf.Clamp(relationsScroll.y + elapsed * TrainerScrollSpeed, 0f,
                Mathf.Max(0f, contentHeight - viewport.height));
        }

        /// <summary>结束滚动区绘制后只提交一次，随后重新读取保存事实。</summary>
        private bool FinishTrainerInput(Rect inRect)
        {
            TrainerGesture gesture = trainerGesture;
            if (gesture == null) return false;
            Event input = Event.current;
            if (input.rawType == EventType.mouseUp && input.button == 0)
            {
                bool stillOwnsInput = GUIUtility.hotControl == gesture.ControlId && TrainerGestureSourceCurrent();
                bool drop = stillOwnsInput && gesture.Dragging && trainerDropHovered;
                // 先释放手势再运行事务，回调触发重绘或切换角色时也无法重复消费松手。
                EndTrainerGesture();
                if (input.type != EventType.Used) input.Use();
                if (drop)
                {
                    if (!gesture.Request.TrySubmit(out string reason))
                        Messages.Message(reason, gesture.Request.Source, MessageTypeDefOf.RejectInput, false);
                    relationSession.RequestRefresh(relationSession.Details);
                    nextCandidateRefresh = 0;
                }
                return gesture.Dragging || !stillOwnsInput;
            }
            if (input.type == EventType.mouseDrag && input.button == 0) input.Use();
            if (gesture.Dragging && input.type == EventType.repaint && Mouse.IsOver(inRect) && SSCBondOverviewUtility.IsValidPawn(gesture.Pawn))
            {
                Vector2 cursor = input.mousePosition;
                SSCBondOverviewWidgets.Portrait(new Rect(cursor.x + 8f, cursor.y + 8f, 40f, 40f), gesture.Pawn, false, true);
            }
            return gesture.Dragging;
        }

        /// <summary>窗外松手可能不进入窗口内容，利用原版窗口栈坐标提前取消。</summary>
        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            Event input = Event.current;
            if (trainerGesture == null || input.rawType != EventType.mouseUp || input.button != 0 ||
                ReferenceEquals(Find.WindowStack.GetWindowAt(UI.GUIToScreenPoint(input.mousePosition)), this)) return;
            EndTrainerGesture();
            if (input.type != EventType.Used) input.Use();
        }

        /// <summary>Esc 优先取消正在进行的头像手势。</summary>
        public override void OnCancelKeyPressed()
        {
            if (trainerGesture == null)
            {
                base.OnCancelKeyPressed();
                return;
            }
            EndTrainerGesture();
            Event.current.Use();
        }
    }
}
