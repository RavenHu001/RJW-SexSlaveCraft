// AI 辅助生成：当前身体的泌乳与子宫开发管理，独立于 SSC 身份及训练研究。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        private const float BodyRowGap = 6f;
        private enum BodyControl { None, Milk, Fertility }

        /// <summary>一行的文字、控件状态与测量结果；不跨 GUI 事件缓存。</summary>
        private sealed class BodyManagementRow
        {
            internal string Label, Tip;
            internal BodyControl Control;
            internal bool Enabled, CanEdit;
            internal float Height;
        }

        /// <summary>同一次测量和绘制共享的身体引用；点击前必须再次确认引用仍然有效。</summary>
        private sealed class BodyManagementView
        {
            internal Pawn Pawn;
            internal CompSexSlaveTraining Comp;
            internal Hediff Milk;
            internal Hediff_SSCUterusDevelopment Uterus;
            internal string Title, ChangedTip;
            internal readonly List<BodyManagementRow> Rows = new List<BodyManagementRow>();
            internal float Height, TitleHeight;
        }

        /// <summary>只读取得当前身体条目；没有对应 Hediff 时不产生空栏或补建开发状态。</summary>
        private static BodyManagementView BuildBodyManagementView(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            var view = new BodyManagementView { Pawn = pawn, Comp = comp };
            if (pawn?.health?.hediffSet == null || comp == null) return view;
            view.Milk = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState);
            view.Uterus = SSCUterusDevelopmentUtility.Find(pawn);
            if (view.Milk == null && view.Uterus == null) return view;

            // 测量时和绘制时使用同样的字体、换行及复选框预留宽度。
            GameFont font = Text.Font;
            bool wrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                float innerWidth = Mathf.Max(1f, width - SectionPadding * 2f);
                // 标题及提示与行文案一起翻译并保存在本次视图，测量和绘制不会读取不同文本。
                view.Title = "SSC_BodyManagement_Title".Translate();
                view.ChangedTip = "SSC_BodyManagement_BodyChanged".Translate();
                view.TitleHeight = Mathf.Max(SectionTitleHeight, Text.CalcHeight(view.Title, innerWidth));
                if (view.Milk != null)
                {
                    // 泌乳操作沿用已有四语翻译键；只为新增说明建立占位键。
                    AddBodyRow(view, innerWidth, Strings.ITab_MilkProductionToggle, "SSC_BodyManagement_MilkTip".Translate(),
                        BodyControl.Milk, comp.milkProductionEnabled, true);
                    string progress = view.Milk.TryGetComp<HediffComp_PermanentLactating>()?.CompLabelInBracketsExtra;
                    if (!string.IsNullOrEmpty(progress)) AddBodyRow(view, innerWidth, progress);
                }
                if (view.Uterus != null)
                {
                    // 保留两位小数，并限制未完成的显示上限；兼顾浮点误差和接近满级时的可读性。
                    double progress = view.Uterus.IsComplete ? 100d : Math.Min(99.99d, Math.Round(view.Uterus.Severity * 100d, 2));
                    string percent = progress.ToString("0.##") + "%";
                    // 完整句式交给语言资源，进度、阶段和倍率均通过参数填入。
                    AddBodyRow(view, innerWidth, "SSC_BodyManagement_Progress".Translate(percent, view.Uterus.CurStage.label));
                    AddBodyRow(view, innerWidth, view.Uterus.IsComplete
                        ? (view.Uterus.FertilityEnabled ? "SSC_BodyManagement_FertilityOn" : "SSC_BodyManagement_FertilityOff").Translate()
                        : "SSC_BodyManagement_FertilityIncomplete".Translate(), "SSC_BodyManagement_FertilityScope".Translate());
                    string benefits = BodyBenefits(view.Uterus.CurStage);
                    string tip = "SSC_BodyManagement_CurrentTip".Translate(benefits, BodyFertilityFactor(view.Uterus.CurStage));
                    if (view.Uterus.IsComplete)
                    {
                        var fertile = view.Uterus.def.GetModExtension<SSCUterusDevelopmentExtension>()?.fertileStage;
                        var closed = view.Uterus.def.stages.LastOrDefault();
                        tip += "\n\n" + "SSC_BodyManagement_TwoStateTip".Translate(BodyBenefits(closed), BodyFertilityFactor(closed),
                            BodyBenefits(fertile), BodyFertilityFactor(fertile), SSCUterusPregnancyUtility.DurationFactor.ToString("0.##"));
                    }
                    AddBodyRow(view, innerWidth, "SSC_BodyManagement_CurrentBenefits".Translate(BodyBenefits(view.Uterus.CurStage, true)), tip);
                    bool enabled = view.Uterus.FertilityEnabled;
                    bool available = SSCUterusDevelopmentUtility.CanSetFertilityEnabled(pawn, !enabled, out string reason);
                    AddBodyRow(view, innerWidth, "SSC_BodyManagement_FertilityToggle".Translate(), reason ?? "SSC_BodyManagement_FertilityTip".Translate(),
                        BodyControl.Fertility, enabled, available);
                }
                view.Height = SectionPadding * 2f + view.TitleHeight + view.Rows.Sum(row => row.Height)
                    + BodyRowGap * Math.Max(0, view.Rows.Count - 1);
            }
            finally { Text.Font = font; Text.WordWrap = wrap; }
            return view;
        }

        /// <summary>将一行的实际文字高度计入视图，复选框始终保留右侧控件宽度。</summary>
        private static void AddBodyRow(BodyManagementView view, float width, string label, string tip = null,
            BodyControl control = BodyControl.None, bool enabled = false, bool canEdit = false)
        {
            view.Rows.Add(new BodyManagementRow
            {
                Label = label, Tip = tip, Control = control, Enabled = enabled, CanEdit = canEdit,
                Height = Mathf.Max(control == BodyControl.None ? Text.LineHeight : 24f,
                    Text.CalcHeight(label, Mathf.Max(1f, width - (control == BodyControl.None ? 0f : 24f))))
            });
        }

        /// <summary>读取阶段真实倍率及 StatDef 已本地化名称；句式、分隔符和无收益提示来自语言资源。</summary>
        private static string BodyBenefits(HediffStage stage, bool compact = false)
        {
            if (stage == null) return "SSC_BodyManagement_ConfigUnavailable".Translate();
            string[] names = { "SexFrequency", "SexSatisfaction", "RestRateMultiplier", "InjuryHealingFactor", "ImmunityGainSpeed" };
            var values = new List<string>();
            for (int i = 0; i < (compact ? 2 : names.Length); i++)
            {
                var modifier = stage.statFactors?.FirstOrDefault(item => item.stat?.defName == names[i]);
                float factor = modifier?.value ?? 1f;
                // 属性名复用原版／RJW 的 Def 本地化，不再在界面维护第二组中文名称。
                if (factor != 1f) values.Add("SSC_BodyManagement_StatFactor".Translate(modifier.stat.LabelCap, factor.ToString("0.##")));
            }
            return values.Count == 0 ? "SSC_BodyManagement_NoBenefits".Translate() : string.Join("SSC_BodyManagement_BenefitsSeparator".Translate(), values);
        }

        /// <summary>显示本项目阶段的生育因子，不将其误写成角色最终受孕概率。</summary>
        private static string BodyFertilityFactor(HediffStage stage)
            => stage == null ? "SSC_BodyManagement_ConfigUnavailable".Translate() : stage.fertilityFactor == 0f ? "0"
                : "SSC_BodyManagement_Multiplier".Translate(stage.fertilityFactor.ToString("0.##"));

        /// <summary>绘制一次共享栏并归还全部 GUI 状态；栏内所有项目沿外层滚动布局排列。</summary>
        private void DrawBodyManagementSection(Rect rect, BodyManagementView view)
        {
            if (view.Height <= 0f) return;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            bool wrap = Text.WordWrap, enabled = GUI.enabled;
            Color color = GUI.color;
            try
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Widgets.DrawMenuSection(rect);
                Rect inner = rect.ContractedBy(SectionPadding);
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, view.TitleHeight), view.Title);
                float y = inner.y + view.TitleHeight;
                foreach (var row in view.Rows)
                {
                    Rect line = new Rect(inner.x, y, inner.width, row.Height);
                    if (row.Control == BodyControl.None)
                    {
                        Widgets.Label(line, row.Label);
                        if (!string.IsNullOrEmpty(row.Tip)) TooltipHandler.TipRegion(line, row.Tip);
                    }
                    else
                    {
                        bool current = IsBodyViewCurrent(view, row.Control);
                        bool next = SSCUIWidgets.Checkbox(line, row.Label, row.Enabled, row.CanEdit && current,
                            current ? row.Tip : view.ChangedTip);
                        // 禁用、原身体离体或切换选中角色后均不写回快照值。
                        if (next != row.Enabled && enabled && row.CanEdit && IsBodyViewCurrent(view, row.Control))
                        {
                            if (row.Control == BodyControl.Milk) view.Comp.milkProductionEnabled = next;
                            else if (!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(view.Pawn, next, out string reason))
                                Messages.Message(reason, view.Pawn, MessageTypeDefOf.RejectInput, false);
                        }
                    }
                    y += row.Height + BodyRowGap;
                }
            }
            finally
            {
                Text.Font = font; Text.Anchor = anchor; Text.WordWrap = wrap;
                GUI.enabled = enabled; GUI.color = color;
            }
        }

        /// <summary>只允许本次选中身体及原组件／Hediff 接受点击，不重新进行身份或绑定准入。</summary>
        private bool IsBodyViewCurrent(BodyManagementView view, BodyControl control)
        {
            if (SelPawn != view.Pawn || view.Pawn?.GetComp<CompSexSlaveTraining>() != view.Comp) return false;
            if (control == BodyControl.Fertility) return SSCUterusDevelopmentUtility.Find(view.Pawn) == view.Uterus;
            return view.Milk != null && view.Pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState) == view.Milk;
        }
    }
}
