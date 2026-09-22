using System;
using System.Collections.Generic;
using System.Linq;
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

    public enum LookMode { Value }
    public static class Scribe_Collections
    {
        /// <summary>复制值列表模拟任务编号的存读档，避免测试保存节点与运行对象共享集合。</summary>
        public static void Look<T>(ref List<T> values, string key, LookMode mode)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.node[key] = values?.ToList();
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
                values = Scribe.node.TryGetValue(key, out object value) ? ((List<T>)value)?.ToList() : null;
        }
    }
    public static class Scribe_References
    {
        /// <summary>通过字典模拟已解析的游戏引用；用例分别调用加载和最终恢复阶段。</summary>
        public static void Look<T>(ref T value, string key) where T : class => Scribe_Values.Look(ref value, key);
    }
    public static class GenTypes
    {
        /// <summary>提供测试程序集内实际驱动列表，让生产动态补丁扫描真实声明方法。</summary>
        public static IEnumerable<Type> AllTypes => typeof(GenTypes).Assembly.GetTypes();
    }
    public static class Translation
    {
        /// <summary>返回键名作为可观察的提示，不在本生命周期模型模拟语言系统。</summary>
        public static string Translate(this string key, params object[] args) => key;
    }
}
