// AI 辅助生成。链接生产缓存逻辑，测稳定调用的复制/分配，并回归每类源变化；不代表实机FPS。
using System;
using FacialAnimation;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static void PerfTests(string root)
    {
        Run("稳定FA皮肤一万次调用不复制不分配", () => WarmSkin(false));
        Run("稳定遮罩眼睑一万次调用不复制不分配", () => WarmSkin(true));
        Run("稳定五官一万次调用不复制不分配", WarmFeature);
        Run("稳定SA一万次调用不复制不分配", WarmSa);
        Run("普通角色一万次调用不创建缓存或材质", WarmNormal);
        Run("源仅切换主纹理也刷新并保留凝胶颜色", PerfTexture);
        Run("源仅切换遮罩也刷新眼睑孔洞", PerfMask);
        Run("源仅切换UV也刷新副本", PerfUv);
        Run("源Alpha变化刷新且只乘一次0.85", PerfAlpha);
        Run("源五官颜色变化保留原色与Alpha", PerfFeatureColor);
        Run("源关键词启用和移除都同步", PerfKeywords);
        Run("源Shader变化刷新仅移队列副本", PerfSourceShader);
        Run("源原生对象重绑定且版本相同也刷新", PerfSourceRebind);
        Run("外部改写输出颜色后下一次恢复正确结果", PerfOutputEdit);
        Run("输出原生对象重绑定后重建快照", PerfOutputRebind);
        Run("源队列改变时更新光照后队列", PerfQueue);
        Run("缓存预热后Shader失效不误用旧副本", PerfShaderFailure);
        Run("状态移除后不复用已染材质", PerfGateRemoval);
        Run("同节点控制器切换不残留皮肤分类", PerfControllerChange);
        Run("同节点图层切换不残留皮肤分类", PerfLayerChange);
        Run("相同属性赋值不触发重复复制", PerfSameValue);
        Run("真实变化只复制一次后重新进入稳定路径", PerfOneRefresh);
    }

    private static void StableCalls(Func<Material> apply)
    {
        // 先预热JIT、值元组比较器、弱表及访问器，分配统计只包围实际稳定调用。
        Material first = null;
        for (int i = 0; i < 512; i++) first = apply();
        int copies = Material.Copies, created = Material.Created, version = first.engineMaterial.Version;
        _ = GC.GetAllocatedBytesForCurrentThread();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) apply();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(Material.Copies == copies && Material.Created == created && first.engineMaterial.Version == version,
            "稳定调用不能复制属性、创建材质或反复标脏输出");
        Check(allocated == 0, "模型宿主稳定路径分配应为0，实际=" + allocated);
    }
    private static void WarmSkin(bool masked)
    {
        var rig = DepthRig(masked); var node = masked ? Fa(new LidControllerComp(), NLFacialAnimationLayerType.Cover) : Fa(new HeadControllerComp());
        StableCalls(() => rig.Apply(node));
    }
    private static void WarmFeature() { var rig = new HeadRig(); var node = Feature(); StableCalls(() => rig.Apply(node)); }
    private static void WarmSa()
    {
        var rig = new SaRig(); var node = new PawnRenderNode { tree = new PawnRenderTree { pawn = rig.pawn } };
        var parms = new PawnDrawParms { pawn = rig.pawn }; var worker = new global::SizedApparel.SizedApparelRenderNodeWorker();
        StableCalls(() => { Material result = rig.source; Harmony_FullGelatinizationBodyTint.SizedApparelMaterialPostfix(worker, node, parms, ref result); return result; });
    }
    private static void WarmNormal() { var rig = new HeadRig(); rig.parms.pawn.FullGel = false; var node = Feature(); StableCalls(() => rig.Apply(node)); }
    private static void PerfTexture()
    {
        var rig = DepthRig(); var node = Fa(new HeadControllerComp()); var first = rig.Apply(node);
        rig.source.mainTexture = new Texture("New-face"); var next = rig.Apply(node);
        Check(first == next && next.mainTexture == rig.source.mainTexture && GelTint(next).a == .85f && GelTint(next).b == .68f, "换帧刷新且不洗掉配色");
    }
    private static void PerfMask()
    {
        var rig = DepthRig(true); var node = Fa(new LidControllerComp(), NLFacialAnimationLayerType.Cover); var first = rig.Apply(node);
        var mask = new Texture("Closed-mask"); rig.source.SetTexture("_MaskTex", mask);
        Check(rig.Apply(node) == first && first.GetTexture("_MaskTex") == mask, "不能只检查主贴图而遗漏眼睑遮罩");
    }
    private static void PerfUv()
    {
        var rig = new HeadRig(); var node = Feature(); var first = rig.Apply(node); rig.source.SetFloat("UV", .25f);
        Check(rig.Apply(node) == first && first.GetFloat("UV") == .25f, "不换纹理的UV动画也须刷新");
    }
    private static void PerfAlpha()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); var first = rig.Apply(node);
        rig.source.color = new Color(.3f,.4f,.5f,.4f); var next = rig.Apply(node);
        Check(next == first && Math.Abs(GelTint(next).a - .34f) < 1e-6f && GelTint(next).r == FullGelAppearanceUtility.DefaultRed, "更新源覆盖率，不重复乘色或透明度");
    }
    private static void PerfFeatureColor()
    {
        var rig = new HeadRig(); var node = Feature(); var first = rig.Apply(node); rig.source.color = new Color(.2f,.3f,.4f,.5f);
        Check(rig.Apply(node) == first && first.color.r == .2f && first.color.a == .5f, "嘴型/眼睛颜色仍来自本帧源材质");
    }
    private static void PerfKeywords()
    {
        var rig = new HeadRig(); var node = Feature(); var first = rig.Apply(node);
        rig.source.shaderKeywords = new[] { "FRAME_OPTION" }; rig.Apply(node);
        Check(first.shaderKeywords.Length == 1, "启用关键词必须同步");
        rig.source.shaderKeywords = Array.Empty<string>(); rig.Apply(node);
        Check(first.shaderKeywords.Length == 0, "源关闭关键词不能残留旧效果");
    }
    private static void PerfSourceShader()
    {
        var rig = new HeadRig(); var node = Feature(); var first = rig.Apply(node); var shader = new Shader("New-expression"); rig.source.shader = shader;
        Check(rig.Apply(node) == first && first.shader == shader, "保留五官原Shader的动态变化");
    }
    private static void PerfSourceRebind()
    {
        var rig = new HeadRig(); var node = Feature(); var first = rig.Apply(node); int copies = Material.Copies;
        rig.source.ReplaceEngineBacking(); rig.Apply(node);
        Check(Material.Copies == copies + 1 && first.shader == rig.source.shader, "不能只比较数值版本而忽略原生对象身份");
    }
    private static void PerfOutputEdit()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); var first = rig.Apply(node); first.color = new Color(1,0,0,1);
        Check(rig.Apply(node) == first && first.color.r == 1 && GelTint(first).r == FullGelAppearanceUtility.DefaultRed && GelTint(first).a == .85f, "外部修改副本后重新按源配置生成");
    }
    private static void PerfOutputRebind()
    {
        var rig = new HeadRig(); var node = Feature(); var first = rig.Apply(node); int copies = Material.Copies;
        first.ReplaceEngineBacking(); Check(rig.Apply(node) == first && Material.Copies == copies + 1, "输出引擎对象变化也使缓存失效");
    }
    private static void PerfQueue()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); var first = rig.Apply(node); rig.source.renderQueue = 3200;
        Check(rig.Apply(node) == first && first.renderQueue == 3200 && first.shader.EngineShader.DepthWrite, "原更晚队列与深度继续保留");
    }
    private static void PerfShaderFailure()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); var first = rig.Apply(node); ShaderDef.DepthAvailable = false;
        Check(rig.Apply(node) == rig.source, "注册/资源失败仍保留原结果"); ShaderDef.DepthAvailable = true;
        Check(rig.Apply(node) == first, "资源恢复可再次使用正确副本");
    }
    private static void PerfGateRemoval()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); rig.Apply(node); rig.parms.pawn.FullGel = false;
        Check(rig.Apply(node) == rig.source, "不缓存可能过期的角色准入状态");
    }
    private static void PerfControllerChange()
    {
        var rig = new HeadRig(); var node = (NLFacialAnimationPartNode)Fa(new HeadControllerComp()); var skin = rig.Apply(node);
        node.controller = new MouthControllerComp(); var feature = rig.Apply(node);
        Check(feature != skin && feature.color.r == rig.source.color.r && feature.color.a == 1, "控制器变更实时识别，五官不染紫");
    }
    private static void PerfLayerChange()
    {
        var rig = new HeadRig(); var node = (NLFacialAnimationPartNode)Fa(new HeadControllerComp()); var skin = rig.Apply(node);
        node.layerType = NLFacialAnimationLayerType.Highlight; var feature = rig.Apply(node);
        Check(feature != skin && feature.shader == rig.source.shader && feature.color.a == 1, "不固定图层或动画分类");
    }
    private static void PerfSameValue()
    {
        var rig = new HeadRig(); var node = Feature(); rig.source.SetFloat("UV", .5f); var first = rig.Apply(node); int copies = Material.Copies;
        rig.source.SetFloat("UV", .5f); Check(rig.Apply(node) == first && Material.Copies == copies, "同值写入不产生重复复制");
    }
    private static void PerfOneRefresh()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); var first = rig.Apply(node); int copies = Material.Copies;
        rig.source.SetFloat("UV", .7f); rig.Apply(node); Check(Material.Copies == copies + 1, "真实变化需要一次刷新");
        StableCalls(() => rig.Apply(node));
    }
}
