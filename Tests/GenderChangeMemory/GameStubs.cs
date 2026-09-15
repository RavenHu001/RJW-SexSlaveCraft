using System.Collections.Generic;

// 直接链接生产手术代码；仅为身体调整、记忆添加及定义查询提供最小宿主。
// 查询缺失定义返回空值，记忆入口访问 IsMemory，均对应本机 RimWorld 1.6 行为。
namespace Verse
{
    public enum Gender { None, Male, Female }
    public class Def { public string defName; }
    public class Thing { public object def; }
    public class BodyPartRecord { }
    public class Hediff { }
    public class Pawn : Thing
    {
        public Gender gender = Gender.Male;
        public NeedsTracker needs = new NeedsTracker();
        public HealthTracker health = new HealthTracker();
        public StoryTracker story = new StoryTracker();
        public StyleTracker style = new StyleTracker();
        public DrawTracker Drawer = new DrawTracker();
        public int RebuiltParts;
    }
    public class NeedsTracker { public RimWorld.Need_Mood mood = new RimWorld.Need_Mood(); }
    public class HealthTracker
    {
        public object hediffSet = new object();
        /// <summary>接收生产器官调整流程的移除调用；本套件不模拟完整健康系统。</summary>
        public void RemoveHediff(Hediff hediff) { }
    }
    public class StoryTracker { public RimWorld.BodyTypeDef bodyType = RimWorld.BodyTypeDefOf.Male; }
    public class StyleTracker { public object beardDef; }
    public class DrawTracker { public PawnRenderer renderer = new PawnRenderer(); }
    public class PawnRenderer
    {
        public int Refreshes;
        /// <summary>记录外观刷新，供用例确认缺失记忆定义不影响已完成的身体调整。</summary>
        public void SetAllGraphicsDirty() { Refreshes++; }
    }
    public static class PortraitsCache
    {
        /// <summary>接收头像刷新通知；测试不绘制游戏头像。</summary>
        public static void SetDirty(Pawn pawn) { }
    }
    public static class ModLister
    {
        /// <summary>本宿主按未安装异种框架处理，不测试 HAR 的反射体型兼容。</summary>
        public static object GetActiveModWithIdentifier(string id) => null;
    }
    public static class Log
    {
        public static readonly List<string> Errors = new List<string>();
        /// <summary>收集定义缺失等错误，验证空值保护没有隐藏原有诊断。</summary>
        public static void Error(string message) { Errors.Add(message); }
    }
    public static class DefDatabase<T> where T : Def
    {
        public static readonly Dictionary<string, T> Definitions = new Dictionary<string, T>();
        public static int Queries;
        /// <summary>按名称查询定义；缺失时按参数记录错误并返回空值，模拟原版查询行为。</summary>
        public static T GetNamed(string name, bool errorOnFail = true)
        {
            Queries++;
            if (Definitions.TryGetValue(name, out T def)) return def;
            if (errorOnFail) Log.Error("Failed to find " + typeof(T).Name + " named " + name);
            return null;
        }
    }
}
namespace RimWorld
{
    public class Bill { }
    public class BodyTypeDef { }
    public static class BodyTypeDefOf { public static BodyTypeDef Male = new BodyTypeDef(), Female = new BodyTypeDef(); }
    public static class BeardDefOf { public static object NoBeard = new object(); }
    public static class TaleDefOf { public static object DidSurgery = new object(); }
    public static class TaleRecorder
    {
        /// <summary>接收手术故事记录；本套件不模拟故事数据库。</summary>
        public static void RecordTale(object def, Verse.Pawn surgeon, Verse.Pawn patient) { }
    }
    public class Recipe_Surgery
    {
        public static bool SurgeryFails;
        /// <summary>提供原版可用性检查的通过结果，以运行生产代码的性别筛选。</summary>
        public virtual bool AvailableOnNow(Verse.Thing thing, Verse.BodyPartRecord part = null) => true;
        /// <summary>声明生产手术的执行入口；具体逻辑由链接的生产子类实现。</summary>
        public virtual void ApplyOnPawn(Verse.Pawn pawn, Verse.BodyPartRecord part, Verse.Pawn billDoer,
            List<Verse.Thing> ingredients, Bill bill) { }
        /// <summary>返回用例设置的手术失败结果，验证失败后不再添加术后记忆。</summary>
        protected bool CheckSurgeryFail(Verse.Pawn surgeon, Verse.Pawn patient,
            List<Verse.Thing> ingredients, Verse.BodyPartRecord part, Bill bill) => SurgeryFails;
    }
    public class ThoughtDef : Verse.Def
    {
        public float durationDays, baseMoodEffect;
        public int stackLimit;
        /// <summary>按本用例定义中的正持续时间识别记忆，模拟原版 IsMemory 的对应分支。</summary>
        public bool IsMemory => durationDays > 0;
    }
    public class Need_Mood { public ThoughtHandler thoughts = new ThoughtHandler(); }
    public class ThoughtHandler { public MemoryThoughtHandler memories = new MemoryThoughtHandler(); }
    public class MemoryThoughtHandler
    {
        public readonly List<ThoughtDef> Memories = new List<ThoughtDef>();
        /// <summary>先访问传入定义的记忆属性，再记录添加；传入空值会暴露与原版相同的异常。</summary>
        public void TryGainMemory(ThoughtDef def)
        {
            if (def.IsMemory) Memories.Add(def);
        }
    }
}
namespace rjw
{
    public static class PawnParts
    {
        /// <summary>提供空的器官列表，允许生产替换流程执行而不模拟 RJW 健康状态。</summary>
        public static List<Verse.Hediff> GetGenitalsList(this Verse.Pawn pawn) => new List<Verse.Hediff>();
        /// <summary>提供空的胸部列表，供生产替换流程调用。</summary>
        public static List<Verse.Hediff> GetBreastList(this Verse.Pawn pawn) => new List<Verse.Hediff>();
    }
    public static class Genital_Helper
    {
        /// <summary>宿主未生成真实器官状态，因此不将占位状态识别为男性器官。</summary>
        public static bool is_penis(Verse.Hediff hediff) => false;
        /// <summary>宿主未生成真实器官状态，因此不将占位状态识别为女性器官。</summary>
        public static bool is_vagina(Verse.Hediff hediff) => false;
    }
    public static class SexPartAdder
    {
        /// <summary>记录生产代码请求重建生殖器官。</summary>
        public static void add_genitals(Verse.Pawn pawn, Verse.Gender gender) { pawn.RebuiltParts++; }
        /// <summary>记录生产代码请求重建胸部。</summary>
        public static void add_breasts(Verse.Pawn pawn, Verse.Gender gender) { pawn.RebuiltParts++; }
        /// <summary>记录生产代码请求补全肛门器官。</summary>
        public static void add_anus(Verse.Pawn pawn, Verse.Gender gender) { pawn.RebuiltParts++; }
    }
}
