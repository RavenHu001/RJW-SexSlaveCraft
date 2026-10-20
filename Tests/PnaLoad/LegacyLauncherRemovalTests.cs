// AI 辅助生成：防止归档旧 Def／源码重新进入 1.7，同时保护现役九武器及其公共身体状态。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SexSlaveCraft;

internal static partial class Program
{
    private static void RunLegacyLauncherRemovalTests()
    {
        string[] obsolete = { "Gun_PNA_Low", "Bullet_PNA_Low", "Gun_PNA_High", "Bullet_PNA_High",
            "Damage_PNA_Damage_LowTier", "Damage_PNA_HighTier", "SSC_RES_PNASimpleLauncher", "Make_Gun_PNA_Low" };
        foreach (string folder in new[] { "Defs", "Patches" })
            Run("退役 Def 不得残留活动定义或引用 " + folder, () =>
            {
                foreach (string path in Directory.GetFiles(SscTestPaths.Combine(root, folder), "*.xml", SearchOption.AllDirectories))
                {
                    var xml = XDocument.Load(path);
                    foreach (var e in xml.Descendants())
                        Assert(!obsolete.Contains(e.Value.Trim()) && !e.Attributes().Any(a => obsolete.Any(a.Value.Contains)), path + ": " + e.Name);
                }
            });
        Run("退役伤害类型不进入当前测试程序集", () =>
        {
            foreach (string type in new[] { "PNA_DWExtension", "DamageWorker_SSCPna", "DamageWorker_LowTier", "DamageWorker_HighTier", "SSCPnaWeaponResistancePolicy" })
                Assert(typeof(SSCPnaWeaponDoseUtility).Assembly.GetType("SexSlaveCraft." + type) == null, "类型已退役 " + type);
            Assert(typeof(SSCPnaWeaponDoseUtility).GetMethod("FromSeverity", BindingFlags.NonPublic | BindingFlags.Static) == null, "旧严重度入口已退役");
        });
        Run("废案源码与配置存在且不在编译清单", () =>
        {
            foreach (string file in new[] { "DamageWorker_PNA.cs", "SSCPnaWeaponDoseUtility.Legacy.cs", "README.md", "Defs/SSC_Gun.xml", "Defs/DemageDef_Gun.xml", "Defs/Research_PNASimpleLauncher.xml" })
                Assert(File.Exists(Path.Combine(root, "Archive/PnaSimpleLauncher", file)), "归档存在 " + file);
            foreach (string project in new[] { "Sexslavecraft/SexSlaveCraft_Alpha.csproj", "Tests/PnaLoad/PnaLoad.csproj" })
            {
                var includes = XDocument.Load(Path.Combine(root, project)).Descendants().Where(e => e.Name.LocalName == "Compile").Select(e => (string)e.Attribute("Include") ?? "").ToArray();
                Assert(!includes.Any(s => s.Contains("DamageWorker_PNA.cs") || s.Contains("Archive")), "归档不编译 " + project);
            }
            Assert(!XDocument.Load(Path.Combine(root, "LoadFolders.xml")).Descendants("li").Any(e => e.Value.Contains("Archive")), "归档不加载");
        });
        Run("现役九武器、三代研究与公共抗性记录保留", () =>
        {
            string[] files = { "SSC_PnaPrimitiveWeapons.xml", "SSC_PnaIndustrialWeapons.xml", "SSC_PnaSpacerWeapons.xml" };
            foreach (string file in files)
            {
                var weapons = XDocument.Load(SscTestPaths.Combine(root, "Defs/ThingsDefs", file)).Root.Elements().Where(e => ((string)e.Element("defName") ?? "").StartsWith("SSC_Weapon_Pna")).ToArray();
                Equal(3, weapons.Length, "每代三把武器 " + file);
                foreach (var weapon in weapons)
                    Assert(!obsolete.Contains(weapon.Element("recipeMaker")?.Element("researchPrerequisite")?.Value), "制作前置有效");
            }
            LoadDef("Defs/ResearchDefs/ResearchProjectDef.xml", "SSC_RES_PNASimpleApp");
            LoadDef("Defs/ResearchDefs/ResearchProjectDef.xml", "SSC_RES_PNAConcentration");
            LoadDef("Defs/HediffDefs/HediffOfBuff.xml", "Hediff_PNA_Consciousness_Record");
        });
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Run("四语退役条目清理 " + language, () =>
            {
                foreach (string folder in new[] { "Languages", "Sexslavecraft/Languages" })
                {
                    string path = folder == "Languages" ? SscTestPaths.Combine(root, folder, language) : Path.Combine(root, folder, language);
                    Assert(!File.Exists(Path.Combine(path, "DefInjected/ThingDef/SSC_Gun.xml")), "旧枪说明已移除");
                    foreach (string file in Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories))
                        foreach (var e in XDocument.Load(file).Descendants())
                            Assert(!obsolete.Any(n => e.Name.LocalName.StartsWith(n + ".")) &&
                                e.Name.LocalName != "SSC_Pna_WeaponHealed" && e.Name.LocalName != "SSC_Pna_WeaponActivated", "无悬空翻译 " + file);
                }
            });
    }
}
