// AI 辅助生成：直接运行实际 1.7 StatPart_Quality 和生产 Harmony 补丁；无图形初始化。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using SexSlaveCraft;

internal static class Program
{
    private static int passed, failed;
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Arguments: repositoryRoot gameRoot");
        string gameRoot = Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(gameRoot, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        return RunNative(Path.GetFullPath(args[0]), gameRoot);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunNative(string root, string gameRoot)
    {
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(gameRoot, "Version.txt")).Trim()}, MVID: {typeof(ThingDef).Module.ModuleVersionId:N}");
        // 按原版 XML 创建真实属性组件；不运行完整游戏静态初始化或启动 ModStartup。
        StatDefOf.MeleeWeapon_DamageMultiplier = new StatDef { defName = "MeleeWeapon_DamageMultiplier" };
        var part = new StatPart_Quality { parentStat = StatDefOf.MeleeWeapon_DamageMultiplier };
        var xml = XDocument.Load(Path.Combine(gameRoot, "Data/Core/Defs/Stats/Stats_Weapons_Melee.xml"))
            .Descendants("StatDef").Single(e => (string)e.Element("defName") == "MeleeWeapon_DamageMultiplier")
            .Element("parts").Elements().Single();
        foreach (var e in xml.Elements())
            typeof(StatPart_Quality).GetField(e.Name.LocalName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(part, float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture));
        var harmony = new Harmony("ssc.tests.pna.melee.quality");
        harmony.CreateClassProcessor(typeof(Harmony_SSCPnaMeleeQualityValue)).Patch();
        harmony.CreateClassProcessor(typeof(Harmony_SSCPnaMeleeQualityExplanation)).Patch();
        try
        {
            var fixedDefs = new[] { MakeWeapon("SSC_Weapon_PnaSpikeStaff", true), MakeWeapon("SSC_Weapon_PnaShortWhip", true) };
            var other = MakeWeapon("UnmarkedSword", false);
            float[] expected = { .8f, .9f, 1f, 1.1f, 1.2f, 1.45f, 1.65f };
            foreach (QualityCategory quality in Enum.GetValues<QualityCategory>())
            {
                foreach (var def in fixedDefs)
                    Check($"{def.defName} {quality}: 抽象及实物请求保留外部倍率", () =>
                    {
                        float value = 1.25f;
                        var req = StatRequest.For(def, null, quality); part.TransformValue(req, ref value); Near(1.25f, value);
                        var thing = new ThingWithComps { def = def }; value = .75f;
                        part.TransformValue(StatRequest.For(thing), ref value); Near(.75f, value);
                        if (part.ExplanationPart(req) != null) throw new Exception("不应显示失效品质系数");
                    });
                Check($"普通近战 {quality}: 原版品质曲线仍生效", () =>
                {
                    float value = 1.25f; part.TransformValue(StatRequest.For(other, null, quality), ref value);
                    Near(1.25f * expected[(int)quality], value);
                });
                Check($"已标记武器其他属性 {quality}: 品质仍生效", () =>
                {
                    var otherPart = new StatPart_Quality { parentStat = new StatDef { defName = "MarketValue" } };
                    foreach (var e in xml.Elements()) typeof(StatPart_Quality).GetField(e.Name.LocalName, BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(otherPart, float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture));
                    float value = 1; otherPart.TransformValue(StatRequest.For(fixedDefs[0], null, quality), ref value); Near(expected[(int)quality], value);
                });
            }
            Check("射针枪即使错误携带标记也不跳过普通防身品质", () =>
            {
                var gun = MakeWeapon("SSC_Weapon_PnaNeedleRepeater", true);
                // Verbs 在空字段时返回共享空表，不能向该表 Add；模拟 XML 对私有字段的赋值。
                typeof(ThingDef).GetField("verbs", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(gun, new List<VerbProperties> { new VerbProperties { verbClass = typeof(Verb_Shoot) } });
                float value = 1; part.TransformValue(StatRequest.For(gun, null, QualityCategory.Legendary), ref value); Near(1.65f, value);
            });
            Check("空请求及无父属性安全放行", () =>
            {
                if (SSCPnaMeleeQualityUtility.SkipQuality(null, default) || SSCPnaMeleeQualityUtility.SkipQuality(new StatPart_Quality(), default)
                    || SSCPnaMeleeQualityUtility.SkipQuality(part, default)) throw new Exception("不应拦截空请求");
            });
            Check("XML只给短矛与短鞭声明品质例外", () =>
            {
                var marked = Directory.GetFiles(Path.Combine(root, "1.7/Defs"), "*.xml", SearchOption.AllDirectories)
                    .SelectMany(f => XDocument.Load(f).Root.Elements()).Where(d => d.Element("modExtensions")?.Elements()
                        .Any(e => (string)e.Attribute("Class") == typeof(SSCPnaFixedMeleeDamageExtension).FullName) == true)
                    .Select(d => d.Element("defName").Value).OrderBy(n => n);
                if (!marked.SequenceEqual(fixedDefs.Select(d => d.defName).OrderBy(n => n))) throw new Exception("XML标记范围不符");
            });
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。"); return failed == 0 ? 0 : 1;
    }

    private static ThingDef MakeWeapon(string name, bool marked) => new ThingDef
    {
        defName = name, category = ThingCategory.Item, equipmentType = EquipmentType.Primary,
        tools = new List<Tool> { new Tool { power = 3 } },
        modExtensions = marked ? new List<DefModExtension> { new SSCPnaFixedMeleeDamageExtension() } : null
    };
    private static void Check(string name, Action action)
    { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    private static void Near(float expected, float value)
    { if (Math.Abs(expected - value) > .00001f) throw new Exception($"Expected {expected}, actual {value}"); }
}
