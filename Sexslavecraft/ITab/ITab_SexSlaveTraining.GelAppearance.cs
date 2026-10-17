// AI 辅助生成。凝胶外观入口属于身体状态，不依赖训练研究、SSC身份或任职资格。
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public partial class ITab_SexSlaveTraining
    {
        /// <summary>绘制和滚动高度共享本次事件快照，不跨角色保留颜色或Hediff。</summary>
        private struct GelAppearanceView
        {
            internal Pawn Pawn;
            internal HediffWithComps Owner;
            internal FullGelAppearance Appearance;
            internal bool Enabled;
            internal string ToggleLabel, StateLabel;
            internal float ToggleHeight, StateHeight, Height;
        }

        private static GelAppearanceView BuildGelAppearanceView(Pawn pawn, float width)
        {
            var owner = FullGelAppearanceUtility.GetOwner(pawn);
            if (owner == null) return default(GelAppearanceView);
            using (TextBlock.Default())
            {
                float inner = Mathf.Max(1, width - SectionPadding * 2);
                var appearance = FullGelAppearanceUtility.Read(owner);
                bool enabled = FullGelAppearanceUtility.ShouldRender(pawn);
                string label = "SSC_GelAppearance_RenderEnabled".Translate();
                string state = (enabled ? "SSC_GelAppearance_Current" : "SSC_GelAppearance_Disabled").Translate();
                float toggleHeight = Mathf.Max(24, Text.CalcHeight(label, Mathf.Max(1, inner - 24)));
                float stateHeight = Text.CalcHeight(state, inner);
                return new GelAppearanceView
                {
                    Pawn = pawn, Owner = owner, Appearance = appearance, Enabled = enabled,
                    ToggleLabel = label, StateLabel = state, ToggleHeight = toggleHeight, StateHeight = stateHeight,
                    Height = SectionPadding * 2 + SectionTitleHeight + toggleHeight + stateHeight + 104
                };
            }
        }

        private static void DrawGelAppearanceSection(Rect rect, GelAppearanceView view)
        {
            DrawSection(rect, "SSC_GelAppearance_Title".Translate(), inner =>
            {
                // 外部模组可能在绘制期间移除/替换Hediff；提交仍由工具再次检查原身体身份。
                bool active = ReferenceEquals(FullGelAppearanceUtility.GetOwner(view.Pawn), view.Owner);
                bool next = SSCUIWidgets.Checkbox(new Rect(inner.x, inner.y, inner.width, view.ToggleHeight),
                    view.ToggleLabel, view.Enabled, active, "SSC_GelAppearance_RenderTip".Translate());
                if (next != view.Enabled)
                    FullGelAppearanceUtility.SetRenderingEnabled(view.Pawn, view.Owner, next);
                float y = inner.y + view.ToggleHeight + 6;
                Widgets.Label(new Rect(inner.x, y, inner.width, view.StateHeight), view.StateLabel);
                y += view.StateHeight + 6;
                // 关闭后仍展示保存值，0%也能通过棋盘背景找到入口。
                SSCUIWidgets.Swatch(new Rect(inner.x, y, inner.width, 26), view.Appearance.Color);
                y += 30;
                Widgets.Label(new Rect(inner.x, y, inner.width, 24),
                    "SSC_GelAppearance_Opacity".Translate() + " " + Mathf.RoundToInt(view.Appearance.Color.a * 100) + "%");
                y += 28;
                if (SSCUIWidgets.Button(new Rect(inner.x, y, inner.width, 30),
                    "SSC_GelAppearance_Edit".Translate(), active))
                    Find.WindowStack.Add(new Dialog_FullGelAppearance(view.Pawn, view.Owner));
            });
        }
    }
}
