using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;

namespace Verse
{
    public interface IExposable
    {
        /// <summary>提供配置序列化签名；实际序列化另由核心套件覆盖。</summary>
        void ExposeData();
    }
    public class Def
    {
        public string defName;
        /// <summary>提供定义校验基类，不添加模拟错误。</summary>
        public virtual IEnumerable<string> ConfigErrors() { yield break; }
    }
    public class DefModExtension
    {
        /// <summary>提供装备扩展校验基类。</summary>
        public virtual IEnumerable<string> ConfigErrors() { yield break; }
    }
    public class ThingDef : Def
    {
        public List<DefModExtension> modExtensions = new List<DefModExtension>();
        /// <summary>读取测试装备上真实的规则扩展。</summary>
        public T GetModExtension<T>() where T : DefModExtension => modExtensions.OfType<T>().FirstOrDefault();
    }
    public class Apparel { public ThingDef def = new ThingDef(); }
    public class ApparelTracker { public List<Apparel> WornApparel = new List<Apparel>(); }
    public static class Scribe_Deep
    {
        /// <summary>只提供编译边界；本套件不宣称验证深度序列化。</summary>
        public static void Look<T>(ref T value, string key) where T : class, IExposable, new() { }
    }
}
namespace SexSlaveCraft
{
    public enum SexSlaveSpecializationType { None, Bus, Cow, PetCat, PetDog, PetRabbit }
    public partial class CompSexSlaveTraining
    {
        public SSCRestrictionConfig restrictionConfig = new SSCRestrictionConfig();
        public SexSlaveSpecializationType specializationType;
    }
    public class Settings
    {
        public bool enableSexSlaveProtectionRules = true, enableSpecializationRestrictionOverrides = true;
        public SSCRestrictionRules restrictionDefaults = new SSCRestrictionRules();
    }
    public static class SSCMod { public static Settings settings = new Settings(); }
}
