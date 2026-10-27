// AI 辅助生成：阶段实际属性运算、原生 XML 补丁/组件及首次妊娠待遇，不复制被测逻辑。
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using rjw;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly string[] stageStats = { "SexFrequency", "SexSatisfaction", "RestRateMultiplier", "InjuryHealingFactor", "ImmunityGainSpeed" };
    private static readonly string[] pregnancyNames = { "Pregnant", "PregnantHuman", "RJW_pregnancy", "RJW_pregnancy_beast" };
    private static int laborCalls;

    /// <summary>从本机 Def 确认名称与基础值；移除其他系统影响，专门验证生产 Hediff 的原生属性运算。</summary>
    private static void LoadStageStatReferences(string game)
    {
        var rjw = XDocument.Load(Path.Combine(game, "Mods/RJW_核心_本体/1.6/Defs/StatDefs/Stats.xml"));
        var core = XDocument.Load(Path.Combine(game, "Data/Core/Defs/Stats/Stats_Pawns_General.xml"));
        foreach (string name in stageStats)
        {
            var node = rjw.Root.Elements().Concat(core.Root.Elements()).Single(x => (string)x.Element("defName") == name);
            DefDatabase<StatDef>.Add(new StatDef { defName = name, defaultBaseValue = (float?)node.Element("defaultBaseValue") ?? 1f });
        }
    }

    /// <summary>先验证阶段，再加载真实妊娠 XML 和生产补丁，最后运行两类孕期及真实存档测试。</summary>
    private static void PregnancyTests(string repo, string game, Harmony harmony)
    {
        StageBenefitTests(harmony);
        Current.Game.World = Bare<World>(); Current.Game.World.worldPawns = new WorldPawns();
        // xxx 静态表还读取本批未加载的众多特性/Thought；只预热这些外部引用，业务测试期间仍收集全部错误。
        bootstrap = true;
        try { RuntimeHelpers.RunClassConstructor(typeof(xxx).TypeHandle); }
        finally { bootstrap = false; }
        harmony.Patch(AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly)),
            prefix: new HarmonyMethod(typeof(Program), nameof(PregnancyTypeBoundary)));
        errors.Clear();
        var pregnancyXml = LoadPregnancyResources(repo, game);
        Require(errors.Count == 0, string.Join('\n', errors));
        Check("batch4 native XML patch preserves original comps and adds exactly one pregnancy comp", () => ValidatePregnancyResources(pregnancyXml));
        PregnancyDecisionTests();
        PregnancyFlowTests(harmony);
        PregnancySaveTests(harmony);
    }

    /// <summary>使用原版 StatWorker 的 Hediff 运算，固定其他系统为空；不把期望倍率写入宿主算法。</summary>
    private static void StageBenefitTests(Harmony harmony)
    {
        human.race.lifeStageAges.Add(new LifeStageAge { minAge = 0, def = new LifeStageDef { defName = "StageAdult" } });
        foreach (var example in new[] {
            (.01f, false, new[] {1f,1f,1f,1f,1f}), (.29999f, false, new[] {1f,1f,1f,1f,1f}),
            (.3f, false, new[] {1.1f,1.1f,1.05f,1.1f,1f}), (.69999f, false, new[] {1.1f,1.1f,1.05f,1.1f,1f}),
            (.7f, false, new[] {1.25f,1.25f,1.1f,1.2f,1f}), (.99999f, false, new[] {1.25f,1.25f,1.1f,1.2f,1f}),
            (1f, false, new[] {1.5f,1.5f,1.35f,1.5f,1.2f}), (1f, true, new[] {1.5f,1.5f,1.25f,1.35f,1.3f}) })
            Check("batch4 actual native stage stat calculation: " + example.Item1 + "/" + example.Item2, () =>
            {
                var p = NewPawn(); p.ageTracker = new Pawn_AgeTracker(p); Start(p, example.Item1);
                if (example.Item2) Toggle(p, true);
                for (int i = 0; i < stageStats.Length; i++) Near(example.Item3[i], StageStat(p, stageStats[i]));
            });
        Check("batch4 stage changes and full toggles refresh without mutating other bodies", () =>
        {
            var p = NewPawn(); p.ageTracker = new Pawn_AgeTracker(p); var h = Start(p);
            var other = NewPawn(); other.ageTracker = new Pawn_AgeTracker(other); Start(other, 1f);
            h.Severity = .3f; Near(1.05f, StageStat(p, "RestRateMultiplier"));
            h.Severity = .7f; Near(1.1f, StageStat(p, "RestRateMultiplier"));
            h.Severity = 1; Toggle(p, true); Near(1.25f, StageStat(p, "RestRateMultiplier"));
            Near(1.35f, StageStat(other, "RestRateMultiplier")); Toggle(p, false); Near(1.35f, StageStat(p, "RestRateMultiplier"));
            p.health.RemoveHediff(h); Near(1f, StageStat(p, "RestRateMultiplier"));
        });
        Check("batch4 native stat worker multiplies existing body development exactly once", () =>
        {
            var p = NewPawn(); p.ageTracker = new Pawn_AgeTracker(p); Start(p, 1);
            var old = new HediffDef { defName = "OldBodyStatFixture", stages = new() { new HediffStage { statFactors = new() {
                new StatModifier { stat = DefDatabase<StatDef>.GetNamed("RestRateMultiplier"), value = 1.5f },
                new StatModifier { stat = DefDatabase<StatDef>.GetNamed("InjuryHealingFactor"), value = 2f } } } } };
            p.health.hediffSet.hediffs.Add(new Hediff { def = old, pawn = p });
            Near(2.025f, StageStat(p, "RestRateMultiplier")); Near(3, StageStat(p, "InjuryHealingFactor"));
        });
    }

    /// <summary>调用真实原版属性计算入口；夹具无装备、特性或能力修正。</summary>
    private static float StageStat(Pawn p, string name) => DefDatabase<StatDef>.GetNamed(name).Worker.GetValueUnfinalized(StatRequest.For(p), false);

    /// <summary>原版 PatchOperation 和 XML 继承实际应用于本机妊娠定义，保留现有 comps 与阶段。</summary>
    private static XmlDocument LoadPregnancyResources(string repo, string game)
    {
        var doc = new XmlDocument(); doc.LoadXml("<Defs/>");
        void Import(string path, Func<XElement, bool> include)
        {
            foreach (var node in XDocument.Load(path).Root.Elements().Where(include))
            { var part = new XmlDocument(); part.LoadXml(node.ToString()); doc.DocumentElement.AppendChild(doc.ImportNode(part.DocumentElement, true)); }
        }
        Import(Path.Combine(game, "Data/Core/Defs/HediffDefs/Hediffs_Global_Misc.xml"), n => (string)n.Element("defName") == "Pregnant");
        Import(Path.Combine(game, "Data/Biotech/Defs/HediffDefs/Hediffs_Global_Misc.xml"), n => (string)n.Element("defName") == "PregnantHuman");
        Import(Path.Combine(game, "Mods/RJW_核心_本体/1.6/Defs/HediffDefs/Hediffs_Pregnancy.xml"), n => true);
        var patch = new XmlDocument(); patch.Load(Path.Combine(repo, "1.7/Patches/SSC_UterusPregnancy.xml"));
        var op = DirectXmlToObject.ObjectFromXml<PatchOperation>(patch.DocumentElement.SelectSingleNode("Operation"), false);
        Require(op.Apply(doc), "native pregnancy XML patch failed");
        foreach (XmlNode node in doc.SelectNodes(".//capacity"))
            if (DefDatabase<PawnCapacityDef>.GetNamedSilentFail(node.InnerText) == null)
                DefDatabase<PawnCapacityDef>.Add(new PawnCapacityDef { defName = node.InnerText });
        foreach (XmlNode node in doc.SelectNodes(".//letterType|.//messageType"))
        {
            if (node.Name == "letterType" && DefDatabase<LetterDef>.GetNamedSilentFail(node.InnerText) == null)
                DefDatabase<LetterDef>.Add(new LetterDef { defName = node.InnerText });
            if (node.Name == "messageType" && DefDatabase<MessageTypeDef>.GetNamedSilentFail(node.InnerText) == null)
                DefDatabase<MessageTypeDef>.Add(new MessageTypeDef { defName = node.InnerText });
        }
        XmlInheritance.Clear();
        foreach (XmlNode node in doc.DocumentElement.ChildNodes) if (node is XmlElement element) XmlInheritance.TryRegister(element, null);
        XmlInheritance.Resolve();
        foreach (XmlElement node in doc.DocumentElement.ChildNodes.OfType<XmlElement>().Where(n => n.GetAttribute("Abstract") != "True"))
        {
            var resolved = (XmlElement)XmlInheritance.GetResolvedNodeFor(node).CloneNode(true);
            resolved.RemoveAttribute("ParentName"); resolved.RemoveAttribute("Abstract");
            var def = DirectXmlToObject.ObjectFromXml<HediffDef>(resolved, false);
            var old = DefDatabase<HediffDef>.GetNamedSilentFail(def.defName);
            // 前三批只使用命名占位；本批替换该槽位的可变配置，保留数据库身份与静态引用。
            if (old != null)
            {
                old.hediffClass = def.hediffClass; old.comps = def.comps; old.stages = def.stages;
                old.initialSeverity = def.initialSeverity; old.maxSeverity = def.maxSeverity;
                old.pregnant = def.pregnant; old.preventsPregnancy = def.preventsPregnancy;
            }
            else DefDatabase<HediffDef>.Add(def);
        }
        XmlInheritance.Clear(); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        HediffDefOf.Pregnant = DefDatabase<HediffDef>.GetNamed("Pregnant"); HediffDefOf.PregnantHuman = DefDatabase<HediffDef>.GetNamed("PregnantHuman");
        return doc;
    }

    private static void ValidatePregnancyResources(XmlDocument doc)
    {
        foreach (string name in pregnancyNames)
            Equal(1, doc.SelectNodes($"Defs/HediffDef[defName='{name}']/comps/li[compClass='SexSlaveCraft.HediffComp_SSCUterusPregnancy']").Count);
        Equal(0, doc.SelectNodes("Defs/HediffDef[defName='RJW_pregnancy_mech']/comps").Count);
        Equal(2, doc.SelectNodes("Defs/HediffDef[defName='PregnantHuman']/comps/li[@Class or compClass='HediffComp_PregnantHuman']").Count);
    }

    /// <summary>使用真正妊娠类和原版 Make/PostMake；RJW 人形关联 Def 的 XML 基类为抽象类，按真实具体实例建立。</summary>
    private static HediffWithComps NewPregnancy(Pawn mother, string name, bool add = true)
    {
        var def = DefDatabase<HediffDef>.GetNamed(name); HediffWithComps h;
        if (name == "RJW_pregnancy")
        { h = new Hediff_HumanlikePregnancy { def = def, pawn = mother, loadID = ++nextId }; h.PostMake(); }
        else h = (HediffWithComps)HediffMaker.MakeHediff(def, mother, mother.RaceProps.body.corePart);
        Require(h.TryGetComp<HediffComp_SSCUterusPregnancy>() != null);
        if (add) mother.health.AddHediff(h, mother.RaceProps.body.corePart);
        return h;
    }

    /// <summary>覆盖首次加入和冻结待遇，包含加入前切换与旧妊娠不补发。</summary>
    private static void PregnancyDecisionTests()
    {
        foreach (string name in pregnancyNames)
        foreach (int state in new[] { 0, 1, 2, 3 })
            Check("batch4 first real Add freezes eligibility: " + name + "/" + state, () =>
            {
                var p = NewPawn(); if (state > 0) Start(p, state == 1 ? .7f : 1f); if (state == 3) Toggle(p, true);
                var h = NewPregnancy(p, name); Equal(state == 3, h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                Require(SSCUterusPregnancyUtility.IsSupported(h));
            });
        foreach (string name in pregnancyNames)
        {
            Check("batch4 PostMake never grants early and actual Add reads then-current toggle: " + name, () =>
            {
                var p = NewPawn(); Start(p, 1); var h = NewPregnancy(p, name, false);
                Require(!h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened); Toggle(p, true);
                h.PostAdd(null); Require(!h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                p.health.AddHediff(h, p.RaceProps.body.corePart); Require(h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
            });
            Check("batch4 normal existing pregnancy never gains on later enable or repeated Add: " + name, () =>
            {
                var p = NewPawn(); Start(p, 1); var h = NewPregnancy(p, name); Toggle(p, true);
                h.PostAdd(null); Require(!h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
            });
            Check("batch4 shortened pregnancy keeps frozen grant after development is removed: " + name, () =>
            {
                var p = NewPawn(); var body = Start(p, 1); Toggle(p, true); var h = NewPregnancy(p, name);
                p.health.RemoveHediff(body); h.PostAdd(null); Require(h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
            });
        }
        Check("batch4 wrong Def or nonregular pregnancy types never become supported", () =>
        {
            var p = NewPawn(); Start(p, 1); Toggle(p, true);
            var h = new Hediff_Pregnant { pawn = p, def = DefDatabase<HediffDef>.GetNamed("RJW_pregnancy") };
            Require(!SSCUterusPregnancyUtility.IsSupported(h));
            h.def = new HediffDef { defName = "ExternalPregnancy" }; Require(!SSCUterusPregnancyUtility.IsSupported(h));
            Require(!SSCUterusPregnancyUtility.IsSupported(new Hediff_MechanoidPregnancy { pawn = p, def = DefDatabase<HediffDef>.GetNamed("RJW_pregnancy_mech") }));
        });
    }
}
