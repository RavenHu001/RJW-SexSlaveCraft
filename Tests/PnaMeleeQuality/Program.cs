// AI 辅助生成：直接运行实际 1.7 品质、穿甲和弹丸算法，验证已撤下品质例外；无图形初始化。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

internal static partial class Program
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
        StatDefOf.MeleeWeapon_DamageMultiplier.parts = new List<StatPart> { part };
        var xml = XDocument.Load(Path.Combine(gameRoot, "Data/Core/Defs/Stats/Stats_Weapons_Melee.xml"))
            .Descendants("StatDef").Single(e => (string)e.Element("defName") == "MeleeWeapon_DamageMultiplier")
            .Element("parts").Elements().Single();
        foreach (var e in xml.Elements())
            typeof(StatPart_Quality).GetField(e.Name.LocalName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(part, float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture));
        var harmony = new Harmony("ssc.tests.pna.melee.quality");
        try
        {
            var pnaDefs = new[] { MakeWeapon("SSC_Weapon_PnaSpikeStaff"), MakeWeapon("SSC_Weapon_PnaShortWhip"), MakeWeapon("SSC_Weapon_PnaSilkWhip") };
            var other = MakeWeapon("UnmarkedSword");
            float[] expected = { .8f, .9f, 1f, 1.1f, 1.2f, 1.45f, 1.65f };
            foreach (QualityCategory quality in Enum.GetValues<QualityCategory>())
            {
                foreach (var def in pnaDefs)
                    Check($"{def.defName} {quality}: 抽象及实物请求正常应用品质", () =>
                    {
                        float value = 1.25f;
                        var req = StatRequest.For(def, null, quality); part.TransformValue(req, ref value); Near(1.25f * expected[(int)quality], value);
                        var thing = WithQuality(def, quality); value = .75f;
                        part.TransformValue(StatRequest.For(thing), ref value); Near(.75f * expected[(int)quality], value);
                    });
                Check($"普通近战 {quality}: 原版品质曲线仍生效", () =>
                {
                    float value = 1.25f; part.TransformValue(StatRequest.For(other, null, quality), ref value);
                    Near(1.25f * expected[(int)quality], value);
                });
                Check($"武器其他属性 {quality}: 品质仍生效", () =>
                {
                    var otherPart = new StatPart_Quality { parentStat = new StatDef { defName = "MarketValue" } };
                    foreach (var e in xml.Elements()) typeof(StatPart_Quality).GetField(e.Name.LocalName, BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(otherPart, float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture));
                    float value = 1; otherPart.TransformValue(StatRequest.For(pnaDefs[0], null, quality), ref value); Near(expected[(int)quality], value);
                });
            }
            Check("射针枪普通防身伤害正常受品质影响", () =>
            {
                var gun = MakeWeapon("SSC_Weapon_PnaNeedleRepeater");
                // Verbs 在空字段时返回共享空表，不能向该表 Add；模拟 XML 对私有字段的赋值。
                typeof(ThingDef).GetField("verbs", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(gun, new List<VerbProperties> { new VerbProperties { verbClass = typeof(Verb_Shoot) } });
                float value = 1; part.TransformValue(StatRequest.For(gun, null, QualityCategory.Legendary), ref value); Near(1.65f, value);
            });
            Check("品质适配源码归档且所有生产和测试入口均已撤下", () =>
            {
                const string obsolete = "SSCPnaFixedMeleeDamageExtension";
                var marked = Directory.GetFiles(Path.Combine(root, "1.7/Defs"), "*.xml", SearchOption.AllDirectories)
                    .Where(f => File.ReadAllText(f).Contains(obsolete)).ToArray();
                if (marked.Length != 0) throw new Exception("运行时 XML 仍引用废案类型");
                string archive = Path.Combine(root, "Archive/PnaMeleeQuality/Harmony_SSCPnaMeleeQuality.cs");
                if (!File.Exists(archive) || !File.ReadAllText(archive).Contains("废案归档")) throw new Exception("源码归档或用途说明缺失");
                if (File.Exists(Path.Combine(root, "Sexslavecraft/ThePatches/Harmony_SSCPnaMeleeQuality.cs"))) throw new Exception("生效源码仍存在");
                foreach (string file in new[] { "Sexslavecraft/SexSlaveCraft_Alpha.csproj", "Tests/PnaMeleeQuality/PnaMeleeQuality.csproj", "Tests/PnaSpacerNative/PnaSpacerNative.csproj" })
                    if (File.ReadAllText(Path.Combine(root, file)).Contains("Harmony_SSCPnaMeleeQuality.cs")) throw new Exception("编译入口未撤下：" + file);
            });
            RunArmorAndRangedChecks(root, gameRoot, harmony);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。"); return failed == 0 ? 0 : 1;
    }

    private static ThingDef MakeWeapon(string name) => new ThingDef
    {
        defName = name, category = ThingCategory.Item, equipmentType = EquipmentType.Primary,
        tools = new List<Tool> { new Tool { power = 3 } }
    };
    private static void Check(string name, Action action)
    { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
    private static void Near(float expected, float value)
    { if (Math.Abs(expected - value) > .00001f) throw new Exception($"Expected {expected}, actual {value}"); }
}
