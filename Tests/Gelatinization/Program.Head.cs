// AI 辅助生成。链接生产材质补丁，FA/引擎只提供身份、帧纹理和材质属性边界。
using System;
using System.IO;
using System.Xml.Linq;
using FacialAnimation;
using HarmonyLib;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static void HeadTests(string root)
    {
        Run("头部注册唯一的最终材质入口", HeadRegistration);
        Run("没有FA仍可渲染原版凝胶头部", HeadWithoutFa);
        Run("FA形状不匹配时保留FA且不阻断原版", HeadBadFaShape);
        Run("原版头皮与身体共用深紫和0.85", () => SkinMaterial(new PawnRenderNode_Head()));
        Run("FA头部Basic采用同色且保留原队列", () => SkinMaterial(Fa(new HeadControllerComp())));
        Run("FA头部Cover保留腮红帧覆盖率", () => SkinMaterial(Fa(new HeadControllerComp(), NLFacialAnimationLayerType.Cover), .3f));
        Run("FA皮肤Basic保留面部贴图与细节", () => SkinMaterial(Fa(new SkinControllerComp())));
        Run("FA派生种族头部控制器同样识别", () => SkinMaterial(Fa(new RaceHeadControllerComp())));
        Run("FA眼睑皮肤Cover使用遮罩材质", () => SkinMaterial(Fa(new LidControllerComp(), NLFacialAnimationLayerType.Cover), 1, true));
        // 精确列出五官类别，不能用“非Head全染色”或“颜色接近肤色”替代这份白名单。
        foreach (var part in new (string name, object controller, NLFacialAnimationLayerType layer)[]
        {
            ("眼白瞳孔", new EyeballControllerComp(), NLFacialAnimationLayerType.Basic),
            ("左眼", new EyeballControllerComp(), NLFacialAnimationLayerType.L_Basic),
            ("右眼", new EyeballControllerComp(), NLFacialAnimationLayerType.R_Basic),
            ("眼睛高光", new EyeballControllerComp(), NLFacialAnimationLayerType.Cover),
            ("左眼附加层", new EyeballControllerComp(), NLFacialAnimationLayerType.L_Option),
            ("右眼附加层", new EyeballControllerComp(), NLFacialAnimationLayerType.R_Option),
            ("睫毛", new LidControllerComp(), NLFacialAnimationLayerType.Basic),
            ("眼部底层", new LidControllerComp(), NLFacialAnimationLayerType.Bottom),
            ("眉毛", new BrowControllerComp(), NLFacialAnimationLayerType.Basic),
            ("嘴型", new MouthControllerComp(), NLFacialAnimationLayerType.Basic),
            ("嘴部覆盖", new MouthControllerComp(), NLFacialAnimationLayerType.Cover),
            ("表情符号", new EmotionControllerComp(), NLFacialAnimationLayerType.Basic),
            ("眼部装饰", new LidOptionControllerComp(), NLFacialAnimationLayerType.Basic),
            ("原头部高光", new HeadControllerComp(), NLFacialAnimationLayerType.Highlight),
            ("未知皮肤层", new SkinControllerComp(), NLFacialAnimationLayerType.Cover),
            ("缺失控制器", null, NLFacialAnimationLayerType.Basic)
        }) Run("保留FA" + part.name, () => KeepHeadMaterial(Fa(part.controller, part.layer)));
        Run("保留头发", () => KeepHeadMaterial(new PawnRenderNode_Hair()));
        Run("保留胡须", () => KeepHeadMaterial(new PawnRenderNode_Beard()));
        Run("保留头饰", () => KeepHeadMaterial(new PawnRenderNode_Apparel()));
        Run("不处理其他普通节点", () => KeepHeadMaterial(new PawnRenderNode()));
        Run("FA空白帧保持dummy身份", HeadDummy);
        Run("眼睑缺失遮罩时不绘制整张头皮", () => MissingHeadMask(true));
        Run("声明遮罩但未绑定时不降级", () => MissingHeadMask(false));
        Run("主贴图缺失安全跳过", HeadMissingTexture);
        Run("普通角色不会被缓存凝胶材质污染", HeadNormalPawn);
        Run("关闭染色立即恢复原材质", HeadDisabled);
        Run("头部空角色安全跳过", () => HeadGuard(p => p.pawn = null));
        Run("头部空节点安全跳过", () => KeepHeadMaterial(null));
        Run("头部空材质安全跳过", HeadNullMaterial);
        Run("无头角色不生成头部材质", () => HeadGuard(p => p.pawn.health.hediffSet.HasHead = false));
        Run("雕像保留原色", () => HeadGuard(p => p.Statue = true));
        Run("腐烂头部保留原图", () => HeadGuard(p => p.rotDrawMode = RotDrawMode.Rotting));
        Run("干尸头骨保留原图", () => HeadGuard(p => p.rotDrawMode = RotDrawMode.Dessicated));
        Run("隐形标记保留原材质", () => HeadGuard(p => p.flags = PawnRenderFlags.Invisible));
        Run("FA清除标记后仍保护隐形角色", () => HeadGuard(p => p.pawn.Invisible = true));
        Run("原生头部Shader缺失保留原材质", HeadMissingShader);
        Run("遮罩Shader缺失不退回无遮罩版本", HeadMissingMaskedShader);
        Run("头部首次资源失败后可恢复", HeadShaderRecovery);
        Run("材质缓存复用且更新动画纹理和UV", HeadAnimation);
        Run("头部重复回调不重复乘透明度", HeadIdempotent);
        Run("头像和地图共用皮肤准入", HeadPortrait);
        Run("头部遮罩孔洞保持透明", HeadMaskHole);
        Run("头部半透明遮罩保留连续覆盖", HeadMaskCoverage);
        Run("完整遮罩与身体Alpha一致", HeadMaskOpaque);
        Run("头部遮罩定义与片元完整", () => HeadResources(root));
    }

    private static PawnRenderNode Fa(object controller, NLFacialAnimationLayerType layer = NLFacialAnimationLayerType.Basic)
        => new NLFacialAnimationPartNode { controller = controller, layerType = layer };

    private sealed class HeadRig
    {
        public PawnDrawParms parms = new PawnDrawParms { pawn = new Pawn { FullGel = true } };
        public Material source = new Material(new Shader("OriginalHead")) { mainTexture = new Texture("Face-open"), color = new Color(.9f, .7f, .5f, 1), renderQueue = 2000 };
        public Material Apply(PawnRenderNode node)
        {
            Material result = source;
            Harmony_FullGelatinizationHeadTint.MaterialPostfix(node, parms, ref result);
            return result;
        }
    }

    private static void SkinMaterial(PawnRenderNode node, float sourceAlpha = 1, bool masked = false)
    {
        var rig = new HeadRig(); rig.source.color = new Color(.9f, .7f, .5f, sourceAlpha);
        if (masked) { rig.source.HasMaskSlot = true; rig.source.SetTexture("_MaskTex", new Texture("LidMask")); }
        Material result = rig.Apply(node);
        Check(result != rig.source && result.shader == (masked ? ShaderDef.Masked : ShaderDef.Native), "皮肤必须取得合适的独立材质");
        Color color = result.color;
        Check(color.r == .46f && color.g == .20f && color.b == .68f && Math.Abs(color.a - .85f * sourceAlpha) < 1e-6f, "与身体共享配色，保留原层Alpha");
        Check(result.renderQueue == 2000 && result.mainTexture == rig.source.mainTexture, "保留原绘制队列与当前原图");
        Check(rig.source.shader.name == "OriginalHead" && rig.source.color.r == .9f && rig.source.color.a == sourceAlpha, "不得改写原共享材质");
        if (masked) Check(result.GetTexture("_MaskTex") == rig.source.GetTexture("_MaskTex"), "保留遮罩对象");
    }

    private static void KeepHeadMaterial(PawnRenderNode node)
    {
        var rig = new HeadRig();
        Check(rig.Apply(node) == rig.source, "非皮肤图层必须保持原材质身份");
    }

    private static void HeadRegistration()
    {
        var harmony = new Harmony(); Harmony_FullGelatinizationHeadTint.Register(harmony);
        Check(harmony.Patched.Count == 1 && harmony.Patched[0].DeclaringType == typeof(PawnRenderNodeWorker)
            && harmony.Patched[0].Name == "GetFinalizedMaterial", "只在原版/FA均经过的最终材质入口注册一次");
    }
    private static void HeadWithoutFa()
    {
        AccessTools.DisableFa = true; Harmony_FullGelatinizationHeadTint.InitializeFacialAnimation();
        SkinMaterial(new PawnRenderNode_Head()); KeepHeadMaterial(Fa(new HeadControllerComp()));
    }
    private static void HeadBadFaShape()
    {
        AccessTools.BadFaShape = true; Harmony_FullGelatinizationHeadTint.InitializeFacialAnimation();
        SkinMaterial(new PawnRenderNode_Head()); KeepHeadMaterial(Fa(new HeadControllerComp()));
    }
    private static void HeadDummy()
    {
        var rig = new HeadRig(); rig.source = GraphicPool.dummyGraphic.MatSingle;
        rig.source.mainTexture = new Texture("Dummy");
        Check(rig.Apply(Fa(new HeadControllerComp())) == rig.source, "缺失帧不能失去dummy身份并显示占位贴图");
    }
    private static void MissingHeadMask(bool eyelid)
    {
        var rig = new HeadRig(); rig.source.HasMaskSlot = !eyelid;
        Check(rig.Apply(eyelid ? Fa(new LidControllerComp(), NLFacialAnimationLayerType.Cover) : Fa(new SkinControllerComp())) == rig.source, "缺遮罩应安全保留原图");
    }
    private static void HeadMissingTexture() { var rig = new HeadRig(); rig.source.mainTexture = null; Check(rig.Apply(new PawnRenderNode_Head()) == rig.source, "缺图安全跳过"); }
    private static void HeadNormalPawn()
    {
        var rig = new HeadRig(); Check(rig.Apply(new PawnRenderNode_Head()) != rig.source, "先产生凝胶材质");
        rig.parms.pawn.FullGel = false; Check(rig.Apply(new PawnRenderNode_Head()) == rig.source, "相同源材质被普通角色使用时保持原样");
    }
    private static void HeadDisabled() { var rig = new HeadRig(); rig.Apply(new PawnRenderNode_Head()); SSCMod.settings.enableFullGelatinizedBodyTint = false; Check(rig.Apply(new PawnRenderNode_Head()) == rig.source, "关闭后不返回凝胶缓存"); }
    // 用可变引用容器修改值类型 PawnDrawParms，避免测试误改了结构副本。
    private sealed class HeadParams
    {
        public Pawn pawn; public bool Statue; public RotDrawMode rotDrawMode; public PawnRenderFlags flags;
    }
    private static void HeadGuard(Action<HeadParams> change)
    {
        var rig = new HeadRig(); var p = new HeadParams { pawn = rig.parms.pawn }; change(p);
        rig.parms = new PawnDrawParms { pawn = p.pawn, Statue = p.Statue, rotDrawMode = p.rotDrawMode, flags = p.flags };
        Check(rig.Apply(new PawnRenderNode_Head()) == rig.source, "保护分支必须保留原材质");
    }
    private static void HeadNullMaterial() { Material m = null; Harmony_FullGelatinizationHeadTint.MaterialPostfix(new PawnRenderNode_Head(), new HeadRig().parms, ref m); Check(m == null, "不可制造空图"); }
    private static void HeadMissingShader() { Waylith.Rendering.ShaderRegistry.Registered = false; KeepHeadMaterial(new PawnRenderNode_Head()); }
    private static void HeadMissingMaskedShader()
    {
        var rig = new HeadRig(); rig.source.HasMaskSlot = true; rig.source.SetTexture("_MaskTex", new Texture("Mask"));
        Waylith.Rendering.ShaderRegistry.MaskedRegistered = false;
        Check(rig.Apply(Fa(new LidControllerComp(), NLFacialAnimationLayerType.Cover)) == rig.source, "禁止回退为全脸覆盖");
    }
    private static void HeadShaderRecovery()
    {
        HeadMissingShader(); Waylith.Rendering.ShaderRegistry.Registered = true; SkinMaterial(new PawnRenderNode_Head());
    }
    private static void HeadAnimation()
    {
        var rig = new HeadRig(); rig.source.HasMaskSlot = true; var node = Fa(new LidControllerComp(), NLFacialAnimationLayerType.Cover);
        rig.source.SetTexture("_MaskTex", new Texture("OpenMask")); rig.source.SetFloat("UVScale", 1);
        Material first = rig.Apply(node); int created = Material.Created;
        rig.source.mainTexture = new Texture("Face-closed"); var closedMask = new Texture("ClosedMask"); rig.source.SetTexture("_MaskTex", closedMask);
        rig.source.SetFloat("UVScale", .7f); rig.source.renderQueue = 2900;
        Material second = rig.Apply(node);
        Check(ReferenceEquals(first, second) && Material.Created == created, "下一帧不新建材质");
        Check(second.mainTexture.name == "Face-closed" && second.GetTexture("_MaskTex") == closedMask
            && second.GetFloat("UVScale") == .7f && second.renderQueue == 2900, "复用对象但必须更新本帧纹理、遮罩、UV与队列");
        Check(rig.source.color.r == .9f && rig.source.shader.name == "OriginalHead", "动画原材质不被污染");
    }
    private static void HeadIdempotent()
    {
        var rig = new HeadRig(); var node = new PawnRenderNode_Head(); Material first = rig.Apply(node), second = first;
        Harmony_FullGelatinizationHeadTint.MaterialPostfix(node, rig.parms, ref second);
        Check(second == first && second.color.a == .85f, "同一结果不能再次乘0.85");
    }
    private static void HeadPortrait() { var rig = new HeadRig(); rig.parms.Portrait = true; Check(rig.Apply(Fa(new HeadControllerComp())) != rig.source, "头像不能漏掉皮肤染色"); }
    private static void HeadMaskHole() { Check(GelShaderMath.sscGelMaskedAlpha(1, 0, .85f, 1) == 0 && GelShaderMath.sscGelMaskedAlpha(0, 1, .85f, 1) == 0, "遮罩孔洞与透明背景不填色"); }
    private static void HeadMaskCoverage()
    {
        float prev = 0;
        for (int i = 1; i <= 256; i++) { float a = GelShaderMath.sscGelMaskedAlpha(1, i / 256f, .85f, 1); Check(a > prev && a <= .85f, "遮罩过渡必须连续"); prev = a; }
        Check(Math.Abs(GelShaderMath.sscGelMaskedAlpha(.5f, .5f, .85f, .5f) - .85f / 8) < 1e-6f, "遮罩/原图/顶点覆盖分别保留");
    }
    private static void HeadMaskOpaque() { Check(GelShaderMath.sscGelMaskedAlpha(1, 1, .85f, 1) == GelShaderMath.sscGelAlpha(1, .85f, 1), "完整遮罩不能重复乘不透明度"); }
    private static void HeadResources(string root)
    {
        string runtime = SscTestPaths.RuntimeRoot(root);
        var def = XDocument.Load(Path.Combine(runtime, "Defs/ShaderDefs/SSC_FullGelHeadMasked17.xml")).Root.Element("ShaderDef");
        Check((string)def.Element("defName") == Harmony_FullGelatinizationHeadTint.MaskedShaderDefName, "资源名称与实际选择器一致");
        string code = File.ReadAllText(Path.Combine(runtime, "Shaders", (string)def.Element("shaderProgram").Element("fragmentShaderFileName")));
        Check(code.Contains("uniform sampler2D _MaskTex") && code.Contains("sscGelMaskedAlpha(source.a, mask.a, tint.a, vColor.a)"), "必须使用实际遮罩采样和生产Alpha函数");
    }
}
