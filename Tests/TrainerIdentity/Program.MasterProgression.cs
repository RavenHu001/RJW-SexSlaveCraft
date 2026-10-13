using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>执行真实成长、身份及迁移代码，原版健康计算只提供已核验的调用边界。</summary>
    private static void RunMasterProgressionTests(string repo)
    {
        XElement definition = XDocument.Load(Path.Combine(repo, "Defs/HediffDefs/SSC_MasterProgression.xml")).Root.Element("HediffDef");
        SSCDefOf.SSC_Hediff_MasterProgression.stages = definition.Element("stages").Elements("li")
            .Select(s => new HediffStage { label = s.Element("label").Value, minSeverity = (float)s.Element("minSeverity"),
                capMods = s.Element("capMods").Elements().Select(_ => new object()).ToList() }).ToList();
        Run("真实旧档迁移、来源恢复和退出后返回不触发升阶消息", () =>
        {
            Messages.Count = 0;
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterProgressionVersion = 0;
            SSCMasterProgressionMigration.MigratePawn(pawn);
            Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, 100));
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            SSCMasterProgressionMigration.MigratePawn(pawn);
            Assert(Messages.Count == 0 && SSCMasterProgressionUtility.GetExperience(pawn) == 100);
        });
        Run("首次选择主人建立零经验，零严重度阶段不移除且不写永久资格", () =>
        {
            Pawn pawn = Pawn(); Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            var record = SSCMasterProgressionUtility.GetRecord(pawn);
            Assert(record != null && record.Severity == 0 && !record.ShouldRemove && record.IsActive && !pawn.Training.masterQualificationUnlocked);
        });
        Run("退出身份保留成长并刷新缓存，返回主人恢复原阶段", () =>
        {
            Pawn pawn = Pawn(); Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            SSCMasterProgressionUtility.TryAddExperience(pawn, 45);
            var record = SSCMasterProgressionUtility.GetRecord(pawn); int before = pawn.health.ChangeNotifications;
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
            Assert(ReferenceEquals(record, SSCMasterProgressionUtility.GetRecord(pawn)) && record.Severity == 45 && !record.IsActive &&
                record.CurStage.capMods.Count == 0 && pawn.health.ChangeNotifications > before);
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master) && record.IsActive && record.CurStageIndex == 2);
        });
        Run("跨越四阶段阈值且经验截断到最高阶段，拒绝无效增量", () =>
        {
            Pawn pawn = Pawn(); Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            var record = SSCMasterProgressionUtility.GetRecord(pawn);
            foreach (var step in new[] { (xp: 11, stage: 0), (xp: 12, stage: 1), (xp: 39, stage: 1), (xp: 40, stage: 2), (xp: 99, stage: 2) })
            {
                record.Severity = step.xp; Assert(record.CurStageIndex == step.stage);
            }
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 10) == 1 && record.Severity == 100 && record.CurStageIndex == 3);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 10) == 0 && SSCMasterProgressionUtility.TryAddExperience(pawn, -3) == 0);
        });
        Run("旧主人补基础阶段仅一次，现代无记录与重复主人选择不补建", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterProgressionVersion = 0;
            SSCMasterProgressionMigration.MigratePawn(pawn); Assert(SSCMasterProgressionUtility.GetExperience(pawn) == 0);
            SSCMasterProgressionUtility.ClearPersonalityRecord(pawn); SSCMasterProgressionMigration.MigratePawn(pawn);
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master) && SSCMasterProgressionUtility.GetExperience(pawn) == -1);
            Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 10) == 0);
        });
        Run("旧主人退出前先迁移，保存零经验供再次进入使用", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterProgressionVersion = 0;
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
            Assert(SSCMasterProgressionUtility.GetExperience(pawn) == 0 && !SSCMasterProgressionUtility.GetRecord(pawn).IsActive);
        });
        Run("初始化组件处理完整角色快照，重复初始化不补现代空记录", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterProgressionVersion = 0;
            RimWorld.PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Add(pawn);
            try
            {
                var component = new SSCMasterProgressionGameComponent(new Game()); component.FinalizeInit();
                Assert(SSCMasterProgressionUtility.GetExperience(pawn) == 0);
                SSCMasterProgressionUtility.ClearPersonalityRecord(pawn); component.FinalizeInit();
                Assert(SSCMasterProgressionUtility.GetExperience(pawn) == -1);
            }
            finally { RimWorld.PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Remove(pawn); }
        });
        Run("旧空壳及克隆不补成长，初始化版本阻止后续重复推断", () =>
        {
            foreach (bool clone in new[] { false, true })
            {
                Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterProgressionVersion = 0; pawn.IsClone = clone;
                if (!clone) pawn.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
                SSCMasterProgressionMigration.MigratePawn(pawn);
                Assert(pawn.Training.masterProgressionVersion == 1 && SSCMasterProgressionUtility.GetExperience(pawn) == -1);
                Assert(SSCMasterProgressionUtility.TryAddExperience(pawn, 10) == 0);
            }
        });
        Run("普通进入主人也不为残留主人身份的空壳建立成长", () =>
        {
            Pawn pawn = Pawn(); pawn.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            Assert(SSCMasterProgressionUtility.GetExperience(pawn) == -1);
        });
        Run("人格恢复主人身份不触发普通首次建立，精确替换高低与无记录", () =>
        {
            Pawn pawn = Pawn(); Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(pawn, PawnIdentity.Master, false));
            Assert(SSCMasterProgressionUtility.GetExperience(pawn) == -1);
            Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, 100));
            Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(pawn, PawnIdentity.Master, false));
            Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, 12) && SSCMasterProgressionUtility.GetExperience(pawn) == 12);
            Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, -1) && SSCMasterProgressionUtility.GetExperience(pawn) == -1);
        });
        Run("绑定身份锁仍生效，拒绝切换不改变主人经验", () =>
        {
            var f = Setup(); Assert(SSCMasterProgressionUtility.EnsureInitialRecord(f.master));
            SSCMasterProgressionUtility.TryAddExperience(f.master, 40); Assert(SSCBondUtility.Bind(f.master, f.slave));
            Assert(!SSCIdentityUtility.TrySetIdentity(f.master, PawnIdentity.Unset) && SSCMasterProgressionUtility.GetExperience(f.master) == 40);
        });
        Run("异常替换请求在清理前拒绝并保留原成长", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); Assert(SSCMasterProgressionUtility.RestoreRecord(pawn, 40));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -9f, -0.5f })
                Assert(!SSCMasterProgressionUtility.RestoreRecord(pawn, invalid) && SSCMasterProgressionUtility.GetExperience(pawn) == 40);
        });
        Run("新克隆清成长及主人身份，但以后能独立成为主人", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.IsClone = true; Assert(SSCMasterProgressionUtility.EnsureInitialRecord(pawn));
            SSCMasterProgressionUtility.TryAddExperience(pawn, 100); SSCMasterProgressionUtility.InitializeNewClone(pawn);
            Assert(pawn.Training.pawnIdentity == PawnIdentity.Unset && pawn.Training.masterProgressionVersion == 1 && SSCMasterProgressionUtility.GetExperience(pawn) == -1);
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master) && SSCMasterProgressionUtility.GetExperience(pawn) == 0);
            Assert(SSCMasterProgressionUtility.IsProgressionDef(SSCDefOf.SSC_Hediff_MasterProgression) &&
                SSCMasterProgressionUtility.IsProgressionDef(new HediffDef { defName = SSCMasterProgressionUtility.DefName }));
        });
        Run("零经验及角色迁移版本保存读回，缺字段恢复旧版本", () =>
        {
            Pawn pawn = Pawn(); Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            var record = SSCMasterProgressionUtility.GetRecord(pawn); Scribe_Values.Data.Clear(); Scribe_Values.Loading = false;
            pawn.Training.ExposeMasterProgression(); record.ExposeData();
            Scribe_Values.Loading = true;
            try
            {
                var loaded = (Hediff_MasterProgression)HediffMaker.MakeHediff(SSCDefOf.SSC_Hediff_MasterProgression, pawn);
                loaded.ExposeData(); pawn.Training.ExposeMasterProgression();
                Assert(loaded.Severity == 0 && !loaded.ShouldRemove && pawn.Training.masterProgressionVersion == 1);
                Scribe_Values.Data.Clear(); pawn.Training.ExposeMasterProgression(); Assert(pawn.Training.masterProgressionVersion == 0);
            }
            finally { Scribe_Values.Loading = false; Scribe_Values.Data.Clear(); }
        });
        Run("阶段 XML 符合已确定的五项加成，伤口恢复只使用原版属性因子", () =>
        {
            var stages = definition.Element("stages").Elements().ToArray(); Assert(stages.Length == 4);
            Assert(stages.Select(s => (float)s.Element("minSeverity")).SequenceEqual(new[] { 0f, 12f, 40f, 100f }));
            Assert((float)definition.Element("initialSeverity") == 0 && (float)definition.Element("maxSeverity") == 100);
            // 核对维护者选定的四阶段总值，尤其防止旧操控／自然愈合与新方案一起叠加。
            float[] consciousness = { .10f, .20f, .30f, .40f };
            float[] senses = { .05f, .10f, .20f, .30f };
            float[] social = { 1.05f, 1.10f, 1.15f, 1.20f };
            float[] healing = { 1.10f, 1.20f, 1.35f, 1.50f };
            for (int i = 0; i < stages.Length; i++)
            {
                var mods = stages[i].Element("capMods").Elements().ToDictionary(e => e.Element("capacity").Value, e => (float)e.Element("offset"));
                Assert(mods.Count == 3 && mods["Consciousness"] == consciousness[i] && mods["Sight"] == senses[i] && mods["Hearing"] == senses[i]);
                var factors = stages[i].Element("statFactors").Elements().ToDictionary(e => e.Name.LocalName, e => (float)e);
                Assert(factors.Count == 2 && factors["SocialImpact"] == social[i] && factors["InjuryHealingFactor"] == healing[i]);
                Assert(stages[i].Element("naturalHealingFactor") == null);
            }
            // 新方案保留最高意识下限；不再要求成长 Hediff 自身直接提供操控偏移。
            var slaveFinal = XDocument.Load(Path.Combine(repo, "Defs/HediffDefs/HediffOfSexSlave.xml")).Root.Elements("HediffDef")
                .First(d => d.Element("defName").Value == "Hediff_ChainOfSexSlave").Element("stages").Elements().Last();
            var slaveConsciousness = slaveFinal.Element("capMods").Elements().Single(e => e.Element("capacity").Value == "Consciousness");
            Assert(consciousness.Last() >= (float)slaveConsciousness.Element("offset"));
        });
        Run("四语成长文案键与占位符完整且镜像一致", () =>
        {
            foreach (string relative in new[] { "Keyed/SSC_MasterProgression.xml", "DefInjected/HediffDef/SSC_MasterProgression.xml" })
            {
                var baseline = XDocument.Load(Path.Combine(repo, "Languages/ChineseSimplified", relative)).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
                {
                    string path = Path.Combine(repo, "Languages", language, relative);
                    var entries = XDocument.Load(path).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                    Assert(entries.Keys.Order().SequenceEqual(baseline.Keys.Order()));
                    foreach (var entry in entries)
                    {
                        Assert(!string.IsNullOrWhiteSpace(entry.Value));
                        Assert(Regex.Matches(entry.Value, "\\{\\d+\\}").Select(m => m.Value).Order().SequenceEqual(
                            Regex.Matches(baseline[entry.Key], "\\{\\d+\\}").Select(m => m.Value).Order()));
                    }
                    Assert(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(repo, "Sexslavecraft/Languages", language, relative))));
                }
            }
        });
    }
}
