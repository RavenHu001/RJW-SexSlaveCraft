using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private const string ExpectedName = "SSC_Thought_GenderChangeSuccess";
    private static string root;
    private static int passed, failed;

    /// <summary>运行定义一致性和生产手术回归，接收仓库根目录并返回测试退出码。</summary>
    private static int Main(string[] args)
    {
        root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        // 定义校验回调：阻止同一个 ThoughtDef 再次出现多个名称或被错误改名。
        Run("术后记忆只定义一个规范名称", () =>
        {
            XElement thought = ReadThought();
            Require(thought.Elements("defName").Count() == 1, "ThoughtDef 必须只有一个 defName。");
            Require((string)thought.Element("defName") == ExpectedName, "定义必须与代码及翻译使用同一名称。");
        });
        // 正常手术回调：使用实际 XML 注册定义，验证生产代码添加正确的记忆。
        Run("成功手术添加十五天基础心情负十的记忆", () =>
        {
            LoadDefinition();
            Pawn patient = Operate();
            RequireBodyChanged(patient);
            ThoughtDef memory = patient.needs.mood.thoughts.memories.Memories.Single();
            Require(memory.defName == ExpectedName, "添加的记忆名称错误。");
            Require(memory.durationDays == 15 && memory.baseMoodEffect == -10 && memory.stackLimit == 1,
                "修复必须保留原有持续时间、基础心情值和堆叠限制。");
            Require(Log.Errors.Count == 0, "正常手术不能产生定义缺失错误。");
        });
        // 缺失定义回调：保留诊断，同时验证手术收尾不会将空值传给原版记忆入口。
        Run("记忆定义缺失时手术正常收尾且保留诊断", () =>
        {
            Pawn patient = Operate();
            RequireBodyChanged(patient);
            Require(patient.needs.mood.thoughts.memories.Memories.Count == 0, "缺失定义时不应伪造记忆。");
            Require(Log.Errors.Count == 1 && Log.Errors[0].Contains(ExpectedName), "缺失定义仍应有可定位的诊断。");
        });
        // 手术失败回调：确保原版失败结果仍会阻止身体变化和术后记忆添加。
        Run("失败手术不修改身体或添加记忆", () =>
        {
            LoadDefinition();
            Recipe_Surgery.SurgeryFails = true;
            Pawn patient = Operate();
            Require(patient.gender == Gender.Male && patient.RebuiltParts == 0, "失败手术不能调整身体。");
            Require(patient.needs.mood.thoughts.memories.Memories.Count == 0, "失败手术不能添加成功记忆。");
            Require(DefDatabase<ThoughtDef>.Queries == 0, "失败手术不应查询术后记忆。");
        });
        // 非男性回调：保留现有执行前的性别限制。
        Run("非男性目标不执行手术", () =>
        {
            Pawn patient = Operate(new Pawn { gender = Gender.Female });
            Require(patient.RebuiltParts == 0 && DefDatabase<ThoughtDef>.Queries == 0, "不适用目标应提前返回。");
        });
        // 无心情需求回调：身体调整仍可完成，无需查找或创建心情记忆。
        Run("没有心情需求时跳过记忆", () =>
        {
            var patient = new Pawn();
            patient.needs.mood = null;
            RequireBodyChanged(Operate(patient));
            Require(DefDatabase<ThoughtDef>.Queries == 0, "无心情需求时不应查询记忆。");
        });
        // 无需求跟踪器回调：验证现有空值路径继续安全。
        Run("没有需求跟踪器时安全完成", () =>
        {
            RequireBodyChanged(Operate(new Pawn { needs = null }));
            Require(DefDatabase<ThoughtDef>.Queries == 0, "无需求跟踪器时不应查询记忆。");
        });
        // 重复执行回调：第一次手术改变性别后，重复进入不会再次添加同一成功记忆。
        Run("重复调用不会重复添加术后记忆", () =>
        {
            LoadDefinition();
            Pawn patient = Operate();
            Operate(patient);
            Require(patient.needs.mood.thoughts.memories.Memories.Count == 1 && patient.RebuiltParts == 3,
                "第二次调用应由性别检查提前返回。");
        });
        foreach (string language in new[] { "ChineseTraditional", "English", "Russian" })
        {
            // 翻译校验回调：逐语言检查非空译文能绑定实际注册的术后记忆。
            Run(language + " 术后记忆翻译指向有效定义", () =>
            {
                LoadDefinition();
                XDocument translation = XDocument.Load(Path.Combine(root, "Languages", language,
                    "DefInjected", "ThoughtDef", "RecipeDef_Surgery_DCMtF.xml"));
                foreach (string suffix in new[] { ".stages.0.label", ".stages.0.description" })
                {
                    XElement entry = translation.Root.Element(ExpectedName + suffix);
                    Require(entry != null && !string.IsNullOrWhiteSpace(entry.Value), "缺少非空翻译：" + suffix);
                    Require(DefDatabase<ThoughtDef>.Definitions.ContainsKey(ExpectedName), "译文对应的定义未注册。");
                }
            });
        }
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>读取实际手术 XML 中唯一的术后记忆节点。</summary>
    private static XElement ReadThought() => XDocument.Load(Path.Combine(root, "Defs", "RecipeDefs",
        "RecipeDef_Surgery_DCMtF.xml")).Root.Elements("ThoughtDef").Single();

    /// <summary>按 XML 子节点顺序读取名称，模拟游戏对重复字段后值覆盖前值的行为。</summary>
    private static void LoadDefinition()
    {
        XElement thought = ReadThought();
        var def = new ThoughtDef();
        foreach (XElement name in thought.Elements("defName")) def.defName = name.Value;
        def.durationDays = float.Parse(thought.Element("durationDays").Value, CultureInfo.InvariantCulture);
        def.baseMoodEffect = float.Parse(thought.Element("stages").Element("li").Element("baseMoodEffect").Value,
            CultureInfo.InvariantCulture);
        def.stackLimit = int.Parse(thought.Element("stackLimit").Value, CultureInfo.InvariantCulture);
        DefDatabase<ThoughtDef>.Definitions.Add(def.defName, def);
    }

    /// <summary>通过生产手术入口处理指定角色，未提供角色时创建默认男性患者。</summary>
    private static Pawn Operate(Pawn patient = null)
    {
        patient ??= new Pawn();
        new Recipe_GenderChange_MtF().ApplyOnPawn(patient, new BodyPartRecord(), new Pawn(), new List<Thing>(), new Bill());
        return patient;
    }

    /// <summary>检查性别、体型、器官重建调用和外观刷新均已完成。</summary>
    private static void RequireBodyChanged(Pawn patient)
    {
        Require(patient.gender == Gender.Female && patient.story.bodyType == BodyTypeDefOf.Female
            && patient.RebuiltParts == 3 && patient.Drawer.renderer.Refreshes == 1, "手术身体调整未正常完成。");
    }

    /// <summary>重置每个用例的定义和计数，执行回调并记录结果，避免用例互相影响。</summary>
    private static void Run(string name, Action body)
    {
        DefDatabase<ThoughtDef>.Definitions.Clear();
        DefDatabase<ThoughtDef>.Queries = 0;
        Recipe_Surgery.SurgeryFails = false;
        Log.Errors.Clear();
        try { body(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception ex) { failed++; Console.WriteLine("失败：" + name + " — " + ex.Message); }
    }

    /// <summary>断言不成立时抛出可读异常，由测试执行器统一记录失败。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
