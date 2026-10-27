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

        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
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

}
