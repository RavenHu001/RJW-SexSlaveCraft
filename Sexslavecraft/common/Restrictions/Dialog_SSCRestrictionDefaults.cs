using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>独立编辑全局默认模板，避免六项个人式控件在 Mod 设置主页与全局开关混淆。</summary>
    internal sealed class Dialog_SSCRestrictionDefaults : Window
    {
        private Vector2 scrollPosition;
        private float contentHeight = 620f;

        /// <summary>给六项模板提供适中宽度，并将关闭按钮及滚动内容限制在可见屏幕内。</summary>
        public override Vector2 InitialSize => new Vector2(Mathf.Min(560f, UI.screenWidth - 32f),
            Mathf.Min(680f, UI.screenHeight - 32f));

        /// <summary>打开只建立窗口状态；模板只在控件回调中写入，不影响已有角色选择。</summary>
        public Dialog_SSCRestrictionDefaults()
        {
            optionalTitle = "SSC_Restrictions_DefaultsTitle".Translate();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        /// <summary>绘制独立滚动模板及批量应用入口，为底部关闭按钮留位并始终恢复全局 GUI 状态。</summary>
        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            bool oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Rect viewport = new Rect(inRect.x, inRect.y, inRect.width, Mathf.Max(1f, inRect.height - 48f));
                Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 20f),
                    Mathf.Max(viewport.height, contentHeight));
                Widgets.BeginScrollView(viewport, ref scrollPosition, content);
                try
                {
                    var listing = new Listing_Standard { maxOneColumn = true };
                    listing.Begin(content);
                    try { SSCRestrictionUI.DrawDefaults(listing); }
                    finally { contentHeight = listing.CurHeight + 12f; listing.End(); }
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
                GUI.enabled = oldEnabled;
                GUI.color = oldColor;
            }
        }
    }
}
