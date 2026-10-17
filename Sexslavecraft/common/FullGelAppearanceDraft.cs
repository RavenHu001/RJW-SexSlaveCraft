// AI 辅助生成。弹窗草稿不持有渲染材质；取消/关闭不会写入身体配置。
using UnityEngine;

namespace SexSlaveCraft
{
    internal sealed class FullGelAppearanceDraft
    {
        internal Color Rgb;
        internal int OpacityPercent;
        internal bool ResetRequested;
        internal FullGelAppearanceDraft(FullGelAppearance value) { Load(value); }
        private void Load(FullGelAppearance value)
        {
            // 原版颜色控件会重建Alpha=1的Color，因此不透明度使用独立整数百分比。
            Rgb = new Color(value.Color.r, value.Color.g, value.Color.b, 1);
            OpacityPercent = (int)System.Math.Round(value.Color.a * 100f);
        }
        // 重置只改草稿；确认时才将ResetRequested转换为清除身体上的custom标记。
        internal void Reset() { Load(FullGelAppearanceUtility.Default); ResetRequested = true; }
        // 在恢复默认后继续编辑任一控件，改为保存这份自定义值。
        internal void Edited() { ResetRequested = false; }
        // 最后统一换回0..1，颜色构造器负责非法数值和范围的兜底。
        internal FullGelAppearance Value => new FullGelAppearance(Rgb, OpacityPercent / 100f);

        /// <summary>颜色字符串仅接受六位 RGB；Alpha 始终由独立百分比控件决定。</summary>
        internal bool SetHex(string text)
        {
            // 完整有效输入才替换颜色，部分输入和错误字符保留上一份有效草稿。
            if (text == null || text.Length != 7 || text[0] != '#' || !ColorUtility.TryParseHtmlString(text, out Color parsed)) return false;
            Rgb = new Color(parsed.r, parsed.g, parsed.b, 1); Edited(); return true;
        }
    }
}
