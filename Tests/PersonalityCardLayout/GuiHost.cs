// 记录生产界面的绘制调用和滚动区域，不启动 Unity；字体度量是近似值。
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        /// <summary>建立二维坐标。</summary>
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2();
    }
    public struct Rect
    {
        public float x, y, width, height;
        /// <summary>记录矩形的位置与尺寸。</summary>
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax => x + width;
        public float yMax => y + height;
        /// <summary>生成统一内缩后的内容矩形。</summary>
        public Rect ContractedBy(float margin) => new Rect(x + margin, y + margin, width - 2 * margin, height - 2 * margin);
        /// <summary>判断鼠标是否位于当前矩形。</summary>
        public bool Contains(Vector2 point) => point.x >= x && point.x < xMax && point.y >= y && point.y < yMax;
    }
    public struct Color
    {
        public float r, g, b, a;
        /// <summary>记录颜色和透明度。</summary>
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
    }
    public enum TextAnchor { UpperLeft, MiddleLeft, MiddleRight }
    public class Texture2D { }
    public static class Mathf
    {
        /// <summary>取得较大数值。</summary>
        public static float Max(float a, float b) => Math.Max(a, b);
        /// <summary>取得较小数值。</summary>
        public static float Min(float a, float b) => Math.Min(a, b);
        /// <summary>将数值限定在指定范围。</summary>
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        /// <summary>将填充比例限定在零到一。</summary>
        public static float Clamp01(float value) => Clamp(value, 0, 1);
    }
    public static class GUI
    {
        public static Color color = Color.white;
        /// <summary>记录分组带来的坐标偏移。</summary>
        public static void BeginGroup(Rect rect) => DrawLog.Push(rect.x, rect.y, null);
        /// <summary>恢复分组外的坐标系。</summary>
        public static void EndGroup() => DrawLog.Pop();
        /// <summary>记录热情图标的绘制区域。</summary>
        public static void DrawTexture(Rect rect, Texture2D icon) => DrawLog.Add("icon", rect, "🔥");
    }
}

namespace Verse
{
    public enum GameFont { Tiny, Small, Medium }
    public static class Text
    {
        public static GameFont Font = GameFont.Small;
        public static TextAnchor Anchor;
        public static bool WordWrap = true;
        public static float LineHeight => Font == GameFont.Medium ? 30f : 22f;
        /// <summary>近似测量文本宽度；不模拟 Unity 的真实字体和富文本排版。</summary>
        public static Vector2 CalcSize(string text) => new Vector2(Plain(text).Sum(c => c > 255 ? 13f : 7f), LineHeight);
        /// <summary>按近似字宽计算换行高度，供生产代码决定长名称行高。</summary>
        public static float CalcHeight(string text, float width) => Math.Max(1, (float)Math.Ceiling(CalcSize(text).x / width)) * LineHeight;
        /// <summary>移除用于标题加粗的标记，便于近似字体测量。</summary>
        public static string Plain(string text) => (text ?? "").Replace("<b>", "").Replace("</b>", "");
    }
    public static class GenText
    {
        /// <summary>缩短单行显示文字，完整文本由生产代码另行注册为提示。</summary>
        public static string Truncate(this string text, float width)
        {
            if (Text.CalcSize(text).x <= width) return text;
            string plain = Text.Plain(text);
            while (plain.Length > 0 && Text.CalcSize(plain + "…").x > width) plain = plain[..^1];
            return plain + "…";
        }
    }
    public static class Widgets
    {
        /// <summary>记录标签、字体、换行状态及其所在的滚动区域。</summary>
        public static void Label(Rect rect, string text) => DrawLog.Add("label", rect, text);
        /// <summary>记录背景与分隔线。</summary>
        public static void DrawBoxSolid(Rect rect, Color color) => DrawLog.Add("box", rect, "", color);
        /// <summary>记录条形图背景与填充范围。</summary>
        public static void FillableBar(Rect rect, float fill)
        {
            DrawLog.Add("box", rect, "", new Color(.15f, .15f, .15f));
            DrawLog.Add("box", new Rect(rect.x, rect.y, rect.width * fill, rect.height), "", new Color(.2f, .75f, .8f));
        }
        /// <summary>记录按钮并根据测试设置模拟点击。</summary>
        public static bool ButtonText(Rect rect, string text)
        {
            DrawLog.Add("button", rect, text);
            bool clicked = DrawLog.ClickButton;
            DrawLog.ClickButton = false;
            return clicked;
        }
        /// <summary>仅在鼠标位于视口时模拟滚动，记录独立视口并建立裁剪坐标系。</summary>
        public static void BeginScrollView(Rect rect, ref Vector2 position, Rect content)
        {
            Rect screen = DrawLog.Screen(rect);
            if (screen.Contains(DrawLog.Mouse)) position.y = Math.Clamp(position.y + DrawLog.Wheel, 0, Math.Max(0, content.height - rect.height));
            var view = new ScrollRecord(screen, content, position);
            DrawLog.Scrolls.Add(view);
            DrawLog.Push(rect.x - position.x, rect.y - position.y, screen);
        }
        /// <summary>退出滚动视口，后续摘要和按钮恢复为固定区域。</summary>
        public static void EndScrollView() => DrawLog.Pop();
    }
    public static class TooltipHandler
    {
        /// <summary>保留悬停文字以检查长名称及完整姓名是否可访问。</summary>
        public static void TipRegion(Rect rect, string text) => DrawLog.Add("tooltip", rect, text);
    }
    public class Thing
    {
        public SexSlaveCraft.CompPersonalityStore Comp;
        /// <summary>返回测试物品上的人格组件。</summary>
        public T TryGetComp<T>() where T : class => Comp as T;
    }
    public class NameTriple
    {
        private readonly string first, nick, last;
        /// <summary>保存用于完整姓名提示的三个姓名字段。</summary>
        public NameTriple(string first, string nick, string last) { this.first = first; this.nick = nick; this.last = last; }
        public string ToStringFull => $"{first} '{nick}' {last}";
    }
    public class ITab
    {
        protected Vector2 size;
        protected string labelKey, tutorTag;
        public Thing SelThing;
        public virtual bool IsVisible => true;
        /// <summary>提供生产页签覆盖的绘制入口。</summary>
        protected virtual void FillTab() { }
    }
    public class Pawn : Thing
    {
        public string LabelShort;
        public bool Dead;
        public RaceProperties RaceProps = new RaceProperties();
        public Health health = new Health();
    }
    public class RaceProperties { public bool Humanlike = true; }
    public class Health { public HediffSet hediffSet = new HediffSet(); }
    public class HediffSet
    {
        public bool IsHollow;
        /// <summary>返回测试角色是否带有空壳标记。</summary>
        public bool HasHediff(object def) => IsHollow;
    }
    public class Map
    {
        public MapPawns mapPawns = new MapPawns();
        public SexSlaveCraft.MapComponent_PersonalityAssignment Assignments = new SexSlaveCraft.MapComponent_PersonalityAssignment();
        /// <summary>提供当前地图的人格分配组件。</summary>
        public T GetComponent<T>() where T : class => Assignments as T;
    }
    public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); }
    public static class Find { public static Map CurrentMap; public static WindowStack WindowStack = new WindowStack(); }
    public class WindowStack
    {
        public FloatMenu LastMenu;
        /// <summary>记录生产界面打开的菜单，允许用例执行菜单选项。</summary>
        public void Add(FloatMenu menu) { LastMenu = menu; }
    }
    public class FloatMenu
    {
        public List<FloatMenuOption> Options;
        /// <summary>保存菜单项及其真实回调。</summary>
        public FloatMenu(List<FloatMenuOption> options) { Options = options; }
    }
    public class FloatMenuOption
    {
        public string Label;
        public Action Action;
        /// <summary>记录菜单文字与选择动作。</summary>
        public FloatMenuOption(string label, Action action) { Label = label; Action = action; }
    }
}

namespace RimWorld
{
    public enum Passion { None, Minor, Major }
    public class SkillDef { public int listOrder; public string LabelCap; }
    public static class SkillUI { public static Texture2D PassionMajorIcon = new Texture2D(), PassionMinorIcon = new Texture2D(); }
    public class BackstoryDef { public string title; }
    public class TraitDef
    {
        public string description;
        /// <summary>返回当前测试特质的描述。</summary>
        public TraitDef DataAtDegree(int degree) => this;
    }
    public class Trait { public TraitDef def = new TraitDef(); public string LabelCap; public int Degree; }
}

namespace SexSlaveCraft
{
    public class StoredSkillData { public RimWorld.SkillDef def; public int level; public float xp; public RimWorld.Passion passion; }
    public class StoredChainHediffData { public Pawn masterPawn; public float severity; }
    public class CompPersonalityStore
    {
        public string firstName = "Riley", nickName = "Riley", lastName = "Test";
        public bool hasSexSlaveTrait = true;
        public RimWorld.BackstoryDef childhood = new RimWorld.BackstoryDef { title = "医疗助理" }, adulthood = new RimWorld.BackstoryDef { title = "舰队科学家" };
        public List<StoredSkillData> storedSkills = new List<StoredSkillData>();
        public List<RimWorld.Trait> storedTraits = new List<RimWorld.Trait>();
        public List<object> storedRelations = new List<object>(), storedMemories = new List<object>();
        public StoredChainHediffData chainHediffData;
    }
    public static class SSCDefOf { public static object SSC_PersonalityExcreted_Done = new object(); }
    public class MapComponent_PersonalityAssignment
    {
        private readonly Dictionary<Thing, Pawn> targets = new Dictionary<Thing, Pawn>();
        /// <summary>查找当前凝胶的分配目标。</summary>
        public Pawn GetAssignedTarget(Thing gel) => targets.GetValueOrDefault(gel);
        /// <summary>查找已经占用目标角色的凝胶。</summary>
        public Thing GetAssignedGel(Pawn target) => targets.FirstOrDefault(pair => pair.Value == target).Key;
        /// <summary>记录生产菜单回调选择的凝胶和目标。</summary>
        public void Assign(Thing gel, Pawn target) { targets[gel] = target; }
        /// <summary>取消指定凝胶的分配。</summary>
        public void Unassign(Thing gel) { targets.Remove(gel); }
    }
    public static class Strings
    {
        private static Dictionary<string, string> translations;
        /// <summary>直接读取仓库语言文件，让布局检查使用真实翻译文本。</summary>
        public static void Load(string root, string language)
        {
            translations = XDocument.Load(Path.Combine(root, "Languages", language, "Keyed", "SSC_Keys.xml"))
                .Root.Elements().GroupBy(element => element.Name.LocalName).ToDictionary(group => group.Key, group => group.First().Value);
        }
        /// <summary>取得真实翻译，并替换测试用例提供的占位符参数。</summary>
        private static string Get(string name, params object[] args) => string.Format(translations["SSC_PES_" + name], args);
        /// <summary>生成完整姓名提示。</summary>
        public static string PES_FullName(string name) => Get("FullName", name);
        /// <summary>生成童年背景行。</summary>
        public static string PES_Childhood(string title) => Get("Childhood", title);
        /// <summary>生成成年背景行。</summary>
        public static string PES_Adulthood(string title) => Get("Adulthood", title);
        public static string PES_SexSlaveTag => Get("SexSlaveTag");
        public static string PES_NoBackground => Get("NoBackground");
        public static string PES_SkillsHeader => Get("SkillsHeader");
        /// <summary>生成技能等级提示。</summary>
        public static string PES_SkillLevel(string skill, int level) => Get("SkillLevel", skill, level);
        /// <summary>生成技能热情提示。</summary>
        public static string PES_SkillPassion(string passion) => Get("SkillPassion", passion);
        /// <summary>生成技能经验提示。</summary>
        public static string PES_SkillXP(string xp) => Get("SkillXP", xp);
        /// <summary>生成带数量的特质标题。</summary>
        public static string PES_TraitsCount(int count) => Get("TraitsCount", count);
        public static string PES_NoTraits => Get("NoTraits");
        public static string PES_NoMaster => Get("NoMaster");
        public static string PES_BondHeader => Get("BondHeader");
        public static string PES_ChainSeverityLabel => Get("ChainSeverityLabel");
        /// <summary>生成主人信息。</summary>
        public static string PES_MasterLabel(string master) => Get("MasterLabel", master);
        /// <summary>生成关系数量。</summary>
        public static string PES_RelationshipSummary(int count) => Get("RelationshipSummary", count);
        /// <summary>生成记忆数量。</summary>
        public static string PES_MemorySummary(int count) => Get("MemorySummary", count);
        public static string PES_MemoriesHint => Get("MemoriesHint");
        public static string PES_TargetUnassigned => Get("TargetUnassigned");
        public static string PES_ChangeTarget => Get("ChangeTarget");
        public static string PES_AssignTarget => Get("AssignTarget");
        public static string PES_Unassign => Get("Unassign");
        public static string PES_NoHollowTargets => Get("NoHollowTargets");
        public static string PES_WaitingForInsert => Get("WaitingForInsert");
        /// <summary>生成已指定目标状态。</summary>
        public static string PES_AssignedTo(string target) => Get("AssignedTo", target);
        /// <summary>生成目标已被其它凝胶占用的提示。</summary>
        public static string PES_AlreadyAssigned(string other) => Get("AlreadyAssigned", other);
    }
}

public record ScrollRecord(Rect Viewport, Rect Content, Vector2 Position);
public record DrawRecord(string Kind, Rect Rect, string Text, Rect? Clip, GameFont Font, TextAnchor Anchor, bool Wrap, Color Color);
public static class DrawLog
{
    public static readonly List<DrawRecord> Items = new List<DrawRecord>();
    public static readonly List<ScrollRecord> Scrolls = new List<ScrollRecord>();
    private static readonly Stack<(Vector2 offset, Rect? clip)> scopes = new Stack<(Vector2, Rect?)>();
    private static Vector2 offset;
    private static Rect? clip;
    public static Vector2 Mouse = new Vector2(-1, -1);
    public static float Wheel;
    public static bool ClickButton;
    public static int Depth => scopes.Count;
    /// <summary>清空上一帧绘制记录，保留测试设置的输入状态。</summary>
    public static void Clear() { Items.Clear(); Scrolls.Clear(); }
    /// <summary>把局部矩形转换为页签屏幕坐标。</summary>
    public static Rect Screen(Rect rect) => new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
    /// <summary>进入界面分组或滚动视口。</summary>
    public static void Push(float x, float y, Rect? newClip)
    {
        scopes.Push((offset, clip));
        offset = new Vector2(offset.x + x, offset.y + y);
        clip = newClip ?? clip;
    }
    /// <summary>恢复上层坐标与裁剪状态。</summary>
    public static void Pop() { (offset, clip) = scopes.Pop(); }
    /// <summary>记录一次原始绘制调用，供用例验证边界及固定区域。</summary>
    public static void Add(string kind, Rect rect, string text, Color? color = null) =>
        Items.Add(new DrawRecord(kind, Screen(rect), Text.Plain(text), clip, Text.Font, Text.Anchor, Text.WordWrap, color ?? GUI.color));
}
