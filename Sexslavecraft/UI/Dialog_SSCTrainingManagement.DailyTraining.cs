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
            float indent = Mathf.Min(16f, width * .12f);
            var edit = new PageEdit(source, comp, currentPage, contextRevision);
            var rows = new List<(float Height, Action<Rect> Draw)>();

            // 先按研究状态建立当次控件，日常设置和锁链各自决定是否可用。
            if (ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining))
            {
                AddTrainingSettings();
                AddGroupGap();
                AddScheduleSettings();
            }
            else
            {
                AddLabel(Strings.ITab_TrainingLocked);
                AddLabel(Strings.ITab_TrainingLockedDesc, indent);
            }
            // 锁链阶段有自己的准入和保存载体，在基础研究未完成时仍可显示。
            AddPnaMaintenance();

            // 测量和绘制复用同一批行。关闭排班后先收紧滚动范围，再绘制剩余控件。
            const float rowGap = 4f;
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
            void AddLabel(string label, float offset = 0f, string tooltip = null)
                => rows.Add((Text.CalcHeight(label, Mathf.Max(1f, width - offset)), rect =>
                {
                    var content = Inset(rect, offset);
                    Widgets.Label(content, label);
                    if (tooltip != null) TooltipHandler.TipRegion(content, tooltip);
                }));

            // 组间分隔线保持左对齐，组内行只在左侧增加一级缩进。
            void AddGroupGap() => rows.Add((8f, rect => Widgets.DrawLineHorizontal(rect.x, rect.y + 3f, rect.width, DividerColor)));

            Rect Inset(Rect rect, float offset) => new Rect(rect.x + offset, rect.y, Mathf.Max(1f, rect.width - offset), rect.height);

            // 开关为原版勾选框预留宽度，实际写入前再次核对角色和研究。
            void AddToggle(string label, bool value, Action<bool> submit, float offset = 0f)
            {
                float rowHeight = Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, width - offset - 24f)));
                rows.Add((rowHeight, rect =>
                {
                    bool next = SSCUIWidgets.Checkbox(Inset(rect, offset), label, value, true);
                    if (next != value && CanEditDailyTraining(edit)) submit(next);
                }));
            }

            // 标签和值共享一行。先测量标签，再为右侧下拉预留宽度。
            void AddSetting(string caption, string value, Action action, float offset = 0f, bool enabled = true, string tooltip = null)
            {
                float captionWidth = (width - offset) * .42f;
                float rowHeight = Mathf.Max(ControlHeight(), Text.CalcHeight(caption, Mathf.Max(1f, captionWidth - 8f)));
                rows.Add((rowHeight, rect =>
                {
                    rect = Inset(rect, offset);
                    using var scope = new SSCBondOverviewWidgets.GuiScope(false);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rect.x, rect.y, Mathf.Max(1f, captionWidth - 8f), rect.height), caption);
                    var control = new Rect(rect.x + captionWidth, rect.y, Mathf.Max(1f, rect.width - captionWidth), rect.height);
                    if (DrawSettingsControl(control, value, dropdown: true, enabled: enabled, tooltip: tooltip)) action();
                }));
            }

            float ControlHeight() => Mathf.Max(32f, Text.LineHeight + 6f);

            // 启停和冷却沿用原调教面板；姿势菜单保留打开时的值以防覆盖外部改动。
            void AddTrainingSettings()
            {
                var training = edit.Comp;
                bool enabled = training.IsEnabled;
                var pose = training.selectedMode;
                string heading = Strings.ITab_TrainingSettingsHeader;
                string cooldown = training.IsOnCooldown ? ITab_SexSlaveTraining.GetDailyTrainingStatus(training) : null;
                float headingWidth = Mathf.Max(1f, width * .42f - 8f);
                // 标题独自决定行高。右侧倒计时出现、变长或消失都不改变下方控件位置。
                rows.Add((Mathf.Max(Text.LineHeight, Text.CalcHeight(heading, headingWidth)), rect =>
                {
                    using var scope = new SSCBondOverviewWidgets.GuiScope(false);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rect.x, rect.y, headingWidth, rect.height), heading);
                    if (cooldown == null) return;

                    float offset = rect.width * .42f;
                    var status = Inset(rect, offset);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Text.WordWrap = false;
                    // 裁剪区域而非富文本字符串，窄窗下仍能悬停查看完整倒计时。
                    GUI.BeginGroup(status);
                    try { Widgets.Label(new Rect(0f, 0f, status.width, status.height), cooldown); }
                    finally { GUI.EndGroup(); }
                    if (Text.CalcSize(cooldown).x > status.width) TooltipHandler.TipRegion(status, cooldown);
                }));
                AddToggle(Strings.ITab_AllowTraining, enabled, next =>
                {
                    if (training.IsEnabled == enabled) ITab_SexSlaveTraining.SetDailyTrainingEnabled(edit.Pawn, training, next);
                }, indent);
                AddSetting(Strings.ITab_PoseHeader, Strings.GetModeLabel(pose), () =>
                {
                    if (!CanEditDailyTraining(edit)) return;
                    OpenSettingsMenu(edit, (TrainingActType[])Enum.GetValues(typeof(TrainingActType)), Strings.GetModeLabel, value =>
                    {
                        if (CanEditDailyTraining(edit) && training.selectedMode == pose) training.selectedMode = value;
                    });
                }, indent);
            }

            // 排班只写原组件字段，状态读取组件现有的本地日期和时段判断。
            void AddScheduleSettings()
            {
                var training = edit.Comp;
                bool scheduled = training.scheduledTrainingEnabled;
                int hour = training.scheduledTrainingHour;
                int days = training.scheduledTrainingIntervalDays;
                AddToggle("<b>" + "SSC_Schedule_Header".Translate() + "</b>", scheduled, next =>
                {
                    if (training.scheduledTrainingEnabled == scheduled) training.scheduledTrainingEnabled = next;
                });
                // 关闭仅隐藏细项，保留已保存的开始时间和频率。
                if (!scheduled) return;

                string hourLabel = "SSC_Schedule_HourButton".Translate(hour.ToString("00"), training.ScheduledTrainingEndHour.ToString("00"));
                string intervalLabel = "SSC_Schedule_IntervalButton".Translate(days);
                const float gap = 8f;
                float columnWidth = (width - indent - gap) / 2f;
                // 以两段实际译文和下拉内边距决定是否并排，长译文可提前换行。
                float minimum = Mathf.Max(180f, Mathf.Max(Text.CalcSize(hourLabel).x, Text.CalcSize(intervalLabel).x) + 36f);
                if (columnWidth >= minimum)
                    rows.Add((ControlHeight(), rect =>
                    {
                        var first = new Rect(rect.x + indent, rect.y, columnWidth, rect.height);
                        var second = new Rect(first.xMax + gap, rect.y, columnWidth, rect.height);
                        DrawScheduleControl(first, hourLabel, OpenHourMenu);
                        DrawScheduleControl(second, intervalLabel, OpenIntervalMenu);
                    }));
                else
                {
                    rows.Add((ControlHeight(), rect => DrawScheduleControl(Inset(rect, indent), hourLabel, OpenHourMenu)));
                    rows.Add((ControlHeight(), rect => DrawScheduleControl(Inset(rect, indent), intervalLabel, OpenIntervalMenu)));
                }

                bool due = training.IsScheduledTrainingAvailableNow;
                AddLabel((due ? "SSC_TrainingManagement_ScheduleDue" : "SSC_TrainingManagement_ScheduleWaiting").Translate(), indent,
                    (due ? "SSC_Schedule_StatusNow" : "SSC_Schedule_StatusWaiting").Translate());

                void DrawScheduleControl(Rect rect, string label, Action action)
                {
                    if (DrawSettingsControl(rect, label, dropdown: true)) action();
                }

                void OpenHourMenu()
                {
                    if (!ScheduleIsEditable()) return;
                    // 用现有窗口长度折算次日结束时刻；保存字段只接收开始小时。
                    OpenSettingsMenu(edit, Enumerable.Range(0, 24), start => "SSC_Schedule_HourOption".Translate(
                        start.ToString("00"), ((start + CompSexSlaveTraining.ScheduledTrainingWindowHours) % 24).ToString("00")), value =>
                    {
                        if (ScheduleIsEditable() && training.scheduledTrainingHour == hour) training.scheduledTrainingHour = value;
                    });
                }

                void OpenIntervalMenu()
                {
                    if (!ScheduleIsEditable()) return;
                    OpenSettingsMenu(edit, Enumerable.Range(1, 7), interval => "SSC_Schedule_IntervalOption".Translate(interval), value =>
                    {
                        if (ScheduleIsEditable() && training.scheduledTrainingIntervalDays == days) training.scheduledTrainingIntervalDays = value;
                    });
                }

                // 排班关闭后，已打开的时间和频率菜单也无权改值。
                bool ScheduleIsEditable() => CanEditDailyTraining(edit) && training.scheduledTrainingEnabled;
            }

            // 只为实际锁链生成维持控件。档位菜单保存原链、主人和设置代次，提交时不重拍凭据。
            void AddPnaMaintenance()
            {
                var chain = SSCPnaMaintenanceUtility.Capture(edit.Pawn);
                if (chain.Chain == null) return;
                AddGroupGap();
                bool canSet = SSCPnaMaintenanceUtility.CanSet(chain, SSCPnaMaintenanceStage.Off, out string reason);
                AddSetting("<b>" + "SSC_PnaMaintenance_Title".Translate() + "</b>", SSCPnaMaintenanceUtility.StageLabel(chain.Stage), () =>
                {
                    if (!PageEditIsCurrent(edit) || !SSCPnaMaintenanceUtility.CanSet(chain, SSCPnaMaintenanceStage.Off, out _)) return;
                    OpenSettingsMenu(edit, (SSCPnaMaintenanceStage[])Enum.GetValues(typeof(SSCPnaMaintenanceStage)),
                        SSCPnaMaintenanceUtility.StageLabel, stage =>
                        {
                            if (SSCPnaMaintenanceUtility.TrySet(chain, stage, out string rejection)) RefreshAfterEdit();
                            else Messages.Message(rejection, edit.Pawn, MessageTypeDefOf.RejectInput, false);
                        }, stage => SSCPnaMaintenanceUtility.CanSet(chain, stage, out string rejection) ? null : rejection);
                }, enabled: canSet, tooltip: reason ?? "SSC_PnaMaintenance_Tooltip".Translate().ToString());
            }
        }

        /// <summary>日常调教和身体页共用的设置菜单，提交先核对原弹窗和页面，再进入对应业务校验。</summary>
        private void OpenSettingsMenu<T>(PageEdit edit, IEnumerable<T> values, Func<T, string> label, Action<T> submit,
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
