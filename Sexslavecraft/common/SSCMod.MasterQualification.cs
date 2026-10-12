using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class SSCMod
    {
        /// <summary>在设置调试区绘制单个道具生成按钮，不受调试 Gizmo 开关控制。</summary>
        private static void DrawMasterQualificationDebugEntry(Listing_Standard listing)
        {
            // 每次绘制取当前游戏与地图，以统一放置服务决定按钮是否可用。
            Map map = Current.Game != null ? Find.CurrentMap : null;
            bool available = SSCMasterQualificationDebugUtility.CanSpawn(map);
            // 保留外层 GUI 禁用状态：本按钮不能重新启用父窗口，退出绘制后必须还原。
            bool oldEnabled = GUI.enabled;
            bool clicked;
            try
            {
                GUI.enabled = oldEnabled && available;
                Rect button = listing.GetRect(30f);
                clicked = Widgets.ButtonText(button, "SSC_MasterQualification_DebugSpawn".Translate()) && GUI.enabled;
                // 禁用时说明所缺条件；可用时说明生成位置，避免被理解为直接赋予资格。
                TooltipHandler.TipRegion(button, (available
                    ? "SSC_MasterQualification_DebugSpawnTip" : "SSC_MasterQualification_DebugUnavailable").Translate());
            }
            finally { GUI.enabled = oldEnabled; }
            if (!clicked) return;

            // 点击才产生物品；服务重新验证并返回实际放置目标，成功与失败分别提示。
            if (SSCMasterQualificationDebugUtility.TrySpawn(map, Find.Selector?.SingleSelectedThing, out Thing spawned))
                Messages.Message("SSC_MasterQualification_DebugSpawned".Translate(), spawned, MessageTypeDefOf.PositiveEvent);
            else
                Messages.Message("SSC_MasterQualification_DebugSpawnFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
