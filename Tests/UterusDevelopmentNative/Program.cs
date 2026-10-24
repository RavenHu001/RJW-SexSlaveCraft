// AI 辅助生成：真实原版 Hediff/生育因子/Scribe；隔离图形及整场健康状态变化。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Entry
{
    /// <summary>先定位实际依赖程序集，再进入含游戏类型的测试入口，避免 JIT 提前解析引用。</summary>
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        using var build = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        var dirs = new[] { game, Path.Combine(repo, "1.7/Assemblies") }.Concat(build.RootElement.GetProperty("references")
            .EnumerateArray().Select(x => Path.GetDirectoryName(x.GetProperty("path").GetString()))).ToArray();
        // 只搜索构建记录中的目录；不复制游戏 DLL，不生成生产依赖替身。
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string dir in dirs)
            {
                string path = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { repo, game });
    }
}

internal static partial class Program
{
    private static int passed, failed, nextId = 1000000, notifications;
    private static bool bootstrap = true;
    private static readonly List<string> errors = new();
    private static string saveDirectory;
    private static HediffDef development;
    private static ThingDef human;

    /// <summary>建立隔离原生宿主，加载生产定义并验证身体状态及正常手术，最后释放全局上下文。</summary>
    internal static int Run(string repo, string game)
    {
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(game, "Version.txt")).Trim()}, MVID: {typeof(Pawn).Module.ModuleVersionId:N}");
        using var harmony = new HarmonyScope();
        // 所有宿主边界补丁都归属于本次测试，离开入口时统一卸载。
        void Patch(MethodBase method, string prefix) => harmony.Value.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        // 原版文件与配置 API 指向工作区隔离目录，不读写玩家的实际存档与模组配置。
        saveDirectory = Path.Combine(repo, ".builds/uterus-development-batch1/userdata");
        Directory.CreateDirectory(Path.Combine(saveDirectory, "Config"));
        File.WriteAllText(Path.Combine(saveDirectory, "Config/ModsConfig.xml"),
            "<ModsConfigData><version>1.7.4985</version><activeMods/><knownExpansions/></ModsConfigData>");
        Patch(AccessTools.PropertyGetter(typeof(GenFilePaths), nameof(GenFilePaths.SaveDataFolderPath)), nameof(SaveDirectory));
        Patch(AccessTools.Method(typeof(ModLister), "RebuildModList"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ContentFinder<UnityEngine.Texture2D>), "Get"), nameof(NoTexture));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange)), nameof(Skip));
        // 保留 Notify_HediffChanged -> DirtyCache；只计数并隔离完整健康/心情通知下游。
        harmony.Value.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged)),
            prefix: new HarmonyMethod(typeof(Program), nameof(CountNotification)));
        Patch(AccessTools.Method(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty)), nameof(Skip));
        // 收集预热完成后的游戏错误，使被原版捕获的 XML／Scribe 异常也能令用例失败。
        foreach (var name in new[] { "Error", "Warning", "Message" })
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Log)).Where(m => m.Name == name && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(method, nameof(LogBoundary));
        try
        {
            // 只初始化本批需要的时钟、唯一 ID、身体与 Def；不启动地图、AI 或完整游戏。
            SetStatic(typeof(UnityData), "mainThreadId", Environment.CurrentManagedThreadId);
            SetStatic(typeof(Prefs), "data", new PrefsData());
            var root = Bare<Game>(); root.tickManager = new TickManager(); root.uniqueIDsManager = new UniqueIDsManager();
            Current.Game = root;
            var pack = Bare<ModContentPack>(); pack.assemblies = new ModAssemblyHandler(pack);
            pack.assemblies.loadedAssemblies.Add(typeof(SSCUterusDevelopmentUtility).Assembly);
            pack.assemblies.loadedAssemblies.Add(typeof(Program).Assembly);
            ((List<ModContentPack>)AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null)).Add(pack);
            SetStatic(typeof(ModsConfig), "biotechActive", true);
            BodyPartDefOf.Torso = new BodyPartDef { defName = "Torso", label = "躯干", hitPoints = 40 };
            DefDatabase<BodyPartDef>.Add(BodyPartDefOf.Torso);
            var body = new BodyDef { defName = "UterusTestBody", corePart = new BodyPartRecord { def = BodyPartDefOf.Torso } };
            body.ResolveReferences(); DefDatabase<BodyDef>.Add(body);
            human = Bare<ThingDef>(); human.defName = "UterusTestHuman"; human.category = ThingCategory.Pawn;
            human.race = new RaceProperties { intelligence = Intelligence.Humanlike, body = body };
            DefDatabase<ThingDef>.Add(human);
            HediffDefOf.Pregnant = PregnancyDef("Pregnant");
            HediffDefOf.PregnantHuman = PregnancyDef("PregnantHuman");
            // RJW 类初始化会读取遗传排除表；本批只使用其妊娠登记，不执行遗传逻辑。
            DefDatabase<rjw.StringListDef>.Add(new rjw.StringListDef { defName = "NonInheritedTraits", strings = new() });
            bootstrap = false;
            // 生产 XML 仍走原版字段解析与交叉引用，防止手工抄写配置掩盖资源错误。
            Check("production XML uses native loader, independent fertile stage and Chinese placeholders", () =>
            {
                var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_UterusDevelopment.xml"));
                development = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode("HediffDef"), false);
                DefDatabase<HediffDef>.Add(development); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                Equal(SSCUterusDevelopmentUtility.DefName, development.defName);
                Equal(typeof(Hediff_SSCUterusDevelopment), development.hediffClass);
                Near(.01f, development.initialSeverity); Near(1f, development.maxSeverity);
                Require(development.label.Contains("子宫开发")); Require(development.description.Contains("正式文案待定"));
                Near(0, development.stages[0].fertilityFactor); Near(0, development.stages[1].fertilityFactor);
                Near(2.5f, development.GetModExtension<SSCUterusDevelopmentExtension>().fertileStage.fertilityFactor);
            });
            if (development == null) return 1;
            // 独立 CLR 不加载 RJW 的旧版 UI 类型。按真实关联属性提供本批两类妊娠登记数据；
            // IsPregnant/KnownPregnancies/HasHediff 的实际查询仍执行 RJW/原版代码。
            var registry = new Dictionary<Type, string>();
            foreach (var type in new[] { typeof(rjw.Hediff_HumanlikePregnancy), typeof(rjw.Hediff_BestialPregnancy) })
                registry[type] = type.GetCustomAttributes(typeof(rjw.RJWAssociatedHediffAttribute), false)
                    .Cast<rjw.RJWAssociatedHediffAttribute>().Single().defName;
            SetStatic(typeof(rjw.Hediff_BasePregnancy), "_hediffOfClass", registry);
            foreach (string name in rjw.Hediff_BasePregnancy.KnownPregnancies())
                if (DefDatabase<HediffDef>.GetNamedSilentFail(name) == null) PregnancyDef(name);
            // 先验证正常行为，再覆盖重复／坏值与真实存档生命周期。
            BodyAndFertility();
            MergeAndNormalization();
            SaveLoad();
            SurgeryTests(repo, game, harmony.Value);
            GrowthTests(repo, harmony.Value);
        }
        finally { Scribe.mode = LoadSaveMode.Inactive; Current.Game = null; }
        Console.WriteLine($"{passed}/{passed + failed} cases passed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>提供妊娠判定所需的定义数据，不执行孕育、遗传或分娩逻辑。</summary>
    private static HediffDef PregnancyDef(string name)
    {
        var def = new HediffDef { defName = name, label = name, hediffClass = typeof(Hediff), maxSeverity = 1 };
        DefDatabase<HediffDef>.Add(def); return def;
    }
    /// <summary>为每例创建独立 ID 的真实 Pawn 和健康列表，不依赖身份组件或绑定关系。</summary>
    private static Pawn NewPawn()
    {
        var pawn = new Pawn { def = human, thingIDNumber = ++nextId, Name = new NameSingle("开发测试") };
        pawn.health = new Pawn_HealthTracker(pawn);
        pawn.needs = new Pawn_NeedsTracker(pawn);
        return pawn;
    }
    /// <summary>经生产创建接口建立记录，再设置本例所需进度；不复制创建或规范化算法。</summary>
    private static Hediff_SSCUterusDevelopment Start(Pawn pawn, float progress = .01f)
    {
        Require(SSCUterusDevelopmentUtility.TryCreate(pawn, out var record, out var reason), reason);
        record.Severity = progress; return record;
    }
    /// <summary>直接执行原版健康生育因子计算，检查当前阶段与其他 Hediff 的相乘结果。</summary>
    private static float Factor(Pawn pawn)
    {
        float result = 1f;
        new StatPart_FertilityByHediffs().TransformValue(StatRequest.For(pawn), ref result);
        return result;
    }
    /// <summary>调用生产开关入口并要求成功，使拒绝原因直接出现在失败报告中。</summary>
    private static void Toggle(Pawn pawn, bool enabled)
        => Require(SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, enabled, out var reason), reason);

    /// <summary>覆盖身体挂载、两态因子、妊娠门禁、既有状态叠加及移除后不恢复。</summary>
    private static void BodyAndFertility()
    {
        // 无记录与合法创建分别验证：只读查询不能误启动，正常创建从躯干 1% 开始。
        Check("old bodies and read-only queries do not create a record or alter native fertility", () =>
        {
            var pawn = NewPawn(); Require(SSCUterusDevelopmentUtility.Find(pawn) == null); Near(1, Factor(pawn));
            Require(!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, true, out var reason)); Require(reason.Contains("尚未开始"));
            Equal(0, pawn.health.hediffSet.hediffs.Count);
        });
        Check("creation attaches to Torso at one percent and persists through native health interval", () =>
        {
            var pawn = NewPawn(); var record = Start(pawn);
            Equal(pawn.RaceProps.body.corePart, record.Part); Near(.01f, record.Severity); Require(!record.ShouldRemove);
            record.TickInterval(60000); Near(.01f, record.Severity); Require(!record.ShouldRemove); Near(0, Factor(pawn));
        });
        Check("no available Torso rejects creation without a whole-body fallback", () =>
        {
            var pawn = NewPawn(); var unusual = Bare<ThingDef>(); unusual.race = new RaceProperties { body = new BodyDef { defName = "NoTorso", corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Core" } } } };
            unusual.race.body.ResolveReferences(); pawn.def = unusual;
            Require(!SSCUterusDevelopmentUtility.TryCreate(pawn, out _, out var reason)); Require(reason.Contains("躯干")); Equal(0, pawn.health.hediffSet.hediffs.Count);
        });
        // 包含后续分段阈值和临近满级的数值，确认本批任何未完成状态都不能开启。
        foreach (float progress in new[] { .01f, .3f, .7f, .999f }) Check("incomplete body stays infertile and refuses enable: " + progress, () =>
        {
            var pawn = NewPawn(); var record = Start(pawn, progress);
            Require(!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, true, out var reason)); Require(reason.Contains("尚未完成"));
            Near(0, Factor(pawn)); Require(!record.FertilityEnabled);
        });
        Check("completion defaults off; enable/disable refreshes health without modifying shared stages", () =>
        {
            var pawn = NewPawn(); var record = Start(pawn, 1); var other = NewPawn(); Start(other, 1);
            Require(!record.FertilityEnabled); Near(0, Factor(pawn)); notifications = 0;
            Toggle(pawn, true); Near(2.5f, Factor(pawn)); Equal(1, notifications); Near(0, Factor(other));
            Near(0, development.stages[1].fertilityFactor); Toggle(pawn, true); Equal(1, notifications);
            Toggle(pawn, false); Near(0, Factor(pawn)); Equal(2, notifications);
        });
        Check("native lactation factor composes normally and is never compensated", () =>
        {
            var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true);
            var lactation = new HediffDef { defName = "TestLactation", label = "永久泌乳", stages = new() { new HediffStage { fertilityFactor = .05f } } };
            pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, def = lactation });
            Near(.125f, Factor(pawn)); Toggle(pawn, false); Near(0, Factor(pawn)); Near(.05f, lactation.stages[0].fertilityFactor);
        });
        // 定义命中即视为怀孕，隐藏状态也要拒绝关闭；不改变该妊娠的严重度或归属。
        foreach (string name in new[] { "Pregnant", "PregnantHuman", "RJW_pregnancy", "RJW_pregnancy_beast" })
            Check("hidden pregnancy blocks disabling and is left intact: " + name, () =>
            {
                var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true);
                var pregnancy = new Hediff { pawn = pawn, def = DefDatabase<HediffDef>.GetNamed(name), Severity = .4f };
                pawn.health.hediffSet.hediffs.Add(pregnancy);
                Require(!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, false, out var reason)); Require(reason.Contains("妊娠"));
                Toggle(pawn, true); Near(2.5f, Factor(pawn)); Require(pawn.health.hediffSet.hediffs.Contains(pregnancy)); Near(.4f, pregnancy.Severity);
                pawn.health.hediffSet.hediffs.Remove(pregnancy); Toggle(pawn, false);
            });
        Check("a previously existing pregnancy can coexist with enable without receiving any conversion", () =>
        {
            var pawn = NewPawn(); Start(pawn, 1);
            var pregnancy = new Hediff { pawn = pawn, def = HediffDefOf.Pregnant, Severity = .6f }; pawn.health.hediffSet.hediffs.Add(pregnancy);
            Toggle(pawn, true); Near(.6f, pregnancy.Severity); Near(2.5f, Factor(pawn));
        });
        Check("removing the record restores other factors and queries never recreate historical progress", () =>
        {
            var pawn = NewPawn(); var record = Start(pawn, 1); Toggle(pawn, true); pawn.health.RemoveHediff(record);
            Require(SSCUterusDevelopmentUtility.Find(pawn) == null); Near(1, Factor(pawn));
            var restarted = Start(pawn); Near(.01f, restarted.Severity); Require(!restarted.FertilityEnabled);
        });
        Check("lowering progress clears an enabled preference and zero uses native ShouldRemove", () =>
        {
            var pawn = NewPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
            record.Severity = .7f; Near(0, Factor(pawn)); record.Severity = 1; Require(!record.FertilityEnabled);
            record.Severity = 0; Require(record.ShouldRemove); Near(1, Factor(pawn));
        });
        Check("missing fertile-stage configuration refuses enable", () =>
        {
            var extension = development.GetModExtension<SSCUterusDevelopmentExtension>(); var original = extension.fertileStage;
            try { extension.fertileStage = null; var pawn = NewPawn(); Start(pawn, 1); Require(!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, true, out _)); Near(0, Factor(pawn)); }
            finally { extension.fertileStage = original; }
        });
    }

    /// <summary>覆盖重复启动、原版添加合并、默认挂载和公开严重度写入的坏值边界。</summary>
    private static void MergeAndNormalization()
    {
        // 既有记录必须原位保留，不能通过重复创建或原版合并获得第二份进度。
        Check("repeated successful-start request refuses without changing progress or toggle", () =>
        {
            var pawn = NewPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
            Require(!SSCUterusDevelopmentUtility.TryCreate(pawn, out var found, out _)); Equal(record, found); Require(record.FertilityEnabled); Near(1, record.Severity);
        });
        Check("native AddDirect merges by maximum, never sums; existing toggle is retained", () =>
        {
            var pawn = NewPawn(); var record = Start(pawn, .6f);
            var added = HediffMaker.MakeHediff(development, pawn); added.Severity = .7f; pawn.health.AddHediff(added);
            Equal(1, pawn.health.hediffSet.hediffs.Count); Equal(record, SSCUterusDevelopmentUtility.Find(pawn)); Near(.7f, record.Severity);
            record.Severity = 1; Toggle(pawn, true); pawn.health.AddHediff(HediffMaker.MakeHediff(development, pawn)); Require(record.FertilityEnabled); Near(2.5f, Factor(pawn));
        });
        Check("direct/debug MakeHediff uses Torso even when caller passes another attachment", () =>
        {
            var pawn = NewPawn(); var record = HediffMaker.MakeHediff(development, pawn, new BodyPartRecord());
            pawn.health.AddHediff(record); Equal(pawn.RaceProps.body.corePart, record.Part); Equal(record, SSCUterusDevelopmentUtility.Find(pawn));
        });
        // 同时检查非有限、非正与超过上限的输入，不仅检查正常范围内的数值。
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f, 2f })
            Check("public severity input is finite and bounded: " + value, () =>
            {
                var pawn = NewPawn(); var record = Start(pawn); record.Severity = value;
                Near(value == 2f ? 1f : 0f, record.Severity); Require(float.IsFinite(record.Severity)); Require(!record.FertilityEnabled);
            });
    }

    /// <summary>用真实 Scribe 回环与经过修改的 XML，覆盖缺字段、重复记录及无效身体数据。</summary>
    private static void SaveLoad()
    {
        // 覆盖初始、培养中、满级关闭、满级开启，核对进度／开关／身体部位一起保存。
        foreach (var state in new[] { (.01f, false), (.65f, false), (1f, false), (1f, true) })
            Check("real Scribe round-trip preserves torso/progress/toggle: " + state, () =>
            {
                var pawn = NewPawn(); Start(pawn, state.Item1); if (state.Item2) Toggle(pawn, true);
                string path = Save(pawn); var loaded = Load(path); var record = SSCUterusDevelopmentUtility.Find(loaded);
                Require(record != null); Equal(loaded.RaceProps.body.corePart, record.Part); Near(state.Item1, record.Severity); Equal(state.Item2, record.FertilityEnabled);
                Near(state.Item2 ? 2.5f : 0, Factor(loaded));
            });
        Check("old Scribe body with no record stays unstarted", () =>
        {
            var loaded = Load(Save(NewPawn())); Require(SSCUterusDevelopmentUtility.Find(loaded) == null); Near(1, Factor(loaded));
        });
        Check("missing saved toggle defaults off, rather than granting fertility on load", () =>
        {
            var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
            Edit(path, doc => doc.Descendants("sscUterusFertilityEnabled").Remove());
            var loaded = Load(path); Require(!SSCUterusDevelopmentUtility.Find(loaded).FertilityEnabled); Near(0, Factor(loaded));
        });
        // 直接构造重复存档条目，避免新增时的合并提前替 PostLoadInit 清理掉异常数据。
        Check("duplicate saved records collapse to maximum progress and preserve first toggle", () =>
        {
            var pawn = NewPawn(); Start(pawn, .7f); string path = Save(pawn);
            Edit(path, doc => { var first = doc.Descendants("hediffs").Elements("li").Single(); var copy = new XElement(first); copy.Element("loadID").Value = "999999"; copy.Element("severity").Value = "1"; copy.Add(new XElement("sscUterusFertilityEnabled", "True")); first.AddAfterSelf(copy); });
            var loaded = Load(path); Equal(1, loaded.health.hediffSet.hediffs.Count); Near(1, SSCUterusDevelopmentUtility.Find(loaded).Severity); Near(0, Factor(loaded));
        });
        Check("duplicate saved records do not multiply enabled fertility or replace the existing choice", () =>
        {
            var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
            Edit(path, doc => { var first = doc.Descendants("hediffs").Elements("li").Single(); var copy = new XElement(first); copy.Element("loadID").Value = "999998"; copy.Element("sscUterusFertilityEnabled").Remove(); first.AddAfterSelf(copy); });
            var loaded = Load(path); Equal(1, loaded.health.hediffSet.hediffs.Count); Near(2.5f, Factor(loaded));
        });
        Check("saved whole-body record is discarded without relocating or restoring historical progress", () =>
        {
            var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
            Edit(path, doc => doc.Descendants("part").Remove());
            var loaded = Load(path); Require(SSCUterusDevelopmentUtility.Find(loaded) == null); Equal(0, loaded.health.hediffSet.hediffs.Count); Near(1, Factor(loaded));
        });
        // 存档字段由基类直接读取，因此必须单独验证读档后规范化，而非只测运行时 setter。
        foreach (string value in new[] { "NaN", "Infinity", "-1", "0", "2" })
            Check("corrupt saved severity is normalized after real loading: " + value, () =>
            {
                var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
                Edit(path, doc => doc.Descendants("severity").Single().Value = value);
                var loaded = Load(path); var record = SSCUterusDevelopmentUtility.Find(loaded);
                if (value == "2") { Require(record != null); Near(1, record.Severity); Near(2.5f, Factor(loaded)); }
                else { Require(record == null); Equal(0, loaded.health.hediffSet.hediffs.Count); Near(1, Factor(loaded)); }
            });
        Check("load clears an impossible incomplete enabled flag", () =>
        {
            var pawn = NewPawn(); Start(pawn, 1); Toggle(pawn, true); string path = Save(pawn);
            Edit(path, doc => doc.Descendants("severity").Single().Value = "0.5");
            var loaded = Load(path); var record = SSCUterusDevelopmentUtility.Find(loaded); Near(.5f, record.Severity); Require(!record.FertilityEnabled); record.Severity = 1; Near(0, Factor(loaded));
        });
    }

    /// <summary>保存原版 HediffSet 到隔离 XML，返回供后续加载或修改的文件路径。</summary>
    private static string Save(Pawn pawn)
    {
        string path = Path.Combine(saveDirectory, "body-" + ++nextId + ".xml");
        var fixture = new HealthFixture(pawn);
        Scribe.saver.InitSaving(path, "UterusFixture"); Scribe_Deep.Look(ref fixture, "body"); Scribe.saver.FinalizeSaving();
        return path;
    }
    /// <summary>加载健康记录并执行引用恢复及 PostLoadInit，返回承载记录的新身体。</summary>
    private static Pawn Load(string path)
    {
        HealthFixture loaded = null; Scribe.loader.InitLoading(path);
        Scribe_Deep.Look(ref loaded, "body", NewPawn()); Scribe.loader.FinalizeLoading(); return loaded.Pawn;
    }
    /// <summary>只修改本例隔离存档，用于模拟旧字段缺失或损坏数据。</summary>
    private static void Edit(string path, Action<XDocument> edit) { var doc = XDocument.Load(path); edit(doc); doc.Save(path); }
    /// <summary>构造无需完整游戏启动的全局根对象，必要字段由入口显式补齐。</summary>
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    /// <summary>仅在宿主初始化时设置实际类型的静态根或登记数据。</summary>
    private static void SetStatic(Type type, string name, object value) => AccessTools.Field(type, name).SetValue(null, value);
    /// <summary>跳过已声明隔离的完整游戏下游入口，不替代被测业务结果。</summary>
    private static bool Skip() => false;
    /// <summary>观察健康通知次数，同时继续执行原版 Notify_HediffChanged。</summary>
    private static void CountNotification() => notifications++;
    /// <summary>没有图形设备时不加载纹理，本批不验证画面。</summary>
    private static bool NoTexture(ref UnityEngine.Texture2D __result) { __result = null; return false; }
    /// <summary>将原版配置与 Scribe 文件定位到当前测试的隔离目录。</summary>
    private static bool SaveDirectory(ref string __result) { __result = saveDirectory; return false; }
    /// <summary>拦截图形日志投递；预热后的 Error 纳入当前用例失败判定。</summary>
    private static bool LogBoundary(MethodBase __originalMethod, object[] __args)
    { if (!bootstrap && __originalMethod.Name == "Error") errors.Add((string)__args[0]); return false; }
    /// <summary>断言条件成立，否则将具体原因交给用例报告。</summary>
    private static void Require(bool condition, string reason = "assertion failed") { if (!condition) throw new Exception(reason); }
    /// <summary>比较引用或精确值，不容忍记录、部位和开关被替换。</summary>
    private static void Equal<T>(T expected, T actual) => Require(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, actual {actual}");
    /// <summary>比较有限浮点结果，明确拒绝 NaN／Infinity 漏过误差判断。</summary>
    private static void Near(float expected, float actual) => Require(float.IsFinite(actual) && Math.Abs(expected - actual) < .0001f, $"expected {expected}, actual {actual}");
    /// <summary>执行独立用例并汇总异常和原版错误；失败后释放 Scribe 状态以继续其他用例。</summary>
    private static void Check(string name, Action action)
    {
        errors.Clear();
        try { action(); Require(errors.Count == 0, string.Join('\n', errors)); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex + "\n" + string.Join('\n', errors)); Scribe.loader.ForceStop(); Scribe.mode = LoadSaveMode.Inactive; }
    }
    /// <summary>用例宿主补丁的生命周期边界，退出测试时避免留给同进程的后续代码。</summary>
    private sealed class HarmonyScope : IDisposable
    {
        public readonly Harmony Value = new("ssc.tests.uterus-development.native");
        /// <summary>只卸载当前专项拥有的补丁，不影响其他 Harmony owner。</summary>
        public void Dispose() => Value.UnpatchAll(Value.Id);
    }
}

/// <summary>为真实 HediffSet 提供最小 Scribe 外壳，不复制 Pawn 的完整游戏生命周期。</summary>
public sealed class HealthFixture : IExposable
{
    public Pawn Pawn;
    /// <summary>读档时接收已经建立健康追踪器的新身体，供原版恢复各 Hediff 的 pawn 引用。</summary>
    public HealthFixture(Pawn pawn) => Pawn = pawn;
    /// <summary>直接委托原版保存／加载所有健康记录，生产 Hediff 自行处理其附加字段。</summary>
    public void ExposeData() => Pawn.health.hediffSet.ExposeData();
}
