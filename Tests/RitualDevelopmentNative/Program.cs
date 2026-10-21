// AI 辅助生成
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Entry
{
    /// <summary>先安装游戏程序集解析器，再解析含游戏类型的专项入口；不加载地图或正式 ModStartup。</summary>
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string game = Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { Path.GetFullPath(args[0]), game });
    }
}

internal static class Program
{
    private static int passed, failed, messages, reformLetters;

    /// <summary>安装生产补丁；仅隔离语言、通知等无界面环境，不替换曲线、收益筛选或累加算法。</summary>
    internal static int Run(string repo, string game)
    {
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(game, "Version.txt")).Trim()}, MVID: {typeof(Ideo).Module.ModuleVersionId:N}");
        var harmony = new Harmony("ssc.tests.ritual-development.native");
        harmony.PatchAll(typeof(Program).Assembly);
        // 所有 Message 重载都只隔离通知投递；实际收益与改革上限继续走原版实现。
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == "Message"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(SkipMessage)));
        // 带参数的 Translate 属于独立扩展类，也必须隔离，否则会触发未加载的语言语法工作器。
        foreach (var type in new[] { typeof(Translator), typeof(TranslatorFormattedStringExtensions) })
            foreach (var method in AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "Translate"
                && m.ReturnType == typeof(TaggedString) && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(Translate)));
        // 改革通知访问 Find.LetterStack；在最外层通知入口截住，保留 TryAddDevelopmentPoints 本体。
        harmony.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.LetterStack)),
            prefix: new HarmonyMethod(typeof(Program), nameof(Letters)));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(LetterStack)).Where(m => m.Name == "ReceiveLetter"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(SkipLetter)));
        try
        {
            Check("production patch registered exactly once", () =>
            {
                var method = AccessTools.Method(typeof(IdeoDevelopmentUtility), nameof(IdeoDevelopmentUtility.GetDevelopmentPointsOverOutcomeIndexCurveForRitual));
                Equal(1, Harmony.GetPatchInfo(method).Postfixes.Count(p => p.owner == harmony.Id));
            });
            Check("real SSC production worker remains outside vanilla FromQuality hierarchy", () =>
                Require(!typeof(RitualOutcomeEffectWorker_FromQuality).IsAssignableFrom(typeof(RitualOutcomeEffectWorker_SSCBinding))));
            Check("new global settings default off", () => Require(!SSCMod.settings.enableRitualDevelopmentPoints));
            Check("disabled SSC curve is absent", () => Require(Curve(Ritual()) == null));
            Check("missing settings fail closed", () => { var r = Ritual(); SSCMod.settings = null; Require(Curve(r) == null); });
            Check("def capability remains required", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var r = Ritual(); r.outcomeEffect.def.givesDevelopmentPoints = false; Require(Curve(r) == null);
            });
            Check("enabled SSC reuses vanilla shared anytime curve without modifying it", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var vanilla = Ritual(vanilla: true); var ssc = Ritual();
                var expected = Curve(vanilla); var actual = Curve(ssc);
                Require(expected != null && ReferenceEquals(expected, actual));
                for (int i = 0; i < 4; i++) Equal(new[] { 0, 0, 1, 2 }[i], (int)actual.Evaluate(i));
            });
            Check("ordinary vanilla ritual retains curve while SSC preference is off", () => Require(Curve(Ritual(vanilla: true)) != null));
            Check("unrelated custom worker remains excluded", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var r = Ritual(); r.outcomeEffect = new UnrelatedWorker { def = r.outcomeEffect.def }; Require(Curve(r) == null);
            });
            Check("non-anytime SSC does not receive an invented date curve", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true;
                var r = Ritual(); r.isAnytime = false; Require(Curve(r) == null);
            });
            Check("patch preserves another provider's nonempty curve while enabled and removes it when disabled", () =>
            {
                var r = Ritual(); var supplied = new SimpleCurve { new CurvePoint(0, 7) }; SimpleCurve actual = supplied;
                SSCMod.settings.enableRitualDevelopmentPoints = true; Harmony_RitualDevelopmentPoints.Postfix(r, ref actual);
                Require(ReferenceEquals(supplied, actual));
                SSCMod.settings.enableRitualDevelopmentPoints = false; Harmony_RitualDevelopmentPoints.Postfix(r, ref actual);
                Require(actual == null);
            });
            Check("query tolerates null ritual", () => { SimpleCurve result = null; Harmony_RitualDevelopmentPoints.Postfix(null, ref result); Require(result == null); });

            // 实际调用原版 tracker，覆盖四档曲线、信徒准入、上限和目标文化，不用测试公式代算。
            for (int i = 0; i < 4; i++)
            {
                int outcome = i;
                Check($"native tracker awards outcome index {outcome}", () =>
                {
                    SSCMod.settings.enableRitualDevelopmentPoints = true; var r = Ritual();
                    Require(r.ideo.development.TryGainDevelopmentPointsForRitualOutcome(r, outcome, out int nominal));
                    int expected = new[] { 0, 0, 1, 2 }[outcome];
                    Equal(expected, nominal); Equal(expected, r.ideo.development.Points);
                    Equal(expected > 0 ? 1 : 0, messages);
                });
            }
            Check("disabled native payout leaves existing points unchanged", () =>
            {
                var r = Ritual(); r.ideo.development.points = 4;
                Require(!r.ideo.development.TryGainDevelopmentPointsForRitualOutcome(r, 3, out _));
                Equal(4, r.ideo.development.Points); Equal(0, messages);
            });
            Check("native believer requirement blocks reward", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true; var r = Ritual();
                AccessTools.Field(typeof(Ideo), "colonistBelieverCountCached").SetValue(r.ideo, 0);
                Require(!r.ideo.development.TryGainDevelopmentPointsForRitualOutcome(r, 3, out _)); Equal(0, r.ideo.development.Points);
            });
            Check("native reform cap clips actual points and emits one reform notification", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true; var r = Ritual(); var dev = r.ideo.development;
                int cap = dev.NextReformationDevelopmentPoints; dev.points = cap - 1;
                Require(dev.TryGainDevelopmentPointsForRitualOutcome(r, 3, out int nominal));
                Equal(2, nominal); Equal(cap, dev.Points); Equal(1, reformLetters);
                Require(dev.TryGainDevelopmentPointsForRitualOutcome(r, 3, out _));
                Equal(cap, dev.Points); Equal(1, reformLetters);
            });
            Check("reward belongs only to ritual culture", () =>
            {
                SSCMod.settings.enableRitualDevelopmentPoints = true; var r = Ritual(); var other = Ritual();
                r.ideo.development.TryGainDevelopmentPointsForRitualOutcome(r, 3, out _);
                Equal(2, r.ideo.development.Points); Equal(0, other.ideo.development.Points);
            });
            Check("native culture source list follows live setting and leaves vanilla sources intact", () =>
            {
                var r = Ritual(); var vanilla = Ritual(vanilla: true); vanilla.ideo = r.ideo;
                r.ideo.PreceptsListForReading.Add(vanilla);
                var sources = new List<Precept_Ritual>();
                IdeoDevelopmentUtility.GetAllRitualsThatGiveDevelopmentPoints(r.ideo, sources);
                Require(!sources.Contains(r) && sources.Contains(vanilla));
                SSCMod.settings.enableRitualDevelopmentPoints = true; sources.Clear();
                IdeoDevelopmentUtility.GetAllRitualsThatGiveDevelopmentPoints(r.ideo, sources);
                Require(sources.Contains(r) && sources.Contains(vanilla));
                SSCMod.settings.enableRitualDevelopmentPoints = false; sources.Clear();
                IdeoDevelopmentUtility.GetAllRitualsThatGiveDevelopmentPoints(r.ideo, sources);
                Require(!sources.Contains(r) && sources.Contains(vanilla));
                r.ideo.Fluid = false; sources.Clear();
                IdeoDevelopmentUtility.GetAllRitualsThatGiveDevelopmentPoints(r.ideo, sources); Equal(0, sources.Count);
            });

            // 运行资源与源码镜像分别核对，确保翻译实际可加载，而非只存在于源码目录。
            Check("active 1.7 ritual Def explicitly supports development points", () =>
                Require(XDocument.Load(Path.Combine(SscTestPaths.RuntimeRoot(repo), "Defs/RitualDefs/Ritual_Outcome.xml"))
                    .Descendants("RitualOutcomeEffectDef").Single(d => (string)d.Element("defName") == "SSC_BindingRitualOutcome")
                    .Element("givesDevelopmentPoints")?.Value == "true"));
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
                Check($"runtime and source localization complete: {language}", () =>
                {
                    string relative = Path.Combine("Languages", language, "Keyed/SSC_RitualDevelopmentPoints.xml");
                    string runtime = Path.Combine(SscTestPaths.RuntimeRoot(repo), relative);
                    Require(File.ReadAllBytes(runtime).SequenceEqual(File.ReadAllBytes(Path.Combine(repo, "Sexslavecraft", relative))));
                    var root = XDocument.Load(runtime).Root;
                    foreach (string key in new[] { "SSC_Setting_EnableRitualDevelopmentPoints", "SSC_Setting_EnableRitualDevelopmentPoints_Desc" })
                        Require(root.Elements(key).Count() == 1 && !string.IsNullOrWhiteSpace(root.Element(key).Value));
                });
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine($"{passed}/{passed + failed} cases passed");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>构造真实文化及生产 SSC worker；直接设置信徒缓存只隔离地图人口查询。</summary>
    private static Precept_Ritual Ritual(bool vanilla = false)
    {
        var ideo = new Ideo { Fluid = true };
        AccessTools.Field(typeof(Ideo), "colonistBelieverCountCached").SetValue(ideo, 1);
        var def = new RitualOutcomeEffectDef { givesDevelopmentPoints = true, comps = new List<RitualOutcomeComp>() };
        var ritual = new Precept_Ritual
        {
            def = new PreceptDef { defName = "SSC_Test_Ritual", label = "Test ritual" },
            ideo = ideo, isAnytime = true,
            outcomeEffect = vanilla ? new RitualOutcomeEffectWorker_FromQuality(def) : new RitualOutcomeEffectWorker_SSCBinding(def)
        };
        ideo.PreceptsListForReading.Add(ritual);
        return ritual;
    }
    private static SimpleCurve Curve(Precept_Ritual ritual)
        => IdeoDevelopmentUtility.GetDevelopmentPointsOverOutcomeIndexCurveForRitual(ritual.ideo, ritual);
    // 对照处理器直接继承真实原版基类；禁止调用它的结算，避免让虚构结果混入原生收益测试。
    private sealed class UnrelatedWorker : RitualOutcomeEffectWorker
    {
        public override void Apply(float progress, Dictionary<Pawn, int> presence, LordJob_Ritual ritual)
            => throw new NotSupportedException();
    }
    private static void Require(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Equal(int expected, int actual) { if (expected != actual) throw new Exception($"Expected {expected}, actual {actual}"); }
    private static void Check(string name, Action test)
    {
        SSCMod.settings = new SSCSettings(); messages = reformLetters = 0;
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
    }
    // 以下钩子只替代语言和通知边界，不更改文化算法或被测查询的返回值。
    private static bool Translate(string key, ref TaggedString __result) { __result = key; return false; }
    private static bool SkipMessage() { messages++; return false; }
    private static bool Letters(ref LetterStack __result) { __result = new LetterStack(); return false; }
    private static bool SkipLetter() { reformLetters++; return false; }
}
