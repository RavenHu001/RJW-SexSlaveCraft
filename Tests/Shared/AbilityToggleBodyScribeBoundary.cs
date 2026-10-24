// AI 辅助生成：GUI 宿主只提供序列化类型边界，真实保存与人格流程继续由原生／人格专项覆盖。
namespace Verse
{
    /// <summary>提供实际能力状态类所需的序列化接口形状，不执行原版 Scribe 生命周期。</summary>
    public interface IExposable
    {
        /// <summary>保留生产类实现所需签名，具体 GUI 宿主不执行真实存档读写。</summary>
        void ExposeData();
    }
    public static class Scribe_Deep
    {
        /// <summary>GUI 宿主的空操作边界；不能用它证明字典已真实保存或加载。</summary>
        public static void Look<T>(ref T value, string key) where T : class, new() { }
    }
    public enum LookMode { Value }
    public static class Scribe_Collections
    {
        /// <summary>仅满足生产状态类的编译签名，不模拟缺键、复制或 XML 恢复规则。</summary>
        public static void Look<K,V>(ref System.Collections.Generic.Dictionary<K,V> value, string key, LookMode keys, LookMode values) { }
    }
}
