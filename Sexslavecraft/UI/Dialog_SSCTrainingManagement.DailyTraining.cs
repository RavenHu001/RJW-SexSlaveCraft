// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private Vector2 dailyTrainingScroll;

        /// <summary>日常设置复查原页面、受训者和基础调教研究。</summary>
        private bool CanEditDailyTraining(PageEdit edit)
            => PageEditIsCurrent(edit) && edit.Identity == PawnIdentity.Slave &&
                ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining);

        /// <summary>为日常调教构建并绘制独立滚动内容。研究只限制普通调教设置。</summary>
        private void DrawDailyTraining(Rect page)
        {
            Widgets.DrawBoxSolid(page, PanelColor);
            var viewport = new Rect(page.x + PanelPadding, page.y + PanelPadding,
                Mathf.Max(1f, page.width - 2f * PanelPadding), Mathf.Max(1f, page.height - 2f * PanelPadding));
            float width = Mathf.Max(1f, viewport.width - 16f);
            var edit = new PageEdit(source, comp, currentPage, contextRevision);
            var rows = new List<(float Height, Action<Rect> Draw)>();

            // 先按研究状态建立当次控件，日常设置和锁链各自决定是否可用。
            AddLabel("<b>" + PageKeys[(int)Page.DailyTraining].Translate() + "</b>");
            if (ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining))
            {
                AddTrainingSettings();
                AddScheduleSettings();
                AddTrainerStatus();
            }
            else
            {
                AddLabel(Strings.ITab_TrainingLocked);
                AddLabel(Strings.ITab_TrainingLockedDesc);
            }
            // 锁链阶段有自己的准入和保存载体，在基础研究未完成时仍可显示。
            AddPnaMaintenance();

            // 测量和绘制复用同一批行。关闭排班后先收紧滚动范围，再绘制剩余控件。
            const float rowGap = 6f;
            float height = 0f;
            foreach (var row in rows) height += row.Height + rowGap;
            dailyTrainingScroll.y = Mathf.Clamp(dailyTrainingScroll.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref dailyTrainingScroll, new Rect(0f, 0f, width, Mathf.Max(height, viewport.height)));
            try
            {
                float y = 0f;
                foreach (var row in rows)
                {
                    row.Draw(new Rect(0f, y, width, row.Height));
                    y += row.Height + rowGap;
                }
            }
            finally { Widgets.EndScrollView(); }

            // 长说明使用当前字体的实际换行高度。
            void AddLabel(string label) => rows.Add((Text.CalcHeight(label, width), rect => Widgets.Label(rect, label)));

            // 开关为原版勾选框预留宽度，实际写入前再次核对角色和研究。
            void AddToggle(string label, bool value, Action<bool> submit)
            {
                float rowHeight = Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, width - 24f)));
                rows.Add((rowHeight, rect =>
                {
                    bool next = SSCUIWidgets.Checkbox(rect, label, value, true);
                    if (next != value && CanEditDailyTraining(edit)) submit(next);
                }));
            }

            // 下拉和普通操作共用概况的深色控件，长值通过悬停查看。
            void AddControl(string label, Action action, bool dropdown = false, bool enabled = true, string tooltip = null)
                => rows.Add((Mathf.Max(34f, Text.LineHeight + 8f), rect =>
                {
                    if (DrawSettingsControl(rect, label, dropdown, enabled, tooltip)) action();
                }));

            // 启停和冷却沿用原调教面板；姿势菜单保留打开时的值以防覆盖外部改动。
            void AddTrainingSettings()
            {
                var training = edit.Comp;
                bool enabled = training.IsEnabled;
                var pose = training.selectedMode;
                AddLabel(Strings.ITab_TrainingSettingsHeader);
                AddToggle(Strings.ITab_AllowTraining, enabled, next =>
                {
                    if (training.IsEnabled == enabled) ITab_SexSlaveTraining.SetDailyTrainingEnabled(edit.Pawn, training, next);
                });
                AddLabel(ITab_SexSlaveTraining.GetDailyTrainingStatus(training));
                AddLabel(Strings.ITab_PoseHeader);
                AddControl(Strings.GetModeLabel(pose), () =>
                {
                    if (!CanEditDailyTraining(edit)) return;
                    OpenDailyMenu(edit, (TrainingActType[])Enum.GetValues(typeof(TrainingActType)), Strings.GetModeLabel, value =>
                    {
                        if (CanEditDailyTraining(edit) && training.selectedMode == pose) training.selectedMode = value;
                    });
                }, dropdown: true);
            }

            // 排班只写原组件字段，状态读取组件现有的本地日期和时段判断。
            void AddScheduleSettings()
            {
                var training = edit.Comp;
                bool scheduled = training.scheduledTrainingEnabled;
                int hour = training.scheduledTrainingHour;
                int days = training.scheduledTrainingIntervalDays;
                AddLabel("<b>" + "SSC_Schedule_Header".Translate() + "</b>");
                AddToggle("SSC_Schedule_Enable".Translate(), scheduled, next =>
                {
                    if (training.scheduledTrainingEnabled == scheduled) training.scheduledTrainingEnabled = next;
                });
                if (!scheduled)
                {
                    // 关闭仅隐藏细项，保留已保存的开始时间和频率。
                    AddLabel("SSC_Schedule_StatusAnytime".Translate());
                    return;
                }

                AddControl("SSC_Schedule_HourButton".Translate(hour.ToString("00"), training.ScheduledTrainingEndHour.ToString("00")), () =>
                {
                    if (!ScheduleIsEditable()) return;
                    // 用现有窗口长度折算次日结束时刻；保存字段只接收开始小时。
                    OpenDailyMenu(edit, Enumerable.Range(0, 24), start => "SSC_Schedule_HourOption".Translate(
                        start.ToString("00"), ((start + CompSexSlaveTraining.ScheduledTrainingWindowHours) % 24).ToString("00")), value =>
                    {
                        if (ScheduleIsEditable() && training.scheduledTrainingHour == hour) training.scheduledTrainingHour = value;
                    });
                }, dropdown: true);
                AddControl("SSC_Schedule_IntervalButton".Translate(days), () =>
                {
                    if (!ScheduleIsEditable()) return;
                    OpenDailyMenu(edit, Enumerable.Range(1, 7), interval => "SSC_Schedule_IntervalOption".Translate(interval), value =>
                    {
                        if (ScheduleIsEditable() && training.scheduledTrainingIntervalDays == days) training.scheduledTrainingIntervalDays = value;
                    });
                }, dropdown: true);
                AddLabel((training.IsScheduledTrainingAvailableNow ? "SSC_Schedule_StatusNow" : "SSC_Schedule_StatusWaiting").Translate());

                // 排班关闭后，已打开的时间和频率菜单也无权改值。
                bool ScheduleIsEditable() => CanEditDailyTraining(edit) && training.scheduledTrainingEnabled;
            }

            // 现任及其工作状态按保存指派显示，调整指派转到关系页。
            void AddTrainerStatus()
            {
                Pawn trainer = edit.Comp.selectedTrainer;
                AddLabel(Strings.ITab_TrainerHeader);
                AddLabel(trainer == null ? Strings.ITab_TrainerNone : trainer.LabelShort.ToString());
                if (trainer != null)
                {
                    var state = SSCTrainerWorkUtility.GetState(trainer, edit.Pawn);
                    AddLabel(SSCTrainerWorkUtility.Reason(state, trainer));
                    // 只打开现任的工作优先级，使用原事务捕获指派和工作载体。
                    if (state == SSCTrainerWorkState.Off)
                    {
                        var enable = SSCTrainerAssignmentMenu.EnableOption(edit.Pawn, RefreshAfterEdit, () => CanEditDailyTraining(edit));
                        if (enable != null) AddControl(enable.Label, enable.action, tooltip: "SSC_TrainerWork_CategoryTip".Translate());
                    }
                }
                AddControl(PageKeys[(int)Page.Relations].Translate(), () =>
                {
                    if (PageEditIsCurrent(edit)) ChangePage(Page.Relations);
                });
            }

            // 只为实际锁链生成维持控件。档位菜单保存原链、主人和设置代次，提交时不重拍凭据。
            void AddPnaMaintenance()
            {
                var chain = SSCPnaMaintenanceUtility.Capture(edit.Pawn);
                if (chain.Chain == null) return;
                AddLabel("<b>" + "SSC_PnaMaintenance_Title".Translate() + "</b>");
                bool canSet = SSCPnaMaintenanceUtility.CanSet(chain, SSCPnaMaintenanceStage.Off, out string reason);
                AddControl(SSCPnaMaintenanceUtility.StageLabel(chain.Stage), () =>
                {
                    if (!PageEditIsCurrent(edit) || !SSCPnaMaintenanceUtility.CanSet(chain, SSCPnaMaintenanceStage.Off, out _)) return;
                    OpenDailyMenu(edit, (SSCPnaMaintenanceStage[])Enum.GetValues(typeof(SSCPnaMaintenanceStage)),
                        SSCPnaMaintenanceUtility.StageLabel, stage =>
                        {
                            if (SSCPnaMaintenanceUtility.TrySet(chain, stage, out string rejection)) RefreshAfterEdit();
                            else Messages.Message(rejection, edit.Pawn, MessageTypeDefOf.RejectInput, false);
                        }, stage => SSCPnaMaintenanceUtility.CanSet(chain, stage, out string rejection) ? null : rejection);
                }, dropdown: true, enabled: canSet, tooltip: reason ?? "SSC_PnaMaintenance_Tooltip".Translate().ToString());
            }
        }

        /// <summary>菜单提交先核对原弹窗和页面，再进入各设置的业务校验。</summary>
        private void OpenDailyMenu<T>(PageEdit edit, IEnumerable<T> values, Func<T, string> label, Action<T> submit,
            Func<T, string> unavailable = null)
        {
            if (!PageEditIsCurrent(edit)) return;
            FloatMenu menu = null;
            var options = new List<FloatMenuOption>();
            // 禁用项保留原因；可用项在原版关闭弹窗前验证同一个实例，再执行各自的提交。
            foreach (T value in values)
            {
                string reason = unavailable?.Invoke(value);
                options.Add(reason != null ? new FloatMenuOption(label(value), null) { tooltip = reason }
                    : new FloatMenuOption(label(value), () =>
                    {
                        if (EditPopupIsCurrent(menu, edit)) submit(value);
                    }));
            }
            menu = new FloatMenu(options);
            ShowEditPopup(menu);
        }
    }
}
