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
            SSCMasterEntryGameComponent entry = Current.Game?.GetComponent<SSCMasterEntryGameComponent>();
            AcceptanceReport report = entry != null ? entry.CanRequestManually()
                : (AcceptanceReport)"SSC_MasterEntry_Unavailable".Translate();
            bool oldEnabled = GUI.enabled;
            bool clicked;
            try
            {
                GUI.enabled = oldEnabled && report.Accepted;
                Rect button = listing.GetRect(30f);
                clicked = Widgets.ButtonText(button, "SSC_MasterEntry_DebugRequest".Translate()) && GUI.enabled;
                TooltipHandler.TipRegion(button, report.Accepted
                    ? "SSC_MasterEntry_DebugRequestTip".Translate().ToString() : report.Reason);
            }
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
