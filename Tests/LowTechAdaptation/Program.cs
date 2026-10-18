using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static string repository;
    private static RecipeDef rough;
    private static RecipeDef simple;
    private static RecipeDef bulk;

    private sealed class ModStove : Building_WorkTable { }

    private static int Main(string[] args)
    {
        repository = SscTestPaths.RepositoryRoot(args.FirstOrDefault());
        Run("启动器等待加载完成再注入", () =>
        {
            ThingDef table = Stove(simple);
            DefDatabase<ThingDef>.AllDefsListForReading.Add(table);
            RuntimeHelpers.RunClassConstructor(typeof(SSCPnaConcentrationBootstrap).TypeHandle);
            Equal(1, LongEventHandler.QueuedActions.Count);
            Assert(!table.AllRecipes.Contains(rough), "排队前不可修改工作台");
            LongEventHandler.QueuedActions.Dequeue()();
            HasRough(table);
        });
        Run("建筑 recipes 的模组灶台自动接入并保留列表顺序", () =>
        {
            var other = new RecipeDef { defName = "ModRecipe" };
            ThingDef table = Stove(other, simple);
            List<RecipeDef> original = table.recipes;
            Equal(1, Inject(table));
            Assert(ReferenceEquals(original, table.recipes), "不替换原始配方列表");
            Assert(table.AllRecipes.SequenceEqual(new[] { other, simple, rough }), "仅末尾追加");
            HasRough(table);
        });
        Run("仅从配方 recipeUsers 获得烹饪能力的建筑也接入", () =>
        {
            ThingDef table = Stove();
            simple.recipeUsers = new List<ThingDef> { table };
            Equal(1, Inject(table));
            HasRough(table);
        });
        Run("仅支持批量简易餐的模组炉具接入", () =>
        {
            ThingDef table = Stove(bulk);
            Equal(1, Inject(table));
            HasRough(table);
        });
        Run("已生成缓存原地更新且重建缓存后仍可见", () =>
        {
            ThingDef table = Stove(simple);
            List<RecipeDef> cache = table.AllRecipes;
            Equal(1, Inject(table));
            Assert(ReferenceEquals(cache, table.AllRecipes), "不替换第三方持有的缓存");
            HasRough(table);
            table.CachedRecipes = null;
            HasRough(table);
        });
        Run("重复执行不重复配方或使用者", () =>
        {
            ThingDef table = Stove(simple);
            Equal(1, Inject(table));
            Equal(0, Inject(table));
            Equal(1, rough.recipeUsers.Count(d => d == table));
            HasRough(table);
        });
        Run("显式 recipeUsers 关联与原版入口不重复注入", () =>
        {
            ThingDef table = Stove(simple);
            rough.recipeUsers = new List<ThingDef> { table };
            Equal(0, Inject(table));
            HasRough(table);
        });
        Run("建筑已显式加入粗放配方时不再添加反向关联", () =>
        {
            ThingDef table = Stove(simple, rough);
            Equal(0, Inject(table));
            Assert(rough.recipeUsers == null, "避免缓存重建后双重关联造成重复");
            table.CachedRecipes = null;
            HasRough(table);
        });
        Run("已有使用者但旧缓存未包含新配方时补齐缓存", () =>
        {
            ThingDef table = Stove(simple);
            _ = table.AllRecipes;
            rough.recipeUsers = new List<ThingDef> { table };
            Equal(1, Inject(table));
            Equal(1, rough.recipeUsers.Count);
            HasRough(table);
        });
        Run("供暖器屠宰台药台及名称相似对象不自动接入", () =>
        {
            var heater = new ThingDef { defName = "Stove", thingClass = typeof(object), recipes = new List<RecipeDef> { simple } };
            ThingDef butcher = Stove(new RecipeDef { defName = "ButcherCorpseFlesh" });
            ThingDef drugLab = Stove(new RecipeDef { defName = "SSC_Make_SuperDrug" });
            Equal(0, Inject(heater, butcher, drugLab, null, new ThingDef()));
            Assert(rough.recipeUsers == null, "不能泛化到非烹饪对象");
        });
        Run("只有同名自定义配方不当作原版简易餐", () =>
        {
            Equal(0, Inject(Stove(new RecipeDef { defName = "CookMealSimple" })));
        });
        Run("缺少定义时不误开放且允许只有一种烹饪锚点", () =>
        {
            ThingDef table = Stove(bulk);
            Equal(0, SSCPnaConcentrationRecipeUtility.AddToCookingTables(new[] { table }, null, simple, bulk));
            Equal(0, SSCPnaConcentrationRecipeUtility.AddToCookingTables(new[] { table }, rough, null, null));
            Equal(0, SSCPnaConcentrationRecipeUtility.AddToCookingTables(null, rough, simple, bulk));
            Equal(1, SSCPnaConcentrationRecipeUtility.AddToCookingTables(new[] { table }, rough, null, bulk));
        });
        Run("多个灶台仅接入粗放配方并保留高效配方使用者", () =>
        {
            var efficient = new RecipeDef { defName = "SSC_Make_SuperDrug", recipeUsers = new List<ThingDef> { Stove() } };
            DefDatabase<RecipeDef>.AllDefsListForReading.Add(efficient);
            var targets = new[] { Stove(simple), Stove(bulk) };
            Equal(2, Inject(targets));
            Equal(1, efficient.recipeUsers.Count);
            Assert(targets.All(t => !t.AllRecipes.Contains(efficient)), "不传播高效药物台配方");
        });

        RunResearchChecks();
        Run("粗放配方原料产物工作与研究门槛", CheckRoughRecipe);
        Run("药物台两份配方保持原工艺参数和入口", CheckEfficientRecipes);
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            string current = language;
            Run(current + " 新配方三项文本和现有配方名称齐全", () => CheckLanguage(current));
        }
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        DefDatabase<RecipeDef>.AllDefsListForReading.Clear();
        DefDatabase<ThingDef>.AllDefsListForReading.Clear();
        rough = new RecipeDef { defName = "SSC_Make_SuperDrug_Rough" };
        simple = new RecipeDef { defName = "CookMealSimple" };
        bulk = new RecipeDef { defName = "CookMealSimpleBulk" };
        DefDatabase<RecipeDef>.AllDefsListForReading.AddRange(new[] { rough, simple, bulk });
        try { test(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception ex) { failed++; Console.WriteLine("失败：" + name + "\n" + ex); }
    }

    private static ThingDef Stove(params RecipeDef[] recipes) => new ThingDef
    {
        defName = "ThirdPartyCookingTable",
        thingClass = typeof(ModStove),
        recipes = recipes.ToList()
    };

    private static int Inject(params ThingDef[] tables)
        => SSCPnaConcentrationRecipeUtility.AddToCookingTables(tables, rough, simple, bulk);

    private static void HasRough(ThingDef table) => Equal(1, table.AllRecipes.Count(r => r == rough));
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual) => Assert(EqualityComparer<T>.Default.Equals(expected, actual), $"预期 {expected}，实际 {actual}");
    private static string Resource(string relative) => SscTestPaths.Combine(repository, relative);
    private static XElement Def(string relative, string name) => XDocument.Load(Resource(relative)).Root.Elements()
        .Single(d => (string)d.Element("defName") == name);

    private static void RunResearchChecks()
    {
        // 记录原树的点数与前置，防止等级调整时顺带拆开材料／人格发展线。
        var expected = new[]
        {
            ("BasicTraining", 500, ""), ("BodyPartTraining", 800, "BasicTraining"),
            ("PNASimpleApp", 800, "BasicTraining"), ("SexChange", 1000, "PNASimpleApp"),
            ("Femboy", 1200, "SexChange"), ("PNASimpleLauncher", 1000, "PNASimpleApp,PrecisionRifling"),
            ("PNAConcentration", 1200, "PNASimpleApp"), ("PE", 1500, "PNAConcentration"),
            ("SlaveSpecialization", 1500, "BodyPartTraining,PE"), ("CowTraining", 1200, "SlaveSpecialization"),
            ("PublicUse", 1200, "SlaveSpecialization"), ("TrainerOfficer", 1200, "SlaveSpecialization"),
            ("PetCat", 1200, "SlaveSpecialization"), ("Combatant", 1200, "SlaveSpecialization"),
            ("PetDog", 1500, "PetCat"), ("PetRabbit", 1800, "PetDog"),
            ("GelatinizationPartial", 2000, "PE"), ("GelatinizationFull", 3000, "GelatinizationPartial")
        };
        foreach (var row in expected)
        {
            var current = row;
            Run("研究等级及原点数／前置：" + row.Item1, () =>
            {
                XElement d = Def("Defs/ResearchDefs/ResearchProjectDef.xml", "SSC_RES_" + current.Item1);
                string tier = current.Item1 == "BasicTraining" || current.Item1 == "BodyPartTraining" ? "Neolithic" :
                    current.Item1 == "PNASimpleLauncher" ? "Industrial" : "Medieval";
                Equal(tier, (string)d.Element("techLevel"));
                Equal(current.Item2, (int)d.Element("baseCost"));
                string[] parents = d.Element("prerequisites")?.Elements().Select(e => e.Value).ToArray() ?? Array.Empty<string>();
                string[] wanted = current.Item3.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s == "PrecisionRifling" ? s : "SSC_RES_" + s).ToArray();
                Assert(parents.SequenceEqual(wanted), "现有研究顺序与前置必须保留");
            });
        }
        Run("研究总数分档与Royalty蓝图要求保留", () =>
        {
            XElement[] defs = XDocument.Load(Resource("Defs/ResearchDefs/ResearchProjectDef.xml")).Root.Elements().ToArray();
            Equal(18, defs.Length);
            Equal(2, defs.Count(d => (string)d.Element("techLevel") == "Neolithic"));
            Equal(15, defs.Count(d => (string)d.Element("techLevel") == "Medieval"));
            Equal(1, defs.Count(d => (string)d.Element("techLevel") == "Industrial"));
            XElement basic = defs.Single(d => (string)d.Element("defName") == "SSC_RES_BasicTraining");
            Equal(1, (int)basic.Element("techprintCount"));
            Equal("Ludeon.RimWorld.Royalty", (string)basic.Element("techprintCount").Attribute("MayRequire"));
        });
    }

    private static void CheckRoughRecipe()
    {
        XElement d = Def("Defs/RecipeDefs/RecipeDef_PNA_Rough.xml", "SSC_Make_SuperDrug_Rough");
        Equal("粗放浓缩紫色纳米催情液", (string)d.Element("label"));
        Equal(600, (int)d.Element("workAmount"));
        Equal("Cooking", (string)d.Element("workSkill"));
        Equal("Cooking", (string)d.Element("requiredGiverWorkType"));
        Equal("CookSpeed", (string)d.Element("workSpeedStat"));
        Equal("SSC_RES_PNAConcentration", (string)d.Element("researchPrerequisite"));
        XElement ingredient = d.Element("ingredients").Elements().Single();
        Equal(10, (int)ingredient.Element("count"));
        Equal("Purple_Aphrodisiac_Nanofluid", ingredient.Element("filter").Element("thingDefs").Elements().Single().Value);
        foreach (string tag in new[] { "fixedIngredientFilter", "defaultIngredientFilter" })
            Equal("Purple_Aphrodisiac_Nanofluid", d.Element(tag).Element("thingDefs").Elements().Single().Value);
        Equal("Purple_Aphrodisiac_Nanofluid_Plus", d.Element("products").Elements().Single().Name.LocalName);
        Equal(1, (int)d.Element("products").Elements().Single());
        Assert(d.Element("recipeUsers").Elements().Select(e => e.Value).SequenceEqual(new[] { "Campfire", "FueledStove", "ElectricStove" }), "原版三个入口");
        Assert(d.Attribute("ParentName") == null && d.Element("efficiencyStat") == null && d.Element("workTableEfficiencyStat") == null,
            "不继承餐食筛选或额外产出倍率");
    }

    private static void CheckEfficientRecipes()
    {
        foreach (var row in new[] { ("SSC_Make_SuperDrug", 5, 1, 450, "浓缩紫色纳米催情液"), ("SSC_Make_SuperDrug_forth", 20, 4, 1350, "批量浓缩紫色纳米催情液") })
        {
            XElement d = Def("Defs/RecipeDefs/RecipeDef_PNA_PLUS.xml", row.Item1);
            Equal(row.Item5, (string)d.Element("label"));
            Equal(row.Item2, (int)d.Element("ingredients").Elements().Single().Element("count"));
            Equal(row.Item3, (int)d.Element("products").Elements().Single());
            Equal(row.Item4, (int)d.Element("workAmount"));
            Equal("Intellectual", (string)d.Element("workSkill"));
            Equal("DrugSynthesisSpeed", (string)d.Element("workSpeedStat"));
            Equal("DrugLab", d.Element("recipeUsers").Elements().Single().Value);
            Equal("SSC_RES_PNAConcentration", (string)d.Element("researchPrerequisite"));
        }
    }

    private static void CheckLanguage(string language)
    {
        XElement data = XDocument.Load(Resource($"Languages/{language}/DefInjected/RecipeDef/RecipeDef_PNA_PLUS.xml")).Root;
        foreach (string suffix in new[] { "label", "description", "jobString" })
            Assert(!string.IsNullOrWhiteSpace((string)data.Element("SSC_Make_SuperDrug_Rough." + suffix)), "新配方文本缺失");
        foreach (string name in new[] { "SSC_Make_SuperDrug.label", "SSC_Make_SuperDrug_forth.label" })
            Assert(!string.IsNullOrWhiteSpace((string)data.Element(name)), "原配方名称缺失");
        if (language == "ChineseSimplified")
        {
            Equal("粗放浓缩紫色纳米催情液", (string)data.Element("SSC_Make_SuperDrug_Rough.label"));
            Equal("浓缩紫色纳米催情液", (string)data.Element("SSC_Make_SuperDrug.label"));
            Equal("批量浓缩紫色纳米催情液", (string)data.Element("SSC_Make_SuperDrug_forth.label"));
        }
    }
}
