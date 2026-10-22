// AI 辅助生成：实际 1.7 和生产程序集专项；无游戏 UI/整场启动宿主。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static class Entry
{
    private static int Main(string[] args)
    {
        // 从工作区构建记录读取真实引用目录，保证宿主加载的游戏与生产依赖来自本次构建环境。
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        var build = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        var dirs = new[] { game, Path.Combine(repo, "1.7/Assemblies") }.Concat(build.RootElement.GetProperty("references")
            .EnumerateArray().Select(x => Path.GetDirectoryName(x.GetProperty("path").GetString()))).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            // 只在已知目录逐项解析，找不到则交回默认加载器，不生成替代程序集。
            foreach (string dir in dirs)
            {
                string path = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        };
        // 解析器安装后再反射进入含游戏类型的方法，避免入口 JIT 提前解析尚未定位的引用。
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { repo, game });
    }
}

internal static partial class Program
{
    // 每例单独收集游戏错误；bootstrap 仅隔离缺少完整 Def 集的宿主预热阶段。
    private static int passed, failed;
    private static bool bootstrap;
    private static readonly List<string> errors = new();
    private static Harmony harmony;
    // 根对象和游戏时钟由夹具提供；下游人口、职位与缓存仍执行实际游戏/生产程序集。
    private static List<Map> maps = new();
    private static WorldObjectsHolder worldObjects;
    private static Faction player;
    private static int tick;
    private static string saveDirectory;
    // 工作量计数只观察入口次数，不替换被测返回值，也不把耗时波动当作性能结论。
    private static readonly Dictionary<string, int> calls = new();

    internal static int Run(string repo, string game)
    {
        // 输出实际构建身份；用户配置与 Scribe 文件全部落在工作区隔离目录。
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(game, "Version.txt")).Trim()}, MVID: {typeof(Ideo).Module.ModuleVersionId:N}");
        harmony = new Harmony("ssc.tests.culture.native");
        saveDirectory = Path.Combine(repo, ".builds/culture-step1/userdata");
        Directory.CreateDirectory(saveDirectory);
        Directory.CreateDirectory(Path.Combine(saveDirectory, "Config"));
        File.WriteAllText(Path.Combine(saveDirectory, "Config/ModsConfig.xml"),
            "<ModsConfigData><version>1.7.4985</version><activeMods/><knownExpansions/></ModsConfigData>");
        PatchGetter(typeof(GenFilePaths), nameof(GenFilePaths.SaveDataFolderPath), nameof(SaveDirectory));
        // 不枚举/启动用户的已安装模组；本宿主显式装载本专项需要的 Def。
        Patch(AccessTools.Method(typeof(ModLister), "RebuildModList"), nameof(SkipBootstrap));
        Patch(AccessTools.Method(typeof(ContentFinder<UnityEngine.Texture2D>), "Get"), nameof(NoTexture));
        // 只隔离世界启动、图形/语言/日志投递。实际 MapPawns、容器递归、角色资格、
        // 原版文化资格/生成/改革以及生产查询与缓存都不替换返回值。
        PatchGetter(typeof(Find), nameof(Find.Maps), nameof(Maps));
        PatchGetter(typeof(Find), nameof(Find.WorldObjects), nameof(Objects));
        PatchGetter(typeof(Faction), nameof(Faction.OfPlayer), nameof(Player));
        PatchGetter(typeof(TickManager), nameof(TickManager.TicksGame), nameof(Ticks));
        // 夹具布置容器时不投递地图/人物栏通知；容器存储和递归收集仍运行原版实现。
        Patch(AccessTools.Method(typeof(ThingOwner), "NotifyAdded", new[] { typeof(Thing) }), nameof(SkipBootstrap));
        // 存读档只验证文化/戒律身份，外观样式需完整美术 Def 集，单独隔离这一非被测子对象。
        Patch(AccessTools.Method(typeof(IdeoStyleTracker), "ExposeData"), nameof(SkipBootstrap));
        // 无界面宿主以 key 代替翻译结果，并将正式测试阶段的原版 Error 汇总为用例失败。
        foreach (var type in new[] { typeof(Translator), typeof(TranslatorFormattedStringExtensions) })
            foreach (var m in AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "Translate" && m.ReturnType == typeof(TaggedString)
                && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))) Patch(m, nameof(Translate));
        foreach (string name in new[] { "Error", "Warning", "Message" })
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(m, nameof(LogBoundary));
        // 先安装底层计数，避免创建上层 Harmony 包装时 JIT 将尚未计数的短属性内联。
        foreach (string property in new[] { "AllPawnsUnspawned", "FreeColonists", "ColonySubhumansControllable" })
            harmony.Patch(AccessTools.PropertyGetter(typeof(MapPawns), property), prefix: new HarmonyMethod(typeof(Program), nameof(CountCall)));
        // 上层 SSC 准备次数与下层原版递归次数分别计量，不能用一次 SSC 查询掩盖内部多次遍历。
        foreach (var type in new[] { typeof(SSCCulturePopulationUtility), typeof(SSCCultureLeaderUtility) })
            foreach (var m in AccessTools.GetDeclaredMethods(type).Where(m => m.IsPublic && m.IsStatic))
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(Program), nameof(CountCall)));
        try
        {
            // 提供本宿主必需的静态运行环境，不调用整场游戏启动或安装模组的自动扫描。
            SetStatic(typeof(Prefs), "data", new PrefsData());
            SetStatic(typeof(UnityData), "mainThreadId", Environment.CurrentManagedThreadId);
            // 把实际生产程序集登记到原版类型目录，Scribe 才能像游戏加载模组时一样解析组件类型。
            var pack = Bare<ModContentPack>(); pack.assemblies = new ModAssemblyHandler(pack);
            pack.assemblies.loadedAssemblies.Add(typeof(SSCCultureGameComponent).Assembly);
            ((List<ModContentPack>)AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null)).Add(pack);
            bootstrap = true;
            RuntimeHelpers.RunClassConstructor(typeof(SSCDefOf).TypeHandle);
            // Caravan 类型静态字段会查询未装载的界面贴图；仅在启动边界预热，查询不依赖它们。
            _ = new Caravan();
            bootstrap = false;
            // 两个实际生产 Hediff 类型配最小 Def 即可验证只读链路；不引入绑定事务的无关依赖。
            SSCDefOf.ChainOfSexSlave = new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave) };
            SSCDefOf.BridleOfSexSlave = new HediffDef { defName = "Hediff_BridleOfSexSlave", hediffClass = typeof(Hediff_BridleOfSexSlave) };
            SetStatic(typeof(ModsConfig), "ideologyActive", true);
            // 先加载和验证生产 Def，再执行依赖这些定义的人口、领袖、缓存与存读档契约。
            ResourceTests(repo, game);
            PopulationTests();
            LeaderTests();
            SnapshotTests();
            SerializationTests();
            // 先完成查询层与生成回归，再进入真实需求/thought/关系缓存和最终结算。
            FeedbackTests(repo, game);
            // 完整命名依赖在旧专项结束后装载，避免污染其固定 Def 数量与世界状态；四语逐次重建规则缓存。
            NamingTests(repo, game);
        }
        // 退出时撤销本宿主的补丁及当前游戏引用，不让一次失败污染后续进程内工作。
        finally { harmony.UnpatchAll(harmony.Id); Current.Game = null; }
        // 将逐例失败汇总到进程退出码，便于总回归入口识别该专项失败。
        Console.WriteLine($"{passed}/{passed + failed} cases passed");
        return failed == 0 ? 0 : 1;
    }

    // 夹具仅跳过依赖完整 Unity 场景的构造；字段名变化立即报错，不静默退化为假对象。
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object obj, string name, object value) => (AccessTools.Field(obj.GetType(), name)
        ?? throw new Exception("Missing fixture field " + obj.GetType() + "." + name)).SetValue(obj, value);
    private static void SetStatic(Type type, string name, object value) => AccessTools.Field(type, name).SetValue(null, value);
    // 断言抛给 Check 汇总；一例失败后继续其余用例，以保留不同契约的诊断结果。
    private static void Require(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, actual {actual}"); }
    private static void Check(string name, Action action)
    {
        // 除显式断言外，该例产生的原版 Error 也必须使其失败；上一例日志先清空。
        errors.Clear();
        try { action(); Require(errors.Count == 0, string.Join("\n", errors)); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
    }
    // 统一使用本宿主 Harmony 所有者安装边界补丁，以便 finally 只卸载自己的补丁。
    private static void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
    private static void PatchGetter(Type type, string property, string prefix) => Patch(AccessTools.PropertyGetter(type, property), prefix);
    // 这些前缀仅提供原版全局根和时钟；人口筛选与角色资格不在这一层伪造。
    private static bool Maps(ref List<Map> __result) { __result = maps; return false; }
    private static bool Objects(ref WorldObjectsHolder __result) { __result = worldObjects; return false; }
    private static bool Player(ref Faction __result) { __result = player; return false; }
    private static bool Ticks(ref int __result) { __result = tick; return false; }
    private static bool SaveDirectory(ref string __result) { __result = saveDirectory; return false; }
    // 图形、翻译、启动通知等边界没有被测逻辑；无界面宿主显式关闭这些调用。
    private static bool SkipBootstrap() => false;
    private static bool NoTexture(ref UnityEngine.Texture2D __result) { __result = null; return false; }
    private static bool Translate(string key, ref TaggedString __result) { __result = key; return false; }
    // 只在预热阶段忽略缺失完整 Def 集的 Error；正式用例的 Error 留给 Check 判失败。
    private static bool LogBoundary(MethodBase __originalMethod, object[] __args)
    { if (!bootstrap && __originalMethod.Name == "Error") errors.Add((string)__args[0]); return false; }
    // void 前缀仅累加观察值，原方法继续执行；计数器按用例或缓存创建边界清零。
    private static void CountCall(MethodBase __originalMethod)
    { string key = __originalMethod.Name; calls[key] = calls.GetValueOrDefault(key) + 1; }
}
