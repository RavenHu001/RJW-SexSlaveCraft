using System;
using System.Collections.Generic;
using System.Linq;

namespace RimWorld
{
    public class Building_WorkTable { }
}

namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }

    public class Def { public string defName; }
    public sealed class RecipeDef : Def { public List<ThingDef> recipeUsers; }

    // 仅模拟配方关联与已生成缓存；不模拟原版 XML 加载或工作执行。
    public sealed class ThingDef : Def
    {
        public Type thingClass;
        public List<RecipeDef> recipes;
        public List<RecipeDef> CachedRecipes;
        public List<RecipeDef> AllRecipes => CachedRecipes ??= (recipes ?? new List<RecipeDef>())
            .Concat(DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r.recipeUsers?.Contains(this) == true))
            .ToList();
    }

    public static class DefDatabase<T> where T : Def
    {
        public static readonly List<T> AllDefsListForReading = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefsListForReading.Find(d => d.defName == name);
    }

    public static class LongEventHandler
    {
        public static readonly Queue<Action> QueuedActions = new Queue<Action>();
        public static void ExecuteWhenFinished(Action action) => QueuedActions.Enqueue(action);
    }
}
