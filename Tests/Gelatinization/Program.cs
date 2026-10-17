using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

/// <summary>链接生产源码并读取发布 XML，验证过程互斥、染色及状态签名；引擎边界由 Stubs.cs 提供。</summary>
internal static class Program
{
    private static int passed, failed;
    private static XElement partialXml, fullXml;

    /// <summary>按故障类别登记用例，输出统一门禁可解析的唯一结果计数及进程退出码。</summary>
    private static int Main(string[] args)
    {
        // 统一入口指定所选版本资源；独立运行默认读取 1.7，不使用冻结的 1.6 配置。
        string root = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        var defs = XDocument.Load(SscTestPaths.Combine(root, "Defs/HediffDefs/Hediffs_Gelatinization.xml")).Root;
        partialXml = defs.Elements().Single(x => (string)x.Element("defName") == "Hediff_GelatinizationTemporary");
        fullXml = defs.Elements().Single(x => (string)x.Element("defName") == "SSC_FullGelatinizationTemporary");
        // 从默认进度完整走到旧缺陷出现的末轮，证明双重结算可由合法增长序列触发。
        Run("局部胶化默认状态的合法竞赛只结算成功", () => NaturalRace(false, 180));
        Run("全胶默认状态的合法竞赛只结算成功", () => NaturalRace(true, 26));
        // 两类过程共用结果互斥、失效回调、正常扫描和旧按钮测试，避免仅修复其中一类。
        foreach (bool full in new[] { false, true })
        {
            string name = full ? "全胶" : "局部胶化";
            Run(name + "同轮双阈值以先执行的适应成功为准", () => Simultaneous(full, false));
            Run(name + "失败先结算后不能再成功", () => Simultaneous(full, true));
            Run(name + "单独失败仍保留原有失败结果", () => Failure(full));
            Run(name + "外部移除后不再推进或结算", () => Inactive(full, false));
            Run(name + "死亡后不再推进或结算", () => Inactive(full, true));
            Run(name + "未达阈值保持进行中", () => Pending(full));
            Run(name + "移除旧过程不会影响同角色的新过程", () => Replacement(full));
            Run(name + "旧调试成功按钮只能结算一次", () => DebugSuccess(full));
        }
        // 再覆盖各分支特有的缺肢、无效部位、必定成功以及缺少所有者边界。
        Run("局部调试失败后不能重复失败或强制成功", DebugFailure);
        Run("局部成功发现缺失肢体时只清理过程", MissingPart);
        Run("全胶必定成功配置仍正常完成", Guaranteed);
        Run("组件缺少父状态或角色时安全跳过", MissingOwner);
        // 渲染测试同时覆盖应染色与应保留原图的分支，防止简单禁用染色也能通过检查。
        Run("1.7选择已注册原生凝胶Shader", BodyShader);
        Run("缺少原生Def时安全回退", () => ShaderFallback(false, true, false));
        Run("非空supported但未注册时安全回退", () => ShaderFallback(true, false, false));
        Run("注册表返回引擎默认时安全回退", () => ShaderFallback(true, true, true));
        Run("早期回退不会永久缓存失败", ShaderRecovery);
        // 直接执行生产 GLSL 函数体，验证像素性质；不以相同公式的另一份拷贝作为断言。
        Run("黑描边映射为可辨认深紫而非黑色", GelDarkEdge);
        Run("灰度顺序与内部细节保持单调", GelDetail);
        Run("亮部有适量光泽且不溢出", GelHighlight);
        Run("透明背景和孔洞始终透明", GelTransparent);
        Run("抗锯齿覆盖连续且不过度透明", GelCoverage);
        Run("异常颜色和Alpha输入被限制在合法区间", GelBounds);
        Run("1.7原生Shader资源成套且不挂接头部", () => ShaderResources(root));
        Run("SizedApparel从渲染树角色染色", SizedApparel);
        Run("SizedApparel空节点安全跳过", () => EmptyNode(null));
        Run("SizedApparel空渲染树安全跳过", () => EmptyNode(new PawnRenderNode()));
        Run("SizedApparel渲染树无角色安全跳过", () => EmptyNode(new PawnRenderNode { tree = new PawnRenderTree() }));
        Run("未完全胶化角色保留原图形", () => BodyGuard(false, true));
        Run("关闭染色保留原图形", () => BodyGuard(true, false));
        Run("空图形安全跳过", () => { Graphic graphic = null; Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = true }, ref graphic); Check(graphic == null, "空图形应保留"); });
        Run("SizedApparel服装变体使用相同透明Shader", ApparelTint);
        Run("普通服装与未成功生成的图形保持原样", ApparelGuard);
        // 最后一项启动独立 PowerShell，必须完成正反两类共 10 项签名断言才算通过。
        Run("兼容验证器接受合法state并拒绝错误配对", () => StateValidator(root));
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>重置可变输入，单项失败继续执行后续用例，最后统一报告失败。</summary>
    private static void Run(string name, Action test)
    {
        // 固定增量分别位于 XML 配置的局部适应、局部严重度和全胶区间内。
        // 连续概率命中是合法序列；测试证明故障可达，不估计实际发生频率。
        Rand.Draws = 0;
        Rand.Hit = true;
        Rand.RangeDraw = range => Math.Abs(range.min - .005f) < 1e-6f ? .00556f :
            Math.Abs(range.min - .003f) < 1e-6f ? .00551f : range.min == range.max ? range.min : .0392f;
        SSCMod.settings = new SSCSettings { enableFullGelatinizedBodyTint = true };
        ShaderDef.Available = true;
        Waylith.Rendering.ShaderRegistry.Registered = true;
        Waylith.Rendering.ShaderRegistry.ReturnDefault = false;
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }

    /// <summary>持有具体过程及角色，以共同接口读取两类生产临时状态的适应字段。</summary>
    private sealed class Process
    {
        public Pawn Pawn;
        public HediffWithComps Parent;
        // Create 会核对实际 XML 的适应→严重度顺序，因此此处索引与发布配置一致。
        public HediffComp Adaptation => Parent.comps[0];
        public HediffComp Severity => Parent.comps[1];
        public float Adapted
        {
            get => Parent is Hediff_FullGelatinizationTemporary f ? f.adaptation : ((Hediff_GelatinizationTemporary)Parent).adaptation;
            set { if (Parent is Hediff_FullGelatinizationTemporary f) f.adaptation = value; else ((Hediff_GelatinizationTemporary)Parent).adaptation = value; }
        }
        // 一次 Scan 对应引擎完整遍历；首组件移除 Parent 不会使模型跳过后续组件。
        public void Scan(int delta = 600) => Parent.PostTickInterval(delta);
    }

    /// <summary>按发布 XML 创建生产属性及组件，可复用角色以验证新旧过程隔离。</summary>
    private static Process Create(bool full, Pawn pawn = null)
    {
        // 默认严重度、有效肢体和生产临时类组成合法起点；不从临近完成状态替代自然竞赛测试。
        XElement xml = full ? fullXml : partialXml;
        pawn ??= new Pawn();
        HediffWithComps parent = full ? new Hediff_FullGelatinizationTemporary() : new Hediff_GelatinizationTemporary();
        parent.pawn = pawn;
        parent.Part = new BodyPartRecord();
        parent.Part.def.tags.Add(BodyPartTagDefOf.ManipulationLimbCore);
        parent.Severity = Number(xml.Element("initialSeverity").Value);
        // 按发布顺序解析真实属性字段，区分整数、小数及区间；未知字段会直接使夹具失败。
        foreach (XElement node in xml.Element("comps").Elements())
        {
            var type = typeof(Program).Assembly.GetType(node.Attribute("Class").Value, true);
            var props = (HediffCompProperties)Activator.CreateInstance(type);
            foreach (XElement field in node.Elements())
            {
                var info = type.GetField(field.Name.LocalName);
                object value = info.FieldType == typeof(int) ? (object)int.Parse(field.Value) :
                    info.FieldType == typeof(float) ? (object)Number(field.Value) : Range(field.Value);
                info.SetValue(props, value);
            }
            // compClass 来自生产属性类，组件算法没有在测试里重写；只补齐引擎通常设置的引用。
            var comp = (HediffComp)Activator.CreateInstance(props.compClass);
            comp.parent = parent; comp.props = props;
            parent.comps.Add(comp);
        }
        Check(parent.comps.Count == 2 && parent.comps[0].GetType().Name.EndsWith("Adaptation"), "使用发布XML的实际组件顺序");
        // 共享终结检查依赖具体对象仍在健康列表中，必须真正附着 Parent 而不能只保存定义名。
        pawn.health.hediffSet.hediffs.Add(parent);
        return new Process { Pawn = pawn, Parent = parent };
    }

    /// <summary>固定使用 XML 的小数格式，避免当前系统区域设置改变测试输入。</summary>
    private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    /// <summary>解析发布配置的 min~max 区间，保留实际上下界供随机输入合法性检查。</summary>
    private static FloatRange Range(string value) { var parts = value.Split('~'); return new FloatRange(Number(parts[0]), Number(parts[1])); }
    // 事件计数比最终状态布尔值更严格，能发现先成功再失败或重复成功等中间副作用。
    private static int Successes(Process p) => p.Pawn.Events.Count(x => x == "success");
    private static int Failures(Process p) => p.Pawn.Events.Count(x => x == "failure");
    /// <summary>同时核对结果次数、父状态移除及身体销毁，防止最终显示正确却已重复结算。</summary>
    private static void Outcome(Process p, int success, int failure)
    {
        Check(Successes(p) == success && Failures(p) == failure, "结局不应重复：" + string.Join(",", p.Pawn.Events));
        Check(!p.Pawn.health.hediffSet.hediffs.Contains(p.Parent), "临时过程必须移除");
        Check(p.Pawn.Destroyed == (p.Parent is Hediff_FullGelatinizationTemporary && failure == 1), "角色销毁状态");
    }

    /// <summary>从默认状态走完合法竞赛，并确认结束后的多次回调不再消耗随机数或推进严重度。</summary>
    private static void NaturalRace(bool full, int scans)
    {
        Process p = Create(full);
        for (int i = 0; i < scans - 1; i++) { p.Scan(); Check(p.Pawn.Events.Count == 0, "不能提前结束"); }
        p.Scan(); Outcome(p, 1, 0);
        float severity = p.Parent.Severity;
        int draws = Rand.Draws;
        p.Scan(); p.Scan();
        Outcome(p, 1, 0);
        Check(p.Parent.Severity == severity && draws == Rand.Draws, "结束后不消耗随机数或继续严重度");
    }

    /// <summary>让两条进度同轮越过阈值，验证实际顺序及反向调用顺序都只接受第一个结果。</summary>
    private static void Simultaneous(bool full, bool failureFirst)
    {
        Process p = Create(full); p.Adapted = .999f; p.Parent.Severity = .999f;
        // 反向调用仅用于检验共享终结规则；正常 Scan 仍严格遵循 XML 的适应优先顺序。
        if (failureFirst)
        {
            float adjustment = 0;
            p.Severity.CompPostTickInterval(ref adjustment, 600);
            p.Adaptation.CompPostTickInterval(ref adjustment, 600);
        }
        else p.Scan();
        Outcome(p, failureFirst ? 0 : 1, failureFirst ? 1 : 0);
    }

    /// <summary>验证自然失败仍有效，且过程结束后的再次扫描不会重复缺肢或销毁角色。</summary>
    private static void Failure(bool full)
    {
        Process p = Create(full); p.Parent.Severity = .999f;
        p.Scan(); Outcome(p, 0, 1);
        p.Scan(); Outcome(p, 0, 1);
    }

    /// <summary>分别模拟外部移除与死亡，旧组件须在推进计时或抽随机数之前停止。</summary>
    private static void Inactive(bool full, bool dead)
    {
        Process p = Create(full); p.Adapted = .999f; p.Parent.Severity = .999f;
        if (dead) p.Pawn.Dead = true; else p.Pawn.health.RemoveHediff(p.Parent);
        p.Scan();
        Check(p.Pawn.Events.Count == 0 && Rand.Draws == 0 && p.Adapted == .999f && p.Parent.Severity == .999f, "失效过程不得推进");
    }

    /// <summary>拆成 599+1 tick 跨越真实扫描边界，验证防重检查没有阻止有效过程正常成长。</summary>
    private static void Pending(bool full)
    {
        Process p = Create(full);
        p.Scan(599); Check(p.Adapted == 0 && Rand.Draws == 0, "600 tick前不扫描");
        p.Scan(1); Check(p.Adapted > 0 && p.Parent.Severity > .01f && p.Pawn.Events.Count == 0, "正常进度保留");
        Check(p.Pawn.health.hediffSet.hediffs.Contains(p.Parent), "未完成过程仍附着");
    }

    /// <summary>同角色添加新的同类过程后，旧对象的回调不得误认新过程为自身的继续。</summary>
    private static void Replacement(bool full)
    {
        Process old = Create(full); old.Pawn.health.RemoveHediff(old.Parent);
        Process current = Create(full, old.Pawn);
        old.Adapted = 1; old.Parent.Severity = 1; old.Scan();
        Check(current.Pawn.health.hediffSet.hediffs.Contains(current.Parent) && current.Pawn.Events.Count == 0, "旧过程不能影响新过程");
    }

    /// <summary>重复调用已创建的成功按钮，再推进旧组件，确认 UI 委托不能绕过终结检查。</summary>
    private static void DebugSuccess(bool full)
    {
        Process p = Create(full);
        var command = (Command_Action)p.Parent.GetGizmos().First();
        command.action(); command.action(); p.Scan();
        if (!full) ((HediffComp_GelatinizationSeverity)p.Severity).DebugForceCompleteFailure();
        Outcome(p, 1, 0);
    }

    /// <summary>强制失败后再强制失败或成功，所有迟到请求都只能保留第一个缺肢结果。</summary>
    private static void DebugFailure()
    {
        Process p = Create(false);
        var failure = (HediffComp_GelatinizationSeverity)p.Severity;
        failure.DebugForceCompleteFailure(); failure.DebugForceCompleteFailure();
        ((HediffComp_GelatinizationAdaptation)p.Adaptation).DebugForceCompleteSuccess();
        Outcome(p, 0, 1);
    }

    /// <summary>无效部位只清理临时过程，不添加成功义肢或追加失败结果。</summary>
    private static void MissingPart()
    {
        Process p = Create(false); p.Pawn.health.hediffSet.Missing = true;
        p.Adapted = .999f; p.Parent.Severity = .999f; p.Scan();
        Check(p.Pawn.Events.Count == 0 && !p.Pawn.health.hediffSet.hediffs.Contains(p.Parent), "无效部位只清理过程");
    }

    /// <summary>必定成功路径使用其专用增长区间，确认修复后仍能正常完成。</summary>
    private static void Guaranteed()
    {
        Process p = Create(true); p.Pawn.Guaranteed = true;
        Rand.RangeDraw = range => range.min;
        for (int i = 0; i < 4; i++) p.Scan();
        Outcome(p, 1, 0);
    }

    /// <summary>分别清空角色引用和组件父引用，入口应安全返回且不消耗随机数。</summary>
    private static void MissingOwner()
    {
        foreach (bool full in new[] { false, true })
        {
            Process p = Create(full); p.Parent.pawn = null; p.Scan();
            p.Parent.comps.ForEach(c => c.parent = null); p.Scan();
        }
        Check(Rand.Draws == 0, "缺失所有者不得消耗随机数");
    }

    /// <summary>创建带可辨认原 Shader 的图形，以便判断补丁是否真正替换结果。</summary>
    private static Graphic Original(string path = "Body") => new Graphic { path = path, shader = new Shader("Original") };
    /// <summary>核对 Shader 身份、原路径及实际颜色参数；模型不验证 GPU 混合效果。</summary>
    private static void Tinted(Graphic graphic, string path)
    {
        Check(graphic.path == path && graphic.shader == ShaderDef.Native, "必须使用已注册原生Shader且保留路径");
        Check(graphic.color.r == .46f && graphic.color.g == .20f && graphic.color.b == .68f && graphic.color.a == .85f, "深紫色RGB并使用0.85不透明度");
    }
    /// <summary>正常注册路径采用原生程序，并不改写共享原图。</summary>
    private static void BodyShader()
    {
        Graphic graphic = Original(), original = graphic;
        Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = true }, ref graphic);
        Tinted(graphic, "Body");
        Check(original.shader.name == "Original" && graphic != original, "不应污染共享原图");
    }

    /// <summary>分别模拟 Def 缺失、未注册和错误默认条目；不能仅凭 supported 认定成功。</summary>
    private static void ShaderFallback(bool available, bool registered, bool defaultEntry)
    {
        ShaderDef.Available = available;
        Waylith.Rendering.ShaderRegistry.Registered = registered;
        Waylith.Rendering.ShaderRegistry.ReturnDefault = defaultEntry;
        Check(new ShaderDef().Shader.isSupported, "边界保留late-bound假成功");
        Graphic graphic = Original();
        Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = true }, ref graphic);
        Check(graphic.shader == ShaderDatabase.TransparentPostLight && graphic.path == "Body", "缺失原生资源时应明确回退");
    }

    private static void ShaderRecovery()
    {
        ShaderFallback(true, false, false);
        Waylith.Rendering.ShaderRegistry.Registered = true;
        Graphic graphic = Original();
        Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = true }, ref graphic);
        Tinted(graphic, "Body");
    }

    private static void GelDarkEdge()
    {
        float r = GelShaderMath.sscGelChannel(0, .46f), g = GelShaderMath.sscGelChannel(0, .20f), b = GelShaderMath.sscGelChannel(0, .68f);
        Check(g > .05f && b > r && r > g, "黑线应变成深紫色并保留目标色相");
        Check(b < GelShaderMath.sscGelChannel(.5f, .68f) * .7f, "描边仍应区别于身体中间调");
    }

    private static void GelDetail()
    {
        foreach (float tint in new[] { .20f, .46f, .68f })
            for (int i = 1; i <= 256; i++)
                Check(GelShaderMath.sscGelChannel(i / 256f, tint) > GelShaderMath.sscGelChannel((i - 1) / 256f, tint), "灰度细节不得反转或被压平");
    }

    private static void GelHighlight()
    {
        foreach (float tint in new[] { .20f, .46f, .68f })
        {
            float white = GelShaderMath.sscGelChannel(1, tint);
            Check(white > tint && white - tint < .1f && white <= 1, "亮部只加克制光泽，不洗成白色");
        }
    }

    private static void GelTransparent()
    {
        Check(GelShaderMath.sscGelAlpha(0, .85f, 1) == 0, "透明背景和内部孔洞不能被填满");
        Check(GelShaderMath.sscGelAlpha(1, 0, 1) == 0 && GelShaderMath.sscGelAlpha(1, 1, 0) == 0, "材质与顶点淡出均须生效");
    }

    private static void GelCoverage()
    {
        float opaque = GelShaderMath.sscGelAlpha(1, .85f, 1);
        Check(Math.Abs(opaque - .85f) < 1e-6f && opaque < 1, "主体必须保留指定的85%不透明度");
        for (int i = 1; i <= 256; i++)
        {
            float a = GelShaderMath.sscGelAlpha(i / 256f, .85f, 1);
            Check(a > GelShaderMath.sscGelAlpha((i - 1) / 256f, .85f, 1) && a <= opaque, "边缘Alpha必须连续保留覆盖率");
        }
        Check(Math.Abs(GelShaderMath.sscGelAlpha(.5f, .85f, .5f) - opaque * .25f) < 1e-6f, "保留抗锯齿与顶点透明叠乘");
    }

    private static void GelBounds()
    {
        foreach (float a in new[] { -1f, 0f, .5f, 1f, 2f })
            foreach (float b in new[] { -1f, 0f, .5f, 1f, 2f })
            {
                float color = GelShaderMath.sscGelChannel(a, b), alpha = GelShaderMath.sscGelAlpha(a, b, 1);
                Check(float.IsFinite(color) && color >= 0 && color <= 1 && alpha >= 0 && alpha <= 1, "有界输入不得产生非法输出");
            }
    }

    /// <summary>核对真正随版本加载的定义、片元及 include，防止只编译DLL却遗漏运行资源。</summary>
    private static void ShaderResources(string root)
    {
        string runtime = SscTestPaths.RuntimeRoot(root);
        var def = XDocument.Load(Path.Combine(runtime, "Defs/ShaderDefs/SSC_FullGelBody17.xml")).Root.Element("ShaderDef");
        Check((string)def.Element("defName") == "SSC_FullGelBody17", "原生定义名称必须匹配生产选择器");
        var program = def.Element("shaderProgram");
        Check((string)program.Element("transparent") == "true" && (string)program.Element("depthWrite") == "false", "透明材质不能遮挡后续节点深度");
        string frag = Path.Combine(runtime, "Shaders", (string)program.Element("fragmentShaderFileName"));
        Check(File.Exists(frag) && File.ReadAllText(frag).Contains("sscGelChannel(source.r, tint.r)") && File.ReadAllText(frag).Contains("sscGelAlpha(source.a, tint.a, vColor.a)"), "片元必须使用被数值回归的生产函数");
        Check(File.ReadAllBytes(Path.Combine(runtime, "Shaders/SSC/FullGelMath.glsl")).SequenceEqual(File.ReadAllBytes(Path.Combine(root, "1.7/Shaders/SSC/FullGelMath.glsl"))), "所选运行资源必须与编译测试的数学函数一致");
        Check(!File.ReadAllText(Path.Combine(root, "Sexslavecraft/ThePatches/HarmonyStartUp.cs")).Contains("\"Verse.PawnRenderNode_Head\""), "本轮不扩大头部染色范围");
    }
    /// <summary>节点没有 pawn 字段，只有渲染树持有角色；该形状能捕获旧反射读取失败。</summary>
    private static void SizedApparel()
    {
        Graphic graphic = Original("SizedApparel/Breast");
        var node = new PawnRenderNode { tree = new PawnRenderTree { pawn = new Pawn { FullGel = true } } };
        Harmony_FullGelatinizationBodyTint.SizedApparelWorkerPostfix(node, ref graphic);
        Tinted(graphic, "SizedApparel/Breast");
    }
    /// <summary>覆盖空节点、空树及无角色三种状态，均应保留原图形引用。</summary>
    private static void EmptyNode(PawnRenderNode node)
    {
        Graphic graphic = Original(), original = graphic;
        Harmony_FullGelatinizationBodyTint.SizedApparelWorkerPostfix(node, ref graphic);
        Check(graphic == original, "空节点不应改动图形");
    }
    /// <summary>分别关闭个人状态条件或全局设置，验证准入规则不被 Shader 修复绕过。</summary>
    private static void BodyGuard(bool full, bool enabled)
    {
        SSCMod.settings.enableFullGelatinizedBodyTint = enabled;
        Graphic graphic = Original(), original = graphic;
        Harmony_FullGelatinizationBodyTint.Postfix(new Pawn { FullGel = full }, ref graphic);
        Check(graphic == original, "不满足染色条件应保持原样");
    }
    /// <summary>验证服装变体同样取得透明染色版本，并保留原服装来源。</summary>
    private static void ApparelTint()
    {
        var apparel = new Apparel { Wearer = new Pawn { FullGel = true } };
        var record = new ApparelGraphicRecord(Original("SizedApparel/Clothes"), apparel);
        Harmony_FullGelatinizationBodyTint.SizedApparelApparelPostfix(apparel, ref record, true);
        Tinted(record.graphic, "SizedApparel/Clothes");
        Check(record.sourceApparel == apparel, "保留服装来源");
    }
    /// <summary>普通服装及原图形生成失败时不染色，避免扩大软依赖补丁的作用范围。</summary>
    private static void ApparelGuard()
    {
        var apparel = new Apparel { Wearer = new Pawn { FullGel = true } };
        Graphic original = Original("Clothes");
        var record = new ApparelGraphicRecord(original, apparel);
        Harmony_FullGelatinizationBodyTint.SizedApparelApparelPostfix(apparel, ref record, true);
        Check(record.graphic == original, "普通服装不染色");
        record = new ApparelGraphicRecord(Original("SizedApparel/Clothes"), apparel); original = record.graphic;
        Harmony_FullGelatinizationBodyTint.SizedApparelApparelPostfix(apparel, ref record, false);
        Check(record.graphic == original, "失败图形不染色");
    }
    /// <summary>运行生产 PowerShell 函数的签名夹具，检查退出码和完整执行标记。</summary>
    private static void StateValidator(string root)
    {
        // 使用独立进程隔离 PowerShell 函数和类型表；ArgumentList 保留含空格路径的真实参数边界。
        var start = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "-NoProfile", "-File", Path.Combine(root, "Tests/Gelatinization/Test-HarmonyState.ps1"), "-RepositoryRoot", root }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start);
        // 捕获子检查的诊断信息用于单项失败报告，主进程只输出自己的一条统一结果摘要。
        string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Check(process.ExitCode == 0, "PowerShell状态签名回归失败：" + output + error);
        Check(output.Contains("State signature checks: 10 passed."), "状态签名检查未完整执行：" + output);
    }
    /// <summary>以异常把断言失败交回 Run 计数，不在辅助函数中提前结束整个套件。</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
