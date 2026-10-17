// AI 辅助生成。回归头身交叠深度及真实工作线程调用边界，不声称代替游戏画面验收。
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static void RenderFixTests(string root)
    {
        Run("基础头皮继承原深度写入而不是统一透明管线", () => HeadDepth(false));
        Run("带遮罩的深度皮肤同样保留写入", () => HeadDepth(true));
        Run("原透明皮肤覆盖层继续不写深度", () => SkinMaterial(Fa(new FacialAnimation.HeadControllerComp(), FacialAnimation.NLFacialAnimationLayerType.Cover), .3f));
        Run("原Shader解析失败时保留原头皮", HeadUnknownDepth);
        Run("深度Shader缺失时保留原头皮不降级", HeadDepthUnavailable);
        Run("动画切换原Shader深度状态时重新选变体", HeadDepthChange);
        Run("头身交叠处阻止后画的身体盖到脸上", () => HeadBodyOcclusion(root));
        Run("原图透明孔洞与低覆盖边缘不写深度", DepthCoverage);
        Run("所有头部写深度变体使用生产裁剪函数", () => DepthResources(root));
        Run("SA注册最终材质而非GetGraphic入口", SaRegistration);
        Run("SA工作线程首次缓存未命中仍完整染色", SaWorkerCold);
        Run("SA帧切换更新贴图并复用材质", SaFrames);
        Run("SA派生worker同样经过材质路径", () => SaTint(new global::SizedApparel.DerivedWorker()));
        Run("普通worker不被SA染色误命中", SaOtherWorker);
        Run("SA与头皮共享原材质时缓存版本隔离", SharedMaterialVariants);
        Run("SA关闭染色后立即恢复原材质", SaDisabled);
        Run("SA不存在材质时不制造图层", SaNull);
        Run("SA源材质与角色不匹配时保留原图", SaWrongPawn);
        Run("身体预缓存工作线程不调用GraphicDatabase", GraphicWorker);
        Run("旧服装入口工作线程不创建Graphic且不丢原图", ApparelWorker);
        Run("主线程Graphic创建返回null时保留原图", GraphicNull);
    }

    private static HeadRig DepthRig(bool masked = false)
    {
        var rig = new HeadRig(); rig.source.shader.EngineShader.DepthWrite = true;
        if (masked) { rig.source.HasMaskSlot = true; rig.source.SetTexture("_MaskTex", new Texture("SkinMask")); }
        return rig;
    }
    private static void HeadDepth(bool masked)
    {
        var rig = DepthRig(masked); var result = rig.Apply(new PawnRenderNode_Head());
        Check(result.shader == (masked ? ShaderDef.MaskedDepth : ShaderDef.Depth), "需要对应的深度写入变体");
        Check(result.renderQueue == 3150 && result.color.a == .85f, "统一光照后队列但保留85%混合");
    }
    private static void HeadUnknownDepth()
    {
        var rig = new HeadRig(); rig.source.shader.EngineShader = null;
        Check(rig.Apply(new PawnRenderNode_Head()) == rig.source, "未知管线不能猜写深度状态");
    }
    private static void HeadDepthUnavailable()
    {
        var rig = DepthRig(); ShaderDef.DepthAvailable = false;
        Check(rig.Apply(new PawnRenderNode_Head()) == rig.source, "不能回退到不写深度的身体程序");
    }
    private static void HeadDepthChange()
    {
        var rig = DepthRig(); var node = new PawnRenderNode_Head(); var first = rig.Apply(node);
        rig.source.shader.EngineShader.DepthWrite = false; var second = rig.Apply(node);
        Check(first != second && second.shader == ShaderDef.Native && first.shader == ShaderDef.Depth, "不同深度版本不能改写已返回材质");
    }
    private static void HeadBodyOcclusion(string root)
    {
        // 最小光栅边界：头部先画且更近，身体在3150后画。深度状态直接读实际运行XML。
        // 旧实现返回身体Shader，depthWrite=false，会让重叠像素的身体覆盖率从0变成0.85。
        var rig = DepthRig(); rig.source.renderQueue = 2900; var head = rig.Apply(new PawnRenderNode_Head());
        bool writes = bool.Parse(XDocument.Load(Path.Combine(SscTestPaths.RuntimeRoot(root), "Defs/ShaderDefs", head.shader.name + ".xml"))
            .Root.Element("ShaderDef").Element("shaderProgram").Element("depthWrite").Value);
        float depth = 1, headDepth = .2f, bodyDepth = .4f;
        if (writes && GelShaderMath.sscGelWritesDepth(1)) depth = headDepth;
        Check(bodyDepth > depth && head.color.a == .85f, "头皮仍半透明混合，但必须遮挡其后的身体");
        depth = 1;
        if (writes && GelShaderMath.sscGelWritesDepth(0)) depth = headDepth;
        Check(bodyDepth < depth, "透明孔洞不得阻挡后方图层");
    }
    private static void DepthCoverage()
    {
        foreach (float coverage in new[] { 0f, .001f, .099f }) Check(!GelShaderMath.sscGelWritesDepth(coverage), "无效覆盖不能写深度");
        foreach (float coverage in new[] { .1f, .5f, 1f }) Check(GelShaderMath.sscGelWritesDepth(coverage), "保留原CutoutSkin阈值，不能乘0.85后再裁剪");
    }
    private static void DepthResources(string root)
    {
        foreach (string name in new[] { "SSC_FullGelHeadDepth17", "SSC_FullGelHeadMaskedDepth17" })
        {
            var program = XDocument.Load(Path.Combine(SscTestPaths.RuntimeRoot(root), "Defs/ShaderDefs", name + ".xml")).Root.Element("ShaderDef").Element("shaderProgram");
            Check((string)program.Element("depthWrite") == "true", "深度变体必须启用写入");
            string fragment = File.ReadAllText(Path.Combine(SscTestPaths.RuntimeRoot(root), "Shaders", program.Element("fragmentShaderFileName").Value));
            Check(fragment.Contains("if (!sscGelWritesDepth(") && fragment.Contains("sscGelChannel(source.r, tint.r)"), "片元使用共同裁剪与颜色函数");
        }
    }
    private static void SaRegistration()
    {
        var harmony = new Harmony(); Harmony_FullGelatinizationBodyTint.RegisterSizedApparel(harmony);
        Check(harmony.Patched.Count == 1 && harmony.Patched[0].Name == "GetFinalizedMaterial", "不能再补GetGraphic");
        string startup = File.ReadAllText(Path.Combine(SscTestPaths.RepositoryRoot(null), "Sexslavecraft/ThePatches/HarmonyStartUp.cs"));
        Check(!startup.Contains("nameof(Harmony_FullGelatinizationBodyTint.SizedApparelWorkerPostfix)"), "启动入口不能留下旧补丁");
    }
    private sealed class SaRig
    {
        public Pawn pawn = new Pawn { FullGel = true };
        public Material source = new Material(new Shader("SA-original")) { mainTexture = new Texture("SA-first"), color = new Color(1,1,1,1) };
        public Material Apply(PawnRenderNodeWorker worker = null, Pawn other = null)
        {
            Material material = source;
            Harmony_FullGelatinizationBodyTint.SizedApparelMaterialPostfix(worker ?? new global::SizedApparel.SizedApparelRenderNodeWorker(),
                new PawnRenderNode { tree = new PawnRenderTree { pawn = pawn } }, new PawnDrawParms { pawn = other ?? pawn }, ref material);
            return material;
        }
    }
    private static void SaTint(PawnRenderNodeWorker worker)
    {
        var rig = new SaRig(); var result = rig.Apply(worker);
        Check(result != rig.source && result.shader == ShaderDef.Native && result.color.a == .85f && result.renderQueue == 3150, "SA材质保持已验收程序、透明度及队列");
    }
    private static void SaWorkerCold()
    {
        var rig = new SaRig(); Material result = null;
        Task.Run(() => { UnityData.IsInMainThread = false; try { result = rig.Apply(); } finally { UnityData.IsInMainThread = true; } }).GetAwaiter().GetResult();
        Check(result != null && result != rig.source && result.shader == ShaderDef.Native && Graphic.ColoredRequests == 0, "工作线程首次使用也不能请求Graphic");
        Check(rig.source.shader.name == "SA-original" && rig.source.color.a == 1, "共享原材质不受污染");
    }
    private static void SaFrames()
    {
        var rig = new SaRig(); var first = rig.Apply(); int created = Material.Created;
        rig.source.mainTexture = new Texture("SA-next"); rig.source.SetFloat("UV", .4f); var next = rig.Apply();
        Check(first == next && Material.Created == created && next.mainTexture == rig.source.mainTexture && next.GetFloat("UV") == .4f, "换帧不能固化纹理或反复创建材质");
    }
    private static void SaOtherWorker() { var rig = new SaRig(); Check(rig.Apply(new PawnRenderNodeWorker()) == rig.source, "只染SA worker"); }
    private static void SharedMaterialVariants()
    {
        var head = new HeadRig(); var headResult = head.Apply(new PawnRenderNode_Head());
        var sa = new SaRig { source = head.source }; var saResult = sa.Apply();
        Check(headResult != saResult && headResult.renderQueue == 3150 && saResult.renderQueue == 3150, "按用途隔离缓存，保持共同光照阶段");
    }
    private static void SaDisabled() { var rig = new SaRig(); rig.Apply(); SSCMod.settings.enableFullGelatinizedBodyTint = false; Check(rig.Apply() == rig.source, "关闭立即回到原材质"); }
    private static void SaNull() { var rig = new SaRig { source = null }; Check(rig.Apply() == null, "空材质保持空"); }
    private static void SaWrongPawn() { var rig = new SaRig(); Check(rig.Apply(other: new Pawn { FullGel = true }) == rig.source, "不能拿另一个角色的状态染色"); }
    private static void GraphicWorker()
    {
        Graphic graphic = Original(), source = graphic; UnityData.IsInMainThread = false;
        Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = true }, ref graphic);
        Check(graphic == source && Graphic.ColoredRequests == 0, "非主线程不进入图形缓存");
    }
    private static void ApparelWorker()
    {
        UnityData.IsInMainThread = false; var apparel = new RimWorld.Apparel { Wearer = new Pawn { FullGel = true } };
        var source = Original("SizedApparel/Clothes"); var rec = new RimWorld.ApparelGraphicRecord(source, apparel);
        Harmony_FullGelatinizationBodyTint.SizedApparelApparelPostfix(apparel, ref rec, true);
        Check(rec.graphic == source && rec.sourceApparel == apparel && Graphic.ColoredRequests == 0, "工作线程不能把已有服装图形替换为null");
    }
    private static void GraphicNull()
    {
        Graphic.ReturnNull = true; var source = Original(); var result = source;
        Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = true }, ref result);
        Check(result == source, "主线程失败也保留已有图形");
    }
}
