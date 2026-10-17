// AI 辅助生成。组合原版控件，显式表达只读状态并恢复调用者的GUI状态。
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCUIWidgets
    {
        /// <summary>GUI.enabled只负责部分控件输入；按钮背景与文字另外显式置灰。</summary>
        internal static bool Button(Rect rect, string label, bool available)
        {
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            try
            {
                bool enabled = previousEnabled && available;
                GUI.enabled = enabled;
                if (!enabled) GUI.color = Dim(previousColor);
                return Widgets.ButtonText(rect, label, active: enabled) && enabled;
            }
            finally { GUI.enabled = previousEnabled; GUI.color = previousColor; }
        }

        /// <summary>必须传disabled，阻止原版拖动复选框绕过GUI.enabled，并使勾选纹理显示禁用色。</summary>
        internal static bool Checkbox(Rect rect, string label, bool value, bool available, string tooltip = null)
        {
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            bool enabled = previousEnabled && available;
            bool next = value;
            try
            {
                GUI.enabled = enabled;
                if (!enabled) GUI.color = Dim(previousColor);
                Widgets.CheckboxLabeled(rect, label, ref next, disabled: !enabled);
            }
            finally { GUI.enabled = previousEnabled; GUI.color = previousColor; }
            // 禁用时仍提供原因提示；不依赖调用者用错误的视觉状态代替权限校验。
            if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(rect, tooltip);
            return enabled ? next : value;
        }

        private static Color Dim(Color color) => new Color(color.r * .55f, color.g * .55f, color.b * .55f, color.a);

        /// <summary>以小棋盘展示保存RGBA；只绘制UI色块，不创建贴图或角色渲染缓存。</summary>
        internal static void Swatch(Rect rect, Color value)
        {
            const float cell = 8f;
            for (int y = 0; y * cell < rect.height; y++)
                for (int x = 0; x * cell < rect.width; x++)
                {
                    float shade = (x + y) % 2 == 0 ? .24f : .44f;
                    var tile = new Rect(rect.x + x * cell, rect.y + y * cell,
                        Mathf.Min(cell, rect.width - x * cell), Mathf.Min(cell, rect.height - y * cell));
                    Widgets.DrawBoxSolid(tile, new Color(shade, shade, shade, 1));
                }
            Widgets.DrawBoxSolid(rect, value);
        }
    }
}
