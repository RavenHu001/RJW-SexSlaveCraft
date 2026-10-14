// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;

// 只提供原版健康和严重度边界；经验资格、截断、记录写入与身份启停均编译生产代码。
namespace Verse
{
    public partial class Pawn { public bool Discarded; public RimWorld.Faction Faction = RimWorld.Faction.OfPlayer; }
    public partial class HediffDef
    {
        public string label;
        public Type hediffClass;
        public float maxSeverity = float.MaxValue;
        public List<HediffStage> stages = new List<HediffStage>();
    }
    public class HediffStage { public string label; public float minSeverity; }
    public partial class Hediff
    {
        public Pawn pawn;
        private float severity;
        // 模拟原版跨阶段健康通知，允许用例在通知中替换任务或重入结算。
        public float Severity
        {
            get => severity;
            set
            {
                int before = CurStageIndex;
                severity = Math.Max(0, Math.Min(value, def.maxSeverity));
                if (before != CurStageIndex && pawn?.health.hediffSet.hediffs.Contains(this) == true)
                    pawn.health.Notify_HediffChanged(this);
            }
        }
        public int CurStageIndex => Math.Max(0, def.stages.FindLastIndex(s => Severity >= s.minSeverity));
        public virtual HediffStage CurStage => def.stages.Count > 0 ? def.stages[CurStageIndex] : null;
        // 提供已核验的原版可见性接口；生产覆写负责按身份隐藏。
        public virtual bool Visible => true;
        public virtual bool ShouldRemove => Severity <= 0;
        public virtual string SeverityLabel => null;
        public virtual string LabelInBrackets => CurStage?.label;
        public virtual string GetTooltip(Pawn pawn, bool debug) => string.Empty;
        public virtual void ExposeData() => Scribe_Values.Look(ref severity, "severity", 0f);
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn)
        {
            var result = (Hediff)Activator.CreateInstance(def.hediffClass ?? typeof(Hediff));
            result.def = def; result.pawn = pawn;
            return result;
        }
    }
    public partial class PawnHealth
    {
        public Action OnHediffChanged;
        public void AddHediff(Hediff hediff) => hediffSet.hediffs.Add(hediff);
        public void Notify_HediffChanged(Hediff hediff) => OnHediffChanged?.Invoke();
    }
}
namespace SexSlaveCraft
{
    public static partial class SSCDefOf
    {
        public static readonly Verse.HediffDef SSC_Hediff_MasterProgression = LoadProgressionDef();

        // 测试读取本仓库 Def 的真实阈值和上限，避免在宿主再维护一套成长数值。
        private static Verse.HediffDef LoadProgressionDef()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Defs/HediffDefs/SSC_MasterProgression.xml")))
                directory = directory.Parent;
            if (directory == null) throw new InvalidOperationException("Master progression Def not found.");
            XElement source = XDocument.Load(Path.Combine(directory.FullName, "Defs/HediffDefs/SSC_MasterProgression.xml")).Root.Element("HediffDef");
            return new Verse.HediffDef
            {
                defName = source.Element("defName").Value,
                label = source.Element("label").Value,
                hediffClass = typeof(Hediff_MasterProgression),
                maxSeverity = float.Parse(source.Element("maxSeverity").Value, CultureInfo.InvariantCulture),
                stages = source.Element("stages").Elements("li").Select(s => new Verse.HediffStage
                {
                    label = s.Element("label").Value,
                    minSeverity = float.Parse(s.Element("minSeverity").Value, CultureInfo.InvariantCulture)
                }).ToList()
            };
        }
    }
}
