// AI 辅助生成
using UnityEngine;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        private const float MasterRowGap = 6f;
        private const float MasterControlGap = 12f;
        private const float MasterBarHeight = 18f;
        private const float MasterBarTextGap = 4f;

        /// <summary>本次绘制事件的文字、尺寸与原对象引用；不是成长或开关的第二份保存数据。</summary>
        private struct MasterPanelView
        {
            public Pawn Pawn;
            public CompSexSlaveTraining Comp;
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            // 旧分伤专用字段仅供冻结分支使用；1.7 的两项开关都通过 AbilityRows 持有快照与凭据。
            public Hediff_BridleOfSexSlave Bridle;
#endif
            public Hediff_MasterProgression Record;
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            public object PersonalityToken;
#endif
            public List<AbilityToggleRow> AbilityRows;
            public string Header, Stage, Experience, Progress, Benefits, PracticeTip;
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            public string ShareLabel, ShareTip;
            public bool CanEditShare, ShareEnabled;
#endif
            public bool ShowProgress;
            public float ProgressFraction;
            public float HeaderHeight, StageHeight, ExperienceHeight, ProgressHeight, BenefitsHeight;
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            public float ShareHeight;
#endif
            public float SectionHeight;
        }

        /// <summary>为两个入口读取同一份成长显示，概况可把数值并入阶段行。</summary>
        internal static (Hediff_MasterProgression Record, string Stage, string Experience, string Progress,
            string Benefits, float? ProgressFraction, bool AtMaxStage) ReadMasterProgression(Pawn pawn, bool compactExperience = false)
        {
            var record = SSCMasterProgressionUtility.GetRecord(pawn);
            if (record == null)
                return (null, "SSC_MasterProgression_NoRecord".Translate(), null, null, null, null, false);

            float experience = SSCMasterProgressionUtility.NormalizeExperience(record.Severity);
            string experienceText = compactExperience ? $"{experience:0} / {record.def.maxSeverity:0}" :
                "SSC_MasterProgression_Experience".Translate(experience.ToString("0"), record.def.maxSeverity.ToString("0")).ToString();
            string benefits = (record.IsActive ? "SSC_MasterProgression_Benefits" : "SSC_MasterProgression_BenefitsInactive").Translate();
            var stages = record.def.stages;
            int index = record.CurStageIndex;
            if (stages == null || index < 0 || index >= stages.Count)
                return (record, record.LabelInBrackets, experienceText, null, benefits, null, false);
            if (index == stages.Count - 1)
                return (record, record.LabelInBrackets, experienceText, "SSC_MasterProgression_MaxStage".Translate(), benefits, 1f, true);

            // 条形进度使用本阶段的区间，累计经验使用记录上限。
            float start = stages[index].minSeverity;
            float next = stages[index + 1].minSeverity;
            float fraction = next > start ? Mathf.Clamp01((experience - start) / (next - start)) : 0f;
            string progress = "SSC_MasterProgression_NextStage".Translate(
                stages[index + 1].label, Mathf.Max(0f, next - experience).ToString("0"));
            return (record, record.LabelInBrackets, experienceText, progress, benefits, fraction, false);
        }

        /// <summary>仅在悬停时读取原健康栏说明，并复查成长记录仍属于该角色。</summary>
        internal static void AddMasterProgressionTip(Rect row, Hediff_MasterProgression record)
        {
            if (record == null) return;
            TooltipHandler.TipRegion(row,
                () => SSCMasterProgressionUtility.GetRecord(record.pawn) == record ? record.GetTooltip(record.pawn, false) : string.Empty,
                record.GetHashCode());
        }

        /// <summary>为旧面板测量原成长和能力行；无记录时仍保留能力入口。</summary>
        private static MasterPanelView BuildMasterPanelView(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            var growth = ReadMasterProgression(pawn);
            var record = growth.Record;
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            Hediff_BridleOfSexSlave bridle = SSCBondUtility.GetBridle(pawn);
#endif
            var view = new MasterPanelView
            {
                Pawn = pawn, Comp = comp, Record = record,
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
                Bridle = bridle,
                PersonalityToken = SSCPersonalityRuntimeUtility.Capture(pawn),
#endif
                Header = "SSC_MasterProgression_PanelHeader".Translate(),
                Stage = growth.Stage, Experience = growth.Experience, Progress = growth.Progress, Benefits = growth.Benefits,
                ShowProgress = growth.ProgressFraction.HasValue, ProgressFraction = growth.ProgressFraction.GetValueOrDefault(),
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
                ShareLabel = "SSC_MasterShareDamage_Label".Translate(),
                ShareEnabled = bridle?.shareDamageEnabled ?? false,
                CanEditShare = CanEditMasterDamageSharing(pawn, comp, bridle),
#endif
                PracticeTip = "SSC_MasterProgression_PracticeTip".Translate()
            };
            // 复选框的可编辑条件与原 Gizmo 一致；当前没有承伤候选并不阻止保存偏好。
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
            view.ShareTip = "SSC_MasterShareDamage_Desc".Translate(SSCMasterBondUtility.DamageShareRatio.ToStringPercent());
            if (bridle == null) view.ShareTip += "\n\n" + "SSC_MasterProgression_ShareNoBond".Translate();
            else if (!view.CanEditShare) view.ShareTip += "\n\n" + "SSC_MasterProgression_ShareReadOnly".Translate();
#endif

            // 标题和正文按与绘制相同的字体/换行测量；长译文及窄窗口增加整体高度。
            GameFont oldFont = Text.Font;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                float innerWidth = Mathf.Max(1f, width - SectionPadding * 2f);
                view.HeaderHeight = Mathf.Max(SectionTitleHeight, Text.CalcHeight(view.Header, innerWidth));
                view.StageHeight = Text.CalcHeight(view.Stage, innerWidth);
                view.ExperienceHeight = record != null ? Text.CalcHeight(view.Experience, innerWidth) : 0f;
                view.ProgressHeight = view.ShowProgress ? Text.CalcHeight(view.Progress, innerWidth) : 0f;
                view.BenefitsHeight = record != null ? Text.CalcHeight(view.Benefits, innerWidth) : 0f;
                // 原版 CheckboxLabeled 在右侧保留 24 像素；测量必须扣除相同宽度。
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
                // 分伤按注册排序仍为第一项，共用同一测量／提交路径。
                view.SectionHeight = SectionPadding * 2f + view.HeaderHeight + view.StageHeight;
#else
                view.ShareHeight = Mathf.Max(24f, Text.CalcHeight(view.ShareLabel, Mathf.Max(1f, innerWidth - 24f)));
                view.SectionHeight = SectionPadding * 2f + view.HeaderHeight + view.StageHeight + MasterControlGap + view.ShareHeight;
#endif
                if (record != null) view.SectionHeight += MasterRowGap + view.ExperienceHeight + MasterRowGap + view.BenefitsHeight;
                if (view.ShowProgress) view.SectionHeight += MasterRowGap + MasterBarHeight + MasterBarTextGap + view.ProgressHeight;
                // 能力行和成长/伤害分担共用本次测量快照，长翻译直接计入滚动高度。
                // 1.7 不额外预留一条分伤高度，注册表中的分伤行也只在绘制器内出现一次。
                view.AbilityRows = BuildAbilityToggleRows(pawn, comp, SSCAbilityToggleGroup.Master, innerWidth);
                foreach (AbilityToggleRow row in view.AbilityRows) view.SectionHeight += AbilityToggleRowGap + row.Height;
            }
            finally { Text.Font = oldFont; Text.WordWrap = oldWrap; }
            return view;
        }

#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
        /// <summary>冻结版本原分伤入口；1.7 统一交由载体适配器处理。</summary>
        private static bool CanEditMasterDamageSharing(Pawn pawn, CompSexSlaveTraining comp, Hediff_BridleOfSexSlave bridle)
        {
            return pawn != null && Faction.OfPlayer != null && pawn.Faction == Faction.OfPlayer &&
                comp != null && pawn.TryGetComp<CompSexSlaveTraining>() == comp &&
                comp.pawnIdentity == PawnIdentity.Master && bridle != null && SSCBondUtility.GetBridle(pawn) == bridle;
        }

        /// <summary>迟到点击复查原角色与缰绳；只写原保存字段，不创建关系或修改成长。</summary>
        private bool TrySetMasterDamageSharing(MasterPanelView view, bool enabled)
        {
            // 旧伤害分担入口也要拒绝同组件换人格后的点击；不改变其原有缰绳保存字段。
            if (SelPawn != view.Pawn || !SSCPersonalityRuntimeUtility.IsCurrent(view.Pawn, view.PersonalityToken) ||
                SSCPersonalityRuntimeUtility.IsMutating(view.Pawn) ||
                !CanEditMasterDamageSharing(view.Pawn, view.Comp, view.Bridle)) return false;
            view.Bridle.shareDamageEnabled = enabled;
            return true;
        }
#endif

        /// <summary>按同一份测量绘制全部行，使用外层左栏滚动，并在异常路径恢复全局 GUI 状态。</summary>
        private void DrawMasterPanel(Rect rect, MasterPanelView view)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap, oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Widgets.DrawMenuSection(rect);
                Rect inner = rect.ContractedBy(SectionPadding);
                float y = inner.y;
                Widgets.Label(new Rect(inner.x, y, inner.width, view.HeaderHeight), view.Header);
                y += view.HeaderHeight;
                DrawMasterLabel(inner, ref y, view.StageHeight, view.Stage, null, view.Record);
                if (view.ExperienceHeight > 0f)
                {
                    y += MasterRowGap;
                    DrawMasterLabel(inner, ref y, view.ExperienceHeight, view.Experience, view.PracticeTip);
                }
                if (view.ShowProgress)
                {
                    y += MasterRowGap;
                    Widgets.FillableBar(new Rect(inner.x, y, inner.width, MasterBarHeight), view.ProgressFraction);
                    y += MasterBarHeight + MasterBarTextGap;
                    DrawMasterLabel(inner, ref y, view.ProgressHeight, view.Progress, view.PracticeTip);
                }
                if (view.BenefitsHeight > 0f)
                {
                    y += MasterRowGap;
                    DrawMasterLabel(inner, ref y, view.BenefitsHeight, view.Benefits, null, view.Record);
                }
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
                y += MasterControlGap;
                Rect shareRect = new Rect(inner.x, y, inner.width, view.ShareHeight);
                bool enabled = view.ShareEnabled;
                // 原版 CheckboxLabeled 自身通过 disabled 拦截点击，不仅依赖 GUI.enabled。
                // 关闭状态仍可重新开启；CanRedirectDamageFrom 不能用作这里的准入条件。
                GUI.enabled = oldEnabled && view.CanEditShare;
                Widgets.CheckboxLabeled(shareRect, view.ShareLabel, ref enabled, !GUI.enabled);
                TooltipHandler.TipRegion(shareRect, view.ShareTip);
                if (GUI.enabled && enabled != view.ShareEnabled) TrySetMasterDamageSharing(view, enabled);
                y += view.ShareHeight;
#endif
                // 公共行决定分伤／恩赐的排序、提示和提交；成长面板不再逐能力写保存字段。
                DrawAbilityToggleRows(inner, ref y, view.AbilityRows, oldEnabled);
            }
            finally
            {
                Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap;
                GUI.enabled = oldEnabled; GUI.color = oldColor;
            }
        }

        /// <summary>固定使用已测量的行高，不另建 Listing 列或计算第二份文本尺寸。</summary>
        private static void DrawMasterLabel(Rect inner, ref float y, float height, string text, string tooltip,
            Hediff_MasterProgression record = null)
        {
            Rect row = new Rect(inner.x, y, inner.width, height);
            Widgets.Label(row, text);
            if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(row, tooltip);
            AddMasterProgressionTip(row, record);
            y += height;
        }
    }
}
