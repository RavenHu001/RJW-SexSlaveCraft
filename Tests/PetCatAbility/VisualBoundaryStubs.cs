// 原版绘制边界仅记录调用，实际猫目标UI直接链接生产Verb与效果组件。
// 不模拟GPU、鼠标选择器、Effecter喷心算法、寿命或完整游戏特效调度。
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace UnityEngine
{
    public sealed class Texture2D
    {
        public readonly string Name;
        public Texture2D(string name) { Name = name; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public struct Color { }
}

namespace Verse
{
    public enum AltitudeLayer { MetaOverlays = 39 }
    public static class GenDraw
    {
        public static readonly List<(Vector3 position, AltitudeLayer layer)> Highlights = new();
        public static void DrawTargetHighlightWithLayer(Vector3 position, AltitudeLayer layer) => Highlights.Add((position, layer));
    }
    public static class GenUI
    {
        public static readonly List<Texture2D> Attachments = new();
        public static void DrawMouseAttachment(Texture2D texture) => Attachments.Add(texture);
    }
    public static class Widgets
    {
        public static readonly List<string> AttachedLabels = new();
        public static void MouseAttachedLabel(string text, float xOffset = 0, float yOffset = 0, Color? color = null) => AttachedLabels.Add(text);
    }
    public partial class Verb
    {
        public int BaseHighlightCalls, BaseGuiCalls;
        public virtual Texture2D UIIcon => null;
        public virtual void DrawHighlight(LocalTargetInfo target) { BaseHighlightCalls++; }
        public virtual void OnGUI(LocalTargetInfo target) { BaseGuiCalls++; }
    }
}

namespace RimWorld
{
    public static class TexCommand { public static readonly Texture2D CannotShoot = new("cannot-shoot"); }
    public partial class Ability
    {
        public readonly List<LocalTargetInfo> EffectPreviews = new();
        public void DrawEffectPreviews(LocalTargetInfo target) => EffectPreviews.Add(target);
    }
    public partial class CompAbilityEffect
    {
        public virtual string ExtraLabelMouseAttachment(LocalTargetInfo target) => null;
    }
    public partial class Verb_CastAbility
    {
        public override Texture2D UIIcon => ability?.def?.uiIcon;
        protected void DrawAttachmentExtraLabel(LocalTargetInfo target)
        {
            // 对应本机原版方法：逐个查询效果组件，只绘制第一条非空附加标签。
            foreach (var effect in ability.EffectComps)
            {
                string text = effect.ExtraLabelMouseAttachment(target);
                if (string.IsNullOrEmpty(text)) continue;
                Widgets.MouseAttachedLabel(text, 0, 0, null);
                return;
            }
        }
    }
}
