// Only game boundaries are modeled. The process classes, components and tint patch
// are linked unchanged from production. Real 1.7 compilation is checked separately.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 仅保留颜色、Shader 身份与数学计算；没有 GPU 资源或真实材质创建。
namespace UnityEngine
{
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public class Shader
    {
        public string name; public bool isSupported = true;
        // 模拟实际 internal getter，仅提供已经解析的管线状态，不依赖 Shader 名字猜测。
        internal Waylith.Rendering.Shader EngineShader { get; set; } = new Waylith.Rendering.Shader();
        public Shader(string name) { this.name = name; }
    }
    public class Texture { public string name; public Texture(string name) { this.name = name; } }
    public class Material
    {
        public static int Created;
        public Shader shader; public Texture mainTexture; public Color color;
        public int renderQueue = 2000;
        public bool HasMaskSlot;
        private Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        private Dictionary<string, float> floats = new Dictionary<string, float>();
        public Material(Shader shader) { Created++; this.shader = shader; color = new Color(1, 1, 1, 1); }
        public Material(Material source) : this(source.shader) { CopyPropertiesFromMaterial(source); renderQueue = source.renderQueue; }
        // 与真实边界一样只复制属性，不靠此方法代替 shader/队列选择。
        public void CopyPropertiesFromMaterial(Material source)
        {
            mainTexture = source.mainTexture; color = source.color; HasMaskSlot = source.HasMaskSlot;
            textures = new Dictionary<string, Texture>(source.textures); floats = new Dictionary<string, float>(source.floats);
        }
        public bool HasProperty(string name) => name == "_MaskTex" && HasMaskSlot;
        public Texture GetTexture(string name) => textures.TryGetValue(name, out var texture) ? texture : null;
        public void SetTexture(string name, Texture texture) { textures[name] = texture; }
        public void SetFloat(string name, float value) { floats[name] = value; }
        public float GetFloat(string name) => floats.TryGetValue(name, out var value) ? value : 0;
    }
    public static class Mathf
    {
        public static float Clamp01(float v) => Math.Clamp(v, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
namespace HarmonyLib
{
    // Kept for the pre-fix source mutation check: actual 1.7 nodes have no pawn field.
    public static class AccessTools
    {
        public static bool DisableFa, BadFaShape;
        public static Type TypeByName(string name) => name.StartsWith("FacialAnimation.") && DisableFa ? null :
            BadFaShape && name == "FacialAnimation.NLFacialAnimationPartNode" ? typeof(BadFaNode) : typeof(AccessTools).Assembly.GetType(name);
        public static MethodInfo Method(Type type, string name, Type[] parameters) => type?.GetMethod(name, parameters);
        public static FieldInfo Field(Type type, string name) => type?.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
    public class BadFaNode { public object controller; public string layerType; }
    public class HarmonyMethod { public Type Type; public string Name; public HarmonyMethod(Type type, string name) { Type = type; Name = name; } }
    public class Harmony
    {
        public List<MethodInfo> Patched = new List<MethodInfo>();
        public void Patch(MethodInfo original, HarmonyMethod postfix) { Patched.Add(original); }
    }
}
// 原版标签仅用于区分臂与腿；服装记录按值保存图形及来源，供生产补丁验证重建行为。
namespace RimWorld
{
    public static class BodyPartTagDefOf { public static readonly object ManipulationLimbCore = new object(), MovingLimbCore = new object(); }
    public static class HediffDefOf { public static readonly Verse.HediffDef SurgicalCut = new Verse.HediffDef("SurgicalCut"); }
    public class Apparel { public Verse.Pawn Wearer; }
    public static class InvisibilityUtility { public static bool IsPsychologicallyInvisible(this Verse.Pawn pawn) => pawn.Invisible; }
    public struct ApparelGraphicRecord
    {
        public Verse.Graphic graphic; public Apparel sourceApparel;
        public ApparelGraphicRecord(Verse.Graphic graphic, Apparel apparel) { this.graphic = graphic; sourceApparel = apparel; }
    }
}
namespace Verse
{
    // 每个角色拥有独立健康列表和事件账本；FullGel、Destroyed、Guaranteed 只表示测试服务状态。
    public class Pawn
    {
        public bool Dead, FullGel, Destroyed, Guaranteed, Invisible;
        public Faction Faction = Faction.OfPlayer;
        public Pawn_NeedsTracker needs = new Pawn_NeedsTracker();
        public Pawn_HealthTracker health;
        public List<string> Events = new List<string>();
        public Pawn() { health = new Pawn_HealthTracker(this); }
    }
    // 默认腐化度为 0.7，满足局部适应每次增长的条件，不对真实 Need 系统做模拟验收。
    public class Pawn_NeedsTracker { public List<Need> AllNeeds = new List<Need> { new Need() }; }
    public class Need { public NeedDef def = new NeedDef(); public float CurLevelPercentage = .7f; }
    public class NeedDef { public string defName = "Corruption"; }
    // 健康边界刻意保留移除后 parent.pawn 仍存在的行为，这是旧缺陷能继续结算的关键。
    public class Pawn_HealthTracker
    {
        private readonly Pawn pawn;
        public HediffSet hediffSet = new HediffSet();
        public Pawn_HealthTracker(Pawn pawn) { this.pawn = pawn; }
        // The actual 1.7 contract: remove does not clear h.pawn or stop an in-flight comp loop.
        public void RemoveHediff(Hediff h) { hediffSet.hediffs.Remove(h); }
        // 每一次添加都记事件，使后续正确结果无法掩盖先前已经发生的错误结算。
        public void AddHediff(Hediff h)
        {
            h.pawn = pawn; hediffSet.hediffs.Add(h);
            pawn.Events.Add(h is Hediff_MissingPart ? "failure" : "success");
        }
        // 通知在夹具中无副作用；界面缓存、疾病联动及其他模组的通知处理均不属于该模型。
        public void Notify_HediffChanged(Hediff h) { }
    }
    // 缺肢可由显式边界标记或实际 MissingPart 对象表示，分别覆盖无效部位与失败结果。
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        public bool Missing;
        public bool HasHead = true;
        public bool PartIsMissing(BodyPartRecord part) => Missing || hediffs.Any(h => h is Hediff_MissingPart && h.Part == part);
    }
    // 保留生产临时类需要的虚方法和数据引用；存档方法仅供链接，不实施序列化测试。
    public class Hediff
    {
        public Pawn pawn; public BodyPartRecord Part; public HediffDef def; public float Severity;
        public virtual void PostAdd(DamageInfo? damage) { }
        public virtual void PostTick() { }
        public virtual void ExposeData() { }
        public virtual IEnumerable<Gizmo> GetGizmos() { yield break; }
    }
    public class HediffWithComps : Hediff
    {
        public List<HediffComp> comps = new List<HediffComp>();
        // 按顺序遍历同一 comps 列表，绝不因父状态已从健康列表移除而提前退出。
        // 若模型自行跳过剩余组件，会掩盖生产实现缺少有效性检查的原始故障。
        public void PostTickInterval(int delta)
        {
            float adjustment = 0;
            foreach (HediffComp comp in comps) comp.CompPostTickInterval(ref adjustment, delta);
        }
    }
    // 以下类型只承载组件会读写的属性、部位与开发按钮数据，不代替生产组件的结算逻辑。
    public class Hediff_MissingPart : Hediff { public HediffDef lastInjury; public bool IsFresh; }
    public class HediffDef { public string defName; public HediffDef(string name) { defName = name; } }
    public class BodyPartRecord { public BodyPartDef def = new BodyPartDef(); }
    public class BodyPartDef { public List<object> tags = new List<object>(); }
    public class HediffCompProperties { public Type compClass; }
    public class HediffComp
    {
        public Hediff parent; public HediffCompProperties props;
        public virtual void CompPostTickInterval(ref float adjustment, int delta) { }
        public virtual string CompDebugString() => "";
        public virtual string CompTipStringExtra => "";
    }
    public class Gizmo { }
    public class Command_Action : Gizmo { public string defaultLabel, defaultDesc; public Action action; }
    public class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public static class Prefs { public static bool DevMode = true; }
    public struct DamageInfo { }
    public static class Scribe_Values { public static void Look(ref float v, string key, float defaultValue) { } }
    // 每次增长都验证抽取值位于 XML 上下界内，防止用不可能的数值制造双阈值故障。
    public struct FloatRange
    {
        public float min, max;
        public FloatRange(float min, float max) { this.min = min; this.max = max; }
        public float RandomInRange
        {
            get
            {
                // 计数用于验证已结束过程不会继续抽随机数；极小误差容限只吸收浮点计算偏差。
                float draw = Rand.RangeDraw(this); Rand.Draws++;
                if (draw < min - .000001f || draw > max + .000001f) throw new Exception($"非法测试随机数：{draw} 不在 {min}..{max}");
                return draw;
            }
        }
    }
    // 测试逐项重置抽取策略；连续概率命中只是合法路径之一，不是实际游戏随机分布。
    public static class Rand
    {
        public static Func<FloatRange, float> RangeDraw;
        public static int Draws; public static bool Hit;
        public static bool Chance(float chance) { Draws++; return Hit && chance > 0; }
    }
    // 只提供用例会请求的定义；ShaderTypeDef 故意给出不可靠的 supported 信号以重现 1.7 边界。
    public static class DefDatabase<T> where T : class
    {
        public static T GetNamedSilentFail(string name) =>
            (typeof(T) == typeof(ShaderTypeDef) ? (object)new ShaderTypeDef() :
             typeof(T) == typeof(ShaderDef) ? ShaderDef.Available && (name != "SSC_FullGelHeadMasked17" || ShaderDef.MaskedAvailable) && (!name.Contains("Depth17") || ShaderDef.DepthAvailable) ? new ShaderDef { defName = name } : null : new HediffDef(name)) as T;
    }
    public static class HediffMaker
    {
        // 通过实际定义名区分稳定缺肢与成功义肢，结果次数由健康列表的添加入口记录。
        public static Hediff MakeHediff(HediffDef def, Pawn pawn, BodyPartRecord part)
        {
            Hediff h = def.defName == "MissingBodyPart" ? new Hediff_MissingPart() : new Hediff();
            h.def = def; h.pawn = pawn; h.Part = part; return h;
        }
    }
    public static class Log { public static void Warning(string text) { } }
    // 翻译只返回键名，百分比只用于诊断；本套件不据此声明四语显示或本地化格式通过。
    public static class Extensions
    {
        public static string ToStringPercent(this float v) => (v * 100).ToString("F1") + "%";
        public static string Translate(this string key, params object[] args) => key;
    }
    // 获取染色版本返回独立对象，便于断言原图未被改写；真实图形缓存和材质复用交由游戏验证。
    public class Graphic
    {
        public UnityEngine.Material MatSingle;
        public string path; public UnityEngine.Shader shader; public UnityEngine.Color color;
        public static int ColoredRequests; public static bool ReturnNull;
        public Graphic GetColoredVersion(UnityEngine.Shader shader, UnityEngine.Color one, UnityEngine.Color two)
        {
            ColoredRequests++;
            // 真实 GraphicDatabase 在非主线程缓存未命中时记录错误并返回 null。
            if (!UnityData.IsInMainThread) throw new InvalidOperationException("Graphic requested off main thread");
            return ReturnNull ? null : new Graphic { path = path, shader = shader, color = one };
        }
    }
    // 按实际 1.7 形状只在树上设置 pawn，节点不增加方便测试但真实不存在的同名字段。
    public class PawnRenderNode { public PawnRenderTree tree; }
    public class PawnRenderNode_Head : PawnRenderNode { }
    public class PawnRenderNode_Hair : PawnRenderNode { }
    public class PawnRenderNode_Beard : PawnRenderNode { }
    public class PawnRenderNode_Apparel : PawnRenderNode { }
    public class PawnRenderNodeWorker
    {
        public UnityEngine.Material GetFinalizedMaterial(PawnRenderNode node, PawnDrawParms parms) => null;
    }
    public enum RotDrawMode { Fresh, Rotting, Dessicated }
    [Flags] public enum PawnRenderFlags { None = 0, Invisible = 1 }
    public struct PawnDrawParms { public Pawn pawn; public RotDrawMode rotDrawMode; public bool Statue, Portrait; public PawnRenderFlags flags; }
    public class PawnRenderTree { public Pawn pawn; }
    public static class UnityData { public static bool IsInMainThread = true; }
    // Deliberately non-null and supported, matching the audited Waylith late-bound contract.
    public class ShaderTypeDef { public string defName = "SSC_FullGelBody", shaderPath = "Sexslavecraft/SSC_FullGelBody"; public UnityEngine.Shader Shader => new UnityEngine.Shader("UnregisteredDefault"); }
    // 与真实注册表区分：即使未注册，属性仍返回 supported 对象，用来捕获伪成功。
    public class ShaderDef
    {
        public static bool Available = true;
        public static bool MaskedAvailable = true;
        public static bool DepthAvailable = true;
        public static readonly UnityEngine.Shader Native = new UnityEngine.Shader("SSC_FullGelBody17");
        public static readonly UnityEngine.Shader Masked = new UnityEngine.Shader("SSC_FullGelHeadMasked17");
        public static readonly UnityEngine.Shader Depth = new UnityEngine.Shader("SSC_FullGelHeadDepth17") { EngineShader = new Waylith.Rendering.Shader { DepthWrite = true } };
        public static readonly UnityEngine.Shader MaskedDepth = new UnityEngine.Shader("SSC_FullGelHeadMaskedDepth17") { EngineShader = new Waylith.Rendering.Shader { DepthWrite = true } };
        public string defName;
        public UnityEngine.Shader Shader => defName == "SSC_FullGelHeadDepth17" ? Depth : defName == "SSC_FullGelHeadMaskedDepth17" ? MaskedDepth : defName == "SSC_FullGelHeadMasked17" ? Masked : Native;
    }
    // 内置透明 Shader 使用稳定对象身份，使断言能区分预期 Shader 与伪成功的自定义对象。
    public static class ShaderDatabase { public static readonly UnityEngine.Shader TransparentPostLight = new UnityEngine.Shader("TransparentPostLight"); }
}
namespace Waylith.Rendering
{
    public class Shader { public bool DepthWrite; }
    public static class ShaderRegistry
    {
        public static readonly object Default = new object();
        public static bool Registered = true, MaskedRegistered = true, ReturnDefault;
        private static readonly object Native = new object();
        public static bool TryResolve(string name, out object shader)
        {
            shader = ReturnDefault ? Default : Native;
            return name == "SSC_FullGelBody17" ? Registered : (name == "SSC_FullGelHeadDepth17" || name == "SSC_FullGelHeadMaskedDepth17") ? Registered : name == "SSC_FullGelHeadMasked17" && MaskedRegistered;
        }
    }
}
// 只保留实际 FA 节点公开字段与控制器身份。动画仍由用例切换材质/遮罩模拟，不复制 FA 实现。
namespace FacialAnimation
{
    public enum NLFacialAnimationLayerType { Basic, Cover, Highlight, Bottom, L_Basic, R_Basic, L_Option, R_Option }
    public class NLFacialAnimationPartNode : Verse.PawnRenderNode { public object controller; public NLFacialAnimationLayerType layerType; }
    public class HeadControllerComp { }
    public class SkinControllerComp { }
    public class LidControllerComp { }
    public class EyeballControllerComp { }
    public class BrowControllerComp { }
    public class MouthControllerComp { }
    public class EmotionControllerComp { }
    public class LidOptionControllerComp { }
    public class RaceHeadControllerComp : HeadControllerComp { }
    public static class GraphicPool
    {
        public static Verse.Graphic dummyGraphic = new Verse.Graphic { MatSingle = new UnityEngine.Material(new UnityEngine.Shader("Dummy")) };
    }
}
namespace SexSlaveCraft
{
    // 设置及日志是最小服务边界，测试只核对染色是否执行，不核对真实设置持久化和日志界面。
    public class SSCSettings { public bool enableFullGelatinizedBodyTint; }
    public static class SSCMod { public static SSCSettings settings; }
    public static class SSCLog { public static void Important(string text) { } public static void WarningImportant(string text) { } }
    public static class FullGelatinizationUtility
    {
        // Model only the helper boundary. All four components are production code;
        // the original helper's formula/ranges and 1.7 traversal were audited separately.
        public static bool ShouldTintFullGelGraphic(Verse.Pawn pawn) => pawn?.FullGel == true && SSCMod.settings?.enableFullGelatinizedBodyTint == true;
        // 固定为合法的非必定成功权重 0.95；完整准备度计算已在先前实际源码审查中独立核对。
        public static float GetSmoothedSuccessWeight(Verse.Pawn pawn) => .95f;
        // 保留生产工具的区间选择及插值公式，使自然竞赛中的确定性增量仍属于合法输入。
        public static Verse.FloatRange GetAdaptationGainRange(Verse.Pawn pawn, Verse.FloatRange guaranteedRange, Verse.FloatRange baseRange, out bool guaranteed)
        {
            guaranteed = pawn.Guaranteed;
            if (guaranteed) return guaranteedRange;
            float gain = UnityEngine.Mathf.Lerp(baseRange.min, baseRange.max, .95f);
            return new Verse.FloatRange(gain, gain);
        }
        // 准备度降低失败增长，必定成功模式再乘 0.35；使用与生产工具相同的数值关系。
        public static Verse.FloatRange GetFailureSeverityGainRange(Verse.Pawn pawn, Verse.FloatRange range)
        {
            float scale = pawn.Guaranteed ? .35f : 1;
            return new Verse.FloatRange(UnityEngine.Mathf.Lerp(range.max, range.min, .95f) * scale,
                UnityEngine.Mathf.Lerp(range.max + .02f, range.max * .6f, .95f) * scale);
        }
        // 成功服务只清理临时全胶状态并记录结果，不模拟完整身体重构、图形刷新或消息发送。
        public static void CompleteSuccess(Verse.Pawn pawn)
        {
            foreach (var h in pawn.health.hediffSet.hediffs.OfType<Hediff_FullGelatinizationTemporary>().ToList()) pawn.health.RemoveHediff(h);
            pawn.FullGel = true; pawn.Events.Add("success");
        }
        // 用可观察标记替代真正的 DeSpawn/WorldPawns.Discard，避免测试执行不可逆游戏操作。
        public static void DestroyPawnCompletely(Verse.Pawn pawn) { pawn.Destroyed = true; pawn.Events.Add("failure"); }
    }
}

// 实际 SA worker 继承原版最终材质方法；GetGraphic 故意抛异常捕获染色补丁的重复调用。
namespace SizedApparel
{
    public class SizedApparelRenderNodeWorker : Verse.PawnRenderNodeWorker
    {
        public Verse.Graphic GetGraphic() => throw new InvalidOperationException("Do not re-enter SA GetGraphic");
    }
    public class DerivedWorker : SizedApparelRenderNodeWorker { }
}
