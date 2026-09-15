using System;
using System.Collections.Generic;
using System.Linq;

namespace Verse
{
    public class Def { public string defName; }
    public class Thing { }
    public class Building : Thing { }
    public class ThingComp { }
    public class Pawn : Thing
    {
        public PawnJobTracker jobs = new PawnJobTracker();
        public List<ThingComp> AllComps = new List<ThingComp>();
    }
    public class PawnJobTracker { public object curDriver; }

    public static class DefDatabase<T> where T : Def
    {
        // 每个封闭泛型类型单独持有定义，基类数据库不汇总派生类数据库。
        public static readonly List<T> AllDefsListForReading = new List<T>();
        /// <summary>返回当前定义类型的数据，与游戏的泛型数据库隔离规则一致。</summary>
        public static IEnumerable<T> AllDefs => AllDefsListForReading;
    }

    public static class GenDefDatabase
    {
        /// <summary>按运行时类型读取泛型数据库；对应本机游戏 API 的反射读取及 Cast 行为。</summary>
        public static IEnumerable<Def> GetAllDefsInDatabaseForDef(Type defType)
        {
            var values = (System.Collections.IEnumerable)typeof(DefDatabase<>).MakeGenericType(defType)
                .GetProperty("AllDefs").GetValue(null);
            return values.Cast<Def>();
        }
    }

    public static class GenCollection
    {
        /// <summary>测试中固定选择首项，便于断言生产筛选后的候选；不测试游戏随机分布。</summary>
        public static T RandomElement<T>(this List<T> values) => values[0];
    }

    public static class Log
    {
        /// <summary>忽略与备用查找无关的阶段信息。</summary>
        public static void Message(string message) { }
    }
}

namespace rjw
{
    public class SexProps { }
    public static class xxx
    {
        public enum rjwSextype { Handjob, Footjob, Sixtynine, Boobjob, Anal, Vaginal }
    }
    public class JobDriver_SexBaseReciever
    {
        public List<Verse.Pawn> parteners = new List<Verse.Pawn>();
    }
}

namespace SexSlaveCraft
{
    public static class RJWSexPropsUtility
    {
        /// <summary>阶段数据适配占位；本套件仅执行备用动画入口。</summary>
        public static void ApplySexType(rjw.SexProps props, Verse.Pawn initiator, Verse.Pawn receiver,
            rjw.xxx.rjwSextype type, string interaction) { }
    }

    public static class SSCLog
    {
        public static readonly List<string> Warnings = new List<string>();
        public static readonly List<string> Errors = new List<string>();
        /// <summary>忽略详细日志，只对警告和错误进行断言。</summary>
        public static void Verbose(string message) { }
        /// <summary>记录生产入口发出的警告。</summary>
        public static void WarningImportant(string message) => Warnings.Add(message);
        /// <summary>记录生产入口捕获的异常。</summary>
        public static void Error(string message) => Errors.Add(message);
    }
}
