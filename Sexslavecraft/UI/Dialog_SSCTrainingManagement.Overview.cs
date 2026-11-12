// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static SexSlaveCraft.SSCTrainingWindowTheme;

namespace SexSlaveCraft
{
    internal sealed partial class Dialog_SSCTrainingManagement
    {
        private const float ImprintHorizontalPadding = 6f;

        // 各页面共用唯一的编辑弹窗，由窗口负责撤销。
        private Window editPopup;
        private (object Key, Action Submit) pageActionPress;

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

        /// <summary>分别测量性奴化进度、恶堕率和日常调教。宽窗把后两块放在右栏。</summary>
        private static (float Height, Action<float> Draw) BuildSlaveOverview(PageEdit edit, float width)
        {
            bool wide = width >= SettingsTwoColumnWidth;
            float progressWidth = wide ? (width - SettingsColumnGap) * .63f : width;
            float statusWidth = wide ? width - progressWidth - SettingsColumnGap : width;
            var progress = new SettingsColumn(progressWidth, panel: true);
            var daily = new SettingsColumn(statusWidth, panel: true);
            string heading = "<b>" + "SSC_TrainingManagement_SlaveProgress".Translate() + "</b>";

            var chain = SSCBondUtility.GetChain(edit.Pawn);
            if (chain == null || !float.IsFinite(chain.Severity))
            {
                progress.AddLabel(heading);
                progress.AddLabel("SSC_TrainingManagement_NoChainProgress".Translate());
            }
            else
            {
                // 玩法阶段从身份工具读取，刻度使用同一条锁链的 Def，避免复制阈值。
                var stages = chain.def.stages;
                int total = stages?.Count ?? 0;
                int reached = Mathf.Clamp(SSCIdentityUtility.GetSexSlaveStageFromChain(chain), 0, total);
                string stage = reached > 0 ? stages[reached - 1]?.label : null;
                stage ??= "SSC_TrainingManagement_BeforeChainStage".Translate();
                bool maxStage = total > 0 && reached == total;
                // 向下显示整数百分比，跨过实际门槛之前不提前显示目标值。
                string value = (Mathf.Floor(Mathf.Clamp01(chain.Severity) * 100f) / 100f).ToString("0%");
                Color accent = StageAccent(PawnIdentity.Slave, reached, total);
                AddOverviewValue(progress, heading, value, accent);
                progress.AddLabel("SSC_TrainingManagement_SlaveCurrentStage".Translate(stage),
                    tooltip: maxStage ? "SSC_MasterProgression_MaxStage".Translate().ToString() : null, textColor: accent);
                AddOverviewProgressScale(progress, chain, chain.Severity, reached, asPercent: true);
            }

            // 恶堕率独立读取当前需求。缺少该需求时直接收起面板及其间距。
            SettingsColumn corruption = null;
            var need = edit.Pawn.needs?.TryGetNeed<Need_Corruption>();
            if (need != null)
            {
                corruption = new SettingsColumn(statusWidth, panel: true);
                AddCorruptionStatus(corruption, need);
            }

            daily.AddLabel("<b>" + PageKeys[2].Translate() + "</b>");
            AddOverviewValue(daily, Strings.ITab_AllowTraining, (edit.Comp.IsEnabled
                ? "SSC_TrainingManagement_TrainingEnabled" : "SSC_TrainingManagement_TrainingDisabled").Translate());
            if (edit.Comp.IsOnCooldown)
                daily.AddLabel(ITab_SexSlaveTraining.GetDailyTrainingStatus(edit.Comp));

            // 面板各自按内容收底，只用最高一栏决定宽窗的总占高。
            float statusX = wide ? progressWidth + SettingsColumnGap : 0f;
            float statusY = wide ? 0f : progress.PanelHeight + SettingsColumnGap;
            float dailyY = statusY + (corruption == null ? 0f : corruption.PanelHeight + SettingsColumnGap);
            float height = Mathf.Max(progress.PanelHeight, dailyY + daily.PanelHeight);
            return (height, y =>
            {
                progress.DrawPanel(0f, y, progress.PanelHeight);
                corruption?.DrawPanel(statusX, y + statusY, corruption.PanelHeight);
                daily.DrawPanel(statusX, y + dailyY, daily.PanelHeight);
            });
        }

        /// <summary>测量两侧译文并右对齐只读值，给较长的标签留出换行空间。</summary>
        private static void AddOverviewValue(SettingsColumn column, string label, string value, Color? valueColor = null)
        {
            float gap = Mathf.Min(8f, column.Width * .1f);
            float valueWidth = Mathf.Clamp(Text.CalcSize(value).x + 2f, column.Width * .25f, column.Width * .5f);
            float labelWidth = column.Width - valueWidth - gap;
            float height = Mathf.Max(Text.CalcHeight(label, Mathf.Max(1f, labelWidth)),
                Text.CalcHeight(value, Mathf.Max(1f, valueWidth)));
            column.Rows.Add((height, rect =>
            {
                using var scope = new SSCBondOverviewWidgets.GuiScope(false);
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);
                Text.Anchor = TextAnchor.UpperRight;
                using var tint = new TextTint(valueColor ?? GUI.contentColor);
                Widgets.Label(new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height), value);
            }));
        }

        /// <summary>保留独立的恶堕面板，第二行并排绘制当前档位和细条。</summary>
        private static void AddCorruptionStatus(SettingsColumn column, Need_Corruption need)
        {
            // 档位和细条都随当前需求回落。百分比向下取整，避免在临界点提前显示下一档门槛。
            var stage = need.GetStagePresentation();
            float fraction = Mathf.Clamp01(need.CurLevelPercentage);
            Color accent = CorruptionAccent(stage.Index, stage.ThresholdCount);
            string percent = (Mathf.Floor(fraction * 100f) / 100f).ToString("0%");
            AddOverviewValue(column, "SSC_TrainingManagement_CurrentCorruption".Translate(), percent, accent);
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Font = GameFont.Small;
            float trackWidth = Mathf.Min(64f, column.Width * .28f);
            float labelWidth = Mathf.Max(1f, column.Width - trackWidth - 8f);
            float height = Text.CalcHeight(stage.Label, labelWidth);
            column.Rows.Add((height, rect =>
            {
                using var drawScope = new SSCBondOverviewWidgets.GuiScope(false);
                using var tint = new TextTint(accent);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), stage.Label);
                DrawStageBar(new Rect(rect.xMax - trackWidth, rect.center.y - 2f, trackWidth, 4f), fraction, accent);
            }));
        }

        /// <summary>保存本次绘制的文案、控件位置和原印记引用，随滚动区一起重算。</summary>
        private sealed class OverviewIdentityView
        {
            internal PageEdit Edit;
            internal Rect IdentityCaption, IdentityDropdown, ImprintRect, Qualification, TrainerToggle, RecoveryText, RecoveryButton;
            internal string IdentityLabel, LockTip, ImprintLabel, QualificationLabel, TrainerLabel, TrainerTip, RecoveryHint, RecoveryLabel;
            internal Hediff Imprint;
            internal bool TrainerEnabled, TrainerEditable;
            internal float Bottom;
        }

        /// <summary>测量身份、印记和后续控件，把换行后的总高度交给概况滚动区。</summary>
        private OverviewIdentityView BuildOverviewIdentity(float panelWidth)
        {
            const float gap = 8f;
            const float buttonHeight = 34f;
            float inset = SettingsPanelInset(panelWidth);
            float width = panelWidth - 2f * inset;
            float y = SettingsPanelPadding;
            float columnGap = Mathf.Min(12f, width * .05f);
            var view = new OverviewIdentityView { Edit = new PageEdit(source, comp, currentPage, contextRevision) };
            // 按同一游标分配行矩形并累计总高度，长译文会把后续行一并下移。
            Rect Row(float height)
            {
                var rect = new Rect(inset, y, width, height);
                y += height + gap;
                return rect;
            }

            view.IdentityLabel = IdentityLabel(view.Edit.Identity);
            // 当前人格的印记由健康记录提供。只显示已有记录，无印记时不生成占位文案。
            view.Imprint = SSCImprintUtility.Find(view.Edit.Pawn);
            if (view.Imprint != null)
                view.ImprintLabel = "SSC_Imprint_GelStatus".Translate(view.Imprint.def.LabelCap);

            // 下拉按各身份的本地化显示值测宽，右侧有没有印记都使用同一位置与宽度。
            float captionWidth = Mathf.Min(width * .40f, Text.CalcSize(Strings.ITab_IdentityHeader).x + 1f);
            float dropdownX = captionWidth + columnGap;
            float longestIdentity = Mathf.Max(Text.CalcSize(IdentityLabel(PawnIdentity.Unset)).x,
                Mathf.Max(Text.CalcSize(IdentityLabel(PawnIdentity.Master)).x, Text.CalcSize(IdentityLabel(PawnIdentity.Slave)).x));
            // 给箭头和文字内边距留宽度，同时限制下拉最多占用本行的比例。
            float dropdownWidth = Mathf.Min(width * .46f, Mathf.Max(120f, longestIdentity + 38f));
            float imprintX = dropdownX + dropdownWidth + columnGap;
            float imprintWidth = width - imprintX;
            // 只在剩余空间足以显示短标签或供长标签换行时使用右列。
            bool inline = view.ImprintLabel != null && imprintWidth >= Mathf.Min(120f,
                Text.CalcSize(view.ImprintLabel).x + 2f * ImprintHorizontalPadding);
            float identityHeight = Mathf.Max(buttonHeight, Text.CalcHeight(Strings.ITab_IdentityHeader, Mathf.Max(1f, captionWidth)) + 8f);
            float imprintHeight = view.ImprintLabel == null ? 0f :
                Text.CalcHeight(view.ImprintLabel, Mathf.Max(1f, (inline ? imprintWidth : width) - 2f * ImprintHorizontalPadding)) + 4f;
            var row = Row(inline ? Mathf.Max(identityHeight, imprintHeight) : identityHeight);
            view.IdentityCaption = new Rect(row.x, row.y, captionWidth, identityHeight);
            view.IdentityDropdown = new Rect(row.x + dropdownX, row.y, dropdownWidth, identityHeight);
            // 同行容许长名称换行；太窄时才在下一行使用全宽，纳入同一滚动区的测高。
            if (view.ImprintLabel != null)
                view.ImprintRect = inline ? new Rect(row.x + imprintX, row.y, imprintWidth, row.height) : Row(imprintHeight);

            if (SSCIdentityUtility.IsIdentityLocked(source))
                view.LockTip = (SSCImprintUtility.GetImprintLockedIdentity(source) != PawnIdentity.Unset ?
                    "SSC_Imprint_GelTooltip" : "SSC_Identity_BoundTip").Translate();

            // 未设身份时保留资格提示。身份菜单的可用性仍按设置与现有资格判定。
            if (view.Edit.Identity == PawnIdentity.Unset)
            {
                view.QualificationLabel = SSCMasterQualificationDisplay.GetLabel(comp.masterQualificationUnlocked);
                view.Qualification = Row(SSCMasterQualificationDisplay.GetHeight(view.QualificationLabel, width, Text.LineHeight));
            }

            // 主人的任职状态已在窗口标题下显示。性奴仍在这里管理任职，失格后也可关闭旧选择。
            var trainer = ITab_SexSlaveTraining.GetTrainerIdentityPresentation(source, comp);
            if (trainer.IsSlave)
            {
                // 勾选框已经表达保存选择。失格但仍有勾选时保留暂停状态，避免误读为正在任职。
                view.TrainerLabel = trainer.Enabled && !trainer.Qualified ? trainer.Label : "SSC_TrainerIdentity_Label".Translate().ToString();
                view.TrainerTip = trainer.Tooltip;
                view.TrainerEnabled = trainer.Enabled;
                view.TrainerEditable = trainer.Qualified || trainer.Enabled;
                view.TrainerToggle = Row(Mathf.Max(24f, Text.CalcHeight(view.TrainerLabel, Mathf.Max(1f, width - 24f))));
            }

            if (SSCIdentityUtility.CanRestoreLegacySlaveIdentity(source))
            {
                view.RecoveryHint = "SSC_Identity_LegacyRecoveryHint".Translate();
                view.RecoveryLabel = "SSC_Identity_LegacyRecoveryButton".Translate();
                view.RecoveryText = Row(Text.CalcHeight(view.RecoveryHint, width));
                view.RecoveryButton = Row(Mathf.Max(buttonHeight, Text.CalcHeight(view.RecoveryLabel, Mathf.Max(1f, width - 16f)) + 8f));
            }
            view.Bottom = y - gap + SettingsPanelPadding;
            return view;
        }

        /// <summary>绘制身份与印记、永久资格、任职和旧档恢复入口。</summary>
        private void DrawOverviewIdentity(OverviewIdentityView view)
        {
            var edit = view.Edit;
            Text.Anchor = TextAnchor.UpperLeft;
            DrawIdentityRow(view);

            if (view.QualificationLabel != null)
                SSCMasterQualificationDisplay.Draw(view.Qualification, view.QualificationLabel,
                    SSCMasterQualificationDisplay.GetPawnTooltip(SSCMod.settings?.allowDirectMasterSelection ?? false));
            if (view.TrainerLabel != null)
            {
                bool next = SSCUIWidgets.Checkbox(view.TrainerToggle, view.TrainerLabel, view.TrainerEnabled,
                    view.TrainerEditable, view.TrainerTip);
                // 任职提交同时核对绘制时的保存值，避免覆盖绘制期间的外部更新。
                if (next != view.TrainerEnabled && PageEditIsCurrent(edit) &&
                    edit.Comp.slaveTrainerEnabled == view.TrainerEnabled && SSCIdentityUtility.SetTrainerEnabled(edit.Pawn, next))
                    RefreshAfterEdit();
            }

            if (view.RecoveryHint == null) return;
            Widgets.Label(view.RecoveryText, view.RecoveryHint);
            if (SSCUIWidgets.Button(view.RecoveryButton, view.RecoveryLabel, true)) OpenRecoveryConfirmation(edit);
        }

        /// <summary>印记根据实际定型方向描边，整块区域提供原健康记录的详情。</summary>
        private void DrawIdentityRow(OverviewIdentityView view)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(view.IdentityCaption, Strings.ITab_IdentityHeader);
            if (DrawSettingsControl(view.IdentityDropdown, view.IdentityLabel, dropdown: true, enabled: view.LockTip == null, tooltip: view.LockTip))
                OpenIdentityMenu(view.Edit);

            if (view.Imprint == null) return;
            var imprintRect = view.ImprintRect;
            // 按印记自身的定型状态着色。未定型角色也可能已取得 SSC 身份。
            var kind = SSCImprintUtility.Kind(view.Imprint.def);
            bool locked = kind == SSCImprintKind.Master || kind == SSCImprintKind.Slave;
            Color accent = kind == SSCImprintKind.Master ? CopperColor : kind == SSCImprintKind.Slave ? ProgressFill : SecondaryText;
            Widgets.DrawBoxSolid(imprintRect, ControlColor);
            DrawStateOutline(imprintRect, locked ? accent : DividerColor, dashed: !locked);
            using (new TextTint(accent))
                Widgets.Label(new Rect(imprintRect.x + ImprintHorizontalPadding, imprintRect.y,
                    Mathf.Max(1f, imprintRect.width - 2f * ImprintHorizontalPadding), imprintRect.height), view.ImprintLabel);
            // 原版健康提示包含当前阶段效果，并会查战斗日志，只有悬停时才生成。
            TooltipHandler.TipRegion(view.ImprintRect, () => ImprintTooltip(view), view.Imprint.GetHashCode());
        }

        /// <summary>从原印记 Def 读取定型方向，悬停时复查当前角色及原记录。</summary>
        private string ImprintTooltip(OverviewIdentityView view)
        {
            var imprint = view.Imprint;
            var pawn = view.Edit.Pawn;
            if (!PageEditIsCurrent(view.Edit) || !ReferenceEquals(SSCImprintUtility.Find(pawn), imprint))
                return string.Empty;

            // 初始印记者可以先取得 SSC 身份，悬停中的定型状态仍只由当前印记 Def 决定。
            string status = SSCImprintUtility.Kind(imprint.def) switch
            {
                SSCImprintKind.Master => "SSC_Imprint_MasterLocked",
                SSCImprintKind.Slave => "SSC_Imprint_SlaveLocked",
                _ => "SSC_Imprint_Unlocked"
            };
            return status.Translate() + "\n\n" + imprint.GetTooltip(pawn, false);
        }

        /// <summary>用统一深色样式绘制设置菜单和操作按钮，悬停显示截断值或禁用原因。</summary>
        private static bool DrawSettingsControl(Rect value, string text, bool dropdown = false, bool enabled = true, string tooltip = null)
        {
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Anchor = TextAnchor.MiddleLeft;
            bool editable = enabled && GUI.enabled;
            bool showArrow = dropdown && editable && value.width >= 36f;
            Widgets.DrawBoxSolid(value, editable && !dropdown ? SelectedColor : ControlColor);
            Widgets.DrawLineHorizontal(value.x, value.yMax - 1f, value.width, DividerColor);
            if (editable) Widgets.DrawHighlightIfMouseover(value);

            Text.WordWrap = false;
            float padding = Mathf.Min(10f, value.width * .1f);
            var labelRect = new Rect(value.x + padding, value.y,
                Mathf.Max(1f, value.width - 2f * padding - (showArrow ? 16f : 0f)), value.height);
            string label = text.Truncate(labelRect.width);
            using (new TextTint(editable ? PrimaryText : DisabledText)) Widgets.Label(labelRect, label);
            if (showArrow)
            {
                // 用三道短线组成下拉箭头，避免受语言字体的符号覆盖影响。
                float x = value.xMax - padding - 5f;
                float y = value.y + value.height / 2f;
                for (int i = 0; i < 3; i++)
                    Widgets.DrawBoxSolid(new Rect(x - 4f + i * 2f, y - 2f + i * 2f, 9f - i * 4f, 2f), SecondaryText);
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

        /// <summary>测量成长与能力的实际行高，决定它们在概况滚动区中的排列。</summary>
        private (float Height, Action<float> Draw) BuildMasterOverview(PageEdit edit, float width, float availableHeight)
        {
            var growth = ITab_SexSlaveTraining.ReadMasterProgression(edit.Pawn, compactExperience: true);
            var abilities = SSCAbilityToggleUtility.Definitions
                .Where(definition => definition.Group == SSCAbilityToggleGroup.Master)
                .OrderBy(definition => definition.Order)
                .Select(definition => (Definition: definition, State: SSCAbilityToggleUtility.GetView(edit.Pawn, definition.Id)))
                .Where(item => item.State.Visible).ToArray();
            if (abilities.Length == 0) return LayoutSettingsPanelRow(BuildGrowth(width));

            bool wide = width >= SettingsTwoColumnWidth;
            string abilityHeading = "<b>" + "SSC_TrainingManagement_MasterAbilities".Translate() + "</b>";
            if (wide)
            {
                float growthWidth = (width - SettingsColumnGap) * .56f;
                var left = BuildGrowth(growthWidth);
                var right = new SettingsColumn(width - growthWidth - SettingsColumnGap, panel: true);
                right.AddLabel(abilityHeading);
                for (int i = 0; i < abilities.Length; i++) AddAbility(right, i);

                // 能力区能装进剩余视口，或不超过左边已有的滚动高度时，优先并排。
                if (right.PanelHeight <= Mathf.Max(availableHeight, left.PanelHeight))
                    return LayoutSettingsPanelRow(left, right);
            }

            var fullGrowth = BuildGrowth(width);
            var heading = new SettingsColumn(width, panel: true);
            heading.AddLabel(abilityHeading);
            bool twoAbilityColumns = wide && abilities.Length > 1;
            float abilityWidth = twoAbilityColumns ? (heading.Width - SettingsColumnGap) / 2f : heading.Width;
            var first = new SettingsColumn(abilityWidth);
            var second = twoAbilityColumns ? new SettingsColumn(abilityWidth) : null;
            for (int i = 0; i < abilities.Length; i++)
                AddAbility(twoAbilityColumns && i % 2 != 0 ? second : first, i);

            // 由左到右保留注册顺序。长标签撑高整行，使下一对控件仍然对齐。
            if (second != null)
                for (int i = 0; i < second.Rows.Count; i++)
                {
                    var left = first.Rows[i];
                    var right = second.Rows[i];
                    float height = Mathf.Max(left.Height, right.Height);
                    first.Rows[i] = (height, left.Draw);
                    second.Rows[i] = (height, right.Draw);
                }

            float headingY = fullGrowth.PanelHeight + SettingsColumnGap;
            float abilityHeight = heading.PanelHeight + Mathf.Max(first.Height, second?.Height ?? 0f);
            float abilitiesY = headingY + SettingsPanelPadding + heading.Height;
            float inset = SettingsPanelInset(width);
            return (headingY + abilityHeight, y =>
            {
                fullGrowth.DrawPanel(0f, y, fullGrowth.PanelHeight);
                // 能力的两列共用同一块底色，分隔只出现在成长和能力两个模块之间。
                heading.DrawPanel(0f, y + headingY, abilityHeight);
                first.Draw(inset, y + abilitiesY);
                second?.Draw(inset + abilityWidth + SettingsColumnGap, y + abilitiesY);
            });

            void AddAbility(SettingsColumn column, int index)
            {
                var ability = abilities[index];
                AddAbilityToggle(column, edit, ability.Definition, ability.State, column.Indent);
            }

            SettingsColumn BuildGrowth(float columnWidth)
            {
                var column = new SettingsColumn(columnWidth, panel: true);
                column.AddLabel("<b>" + "SSC_TrainingManagement_MasterGrowth".Translate() + "</b>");
                string label = growth.Record == null ? growth.Stage : growth.Stage + " · " + growth.Experience;
                var stages = growth.Record?.def.stages;
                int nextStage = (growth.Record?.CurStageIndex ?? -1) + 1;
                bool maxStage = stages?.Count > 0 && nextStage >= stages.Count;
                if (maxStage) label += " · " + growth.Progress;
                float height = Text.CalcHeight(label, Mathf.Max(1f, column.Width - column.Indent));
                column.Rows.Add((height, rect =>
                {
                    rect = InsetSettingsRow(rect, column.Indent);
                    using var tint = new TextTint(growth.Record == null ? GUI.contentColor :
                        StageAccent(PawnIdentity.Master, nextStage, stages?.Count ?? 0));
                    Widgets.Label(rect, label);
                    ITab_SexSlaveTraining.AddMasterProgressionTip(rect, growth.Record);
                }));
                if (growth.Record != null)
                    AddOverviewProgressScale(column, growth.Record, growth.ExperienceValue, nextStage,
                        nextTip: growth.Progress, trackTip: "SSC_MasterProgression_PracticeTip".Translate());
                return column;
            }
        }

        /// <summary>进度和刻度使用同一个矩形，按实际数值比例填充。</summary>
        private static void DrawStageBar(Rect track, float fraction, Color accent)
        {
            Widgets.DrawBoxSolid(track, ProgressTrack);
            if (fraction > 0f)
                Widgets.DrawBoxSolid(new Rect(track.x, track.y, track.width * fraction, track.height), accent);
        }

        /// <summary>头像和概况以同一身份与阶段入口取值。边框画在头像内，避免覆盖相邻把手。</summary>
        private static string DrawPortraitStage(Rect rect, Pawn pawn)
        {
            var identity = SSCIdentityUtility.GetIdentity(pawn);
            if (identity == PawnIdentity.Unset || rect.width < 16f || rect.height < 16f) return null;
            var chain = identity == PawnIdentity.Slave ? SSCBondUtility.GetChain(pawn) : null;
            Hediff record = identity == PawnIdentity.Master ? SSCMasterProgressionUtility.GetRecord(pawn) : chain;

            int total = record?.def.stages?.Count ?? 0;
            // 主人成长索引从零开始，锁链返回已达到的阶段数。两者统一为已达到的阶段数再绘制。
            int reached = total == 0 || !float.IsFinite(record.Severity) ? 0 : identity == PawnIdentity.Master
                ? Mathf.Clamp(record.CurStageIndex + 1, 0, total)
                : Mathf.Clamp(SSCIdentityUtility.GetSexSlaveStageFromChain(chain), 0, total);
            Color accent = StageAccent(identity, reached, total);
            DrawStateOutline(rect, accent);
            if (total > 0 && reached == total) DrawStateOutline(rect.ContractedBy(2f), accent);

            if (total > 0)
            {
                // 缩略头像也在框内留出完整的一组短线。最高阶段的内框在短线之外。
                float step = Mathf.Min(11f, (rect.width - 8f) / total);
                float x = rect.center.x - step * total / 2f;
                for (int i = 0; i < total; i++)
                    Widgets.DrawBoxSolid(new Rect(x + step * i + step * .15f, rect.yMax - 6f, step * .7f, 2f),
                        i < reached ? accent : DividerColor);
            }

            return reached > 0 ? record.def.stages[reached - 1]?.label : null;
        }

        /// <summary>一像素描边，未定型印记用虚线表达状态。</summary>
        private static void DrawStateOutline(Rect rect, Color accent, bool dashed = false)
        {
            if (rect.width < 2f || rect.height < 2f) return;
            if (!dashed)
            {
                var previous = GUI.color;
                try
                {
                    GUI.color = accent;
                    Widgets.DrawBox(rect);
                }
                finally { GUI.color = previous; }
                return;
            }

            const float step = 7f;
            const float dash = 4f;
            for (float x = 0; x < rect.width; x += step)
            {
                float width = Mathf.Min(dash, rect.width - x);
                Widgets.DrawBoxSolid(new Rect(rect.x + x, rect.y, width, 1f), accent);
                Widgets.DrawBoxSolid(new Rect(rect.x + x, rect.yMax - 1f, width, 1f), accent);
            }
            for (float y = 1f; y < rect.height - 1f; y += step)
            {
                float height = Mathf.Min(dash, rect.height - 1f - y);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + y, 1f, height), accent);
                Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y + y, 1f, height), accent);
            }
        }

        /// <summary>主人经验与锁链进度共用比例刻度，突出当前阶段并保留下一级门槛。</summary>
        private static void AddOverviewProgressScale(SettingsColumn column, Hediff record, float value, int nextStage,
            bool asPercent = false, string nextTip = null, string trackTip = null)
        {
            float maximum = asPercent ? 1f : record.def.maxSeverity;
            if (!(maximum > 0f) || float.IsInfinity(maximum)) return;

            const float barHeight = 14f;
            const float tickExtension = 5f;
            const float labelOffset = tickExtension + 3f;
            const float labelGap = 4f;
            const GameFont stageFont = GameFont.Medium;
            float width = Mathf.Max(1f, column.Width - column.Indent);
            float fill = Mathf.Clamp01(value / maximum);
            var labels = new List<(float Position, Rect Label, string Text, string Tip, bool Current, bool Next, bool Above)>();
            var below = new List<float>();
            var above = new List<float>();
            using var scope = new SSCBondOverviewWidgets.GuiScope(false);
            Text.Font = stageFont;
            Text.WordWrap = false;
            float lineHeight = Text.LineHeight;

            // 第一处刻度朝下，后续交替朝上和朝下。长译名只在同侧追加一行。
            var stages = record.def.stages;
            // 已达到的阶段数同时是下一阶段的零基索引；最高阶段仍按实际值填充。
            Color accent = StageAccent(asPercent ? PawnIdentity.Slave : PawnIdentity.Master, nextStage, stages?.Count ?? 0);
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null || !(stage.minSeverity >= 0f && stage.minSeverity <= maximum)) continue;
                float position = stage.minSeverity / maximum;
                string name = stage.label ?? string.Empty;
                bool isNext = i == nextStage;
                string threshold = stage.minSeverity.ToString(asPercent ? "0%" : "0.##");
                string tip = name + " · " + threshold;
                if (isNext)
                {
                    name = tip;
                    string remaining = asPercent ? "SSC_TrainingManagement_NextPercentGap".Translate(
                        (Mathf.Max(0f, stage.minSeverity - value) * 100f).ToString("0.##")).ToString() : nextTip;
                    if (!string.IsNullOrEmpty(remaining)) tip += "\n" + remaining;
                }
                float textWidth = Mathf.Min(width, Mathf.Ceil(Text.CalcSize(name).x) + 2f);
                float maxX = width - textWidth;
                float x = Mathf.Clamp(position * width - textWidth / 2f, 0f, maxX);
                bool isAbove = labels.Count % 2 != 0;
                var laneEnds = isAbove ? above : below;
                int lane = 0;
                while (lane < laneEnds.Count && x < laneEnds[lane] + labelGap) lane++;
                if (lane == laneEnds.Count) laneEnds.Add(x + textWidth);
                else laneEnds[lane] = x + textWidth;
                labels.Add((position, new Rect(x, labelOffset + lane * lineHeight, textWidth, lineHeight),
                    name.Truncate(textWidth), tip, i == nextStage - 1, isNext, isAbove));
            }

            // 两侧名称都计入同一行的高度，面板底色和滚动区也随之增高。
            float aboveHeight = above.Count == 0 ? 0f : labelOffset + above.Count * lineHeight;
            float belowHeight = below.Count == 0 ? 0f : labelOffset + below.Count * lineHeight;
            column.Rows.Add((aboveHeight + barHeight + belowHeight, rect =>
            {
                rect = InsetSettingsRow(rect, column.Indent);
                var track = new Rect(rect.x, rect.y + aboveHeight, rect.width, barHeight);
                DrawStageBar(track, fill, accent);
                using var drawScope = new SSCBondOverviewWidgets.GuiScope(false);
                Text.Font = stageFont;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;
                bool hoveringTick = false;
                foreach (var tick in labels)
                {
                    float tickWidth = Mathf.Min(2f, track.width);
                    float tickX = Mathf.Clamp(track.x + tick.Position * track.width - tickWidth / 2f,
                        track.x, track.xMax - tickWidth);
                    float markY = tick.Above ? track.y - tickExtension : track.y;
                    var mark = new Rect(tickX, markY, tickWidth, track.height + tickExtension);
                    // 用深色衬边保持刻度可辨，当前刻度用亮字色跨过同色填充条。
                    Widgets.DrawBoxSolid(new Rect(mark.x - 1f, mark.y, mark.width + 2f, mark.height), ProgressTrack);
                    Widgets.DrawBoxSolid(mark, tick.Current ? PrimaryText : tick.Next ? ProgressNext : SecondaryText);
                    float labelY = tick.Above ? track.y - tick.Label.y - tick.Label.height : track.yMax + tick.Label.y;
                    var label = new Rect(rect.x + tick.Label.x, labelY, tick.Label.width, tick.Label.height);
                    using (new TextTint(tick.Current ? accent : tick.Next ? ProgressNext : SecondaryText))
                        Widgets.Label(label, tick.Text);
                    var tickHit = new Rect(mark.x - 4f, mark.y, mark.width + 8f, mark.height);
                    hoveringTick |= Mouse.IsOver(tickHit);
                    TooltipHandler.TipRegion(tickHit, tick.Tip);
                    TooltipHandler.TipRegion(label, tick.Tip);
                }
                if (!hoveringTick && !string.IsNullOrEmpty(trackTip)) TooltipHandler.TipRegion(track, trackTip);
            }));
        }

        /// <summary>只允许原页面、原角色和原人格的操作进入业务入口。</summary>
        private bool PageEditIsCurrent(PageEdit edit)
            // revision 区分切页后回到原页；原生选中列表复查和人格凭据覆盖角色及载体替换。
            => edit.Revision == contextRevision && currentPage == edit.OriginPage &&
                ReferenceEquals(edit.Pawn, source) && ReferenceEquals(edit.Comp, comp) && SourceIsCurrent() &&
                edit.Comp.pawnIdentity == edit.Identity && SSCPersonalityRuntimeUtility.IsCurrent(edit.Pawn, edit.Personality) &&
                !SSCPersonalityRuntimeUtility.IsMutating(edit.Pawn);

        /// <summary>保留按下时的页面和操作，释放时只消费同一份请求。</summary>
        private Action PreparePageAction(PageEdit edit, Rect rect, object key, Action submit)
        {
            var input = Event.current;
            if (input.button != 0 || !Mouse.IsOver(rect)) return null;
            if (input.type == EventType.mouseDown && PageEditIsCurrent(edit))
            {
                pageActionPress = (key, () =>
                {
                    if (PageEditIsCurrent(edit)) submit();
                });
            }

            var request = pageActionPress.Submit;
            if ((input.rawType != EventType.mouseDown && input.rawType != EventType.mouseUp) ||
                request == null || !Equals(pageActionPress.Key, key)) return null;
            return () =>
            {
                // 原版控件消费事件前，切角或切页可能已经撤销这次按压。
                if (!ReferenceEquals(pageActionPress.Submit, request)) return;
                pageActionPress = default;
                request();
            };
        }

        /// <summary>编辑弹窗提交前复查原页面及其窗口仍在栈中。</summary>
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

        /// <summary>在原生选择写入后立即撤销弹窗及页面按压，逐帧刷新负责更新展示。</summary>
        internal static void NotifyEditSelection(Selector selector)
        {
            if (!GameReady() || !ReferenceEquals(selector, Find.Selector)) return;
            var window = Find.WindowStack.WindowOfType<Dialog_SSCTrainingManagement>();
            if (window != null && (window.editPopup != null || window.abilityTogglePress.HasValue || window.pageActionPress.Submit != null) &&
                !window.SourceIsCurrent()) window.InvalidateWindowContext();
        }
    }
}
