// AI 辅助生成：身体枚举专用行，不注册互斥 bool、不保存 UI 状态。
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>档位即时本地化；界面与健康名称使用同一有效档位，不缓存旧语言。</summary>
    internal static class SSCOrgasmSensitivityPresentation
    {
        /// <summary>稳定档位的显示名；关闭与四档都由 Keyed 资源提供。</summary>
        internal static string Level(int level) => ("SSC_Sensitivity_Level" + level).Translate();

    }

    public partial class ITab_SexSlaveTraining
    {
        /// <summary>一次 GUI 视图的文字度量与原凭据；只供本次绘制及其菜单闭包使用，不保存。</summary>
        private sealed class OrgasmSensitivityRow
        {
            internal Pawn Pawn;
            internal SSCOrgasmSensitivityContext Context;
            internal string Button;
            internal float ButtonWidth;
        }

        /// <summary>即使没有任何身体 bool 行也独立加入；读取与布局不会创建 Hediff。</summary>
        private static void AppendOrgasmSensitivityRows(BodyManagementView view, float width)
        {
            var context = SSCOrgasmSensitivityUtility.Capture(view.Pawn);
            // 已失去性奴身份的身体仍显示原记录及关闭入口，不能因实际效果暂停而隐藏保存选择。
            if (!SSCIdentityUtility.IsSexSlave(view.Pawn) && context.Records.Length == 0) return;
            var state = SSCOrgasmSensitivityUtility.GetState(view.Pawn);
            string label = "SSC_Sensitivity_Title".Translate();
            string button = SSCOrgasmSensitivityPresentation.Level(state.SavedLevel);
            float buttonWidth = Mathf.Clamp(width * .5f, 100f, 150f);
            float labelHeight = Text.CalcHeight(label, Mathf.Max(1f, width - buttonWidth - BodyRowGap));
            float buttonHeight = Text.CalcHeight(button, Mathf.Max(1f, buttonWidth - 16f));
            view.Rows.Add(new BodyManagementRow
            {
                Label = label,
                GapBefore = view.Rows.Count == 0 ? 0f : BodyGroupGap,
                Height = Mathf.Max(24f, Mathf.Max(labelHeight, buttonHeight)),
                Sensitivity = new OrgasmSensitivityRow
                {
                    Pawn = view.Pawn, Context = context, ButtonWidth = buttonWidth, Button = button
                }
            });
            // 只显示实际档位；保存选择留在按钮上，降阶仍不覆写原选择。此行不注册整行悬浮说明。
            AddBodyRow(view, width, "SSC_Sensitivity_EffectiveLevel".Translate(
                SSCOrgasmSensitivityPresentation.Level(state.EffectiveLevel)), indent: BodyChildIndent);
        }

        /// <summary>名称与当前保存档位按钮同行，不在ITab显示健康说明；菜单仍使用原上下文。</summary>
        private void DrawOrgasmSensitivityRow(Rect line, BodyManagementRow bodyRow)
        {
            var row = bodyRow.Sensitivity;
            bool enabled = GUI.enabled;
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(line.x, line.y, Mathf.Max(1f, line.width - row.ButtonWidth - BodyRowGap), line.height), bodyRow.Label);
                GUI.enabled = enabled && SelPawn == row.Pawn && SSCOrgasmSensitivityUtility.IsCurrent(row.Context) &&
                    SSCOrgasmSensitivityUtility.CanSet(row.Context, 0, out _);
                // “可关闭”只决定按钮能否打开菜单，各非零选项的最新准入在菜单生成和提交时分别校验。
                if (Widgets.ButtonText(new Rect(line.xMax - row.ButtonWidth, line.y, row.ButtonWidth, line.height), row.Button))
                    Find.WindowStack.Add(new FloatMenu(BuildOrgasmSensitivityOptions(row)));
            }
            finally { GUI.enabled = enabled; Text.Anchor = anchor; }
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
                    options.Add(new FloatMenuOption(label, null) { tooltip = reason });
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
