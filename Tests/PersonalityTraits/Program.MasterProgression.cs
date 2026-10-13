using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>验证真实抽取、凝胶读写、CopyFrom 和完整植入，身份核心另由 TrainerIdentity 覆盖。</summary>
    private static void RunMasterProgressionCases()
    {
        Run("主人成长经抽取存读档复制和跨体植入完整转移，清除空壳加成", () =>
        {
            Pawn source = Body("Source"); MasterTraining(source, PawnIdentity.Master, false);
            Assert(SSCMasterProgressionUtility.EnsureInitialRecord(source), "Record must be created.");
            SSCMasterProgressionUtility.TryAddExperience(source, 45);
            var gel = Excrete(source);
            Assert(gel.masterGrowthPoints == 45 && SSCMasterProgressionUtility.GetExperience(source) == -1 &&
                source.TryGetComp<CompSexSlaveTraining>().masterProgressionVersion == 1, "Extraction must capture before clearing source.");
            Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
            var loaded = new CompPersonalityStore { parent = new Thing() };
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            var copy = Gel(); copy.CopyFrom(loaded);
            Pawn host = Body("Host"); MasterTraining(host, PawnIdentity.Master, false);
            Assert(SSCMasterProgressionUtility.RestoreRecord(host, 100), "Host old growth must exist.");
            Assert(ExcretionUtility.InheritEverything(host, copy), "Insertion must succeed.");
            Assert(SSCMasterProgressionUtility.GetExperience(host) == 45 && SSCMasterProgressionUtility.GetRecord(host).IsActive && copy.parent.Destroyed,
                "Source value must replace the host exactly.");
            SSCMasterProgressionMigration.MigratePawn(source);
            Assert(SSCMasterProgressionUtility.GetExperience(source) == -1, "The hollow must not recreate transferred growth.");
        });
        Run("基础零经验快照与无记录在存档和植入后仍不同", () =>
        {
            foreach (float experience in new[] { -1f, 0f })
            {
                var gel = Gel(); gel.sscIdentity = PawnIdentity.Master; gel.masterGrowthPoints = experience;
                Scribe.Values.Clear(); Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
                var loaded = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
                Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
                Pawn host = Body("Host"); MasterTraining(host, PawnIdentity.Master, false);
                Assert(ExcretionUtility.InheritEverything(host, loaded), "Zero and empty snapshots must be accepted.");
                Assert(SSCMasterProgressionUtility.GetExperience(host) == experience, "Presence must remain distinct from zero.");
                SSCMasterProgressionMigration.MigratePawn(host);
                Assert(SSCMasterProgressionUtility.GetExperience(host) == experience, "Modern absence must survive subsequent migration.");
            }
        });
        Run("明确无成长的主人凝胶清空高阶段宿主及通用标签中的旧主人记录", () =>
        {
            Pawn host = Body("Host"); MasterTraining(host, PawnIdentity.Master, false);
            Assert(SSCMasterProgressionUtility.RestoreRecord(host, 100), "Old host record must exist.");
            var gel = Gel(); gel.sscIdentity = PawnIdentity.Master;
            gel.hediffTags[SSCDefOf.SSC_Hediff_MasterProgression] = 100;
            Assert(ExcretionUtility.InheritEverything(host, gel), "Insertion must succeed.");
            Assert(SSCMasterProgressionUtility.GetExperience(host) == -1, "Generic tag must not reintroduce removed growth.");
        });
        Run("低经验专用快照不会被通用高经验标签覆盖", () =>
        {
            Pawn host = Body("Host"); MasterTraining(host, PawnIdentity.Master, false);
            var gel = Gel(); gel.sscIdentity = PawnIdentity.Master; gel.masterGrowthPoints = 12;
            gel.hediffTags[SSCDefOf.SSC_Hediff_MasterProgression] = 100;
            Assert(ExcretionUtility.InheritEverything(host, gel) && SSCMasterProgressionUtility.GetExperience(host) == 12,
                "Dedicated snapshot must be the only growth authority.");
        });
        Run("退出主人后的成长仍随人格迁移且恢复后暂停加成", () =>
        {
            Pawn source = Body("Source"); var comp = MasterTraining(source, PawnIdentity.Master, false);
            Assert(SSCMasterProgressionUtility.RestoreRecord(source, 40), "Source record must exist.");
            comp.pawnIdentity = PawnIdentity.Unset;
            var gel = Excrete(source); Pawn host = Body("Host"); MasterTraining(host, PawnIdentity.Master, false);
            Assert(ExcretionUtility.InheritEverything(host, gel), "Inactive source insertion must succeed.");
            Assert(SSCMasterProgressionUtility.GetExperience(host) == 40 && !SSCMasterProgressionUtility.GetRecord(host).IsActive,
                "Growth follows personality independently of its active role.");
        });
        Run("旧主人凝胶独立迁移成长，上一批已有资格版本不会阻止补基础阶段", () =>
        {
            var gel = Gel(); gel.sscIdentity = PawnIdentity.Master; gel.masterQualificationVersion = 1;
            Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
            Scribe.Values.Remove("masterGrowthPoints"); Scribe.Values.Remove("masterProgressionSnapshotVersion");
            var loaded = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
            Assert(loaded.masterProgressionSnapshotVersion == 0, "Missing growth schema must load independently.");
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert(loaded.masterGrowthPoints == 0 && loaded.masterProgressionSnapshotVersion == 1, "Old Master gel gets only baseline.");
            loaded.masterGrowthPoints = -1; loaded.NormalizeMasterProgressionSnapshot();
            Assert(loaded.masterGrowthPoints == -1, "Modern cleared snapshot must not be inferred again.");
        });
        Run("旧非主人凝胶保持无成长，复制旧快照保留版本并独立迁移", () =>
        {
            foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Slave, PawnIdentity.Master })
            {
                var legacy = Gel(); legacy.sscIdentity = identity; legacy.masterProgressionSnapshotVersion = 0;
                var copy = Gel(); copy.CopyFrom(legacy); copy.NormalizeMasterProgressionSnapshot();
                Assert(copy.masterGrowthPoints == (identity == PawnIdentity.Master ? 0 : -1) && legacy.masterProgressionSnapshotVersion == 0,
                    "Copy must preserve legacy metadata without mutating its source.");
            }
        });
        Run("旧主人采集前补零经验，成功产物清源后不重复补建", () =>
        {
            Pawn source = Body("OldMaster"); var comp = MasterTraining(source, PawnIdentity.Master, false); comp.masterProgressionVersion = 0;
            var gel = Excrete(source);
            Assert(gel.masterGrowthPoints == 0 && SSCMasterProgressionUtility.GetExperience(source) == -1 && comp.masterProgressionVersion == 1,
                "Old Master capture must precede source clearing.");
        });
        Run("复用凝胶采集覆盖旧成长，CopyFrom 与来源后续改动独立", () =>
        {
            Pawn source = Body("Source"); MasterTraining(source, PawnIdentity.Master, false);
            Assert(SSCMasterProgressionUtility.RestoreRecord(source, 40), "Source record must exist.");
            var gel = Gel(); gel.StorePawnData(source); var copy = Gel(); copy.CopyFrom(gel);
            gel.StorePawnData(Body("NoRecord"));
            Assert(gel.masterGrowthPoints == -1 && copy.masterGrowthPoints == 40, "Recapture must clear stale value and retain copied snapshot.");
        });
        Run("无资格但有成长的人格在不支持宿主中植入前拒绝并保留凝胶", () =>
        {
            Pawn host = Body("Unsupported"); host.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            var gel = Gel(); gel.sscIdentity = PawnIdentity.Master; gel.masterGrowthPoints = 40; gel.masterQualificationUnlocked = false;
            Assert(!ExcretionUtility.InheritEverything(host, gel), "Missing training component must reject growth independently of qualification.");
            Assert(host.LabelShort == "Unsupported" && host.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) && !gel.parent.Destroyed,
                "Preflight must leave the body and gel intact.");
            Assert(Log.Errors.Count == 1, "Failure must be reported."); Log.Errors.Clear();
        });
        Run("现代损坏经验规范化，无穷及 NaN 不建立成长，超上限截断", () =>
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -9f, 1000f })
            {
                var gel = Gel(); gel.masterGrowthPoints = value; gel.NormalizeMasterProgressionSnapshot();
                Assert(gel.masterGrowthPoints == (value == 1000f ? 100f : -1f), "Invalid snapshots must not enter health stage computation.");
            }
        });
    }
}
