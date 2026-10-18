using System;
using System.Collections.Generic;
using System.Linq;

// 仅提供信息栏输入／输出边界；原版条目生成与 Unity 绘制不在此宿主模拟。
namespace Verse
{
    public partial class ThingDef
    {
        public bool IsMeleeWeapon, IsRangedWeapon;
        public List<Tool> tools;
        public List<VerbProperties> verbs;
        public List<VerbProperties> Verbs => verbs ?? new List<VerbProperties>();
        public ProjectileProperties projectile;
        public readonly List<RimWorld.StatDrawEntry> NativeInfoEntries = new();
        public virtual IEnumerable<RimWorld.StatDrawEntry> SpecialDisplayStats(RimWorld.StatRequest req) => NativeInfoEntries;
    }
    public class ProjectileProperties { public DamageDef damageDef; }
    public class VerbProperties { public ThingDef defaultProjectile; public DamageDef meleeDamageDef; }
    public class ToolCapacityDef { }
    public class ManeuverDef { public ToolCapacityDef requiredCapacity; public VerbProperties verb; }
    public class Tool
    {
        public List<ToolCapacityDef> capacities;
        // 仅提供动作关联边界；不模拟原版攻击选择、品质或命中权重。
        public IEnumerable<ManeuverDef> Maneuvers => DefDatabase<ManeuverDef>.AllDefs.Where(m => capacities.Contains(m.requiredCapacity));
    }
    public enum ToStringNumberSense { Absolute }
}

namespace RimWorld
{
    public struct StatRequest
    {
        public object Def;
        public Verse.Thing Thing;
        public static StatRequest For(Verse.ThingDef def) => new StatRequest { Def=def };
        public static StatRequest For(Verse.Thing thing) => new StatRequest { Def=thing.def, Thing=thing };
    }
    public class StatDef { }
    // 原生筛选／修正作为可观察边界；UI 提示绘制与完整原版 StatWorker 不在此宿主模拟。
    public class StatWorker
    {
        public bool NativeVisibility = true;
        public virtual bool ShouldShowFor(StatRequest req) => NativeVisibility;
        public virtual float GetValueUnfinalized(StatRequest req, bool applyPostProcess=true) => throw new NotSupportedException();
        public virtual void FinalizeValue(StatRequest req, ref float val, bool applyPostProcess) => throw new NotSupportedException();
        public virtual string GetExplanationUnfinalized(StatRequest req, Verse.ToStringNumberSense numberSense) => throw new NotSupportedException();
        public virtual string GetExplanationFinalizePart(StatRequest req, Verse.ToStringNumberSense numberSense, float finalVal) => throw new NotSupportedException();
    }
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
