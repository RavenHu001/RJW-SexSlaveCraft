// AI 辅助生成：生产 XML 原生反序列化/交叉引用与文化生成、改革契约。
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 固定期望来自已确认的设计，不从被测 XML 反推，防止配置写错后测试仍自洽通过。
    private const string MemeName = "SSC_Meme_MasterSlaveOrder";
    private static readonly string[] groups = { "SexSlave", "Trainer", "Master", "MasterCount", "SlaveCount", "SlaveGender" };
    private static readonly string[][] suffixes = {
        new[] { "Hostile", "Revulsion", "Indifference", "Respect", "Sacred", "Envy" },
        new[] { "Hostile", "Revulsion", "Indifference", "Reverence", "SacredOffice" },
        new[] { "Hostile", "Revulsion", "Indifference", "Deference", "HolyMaster" },
        new[] { "One", "Three", "Unlimited" }, new[] { "One", "Three", "Unlimited" },
        new[] { "FemaleOnly", "MaleOnly", "Unrestricted", "FemaleRequired", "MaleRequired", "NonMasterRequired" }
    };
    private static readonly int[][] selection = { new[] { 0, 0, 0, 0, 1, 0 }, new[] { 0, 0, 0, 0, 1 }, new[] { 0, 0, 0, 0, 1 }, new[] { 0, 1, 0 }, new[] { 0, 1, 0 }, new[] { 0, 0, 1, 0, 0, 0 } };
    private static readonly int[][] defaults = { new[] { 1, 2, 7, 0, 0, 0 }, new[] { 1, 2, 7, 0, 0 }, new[] { 1, 2, 7, 0, 0 }, new[] { 0, 0, 1 }, new[] { 0, 0, 1 }, new[] { 0, 0, 1, 0, 0, 0 } };
    private static MemeDef mainMeme;
    private static List<MemeDef> vanillaMemes = new();
    private static PreceptDef D(string name) => DefDatabase<PreceptDef>.GetNamed("SSC_Precept_" + name);
    private static string[] FullDefaults = { "SexSlave_Sacred", "Trainer_SacredOffice", "Master_HolyMaster", "MasterCount_Three", "SlaveCount_Three", "SlaveGender_Unrestricted" };

    private static void ReadDef<T>(XElement element) where T : Def, new()
    {
        // 走原版字段反序列化和 DefDatabase 注册；跨 Def 引用统一在批量装载后解析。
        var doc = new XmlDocument(); doc.LoadXml(element.ToString());
        var def = DirectXmlToObject.ObjectFromXml<T>(doc.DocumentElement, false);
        DefDatabase<T>.Add(def);
    }

    private static Ideo Culture(params MemeDef[] memes)
    {
        // 使用实际可改革文化与原版 foundation，绕过整套文化外观生成但不替换资格/改革算法。
        var ideo = new Ideo { memes = memes.ToList(), Fluid = true };
        var foundation = new IdeoFoundation_Deity();
        Set(foundation, "ideo", ideo); ideo.foundation = foundation;
        return ideo;
    }
    // 无关武器偏好分支关闭；戒律仍由原版 maker 构造并经 AddPrecept 更新实际文化缓存。
    private static IdeoGenerationParms Parms() => new IdeoGenerationParms(null, forceNoWeaponPreference: true);
    private static void Add(Ideo ideo, string name) => ideo.AddPrecept(PreceptMaker.MakePrecept(D(name)));
    private static void VerifyGroups(Ideo ideo)
    {
        // 每组按 Issue 检查恰有一个且属于合法替代范围，不能只检查戒律总数为五。
        foreach (var options in mainMeme.requireOne)
        {
            var chosen = ideo.PreceptsListForReading.Where(p => p.def.issue == options[0].issue).ToArray();
            Equal(1, chosen.Length); Require(options.Contains(chosen[0].def));
        }
    }

    /// <summary>测试侧可行性检查：先识别已有合法必选项，待填组才按真实生成资格计算正权重。</summary>
    private static bool Feasible(Ideo ideo, IdeoGenerationParms parms)
    {
        foreach (var options in mainMeme.requireOne)
        {
            // 已存在的合法选项即使生成权重为零也可保留，不能要求改革强制重抽。
            if (ideo.PreceptsListForReading.Any(p => options.Contains(p.def)
                && ideo.foundation.CanAddForFaction(p.def, parms.forFaction, parms.disallowedPrecepts, false))) continue;
            // 只有待填组才需至少一个真实可生成且正权重的候选；同时考虑阵营和临时禁用列表。
            var eligible = options.Where(p => ideo.foundation.CanAddForFaction(p, parms.forFaction, parms.disallowedPrecepts, true)).ToArray();
            if (eligible.Length == 0 || eligible.Sum(p => p.selectionWeight) <= 0) return false;
        }
        return true;
    }

    /// <summary>所有范围共用原生Def初始化，验证断言单独归资源专项。</summary>
    private static XElement[] LoadCultureFixture(string repo)
    {
        // 态度定义与 thought 一起交叉引用；逐顶层节点核对 Ideology 门控，不以 XML 语法代替解析。
        var paths = new[] { "MemeDefs/SSC_Meme_MasterSlaveOrder.xml", "PreceptDefs/SSC_Precepts_SexSlave.xml",
            "PreceptDefs/SSC_Precepts_Trainer.xml", "PreceptDefs/SSC_Precepts_Master.xml", "PreceptDefs/SSC_Precepts_Counts.xml",
            "ThoughtDefs/SSC_CultureThoughts_SexSlave.xml", "ThoughtDefs/SSC_CultureThoughts_Trainer.xml", "ThoughtDefs/SSC_CultureThoughts_Master.xml",
            "ThoughtDefs/SSC_CultureThoughts_Counts.xml", "PreceptDefs/SSC_Precepts_SlaveGender.xml",
            "ThoughtDefs/SSC_CultureThoughts_SlaveGender.xml" };
        var nodes = paths.SelectMany(path => XDocument.Load(Path.Combine(repo, "1.7/Defs", path)).Root.Elements()).ToArray();
        foreach (var node in nodes)
        {
            switch (node.Name.LocalName)
            {
                case "MemeGroupDef": ReadDef<MemeGroupDef>(node); break;
                case "MemeDef": ReadDef<MemeDef>(node); break;
                case "IssueDef": ReadDef<IssueDef>(node); break;
                case "PreceptDef": ReadDef<PreceptDef>(node); break;
                case "ThoughtDef": ReadDef<ThoughtDef>(node); break;
                default: throw new Exception("Unexpected Def " + node.Name);
            }
        }
        // 解析失败立即停止夹具初始化，不能被下一条用例的错误清理掩盖。
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        mainMeme = DefDatabase<MemeDef>.GetNamed(MemeName);
        Require(errors.Count == 0, string.Join('\n', errors));
        ThoughtUtility.Reset();
        return nodes;
    }

    private static void ResourceTests(string repo, string game)
    {
        // 后续用例共享已解析的生产 Def；游戏根对象仍由各类测试按需重置。
        Context();
        var nodes = LoadCultureFixture(repo);
        Check("native XML fields and cross-references resolve for all authored culture definitions", () =>
        {
            foreach (var node in nodes) Equal("Ludeon.RimWorld.Ideology", (string)node.Attribute("MayRequire"));
            Equal(28, DefDatabase<PreceptDef>.AllDefs.Count()); Equal(6, DefDatabase<IssueDef>.AllDefs.Count());
            // 实机调整：主奴秩序使用原版最高影响三级；Normal 是模因类别，不是影响档位。
            Equal(3, mainMeme.impact); Equal(MemeCategory.Normal, mainMeme.category);
            Equal(1f, mainMeme.randomizationSelectionWeightFactor); Require(mainMeme.exclusionTags.NullOrEmpty());
            Equal(6, mainMeme.requireOne.Count);
            Equal("3,5,2,3,3,6", string.Join(',', mainMeme.requireOne.Select(l => l.Count)));
            Require(mainMeme.requireOne.All(l => l.All(p => p.issue == l[0].issue)));
            Require(DefDatabase<IssueDef>.AllDefs.All(d => !d.allowMultiplePrecepts));
            // 步骤 3 补有限数量反馈；两条不限及三条漠视无组件，不混入事件或意愿拦截。
            Require(DefDatabase<PreceptDef>.AllDefs.All(d => !d.allowDuplicates && d.countsTowardsPreceptLimit));
            Equal(22, DefDatabase<PreceptDef>.AllDefs.Count(d => !d.comps.NullOrEmpty()));
            Equal(65, DefDatabase<PreceptDef>.AllDefs.Sum(d => d.comps.Count));
            Require(DefDatabase<PreceptDef>.AllDefs.SelectMany(d => d.comps).All(c => c is PreceptComp_SituationalThought));
            Equal(56, DefDatabase<ThoughtDef>.AllDefs.Count());
        });
        Check("all 28 weights, ordering, prefix and 7/6/19 meme lists match the fixed contract", () =>
        {
            // 逐档比对固定权重、顺序和正式描述；数量组顺序从 20 起，与已确认显示约定一致。
            int required = 0, conflicting = 0, associated = 0;
            for (int g = 0; g < groups.Length; g++)
                for (int i = 0; i < suffixes[g].Length; i++)
                {
                    var d = D(groups[g] + "_" + suffixes[g][i]);
                    Equal((float)selection[g][i], d.selectionWeight); Equal((float)defaults[g][i], d.defaultSelectionWeight);
                    Equal((i + (g == 3 || g == 4 ? 2 : 1)) * 10f, d.displayOrderInIssue);
                    RequireFinalCopy(d.description);
                    // 三种模因列表语义分别核对，不能把仅关联误写为前置要求或硬冲突。
                    bool r = (g == 0 && i >= 4) || (g == 1 && i == 4) || (g == 2 && i == 4) || (g == 5 && i >= 3);
                    bool c = (g == 0 || g == 2) && i < 3;
                    bool a = g >= 3 || (g == 0 && i >= 3) || ((g == 1 || g == 2) && i >= 3);
                    Equal(r, d.requiredMemes?.Contains(mainMeme) == true); Equal(c, d.conflictingMemes.Contains(mainMeme)); Equal(a, d.associatedMemes.Contains(mainMeme));
                    required += d.requiredMemes?.Count ?? 0; conflicting += d.conflictingMemes.Count; associated += d.associatedMemes.Count;
                }
            // 总数是逐档断言之外的额外漏项检查，确保未额外挂载无关模因。
            Equal(7, required); Equal(6, conflicting); Equal(19, associated);
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("four-language keys, final copy and byte-identical mirror: " + language, () =>
            {
                // 四语分别检查运行时文件与源镜像字节一致，再检查键唯一、数量完整及值非空。
                foreach (string type in new[] { "MemeDef", "IssueDef", "PreceptDef" })
                {
                    string relative = $"Languages/{language}/DefInjected/{type}/SSC_Culture.xml";
                    string path = Path.Combine(repo, "1.7", relative);
                    Require(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(repo, "Sexslavecraft", relative))));
                    var entries = XDocument.Load(path).Root.Elements().ToArray();
                    Equal(type == "MemeDef" ? 45 : type == "IssueDef" ? 6 : 99, entries.Length);
                    Equal(entries.Length, entries.Select(x => x.Name).Distinct().Count());
                    // 正式文案只验证完整、无交接标记；机制与键集合仍使用独立固定预期。
                    foreach (var e in entries) RequireFinalCopy(e.Value);
                    var names = type == "MemeDef" ? new[] { MemeName } : type == "IssueDef" ? groups.Select(g => "SSC_Issue_" + g) : DefDatabase<PreceptDef>.AllDefs.Select(d => d.defName);
                    foreach (var name in names) Require(entries.Any(e => e.Name.LocalName == name + ".label"));
                }
            });
        foreach (bool withMeme in new[] { false, true })
            Check("native CanAdd and zero-weight manual replacement: meme=" + withMeme, () =>
            {
                // 同一组候选在有/无主模因时合法数量不同；资格交给原版判断，不能用权重代替资格。
                var expected = withMeme ? new[] { 3, 5, 2, 3, 3, 6 } : new[] { 4, 4, 4, 3, 3, 3 };
                var ideo = Culture(withMeme ? new[] { mainMeme } : Array.Empty<MemeDef>());
                for (int g = 0; g < groups.Length; g++)
                {
                    var options = suffixes[g].Select(s => D(groups[g] + "_" + s)).ToArray();
                    Equal(expected[g], options.Count(p => ideo.foundation.CanAdd(p)));
                    // 加入一个选项后，手动替换检查仍允许全部合法档位，新增检查则须拒绝同 Issue 重复。
                    var legal = options.First(p => ideo.foundation.CanAdd(p)); ideo.AddPrecept(PreceptMaker.MakePrecept(legal));
                    Equal(expected[g], options.Count(p => ideo.foundation.CanAdd(p, false)));
                    Equal(0, options.Count(p => ideo.foundation.CanAdd(p, true)));
                }
            });
        // 原生随机化会访问动物特殊戒律上限；该无关分支以 maxCount=0 的夹具定义关闭。
        // 不拦截 RandomizePrecepts/AddRequired/CanAdd/改革/RecachePrecepts。
        bootstrap = true;
        PreceptDefOf.AnimalVenerated = new PreceptDef { maxCount = 0 };
        bootstrap = false;
        Check("native full RandomizePrecepts produces the six fixed defaults (not requiredPreceptsOnly)", () =>
        {
            // 固定多个随机种子运行完整生成，验证普通补齐过程不会覆盖模因的六个默认结果。
            for (int seed = 1; seed <= 8; seed++)
            {
                Rand.PushState(seed);
                try
                {
                    var ideo = Culture(mainMeme); ideo.foundation.RandomizePrecepts(false, Parms()); VerifyGroups(ideo);
                    foreach (string d in FullDefaults) Require(ideo.HasPrecept(D(d)));
                }
                // 恢复原版随机状态，避免本例对后续生成和改革测试产生隐式种子依赖。
                finally { Rand.PopState(); }
            }
        });
        Check("ordinary full generation uses low-attitude defaults and unlimited counts", () =>
        {
            // 无主模因走 defaultSelectionWeight，态度应来自普通低档，两个数量组默认均无限制。
            var ideo = Culture(); ideo.foundation.RandomizePrecepts(false, Parms());
            foreach (string g in groups.Take(3)) Require(ideo.PreceptsListForReading.Single(p => p.def.issue.defName == "SSC_Issue_" + g).def.defaultSelectionWeight > 0);
            Require(ideo.HasPrecept(D("MasterCount_Unlimited")) && ideo.HasPrecept(D("SlaveCount_Unlimited")));
        });
        Check("native reform preserves legal zero-weight choices, replaces conflicts and fills gaps", () =>
        {
            // 同时布置合法零权重、冲突旧档和缺失组，一次实际改革验证保留、替换与补齐三个分支。
            var ideo = Culture(); Add(ideo, "SexSlave_Respect"); Add(ideo, "Trainer_Hostile"); Add(ideo, "Master_Hostile"); Add(ideo, "SlaveCount_One");
            ideo.memes.Add(mainMeme); ideo.foundation.EnsurePreceptsCompatibleWithMemes(new(), ideo.memes.ToList(), Parms()); VerifyGroups(ideo);
            foreach (string d in new[] { "SexSlave_Respect", "Trainer_Hostile", "SlaveCount_One", "Master_HolyMaster", "MasterCount_Three" }) Require(ideo.HasPrecept(D(d)));
        });
        Check("native meme removal replaces exclusive choices but keeps common counts", () =>
        {
            // 移除模因后专属高档不再合法，公共数量档不能因仅有 associatedMemes 就被清除。
            var ideo = Culture(mainMeme); ideo.foundation.RandomizePrecepts(false, Parms());
            ideo.memes.Clear(); ideo.foundation.EnsurePreceptsCompatibleWithMemes(new() { mainMeme }, new(), Parms());
            Require(ideo.PreceptsListForReading.All(p => p.def.requiredMemes.NullOrEmpty()));
            Require(ideo.HasPrecept(D("MasterCount_Three")) && ideo.HasPrecept(D("SlaveCount_Three")));
            Equal(6, ideo.PreceptsListForReading.Count);
        });
        Check("feasibility detects zero positive eligible weight and distinguishes a retained legal choice", () =>
        {
            // 禁止唯一正权重主人档时新组不可生成；已有合法零权重档仍可保留，再禁该档才不可行。
            var parms = Parms(); parms.disallowedPrecepts = new() { D("Master_HolyMaster") };
            var ideo = Culture(mainMeme); Require(!Feasible(ideo, parms));
            Add(ideo, "Master_Deference"); Require(Feasible(ideo, parms));
            parms.disallowedPrecepts.Add(D("Master_Deference")); Require(!Feasible(ideo, parms));
            // 阵营 Def 的禁止列表也必须进入可行性检查，不能只处理一次生成参数的临时禁项。
            var faction = new FactionDef { disallowedPrecepts = new() { D("Master_HolyMaster") } };
            var factionParms = new IdeoGenerationParms(faction); Require(!Feasible(Culture(mainMeme), factionParms));
        });
        VanillaPairTests(game);
    }

    private static void VanillaPairTests(string game)
    {
        Check("all local vanilla normal meme exclusion contracts allow SSC pairings and six positive groups", () =>
        {
            // 仅取本机原版 XML 的模因资格字段；不导入其仪式/建筑/美术等无关依赖。
            // 这是原版 CanAdd 的 SSC 六组组合检查，不宣称完整原版 Def 集或整套文化已加载。
            var nodes = new List<XmlNode>();
            XmlInheritance.Clear();
            foreach (string dir in Directory.EnumerateDirectories(Path.Combine(game, "Data"), "MemeDefs", SearchOption.AllDirectories))
                foreach (string file in Directory.EnumerateFiles(dir, "*.xml"))
                    foreach (var node in XDocument.Load(file).Root.Elements("MemeDef"))
                    {
                        var copy = new XElement("MemeDef", node.Attributes().Where(a => new[] { "Name", "ParentName", "Abstract" }.Contains(a.Name.LocalName)),
                            node.Elements().Where(e => new[] { "defName", "label", "category", "impact", "exclusionTags" }.Contains(e.Name.LocalName)));
                        var doc = new XmlDocument(); doc.LoadXml(copy.ToString());
                        nodes.Add(doc.DocumentElement); XmlInheritance.TryRegister(doc.DocumentElement, null);
                    }
            // 原版继承器合并资格字段，特别是性别至上两档继承的 GenderSupremacy 排斥标签。
            XmlInheritance.Resolve();
            foreach (var node in nodes)
            {
                var resolved = XmlInheritance.GetResolvedNodeFor(node);
                if (resolved["defName"] == null) continue;
                // ObjectFromXml 自身会查询继承表，传原始节点，避免把已合并节点当作未登记子节点。
                var meme = DirectXmlToObject.ObjectFromXml<MemeDef>(node, false);
                DefDatabase<MemeDef>.Add(meme);
                if (meme.category == MemeCategory.Normal) vanillaMemes.Add(meme);
            }
            XmlInheritance.Clear();
            Require(vanillaMemes.Count > 10);
            // 对每个本机普通模因调用原版组合资格，再检查 SSC 六组是否仍有可生成的必选项。
            foreach (var meme in vanillaMemes)
            {
                Require(MemeCanAdd(mainMeme, new() { meme }), meme.defName);
                var ideo = Culture(mainMeme, meme); Require(Feasible(ideo, Parms()), meme.defName);
            }
            // 原版自有互斥标签仍是原来的标签；SSC 没有追加或清除它们。
            Require(vanillaMemes.Any(m => !m.exclusionTags.NullOrEmpty()));
            Require(!MemeCanAdd(vanillaMemes.Single(m => m.defName == "MaleSupremacy"),
                new() { vanillaMemes.Single(m => m.defName == "FemaleSupremacy") }));
            Console.WriteLine($"MATRIX {vanillaMemes.Count} vanilla normal meme contracts paired with SSC");
        });
        Check("native legal multi-meme combinations keep six feasible groups and preserve reform choices in both orders", () =>
        {
            // 先筛掉原版自身互斥的组合，再枚举 SSC 加两个原版模因，避免把无效基底算作 SSC 冲突。
            int tested = 0;
            for (int i = 0; i < vanillaMemes.Count; i++)
                for (int j = i + 1; j < vanillaMemes.Count; j++)
                {
                    var a = vanillaMemes[i]; var b = vanillaMemes[j];
                    if (!MemeCanAdd(b, new() { a })) continue;
                    Require(Feasible(Culture(mainMeme, a, b), Parms())); tested++;
                }
            Require(tested > 10); Console.WriteLine($"MATRIX {tested} legal three-meme contracts (SSC + two vanilla)");
            // 用一个原版模因分别测试先加 SSC 和后加 SSC，合法手选档不能因顺序不同而被重置。
            var other = vanillaMemes.First();
            foreach (bool sscFirst in new[] { false, true })
            {
                var ideo = Culture(sscFirst ? mainMeme : other);
                Add(ideo, "SexSlave_Respect"); Add(ideo, "Trainer_Hostile"); Add(ideo, "Master_Deference");
                Add(ideo, "MasterCount_One"); Add(ideo, "SlaveCount_One");
                Add(ideo, "SlaveGender_FemaleOnly");
                var old = ideo.memes.ToList(); ideo.memes.Add(sscFirst ? other : mainMeme);
                ideo.foundation.EnsurePreceptsCompatibleWithMemes(old, ideo.memes.ToList(), Parms()); VerifyGroups(ideo);
                Require(ideo.HasPrecept(D("Trainer_Hostile")) && ideo.HasPrecept(D("Master_Deference")) && ideo.HasPrecept(D("MasterCount_One")));
            }
        });
        Check("vanilla Lovin_Prohibited qualification contract coexists and survives SSC addition/removal", () =>
        {
            // 仅装载全面禁欲的资格字段，不装载其反馈组件；本例验证共存/改革保留，不声称覆盖行为效果。
            var original = XDocument.Load(Path.Combine(game, "Data/Ideology/Defs/PreceptDefs/Precepts_Lovin.xml"))
                .Root.Elements("PreceptDef").Single(n => (string)n.Element("defName") == "Lovin_Prohibited");
            ReadDef<IssueDef>(new XElement("IssueDef", new XElement("defName", "Lovin")));
            ReadDef<PreceptDef>(new XElement("PreceptDef", original.Elements().Where(e => new[] { "defName", "issue", "label", "description", "impact", "requiredMemes", "conflictingMemes", "associatedMemes", "exclusionTags" }.Contains(e.Name.LocalName))));
            DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
            var prohibition = DefDatabase<PreceptDef>.GetNamed("Lovin_Prohibited"); var ideo = Culture();
            ideo.AddPrecept(PreceptMaker.MakePrecept(prohibition));
            // 先加入再移除主奴秩序，两次均经过原版兼容性维护，禁欲戒律必须始终保留。
            ideo.memes.Add(mainMeme); ideo.foundation.EnsurePreceptsCompatibleWithMemes(new(), ideo.memes.ToList(), Parms());
            Require(ideo.HasPrecept(prohibition)); VerifyGroups(ideo);
            ideo.memes.Clear(); ideo.foundation.EnsurePreceptsCompatibleWithMemes(new() { mainMeme }, new(), Parms());
            Require(ideo.HasPrecept(prohibition));
        });
    }
    // 反射只为访问原版非公开入口，不复制冲突算法；参数沿用本矩阵的无阵营限制组合检查。
    private static bool MemeCanAdd(MemeDef meme, List<MemeDef> existing)
        => (bool)AccessTools.Method(typeof(IdeoUtility), "CanAdd").Invoke(null, new object[] { meme, existing, null, false });
}
