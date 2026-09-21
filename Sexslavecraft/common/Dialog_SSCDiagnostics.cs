using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal sealed class Dialog_SSCDiagnostics : Window
    {
        private string report;
        private Vector2 scrollPosition;
        private bool copied;

        public override Vector2 InitialSize => new Vector2(780f, 680f);

        public Dialog_SSCDiagnostics()
        {
            optionalTitle = "SSC_Diagnostics_Title".Translate();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            Refresh();
        }

        private void Refresh()
        {
            report = SSCDiagnostics.BuildReport();
            scrollPosition = Vector2.zero;
            copied = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float buttonWidth = (inRect.width - 12f) / 2f;
                if (Widgets.ButtonText(new Rect(0f, 0f, buttonWidth, 32f), "SSC_Diagnostics_Refresh".Translate()))
                    Refresh();
                if (Widgets.ButtonText(new Rect(buttonWidth + 12f, 0f, buttonWidth, 32f),
                    (copied ? "SSC_Diagnostics_Copied" : "SSC_Diagnostics_Copy").Translate()))
                {
                    GUIUtility.systemCopyBuffer = report;
                    copied = true;
                }
                // Reserve the standard close-button footer, even on a screen-clamped window.
                Rect viewport = new Rect(0f, 44f, inRect.width, Mathf.Max(1f, inRect.height - 108f));
                Widgets.LabelScrollable(viewport, report, ref scrollPosition, longLabel: true);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
            }
        }
    }
}
