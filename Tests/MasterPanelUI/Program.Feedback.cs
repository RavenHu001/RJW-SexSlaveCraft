// AI 辅助生成
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>运行真实经验入口；消息宿主只记录输出参数，健康宿主允许 setter 回调重入。</summary>
    private static void RunFeedbackTests()
    {
        foreach (int threshold in new[] { 12, 40, 100 })
            Run($"经验 {threshold - 1}→{threshold} 仅首次跨阶提示", () =>
            {
                var pawn = Grown(threshold - 1);
                Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 1) == 1);
                Assert(Messages.Received.Count == 1);
                SSCMasterProgressionUtility.TryAddExperience(pawn, 1);
                Assert(Messages.Received.Count == 1);
            });
        Run("未跨阶、非正增量与满级不提示", () =>
        {
            var pawn = Grown(0);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 1) == 1);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 0) == 0);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, -1) == 0);
            SSCMasterProgressionUtility.RestoreRecord(pawn, 100);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 10) == 0);
            Assert(Messages.Received.Count == 0);
        });
        Run("调试 +10 跨阶走同一消息入口", () =>
        {
            var pawn = Grown(11);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, SSCMasterProgressionUtility.DebugExperience) == 10);
            Assert(Messages.Received.Count == 1 && Messages.Received[0].Text.Contains(pawn.health.hediffSet.hediffs[0].def.stages[1].label));
        });
        Run("一次跨多个阈值仅提示最终阶段", () =>
        {
            var pawn = Grown(0);
            SSCMasterProgressionUtility.TryAddExperience(pawn, 100);
            Assert(Messages.Received.Count == 1);
            Assert(Messages.Received[0].Text == "SSC_MasterProgression_StageAdvanced".Translate(
                pawn.LabelShort, SSCDefOf.SSC_Hediff_MasterProgression.label, SSCDefOf.SSC_Hediff_MasterProgression.stages[3].label));
        });
        Run("非玩家正常增长不发玩家消息", () =>
        {
            var pawn = Grown(11); pawn.Faction = new Faction();
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 1) == 1 && Messages.Received.Count == 0);
        });
        Run("建立、精确恢复和身份刷新不报升阶", () =>
        {
            var pawn = new Pawn();
            Assert(SSCMasterProgressionUtility.EnsureInitialRecord(pawn));
            Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, 100));
            SSCMasterProgressionUtility.NotifyIdentityChanged(pawn, false);
            pawn.Training.pawnIdentity = PawnIdentity.Unset;
            SSCMasterProgressionUtility.RefreshActivation(pawn);
            pawn.Training.pawnIdentity = PawnIdentity.Master;
            SSCMasterProgressionUtility.NotifyIdentityChanged(pawn, true);
            Assert(Messages.Received.Count == 0 && SSCMasterProgressionUtility.GetExperience(pawn) == 100);
        });
        foreach (string mutation in new[] { "removed", "replaced", "identity", "hollow", "dead", "discarded", "faction" })
            Run("跨阶健康回调后不向失效人格提示：" + mutation, () =>
            {
                var pawn = Grown(11);
                pawn.health.OnChanged = () =>
                {
                    pawn.health.OnChanged = null;
                    switch (mutation)
                    {
                        case "removed": SSCMasterProgressionUtility.ClearPersonalityRecord(pawn); break;
                        case "replaced": SSCMasterProgressionUtility.RestoreRecord(pawn, 40); break;
                        case "identity": pawn.Training.pawnIdentity = PawnIdentity.Unset; break;
                        case "hollow": pawn.health.AddHediff(new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done }); break;
                        case "dead": pawn.Dead = true; break;
                        case "discarded": pawn.Discarded = true; break;
                        case "faction": pawn.Faction = new Faction(); break;
                    }
                };
                SSCMasterProgressionUtility.TryAddExperience(pawn, 1);
                Assert(Messages.Received.Count == 0);
            });
        Run("默认 Def 与简中注入一致且四语悬停字段镜像完整", () =>
        {
            var def = XDocument.Load(Path.Combine(repo, "Defs/HediffDefs/SSC_MasterProgression.xml")).Root.Element("HediffDef");
            foreach (string language in Languages)
            {
                string relative = "DefInjected/HediffDef/SSC_MasterProgression.xml";
                var entries = XDocument.Load(Path.Combine(repo, "Languages", language, relative)).Root;
                Assert(entries.Elements().Count() == 11);
                foreach (string field in new[] { "label", "description", "extraTooltip" })
                {
                    string value = entries.Element("SSC_Hediff_MasterProgression." + field)?.Value;
                    Assert(!string.IsNullOrWhiteSpace(value));
                    if (language == "ChineseSimplified") Assert(value == def.Element(field).Value);
                }
                for (int i = 0; i < 4; i++)
                    foreach (string field in new[] { "label", "extraTooltip" })
                    {
                        string value = entries.Element($"SSC_Hediff_MasterProgression.stages.{i}.{field}")?.Value;
                        Assert(!string.IsNullOrWhiteSpace(value));
                        if (language == "ChineseSimplified") Assert(value == def.Element("stages").Elements().ElementAt(i).Element(field).Value);
                    }
                Assert(File.ReadAllBytes(Path.Combine(repo, "Languages", language, relative)).SequenceEqual(
                    File.ReadAllBytes(Path.Combine(repo, "Sexslavecraft/Languages", language, relative))));
            }
        });
    }

    /// <summary>四语实际名称参与消息格式；定位和历史参数属于生产代码输出，不由宿主推导。</summary>
    private static void StageFeedback()
    {
        var pawn = Grown(11); pawn.LabelShort = "Tester";
        Assert(Messages.Received.Count == 0);
        SSCMasterProgressionUtility.TryAddExperience(pawn, 1);
        var entry = Messages.Received.Single();
        var def = SSCDefOf.SSC_Hediff_MasterProgression;
        Assert(entry.Text == "SSC_MasterProgression_StageAdvanced".Translate(pawn.LabelShort, def.label, def.stages[1].label));
        Assert(entry.Text.Contains(pawn.LabelShort) && entry.Text.Contains(def.label) && entry.Text.Contains(def.stages[1].label));
        Assert(entry.Target == pawn && entry.Type == MessageTypeDefOf.PositiveEvent && !entry.Historical);
    }

    /// <summary>阶段短句沿原版 extraTooltip 字段；属性计算和完整健康提示保留实机边界。</summary>
    private static void StageDescriptions()
    {
        var def = SSCDefOf.SSC_Hediff_MasterProgression;
        for (int i = 0; i < def.stages.Count; i++)
        {
            var pawn = Grown(def.stages[i].minSeverity);
            var record = SSCMasterProgressionUtility.GetRecord(pawn);
            string tooltip = record.GetTooltip(pawn, false);
            Assert(tooltip.Contains(def.description) && tooltip.Contains(def.extraTooltip) && tooltip.Contains(def.stages[i].extraTooltip));
            var view = Tab().Read(pawn);
            Assert(view.Stage == def.stages[i].label);
            if (i + 1 < def.stages.Count)
                Assert(view.Progress == "SSC_MasterProgression_NextStage".Translate(def.stages[i + 1].label,
                    (def.stages[i + 1].minSeverity - record.Severity).ToString("0")));
            pawn.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(!record.GetTooltip(pawn, false).Contains(def.stages[i].extraTooltip));
        }
    }
}
