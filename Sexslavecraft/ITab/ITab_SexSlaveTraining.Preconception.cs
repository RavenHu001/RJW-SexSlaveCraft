// AI 辅助生成：身体孕前枚举设置，只捕获原上下文，不创建身体或妊娠记录。
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>终极人格缺少身体载体时只提示前置，不创建子宫或伪造可保存的控件。</summary>
        private static void AppendMissingFertilityBodyRow(BodyManagementView view, float width)
        {
            if (!FertilitySpecializationUtility.HasFinalState(view.Pawn) || SSCUterusDevelopmentUtility.Find(view.Pawn) != null) return;
            AddBodyRow(view, width, "SSC_Preconception_MissingUterus".Translate(),
                "SSC_Fertility_Uterus".Translate(), gapBefore: view.Rows.Count == 0 ? 0f : BodyGroupGap);
        }
        /// <summary>本次视图的保存选择与原提交凭据；浮动菜单继续使用同一上下文。</summary>
        private sealed class PreconceptionRow
        {
            internal Pawn Pawn;
            internal SSCFertilityPreconceptionContext Context;
            internal bool Species;
            internal string Button;
            internal float ButtonWidth;
        }

        /// <summary>有效子宫记录始终展示保存值；未解锁和失格只禁用编辑，不隐藏或补建预设。</summary>
        private static void AppendPreconceptionRows(BodyManagementView view, float width)
        {
            var context = SSCFertilityPreconceptionUtility.Capture(view.Pawn);
            var record = SSCUterusDevelopmentUtility.Find(view.Pawn);
            if (record == null) return;
            AddPreconceptionRow(view, width, context, false, (int)record.OffspringGender);
            AddPreconceptionRow(view, width, context, true, (int)record.OffspringSpeciesSource);
        }

        /// <summary>名称和按钮分别按实际宽度测量，两项预设与后代印记一起缩进归入子宫区域。</summary>
        private static void AddPreconceptionRow(BodyManagementView view, float width,
            SSCFertilityPreconceptionContext context, bool species, int selected)
        {
            float rowWidth = Mathf.Max(1f, width - BodyChildIndent);
            float buttonWidth = Mathf.Min(rowWidth * .55f, 150f);
            string label = (species ? "SSC_Preconception_SpeciesLabel" : "SSC_Preconception_GenderLabel").Translate();
            string button = PreconceptionOptionLabel(species, selected);
            view.Rows.Add(new BodyManagementRow
            {
                Label = label,
                Tip = (species ? "SSC_Preconception_SpeciesTip" : "SSC_Preconception_GenderTip").Translate(),
                Indent = BodyChildIndent,
                Height = Mathf.Max(24f, Mathf.Max(
                    Text.CalcHeight(label, Mathf.Max(1f, rowWidth - buttonWidth - BodyRowGap)),
                    Text.CalcHeight(button, Mathf.Max(1f, buttonWidth - 16f)))),
                Preconception = new PreconceptionRow
                {
                    Pawn = view.Pawn, Context = context, Species = species,
                    Button = button, ButtonWidth = buttonWidth
                }
            });
        }

        /// <summary>三个稳定枚举值即时翻译；自然选项不假装已经获得特定性别或物种。</summary>
        private static string PreconceptionOptionLabel(bool species, int selected)
        {
            string suffix = selected == 1 ? species ? "SpeciesSelf" : "GenderMale"
                : selected == 2 ? species ? "SpeciesPartner" : "GenderFemale"
                : species ? "SpeciesNatural" : "GenderNatural";
            return ("SSC_Preconception_" + suffix).Translate();
        }

        /// <summary>绘制当前保存值；实时复查原上下文和编辑门槛，禁用原因只作为本次说明。</summary>
        private void DrawPreconceptionRow(Rect line, BodyManagementRow bodyRow)
        {
            var row = bodyRow.Preconception;
            bool enabled = GUI.enabled;
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(line.x, line.y, Mathf.Max(1f, line.width - row.ButtonWidth - BodyRowGap), line.height), bodyRow.Label);
                string reason = null;
                bool current = SelPawn == row.Pawn && SSCFertilityPreconceptionUtility.IsCurrent(row.Context);
                bool canChange = current && CanSetPreconceptionOption(row, 0, out reason);
                if (!current) reason = "SSC_BodyManagement_BodyChanged".Translate();
                // 共用按钮同时处理外层禁用、active和置灰；即使底层误报点击，也不为禁用项创建菜单。
                // 打开菜单与每一选项提交仍查同一原凭据；孕中锁定不改写保存选择。
                if (SSCUIWidgets.Button(new Rect(line.xMax - row.ButtonWidth, line.y, row.ButtonWidth, line.height), row.Button, canChange))
                    Find.WindowStack.Add(new FloatMenu(BuildPreconceptionOptions(row)));
                TooltipHandler.TipRegion(line, string.IsNullOrEmpty(reason) ? bodyRow.Tip : bodyRow.Tip + "\n\n" + reason);
            }
            finally { GUI.enabled = enabled; Text.Anchor = anchor; }
        }

        /// <summary>菜单形成和点击分别校验最新资格，点击时不重新捕获人格或子宫实例。</summary>
        private List<FloatMenuOption> BuildPreconceptionOptions(PreconceptionRow row)
        {
            var options = new List<FloatMenuOption>();
            for (int value = 0; value <= 2; value++)
            {
                int selected = value;
                string label = PreconceptionOptionLabel(row.Species, selected);
                if (!CanSetPreconceptionOption(row, selected, out string reason))
                    options.Add(new FloatMenuOption(label, null) { tooltip = reason });
                else options.Add(new FloatMenuOption(label, () =>
                {
                    if (SelPawn != row.Pawn)
                        Messages.Message("SSC_BodyManagement_BodyChanged".Translate(), MessageTypeDefOf.RejectInput, false);
                    else if (!TrySetPreconceptionOption(row, selected, out string rejection))
                        Messages.Message(rejection, row.Pawn, MessageTypeDefOf.RejectInput, false);
                }));
            }
            return options;
        }

        /// <summary>仅分派本行枚举类型，全部阶段、孕态与身体归属判断仍由权威工具执行。</summary>
        private static bool CanSetPreconceptionOption(PreconceptionRow row, int selected, out string reason)
            => row.Species
                ? SSCFertilityPreconceptionUtility.CanSetSpeciesSource(row.Context, (SSCOffspringSpeciesSource)selected, out reason)
                : SSCFertilityPreconceptionUtility.CanSetGender(row.Context, (SSCOffspringGender)selected, out reason);

        /// <summary>向对应枚举的唯一写入口提交原请求，不直接写 Hediff 字段或原生育开关。</summary>
        private static bool TrySetPreconceptionOption(PreconceptionRow row, int selected, out string reason)
            => row.Species
                ? SSCFertilityPreconceptionUtility.TrySetSpeciesSource(row.Context, (SSCOffspringSpeciesSource)selected, out reason)
                : SSCFertilityPreconceptionUtility.TrySetGender(row.Context, (SSCOffspringGender)selected, out reason);
    }
}
