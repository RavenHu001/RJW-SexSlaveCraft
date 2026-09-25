// Headless stand-ins for the game host. Progression, decay and chain mutation are
// compiled from the production files; Unity, UI, traits and conversion are not simulated.
// 中文说明：这里只补足生产源码编译和执行所需的游戏宿主类型。
// 不能把它加入模组主项目：它定义了与 Verse/RimWorld/UnityEngine 同名的测试类型。
// 锁链增量、退阶、装备底线和需求刷新仍调用真正的生产类；下方替身不复制这些算法。
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UnityEngine
{
    // 测试范围只需要基础数值运算。Rect 仅用于满足需求 UI 方法的签名，不执行绘制。
    public struct Rect { }
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Round(float value) => (float)Math.Round(value);
    }
}

namespace Verse
{
    // 最小的 Def 容器：Program 从发布 XML 载入锁链阶段后注册到这里。
    // 不执行 RimWorld 的补丁、跨 Def 引用解析和启动回调，因此不验证完整模组加载。
    public class Def { public string defName; public string LabelCap; public string description; }
    public class NeedDef : Def { }
    // 与本机 RimWorld HediffDef 构造器默认值一致。未声明 maxSeverity 的 XML
    // 不会自动把严重度限制在 100%；不能在替身中人为添加这个上限。
    public class HediffDef : Def
    {
        public float initialSeverity = 0.5f, maxSeverity = float.MaxValue;
        public List<HediffStage> stages;
    }
    public class HediffStage { public float minSeverity; }
    public static class DefDatabase<T> where T : Def
    {
        public static readonly List<T> AllDefs = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefs.FirstOrDefault(d => d.defName == name);
    }
    // Thing/Pawn 只保留本次生产入口会访问的字段和组件查询。
    // Pawn 默认视为已是原版奴隶，使测试聚焦锁链成长，不进入原版转奴调用。
    public class Thing
    {
        public bool Destroyed;
        public readonly List<ThingComp> comps = new List<ThingComp>();
        public T TryGetComp<T>() where T : ThingComp => comps.OfType<T>().FirstOrDefault();
    }
    public class Pawn : Thing
    {
        public bool Discarded, Dead, IsSlave = true, IsColonist, IsPrisonerOfColony;
        public string LabelShort = "test pawn", LabelShortCap = "Test pawn";
        public NeedsTracker needs = new NeedsTracker();
        public HealthTracker health = new HealthTracker();
        public ApparelTracker apparel = new ApparelTracker();
        public RelationsTracker relations = new RelationsTracker();
        public SkillTracker skills = new SkillTracker();
        public GuestTracker guest = new GuestTracker();
        public Faction Faction = new Faction();
    }
    public class NeedsTracker
    {
        public readonly List<RimWorld.Need> allNeeds = new List<RimWorld.Need>();
        public T TryGetNeed<T>() where T : RimWorld.Need => allNeeds.OfType<T>().FirstOrDefault();
    }
    // 用内存列表保存真实 Hediff 实例；不模拟游戏 tick 调度、健康缓存或添加/删除通知。
    // Program 显式调用 NeedInterval，保证“结算后刷新”的先后顺序可以被确定地复现。
    public class HealthTracker
    {
        public HediffSet hediffSet = new HediffSet();
        public void AddHediff(Hediff h) => hediffSet.hediffs.Add(h);
    }
    public class HediffSet
    {
        public readonly List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
    }
    public class ApparelTracker { public List<RimWorld.Apparel> WornApparel = new List<RimWorld.Apparel>(); }
    public class RelationsTracker { public int OpinionOf(Pawn other) => 0; }
    public class SkillTracker { public SkillRecord GetSkill(object def) => new SkillRecord(); }
    public class SkillRecord { public int Level; }
    public class GuestTracker { public bool IsSlave = true; public float will; }
    public class Faction { public string Name = "test faction"; }
    public struct DamageInfo { }
    public class Hediff
    {
        public HediffDef def;
        public Pawn pawn;
        public float Severity;
        public virtual string LabelBase => "chain";
        public virtual bool ShouldRemove => false;
        public virtual void PostAdd(DamageInfo? info) { }
        public virtual void PostRemoved() { }
    }
    public class HediffWithComps : Hediff { public List<HediffComp> comps = new List<HediffComp>(); }
    public class HediffWithTarget : HediffWithComps { public Thing target; }
    public class HediffComp { }
    // 创建真实 Hediff_ChainOfSexSlave；初始严重度、上限和阶段阈值均由 Program
    // 从 XML 读取，缺失值沿用上面的游戏默认值，避免宿主替身改变复现条件。
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn) =>
            new SexSlaveCraft.Hediff_ChainOfSexSlave { def = def, pawn = pawn, Severity = def.initialSeverity };
    }
    public class ThingComp { public Thing parent; public CompProperties props; }
    public class CompProperties { public Type compClass; }
    public static class Extensions
    {
        public static bool DestroyedOrNull(this Thing thing) => thing == null || thing.Destroyed;
        public static string ToStringPercent(this float value) => value.ToString("P0");
    }
    // 这些空实现仅满足生产类的存档接口。测试没有调用 ExposeData，
    // 所以此测试集不能证明读写存档或旧档迁移正确。
    public enum LoadSaveMode { PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values { public static void Look(ref float value, string name, float defaultValue) { } }
    public static class Rand { public static bool Chance(float chance) => false; }
}

namespace RimWorld
{
    // 需求基类保存最小内存状态，生产 Need_Corruption 在其上运行实际刷新逻辑。
    // 不模拟真实 Need 的冻结原因、需求显示或游戏内部事件。
    public class Need
    {
        protected Pawn pawn;
        protected List<float> threshPercents;
        public float CurLevel;
        public float CurLevelPercentage => CurLevel;
        public virtual float MaxLevel => 1f;
        public bool IsFrozen;
        public NeedDef def = new NeedDef();
        public Need(Pawn pawn) { this.pawn = pawn; }
        public virtual void NeedInterval() { }
        public virtual void SetInitialLevel() { }
        public virtual string GetTipString() => "";
        public virtual void ExposeData() { }
        public virtual void DrawOnGUI(UnityEngine.Rect rect, int maxThresholdMarkers = int.MaxValue,
            float customMargin = -1f, bool drawArrows = true, bool doTooltip = true,
            UnityEngine.Rect? rectForTooltip = null, bool drawLabel = true) { }
    }
    public class Apparel : Thing { public Pawn Wearer; }
    public class ThoughtDef : Def { }
    public static class SkillDefOf { public static object Social; }
    public static class GenGuest { public static bool TryEnslavePrisoner(Pawn master, Pawn slave) => false; }
    public static class SoundDefOf { public static object TechprintApplied; }
}
namespace Verse.Sound
{
    // 声音是结算副作用，与数值回归无关，在无游戏环境中忽略。
    public static class SoundExtensions { public static void PlayOneShot(this object sound, Pawn pawn) { } }
}
namespace rjw
{
    // 本测试不验证 RJW 评分与部位尺寸，返回空部位数据以满足共享工具类依赖。
    // 未添加 Need_Sex 时，生产需求刷新会采用它原本定义的 0.5 缺省值。
    public static class GenderHelper { }
    public class Need_Sex : RimWorld.Need { public Need_Sex(Pawn pawn) : base(pawn) { } }
    public static class Genital_Helper
    {
        public static List<Hediff> get_AllPartsHediffList(Pawn pawn) => new List<Hediff>();
        public static bool is_penis(Hediff h) => false;
        public static bool is_vagina(Hediff h) => false;
    }
    public static class PartSizeCalculator
    {
        public static bool TryGetLength(Hediff h, out float value) { value = 0; return false; }
        public static bool TryGetGirth(Hediff h, out float value) { value = 0; return false; }
    }
}
namespace SexSlaveCraft
{
    // 只替代未纳入本次数值测试的其他 SSC 服务；设置在每个用例开始时重新创建。
    public class CompSexSlaveTraining : ThingComp { public float lastTrainingScore; }
    public class TestSettings
    {
        public bool useOldScoring, enableCorruptionDecay = true, enableRitualEnslavement;
        public float corruptionDecayPerDay = 0.02f;
    }
    public static class SSCMod { public static TestSettings settings = new TestSettings(); }
    // 数值套件只验证锁链计算；跨门槛通知后的状态转换在 TrainerIdentity 中验证。
    public static class TrainerSpecializationLifecycle
    {
        public static void Notify(Pawn pawn) { }
    }
    // Trait 同步被忽略，GetSexSlaveStage 仅供装备替身判断“有锁链”。
    // 实际阶段断言使用生产 TrainingOutcomeUtility，不能把这里的返回值当作阶段算法验证。
    public static class SSCIdentityUtility
    {
        public static void SyncSexSlaveTraitFromHighestCorruption(Pawn pawn) { }
        public static int GetSexSlaveStage(Pawn pawn) => SSCBondUtility.GetChain(pawn) != null ? 1 : 0;
    }
    public class Hediff_BridleOfSexSlave
    {
        public List<Pawn> targets = new List<Pawn>();
        public void RemoveTarget(Pawn pawn) => targets.Remove(pawn);
    }
    // 简化主从服务保留锁链查找与复用，并把创建操作交给真实 AddToPawn。
    // 不模拟主从双方身份校验、换主保护或缰绳同步；这些行为不在本测试的覆盖声明中。
    public static class SSCBondUtility
    {
        public static Hediff_ChainOfSexSlave GetChain(Pawn pawn) =>
            pawn?.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.ChainOfSexSlave) as Hediff_ChainOfSexSlave;
        public static Hediff_BridleOfSexSlave GetBridle(Pawn pawn) => null;
        public static bool Bind(Pawn master, Pawn slave) => Hediff_ChainOfSexSlave.AddToPawn(slave, master) != null;
    }
    // 评分、转奴概率、日志和本地化不是本次待测逻辑，仅提供无外部副作用的占位接口。
    // “legacy”用例验证的是 Need_Corruption 的旧模式分支，并非旧评分公式本身。
    public static class LegacyTrainingUtility
    {
        public static float GetScore_Legacy(Pawn master, Pawn slave) => 0f;
        public static float CalculateCorruptionGain_Legacy(float score, int level) => 0f;
    }
    public static class WillReductionUtility
    {
        public static float CalculateEnslaveChance(Pawn pawn, float score, float corruption, bool ritual, out string detail)
        { detail = ""; return 0f; }
    }
    public static class SSCLog
    {
        public static void Important(string message) { }
        public static void WarningImportant(string message) { }
    }
    public static class ModLog { public static void Message(string message) { } }
    public static class SSCDefOf
    {
        public static HediffDef ChainOfSexSlave;
        public static RimWorld.ThoughtDef SSC_Ritual_Terrible, SSC_Ritual_Boring, SSC_Ritual_Good, SSC_Ritual_Best;
        public static RimWorld.ThoughtDef SSC_Ritual_Euphoria_1, SSC_Ritual_Euphoria_2, SSC_Ritual_Euphoria_3, SSC_Ritual_Euphoria_4;
    }
    public static class Strings
    {
        public static string Stage_Extreme, Stage_Severe, Stage_Moderate, Stage_Minor, Stage_Slight, Stage_Stable;
        public static string Bond_BridleAdded(string master, string slave) => "bridle added";
        public static string Bond_ChainAdded(string slave, string master) => "chain added";
        public static string Ritual_Enslaved(string pawn) => "converted";
        public static string Ritual_EnslaveFailed(string pawn) => "failed";
        public static string Chain_LabelWithPawn(string label, string pawn) => label;
        public static string Need_TipFormat(params string[] text) => "";
    }
}
