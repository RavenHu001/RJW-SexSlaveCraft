using SexSlaveCraft;
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public static class Scribe_Deep
    {
        public static void Look<T>(ref T value,string key) where T : class, IExposable, new() { }
    }
    public static partial class Scribe_Collections
    {
        public static void Look<K,V>(ref Dictionary<K,V> value,string key,LookMode keys,LookMode values) where K : notnull { }
    }
    public static class DefDatabase<T> where T : class
    {
        public static readonly List<T> AllDefs = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefs.FirstOrDefault(d => (d as HediffDef)?.defName == name);
    }
}
