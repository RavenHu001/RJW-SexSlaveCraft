// AI 辅助生成：近似字体和鼠标输入、记录真实生产区块的绘制矩形；不代表 Waylith 实机运行。
using SexSlaveCraft;
using Verse;

namespace UnityEngine
{
    public struct Rect(float x, float y, float width, float height)
    {
        public float x = x, y = y, width = width, height = height;
        public float xMax => x + width;
        public float yMax => y + height;
        public Rect ContractedBy(float amount) => new(x + amount, y + amount, width - amount * 2, height - amount * 2);
    }
    public record struct Color(float r, float g, float b, float a)
    { public static Color white => new(1, 1, 1, 1); }
    public enum TextAnchor { UpperLeft, MiddleCenter }
    public static class GUI { public static bool enabled = true; public static Color color = Color.white; }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
    }
}
namespace RimWorld
{
    public static class BodyPartDefOf { public static readonly Verse.BodyPartDef Torso = new(); }
    public static class MessageTypeDefOf { public static readonly object RejectInput = new(); }
}
namespace Verse
{
    using UnityEngine;
    public enum GameFont { Small, Medium }
    public enum LoadSaveMode { Inactive, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values { public static void Look(ref bool value, string key, bool fallback) { } }
    public class DefModExtension { }
    public class BodyPartDef { }
    public class BodyPartRecord { public BodyPartDef def; }
    public struct DamageInfo { }
    public class StatDef { public string defName, label; public string LabelCap => label ?? defName; }
    public class StatModifier { public StatDef stat; public float value; }
    public class HediffStage { public float minSeverity, fertilityFactor = 1; public string label; public List<StatModifier> statFactors; }
    public class HediffDef
    {
        public string defName;
        public Type hediffClass;
        public List<HediffStage> stages = new();
        public SSCUterusDevelopmentExtension Extension;
        public T GetModExtension<T>() where T : class => Extension as T;
    }
    public static class DefDatabase<T> where T : class
    {
        public static T Definition;
        public static T GetNamedSilentFail(string name) => Definition;
    }
    public class Hediff
    {
        public HediffDef def;
        public Pawn pawn;
        public BodyPartRecord Part;
        public object Comp;
        public virtual float Severity { get; set; }
        public virtual HediffStage CurStage => def.stages.Last(stage => Severity >= stage.minSeverity);
        public T TryGetComp<T>() where T : class => Comp as T;
        public virtual void PostMake() { }
        public virtual void PostAdd(DamageInfo? info) { }
        public virtual bool TryMergeWith(Hediff other) => false;
        public virtual void ExposeData() { }
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn, BodyPartRecord part)
        {
            var item = (Hediff)Activator.CreateInstance(def.hediffClass);
            item.def = def; item.pawn = pawn; item.Part = part; item.PostMake(); return item;
        }
    }
    public sealed class HediffSet(Pawn pawn)
    {
        public readonly List<Hediff> hediffs = new();
        public IEnumerable<BodyPartRecord> GetNotMissingParts() => new[] { pawn.Torso };
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
    }
    public sealed class Health(Pawn pawn)
    {
        public HediffSet hediffSet = new(pawn);
        public int Notifications;
        public void Notify_HediffChanged(Hediff value) => Notifications++;
        public void RemoveHediff(Hediff item) => hediffSet.hediffs.Remove(item);
        public void AddHediff(Hediff item, BodyPartRecord part) { item.Part = part; hediffSet.hediffs.Add(item); item.PostAdd(null); }
    }
    public sealed class Pawn
    {
        public Health health;
        public bool Pregnant;
        public CompSexSlaveTraining Training;
        public BodyPartRecord Torso = new() { def = RimWorld.BodyPartDefOf.Torso };
        public Pawn() { health = new(this); Training = new(); }
        public T GetComp<T>() where T : class => Training as T;
    }
    public static class Text
    {
        public static GameFont Font;
        public static TextAnchor Anchor;
        public static bool WordWrap;
        public static float WidthScale = 1;
        public static float LineHeight => Font == GameFont.Small ? 20 : 28;
        /// <summary>中英文等宽近似及换行；实际游戏字体仍需实机验收。</summary>
        public static float CalcHeight(string text, float width) => (float)text.Split('\n')
            .Sum(line => Math.Max(1, Math.Ceiling(line.Length * 12 * WidthScale / Math.Max(1, width))) * LineHeight);
    }
    public record Draw(string Kind, string Label, Rect Rect, bool Disabled = false);
    public static class Widgets
    {
        public static readonly List<Draw> Drawn = new();
        public static string Click;
        public static bool IgnoreDisabled, ThrowLabel;
        public static Action BeforeClick;
        public static void DrawMenuSection(Rect rect) => Drawn.Add(new("section", "", rect));
        public static void Label(Rect rect, string label)
        { if (ThrowLabel) throw new InvalidOperationException("UI boundary"); Drawn.Add(new("label", label, rect)); }
        public static void CheckboxLabeled(Rect rect, string label, ref bool value, bool disabled)
        {
            Drawn.Add(new("checkbox", label, rect, disabled));
            if (Click != label) return;
            BeforeClick?.Invoke();
            if (!disabled || IgnoreDisabled) value = !value;
            Click = null;
        }
        public static bool ButtonText(Rect rect, string label, bool active = true) => false;
        public static void DrawBoxSolid(Rect rect, Color color) { }
    }
    public static class TooltipHandler
    { public static void TipRegion(Rect rect, string tip) => Widgets.Drawn.Add(new("tip", tip, rect)); }
    public static class Messages
    {
        public static readonly List<string> Rejections = new();
        public static void Message(string reason, Pawn pawn, object type, bool historical) => Rejections.Add(reason);
    }
    /// <summary>以真实语言 XML 提供键值和参数格式化；丢键直接失败，不用键名充当可见文本。</summary>
    public static class Translator
    {
        public static Dictionary<string, string> Values = new();
        public static void Load(string repo, string language)
        {
            Values = new();
            foreach (string file in new[] { "SSC_Keys.xml", "SSC_BodyManagement.xml", "SSC_UterusDevelopment.xml" })
                foreach (var element in System.Xml.Linq.XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed", file)).Root.Elements())
                    Values.Add(element.Name.LocalName, element.Value.Replace("\\n", "\n"));
        }
        public static string Translate(this string key, params object[] arguments)
            => string.Format(System.Globalization.CultureInfo.CurrentCulture, Values[key], arguments);
    }
}
namespace rjw
{
    public static class PawnExtensions { public static bool IsPregnant(Verse.Pawn pawn, bool visible) => pawn.Pregnant; }
}
namespace SexSlaveCraft
{
    public sealed class CompSexSlaveTraining { public bool milkProductionEnabled = true; }
    public static class SSCDefOf { public static readonly Verse.HediffDef SSC_Lactating_SubState = new(); }
    // 与原 Strings 入口相同的单键转发；实际四语资源从磁盘加载，不在宿主复制译文。
    public static class Strings { public static string ITab_MilkProductionToggle => "SSC_ITab_MilkProductionToggle".Translate(); }
    // 泌乳标签及孕期因子属于外部组件边界，生产实现另由原生／泌乳专项验证。
    public sealed class HediffComp_PermanentLactating { public string CompLabelInBracketsExtra = "乳汁充盈度 53%"; }
    public static class SSCUterusPregnancyUtility { public const float DurationFactor = .8f; }
    public partial class ITab_SexSlaveTraining
    {
        private const float SectionPadding = 12, SectionTitleHeight = 28;
        public Verse.Pawn SelPawn;
        /// <summary>仅开放私有生产区块供测试调用，不另写布局或状态判断。</summary>
        internal (float Height, Action Draw) Capture(Verse.Pawn pawn, float width)
        {
            SelPawn = pawn;
            var view = BuildBodyManagementView(pawn, pawn.Training, width);
            return (view.Height, () => DrawBodyManagementSection(new UnityEngine.Rect(0, 0, width, view.Height), view));
        }
    }
}
