// 引擎边界：保存容器副本，不模拟真实跨引用 Scribe XML。
using System.Collections.Generic;
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public enum LookMode { Value }
    public static class Scribe_Deep
    {
        private static readonly Dictionary<string, object> saved = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key) where T : class, IExposable, new()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                saved[key] = value is SexSlaveCraft.SSCAbilityToggleState state ? state.Copy() : value;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = saved.TryGetValue(key, out object old) ?
                    (old is SexSlaveCraft.SSCAbilityToggleState state ? state.Copy() as T : old as T) : null;
        }
    }
    public static class Scribe_Collections
    {
        private static readonly Dictionary<string, object> saved = new Dictionary<string, object>();
        public static void Look<K,V>(ref Dictionary<K,V> value,string key,LookMode keys,LookMode values) where K : notnull
        {
            if (Scribe.mode == LoadSaveMode.Saving) saved[key] = value == null ? null : new Dictionary<K,V>(value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = saved.TryGetValue(key,out object old) && old != null ? new Dictionary<K,V>((Dictionary<K,V>)old) : null;
        }
    }
}
