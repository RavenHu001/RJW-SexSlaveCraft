using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

// 仅模拟健康接口、组件调度、字段保存与 Harmony 目标；不替代 Unity 能力计算或真实 Scribe。
namespace Verse
{
    public partial class Thing { public bool Destroyed, Discarded; public ThingDef def; public object Comp; public T TryGetComp<T>() where T : class => Comp as T; }
    public partial class ThingDef { public string defName, label; public IngestibleProperties ingestible = new IngestibleProperties(); }
    public class IngestibleProperties { public List<RimWorld.IngestionOutcomeDoer> outcomeDoers = new List<RimWorld.IngestionOutcomeDoer>(); }
    public partial class Pawn : Thing
    {
        public bool Dead, Suspended, IsSlave;
        public float Corruption;
        public bool IsMaster;
        public string LabelShort => "Test pawn";
        public bool NotifyPlayer;
        public int NativeTicks;
        public bool IsHashIntervalTick(int interval,int delta)=>NativeTicks%interval<delta;
        public float BodySize = 1f, ChemicalEffectFactor = 1f;
        public object DrawPos => null;
        public Map Map;
        public RaceProperties RaceProps = new RaceProperties();
        public Pawn_HealthTracker health;
        public RimWorld.Pawn_NeedsTracker needs;
        public Pawn() { health = new Pawn_HealthTracker(this); needs = new RimWorld.Pawn_NeedsTracker(this); }
    }
    public class RaceProperties { public bool IsFlesh = true, Humanlike = true; public BodyDef body = new BodyDef(); }
    public partial class HediffDef
    {
        public Type hediffClass;
        public string defName;
        public float initialSeverity = 0.01f, maxSeverity = 2f;
        public float[] thresholds = new[] { 0f };
        public bool HasPnaComp;
        public bool HasRecoveryComp;
        public bool organicAddedBodypart;
        public RimWorld.NeedDef chemicalNeed;
        public T CompProps<T>() where T : HediffCompProperties
            => HasRecoveryComp ? new HediffCompProperties_SeverityPerDay{severityPerDay=-.1f,showDaysToRecover=true} as T : null;
    }
    public class BodyDef { public List<BodyPartRecord> AllParts = new List<BodyPartRecord>(); }
    public class BodyPartDef
    {
        public bool Solid;
        public bool IsSolid(BodyPartRecord part, List<Hediff> hediffs) => Solid;
    }
    public class BodyPartRecord { public BodyPartDef def = new BodyPartDef(); public BodyPartRecord parent; }
    public struct DamageInfo
    {
        public DamageDef Def;
        public float Amount;
        public int Parts;
        public bool Deflected;
        public float ArmorPenetrationInt;
        public bool IgnoreArmor, AllowDamagePropagation;
        public DamageInfo(DamageInfo other) { this=other; }
        public void SetAmount(float value) { Amount = value; }
        public void SetIgnoreArmor(bool value) { IgnoreArmor = value; }
    }
    public class DefModExtension { }
    public class DamageDef
    {
        public Type workerClass = typeof(DamageWorker_AddInjury);
        public DefModExtension Extension;
        public T GetModExtension<T>() where T : DefModExtension => Extension as T;
    }
    public class DamageWorker
    {
        public class DamageResult
        {
            public bool wounded, deflected, diminished;
            public float totalDamageDealt;
            public List<Hediff> hediffs;
        }
    }
    public class DamageWorker_AddInjury : DamageWorker
    {
        public Action<Pawn> BeforePart;
        public int InjuryCalls;
        // 直接提供物理完成结果和同步回调边界，不模拟护甲随机数或复制原版伤害算法。
        public Func<DamageInfo, Thing, DamageResult> PhysicalApply;
        public int ApplyCalls;
        public virtual DamageResult Apply(DamageInfo info, Thing target)
        {
            ApplyCalls++;
            if (PhysicalApply != null) return PhysicalApply(info, target);
            var result = new DamageResult();
            if (target is Pawn pawn && info.Amount > 0 && !info.Deflected)
                for (int i=0;i<Math.Max(1,info.Parts);i++) { BeforePart?.Invoke(pawn); ApplySpecialEffectsToPart(pawn,info.Amount,info,result); }
            return result;
        }
        protected virtual void ApplySpecialEffectsToPart(Pawn pawn,float totalDamage,DamageInfo info,DamageResult result) { InjuryCalls++; }
    }
    public class Hediff_Injury : Hediff
    {
        public float Healed;
        public bool CanHealNaturally() => true;
        public Action OnHeal;
        public void Heal(float amount) { Healed += amount; OnHeal?.Invoke(); }
    }
    public class Hediff_AddedPart : Hediff { }
    public class Hediff_MissingPart : Hediff { public bool IsFresh; public HediffDef lastInjury; }
    public class PawnCapacityDef { }
    public class PawnCapacitiesHandler { public float Consciousness=1f; public float GetLevel(PawnCapacityDef def)=>Consciousness; }
    public class Hediff
    {
        public virtual bool Visible => true;
        public virtual bool ShouldRemove => false;
        public Pawn pawn;
        public BodyPartRecord Part;
        public HediffDef def;
        public float RawSeverity;
        public virtual int CurStageIndex => Array.FindLastIndex(def.thresholds, threshold => RawSeverity >= threshold);
        public virtual float Severity
        {
            get => RawSeverity;
            set
            {
                int stage = CurStageIndex;
                RawSeverity = float.IsNaN(value) ? value : Math.Clamp(value, 0f, def.maxSeverity);
                if (stage != CurStageIndex && pawn.health.hediffSet.hediffs.Contains(this)) pawn.health.Notify_HediffChanged(this);
            }
        }
        public virtual string SeverityLabel { [MethodImpl(MethodImplOptions.NoInlining)] get => (Severity * 100).ToString("F0") + "%"; }
    }
    public class HediffWithComps : Hediff
    {
        public List<HediffComp> comps = new List<HediffComp>();
        public T GetComp<T>() where T : HediffComp => comps.OfType<T>().FirstOrDefault();
        public T TryGetComp<T>() where T : HediffComp => GetComp<T>();
    }
    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def)=>hediffs.FirstOrDefault(h=>h.def==def);
        public bool HasHediff(HediffDef def)=>GetFirstHediffOfDef(def)!=null;
        public Hediff GetDirectlyAddedPartFor(BodyPartRecord part)=>hediffs.FirstOrDefault(h=>h.Part==part && h is Hediff_AddedPart);
        public bool PartIsMissing(BodyPartRecord part)
        {
            for (var current=part;current!=null;current=current.parent)
                if(hediffs.Any(h=>h is Hediff_MissingPart && h.Part==current)) return true;
            return false;
        }
    }
    public class Pawn_HealthTracker
    {
        public readonly Pawn Pawn;
        public readonly HediffSet hediffSet = new HediffSet();
        public Action<Hediff> OnChanged, OnAdded, OnRemoved;
        public int Notifications;
        public PawnCapacitiesHandler capacities = new PawnCapacitiesHandler();
        public Pawn_HealthTracker(Pawn pawn) { Pawn = pawn; }
        public void AddHediff(Hediff hediff) => AddHediff(hediff, null, null, null);
        public Hediff AddHediff(HediffDef def) { var h=HediffMaker.MakeHediff(def,Pawn);AddHediff(h);return h; }
        public Hediff AddHediff(HediffDef def, BodyPartRecord part) { var h=HediffMaker.MakeHediff(def,Pawn);h.Part=part;AddHediff(h);return h; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddHediff(Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
        {
            if (part != null) hediff.Part = part;
            Hediff previous = hediff is Hediff_Injury || hediff is Hediff_MissingPart ? null :
                hediffSet.hediffs.FirstOrDefault(h => h.def == hediff.def && h is not SexSlaveCraft.Hediff_ChainOfSexSlave);
            if (previous != null) previous.Severity += hediff.Severity;
            else hediffSet.hediffs.Add(hediff);
            OnAdded?.Invoke(hediff);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void RemoveHediff(Hediff hediff)
        {
            if (!hediffSet.hediffs.Remove(hediff)) return;
            Notifications++;
            OnRemoved?.Invoke(hediff);
        }
        public void Notify_HediffChanged(Hediff hediff) { Notifications++; OnChanged?.Invoke(hediff); }
    }
    public static class HealthUtility
    {
        public static void AdjustSeverity(Pawn pawn,HediffDef def,float value)
        {
            var existing=pawn.health.hediffSet.hediffs.FirstOrDefault(h=>h.def==def);
            if(existing!=null)existing.Severity+=value;
            else { var h=HediffMaker.MakeHediff(def,pawn);h.Severity=value;pawn.health.AddHediff(h); }
        }
    }
    public static class MoteMaker
    {
        // 记录显示边界，验证有效输入与真实治疗才显示；不模拟 Unity 绘制。
        public static readonly List<string> Sent = new List<string>();
        public static void ThrowText(object pos,string text,float time) => Sent.Add(text);
        public static void ThrowText(object pos,object map,string text,float time) => Sent.Add(text);
    }
    public class HediffCompProperties { public Type compClass; }
    public class HediffComp
    {
        public HediffWithComps parent;
        public HediffCompProperties props;
        public Pawn Pawn => parent.pawn;
        public virtual void CompPostTick(ref float adjustment) { }
        public virtual void CompPostTickInterval(ref float adjustment, int delta) { }
        public virtual void CompExposeData() { }
        public virtual bool CompShouldRemove => false;
        public virtual string CompTipStringExtra => null;
        public virtual string CompDebugString() => null;
    }
    public class HediffCompProperties_SeverityPerDay : HediffCompProperties
    {
        public float severityPerDay;
        public bool showDaysToRecover;
    }
    public class HediffComp_SeverityPerDay : HediffComp
    {
        public float severityPerDay;
        [MethodImpl(MethodImplOptions.NoInlining)] public virtual float SeverityChangePerDay()=>severityPerDay;
        // 隔离原版 200-tick 组件调度和显示边界；生产代码不再补丁恢复速率。
        public override void CompPostTickInterval(ref float adjustment,int delta)
        {
            if(delta>0 && Pawn.IsHashIntervalTick(200,delta))adjustment+=SeverityChangePerDay()*(200f/60000f);
        }
        public override string CompTipStringExtra=>SeverityChangePerDay()<0f ? "DaysToRecover" : null;
        public override void CompExposeData()=>Scribe_Values.Look(ref severityPerDay,"severityPerDay",0f);
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn)
        {
            if (def.hediffClass == typeof(SexSlaveCraft.Hediff_SSCToggleAbilityMarker))
                return new SexSlaveCraft.Hediff_SSCToggleAbilityMarker { def = def, pawn = pawn };
            HediffWithComps record = def.defName == "PNA_Addiction" ? new RimWorld.Hediff_Addiction() : new HediffWithComps();
            record.def = def; record.pawn = pawn; record.RawSeverity = def.initialSeverity;
            if (def.HasPnaComp) record.comps.Add(new SexSlaveCraft.HediffComp_SSCPnaLoad { parent = record, props = new SexSlaveCraft.HediffCompProperties_SSCPnaLoad() });
            if (def.HasRecoveryComp) record.comps.Add(new HediffComp_SeverityPerDay { parent = record, props = def.CompProps<HediffCompProperties_SeverityPerDay>(),severityPerDay=-.1f });
            if (def.defName == "SSC_Hediff_RabbitCloneLink") record.comps.Add(new SexSlaveCraft.HediffComp_RabbitCloneLink { parent = record });
            return record;
        }
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; public static Dictionary<string, object> Data = new Dictionary<string, object>(); }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Scribe.Data.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
        }
    }
    public class Game { }
    public class GameComponent { public GameComponent() { } public virtual void FinalizeInit() { } }
    public static partial class Find { public static Selector Selector = new Selector(); }
    public class Selector { public Thing SingleSelectedThing; }
    public static class Messages
    {
        public static readonly List<string> Sent = new List<string>();
        public static void Message(string text, RimWorld.MessageTypeDef type) => Sent.Add(text);
        public static void Message(string text, Pawn pawn, RimWorld.MessageTypeDef type, bool historical) => Sent.Add(text);
    }
    public static class StringExtensions
    {
        public static string Translate(this string text, params object[] args) => text + string.Join(",", args);
        public static string ToStringPercent(this float value) => (value * 100).ToString("F0") + "%";
        public static string CapitalizeFirst(this string value) => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
        public static T RandomElement<T>(this IEnumerable<T> values)=>values.First();
    }
}
namespace RimWorld
{
    // 原版成瘾只依据合法需求类别返回 0／1；用于检查生产偏好直接读取此虚属性。
    public class Hediff_Addiction : Verse.HediffWithComps
    {
        public override int CurStageIndex => pawn?.needs?.TryGetNeed(def.chemicalNeed)?.CurCategory == DrugDesireCategory.Withdrawal ? 1 : 0;
    }
    public partial class Pawn_NeedsTracker
    {
        private readonly Verse.Pawn pawn;
        public bool AllowPnaNeed = true;
        // 模拟除成瘾外的合法需求来源，用于验证新生不沿用复制值，而成熟保留自身合法值。
        public bool OtherPnaNeedSource;
        private Need_Chemical chemicalNeed;
        public float? PnaNeed
        {
            get=>chemicalNeed?.CurLevel;
            set { chemicalNeed=value.HasValue ? new Need_Chemical{CurLevel=value.Value}:null; }
        }
        public Need_Chemical TryGetNeed(NeedDef def)=>chemicalNeed;
        public int Refreshes;
        public Pawn_NeedsTracker(Verse.Pawn pawn) { this.pawn = pawn; }
        public void AddOrRemoveNeedsAsAppropriate()
        {
            Refreshes++;
            if (!AllowPnaNeed || !OtherPnaNeedSource && !pawn.health.hediffSet.hediffs.Any(h => SexSlaveCraft.SSCPnaExposureUtility.IsAddictionDef(h.def))) PnaNeed = null;
            else if(chemicalNeed==null) chemicalNeed=new Need_Chemical{CurLevel=.8f};
        }
    }
    public class NeedDef { public string defName; }
    public enum DrugDesireCategory { Satisfied, Desire, Withdrawal }
    public class Need_Chemical : Need
    {
        public int Initializations;
        // 原版随机 80%～100% 的固定边界值，测试不复刻随机分布。
        public void SetInitialLevel() { Initializations++; CurLevel=.8f; }
        public DrugDesireCategory CurCategory=>CurLevel>.1f?DrugDesireCategory.Satisfied:CurLevel>.01f?DrugDesireCategory.Desire:DrugDesireCategory.Withdrawal;
    }
    public abstract class IngestionOutcomeDoer
    {
        public void DoIngestionOutcome(Verse.Pawn pawn,Verse.Thing ingested,int count)=>DoIngestionOutcomeSpecial(pawn,ingested,count);
        protected abstract void DoIngestionOutcomeSpecial(Verse.Pawn pawn,Verse.Thing ingested,int count);
    }
    public static class AddictionUtility
    {
        public static int ModifierCalls;
        public static void ModifyChemicalEffectForToleranceAndBodySize(Verse.Pawn pawn,ChemicalDef chemical,ref float effect,bool applyGeneToleranceFactor,bool divideByBodySize=true)
        {
            ModifierCalls++;
            if(chemical!=null)effect*=pawn.ChemicalEffectFactor;
            if(divideByBodySize)effect/=pawn.BodySize;
        }
    }
    public static class PawnUtility { public static bool ShouldSendNotificationAbout(Verse.Pawn pawn)=>pawn.NotifyPlayer; }
    public static class PawnCapacityDefOf { public static Verse.PawnCapacityDef Consciousness=new Verse.PawnCapacityDef(); }
    public class ChemicalDef { public Verse.HediffDef addictionHediff; }
    public class CompProperties_Drug { public ChemicalDef chemical; public bool Addictive = true; public float existingAddictionSeverityOffset = .1f, needLevelOffset = 1f; }
    public class CompDrug
    {
        public CompProperties_Drug Props;
        public int OriginalCalls;
        public Verse.Thing parent = new Verse.Thing{def=new Verse.ThingDef()};
        // 原版边界替身仅模拟已有成瘾的属性增加和需求修正；不实现随机成瘾或游戏宿主。
        [MethodImpl(MethodImplOptions.NoInlining)] public void PrePostIngested(Verse.Pawn ingester)
        {
            OriginalCalls++;
            if(!Props.Addictive || !ingester.RaceProps.IsFlesh)return;
            var addiction=SexSlaveCraft.SSCPnaExposureUtility.GetAddiction(ingester);
            if(addiction!=null)addiction.Severity+=Props.existingAddictionSeverityOffset;
            var need=ingester.needs.TryGetNeed(Props.chemical.addictionHediff.chemicalNeed);
            if(need!=null)
            {
                float effect=Props.needLevelOffset;
                AddictionUtility.ModifyChemicalEffectForToleranceAndBodySize(ingester,Props.chemical,ref effect,false);
                need.CurLevel=Math.Min(need.MaxLevel,need.CurLevel+effect);
            }
        }
    }
    public class MessageTypeDef { }
    public static class MessageTypeDefOf { public static MessageTypeDef RejectInput = new MessageTypeDef(), NeutralEvent = new MessageTypeDef(), NegativeHealthEvent = new MessageTypeDef(), PositiveEvent = new MessageTypeDef(); }
}
namespace LudeonTK
{
    public enum AllowedGameStates { PlayingOnMap }
    public class DebugActionAttribute : Attribute
    {
        public AllowedGameStates allowedGameStates;
        public DebugActionAttribute(string category, string name) { }
    }
}
namespace SexSlaveCraft
{
    public static class SSCDefOf { public static Verse.HediffDef SSC_PnaLoad, SSC_PnaRootedLoad, SSC_PnaAddiction, SSC_PersonalityExcreting, SSC_PersonalityExcreted_Done; public static RimWorld.NeedDef SSC_PnaNeed; public static RimWorld.TraitDef SexSlaveTrait; }
    public class Hediff_ChainOfSexSlave : Verse.Hediff
    {
        public Verse.Pawn LinkedPawn;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Hediff_ChainOfSexSlave AddToPawn(Verse.Pawn pawn, Verse.Pawn master)
        {
            var chain = pawn.health.hediffSet.hediffs.OfType<Hediff_ChainOfSexSlave>().FirstOrDefault();
            if (chain == null)
            {
                chain = new Hediff_ChainOfSexSlave { pawn = pawn, LinkedPawn = master, def = Program.ChainDef };
                pawn.health.AddHediff(chain);
            }
            else chain.LinkedPawn = master;
            return chain;
        }
    }
    public static class SSCRestrictionGameComponent
    {
        public static List<Verse.Pawn> Pawns = new List<Verse.Pawn>();
        public static List<Verse.Pawn> AllPawns() => Pawns.ToList();
    }
}
namespace UnityEngine
{
    public static class Mathf { public static float Max(float first,float second)=>Math.Max(first,second); public static float Clamp01(float value)=>Math.Clamp(value,0f,1f); }
}
