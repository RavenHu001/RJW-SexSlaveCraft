// AI 辅助生成。窗口持有一次把手拖放，调教员事务仍由 SSCTrainerWorkUtility 负责。
using RimWorld;
using UnityEngine;
using Verse;
using static SexSlaveCraft.SSCTrainingWindowTheme;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private const float TrainerDragThreshold = 12f;
        private const float TrainerScrollEdge = 28f;
        private const float TrainerScrollSpeed = 300f;
        private static readonly Color TrainerDropColor = new Color(.35f, .72f, .56f, .24f);
        private static readonly Color TrainerDropHoverColor = new Color(.35f, .72f, .56f, .42f);

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
        private bool trainerDropHovered;
        private string trainerDropPreview;

        /// <summary>释放本窗口持有的输入捕获，保留已交给同步提交的上下文代次。</summary>
        private void EndTrainerGesture()
        {
            if (trainerGesture != null && GUIUtility.hotControl == trainerGesture.ControlId) GUIUtility.hotControl = 0;
            trainerGesture = null;
            trainerDropHovered = false;
            trainerDropPreview = null;
        }

        /// <summary>只允许当前游戏中仍打开的性奴关系页处理手势。</summary>
        private bool TrainerGestureSourceCurrent()
            => Application.isFocused && currentPage == Page.Relations && SourceIsCurrent() && comp.pawnIdentity == PawnIdentity.Slave &&
                Find.WindowStack.IsOpen(this);

        /// <summary>核对实时指派，旧快照上的卡片不能代表后来换入的指派。</summary>
        private bool TrainerCardCurrent(Pawn pawn, SummaryRelation origin)
            => comp.HasAssignedTrainer(pawn) == (origin == SummaryRelation.AssignedTrainer);

        /// <summary>清理上一事件的落点和预览，再判定本次输入是否进入拖放。</summary>
        private void BeginTrainerInput()
        {
            trainerControlId = GUIUtility.GetControlID(FocusType.Passive);
            trainerDropHovered = false;
            trainerDropPreview = null;
            if (trainerGesture == null) return;
            Event input = Event.current;
            if (!TrainerGestureSourceCurrent() || GUIUtility.hotControl != trainerGesture.ControlId ||
                input.type == EventType.mouseDown && input.button == 0)
            {
                EndTrainerGesture();
                return;
            }
            // 只在真实拖动事件中进入拖放。松手时的位置变化不会启动新手势。
            if (trainerGesture.Dragging || input.button != 0 || input.rawType != EventType.mouseDrag) return;
            // 按下可能发生在滚动区内。统一比较屏幕坐标，再把阈值换算为相同的单位。
            Vector2 moved = UI.GUIToScreenPoint(input.mousePosition) - trainerGesture.PressPosition;
            float threshold = TrainerDragThreshold / Prefs.UIScale;
            trainerGesture.Dragging = moved.sqrMagnitude >= threshold * threshold;
        }

        /// <summary>只有按下独立把手才捕获指派请求。</summary>
        private void TrainerHandleInput(Rect rect, Pawn pawn, SummaryRelation origin)
        {
            Event input = Event.current;
            if (!GUI.enabled || input.type != EventType.mouseDown || input.button != 0 || GUIUtility.hotControl != 0 ||
                !Mouse.IsOver(rect) || !TrainerGestureSourceCurrent() || !TrainerCardCurrent(pawn, origin)) return;

            int revision = contextRevision;
            Pawn originalSource = source;
            var operation = origin == SummaryRelation.AssignedTrainer
                ? SSCTrainerAssignmentOperation.Remove : SSCTrainerAssignmentOperation.Add;
            // 事务在按下时捕获真实指派、人格和工作设置。松手阶段只验证该请求并尝试提交。
            trainerGesture = new TrainerGesture
            {
                Pawn = pawn,
                Origin = origin,
                ControlId = trainerControlId,
                PressPosition = UI.GUIToScreenPoint(input.mousePosition),
                LastScrollTime = Time.realtimeSinceStartup,
                Request = new SSCTrainerWorkUtility.Request(originalSource, pawn, operation,
                    current: () => contextRevision == revision && ReferenceEquals(source, originalSource) &&
                        TrainerGestureSourceCurrent() && TrainerCardCurrent(pawn, origin))
            };
            GUIUtility.hotControl = trainerControlId;
            input.Use();
        }

        /// <summary>在当前绘制坐标中用同一个内容矩形计算高亮、预览和松手命中。</summary>
        private void OfferTrainerDrop(Rect rect, SummaryRelation destination)
        {
            TrainerGesture gesture = trainerGesture;
            if (gesture?.Dragging != true || gesture.Origin == destination) return;
            bool repaint = Event.current.type == EventType.repaint;
            // Mouse.IsOver 同时检查外层滚动区裁剪和窗口遮挡。非重绘事件仅在松手时重做预检。
            bool hovered = Mouse.IsOver(rect);
            if (!repaint && (!hovered || Event.current.rawType != EventType.mouseUp)) return;

            // 高亮、提示和松手命中共用预检结果，实际写入仍由松手后的原事务复查。
            bool allowed = gesture.Request.CanSubmit(out string reason, out bool opensNonOwnerTraining);
            trainerDropHovered = hovered && allowed;
            if (!repaint) return;
            if (allowed) Widgets.DrawBoxSolid(rect, hovered ? TrainerDropHoverColor : TrainerDropColor);
            if (hovered) trainerDropPreview = allowed ? DescribeTrainerDrop(gesture, opensNonOwnerTraining) : reason;
        }

        /// <summary>显示通过预检的指派结果，并按实际变化附加工作启用和授权说明。</summary>
        private string DescribeTrainerDrop(TrainerGesture gesture, bool opensNonOwnerTraining)
        {
            string name = gesture.Pawn.LabelShort;
            if (gesture.Origin == SummaryRelation.AssignedTrainer)
                return "SSC_TrainingManagement_DropClear".Translate(name);

            string result = "SSC_TrainingManagement_DropAssign".Translate(name);
            if (SSCTrainerWorkUtility.GetState(gesture.Pawn, source) == SSCTrainerWorkState.Off)
                result += "\n" + "SSC_TrainingManagement_DropEnable".Translate(name);
            if (opensNonOwnerTraining) result += "\n" + "SSC_TrainingManagement_DropAuthorizeNonOwner".Translate();
            if (SSCBondUtility.GetBoundMaster(source) == null)
                result += "\n" + "SSC_TrainingManagement_DropFirstBinding".Translate(name);
            return result;
        }

        /// <summary>在滚动区之外显示指针旁的操作预览，限制在窗口内。</summary>
        private void DrawTrainerDropPreview(Rect inRect, Vector2 cursor)
        {
            if (string.IsNullOrEmpty(trainerDropPreview)) return;
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            const float padding = 8f;
            float width = Mathf.Min(320f, inRect.width);
            float textWidth = Mathf.Max(1f, width - 2f * padding);
            float height = Mathf.Min(inRect.height, Text.CalcHeight(trainerDropPreview, textWidth) + 2f * padding);
            // 窗口下方空间不足时将预览翻到指针上方，保留落区在指针附近的可见反馈。
            float y = cursor.y + 52f;
            if (y + height > inRect.yMax) y = cursor.y - height - padding;
            var rect = new Rect(Mathf.Clamp(cursor.x + 8f, inRect.x, inRect.xMax - width),
                Mathf.Clamp(y, inRect.y, inRect.yMax - height), width, height);
            Widgets.DrawBoxSolid(rect, PanelColor);
            Widgets.Label(new Rect(rect.x + padding, rect.y + padding, textWidth, Mathf.Max(1f, height - 2f * padding)), trainerDropPreview);
        }

        /// <summary>在指针所在视口边缘滚动，供宽窄布局共用。</summary>
        private void ScrollTrainerDrag(Rect viewport, float contentHeight, ref Vector2 scroll)
        {
            if (trainerGesture?.Dragging != true || Event.current.type != EventType.repaint || !Mouse.IsOver(viewport)) return;
            float now = Time.realtimeSinceStartup;
            // 限制单帧滚动量，窗口掉帧或切回前台后不会突然跳过落区。
            float elapsed = Mathf.Clamp(now - trainerGesture.LastScrollTime, 0f, .05f);
            trainerGesture.LastScrollTime = now;
            float edge = Mathf.Min(TrainerScrollEdge, viewport.height / 3f);
            float y = Event.current.mousePosition.y;
            if (y < viewport.y + edge) elapsed = -elapsed;
            else if (y <= viewport.yMax - edge) return;
            scroll.y = Mathf.Clamp(scroll.y + elapsed * TrainerScrollSpeed, 0f,
                Mathf.Max(0f, contentHeight - viewport.height));
        }

        /// <summary>在滚动区结束后完成把手手势。只有仍持有输入且命中有效落区才提交。</summary>
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
                if (drop) SubmitTrainerRequest(gesture.Request);
                return true;
            }
            if (input.type == EventType.mouseDrag && input.button == 0) input.Use();
            if (gesture.Dragging && input.type == EventType.repaint && Mouse.IsOver(inRect))
            {
                Vector2 cursor = input.mousePosition;
                if (SSCBondOverviewUtility.IsValidPawn(gesture.Pawn))
                    SSCBondOverviewWidgets.Portrait(new Rect(cursor.x + 8f, cursor.y + 8f, 40f, 40f), gesture.Pawn, false, true);
                DrawTrainerDropPreview(inRect, cursor);
            }
            return true; // 把手捕获期间由本入口负责输入，普通肖像点击不会再次分派。
        }

        private void SubmitTrainerRequest(SSCTrainerWorkUtility.Request request)
        {
            if (!request.TrySubmit(out string reason))
                Messages.Message(reason, request.Source, MessageTypeDefOf.RejectInput, false);
            RefreshAfterEdit();
        }

        /// <summary>窗外松手可能不进入窗口内容，利用原版窗口栈坐标撤销拖放和身体按压。</summary>
        public override void ExtraOnGUI()
        {
            base.ExtraOnGUI();
            Event input = Event.current;
            if ((trainerGesture == null && !abilityTogglePress.HasValue && pageActionPress.Submit == null) ||
                input.rawType != EventType.mouseUp || input.button != 0 ||
                ReferenceEquals(Find.WindowStack.GetWindowAt(UI.GUIToScreenPoint(input.mousePosition)), this)) return;
            abilityTogglePress = null;
            pageActionPress = default;
            // 普通页面控件不捕获全局鼠标，窗外松手仍交给原版界面处理。
            if (trainerGesture == null) return;
            EndTrainerGesture();
            if (input.type != EventType.Used) input.Use();
        }

        /// <summary>Esc 优先取消正在进行的把手手势。</summary>
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
