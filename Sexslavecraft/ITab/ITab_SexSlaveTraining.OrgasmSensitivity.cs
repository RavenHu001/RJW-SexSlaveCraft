// AI 辅助生成：身体枚举专用行，不注册互斥 bool、不保存 UI 状态。
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>ITab 与健康说明共用的即时本地化；切换语言不保留旧文案缓存。</summary>
    internal static class SSCOrgasmSensitivityPresentation
    {
        /// <summary>稳定档位的显示名；关闭与四档都由 Keyed 资源提供。</summary>
        internal static string Level(int level) => ("SSC_Sensitivity_Level" + level).Translate();

        /// <summary>显示保存／实际值和配置参数，暂停原因沿统一有效查询返回。</summary>
        internal static string Describe(Pawn pawn)
        {
            var state = SSCOrgasmSensitivityUtility.GetState(pawn);
            string text = "SSC_Sensitivity_Summary".Translate(Level(state.SavedLevel), Level(state.EffectiveLevel),
                state.Multiplier.ToString("0.###"), state.VulnerabilityOffset.ToString("+0.##;-0.##;0"));
            if (state.InactiveReason != null) text += "\n" + state.InactiveReason.Translate();
            else if (state.SavedLevel > state.EffectiveLevel) text += "\n" + "SSC_Sensitivity_LevelLocked".Translate();
            return text;
        }
    }

    public partial class ITab_SexSlaveTraining
    {
        /// <summary>一次 GUI 视图的文字度量与原凭据；只供本次绘制及其菜单闭包使用，不保存。</summary>
        private sealed class OrgasmSensitivityRow
        {
            internal Pawn Pawn;
            internal SSCOrgasmSensitivityContext Context;
            internal string Button;
            internal float LabelHeight;
        }

        /// <summary>即使没有任何身体 bool 行也独立加入；读取与布局不会创建 Hediff。</summary>
        private static void AppendOrgasmSensitivityRows(BodyManagementView view, float width)
        {
            var context = SSCOrgasmSensitivityUtility.Capture(view.Pawn);
            // 已失去性奴身份的身体仍显示原记录及关闭入口，不能因实际效果暂停而隐藏保存选择。
            if (!SSCIdentityUtility.IsSexSlave(view.Pawn) && context.Records.Length == 0) return;
            var state = SSCOrgasmSensitivityUtility.GetState(view.Pawn);
            string label = "SSC_Sensitivity_Title".Translate() + "\n" + SSCOrgasmSensitivityPresentation.Describe(view.Pawn);
            if (!SSCOrgasmSensitivityUtility.CanSet(context, 0, out string editReason) && !label.Contains(editReason))
                label += "\n" + editReason;
            float labelHeight = Text.CalcHeight(label, Mathf.Max(1f, width));
            view.Rows.Add(new BodyManagementRow
            {
                Label = label, Tip = "SSC_Sensitivity_Tip".Translate(),
                GapBefore = view.Rows.Count == 0 ? 0f : BodyGroupGap,
                Height = labelHeight + BodyRowGap + 30f,
                Sensitivity = new OrgasmSensitivityRow
                {
                    Pawn = view.Pawn, Context = context, LabelHeight = labelHeight,
                    Button = "SSC_Sensitivity_Adjust".Translate(SSCOrgasmSensitivityPresentation.Level(state.SavedLevel))
                }
            });
        }

        /// <summary>同一行按测量的换行高度绘制；菜单闭包保留原凭据和原选中角色。</summary>
        private void DrawOrgasmSensitivityRow(Rect line, BodyManagementRow bodyRow)
        {
            var row = bodyRow.Sensitivity;
            Widgets.Label(new Rect(line.x, line.y, line.width, row.LabelHeight), bodyRow.Label);
            TooltipHandler.TipRegion(line, bodyRow.Tip);
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && SelPawn == row.Pawn && SSCOrgasmSensitivityUtility.IsCurrent(row.Context) &&
                    SSCOrgasmSensitivityUtility.CanSet(row.Context, 0, out _);
                // “可关闭”只决定按钮能否打开菜单，各非零选项的最新准入在菜单生成和提交时分别校验。
                if (Widgets.ButtonText(new Rect(line.x, line.y + row.LabelHeight + BodyRowGap, line.width, 30f), row.Button))
                    Find.WindowStack.Add(new FloatMenu(BuildOrgasmSensitivityOptions(row)));
            }
            finally { GUI.enabled = enabled; }
        }

        /// <summary>逐项呈现锁定原因；提交仍重查原上下文和最新阶段，点击时不重新捕获授权。</summary>
        private List<FloatMenuOption> BuildOrgasmSensitivityOptions(OrgasmSensitivityRow row)
        {
            var options = new List<FloatMenuOption>();
            for (int level = 0; level <= 4; level++)
            {
                int selected = level;
                string label = SSCOrgasmSensitivityPresentation.Level(level);
                var config = SSCOrgasmSensitivityConfig.Get(SSCOrgasmSensitivityUtility.Definition);
                if (level > 0 && config?.Valid == true)
                    label += "SSC_Sensitivity_OptionParameters".Translate(config.Multipliers[level - 1].ToString("0.###"),
                        config.Offsets[level - 1].ToString("+0.##;-0.##;0"));
                if (!SSCOrgasmSensitivityUtility.CanSet(row.Context, selected, out string reason))
                    // 原生 null action 表示禁用，不给锁定选项保留一个会重捕获权限的回调。
                    options.Add(new FloatMenuOption(label + " — " + reason, null));
                else options.Add(new FloatMenuOption(label, () =>
                {
                    // 选中项也是原请求的一部分；不能用另一个角色的当前页提交旧菜单。
                    if (SelPawn != row.Pawn)
                        Messages.Message("SSC_BodyManagement_BodyChanged".Translate(), MessageTypeDefOf.RejectInput, false);
                    else if (!SSCOrgasmSensitivityUtility.TrySet(row.Context, selected, out string rejection))
                        Messages.Message(rejection, row.Pawn, MessageTypeDefOf.RejectInput, false);
                }));
            }
            return options;
        }
    }
}
