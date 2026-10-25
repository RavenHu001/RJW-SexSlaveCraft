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
        public float xMax => x + width;
        public Rect ContractedBy(float margin) => new Rect(x + margin, y + margin, width - margin * 2, height - margin * 2);
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color operator *(Color a, Color b) => new(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);
    }
    public enum TextAnchor { UpperLeft, MiddleRight, MiddleCenter }
    public static class GUI { public static bool enabled = true; public static Color color = Color.white; }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
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
    public static partial class Widgets
    {
        public static List<DrawItem> Items = new List<DrawItem>();
        public static bool Click, Throw;
        public static Action BeforeCheckbox;
        public static void DrawMenuSection(Rect rect) => Items.Add(new DrawItem("section", rect));
        public static void Label(Rect rect, string label)
        {
            if (Throw) throw new InvalidOperationException("test draw failure");
            Items.Add(new DrawItem("label", RomanceUiOrigins.Transform(rect), label));
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
        public Pawn BoundMaster;
        public bool Destroyed, Discarded, Dead, Downed;
        public string LabelShortCap = "Pawn";
        public string LabelShort = "Pawn";
        public RimWorld.Faction Faction = RimWorld.Faction.OfPlayer;
        public RaceProperties RaceProps = new RaceProperties();
        public Health health = new Health();
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class RaceProperties { public bool Humanlike = true; }
    public class HediffDef
    {
        public string defName, label, description, extraTooltip;
        public Type hediffClass;
        public float maxSeverity = 100;
        public List<HediffStage> stages = new List<HediffStage>();
    }
    public class HediffStage { public string label, extraTooltip; public float minSeverity; }
    public struct DamageInfo { }
    public class Gizmo { }
    public class Hediff
    {
        public HediffDef def;
        public Pawn pawn;
        private float severity;
        public int TooltipReads;
        // 模拟已取证的跨阶段健康通知，供用例在 setter 返回前替换人格记录。
        public float Severity
        {
            get => severity;
            set
            {
                int before = CurStageIndex;
                severity = Math.Clamp(value, 0, def.maxSeverity);
                if (before != CurStageIndex && pawn?.health.hediffSet.hediffs.Contains(this) == true)
                    pawn.health.Notify_HediffChanged(this);
            }
        }
        public int CurStageIndex => Math.Max(0, def.stages.FindLastIndex(s => severity >= s.minSeverity));
        public virtual HediffStage CurStage => def.stages.Count == 0 ? null : def.stages[CurStageIndex];
        // 提供已核验的原版可见性接口；生产覆写负责按身份隐藏。
        public virtual bool Visible => true;
        public virtual bool ShouldRemove => severity <= 0;
        public virtual string SeverityLabel => null;
        public virtual string LabelInBrackets => CurStage?.label;
        public virtual string LabelBase => "Bridle";
        public virtual string Description => "";
        public virtual string TipStringExtra => "";
        // 仅模拟已核验的原版悬停组合边界；实际属性、日志和排版仍由游戏验收。
        public virtual string GetTooltip(Pawn pawn, bool debug)
        {
            TooltipReads++;
            return "Native stage detail: " + CurStage?.label + "\n" + def.description + "\n" + def.extraTooltip + "\n" + CurStage?.extraTooltip;
        }
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
        public Action<Hediff> OnAdded, OnRemoved;
        public void AddHediff(Hediff record) { hediffSet.hediffs.Add(record); Adds++; OnAdded?.Invoke(record); }
        public void RemoveHediff(Hediff record) { hediffSet.hediffs.Remove(record); Removes++; OnRemoved?.Invoke(record); }
        public Action OnChanged;
        public void Notify_HediffChanged(Hediff record) { Notifications++; OnChanged?.Invoke(); }
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
    public enum LookMode { Reference, Value }
    public static partial class Scribe_Collections
    {
        public static void Look<T>(ref List<T> values, string key, LookMode mode) { }
    }
}
namespace Verse
{
    // 只观察原版消息的参数，不重写生产升阶资格，也不模拟屏幕或消息档案。
    public static class Messages
    {
        public record Entry(string Text, Pawn Target, object Type, bool Historical);
        public static readonly List<Entry> Received = new List<Entry>();
        public static void Message(string text, Pawn target, object type, bool historical = true)
            => Received.Add(new Entry(text, target, type, historical));
    }
}
namespace RimWorld
{
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public static class MessageTypeDefOf { public static readonly object PositiveEvent = new object(), RejectInput = new object(); }
}

namespace SexSlaveCraft
{
    public enum PawnIdentity { Unset, Master, Slave }
    public partial class CompSexSlaveTraining { public PawnIdentity pawnIdentity = PawnIdentity.Master; public int masterProgressionVersion = 1; }
    public static partial class SSCIdentityUtility { public static bool IsMaster(Pawn pawn) => pawn?.Training?.pawnIdentity == PawnIdentity.Master; }
    public static partial class SSCBondUtility
    {
        // 面板宿主只提供实际归属引用，缰绳的有效性和空状态清理由生产类判断。
        public static Pawn GetBoundMaster(Pawn pawn) => pawn?.BoundMaster;
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
        /// <summary>从真实主人面板快照取出测试观测值，分伤资格直接读取注册行而非另写判断。</summary>
        public Probe Read(Pawn pawn, float width = 322)
        {
            var v = BuildMasterPanelView(pawn, pawn.Training, width);
            return new Probe(v.Stage, v.Experience, v.Benefits, v.Progress, v.ProgressFraction, v.SectionHeight,
                v.AbilityRows.Single(r => r.Id == SSCAbilityToggleUtility.MasterDamageSharing).CanEdit);
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
        /// <summary>保留测量时的原分伤行与上下文，供用例在状态改变后调用生产 TrySet 验证迟到请求。</summary>
        public Action<bool> CaptureShareAction(Pawn pawn)
        {
            SelPawn = pawn;
            var view = BuildMasterPanelView(pawn, pawn.Training, 322);
            var row = view.AbilityRows.Single(r => r.Id == SSCAbilityToggleUtility.MasterDamageSharing);
            return value => { if (SelPawn == row.Pawn) SSCAbilityToggleUtility.TrySet(row.Pawn, row.Id, value, row.Context, out _); };
        }
        /// <summary>直接执行真实分组测量与绘制器；只提供选中对象和矩形，空组不额外绘制。</summary>
        public float DrawToggleGroup(Pawn pawn, SSCAbilityToggleGroup group, float width)
        {
            SelPawn=pawn;Widgets.Items.Clear();
            var rows=BuildAbilityToggleRows(pawn,pawn.Training,group,width-SectionPadding*2);
            float height=AbilityToggleSectionHeight(rows);
            if(height>0)DrawAbilityToggleSection(new Rect(0,0,width,height),rows);
            return height;
        }
    }
}
