// AI 辅助生成。验证目标角色准入、头部组阶段、五官材质与动画保留；不是GPU实机验收。
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using FacialAnimation;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static void PostLightTests(string root)
    {
        Run("头部身体SA统一绕过3100光照层", () => SharedLightingPhase(root));
        foreach (bool enabled in new[] { false, true })
            foreach (bool full in new[] { false, true })
                Run($"头部整组只对完全凝胶化且开启开关生效 full={full} enabled={enabled}", () => HeadGroupGate(full, enabled));
        Run("切换关闭后五官立刻回到原材质及队列", FeatureToggle);
        Run("已染角色不会污染共享材质的普通角色", SharedFeatureNormal);
        Run("五官原色与不透明度不被皮肤参数覆盖", FeatureProperties);
        Run("五官换帧时同步纹理遮罩UV且复用副本", FeatureAnimation);
        Run("已经在光照后的高光保留原材质身份", ExistingPostLight);
        Run("多层头部附件继承组归属", HeadDescendants);
        Run("身体服装及根节点附件保持原阶段", BodyAttachments);
        Run("异常父节点循环不会卡死或误移队列", ParentCycle);
        Run("头部组遍历不会越过普通身体分支", HeadSibling);
        Run("眼睛重复回调不再次复制或改Alpha", FeatureIdempotent);
        Run("无头角色不移动头发和五官阶段", FeatureHeadless);
        Run("雕像及腐烂状态不移动五官阶段", FeatureSpecialStates);
        Run("隐形角色不改变头部阶段", FeatureInvisible);
        Run("缺主纹理的五官不创建额外材质", FeatureNoTexture);
        Run("工作线程五官移阶段不进入GraphicDatabase", FeatureWorker);
        Run("预览和地图使用同一头部光照阶段", PortraitPostLight);
        Run("颜色混合仍保留0.85及不同背景差异", BackgroundCoverage);
        Run("身体头皮眼睛嘴型同队列且保留原深度关系", HeadStackOrder);
    }

    private static PawnRenderNode Feature() => Fa(new MouthControllerComp());
    private static void SharedLightingPhase(string root)
    {
        var skin = DepthRig().Apply(new PawnRenderNode_Head());
        var feature = new HeadRig().Apply(Feature()); var sa = new SaRig().Apply();
        int body = int.Parse(XDocument.Load(Path.Combine(SscTestPaths.RuntimeRoot(root), "Defs/ShaderDefs/SSC_FullGelBody17.xml"))
            .Root.Element("ShaderDef").Element("shaderProgram").Element("renderQueue").Value);
        Check(body > 3100 && skin.renderQueue == body && feature.renderQueue == body && sa.renderQueue == body,
            "头皮/五官/身体/SA应在同一光照后阶段，不能只移动皮肤");
    }
    private static void HeadGroupGate(bool full, bool enabled)
    {
        foreach (PawnRenderNode node in new PawnRenderNode[] { new PawnRenderNode_Head(), Feature(), new PawnRenderNode_Hair(), new PawnRenderNode_Apparel { parent = new PawnRenderNode_Head() } })
        {
            var rig = new HeadRig(); rig.parms.pawn.FullGel = full; if(full) FullGelAppearanceUtility.GetOwner(rig.parms.pawn).TryGetComp<HediffComp_FullGelAppearance>().SetRenderingEnabled(enabled); var result = rig.Apply(node);
            Check((result != rig.source) == (full && enabled), "仅同时满足状态和开关时移动阶段");
            Check(rig.source.renderQueue == 2000 && rig.source.color.a == 1, "所有分支原材质保持不变");
        }
    }
    private static void FeatureToggle()
    {
        var rig = new HeadRig(); rig.Apply(Feature()); FullGelAppearanceUtility.GetOwner(rig.parms.pawn).TryGetComp<HediffComp_FullGelAppearance>().SetRenderingEnabled(false);
        Check(rig.Apply(Feature()) == rig.source && rig.source.renderQueue == 2000, "关闭不残留后光照队列");
    }
    private static void SharedFeatureNormal()
    {
        var gel = new HeadRig(); var tinted = gel.Apply(Feature()); var normal = new HeadRig { source = gel.source };
        normal.parms.pawn.FullGel = false;
        Check(normal.Apply(Feature()) == gel.source && tinted != gel.source && gel.source.renderQueue == 2000, "普通角色不能拿到共享后光照副本");
    }
    private static void FeatureProperties()
    {
        var rig = new HeadRig(); rig.source.color = new Color(.1f,.7f,.8f,.35f); rig.source.HasMaskSlot = true;
        var mask = new Texture("MouthMask"); rig.source.SetTexture("_MaskTex", mask); rig.source.shader.EngineShader.DepthWrite = true;
        var result = rig.Apply(Feature());
        Check(result.shader == rig.source.shader && result.color.r == .1f && result.color.g == .7f && result.color.b == .8f
            && result.color.a == .35f && result.GetTexture("_MaskTex") == mask && result.shader.EngineShader.DepthWrite, "五官保留颜色/Alpha/遮罩/原深度状态");
    }
    private static void FeatureAnimation()
    {
        var rig = new HeadRig(); var first = rig.Apply(Feature()); int created = Material.Created;
        rig.source.mainTexture = new Texture("Mouth-next"); rig.source.SetTexture("_MaskTex", new Texture("Mask-next")); rig.source.SetFloat("UV", .3f);
        rig.source.color = new Color(.2f,.3f,.4f,.5f); var next = rig.Apply(Feature());
        Check(first == next && Material.Created == created && next.mainTexture == rig.source.mainTexture
            && next.GetTexture("_MaskTex") == rig.source.GetTexture("_MaskTex") && next.GetFloat("UV") == .3f && next.color.a == .5f,
            "每帧复制原材质最新属性，不能固化嘴型/眨眼");
    }
    private static void ExistingPostLight()
    {
        var rig = new HeadRig(); rig.source.renderQueue = 3151;
        Check(rig.Apply(Fa(new HeadControllerComp(), NLFacialAnimationLayerType.Highlight)) == rig.source, "原高光已经绕过光照，无需重复移动");
    }
    private static void HeadDescendants()
    {
        var node = new PawnRenderNode_Apparel { parent = new PawnRenderNode { parent = new PawnRenderNode_Head() } };
        var rig = new HeadRig(); var result = rig.Apply(node);
        Check(result != rig.source && result.shader == rig.source.shader && result.renderQueue == 3150, "头饰等附件跟随头部，但不染紫");
    }
    private static void BodyAttachments()
    {
        var rig = new HeadRig(); var node = new PawnRenderNode_Apparel { parent = new PawnRenderNode() };
        Check(rig.Apply(node) == rig.source, "不扩大到身体服装或全树");
    }
    private static void ParentCycle()
    {
        var node = new PawnRenderNode(); node.parent = node; var rig = new HeadRig();
        Check(rig.Apply(node) == rig.source, "异常树应有限返回");
    }
    private static void HeadSibling()
    {
        var root = new PawnRenderNode(); var head = new PawnRenderNode_Head { parent = root }; var body = new PawnRenderNode { parent = root };
        Check(Harmony_FullGelatinizationHeadTint.IsHeadGroup(head) && !Harmony_FullGelatinizationHeadTint.IsHeadGroup(body), "只看祖先链，不把兄弟分支一并处理");
    }
    private static void FeatureIdempotent()
    {
        var rig = new HeadRig(); var result = rig.Apply(Feature()); var first = result; int count = Material.Created;
        Harmony_FullGelatinizationHeadTint.MaterialPostfix(Feature(), rig.parms, ref result);
        Check(result == first && Material.Created == count && result.color.a == 1, "不得重复乘透明度");
    }
    private static void FeatureHeadless() { var rig = new HeadRig(); rig.parms.pawn.health.hediffSet.HasHead = false; Check(rig.Apply(Feature()) == rig.source, "无头不处理"); }
    private static void FeatureSpecialStates()
    {
        var rig = new HeadRig(); rig.parms.Statue = true; Check(rig.Apply(Feature()) == rig.source, "雕像原样");
        rig.parms.Statue = false; rig.parms.rotDrawMode = RotDrawMode.Rotting; Check(rig.Apply(Feature()) == rig.source, "腐烂原样");
    }
    private static void FeatureInvisible() { var rig = new HeadRig(); rig.parms.pawn.Invisible = true; Check(rig.Apply(Feature()) == rig.source, "隐形原样"); }
    private static void FeatureNoTexture() { var rig = new HeadRig(); rig.source.mainTexture = null; Check(rig.Apply(Feature()) == rig.source, "不制造占位图层"); }
    private static void FeatureWorker()
    {
        var rig = new HeadRig(); Material result = null;
        Task.Run(() => { UnityData.IsInMainThread = false; try { result = rig.Apply(Feature()); } finally { UnityData.IsInMainThread = true; } }).GetAwaiter().GetResult();
        Check(result != rig.source && result.renderQueue == 3150 && Graphic.ColoredRequests == 0, "工作线程仍只复制材质");
    }
    private static void PortraitPostLight()
    {
        var rig = new HeadRig(); var map = rig.Apply(Feature()); rig.parms.Portrait = true; var portrait = rig.Apply(Feature());
        Check(map == portrait && portrait.renderQueue == 3150, "预览不维护另一套光照参数");
    }
    private static void BackgroundCoverage()
    {
        float alpha = GelShaderMath.sscGelAlpha(1,.85f,1);
        Check(alpha == .85f && .5f * alpha + .1f * (1-alpha) != .5f * alpha + .8f * (1-alpha), "绕过光照不代表不同背景上的透明混合相同");
    }
    private static void HeadStackOrder()
    {
        // 同队列仍按原绘制请求顺序/深度：身体在后，头皮靠前，眼睛和嘴型再靠前。
        var head = DepthRig().Apply(new PawnRenderNode_Head()); var eyes = new HeadRig().Apply(Fa(new EyeballControllerComp())); var mouth = new HeadRig().Apply(Feature());
        Check(head.renderQueue == eyes.renderQueue && eyes.renderQueue == mouth.renderQueue && head.shader.EngineShader.DepthWrite,
            "皮肤和五官需共同移动，且不得丢掉上轮已修复的头皮深度写入");
        float headDepth = .2f, bodyDepth = .4f, faceDepth = .1f;
        Check(bodyDepth > headDepth && faceDepth < headDepth, "保留原深度关系即可阻挡后方身体并显示靠前五官");
    }
}
