// AI 辅助生成：真实随机神明流程与单模因规则隔离，不替换 FillDeity／GenerateDeities／语法解析。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;
using Deity = RimWorld.IdeoFoundation_Deity.Deity;

internal static partial class Program
{
    private static void DeityGenerationTests(string repo, string language)
    {
        foreach (string cultureName in new[] { "Astropolitan", "Corunan", "Rustican", "Kriminul" })
            Check("native deity fill uses only its related SSC meme: " + cultureName + ": " + language, () =>
            {
                // 结构模因已有一名关联神明，下一名由原版未用候选选择必定关联 SSC，不能借结构规则补缺。
                var structure = namingStructures.Single(m => m.defName == "Structure_TheistEmbodied");
                var ideo = NamingIdeo(true, structure); ideo.culture = DefDatabase<CultureDef>.GetNamed(cultureName);
                var foundation = (IdeoFoundation_Deity)ideo.foundation;
                foundation.SetDeities(new() { new() { name = "EXISTINGSTRUCTUREDEITY", relatedMeme = structure } });
                var seenGenders = new HashSet<Gender>();
                string concept = mainMeme.generalRules.Rules.Single(r => r.keyword == "memeConcept").Generate();
                for (int seed = 0; seed < 16; seed++)
                {
                    // 同一原版 FillDeity 也用于重新随机一名神明；固定种子覆盖男女，结果保留原命名器大小写处理。
                    var deity = new Deity();
                    Seeded(seed, () => { AccessTools.Method(typeof(IdeoFoundation_Deity), "FillDeity").Invoke(foundation, new object[] { deity }); return deity.name; });
                    Equal(mainMeme, deity.relatedMeme); ValidGenerated(deity.name); ValidGenerated(deity.type);
                    Require(deity.type.Contains(concept, StringComparison.OrdinalIgnoreCase), deity.type);
                    seenGenders.Add(deity.gender);
                }
                Require(seenGenders.SetEquals(new[] { Gender.Male, Gender.Female }));
            });
        Check("native random deity button generates complete theist lists: " + language, () =>
        {
            // 直接调用界面按钮使用的公开 GenerateDeities，覆盖两种有神结构和四类文化，不预填名字或称谓。
            foreach (string structureName in new[] { "Structure_TheistEmbodied", "Structure_TheistAbstract" })
                foreach (string cultureName in new[] { "Astropolitan", "Corunan", "Rustican", "Kriminul" })
                {
                    var ideo = NamingIdeo(true, namingStructures.Single(m => m.defName == structureName));
                    ideo.culture = DefDatabase<CultureDef>.GetNamed(cultureName);
                    var foundation = (IdeoFoundation_Deity)ideo.foundation; bool sawSSC = false;
                    for (int seed = 0; seed < 8; seed++)
                    {
                        Seeded(seed, () => { foundation.GenerateDeities(); return "generated"; });
                        Require(foundation.DeitiesListForReading.Count > 0);
                        foreach (var deity in foundation.DeitiesListForReading)
                        {
                            ValidGenerated(deity.name); ValidGenerated(deity.type);
                            sawSSC |= deity.relatedMeme == mainMeme;
                        }
                    }
                    Require(sawSSC);
                }
        });
        Check("native deity grammar rejects a missing related-meme concept: " + language, () =>
        {
            // 负例只在内存移除新增关键词，重现玩家日志中的独立请求；不能靠额外加入其他模因规则让它通过。
            var entries = XDocument.Load(Path.Combine(repo, $"1.7/Languages/{language}/DefInjected/MemeDef/SSC_Culture.xml")).Root.Elements()
                .Where(e => e.Name.LocalName.Contains(".generalRules.rulesStrings."))
                .Select(e => e.Value).Where(v => !v.StartsWith("memeConcept->", StringComparison.Ordinal));
            var bare = DirectXmlToObject.ObjectFromXml<RulePack>(Xml(new XElement("generalRules",
                new XElement("rulesStrings", entries.Select(v => new XElement("li", v))))), false);
            var request = new GrammarRequest(); request.Includes.Add(DefDatabase<RulePackDef>.GetNamed("DeityTypeMakerAstropolitan"));
            request.IncludesBare.Add(bare); request.Constants.Add("gender", Gender.Female.ToString());
            GrammarResolver.ResolveUnsafe("r_deityType", request, out bool success);
            Require(!success);
            // 原版会记录预期的失败日志，只接受本根规则的缺词错误；其他错误仍使负例失败。
            Require(errors.All(e => e.Contains("r_deityType") && e.Contains("memeConcept")));
            errors.Clear();
        });
    }
}
