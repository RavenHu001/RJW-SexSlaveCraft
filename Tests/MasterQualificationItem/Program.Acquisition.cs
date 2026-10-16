using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class Program
{
    private const string ModuleName = "SSC_MasterQualificationItem";
    private const string TempleName = "MapGen_AncientTempleContents";
    private const string RoomName = "MapGen_AncientComplexRoomLoot_Default";
    private const string BetterRoomName = "MapGen_AncientComplexRoomLoot_Better";
    private const string CrateName = "MapGen_AncientComplex_SecurityCrate";

    /// <summary>核对实际 XML 的长期入口及精确追加范围，不把 XML 模型当作原版物品生成测试。</summary>
    private static void RunAcquisitionDefinitions()
    {
        Run("模块仅由奖励获取，停用商人库存且不受研究或 DLC 控制", () => {
            var item = XDocument.Load(SscTestPaths.Combine(root, "Defs/ThingsDefs/SSC_MasterQualificationItem.xml")).Root.Element("ThingDef");
            Check((string)item.Element("tradeability") == "Sellable" && item.Element("tradeTags") == null);
            Check(item.Element("thingSetMakerTags").Elements("li").Select(x => x.Value).SequenceEqual(new[] { "RewardStandardHighFreq" }));
            Check((int)item.Element("minRewardCount") == 1 && (int)item.Element("stackLimit") == 10);
            Check((decimal)item.Element("statBases").Element("MarketValue") == 1000);
            Check(!item.DescendantsAndSelf().Attributes().Any(x => x.Name == "MayRequire"));
            Check(item.Element("requiresFactionToAcquire") == null && item.Element("destroyOnDrop") == null);
            Check(!item.Descendants().Any(x => x.Name.LocalName.Contains("research", StringComparison.OrdinalIgnoreCase)));
        });
        Run("神殿并入既有高价值组，不添加独立模块概率分支", () => {
            var defs = Fixture(false); var options = Options(defs, TempleName); string original = options.InnerXml;
            ApplySources(defs);
            var module = ModuleOption(options);
            Check(module.SelectSingleNode("chance").InnerText == "0.5" && options.SelectNodes("li").Count == 2);
            var list = module.SelectSingleNode("thingSetMaker/fixedParams/filter/thingDefs");
            Check(list.SelectSingleNode("li[text()='MechSerumResurrector']") != null);
            list.RemoveChild(list.SelectSingleNode($"li[text()='{ModuleName}']"));
            Check(options.InnerXml == original);
        });
        Run("Core 普通房间采用单件神器等效权重 0.002，命中一个", () => {
            var defs = Fixture(false); var options = Options(defs, RoomName); string original = options.InnerXml;
            ApplySources(defs); Check(options.InnerXml.StartsWith(original, StringComparison.Ordinal));
            var module = ModuleOption(options);
            Check(module.SelectSingleNode("weight").InnerText == "0.002" && module.SelectSingleNode("chance") == null);
            CheckSingleModule(module); Check(options.SelectNodes("li").Count == 3);
        });
        Run("缺 Ideology 增强池与安全箱时独立成功跳过", () => {
            var defs = Fixture(false); ApplySources(defs);
            Check(defs.SelectNodes($"/Defs/ThingSetMakerDef[defName='{BetterRoomName}']").Count == 0);
            Check(defs.SelectNodes($"/Defs/ThingSetMakerDef[defName='{CrateName}']").Count == 0);
            Check(defs.SelectNodes($"//thingDefs/li[text()='{ModuleName}']").Count == 2);
        });
        Run("增强房间并入心灵枪组并保留 0.05 组权重", () => CheckArtifactGroup(BetterRoomName, "0.05"));
        Run("安全箱并入心灵枪组并保留 0.5 组权重", () => CheckArtifactGroup(CrateName, "0.5"));
        Run("一个可选神器池缺失不阻止另一个接入", () => {
            foreach (string absent in new[] { BetterRoomName, CrateName }) {
                var defs = Fixture(true); var missing = defs.SelectSingleNode($"/Defs/ThingSetMakerDef[defName='{absent}']");
                missing.ParentNode.RemoveChild(missing); ApplySources(defs);
                Check(ModuleOption(Options(defs, absent == BetterRoomName ? CrateName : BetterRoomName)) != null);
            }
        });
        Run("来源补丁不改其他物品池、原版蓝图或研究", () => {
            var defs = Fixture(true); var untouched = defs.SelectNodes("/Defs/*[defName='OtherLoot' or defName='SSC_RES_BasicTraining' or defName='Techprint_SSC_RES_BasicTraining']")
                .Cast<XmlNode>().Select(x => x.OuterXml).ToArray();
            ApplySources(defs);
            Check(untouched.SequenceEqual(defs.SelectNodes("/Defs/*[defName='OtherLoot' or defName='SSC_RES_BasicTraining' or defName='Techprint_SSC_RES_BasicTraining']")
                .Cast<XmlNode>().Select(x => x.OuterXml)));
            var patch = XDocument.Load(SscTestPaths.Combine(root, "Patches/SSC_MasterQualificationSources.xml"));
            Check(!patch.Descendants().Attributes().Any(x => x.Name == "MayRequire"));
            Check(patch.Descendants("value").Descendants("li").Where(x => !x.HasElements).All(x => x.Value == ModuleName));
        });
        Run("Core 必需目标缺失报告失败，不能悄悄失去遗迹入口", () => {
            var defs = Fixture(false); var node = defs.SelectSingleNode($"/Defs/ThingSetMakerDef[defName='{TempleName}']");
            node.ParentNode.RemoveChild(node); Check(!ApplyPatch(LoadPatch().SelectSingleNode("/Patch/Operation"), defs));
        });
    }

    /// <summary>同组候选共享原版选择规则；删除新增模块后整份原组必须保持一致。</summary>
    private static void CheckArtifactGroup(string name, string weight)
    {
        var defs = Fixture(true); var options = Options(defs, name); string original = options.InnerXml;
        ApplySources(defs); var group = ModuleOption(options);
        Check(group.SelectSingleNode("weight").InnerText == weight && options.SelectNodes("li").Count == 2);
        Check(group.SelectSingleNode("thingSetMaker/fixedParams/countRange").InnerText == "1");
        var list = group.SelectSingleNode("thingSetMaker/fixedParams/filter/thingDefs");
        Check(list.SelectSingleNode("li[text()='Apparel_PsychicShockLance']") != null);
        Check(list.SelectSingleNode("li[text()='Apparel_PsychicInsanityLance']") != null);
        list.RemoveChild(list.SelectSingleNode($"li[text()='{ModuleName}']"));
        Check(options.InnerXml == original);
    }

    /// <summary>保留已有物品和无关 Def 的最小夹具，专测追加、不覆盖及可选目标缺失。</summary>
    private static XmlDocument Fixture(bool includeBetter)
    {
        var defs = new XmlDocument(); defs.LoadXml("<Defs/>");
        foreach (var (name, type) in new[] { (TempleName, "ThingSetMaker_Sum"), (RoomName, "ThingSetMaker_RandomOption"), (BetterRoomName, "ThingSetMaker_RandomOption"), (CrateName, "ThingSetMaker_RandomOption"), ("OtherLoot", "ThingSetMaker_Sum") }) {
            if ((name == BetterRoomName || name == CrateName) && !includeBetter) continue;
            string candidates = name == TempleName ? "<li>MechSerumResurrector</li><li>TechprofSubpersonaCore</li>" :
                (name == BetterRoomName || name == CrateName) ? "<li>Apparel_PsychicShockLance</li><li>Apparel_PsychicInsanityLance</li>" : "<li>Luciferium</li>";
            string weight = name == BetterRoomName ? "0.05" : name == CrateName ? "0.5" : "1";
            var doc = new XmlDocument();
            doc.LoadXml($"<ThingSetMakerDef><defName>{name}</defName><root Class='{type}'><options><li><weight>{weight}</weight><chance>0.5</chance><thingSetMaker Class='ThingSetMaker_StackCount'><fixedParams><filter><thingDefs>{candidates}</thingDefs></filter><countRange>1</countRange></fixedParams></thingSetMaker></li><li><weight>0.15</weight><thingSetMaker Class='OtherMod.CustomMaker'/></li></options></root></ThingSetMakerDef>");
            defs.DocumentElement.AppendChild(defs.ImportNode(doc.DocumentElement, true));
        }
        foreach (string xml in new[] { "<ResearchProjectDef><defName>SSC_RES_BasicTraining</defName><baseCost>500</baseCost></ResearchProjectDef>", "<ThingDef><defName>Techprint_SSC_RES_BasicTraining</defName><comps><li Class='CompProperties_Techprint'/></comps></ThingDef>" }) {
            var doc = new XmlDocument(); doc.LoadXml(xml); defs.DocumentElement.AppendChild(defs.ImportNode(doc.DocumentElement, true));
        }
        return defs;
    }
    private static XmlNode Options(XmlDocument defs, string name) => defs.SelectSingleNode($"/Defs/ThingSetMakerDef[defName='{name}']/root/options");
    private static XmlNode ModuleOption(XmlNode options) => options.SelectSingleNode($"li[thingSetMaker/fixedParams/filter/thingDefs/li='{ModuleName}']");
    private static void CheckSingleModule(XmlNode option)
    {
        Check(option != null && option.SelectSingleNode("thingSetMaker").Attributes["Class"].Value == "ThingSetMaker_StackCount");
        Check(option.SelectSingleNode("thingSetMaker/fixedParams/countRange").InnerText == "1");
        Check(option.SelectNodes("thingSetMaker/fixedParams/filter/thingDefs/li").Count == 1);
    }
    private static XmlDocument LoadPatch()
    { var doc = new XmlDocument(); doc.Load(SscTestPaths.Combine(root, "Patches/SSC_MasterQualificationSources.xml")); return doc; }
    private static void ApplySources(XmlDocument defs) => Check(ApplyPatch(LoadPatch().SelectSingleNode("/Patch/Operation"), defs));

    /// <summary>仅实现本文件使用的 XML 操作子集，核对 XPath 和追加语义；原版 Apply 另由实库审计执行。</summary>
    private static bool ApplyPatch(XmlNode operation, XmlDocument defs)
    {
        switch (operation.Attributes["Class"].Value) {
            case "PatchOperationSequence":
                foreach (XmlNode child in operation.SelectNodes("operations/li")) if (!ApplyPatch(child, defs)) return false;
                return true;
            case "PatchOperationAdd":
                var targets = defs.SelectNodes(operation.SelectSingleNode("xpath").InnerText);
                foreach (XmlNode target in targets) foreach (XmlNode child in operation.SelectSingleNode("value").ChildNodes)
                    if (child.NodeType == XmlNodeType.Element) target.AppendChild(defs.ImportNode(child, true));
                return targets.Count > 0;
            case "PatchOperationConditional":
                return defs.SelectSingleNode(operation.SelectSingleNode("xpath").InnerText) == null || ApplyPatch(operation.SelectSingleNode("match"), defs);
            default: throw new InvalidOperationException("Unsupported patch operation in acquisition test");
        }
    }
}
