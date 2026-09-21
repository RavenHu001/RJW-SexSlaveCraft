using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;

// Only the game object/serialization boundaries are modeled. All rule decisions are production code.
namespace Verse
{
    public interface IExposable
    {
        /// <summary>为测试用序列化器提供对象字段的读写入口，签名与游戏接口保持一致。</summary>
        void ExposeData();
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> node = new Dictionary<string, object>();
    }
    public static class Scribe_Values
    {
        /// <summary>模拟简单字段的保存与加载；加载缺失字段时使用调用方提供的默认值。</summary>
        public static void Look<T>(ref T value, string key, T defaultValue = default)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.node[key] = value;
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.node.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
        }
    }
    public static class Scribe_Deep
    {
        /// <summary>通过独立字典节点模拟深度序列化，保留空对象语义，并在异常或完成后恢复父节点。</summary>
        public static void Look<T>(ref T value, string key) where T : class, IExposable, new()
        {
            Dictionary<string, object> parent = Scribe.node;
            try
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    if (value == null) { parent[key] = null; return; }
                    Scribe.node = new Dictionary<string, object>();
                    value.ExposeData();
                    parent[key] = Scribe.node;
                }
                else if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    if (!parent.TryGetValue(key, out object saved) || saved == null) { value = null; return; }
                    Scribe.node = (Dictionary<string, object>)saved;
                    value = new T();
                    value.ExposeData();
                }
            }
            finally { Scribe.node = parent; }
        }
    }
    public class Def
    {
        public string defName;
        /// <summary>提供无基础错误的 Def 测试边界，让生产子类单独校验自己的配置。</summary>
        public virtual IEnumerable<string> ConfigErrors() { yield break; }
    }
    public class DefModExtension
    {
        /// <summary>提供无基础错误的扩展测试边界，让生产装备扩展报告自身条目错误。</summary>
        public virtual IEnumerable<string> ConfigErrors() { yield break; }
    }
    public class ThingDef : Def
    {
        public List<DefModExtension> modExtensions = new List<DefModExtension>();
        /// <summary>返回装备定义中首个匹配类型的扩展，模拟规则解析器需要的游戏查询接口。</summary>
        public T GetModExtension<T>() where T : DefModExtension => modExtensions.OfType<T>().FirstOrDefault();
    }
    public static class DefDatabase<T> where T : Def
    {
        public static List<T> AllDefsListForReading = new List<T>();
    }
    public class Apparel { public ThingDef def; }
    public class Pawn_ApparelTracker { public List<Apparel> WornApparel = new List<Apparel>(); }
    public class Pawn
    {
        public string LabelShort;
        public Pawn BoundMaster;
        public bool HasBusState;
        public Pawn_ApparelTracker apparel = new Pawn_ApparelTracker();
        public CompSexSlaveTraining Training = new CompSexSlaveTraining();
        /// <summary>按请求类型返回测试训练组件；本替身不模拟其他游戏组件。</summary>
        public T TryGetComp<T>() where T : class => Training as T;
    }
}
namespace SexSlaveCraft
{
    public enum PawnIdentity { Unset, Slave, Master }
    public enum SexSlaveSpecializationType { None, Bus, Cow, PetCat, PetDog, PetRabbit }
    public partial class CompSexSlaveTraining
    {
        public PawnIdentity pawnIdentity;
        public SexSlaveSpecializationType specializationType;
        public Verse.Pawn selectedTrainer;
        public bool allowOthersForTrainingOrSex;
    }
    public partial class SSCSettings : Verse.IExposable
    {
        public bool enableSexSlaveProtectionRules = true;
        /// <summary>转调生产代码的新限制设置序列化入口，测试替身不重复实现其保存逻辑。</summary>
        public void ExposeData() { ExposeRestrictionSettings(); }
    }
    public static class SSCMod { public static SSCSettings settings = new SSCSettings(); }
    public static class SSCBondUtility
    {
        /// <summary>读取用例显式设置的绑定主人，不根据身份或指定训练者推断关系。</summary>
        public static Verse.Pawn GetBoundMaster(Verse.Pawn pawn) => pawn?.BoundMaster;
        /// <summary>按目标指向主人的单向引用检查绑定，确保反向请求不会被视为主人请求。</summary>
        public static bool IsBoundTo(Verse.Pawn slave, Verse.Pawn master) => slave != null && master != null && slave.BoundMaster == master;
    }
    public static class SSCIdentityUtility
    {
        /// <summary>读取测试组件的主人身份，用于首次绑定准备的资格判断。</summary>
        public static bool IsMaster(Verse.Pawn pawn) => pawn?.Training?.pawnIdentity == PawnIdentity.Master;
    }
    public static class BusSpecializationUtility
    {
        /// <summary>返回用例设置的巴士健康状态标记，模拟与所选特化方向独立的状态来源。</summary>
        public static bool HasAnyBusState(Verse.Pawn pawn) => pawn?.HasBusState == true;
    }
}
