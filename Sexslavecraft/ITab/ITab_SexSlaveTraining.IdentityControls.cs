// AI 辅助生成。身份控件使用显式禁用参数；任职提交继续经过权威资格校验。
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>只在本次界面事件内复用任职结果、翻译及测量值，不跨帧缓存角色资格。</summary>
        private struct TrainerIdentityView
        {
            public bool IsSlave;
            public bool Enabled;
            public bool Qualified;
            public string Label;
            public string Tooltip;
            public float ToggleHeight;
            public float SectionHeight;
            public string MasterQualificationLabel;
            public string MasterQualificationTooltip;
            public float MasterQualificationHeight;
        }

        /// <summary>显示个人保存选择与有效任职的区别；失格时仍允许关闭先前保存的开启选择。</summary>
        private static void DrawTrainerIdentityToggle(Listing_Standard listing, Pawn pawn, TrainerIdentityView view)
        {
            // 绘制和高度预留读取同一份本帧文字及保存选择；不在这里再次查询资格。
            // 失格后仍允许关闭已保存选择。原版Listing不提供disabled参数，改用Rect版控件。
            bool available = view.IsSlave && (view.Qualified || view.Enabled);
            bool next = SSCUIWidgets.Checkbox(listing.GetRect(view.ToggleHeight), view.Label,
                view.Enabled, available, view.Tooltip);
            listing.Gap(IdentityListingSpacing);
            if (next != view.Enabled) SSCIdentityUtility.SetTrainerEnabled(pawn, next);
        }

    }
}
