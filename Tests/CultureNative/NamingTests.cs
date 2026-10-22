// AI 辅助生成：原版文化／职位／仪式命名和四语注入契约，不模拟 GrammarResolver。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

internal static partial class Program
{
    private static void NamingTests(string repo, string game)
    {
        namingGameRoot = game;
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            // 旧专项先完成；每语完整重建命名定义，不复用其他语种已经解析的 RulePack 缓存。
            bool ready = false;
            Check("naming native dependency closure, real patch and language injection: " + language, () =>
            { PrepareNaming(repo, game, language); Require(errors.Count == 0, string.Join("\n", errors)); ready = true; });
            if (!ready) continue;
            Check("naming fixed resource keys and placeholder grammar: " + language, () => NamingResourceContract(repo, language));
            Check("native symbols select SSC and ordinary sources without unresolved rules: " + language, () =>
            {
                // 原版结构包与神祇仍参与选择；确定种子集合应能走到 SSC，但不要求每次结果来自 SSC。
                bool selectedSSC = false, selectedOther = false;
                for (int seed = 0; seed < 40; seed++)
                {
                    var ideo = NamingIdeo(true);
                    Seeded(seed, () => { ideo.foundation.GenerateTextSymbols(); return ideo.name; });
                    ValidGenerated(ideo.name); ValidGenerated(ideo.adjective); ValidGenerated(ideo.memberName);
                    selectedSSC |= ideo.usedSymbolPacks.Any(s => s.StartsWith("SSCNAMETODO"));
                    selectedOther |= !ideo.usedSymbolPacks.Any(s => s.StartsWith("SSCNAMETODO"));
                }
                Require(selectedSSC && selectedOther);
                var plain = NamingIdeo(false); plain.foundation.GenerateTextSymbols();
                ValidGenerated(plain.name); Require(!plain.name.Contains("SSCNAMETODO"));
            });
            Check("native structure override and no-deity symbol source: " + language, () =>
            {
                // 只改变夹具的原版结构开关，验证 SSC 未全局覆盖原版来源算法。
                var ideo = NamingIdeo(true); var structure = ideo.StructureMeme;
                bool saved = structure.symbolPackOverride;
                try
                {
                    structure.symbolPackOverride = true; ((IdeoFoundation_Deity)ideo.foundation).SetDeities(new());
                    ideo.foundation.GenerateTextSymbols(); ValidGenerated(ideo.name);
                    Require(ideo.usedSymbolPacks.Count == 1 && structure.symbolPacks.Any(p => p.PrimarySymbol == ideo.usedSymbolPacks[0]));
                }
                finally { structure.symbolPackOverride = saved; }
            });
            Check("native used symbol packs filter candidates and exhausted packs fall back: " + language, () =>
            {
                // 用真实世界文化管理器占用所有候选；保留一组空闲时必须选择它，全部占用后仍能生成。
                var ideo = NamingIdeo(true); ((IdeoFoundation_Deity)ideo.foundation).SetDeities(new());
                var existing = NamingIdeo(false);
                existing.usedSymbolPacks.AddRange(ideo.memes.SelectMany(m => m.symbolPacks).Select(p => p.PrimarySymbol));
                string available = mainMeme.symbolPacks[2].PrimarySymbol; existing.usedSymbolPacks.Remove(available);
                Find.IdeoManager.IdeosListForReading.Add(existing);
                try
                {
                    ideo.foundation.GenerateTextSymbols(); Equal(available, ideo.usedSymbolPacks.Single());
                    existing.usedSymbolPacks.Add(available); ideo.foundation.GenerateTextSymbols(); ValidGenerated(ideo.name);
                    Equal(1, ideo.usedSymbolPacks.Count);
                }
                finally { Find.IdeoManager.IdeosListForReading.Clear(); }
            });
            Check("native leader title preset, fallback, classic and absent maker: " + language, () =>
            {
                foreach (var culture in DefDatabase<CultureDef>.AllDefs)
                {
                    var ideo = NamingIdeo(true); ideo.culture = culture; ideo.foundation.GenerateLeaderTitle();
                    Require(LeaderPool.Contains(ideo.leaderTitleMale)); Equal(ideo.leaderTitleMale, ideo.leaderTitleFemale);
                    ideo.memes.Remove(mainMeme); ideo.foundation.GenerateLeaderTitle();
                    ValidGenerated(ideo.leaderTitleMale); Require(!LeaderPool.Contains(ideo.leaderTitleMale));
                }
                var classic = NamingIdeo(true); classic.classicMode = true; classic.foundation.GenerateLeaderTitle();
                Equal(leaderDef.label, classic.leaderTitleMale); Equal(classic.leaderTitleMale, classic.leaderTitleFemale);
                var noMaker = NamingIdeo(true); noMaker.culture = new CultureDef(); noMaker.foundation.GenerateLeaderTitle();
                Require(noMaker.leaderTitleMale == null && noMaker.leaderTitleFemale == null);
            });
            Check("native moralist preset and fallback do not rename other roles: " + language, () =>
            {
                foreach (bool with in new[] { true, false })
                {
                    var ideo = NamingIdeo(with);
                    var role = Role(ideo, moralistDef); role.RegenerateName(); ValidGenerated(role.Label);
                    Equal(with, MoralistPool.Contains(role.Label));
                    foreach (var def in new[] { shooterDef, researcherDef })
                    {
                        var specialist = Role(ideo, def); specialist.RegenerateName(); ValidGenerated(specialist.Label);
                        Require(!MoralistPool.Contains(specialist.Label));
                    }
                }
            });
            Check("native binding Fill and festival fallback preserve other ritual names: " + language, () =>
            {
                foreach (bool with in new[] { true, false })
                {
                    var ideo = NamingIdeo(with); var ritual = Ritual(ideo, bindingPattern);
                    Require(ritual.nameMaker == DefDatabase<RulePackDef>.GetNamed("SSC_NamerRitualBinding"));
                    ritual.RegenerateName(); ValidGenerated(ritual.Label); Equal(with, RitualPool.Contains(ritual.Label));
                    var vanilla = Ritual(ideo, vanillaPattern); vanilla.RegenerateName(); ValidGenerated(vanilla.Label);
                    Require(!RitualPool.Contains(vanilla.Label));
                    // 原版缺少实例命名器时使用定义 label，保留既有降级行为。
                    ritual.nameMaker = null; Equal(bindingDef.label, ritual.GenerateNameRaw());
                }
            });
            Check("native duplicate binding rituals exhaust three candidates with bounded retries: " + language, () =>
            {
                var ideo = NamingIdeo(true); Require(bindingDef.allowDuplicates && !bindingDef.ignoreNameUniqueness);
                for (int i = 0; i < 6; i++)
                {
                    var ritual = Ritual(ideo, bindingPattern); ritual.RegenerateName(); Require(RitualPool.Contains(ritual.Label));
                }
                Equal(6, ideo.PreceptsListForReading.Count); Require(ideo.PreceptsListForReading.Select(p => p.Label).Distinct().Count() <= 3);
            });
            Check("native meme changes, precept locks and saved ritual maker: " + language, () => NamingLifecycle());
            Check("native reform adds and removes SSC before regenerating unlocked target names: " + language, () =>
            {
                // 真正执行原版改革兼容流程，再执行原版改名；不只手动改列表后调用解析器。
                var ideo = NamingIdeo(false); var role = Role(ideo, moralistDef); var ritual = Ritual(ideo, bindingPattern);
                var old = ideo.memes.ToList(); ideo.memes.Add(mainMeme);
                ideo.foundation.EnsurePreceptsCompatibleWithMemes(old, ideo.memes.ToList(), Parms());
                VerifyFive(ideo); ideo.RegenerateAllPreceptNames();
                Require(ideo.PreceptsListForReading.Contains(role) && ideo.PreceptsListForReading.Contains(ritual));
                Require(MoralistPool.Contains(role.Label) && RitualPool.Contains(ritual.Label));
                old = ideo.memes.ToList(); ideo.memes.Remove(mainMeme);
                ideo.foundation.EnsurePreceptsCompatibleWithMemes(old, ideo.memes.ToList(), Parms());
                ideo.RegenerateAllPreceptNames();
                Require(!MoralistPool.Contains(role.Label) && !RitualPool.Contains(ritual.Label));
            });
            DescriptionTests(language);
            Check("real injection changes a generated role candidate rather than only counting keys: " + language, () =>
            {
                // 原版注入到新建的未解析 RulePack，替换右侧探针只存在于内存，资源文件始终不变。
                mainMeme.generalRules = DirectXmlToObject.ObjectFromXml<RulePack>(Xml(XDocument.Load(Path.Combine(repo,
                    "1.7/Defs/MemeDefs/SSC_Meme_MasterSlaveOrder.xml")).Root.Element("MemeDef").Element("generalRules")), false);
                string path = Path.Combine(repo, $"1.7/Languages/{language}/DefInjected/MemeDef/SSC_Culture.xml");
                var package = new DefInjectionPackage(typeof(MemeDef));
                package.AddDataFromFile(new NamingFile(path), out bool error, File.ReadAllText(path).Replace("SSCMORALTODO", "INJECTIONPROBE"));
                Require(!error); package.InjectIntoDefs(true); Require(package.injections.Values.All(i => i.injected));
                var role = Role(NamingIdeo(true), moralistDef); role.RegenerateName(); Require(role.Label.StartsWith("INJECTIONPROBE"), role.Label);
            });
        }
        Check("current 1.7 XML deserializer accepts authored meme and both namers", () =>
        {
            // 当前游戏默认使用新版 XML 反序列化器；额外覆盖该路径，不仅验证兼容旧解析器。
            var nodes = XDocument.Load(Path.Combine(repo, "1.7/Defs/MemeDefs/SSC_Meme_MasterSlaveOrder.xml")).Root.Elements("MemeDef")
                .Concat(XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_CultureNamers.xml")).Root.Elements());
            var parsed = nodes.Select(n => DirectXmlToObjectNew.DefFromNodeNew(Xml(n), null)).ToArray();
            DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
            var meme = (MemeDef)parsed[0]; Equal(3, meme.symbolPacks.Count); Equal(13, meme.generalRules.Rules.Count);
            Equal(17, meme.descriptionMaker.rules.Rules.Count); Require(meme.descriptionMaker.patterns.NullOrEmpty());
            Equal("SSC_NamerRoleMoralist", parsed[1].defName); Equal("SSC_NamerRitualBinding", parsed[2].defName);
        });
        Check("native XML loader skips new Ideology definitions with DLC inactive", () =>
        {
            // 执行原版顶层 MayRequire 分支，不能仅凭属性存在就声称未启用 DLC 时安全装载。
            SetStatic(typeof(ModsConfig), "ideologyActive", false); SetStatic(typeof(ModLister), "ideologyInstalled", (bool?)false);
            try
            {
                var root = new XElement("Defs", XDocument.Load(Path.Combine(repo, "1.7/Defs/MemeDefs/SSC_Meme_MasterSlaveOrder.xml")).Root.Elements(),
                    XDocument.Load(Path.Combine(repo, "1.7/Defs/RulePackDefs/SSC_CultureNamers.xml")).Root.Elements());
                LoadedModManager.ParseAndProcessXML(Xml(root).OwnerDocument, new Dictionary<System.Xml.XmlNode, LoadableXmlAsset>());
                var parsed = (IEnumerable<Def>)AccessTools.Field(typeof(LoadedModManager), "patchedDefs").GetValue(null);
                Require(!parsed.Any());
                var patch = DirectXmlToObject.ObjectFromXml<PatchOperation>(Xml(XDocument.Load(Path.Combine(repo, "1.7/Patches/SSC_CultureNaming.xml")).Root.Element("Operation")), false);
                Require(patch.Apply(Xml(new XElement("Defs")).OwnerDocument));
            }
            finally
            {
                XmlInheritance.Clear(); SetStatic(typeof(ModsConfig), "ideologyActive", true);
                SetStatic(typeof(ModLister), "ideologyInstalled", (bool?)true);
            }
        });
    }

    private static readonly string[] LeaderPool = { "SSCLEADERTODO01", "SSCLEADERTODO02", "SSCLEADERTODO03" };
    private static readonly string[] MoralistPool = { "SSCMORALTODO01", "SSCMORALTODO02", "SSCMORALTODO03" };
    private static readonly string[] RitualPool = { "SSCRITUALTODO01", "SSCRITUALTODO02", "SSCRITUALTODO03" };

    private static void NamingResourceContract(string repo, string language)
    {
        // 独立固定集合同时检查缺键、错索引和多余键；连接规则也必须保留完整左侧语法。
        string prefix = MemeName + "."; var expected = new HashSet<string> { prefix + "label", prefix + "description" };
        for (int i = 0; i < 3; i++) foreach (string field in new[] { "ideoName", "theme", "adjective", "member" }) expected.Add(prefix + $"symbolPacks.{i}.{field}");
        for (int i = 0; i < 13; i++) expected.Add(prefix + $"generalRules.rulesStrings.{i}");
        for (int i = 0; i < 17; i++) expected.Add(prefix + $"descriptionMaker.rules.rulesStrings.{i}");
        var entries = XDocument.Load(Path.Combine(repo, $"1.7/Languages/{language}/DefInjected/MemeDef/SSC_Culture.xml")).Root.Elements().ToArray();
        Require(expected.SetEquals(entries.Select(e => e.Name.LocalName))); Equal(expected.Count, entries.Length);
        Equal(3, mainMeme.symbolPacks.Count); Require(mainMeme.symbolPacks.All(p => !p.prefix));
        Equal(3, mainMeme.symbolPacks.Select(p => p.PrimarySymbol).Distinct().Count());
        for (int i = 0; i < 3; i++)
        {
            var pack = mainMeme.symbolPacks[i]; Equal($"SSCNAMETODO{i + 1:00}", pack.ideoName);
            Equal($"SSCTHEMETODO{i + 1:00}", pack.theme); Equal($"SSCADJTODO{i + 1:00}", pack.adjective);
            Equal($"SSCMEMBERTODO{i + 1:00}", pack.member);
        }
        var rules = mainMeme.generalRules.Rules; Equal(13, rules.Count);
        Require(rules.All(r => r.keyword != "r_roleName" && r.keyword != "r_ritualName"));
        // 全部连接／候选使用独立字面量检查，避免随机生成未选到某一条时漏掉错误或缺失候选。
        var expectedRules = new[] { "memeAdjective->SSCADJTODO", "memeCreed->SSCCREEDTODO", "memeHyphenPrefix->SSCPREFIXTODO",
            "r_leaderTitle(priority=10)->[SSC_LeaderPreset]" }.Concat(LeaderPool.Select(v => "SSC_LeaderPreset->" + v))
            .Concat(MoralistPool.Select(v => "SSC_MoralistPreset->" + v)).Concat(RitualPool.Select(v => "SSC_BindingRitualPreset->" + v)).ToArray();
        for (int i = 0; i < expectedRules.Length; i++)
            Equal(expectedRules[i], entries.Single(e => e.Name.LocalName == prefix + $"generalRules.rulesStrings.{i}").Value);
        Require(mainMeme.descriptionMaker.patterns.NullOrEmpty()); Equal(17, mainMeme.descriptionMaker.rules.Rules.Count);
        for (int i = 0; i < 17; i++)
        {
            var rule = mainMeme.descriptionMaker.rules.Rules[i]; Equal(DescriptionKeywords[i], rule.keyword);
            Equal("meme_SSC_MasterSlaveOrder", rule.tag); Equal($"SSCDESCTODO{i + 1:00}", rule.Generate());
            Equal((int?)(i == 1 || i == 7 ? 1 : null), rule.usesLimit);
        }
    }

    private static Precept_Role Role(Ideo ideo, PreceptDef def)
    {
        // 走原版 maker 和文化添加路径，直接生成使用真实 def.nameMaker；不生成任职者或能力。
        var role = (Precept_Role)PreceptMaker.MakePrecept(def); ideo.AddPrecept(role, init: true); return role;
    }

    private static Precept_Ritual Ritual(Ideo ideo, RitualPatternDef pattern)
    {
        // 实际模式 Fill 写入实例引用，原生 GenerateNameRaw 随后读取该实例而不是测试拼装请求。
        var ritual = (Precept_Ritual)PreceptMaker.MakePrecept(bindingDef); ideo.AddPrecept(ritual, init: true, fillWith: pattern); return ritual;
    }

    private static void NamingLifecycle()
    {
        // 正常重新生成尊重 nameLocked；移除模因只影响未锁定对象未来的生成请求。
        var ideo = NamingIdeo(true); ideo.foundation.GenerateLeaderTitle();
        var moralist = Role(ideo, moralistDef); var ritual = Ritual(ideo, bindingPattern);
        moralist.RegenerateName(); ritual.RegenerateName();
        Set(moralist, "name", "LOCKEDMORALIST"); moralist.nameLocked = true;
        ideo.memes.Remove(mainMeme); ideo.RegenerateAllPreceptNames();
        Equal("LOCKEDMORALIST", moralist.Label); Require(!RitualPool.Contains(ritual.Label));
        ideo.memes.Add(mainMeme); ideo.RegenerateAllPreceptNames(); Require(RitualPool.Contains(ritual.Label));
        // 用真实 Scribe 保存旧实例的节庆命名器，读档不应被新模式引用隐式替换。
        ritual.nameMaker = DefDatabase<RulePackDef>.GetNamed("NamerRitualFestival");
        Set(ritual, "name", "LOCKEDRITUAL"); ritual.nameLocked = true; ideo.description = "SAVEDDESCRIPTION";
        var loaded = NamingRoundtrip(ideo);
        Equal(ideo.name, loaded.name); Equal(ideo.adjective, loaded.adjective); Equal(ideo.memberName, loaded.memberName);
        Equal(ideo.leaderTitleMale, loaded.leaderTitleMale); Equal(ideo.leaderTitleFemale, loaded.leaderTitleFemale);
        var loadedMoralist = loaded.PreceptsListForReading.OfType<Precept_Role>().Single();
        Equal("LOCKEDMORALIST", loadedMoralist.Label); Require(loadedMoralist.nameLocked);
        var old = loaded.PreceptsListForReading.OfType<Precept_Ritual>().Single();
        Equal("NamerRitualFestival", old.nameMaker.defName); Equal("LOCKEDRITUAL", old.Label); Require(old.nameLocked);
        loaded.RegenerateAllPreceptNames(); Equal("LOCKEDRITUAL", old.Label);
        old.nameLocked = false; old.RegenerateName(); Require(!RitualPool.Contains(old.Label));
        bindingPattern.Fill(old); old.RegenerateName(); Require(RitualPool.Contains(old.Label));
    }

    private static Ideo NamingRoundtrip(Ideo source)
    {
        // 原版文化／实例保存加载，不跳过名称、模板、锁定或命名器字段；只有外观由旧宿主边界隔离。
        string path = Path.Combine(saveDirectory, "naming.xml");
        try
        {
            Scribe.saver.InitSaving(path, "NamingFixture"); Scribe_Deep.Look(ref source, "ideo"); Scribe.saver.FinalizeSaving();
            Ideo loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "ideo"); Scribe.loader.FinalizeLoading(); return loaded;
        }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
    }
}
