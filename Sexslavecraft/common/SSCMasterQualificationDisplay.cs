using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>永久主人资格的只读文案和绘制；调用方传入人格记录，不使用身份准入结果。</summary>
    public static class SSCMasterQualificationDisplay
    {
        public static string GetLabel(bool unlocked)
        {
            return (unlocked ? "SSC_MasterQualification_StatusUnlocked"
                : "SSC_MasterQualification_StatusLocked").Translate();
        }

        /// <summary>设置只影响选择资格，单独解释其状态，不将设置放行解释为永久解锁。</summary>
        public static string GetPawnTooltip(bool allowDirectSelection)
        {
            return "SSC_MasterQualification_PawnStatusTip".Translate() + "\n\n" +
                (allowDirectSelection ? "SSC_MasterQualification_DirectSelectionOnTip"
                    : "SSC_MasterQualification_DirectSelectionOffTip").Translate();
        }

        public static string GetGelTooltip()
        {
            return "SSC_MasterQualification_GelStatusTip".Translate();
        }

        /// <summary>由调用方统一 Small 字体，测量与绘制使用同一宽度，长译文完整换行。</summary>
        public static float GetHeight(string label, float width, float minimumHeight)
        {
            return Mathf.Max(minimumHeight, Text.CalcHeight(label, Mathf.Max(1f, width)));
        }

        public static void Draw(Rect rect, string label, string tooltip)
        {
            // 长译文必须完整换行；绘制后恢复状态，避免影响角色栏的其他控件。
            bool previousWordWrap = Text.WordWrap;
            try
            {
                Text.WordWrap = true;
                Widgets.Label(rect, label);
                TooltipHandler.TipRegion(rect, tooltip);
            }
            finally { Text.WordWrap = previousWordWrap; }
        }
    }
}
