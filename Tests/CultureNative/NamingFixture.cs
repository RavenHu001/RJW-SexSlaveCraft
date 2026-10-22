// AI 辅助生成：原生命名／描述专项环境；只隔离美术和整场启动，不替换生成器、语法解析或语言注入。
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.IO;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

internal static partial class Program
{
    // 固定合同独立于被测文件：索引、关键词与候选池不能由生成结果反推。
    private static readonly string[] DescriptionKeywords = { "creation", "episode", "intro", "conflict", "founderJoin", "victory",
        "setup", "story", "lessonIntro", "lesson", "lessonReinforcement", "archistBasis", "archistFact", "archistProphecy",
        "animistBasis", "animistFact", "animistProphecy" };
    private static readonly string[] StoryNames = { "TheistMyth", "TheistFounder", "IdeologicalFounder", "IdeologicalLesson", "ArchistDescription", "AnimistDescription" };
    private static Dictionary<string, XElement> ruleNodes;
    private static readonly HashSet<string> loadedRuleNames = new();
    private static List<MemeDef> namingStructures;
    private static CultureDef namingCulture;
    private static RitualPatternDef bindingPattern, vanillaPattern;
    private static PreceptDef moralistDef, leaderDef, shooterDef, researcherDef, bindingDef;
    private static readonly Type DescriptionUtility = typeof(Ideo).Assembly.GetType("RimWorld.IdeoDescriptionUtility", true);

    private static XmlElement Xml(XElement element)
    {
        // 使用独立文档，原版补丁／继承器可以直接变更节点，不触碰安装目录的 XML。
        var doc = new XmlDocument(); doc.LoadXml(element.ToString()); return doc.DocumentElement;
    }

    private static XElement Project(XElement node, params string[] fields) => new XElement(node.Name,
        node.Attributes().Where(a => new[] { "Name", "ParentName", "Abstract" }.Contains(a.Name.LocalName)),
        node.Elements().Where(e => fields.Contains(e.Name.LocalName)));

    private static List<XElement> Inherit(IEnumerable<XElement> nodes)
    {
        // 原版先合并继承再解析字段；保留真实父子名，不用测试代码手工拼装 nameMaker。
        XmlInheritance.Clear(); var xml = nodes.Select(Xml).ToList();
        foreach (var node in xml) XmlInheritance.TryRegister(node, null);
        XmlInheritance.Resolve();
        var result = xml.Select(XmlInheritance.GetResolvedNodeFor).Where(n => n["defName"] != null)
            .Select(n => XElement.Parse(n.OuterXml)).ToList();
        // 结果已由原版合并；复制后移除原继承标记，避免反序列化器向已清空的节点注册表二次查询。
        foreach (var node in result) { node.Attribute("ParentName")?.Remove(); node.Attribute("Abstract")?.Remove(); }
        XmlInheritance.Clear(); return result;
    }

    private static void LoadRule(string name)
    {
        // 只递归装载当前原生命名所需的 include 闭包，缺失引用立即失败，不生成替代规则。
        if (!loadedRuleNames.Add(name)) return;
        Require(ruleNodes.ContainsKey(name), "Missing native rule pack " + name);
        var node = ruleNodes[name];
        foreach (var include in node.Descendants("include").Elements("li")) LoadRule(include.Value);
        ReadDef<RulePackDef>(node);
    }

    private static void LoadReferencedRules(XElement node)
    {
        foreach (var item in node.Descendants("include").Elements("li")) LoadRule(item.Value);
    }

    private static void PrepareNaming(string repo, string game, string language)
    {
        // 每语重建 Def／RulePack 对象，防止 rulesResolved 等原版惰性缓存掩盖语言切换或遗漏注入。
        Context(); SetStatic(typeof(ModsConfig), "ideologyActive", true);
        // 角色初始化检查已安装 DLC；只布置宿主的安装标记，不替换原版 CheckIdeology 或角色算法。
        SetStatic(typeof(ModLister), "ideologyInstalled", (bool?)true);
        Set(Current.Game.World, "ideoManager", new IdeoManager());
        Current.Game.World.worldPawns = new WorldPawns();
        foreach (var type in new[] { typeof(Translator), typeof(TranslatorFormattedStringExtensions) })
            foreach (var method in AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "Translate"))
                harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        DefDatabase<RulePackDef>.Clear(); DefDatabase<IdeoStoryPatternDef>.Clear(); DefDatabase<PlaceDef>.Clear();
        DefDatabase<CultureDef>.Clear(); DefDatabase<MemeDef>.Clear(); DefDatabase<RitualPatternDef>.Clear(); loadedRuleNames.Clear();
        // 原版英文词库作为通用框架依赖；SSC 四语文本通过真实 DefInjectionPackage 独立注入。
        // 本专项验证四语规则注入，不宣称已装载原版各语完整翻译或界面。
        var lang = Bare<LoadedLanguage>(); lang.folderName = "English"; Set(lang, "legacyFolderName", "English");
        foreach (var mod in LoadedModManager.RunningMods)
            Set(mod, "foldersToLoadDescendingOrder", new List<string> { Path.Combine(game, "Data/Core"), Path.Combine(game, "Data/Ideology") });
        lang.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
        lang.stringFiles = new(); lang.keyedReplacements = new(); lang.loadErrors = new();
        foreach (string expansion in new[] { "Core", "Ideology" })
        {
            string root = Path.Combine(game, "Data", expansion, "Languages/English");
            foreach (string file in Directory.Exists(Path.Combine(root, "Strings")) ? Directory.EnumerateFiles(Path.Combine(root, "Strings"), "*.txt", SearchOption.AllDirectories) : Array.Empty<string>())
                lang.stringFiles[Path.ChangeExtension(Path.GetRelativePath(Path.Combine(root, "Strings"), file), null).Replace('\\', '/')] =
                    File.ReadAllLines(file).Where(s => s.Length != 0 && !s.StartsWith("//")).ToList();
            foreach (string file in Directory.Exists(Path.Combine(root, "Keyed")) ? Directory.EnumerateFiles(Path.Combine(root, "Keyed"), "*.xml", SearchOption.AllDirectories) : Array.Empty<string>())
                foreach (var e in XDocument.Load(file).Root.Elements()) lang.keyedReplacements[e.Name.LocalName] = new LoadedLanguage.KeyedReplacement { key = e.Name.LocalName, value = e.Value };
        }
        // 手动读取原版文件后标记已装载，避免 LoadedLanguage 再扫描宿主未启用的真实模组目录。
        Set(lang, "dataIsLoaded", true); Set(lang, "wordInfo", new LanguageWordInfo());
        LanguageDatabase.activeLanguage = LanguageDatabase.defaultLanguage = lang;
        FactionDefOf.PlayerColony = new FactionDef { defName = "NamingPlayer", pawnsPlural = "colonists", pawnSingular = "colonist" };
        ColoredText.ResetStaticData();
        // 仅美术构造已在宿主隔离。实际地点、结构模因、故事框架与命名器从当前安装文件读取。
        ruleNodes = new();
        foreach (string expansion in new[] { "Core", "Ideology" })
            foreach (string file in Directory.EnumerateFiles(Path.Combine(game, "Data", expansion, "Defs"), "*.xml", SearchOption.AllDirectories))
                foreach (var node in XDocument.Load(file).Root.Elements("RulePackDef").Where(n => n.Element("defName") != null))
                    ruleNodes[node.Element("defName").Value] = node;
        foreach (var node in XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_CultureNamers.xml")).Root.Elements())
            ruleNodes.Add(node.Element("defName").Value, node);
        foreach (var node in XDocument.Load(Path.Combine(game, "Data/Ideology/Defs/RulePackDefs/RulePacks_Ideo_Description.xml")).Root.Elements("IdeoStoryPatternDef"))
        { LoadReferencedRules(node); ReadDef<IdeoStoryPatternDef>(node); }
        var cultures = XDocument.Load(Path.Combine(game, "Data/Core/Defs/CultureDefs/Cultures.xml")).Root.Elements("CultureDef");
        foreach (var node in cultures)
        {
            var copy = Project(node, "defName", "label", "pawnNameMaker", "ideoNameMaker", "leaderTitleMaker", "festivalNameMaker");
            foreach (var e in copy.Elements().Where(e => e.Name.LocalName.EndsWith("Maker"))) LoadRule(e.Value);
            ReadDef<CultureDef>(copy);
        }
        // Corunan 的原版人物命名器可完整提供创始者／信徒姓名，不替换人物命名算法或描述请求。
        namingCulture = DefDatabase<CultureDef>.GetNamed("Corunan");
        foreach (var node in XDocument.Load(Path.Combine(game, "Data/Ideology/Defs/MemeDefs/Memes_Structures_Basic.xml")).Root.Elements("MemeDef"))
        {
            var copy = Project(node, "defName", "label", "category", "generalRules", "descriptionMaker", "symbolPacks", "symbolPackOverride", "worshipRoomLabel", "deityCount");
            LoadReferencedRules(copy); ReadDef<MemeDef>(copy);
        }
        namingStructures = DefDatabase<MemeDef>.AllDefs.ToList();
        foreach (var node in XDocument.Load(Path.Combine(game, "Data/Ideology/Defs/Misc/PlaceDefs/Places.xml")).Root.Elements("PlaceDef")) LoadReferencedRules(node);
        ReadDef<MemeDef>(XDocument.Load(Path.Combine(repo, "1.7/Defs/MemeDefs/SSC_Meme_MasterSlaveOrder.xml")).Root.Element("MemeDef"));
        mainMeme = DefDatabase<MemeDef>.GetNamed(MemeName);
        // 旧机制专项保留了这些实际戒律对象；每语重建模因后重新绑定其引用，维持完整原版 Def 图。
        foreach (var precept in DefDatabase<PreceptDef>.AllDefs.Where(p => p.defName.StartsWith("SSC_Precept_")))
            foreach (var list in new[] { precept.requiredMemes, precept.conflictingMemes, precept.associatedMemes })
                if (list != null) for (int i = 0; i < list.Count; i++) if (list[i].defName == MemeName) list[i] = mainMeme;
        PrepareNamingRoles(repo, game);
        PrepareNamingRitual(repo, game);
        // 跨引用解析和原版 PostLoad 在注入之前；首次读取 Rules 必须发生在注入之后。
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        foreach (var pack in DefDatabase<RulePackDef>.AllDefs)
        {
            ((RulePack)AccessTools.Field(typeof(RulePackDef), "rulePack").GetValue(pack))?.PostLoad();
            pack.PostLoad();
        }
        RulePackDefOf.GlobalUtility = DefDatabase<RulePackDef>.GetNamed("GlobalUtility");
        mainMeme.generalRules.PostLoad(); mainMeme.descriptionMaker.rules.PostLoad();
        InjectNaming(repo, language, typeof(MemeDef), 44);
        InjectNaming(repo, language, typeof(RulePackDef), 2);
        // 提供原版人物语法所需的实际 PawnKind／种族字段，不生成完整场景人物。
        var human = Bare<ThingDef>(); human.defName = "NamingHuman"; human.label = "human";
        human.race = new RaceProperties { intelligence = Intelligence.Humanlike };
        Set(human.race, "fleshType", new FleshTypeDef { defName = "Normal" });
        ThingDefOf.Human = human;
        PawnKindDefOf.Colonist = new PawnKindDef { defName = "NamingColonist", label = "colonist", race = human };
    }

    private static void PrepareNamingRoles(string repo, string game)
    {
        // 实际补丁作用于完整原版角色 XML；除目标字段外逐字核对其余树未改变。
        var raw = XDocument.Load(Path.Combine(game, "Data/Ideology/Defs/PreceptDefs/Precepts_Role.xml"));
        string before = raw.ToString(); var doc = Xml(raw.Root).OwnerDocument;
        var operation = DirectXmlToObject.ObjectFromXml<PatchOperation>(Xml(XDocument.Load(Path.Combine(repo, "1.7/Patches/SSC_CultureNaming.xml")).Root.Element("Operation")), false);
        Require(operation.Apply(doc)); var patched = XElement.Parse(doc.DocumentElement.OuterXml);
        var target = patched.Elements("PreceptDef").Single(n => (string)n.Element("defName") == "IdeoRole_Moralist");
        Equal("SSC_NamerRoleMoralist", target.Element("nameMaker").Value);
        target.Element("nameMaker").Value = "NamerRoleMoralist";
        Equal(before, new XDocument(patched).ToString()); target.Element("nameMaker").Value = "SSC_NamerRoleMoralist";
        Require(operation.Apply(Xml(new XElement("Defs")).OwnerDocument));
        // 角色美术、能力、衣装非命名依赖，投影保留命名字段、原版父子继承与职位类别。
        if (DefDatabase<IssueDef>.GetNamedSilentFail("IdeoRole") == null) ReadDef<IssueDef>(patched.Element("IssueDef"));
        IssueDefOf.IdeoRitualSeat = new IssueDef { defName = "NamingRitualSeat" };
        var nodes = Inherit(patched.Elements("PreceptDef").Select(n => Project(n, "defName", "label", "nameMaker", "leaderRole", "preceptClass", "issue")));
        foreach (string name in new[] { "IdeoRole_Moralist", "IdeoRole_Leader", "IdeoRole_ShootingSpecialist", "IdeoRole_ResearchSpecialist" })
        {
            var node = nodes.Single(n => (string)n.Element("defName") == name);
            if (node.Element("nameMaker") is XElement maker) LoadRule(maker.Value);
            DefDatabase<PreceptDef>.AllDefs.Where(d => d.defName == name).ToList().ForEach(d => AccessTools.Method(typeof(DefDatabase<PreceptDef>), "Remove").Invoke(null, new object[] { d }));
            ReadDef<PreceptDef>(node);
        }
        moralistDef = DefDatabase<PreceptDef>.GetNamed("IdeoRole_Moralist"); leaderDef = DefDatabase<PreceptDef>.GetNamed("IdeoRole_Leader");
        shooterDef = DefDatabase<PreceptDef>.GetNamed("IdeoRole_ShootingSpecialist");
        researcherDef = DefDatabase<PreceptDef>.GetNamed("IdeoRole_ResearchSpecialist");
        PreceptDefOf.IdeoRole_Moralist = moralistDef; PreceptDefOf.IdeoRole_Leader = leaderDef;
    }

    private static void PrepareNamingRitual(string repo, string game)
    {
        // 保留真实模式继承／命名引用；效果与行为只布置原版可初始化的空 Def，命名专项不执行结算。
        var parent = XDocument.Load(Path.Combine(game, "Data/Ideology/Defs/PreceptDefs/RitualPatternDefs/RitualPatterns.xml")).Root
            .Elements("RitualPatternDef").Single(n => (string)n.Element("defName") == "CelebrationPartyChat");
        var child = XDocument.Load(Path.Combine(repo, "1.7/Defs/RitualPatternDefs/RitualPatternDef.xml")).Root.Element("RitualPatternDef");
        foreach (var node in Inherit(new[] { Project(parent, "defName", "nameMaker"), Project(child, "defName", "nameMaker") }))
        { LoadRule(node.Element("nameMaker").Value); ReadDef<RitualPatternDef>(node); }
        vanillaPattern = DefDatabase<RitualPatternDef>.GetNamed("CelebrationPartyChat");
        bindingPattern = DefDatabase<RitualPatternDef>.GetNamed("SSC_BindingRitualPattern");
        DefDatabase<RitualBehaviorDef>.Clear();
        var behavior = new RitualBehaviorDef { defName = "NamingBehavior", workerClass = typeof(RitualBehaviorWorker) };
        DefDatabase<RitualBehaviorDef>.Add(behavior);
        foreach (var pattern in new[] { vanillaPattern, bindingPattern }) pattern.ritualBehavior = behavior;
        if (DefDatabase<IssueDef>.GetNamedSilentFail("Ritual") == null)
            ReadDef<IssueDef>(new XElement("IssueDef", new XElement("defName", "Ritual"), new XElement("label", "ritual")));
        var binding = Project(XDocument.Load(Path.Combine(repo, "1.7/Defs/PreceptDefs/Precepts_Ritual.xml")).Root.Element("PreceptDef"),
            "defName", "label", "preceptClass", "ritualPatternBase", "allowDuplicates", "ignoreNameUniqueness", "issue");
        DefDatabase<PreceptDef>.AllDefs.Where(d => d.defName == "SSC_BindingRitualPrecept").ToList().ForEach(d => AccessTools.Method(typeof(DefDatabase<PreceptDef>), "Remove").Invoke(null, new object[] { d }));
        ReadDef<PreceptDef>(binding); bindingDef = DefDatabase<PreceptDef>.GetNamed("SSC_BindingRitualPrecept");
    }

    private static void InjectNaming(string repo, string language, Type type, int count)
    {
        // 使用原版文件解析及字段注入，检查 injected 标记；不把手工赋值或非空键检查当成四语验证。
        string path = Path.Combine(repo, $"1.7/Languages/{language}/DefInjected/{type.Name}/SSC_Culture.xml");
        Require(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(repo, $"Sexslavecraft/Languages/{language}/DefInjected/{type.Name}/SSC_Culture.xml"))));
        var package = new DefInjectionPackage(type);
        package.AddDataFromFile(new NamingFile(path), out bool error, File.ReadAllText(path));
        Require(!error); Equal(count, package.injections.Count);
        package.InjectIntoDefs(true);
        Require(package.loadErrors.Count == 0, string.Join("\n", package.loadErrors));
        Require(package.injections.Values.All(i => i.injected));
    }

    private sealed class NamingFile(string path) : VirtualFile
    {
        // 文件边界适配原版 VirtualFile；读取的仍是待交付资源，无内容替代或临时生成翻译。
        public override string Name => Path.GetFileName(path);
        public override string FullPath => path;
        public override bool Exists => File.Exists(path);
        public override long Length => new FileInfo(path).Length;
        public override Stream CreateReadStream() => File.OpenRead(path);
        public override string ReadAllText() => File.ReadAllText(path);
        public override string[] ReadAllLines() => File.ReadAllLines(path);
        public override byte[] ReadAllBytes() => File.ReadAllBytes(path);
    }

    private static Ideo NamingIdeo(bool withMeme, MemeDef structure = null)
    {
        // 每个生成对象都有合法结构／文化／地点；“仅 SSC”仍保留原版结构模因。
        structure ??= namingStructures.Single(m => m.defName == "Structure_Ideological");
        var ideo = Culture(withMeme ? new[] { structure, mainMeme } : new[] { structure });
        ideo.culture = namingCulture; ideo.name = "NAMINGFIXTURE"; ideo.adjective = "FIXTUREADJECTIVE"; ideo.memberName = "FIXTUREMEMBER";
        var placeNode = XDocument.Load(Path.Combine(namingGameRoot, "Data/Ideology/Defs/Misc/PlaceDefs/Places.xml")).Root.Elements("PlaceDef").First();
        var place = DefDatabase<PlaceDef>.GetNamedSilentFail(placeNode.Element("defName").Value);
        if (place == null) { ReadDef<PlaceDef>(placeNode); place = DefDatabase<PlaceDef>.GetNamed(placeNode.Element("defName").Value); }
        // 地点 include 的闭包在专项装载阶段准备，新增地点引用需按原版解析后再使用。
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Set(ideo.foundation, "place", place);
        ((IdeoFoundation_Deity)ideo.foundation).SetDeities(new() { new() { name = "NAMINGDEITY", type = "god", gender = Gender.Male } });
        Set(ideo, "id", 9000 + namingId++); return ideo;
    }
    private static string namingGameRoot;
    private static int namingId;

    private static string Seeded(int seed, Func<string> action)
    {
        // 原版 RNG 状态在每次确定性用例后恢复，候选覆盖不依赖其他测试消耗的随机数。
        Rand.PushState(seed); try { return action(); } finally { Rand.PopState(); }
    }

    private static void ValidGenerated(string value)
    {
        Require(!string.IsNullOrWhiteSpace(value), "Empty generated text");
        Require(!value.Contains('[') && !value.Contains(']') && !value.Contains("ERR") && !value.Contains("Errorism") && !value.Contains("Filestring"), value);
    }
}
