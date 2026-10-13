// AI 辅助生成
// 仅模拟外部接口：字体为近似测量，健康/翻译与 Scribe 是字典模型，不运行 Unity 或跨引用恢复。
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float yMax => y + height;
        public Rect ContractedBy(float margin) => new Rect(x + margin, y + margin, width - margin * 2, height - margin * 2);
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
    }
    public enum TextAnchor { UpperLeft, MiddleRight }
    public static class GUI { public static bool enabled = true; public static Color color = Color.white; }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
}

namespace Verse
{
    public enum GameFont { Small, Medium }
    public static class Text
    {
        public static GameFont Font = GameFont.Small;
        public static TextAnchor Anchor;
        public static bool WordWrap = true;
        // 近似字体测量仅用于区域容纳检查，不宣称等同 Unity 的字形、裁剪与输入行为。
        public static float CalcHeight(string text, float width) => Math.Max(1, (float)Math.Ceiling((text ?? "").Sum(c => c > 255 ? 13f : 7f) / width)) * (Font == GameFont.Small ? 22 : 30);
    }
    public record DrawItem(string Kind, Rect Rect, string Text = "", bool Disabled = false);
    public static class Widgets
    {
        public static List<DrawItem> Items = new List<DrawItem>();
        public static bool Click, Throw;
        public static Action BeforeCheckbox;
        public static void DrawMenuSection(Rect rect) => Items.Add(new DrawItem("section", rect));
        public static void Label(Rect rect, string label)
        {
            if (Throw) throw new InvalidOperationException("test draw failure");
            Items.Add(new DrawItem("label", rect, label));
        }
        public static Rect FillableBar(Rect rect, float value)
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new Exception("Invalid bar fraction");
            Items.Add(new DrawItem("bar", rect, value.ToString())); return rect;
        }
        /// <summary>按已查证的 disabled 条件控制点击，允许用例在提交前替换角色或缰绳。</summary>
        public static void CheckboxLabeled(Rect rect, string label, ref bool value, bool disabled = false)
        {
            Items.Add(new DrawItem("checkbox", rect, label, disabled));
            BeforeCheckbox?.Invoke();
            if (Click && !disabled) value = !value;
        }
    }
    public static class TooltipHandler
    {
        public static List<Func<string>> Getters = new List<Func<string>>();
        public static void TipRegion(Rect rect, string text) => Widgets.Items.Add(new DrawItem("tooltip", rect, text));
        // 模拟延迟提示注册，不在绘制事件调用 getter；用例显式模拟悬停。
        public static void TipRegion(Rect rect, Func<string> textGetter, int uniqueId) => Getters.Add(textGetter);
    }
    public static class Translation
    {
        public static Dictionary<string, string> Entries = new Dictionary<string, string>();
        public static string Translate(this string key, params object[] args) => string.Format(Entries.TryGetValue(key, out string text) ? text : key, args);
        public static string ToStringPercent(this float value) => value.ToString("P0");
        public static bool DestroyedOrNull(this Pawn pawn) => pawn == null || pawn.Destroyed;
        public static bool IsHashIntervalTick(this Pawn pawn, int interval) => false;
    }
    public class Pawn
    {
        public bool Destroyed, Discarded, Dead, Downed;
        public string LabelShortCap = "Pawn";
        public RimWorld.Faction Faction = RimWorld.Faction.OfPlayer;
        public RaceProperties RaceProps = new RaceProperties();
        public Health health = new Health();
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class RaceProperties { public bool Humanlike = true; }
    public class HediffDef
    {
        public string defName;
        public Type hediffClass;
        public float maxSeverity = 100;
        public List<HediffStage> stages = new List<HediffStage>();
    }
    public class HediffStage { public string label; public float minSeverity; }
    public struct DamageInfo { }
    public class Gizmo { }
    public class Hediff
    {
        public HediffDef def;
        public Pawn pawn;
        private float severity;
        public int TooltipReads;
        public float Severity { get => severity; set => severity = Math.Clamp(value, 0, def.maxSeverity); }
        public int CurStageIndex => Math.Max(0, def.stages.FindLastIndex(s => severity >= s.minSeverity));
        public virtual HediffStage CurStage => def.stages.Count == 0 ? null : def.stages[CurStageIndex];
        public virtual bool ShouldRemove => severity <= 0;
        public virtual string SeverityLabel => null;
        public virtual string LabelInBrackets => CurStage?.label;
        public virtual string LabelBase => "Bridle";
        public virtual string Description => "";
        public virtual string TipStringExtra => "";
        public virtual string GetTooltip(Pawn pawn, bool debug) { TooltipReads++; return "Native stage detail: " + CurStage?.label; }
        public virtual void ExposeData() { }
        public virtual IEnumerable<Gizmo> GetGizmos() { yield return NativeGizmo; }
        public static readonly Gizmo NativeGizmo = new Gizmo();
        public virtual void PostAdd(DamageInfo? info) { }
        public virtual void PostRemoved() { }
        public virtual void Tick() { }
    }
    public class HediffWithComps : Hediff { }
    public class Health
    {
        public HediffSet hediffSet = new HediffSet();
        public int Adds, Removes, Notifications;
        public void AddHediff(Hediff record) { hediffSet.hediffs.Add(record); Adds++; }
        public void RemoveHediff(Hediff record) { hediffSet.hediffs.Remove(record); Removes++; }
        public void Notify_HediffChanged(Hediff record) => Notifications++;
    }
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) != null;
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn)
        {
            var result = (Hediff)Activator.CreateInstance(def.hediffClass); result.def = def; result.pawn = pawn; return result;
        }
    }
    public enum LoadSaveMode { Inactive, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values
    {
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Data.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
            else Data[key] = value;
        }
    }
    public enum LookMode { Reference }
    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> values, string key, LookMode mode) { }
    }
}
namespace RimWorld { public class Faction { public static Faction OfPlayer = new Faction(); } }

namespace SexSlaveCraft
{
    public enum PawnIdentity { Unset, Master, Slave }
    public class CompSexSlaveTraining { public PawnIdentity pawnIdentity = PawnIdentity.Master; public int masterProgressionVersion = 1; }
    public static class SSCIdentityUtility { public static bool IsMaster(Pawn pawn) => pawn?.Training?.pawnIdentity == PawnIdentity.Master; }
    public static class SSCBondUtility
    {
        // 绑定查询作为外部接口，仅返回已有缰绳；不在测试中重写面板编辑准入。
        public static Hediff_BridleOfSexSlave GetBridle(Pawn pawn) => pawn?.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.BridleOfSexSlave) as Hediff_BridleOfSexSlave;
    }
    public static class SSCMasterBondUtility
    {
        public const float DamageShareRatio = .7f;
        public static void RefreshMasterEmpowerment(Pawn pawn) { }
        public static string GetEmpowermentSummary(Pawn pawn) => "";
    }
    public static class Strings
    {
        public static string Bridle_LabelWithNames(string label, string names) => label + names;
        public static string Bridle_DescSlaves(string names) => names;
    }
    public static class SSCDefOf
    {
        public static HediffDef SSC_Hediff_MasterProgression = new HediffDef { defName = SSCMasterProgressionUtility.DefName, hediffClass = typeof(Hediff_MasterProgression) };
        public static HediffDef BridleOfSexSlave = new HediffDef { hediffClass = typeof(Hediff_BridleOfSexSlave) };
        public static HediffDef SSC_PersonalityExcreted_Done = new HediffDef(), SSC_MasterBondEmpowerment = new HediffDef();
    }

    public partial class ITab_SexSlaveTraining
    {
        // 主文件提供的共同区块几何作为界面边界；用例另检查它与实际主文件一致。
        private const float SectionPadding = 12f, SectionTitleHeight = 28f;
        public Pawn SelPawn;
        public record Probe(string Stage, string Experience, string Benefits, string Progress, float Fraction, float Height, bool CanEdit);
        public Probe Read(Pawn pawn, float width = 322)
        {
            var v = BuildMasterPanelView(pawn, pawn.Training, width);
            return new Probe(v.Stage, v.Experience, v.Benefits, v.Progress, v.ProgressFraction, v.SectionHeight, v.CanEditShare);
        }
        /// <summary>执行真实绘制方法；不复制布局或点击提交算法。</summary>
        public float Draw(Pawn pawn, float width = 322)
        {
            SelPawn = pawn;
            Widgets.Items.Clear();
            TooltipHandler.Getters.Clear();
            var view = BuildMasterPanelView(pawn, pawn.Training, width);
            DrawMasterPanel(new Rect(0, 0, width, view.SectionHeight), view);
            return view.SectionHeight;
        }
        public Action<bool> CaptureShareAction(Pawn pawn)
        {
            SelPawn = pawn;
            var view = BuildMasterPanelView(pawn, pawn.Training, 322);
            return value => TrySetMasterDamageSharing(view, value);
        }
    }
}
