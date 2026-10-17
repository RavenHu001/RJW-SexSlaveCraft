// AI 辅助生成。生产遮罩/材质代码的回归；四种软依赖组合使用显式类型可见性边界。
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml.Linq;
using FacialAnimation;
using HarmonyLib;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static void AutoAdapterTests(string root)
    {
        foreach (bool fa in new[] { false, true }) foreach (bool sa in new[] { false, true })
        {
            Run($"FA={fa}/SA={sa}按实际节点自动适配", () => AdapterCombination(fa, sa));
            Run($"FA={fa}/SA={sa}普通角色和开关恢复", () => AdapterGates(fa, sa));
            Run($"FA={fa}/SA={sa}隔离编译验证可选类型真实存在性", () => AdapterPresenceHost(root,fa,sa));
        }
        Run("正面两眼遮罩排除头部轮廓和皮肤", EyeMaskGeometry);
        Run("眼角连接外轮廓时仍识别内部眼睛", EyeConnectedOutline);
        Run("轮廓裁剪后的细碎墨迹不被当作额外眼睛", EyeOutlineFragments);
        Run("眼睛内部纯白高光同样保留原色", EyeHighlights);
        Run("单眼侧面及西向镜像使用单眼保护", EyeSides);
        Run("背面保持既有凝胶程序且不读取像素", EyeNorth);
        Run("翻头按实际取图朝向保护正面眼睛", EyeFlipHead);
        Run("眼睛保持原色与原Alpha皮肤保持0.85", EyeChannels);
        Run("原版眼睛绕过凝胶色但保留每帧淡出和色调", EyeDrawTint);
        Run("半覆盖眼睛平滑混合且保留背景孔洞", EyeAntialias);
        Run("写深度与非写深度原版头部各选正确程序", EyeDepthChoice);
        Run("原版眼部材质保留原主图UV与关键词", EyeProperties);
        Run("原版眼部源肤色Alpha变化刷新原色参数", EyeColorRefresh);
        Run("原版换头图不复用旧眼部遮罩", EyeTextureSwap);
        Run("原版眼部预热后一万次不分配不复制不读像素", EyeWarm);
        Run("共享头图的普通角色不被眼部缓存污染", EyeSharedSource);
        Run("首次工作线程不读像素不制造染眼回退", EyeColdWorker);
        Run("工作线程可复用主线程预热的眼部遮罩", EyeWarmWorker);
        Run("不可读纹理只失败一次且保留原头图", EyeUnreadable);
        Run("不支持的纹理类型不退回整头染色", EyeNonReadableType);
        Run("缺眼与额外面部墨迹拒绝猜测", EyeAmbiguous);
        Run("错误尺寸空图和错误眼睛数安全拒绝", EyeBadInputs);
        Run("眼部Shader缺失保留原图且恢复后可用", EyeShaderRecovery);
        Run("隐形雕像腐烂无头跳过眼部分析", EyeGuards);
        Run("FA的独立眼睛不使用原版眼部Shader", EyeFaUnchanged);
        Run("原版头部附加眼睛头发头饰只移光照阶段", EyeAttachments);
        Run("原版头部遮罩槽仍保留且缺失时跳过", EyeSkinMask);
        Run("原版眼部重复回调不重复透明化", EyeIdempotent);
        Run("原版眼部原生资源及镜像正确", () => EyeResources(root));
    }

    private static void SetAdapters(bool fa, bool sa)
    {
        AccessTools.DisableFa = !fa; AccessTools.DisableSa = !sa;
        Harmony_FullGelatinizationHeadTint.InitializeFacialAnimation();
        var hooks = new Harmony(); Harmony_FullGelatinizationBodyTint.RegisterSizedApparel(hooks);
        Check(hooks.Patched.Count == (sa ? 1 : 0), "SA未加载时不注册其专用入口");
    }
    private static void AdapterPresenceHost(string root, bool fa, bool sa)
    {
        string label = $"fa-{fa}-sa-{sa}";
        string output = Path.GetFullPath(Path.Combine(root,".builds/gel-adapter-matrix",label));
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetFullPath(root), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in new[] { "run", "--project", Path.GetFullPath(Path.Combine(root,"Tests/Gelatinization/Gelatinization.csproj")), "--configuration", "Release",
            "-p:SscAdapterMatrixOnly=true", $"-p:SscMatrixFa={fa.ToString().ToLowerInvariant()}", $"-p:SscMatrixSa={sa.ToString().ToLowerInvariant()}",
            "-p:BaseIntermediateOutputPath="+output+"/obj/", "-p:OutputPath="+output+"/bin/" }) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start);
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90000)) { process.Kill(true); throw new Exception("隔离组合宿主超时"); }
        string log = stdout.GetAwaiter().GetResult()+stderr.GetAwaiter().GetResult();
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output,"result.log"),log);
        Check(process.ExitCode==0 && log.Contains($"PASS isolated FA={fa} SA={sa}"),"隔离组合失败："+log);
    }
    // 自制几何夹具，不复制原版像素：带黑色轮廓的白头与一/两只独立眼睛。
    private static Texture2D EyeTexture(int eyes = 2)
    {
        const int size = 64; var pixels = new Color32[size * size];
        for (int y = 8; y < 56; y++) for (int x = 8; x < 56; x++)
            pixels[y * size + x] = x < 10 || x > 53 || y < 10 || y > 53 ? new Color32(0,0,0,255) : new Color32(255,255,255,255);
        for (int eye = 0; eye < eyes; eye++) for (int y = 24; y <= 28; y++) for (int x = 22 + eye * 16; x <= 25 + eye * 16; x++)
            pixels[y * size + x] = new Color32(0,0,0,255);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true); texture.SetPixels32(pixels); return texture;
    }
    private static HeadRig EyeRig(bool depth = true, Rot4? facing = null)
    {
        Rot4 direction = facing ?? Rot4.South;
        var rig = new HeadRig(); rig.parms.facing = direction; rig.source.mainTexture = EyeTexture(direction == Rot4.South ? 2 : 1);
        rig.source.shader.EngineShader.DepthWrite = depth; return rig;
    }
    private static void AdapterCombination(bool fa, bool sa)
    {
        SetAdapters(fa, sa);
        var rig = EyeRig(); var material = rig.Apply(new PawnRenderNode_Head());
        Check(material.shader == ShaderDef.VanillaDepth && GelTint(material).a == .85f && material.renderQueue == 3150, "原版节点不依赖FA加载状态");
        Graphic body = new Graphic { path = "Naked" }; Harmony_FullGelatinizationBodyTint.Postfix(rig.parms.pawn, ref body);
        Check(body.shader == ShaderDef.Native && body.color.r == 1 && body.color.a == 1, "无SA时身体仍凝胶化");
        if (fa)
        {
            Check(new HeadRig().Apply(Fa(new HeadControllerComp())).shader == ShaderDef.Native, "FA皮肤仍使用原分层程序");
            var feature = new HeadRig(); var eye = feature.Apply(Fa(new EyeballControllerComp()));
            Check(eye.shader == feature.source.shader && eye.color.a == 1 && eye.GetTexture("_EyeMask") == null, "FA眼球不染色不叠加原版遮罩");
        }
        var sr = new SaRig(); Check((sr.Apply() != sr.source) == sa, "SA路径独立于FA启停");
    }
    private static void AdapterGates(bool fa, bool sa)
    {
        SetAdapters(fa, sa); var rig = EyeRig(); var node = new PawnRenderNode_Head();
        rig.parms.pawn.FullGel = false; Check(rig.Apply(node) == rig.source, "普通角色保留原图");
        Check(((Texture2D)rig.source.mainTexture).Reads == 0, "普通角色不分析像素");
        rig.parms.pawn.FullGel = true; Check(rig.Apply(node) != rig.source, "完全凝胶且开启后生效");
        SSCMod.settings.enableFullGelatinizedBodyTint = false; Check(rig.Apply(node) == rig.source, "关闭开关立即恢复");
        var sr = new SaRig(); Check(sr.Apply() == sr.source, "关闭同时恢复SA");
        SSCMod.settings.enableFullGelatinizedBodyTint = true; rig.parms.pawn.FullGel = false;
        Check(rig.Apply(node) == rig.source, "移除状态不使用缓存结果");
    }
    private static void EyeMaskGeometry()
    {
        var t = EyeTexture(); Check(FullGelVanillaEyeMask.TryBuild(t.pixels, 64,64,2,out var m), "两眼应识别");
        Check(m[25*64+23].r == 255 && m[25*64+39].r == 255, "两眼保持原色");
        Check(m[32*64+8].r == 0 && m[32*64+32].r == 0 && m[0].r == 0, "黑轮廓/皮肤/背景不误判眼睛");
    }
    private static void EyeSides() { foreach (var facing in new[] { Rot4.East, Rot4.West }) { var r=EyeRig(true,facing); Check(r.Apply(new PawnRenderNode_Head()).shader==ShaderDef.VanillaDepth,"侧面保护单眼"); } }
    private static void EyeConnectedOutline()
    {
        var t = EyeTexture();
        for (int y = 24; y <= 28; y++) for (int x = 42; x < 55; x++) t.pixels[y * 64 + x] = new Color32(0,0,0,255);
        Check(FullGelVanillaEyeMask.TryBuild(t.pixels,64,64,2,out var mask) && mask[25*64+39].r==255 && mask[25*64+54].r==0,
            "连到鼻侧轮廓的眼角须保护，最外轮廓仍柔化");
    }
    private static void EyeOutlineFragments()
    {
        var t = EyeTexture();
        for (int y = 30; y <= 34; y++) t.pixels[y*64+13]=new Color32(0,0,0,255);
        Check(FullGelVanillaEyeMask.TryBuild(t.pixels,64,64,2,out var mask) && mask[32*64+13].r==0,"细碎轮廓不能制造第三只眼睛");
    }
    private static void EyeHighlights()
    {
        var t = EyeTexture(); t.pixels[26*64+23] = new Color32(255,255,255,255);
        Check(FullGelVanillaEyeMask.TryBuild(t.pixels,64,64,2,out var mask) && mask[26*64+23].r==255,"封闭的白高光不能染紫");
    }
    private static void EyeNorth() { var r=EyeRig(true,Rot4.North); Check(r.Apply(new PawnRenderNode_Head()).shader==ShaderDef.Depth && ((Texture2D)r.source.mainTexture).Reads==0,"背面不需要眼部遮罩"); }
    private static void EyeFlipHead()
    {
        var front = EyeRig(); front.parms.facing = Rot4.North; front.parms.flipHead = true;
        Check(front.Apply(new PawnRenderNode_Head()).shader == ShaderDef.VanillaDepth,"北向翻头实际取正面，必须保护两眼");
        var back = EyeRig(); back.parms.facing = Rot4.South; back.parms.flipHead = true;
        Check(back.Apply(new PawnRenderNode_Head()).shader == ShaderDef.Depth && ((Texture2D)back.source.mainTexture).Reads == 0,
            "南向翻头实际取背面，不应识别不存在的眼睛");
    }
    private static void EyeChannels()
    {
        Check(GelShaderMath.sscGelEyeBlend(FullGelAppearanceUtility.DefaultRed,0,1)==0 && GelShaderMath.sscGelEyeBlend(.85f,1,1)==1,"眼部黑色与Alpha不染色或透明化");
        Check(GelShaderMath.sscGelEyeBlend(FullGelAppearanceUtility.DefaultRed,.9f,0)==FullGelAppearanceUtility.DefaultRed && GelShaderMath.sscGelEyeBlend(.85f,1,0)==.85f,"皮肤保留当前参数");
    }
    private static void EyeAntialias() { Check(Math.Abs(GelShaderMath.sscGelEyeBlend(.4f,.8f,.5f)-.6f)<1e-6f,"半覆盖连续混合"); Check(GelShaderMath.sscGelEyeBlend(0,0,1)==0,"背景Alpha不能被眼睛填充"); }
    private static void EyeDrawTint()
    {
        var r = EyeRig(); var m = r.Apply(new PawnRenderNode_Head());
        Check(m.GetColor("_GelColor").a == .85f && m.color.r == 1 && m.color.a == 1,"凝胶参数与原绘制色独立");
        foreach (float modulation in new[] { 0f, .25f, 1f, 1.5f })
        {
            float channel = GelShaderMath.sscGelOriginalChannel(.8f,.5f,modulation);
            Check(Math.Abs(channel-.4f*modulation)<1e-6f,"眼睛直接跟随parms.tint而不受凝胶零通道影响");
        }
    }
    private static void EyeDepthChoice() { foreach(bool d in new[]{false,true}) {var r=EyeRig(d);Check(r.Apply(new PawnRenderNode_Head()).shader==(d?ShaderDef.VanillaDepth:ShaderDef.Vanilla),"继承原深度状态");} }
    private static void EyeProperties() {var r=EyeRig();r.source.SetFloat("UV",.3f);r.source.shaderKeywords=new[]{"ANIM"};var m=r.Apply(new PawnRenderNode_Head());Check(m.mainTexture==r.source.mainTexture&&m.GetFloat("UV")==.3f&&Array.IndexOf(m.shaderKeywords,"ANIM")>=0,"同图同UV同关键词");Check(r.source.GetTexture("_EyeMask")==null,"共享原材质不变");}
    private static void EyeColorRefresh() {var r=EyeRig();var n=new PawnRenderNode_Head();var m=r.Apply(n);r.source.color=new Color(.2f,.3f,.4f,.6f);Check(r.Apply(n)==m&&m.GetColor("_EyeSourceColor").r==.2f&&m.GetColor("_EyeSourceColor").a==.6f&&Math.Abs(GelTint(m).a-.51f)<1e-6f,"眼部原色和皮肤Alpha同步");}
    private static void EyeTextureSwap() {var r=EyeRig();var n=new PawnRenderNode_Head();var m=r.Apply(n);var mask=m.GetTexture("_EyeMask");r.source.mainTexture=EyeTexture();Check(r.Apply(n)==m&&m.GetTexture("_EyeMask")!=mask,"换图刷新遮罩");}
    private static void EyeWarm() {var r=EyeRig();var n=new PawnRenderNode_Head();StableCalls(()=>r.Apply(n));Check(((Texture2D)r.source.mainTexture).Reads==1,"只扫描一次");}
    private static void EyeSharedSource() {var r=EyeRig();r.Apply(new PawnRenderNode_Head());r.parms.pawn.FullGel=false;Check(r.Apply(new PawnRenderNode_Head())==r.source&&r.source.color.a==1,"普通角色不串材质");}
    private static void EyeColdWorker() {var r=EyeRig();Task.Run(()=>{UnityData.IsInMainThread=false;Check(r.Apply(new PawnRenderNode_Head())==r.source,"工作线程首次保留原图");}).GetAwaiter().GetResult();UnityData.IsInMainThread=true;Check(((Texture2D)r.source.mainTexture).Reads==0&&r.Apply(new PawnRenderNode_Head())!=r.source,"主线程后续可恢复");}
    private static void EyeWarmWorker() {var r=EyeRig();var n=new PawnRenderNode_Head();var m=r.Apply(n);Task.Run(()=>{UnityData.IsInMainThread=false;Check(r.Apply(n)==m,"工作线程复用");}).GetAwaiter().GetResult();UnityData.IsInMainThread=true;Check(((Texture2D)r.source.mainTexture).Reads==1,"不再读纹理");}
    private static void EyeUnreadable() {var r=EyeRig();var t=(Texture2D)r.source.mainTexture;t.ThrowRead=true;for(int i=0;i<4;i++)Check(r.Apply(new PawnRenderNode_Head())==r.source,"不可读时原样保留");Check(t.Reads==1,"失败只读一次");}
    private static void EyeNonReadableType() {var r=EyeRig();r.source.mainTexture=new Texture("RenderTexture");Check(r.Apply(new PawnRenderNode_Head())==r.source,"不染眼回退");}
    private static void EyeAmbiguous() {var t=EyeTexture(1);Check(!FullGelVanillaEyeMask.TryBuild(t.pixels,64,64,2,out _),"正面缺眼不猜测");t=EyeTexture();for(int y=35;y<39;y++)for(int x=30;x<34;x++)t.pixels[y*64+x]=new Color32(0,0,0,255);Check(!FullGelVanillaEyeMask.TryBuild(t.pixels,64,64,2,out _),"第三块面部墨迹不染色");}
    private static void EyeBadInputs() {Check(!FullGelVanillaEyeMask.TryBuild(null,64,64,2,out _)&&!FullGelVanillaEyeMask.TryBuild(new Color32[4],2,2,2,out _)&&!FullGelVanillaEyeMask.TryBuild(new Color32[64*64],64,64,0,out _),"输入保护");}
    private static void EyeShaderRecovery() {var r=EyeRig();var n=new PawnRenderNode_Head();ShaderDef.Available=false;Check(r.Apply(n)==r.source,"缺Shader保留眼睛");ShaderDef.Available=true;Check(r.Apply(n).shader==ShaderDef.VanillaDepth,"资源恢复可用");}
    private static void EyeGuards() {foreach(int mode in new[]{0,1,2,3}) {var r=EyeRig();if(mode==0)r.parms.flags=PawnRenderFlags.Invisible;if(mode==1)r.parms.Statue=true;if(mode==2)r.parms.rotDrawMode=RotDrawMode.Rotting;if(mode==3)r.parms.pawn.health.hediffSet.HasHead=false;Check(r.Apply(new PawnRenderNode_Head())==r.source&&((Texture2D)r.source.mainTexture).Reads==0,"特殊状态先于扫描");}}
    private static void EyeFaUnchanged() {var r=EyeRig();var m=r.Apply(Fa(new HeadControllerComp()));Check(m.shader==ShaderDef.Depth&&m.GetTexture("_EyeMask")==null&&((Texture2D)r.source.mainTexture).Reads==0,"FA头皮不触发原版分析");var e=r.Apply(Fa(new EyeballControllerComp()));Check(e.shader==r.source.shader&&e.color.a==1,"FA眼睛保留");}
    private static void EyeAttachments() {foreach(var n in new PawnRenderNode[]{new PawnRenderNode {parent=new PawnRenderNode_Head()},new PawnRenderNode_Hair(),new PawnRenderNode_Apparel {parent=new PawnRenderNode_Head()}}) {var r=EyeRig();var m=r.Apply(n);Check(m.shader==r.source.shader&&m.color.a==1&&m.renderQueue==3150,"附加眼睛和毛发不染色");}}
    private static void EyeSkinMask() {var r=EyeRig();r.source.HasMaskSlot=true;Check(r.Apply(new PawnRenderNode_Head())==r.source,"缺遮罩不漏皮肤");var mask=new Texture("Skin");r.source.SetTexture("_MaskTex",mask);var m=r.Apply(new PawnRenderNode_Head());Check(m.GetTexture("_MaskTex")==mask&&m.GetFloat("_UseSkinMask")==1,"已有皮肤遮罩继续生效");}
    private static void EyeIdempotent() {var r=EyeRig();var n=new PawnRenderNode_Head();var m=r.Apply(n);var again=m;Harmony_FullGelatinizationHeadTint.MaterialPostfix(n,r.parms,ref again);Check(again==m&&GelTint(m).a==.85f,"不能重复乘Alpha");}
    private static void EyeResources(string root)
    {
        foreach(var name in new[]{Harmony_FullGelatinizationHeadTint.VanillaShaderDefName,Harmony_FullGelatinizationHeadTint.VanillaDepthShaderDefName})
        {
            string rel="Defs/ShaderDefs/"+name+".xml";Check(File.ReadAllText(Path.Combine(root,"1.7",rel))==File.ReadAllText(Path.Combine(root,"Sexslavecraft",rel)),"ShaderDef镜像一致");
            var p=XDocument.Load(Path.Combine(root,"1.7",rel)).Root.Element("ShaderDef").Element("shaderProgram");
            Check(p.Element("renderQueue").Value=="3150"&&p.Element("depthWrite").Value==name.Contains("Depth").ToString().ToLowerInvariant(),"光照与深度状态");
            string code=File.ReadAllText(Path.Combine(root,"1.7/Shaders",p.Element("fragmentShaderFileName").Value));
            Check(code.Contains("_EyeSourceColor")&&code.Contains("_EyeMask")&&code.Contains("sscGelEyeBlend"),"原色及遮罩接入片元");
        }
    }
}
