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
            public Hediff_BridleOfSexSlave Bridle;
            public Hediff_MasterProgression Record;
            public object PersonalityToken;
            public List<AbilityToggleRow> AbilityRows;
            public string Header, Stage, Experience, Progress, Benefits, PracticeTip;
            public string ShareLabel, ShareTip;
            public bool CanEditShare, ShareEnabled, ShowProgress;
            public float ProgressFraction;
            public float HeaderHeight, StageHeight, ExperienceHeight, ProgressHeight, BenefitsHeight, ShareHeight;
            public float SectionHeight;
        }

        /// <summary>只读现存记录，以原生阶段索引和 Def 阈值形成显示；无记录不推断零经验。</summary>
        private static MasterPanelView BuildMasterPanelView(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            // 成长与绑定分别查询：没有成长不影响原有伤害分担，查询也不创建任何 Hediff。
            Hediff_MasterProgression record = SSCMasterProgressionUtility.GetRecord(pawn);
            Hediff_BridleOfSexSlave bridle = SSCBondUtility.GetBridle(pawn);
            var view = new MasterPanelView
            {
                Pawn = pawn, Comp = comp, Bridle = bridle, Record = record,
                PersonalityToken = SSCPersonalityRuntimeUtility.Capture(pawn),
                Header = "SSC_MasterProgression_PanelHeader".Translate(),
                Stage = record == null ? "SSC_MasterProgression_NoRecord".Translate().ToString() : record.LabelInBrackets,
                ShareLabel = "SSC_MasterShareDamage_Label".Translate(),
                ShareEnabled = bridle?.shareDamageEnabled ?? false,
                CanEditShare = CanEditMasterDamageSharing(pawn, comp, bridle),
                PracticeTip = "SSC_MasterProgression_PracticeTip".Translate()
            };
            // 复用健康栏的原版数值说明与能力名称，不把共振或人物最终属性混入阶段加成。
            if (record != null)
            {
                float experience = SSCMasterProgressionUtility.NormalizeExperience(record.Severity);
                view.Experience = "SSC_MasterProgression_Experience".Translate(experience.ToString("0"), record.def.maxSeverity.ToString("0"));
                view.Benefits = (record.IsActive ? "SSC_MasterProgression_Benefits" : "SSC_MasterProgression_BenefitsInactive").Translate();
                var stages = record.def.stages;
                int index = record.CurStageIndex;
                if (stages != null && index >= 0 && index < stages.Count)
                {
                    view.ShowProgress = true;
                    if (index == stages.Count - 1)
                    {
                        view.ProgressFraction = 1f;
                        view.Progress = "SSC_MasterProgression_MaxStage".Translate();
                    }
                    else
                    {
                        // 进度条表示本阶段到下一阶段的区间；累计经验另外显示，避免混淆两种分母。
                        float start = stages[index].minSeverity;
                        float next = stages[index + 1].minSeverity;
                        view.ProgressFraction = next > start ? Mathf.Clamp01((experience - start) / (next - start)) : 0f;
                        // 阶段名称与剩余经验共用 Def，四语改名无需在面板再维护一份名称表。
                        view.Progress = "SSC_MasterProgression_NextStage".Translate(
                            stages[index + 1].label, Mathf.Max(0f, next - experience).ToString("0"));
                    }
                }
            }
            // 复选框的可编辑条件与原 Gizmo 一致；当前没有承伤候选并不阻止保存偏好。
            view.ShareTip = "SSC_MasterShareDamage_Desc".Translate(SSCMasterBondUtility.DamageShareRatio.ToStringPercent());
            if (bridle == null) view.ShareTip += "\n\n" + "SSC_MasterProgression_ShareNoBond".Translate();
            else if (!view.CanEditShare) view.ShareTip += "\n\n" + "SSC_MasterProgression_ShareReadOnly".Translate();

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
                view.ShareHeight = Mathf.Max(24f, Text.CalcHeight(view.ShareLabel, Mathf.Max(1f, innerWidth - 24f)));
                view.SectionHeight = SectionPadding * 2f + view.HeaderHeight + view.StageHeight + MasterControlGap + view.ShareHeight;
                if (record != null) view.SectionHeight += MasterRowGap + view.ExperienceHeight + MasterRowGap + view.BenefitsHeight;
                if (view.ShowProgress) view.SectionHeight += MasterRowGap + MasterBarHeight + MasterBarTextGap + view.ProgressHeight;
                // 能力行和成长/伤害分担共用本次测量快照，长翻译直接计入滚动高度。
                view.AbilityRows = BuildAbilityToggleRows(pawn, comp, SSCAbilityToggleGroup.Master, innerWidth);
                foreach (AbilityToggleRow row in view.AbilityRows) view.SectionHeight += AbilityToggleRowGap + row.Height;
            }
            finally { Text.Font = oldFont; Text.WordWrap = oldWrap; }
            return view;
        }

        /// <summary>配置开关不依赖当前伤害、地图候选、成长等级或永久资格；主人面板限定当前身份。</summary>
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
            // 原版健康悬停会查询战斗日志，只在悬停时生成，避免每次重绘都扫描。
            // 迟到的提示也核对原记录仍属于该角色，移除后不展示已经转走的人格加成。
            if (record != null) TooltipHandler.TipRegion(row,
                () => SSCMasterProgressionUtility.GetRecord(record.pawn) == record ? record.GetTooltip(record.pawn, false) : string.Empty,
                record.GetHashCode());
            y += height;
        }
    }
}
