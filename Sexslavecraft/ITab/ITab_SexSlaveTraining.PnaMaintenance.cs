// AI 辅助生成：锁链枚举专用行；测量与绘制共用快照，菜单提交保留原owner。
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>本次GUI事件的布局与原请求快照；不保存界面选择，也不承担任务调度。</summary>
        private sealed class PnaMaintenanceView
        {
            internal SSCPnaMaintenanceContext Context;
            internal string Label, Button;
            internal float Height, RowHeight, ButtonWidth;
        }

        /// <summary>仅为有效锁链性奴生成菜单行，按当前译文测量高度并保留原提交凭据。</summary>
        private static PnaMaintenanceView BuildPnaMaintenanceView(Pawn pawn, float width)
        {
            if (!SSCIdentityUtility.IsSexSlave(pawn) || SSCPnaMaintenanceUtility.FindChain(pawn) == null) return null;
            var context = SSCPnaMaintenanceUtility.Capture(pawn);
            string label = "SSC_PnaMaintenance_Title".Translate();
            string button = SSCPnaMaintenanceUtility.StageLabel(context.Stage);
            // 长译文同时参与行高计算；总滚动高度与实际按钮绘制复用这份测量结果。
            float buttonWidth = Mathf.Clamp((width - SectionPadding * 2f) * .45f, 90f, 130f);
            float height = Mathf.Max(28f, Mathf.Max(Text.CalcHeight(label, Mathf.Max(1f, width - SectionPadding * 2f - buttonWidth - 8f)),
                Text.CalcHeight(button, buttonWidth - 12f)));
            return new PnaMaintenanceView { Context = context, Label = label, Button = button, ButtonWidth = buttonWidth,
                RowHeight = height, Height = height + SectionPadding * 2f };
        }

        /// <summary>绘制只读快照并打开菜单；临时GUI状态始终恢复，过期角色或链不可点击。</summary>
        private void DrawPnaMaintenanceSection(Rect rect, PnaMaintenanceView view)
        {
            Widgets.DrawMenuSection(rect);
            Rect row = rect.ContractedBy(SectionPadding);
            Rect button = new Rect(row.xMax - view.ButtonWidth, row.y, view.ButtonWidth, view.RowHeight);
            TextAnchor anchor = Text.Anchor;
            bool enabled = GUI.enabled;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(row.x, row.y, row.width - view.ButtonWidth - 8f, view.RowHeight), view.Label);
                // 关闭项的准入只控制菜单能否打开；每个目标档位仍由菜单生成和最终提交分别复查。
                GUI.enabled = enabled && SelPawn == view.Context.Pawn &&
                    SSCPnaMaintenanceUtility.CanSet(view.Context, SSCPnaMaintenanceStage.Off, out _);
                if (Widgets.ButtonText(button, view.Button)) Find.WindowStack.Add(new FloatMenu(BuildPnaMaintenanceOptions(view)));
            }
            finally { Text.Anchor = anchor; GUI.enabled = enabled; }
            TooltipHandler.TipRegion(row, "SSC_PnaMaintenance_Tooltip".Translate());
        }

        /// <summary>生成关闭加五阶段选项；回调使用原快照，失格项保留原因但不提供提交动作。</summary>
        private List<FloatMenuOption> BuildPnaMaintenanceOptions(PnaMaintenanceView view)
        {
            var options = new List<FloatMenuOption>();
            for (int value = 0; value <= 5; value++)
            {
                var stage = (SSCPnaMaintenanceStage)value;
                string label = SSCPnaMaintenanceUtility.StageLabel(stage);
                if (!SSCPnaMaintenanceUtility.CanSet(view.Context, stage, out string reason))
                    options.Add(new FloatMenuOption(label, null) { tooltip = reason });
                else options.Add(new FloatMenuOption(label, () =>
                {
                    // 不在点击时重捕获新锁链或新人格的权限。
                    if (SelPawn != view.Context.Pawn)
                        Messages.Message("SSC_BodyManagement_BodyChanged".Translate(), MessageTypeDefOf.RejectInput, false);
                    else if (!SSCPnaMaintenanceUtility.TrySet(view.Context, stage, out string rejection))
                        Messages.Message(rejection, view.Context.Pawn, MessageTypeDefOf.RejectInput, false);
                }));
            }
            return options;
        }
    }
}
