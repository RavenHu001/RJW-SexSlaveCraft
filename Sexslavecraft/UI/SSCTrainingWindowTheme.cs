// AI 辅助生成
using System;
using UnityEngine;

namespace SexSlaveCraft
{
    /// <summary>调教窗口及内嵌规则控件共用的墨紫暗铜金配色。</summary>
    internal static class SSCTrainingWindowTheme
    {
        internal static readonly Color WindowColor = Rgb(0x19151B);
        internal static readonly Color PanelColor = Rgb(0x251F29);
        internal static readonly Color SettingsPanelColor = Rgb(0x29232D);
        internal static readonly Color HeaderColor = Rgb(0x342B39);
        internal static readonly Color ControlColor = Rgb(0x211C25);
        internal static readonly Color SelectedColor = Rgb(0x493749);
        internal static readonly Color DividerColor = Rgb(0x514454);
        internal static readonly Color CopperColor = Rgb(0xB59A73);
        internal static readonly Color PrimaryText = Rgb(0xF2ECF0);
        internal static readonly Color SecondaryText = Rgb(0xC4B8C7);
        internal static readonly Color DisabledText = Rgb(0x9A8C9E);
        internal static readonly Color ProgressTrack = Rgb(0x17131A);
        internal static readonly Color ProgressFill = Rgb(0xAE81C7);
        internal static readonly Color ProgressNext = Rgb(0xE3C7F1);

        private static Color Rgb(int rgb) => new Color((rgb >> 16) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>只着色文字内容。原版肖像、勾叉和真实色块继续用各自的绘制颜色。</summary>
        internal readonly struct TextTint : IDisposable
        {
            private readonly Color previous;

            internal TextTint(Color color)
            {
                previous = GUI.contentColor;
                GUI.contentColor = color;
            }

            public void Dispose() => GUI.contentColor = previous;
        }
    }
}
