using System;
using System.Collections.Generic;

namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartupAttribute : Attribute { }

    public enum Intelligence { Animal, ToolUser, Humanlike }

    public sealed class RaceProperties
    {
        public Intelligence intelligence;
    }

    public class CompProperties
    {
        public Type compClass;
    }

    public class ThingDef
    {
        public string defName;
        public RaceProperties race;
        public List<CompProperties> comps;
        public List<Type> inspectorTabs;
        public List<InspectTabBase> inspectorTabsResolved;
        public int ResolveCalls;

        /// <summary>记录 Def 解析次数，供测试检测重复初始化；不模拟游戏或 HAR 的完整解析过程。</summary>
        public virtual void ResolveReferences() { ResolveCalls++; }
    }

    public static class DefDatabase<T>
    {
        public static readonly List<T> AllDefsListForReading = new List<T>();
    }

    public abstract class InspectTabBase { }

    public static class InspectTabManager
    {
        private static readonly Dictionary<Type, InspectTabBase> Instances =
            new Dictionary<Type, InspectTabBase>();

        /// <summary>按分页类型缓存并返回同一实例，模拟游戏共享分页管理器的身份语义。</summary>
        public static InspectTabBase GetSharedInstance(Type type)
        {
            if (!Instances.TryGetValue(type, out InspectTabBase tab))
            {
                tab = (InspectTabBase)Activator.CreateInstance(type);
                Instances.Add(type, tab);
            }
            return tab;
        }
    }

    public static class LongEventHandler
    {
        public static readonly Queue<Action> QueuedActions = new Queue<Action>();

        /// <summary>捕获生产静态构造安排的任务，由测试显式执行，以检查延迟注入行为。</summary>
        public static void QueueLongEvent(Action action, string textKey, bool doAsynchronously,
            Action<Exception> exceptionHandler)
        {
            QueuedActions.Enqueue(action);
        }
    }

    public static class Log
    {
        public static readonly List<string> Warnings = new List<string>();
        /// <summary>保存注入警告，让场景执行器将原本被生产代码捕获的异常判为测试失败。</summary>
        public static void Warning(string message) { Warnings.Add(message); }

        /// <summary>忽略正常运行日志，避免其干扰测试结果输出。</summary>
        public static void Message(string message) { }
    }
}

namespace SexSlaveCraft
{
    public sealed class CompSexSlaveTraining { }

    public sealed class CompProperties_SexSlaveTraining : Verse.CompProperties
    {
        /// <summary>指定测试训练组件类型，使生产注入器能够执行与游戏一致的组件防重检查。</summary>
        public CompProperties_SexSlaveTraining() { compClass = typeof(CompSexSlaveTraining); }
    }

    public sealed class ITab_SexSlaveTraining : Verse.InspectTabBase { }
}
