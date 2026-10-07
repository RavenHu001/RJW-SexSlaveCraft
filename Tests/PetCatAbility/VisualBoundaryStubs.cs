// 按本机原版 DLL 核验的 Touch 选取顺序分发；猫资格仍执行生产 Verb 与效果组件。
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
    public static class GenDraw
    {
        // 原版 DrawTargetHighlight 根据 Thing/Cell 选择绘制入口；这里只记录所选目标。
        // 不将旧版固定层绘制的坐标/高度假设套用于原版 Thing 高亮及 TargetHighlighter。
        public static readonly List<LocalTargetInfo> Highlights = new();
        public static void DrawTargetHighlight(LocalTargetInfo target) => Highlights.Add(target);
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
        // 原版基类默认不增加资格条件；真实宠物 Verb 必须让此入口遵守地图选取资格。
        public virtual bool IsApplicableTo(LocalTargetInfo target, bool throwMessages = false) => true;
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
    public class Verb_CastAbilityTouch : Verb_CastAbility
    {
        // 与本机原版 Touch IL 一致：高亮不检查 CanHitTarget 或距离，只检查有效性与适用性。
        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (!target.IsValid || !IsApplicableTo(target, false)) return;
            GenDraw.DrawTargetHighlight(target);
            ability.DrawEffectPreviews(target);
        }
        public override void OnGUI(LocalTargetInfo target)
        {
            GenUI.DrawMouseAttachment(ValidateTarget(target, false) ? UIIcon : TexCommand.CannotShoot);
            DrawAttachmentExtraLabel(target);
        }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!IsApplicableTo(target, showMessages)) return false;
            foreach (var effect in ability.EffectComps)
                if (!effect.Valid(target, showMessages)) return false;
            return true;
        }
    }
}
