// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        // 概况和日常调教共用唯一的编辑弹窗，由窗口负责撤销。
        private Window editPopup;

        /// <summary>绑定绘制时的角色和人格，延迟回调不得重新取得编辑资格。</summary>
        private readonly struct PageEdit
        {
            internal readonly Pawn Pawn;
            internal readonly CompSexSlaveTraining Comp;
            internal readonly PawnIdentity Identity;
            internal readonly Page OriginPage;
            internal readonly object Personality;
            internal readonly int Revision;

            /// <summary>在控件接收输入前记录这次绘制对应的页面、身份和人格。</summary>
            internal PageEdit(Pawn pawn, CompSexSlaveTraining comp, Page page, int revision)
            {
                Pawn = pawn;
                Comp = comp;
                Identity = comp.pawnIdentity;
                OriginPage = page;
                Personality = SSCPersonalityRuntimeUtility.Capture(pawn);
                Revision = revision;
            }
        }

        /// <summary>保存本次绘制的文案、状态和行位置，随滚动区一起重算。</summary>
        private sealed class OverviewIdentityView
        {
            internal PageEdit Edit;
            internal Rect IdentityRow, Qualification, TrainerToggle, RecoveryText, RecoveryButton;
            internal string IdentityLabel, LockTip, QualificationLabel, TrainerLabel, TrainerTip, RecoveryHint, RecoveryLabel;
            internal bool TrainerEnabled, TrainerEditable;
            internal float Bottom;
        }

        /// <summary>按当前字体和内容宽度测量控件，供绘制和滚动区共用。</summary>
        private OverviewIdentityView BuildOverviewIdentity(float width, float y)
        {
            const float gap = 8f;
            const float buttonHeight = 34f;
            var view = new OverviewIdentityView { Edit = new PageEdit(source, comp, currentPage, contextRevision) };
            // 按同一游标分配行矩形并累计总高度，长译文会把后续行一并下移。
            Rect Row(float height)
            {
                var rect = new Rect(0f, y, width, height);
                y += height + gap;
                return rect;
            }

            view.IdentityLabel = IdentityLabel(view.Edit.Identity);
            float identityLabelWidth = Mathf.Max(1f, width * .42f - 8f);
            view.IdentityRow = Row(Mathf.Max(buttonHeight, Text.CalcHeight(Strings.ITab_IdentityHeader, identityLabelWidth) + 8f));
            if (SSCIdentityUtility.IsIdentityLocked(source))
                view.LockTip = (SSCImprintUtility.GetImprintLockedIdentity(source) != PawnIdentity.Unset ?
                    "SSC_Imprint_GelTooltip" : "SSC_Identity_BoundTip").Translate();

            // 永久资格来自保存字段，菜单可用性另按设置及当前资格判断。
            view.QualificationLabel = SSCMasterQualificationDisplay.GetLabel(comp.masterQualificationUnlocked);
            view.Qualification = Row(SSCMasterQualificationDisplay.GetHeight(view.QualificationLabel, width, Text.LineHeight));

            // 与旧面板共用任职状态；失格仍显示保存的勾选并允许关闭。
            var trainer = ITab_SexSlaveTraining.GetTrainerIdentityPresentation(source, comp);
            view.TrainerLabel = trainer.Label;
            view.TrainerTip = trainer.Tooltip;
            view.TrainerEnabled = trainer.Enabled;
            view.TrainerEditable = trainer.IsSlave && (trainer.Qualified || trainer.Enabled);
            view.TrainerToggle = Row(Mathf.Max(24f, Text.CalcHeight(trainer.Label, Mathf.Max(1f, width - 24f))));

            if (SSCIdentityUtility.CanRestoreLegacySlaveIdentity(source))
            {
                view.RecoveryHint = "SSC_Identity_LegacyRecoveryHint".Translate();
                view.RecoveryLabel = "SSC_Identity_LegacyRecoveryButton".Translate();
                view.RecoveryText = Row(Text.CalcHeight(view.RecoveryHint, width));
                view.RecoveryButton = Row(Mathf.Max(buttonHeight, Text.CalcHeight(view.RecoveryLabel, Mathf.Max(1f, width - 16f)) + 8f));
            }
            view.Bottom = y;
            return view;
        }

        /// <summary>绘制身份、永久资格、任职与历史锁链的显式恢复入口。</summary>
        private void DrawOverviewIdentity(OverviewIdentityView view)
        {
            var edit = view.Edit;
            Text.Anchor = TextAnchor.UpperLeft;
            DrawIdentityDropdown(view);

            SSCMasterQualificationDisplay.Draw(view.Qualification, view.QualificationLabel,
                SSCMasterQualificationDisplay.GetPawnTooltip(SSCMod.settings?.allowDirectMasterSelection ?? false));
            bool next = SSCUIWidgets.Checkbox(view.TrainerToggle, view.TrainerLabel, view.TrainerEnabled,
                view.TrainerEditable, view.TrainerTip);
            // 任职提交同时核对绘制时的保存值，避免覆盖绘制期间的外部更新。
            if (next != view.TrainerEnabled && PageEditIsCurrent(edit) &&
                edit.Comp.slaveTrainerEnabled == view.TrainerEnabled && SSCIdentityUtility.SetTrainerEnabled(edit.Pawn, next))
                RefreshAfterEdit();

            if (view.RecoveryHint == null) return;
            Widgets.Label(view.RecoveryText, view.RecoveryHint);
            if (SSCUIWidgets.Button(view.RecoveryButton, view.RecoveryLabel, true)) OpenRecoveryConfirmation(edit);
        }

        /// <summary>身份行使用窗口配色，右侧值区域负责打开现有菜单。</summary>
        private void DrawIdentityDropdown(OverviewIdentityView view)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Rect row = view.IdentityRow;
            float valueX = row.x + row.width * .42f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x, row.y, Mathf.Max(1f, valueX - row.x - 8f), row.height), Strings.ITab_IdentityHeader);
            var value = new Rect(valueX, row.y, Mathf.Max(1f, row.xMax - valueX), row.height);
            if (DrawSettingsControl(value, view.IdentityLabel, dropdown: true, enabled: view.LockTip == null, tooltip: view.LockTip))
                OpenIdentityMenu(view.Edit);
        }

        /// <summary>用统一深色样式绘制设置菜单和操作按钮，悬停显示截断值或禁用原因。</summary>
        private static bool DrawSettingsControl(Rect value, string text, bool dropdown = false, bool enabled = true, string tooltip = null)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Anchor = TextAnchor.MiddleLeft;
            bool editable = enabled && GUI.enabled;
            bool showArrow = dropdown && editable && value.width >= 36f;
            Widgets.DrawBoxSolid(value, editable ? HeaderColor : PanelColor);
            Widgets.DrawLineHorizontal(value.x, value.yMax - 1f, value.width, DividerColor);
            if (editable) Widgets.DrawHighlightIfMouseover(value);

            Text.WordWrap = false;
            float padding = Mathf.Min(10f, value.width * .1f);
            var labelRect = new Rect(value.x + padding, value.y,
                Mathf.Max(1f, value.width - 2f * padding - (showArrow ? 16f : 0f)), value.height);
            string label = text.Truncate(labelRect.width);
            Widgets.Label(labelRect, label);
            if (showArrow)
            {
                // 用三道短线组成下拉箭头，避免受语言字体的符号覆盖影响。
                float x = value.xMax - padding - 5f;
                float y = value.y + value.height / 2f;
                for (int i = 0; i < 3; i++)
                    Widgets.DrawBoxSolid(new Rect(x - 4f + i * 2f, y - 2f + i * 2f, 9f - i * 4f, 2f), GUI.color);
            }
            if (tooltip != null || label != text) TooltipHandler.TipRegion(value, tooltip ?? text);
            return editable && Widgets.ButtonInvisible(value);
        }

        /// <summary>使用原主人准入和身份 setter 构造菜单。回调只提交所捕获的编辑请求。</summary>
        private void OpenIdentityMenu(PageEdit edit)
        {
            if (!PageEditIsCurrent(edit) || SSCIdentityUtility.IsIdentityLocked(edit.Pawn)) return;
            bool canSelectMaster = edit.Identity == PawnIdentity.Master || SSCMasterQualificationUtility.CanSelectMaster(edit.Pawn);
            string masterLabel = Strings.ITab_SetAsMaster;
            if (!canSelectMaster) masterLabel += " (" + "SSC_Identity_MasterQualificationRequired".Translate() + ")";
            // 每个回调持有创建它的菜单。旧菜单即使对应同一角色也不能冒用新菜单的资格。
            FloatMenu menu = null;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(Strings.ITab_SetIdentityUnset, () => SubmitIdentity(menu, edit, PawnIdentity.Unset)),
                new FloatMenuOption(Strings.ITab_SetAsSlave, () => SubmitIdentity(menu, edit, PawnIdentity.Slave)),
                new FloatMenuOption(masterLabel, canSelectMaster ? (Action)(() => SubmitIdentity(menu, edit, PawnIdentity.Master)) : null)
            };
            menu = new FloatMenu(options);
            ShowEditPopup(menu);
        }

        /// <summary>身份变化和锁定都可能发生在菜单打开之后，提交时重新检查。</summary>
        private void SubmitIdentity(FloatMenu menu, PageEdit edit, PawnIdentity identity)
        {
            if (!EditPopupIsCurrent(menu, edit) || SSCIdentityUtility.IsIdentityLocked(edit.Pawn)) return;
            if (!SSCIdentityUtility.TrySetIdentity(edit.Pawn, identity) && identity == PawnIdentity.Master)
                Messages.Message("SSC_Identity_SelectionUnavailable".Translate(), edit.Pawn, MessageTypeDefOf.RejectInput, false);
            RefreshAfterEdit();
        }

        /// <summary>确认时复查原锁链和主人，避免恢复操作落到后来建立的绑定上。</summary>
        private void OpenRecoveryConfirmation(PageEdit edit)
        {
            Pawn pawn = edit.Pawn;
            if (!PageEditIsCurrent(edit) || !SSCIdentityUtility.CanRestoreLegacySlaveIdentity(pawn)) return;
            var chain = SSCBondUtility.GetChain(pawn);
            Pawn owner = chain?.LinkedPawn;
            if (owner == null) return;
            Dialog_MessageBox confirmation = null;
            confirmation = Dialog_MessageBox.CreateConfirmation(
                "SSC_Identity_LegacyRecoveryConfirm".Translate(pawn.LabelShort, owner.LabelShort), () =>
                {
                    if (!EditPopupIsCurrent(confirmation, edit)) return;
                    if (SSCBondUtility.GetChain(pawn) != chain ||
                        SSCBondUtility.GetBoundMaster(pawn) != owner || !SSCIdentityUtility.TryRestoreLegacySlaveIdentity(pawn))
                    {
                        Messages.Message("SSC_Identity_LegacyRecoveryRejected".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    RefreshAfterEdit();
                    Messages.Message("SSC_Identity_LegacyRecoveryCompleted".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent, false);
                });
            ShowEditPopup(confirmation);
        }

        /// <summary>只允许原页面、原角色和原人格的操作进入业务入口。</summary>
        private bool PageEditIsCurrent(PageEdit edit)
            => edit.Revision == contextRevision && currentPage == edit.OriginPage &&
                ReferenceEquals(edit.Pawn, source) && ReferenceEquals(edit.Comp, comp) && SourceIsCurrent() &&
                edit.Comp.pawnIdentity == edit.Identity && SSCPersonalityRuntimeUtility.IsCurrent(edit.Pawn, edit.Personality) &&
                !SSCPersonalityRuntimeUtility.IsMutating(edit.Pawn);

        /// <summary>1.7.5005 的菜单和确认框先执行回调再关闭，可在提交时复查弹窗仍在栈中。</summary>
        private bool EditPopupIsCurrent(Window popup, PageEdit edit)
            => ReferenceEquals(popup, editPopup) && PageEditIsCurrent(edit) && Find.WindowStack.IsOpen(popup);

        /// <summary>编辑后用实际保存状态更新标题、关系和候选。</summary>
        private void RefreshAfterEdit()
        {
            InvalidateWindowContext();
            relationSession.RequestRefresh(relationSession.Details);
            nextCandidateRefresh = 0;
        }

        /// <summary>为本窗口保留一个编辑弹窗，便于切角、切页和关闭时撤销。</summary>
        private void ShowEditPopup(Window popup)
        {
            CloseEditPopup();
            editPopup = popup;
            Find.WindowStack.Add(popup);
        }

        /// <summary>先撤销弹窗引用，再让原版窗口栈关闭它。</summary>
        private void CloseEditPopup()
        {
            Window popup = editPopup;
            editPopup = null;
            if (popup != null) Find.WindowStack.TryRemove(popup, false);
        }

        /// <summary>在原生选择写入后立即撤销菜单，逐帧刷新继续负责更新窗口展示。</summary>
        internal static void NotifyEditSelection(Selector selector)
        {
            if (!GameReady() || !ReferenceEquals(selector, Find.Selector)) return;
            var window = Find.WindowStack.WindowOfType<Dialog_SSCTrainingManagement>();
            if (window?.editPopup != null && !window.SourceIsCurrent()) window.InvalidateWindowContext();
        }
    }
}
