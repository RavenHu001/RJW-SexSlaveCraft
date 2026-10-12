using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class SSCMod
    {
        /// <summary>独立的任务调试按钮；不受手动赋予和角色调试 Gizmo 设置控制。</summary>
        private static void DrawMasterEntryDebugEntry(Listing_Standard listing)
        {
            // 每次绘制从当前游戏取组件；主菜单没有组件时使用相同的本地化不可用原因。
            SSCMasterEntryGameComponent entry = Current.Game?.GetComponent<SSCMasterEntryGameComponent>();
            AcceptanceReport report = entry != null ? entry.CanRequestManually()
                : (AcceptanceReport)"SSC_MasterEntry_Unavailable".Translate();
            // 保存父窗口启用状态，按钮自身可用不能反向开启原本禁用的 GUI。
            bool oldEnabled = GUI.enabled;
            bool clicked;
            try
            {
                GUI.enabled = oldEnabled && report.Accepted;
                Rect button = listing.GetRect(30f);
                clicked = Widgets.ButtonText(button, "SSC_MasterEntry_DebugRequest".Translate()) && GUI.enabled;
                // 可用时解释手动任务规则；禁用时显示实际原因，不将领取任务误称为直接生成物品。
                TooltipHandler.TipRegion(button, report.Accepted
                    ? "SSC_MasterEntry_DebugRequestTip".Translate().ToString() : report.Reason);
            }
            // 异常或正常离开绘制块都恢复 GUI，未点击不产生任务、物品或资格副作用。
            finally { GUI.enabled = oldEnabled; }
            if (!clicked) return;
            // 点击时再次判定，防止绘制之后的任务状态变化产生并行任务。
            if (entry.TryRequestManually(out string reason))
                Messages.Message("SSC_MasterEntry_Created".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
            else
                Messages.Message(reason, MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
