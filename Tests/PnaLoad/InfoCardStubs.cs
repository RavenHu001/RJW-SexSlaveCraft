using System.Collections.Generic;

// 仅提供信息栏输入／输出边界；原版条目生成与 Unity 绘制不在此宿主模拟。
namespace Verse
{
    public partial class ThingDef
    {
        public readonly List<RimWorld.StatDrawEntry> NativeInfoEntries = new();
        public virtual IEnumerable<RimWorld.StatDrawEntry> SpecialDisplayStats(RimWorld.StatRequest req) => NativeInfoEntries;
    }
}

namespace RimWorld
{
    public struct StatRequest { }
    public class StatCategoryDef { }
    public static class StatCategoryDefOf
    {
        public static readonly StatCategoryDef Drug = new(), DrugAddiction = new(), Basics = new();
    }
    public sealed class StatDrawEntry
    {
        public StatCategoryDef category;
        public object stat;
        public string LabelCap { get; }
        public string ValueString { get; }
        public string Report { get; }
        public StatDrawEntry(StatCategoryDef category, string label, string valueString, string reportText, int displayPriorityWithinCategory)
        { this.category = category; LabelCap = label; ValueString = valueString; Report = reportText; }
    }
}
