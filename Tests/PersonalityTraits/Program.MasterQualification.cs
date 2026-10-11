using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static CompSexSlaveTraining MasterTraining(Pawn pawn, PawnIdentity identity, bool unlocked)
    {
        var comp = pawn.TryGetComp<CompSexSlaveTraining>();
        if (comp == null)
        {
            comp = new CompSexSlaveTraining { parent = pawn };
            pawn.Comps[typeof(CompSexSlaveTraining)] = comp;
        }
        comp.pawnIdentity = identity;
        comp.ReplaceMasterQualification(unlocked);
        return comp;
    }

    private static void RunMasterQualificationCases()
    {
        Run("主人资格经提取、存读档、加工复制和跨体植入完整转移", () =>
        {
            Pawn source = Body("Qualified"); var sourceComp = MasterTraining(source, PawnIdentity.Unset, true);
            CompPersonalityStore gel = Excrete(source);
            Assert(gel.masterQualificationUnlocked && !sourceComp.masterQualificationUnlocked,
                "Extraction must move qualification into the gel and clear the original body.");
            Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
            var loaded = new CompPersonalityStore { parent = new Thing() };
            Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            var processed = new CompPersonalityStore { parent = new Thing() }; processed.CopyFrom(loaded);
            Pawn host = Body("Host"); var hostComp = MasterTraining(host, PawnIdentity.Slave, false);
            Assert(ExcretionUtility.InheritEverything(host, processed), "Insertion must succeed.");
            Assert(hostComp.masterQualificationUnlocked && hostComp.pawnIdentity == PawnIdentity.Unset &&
                hostComp.masterQualificationVersion == 1 && processed.parent.Destroyed,
                "Qualification must survive the entire pipeline independently of the current identity.");
            SSCMasterQualificationMigration.MigratePawn(source);
            Assert(!sourceComp.masterQualificationUnlocked, "Reload migration must not grant the emptied body qualification again.");
        });
        Run("无资格人格植入已解锁宿主时清除宿主资格，而不按当前 Master 身份补发", () =>
        {
            Pawn source = Body("ManualMaster"); MasterTraining(source, PawnIdentity.Master, false);
            var gel = Excrete(source);
            Pawn host = Body("QualifiedHost"); var comp = MasterTraining(host, PawnIdentity.Master, true);
            Assert(ExcretionUtility.InheritEverything(host, gel), "Manual Master identity must remain restorable.");
            SSCMasterQualificationMigration.MigratePawn(host);
            Assert(comp.pawnIdentity == PawnIdentity.Master && !comp.masterQualificationUnlocked && comp.masterQualificationVersion == 1,
                "A new-format Master + false snapshot must override the host and remain false after migration.");
        });
        Run("新版明确未解锁的主人凝胶存读档后仍未解锁", () =>
        {
            var gel = Gel(); gel.sscIdentity = PawnIdentity.Master;
            Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
            var loaded = new CompPersonalityStore(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert(loaded.sscIdentity == PawnIdentity.Master && !loaded.masterQualificationUnlocked && loaded.masterQualificationVersion == 1,
                "The version marker must distinguish explicitly false from a missing legacy qualification.");
        });
        Run("旧凝胶缺资格字段时仅补主人记录，后续读档不重算", () =>
        {
            foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Slave, PawnIdentity.Master })
            {
                Scribe.Values.Clear(); Scribe.Values["sscIdentity"] = identity;
                var gel = new CompPersonalityStore(); Scribe.mode = LoadSaveMode.LoadingVars; gel.PostExposeData();
                Assert(gel.masterQualificationVersion == 0 && !gel.masterQualificationUnlocked, "Missing legacy fields must read version zero.");
                Scribe.mode = LoadSaveMode.PostLoadInit; gel.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
                Assert(gel.masterQualificationUnlocked == (identity == PawnIdentity.Master) && gel.masterQualificationVersion == 1,
                    "Only the legacy Master identity is evidence of qualification.");
                gel.masterQualificationUnlocked = false; gel.sscIdentity = PawnIdentity.Master; gel.NormalizeMasterQualification();
                Assert(!gel.masterQualificationUnlocked, "Normalized snapshots must not receive repeated grants.");
            }
        });
        Run("旧主人凝胶加工复制保留版本，植入时一次性迁移且不改源副本", () =>
        {
            var legacy = Gel(); legacy.sscIdentity = PawnIdentity.Master; legacy.masterQualificationVersion = 0;
            var copy = Gel(); copy.CopyFrom(legacy);
            Assert(copy.masterQualificationVersion == 0 && !copy.masterQualificationUnlocked,
                "Copy must preserve the legacy format until normalization.");
            Pawn host = Body("Host"); var comp = MasterTraining(host, PawnIdentity.Unset, false);
            Assert(ExcretionUtility.InheritEverything(host, copy), "Legacy copy insertion must succeed.");
            Assert(comp.masterQualificationUnlocked && comp.pawnIdentity == PawnIdentity.Master && copy.masterQualificationVersion == 1,
                "Insertion must normalize the copied legacy Master qualification.");
            Assert(legacy.masterQualificationVersion == 0 && !legacy.masterQualificationUnlocked,
                "Normalization of a processed copy must not mutate its source.");
        });
        Run("旧主人在采集前先迁移资格，成功生成凝胶后身体资格清空", () =>
        {
            Pawn source = Body("OldMaster"); var comp = MasterTraining(source, PawnIdentity.Master, false);
            comp.masterQualificationVersion = 0;
            var gel = Excrete(source);
            Assert(gel.masterQualificationUnlocked && gel.masterQualificationVersion == 1 &&
                !comp.masterQualificationUnlocked && comp.masterQualificationVersion == 1,
                "Capture must retain the old Master's qualification without leaving it on the hollow body.");
            SSCMasterQualificationMigration.MigratePawn(source);
            Assert(!comp.masterQualificationUnlocked, "The original Master identity cannot trigger another grant.");
        });
        Run("复用凝胶组件采集时覆盖旧资格，缺 SSC 组件也不残留上次数据", () =>
        {
            Pawn source = Body("First"); MasterTraining(source, PawnIdentity.Master, true);
            var gel = Gel(); gel.StorePawnData(source);
            Assert(gel.masterQualificationUnlocked, "First snapshot must retain qualification.");
            gel.StorePawnData(Body("NoComponent"));
            Assert(!gel.masterQualificationUnlocked && gel.masterQualificationVersion == 1,
                "A reused container must clear stale qualification even when the new source has no SSC component.");
        });
        Run("CopyFrom 独立保存资格及版本，新格式 false 不会受源后续修改影响", () =>
        {
            var source = Gel(); source.sscIdentity = PawnIdentity.Master;
            var copy = Gel(); copy.masterQualificationUnlocked = true; copy.CopyFrom(source);
            source.masterQualificationUnlocked = true; source.masterQualificationVersion = 0;
            copy.NormalizeMasterQualification();
            Assert(!copy.masterQualificationUnlocked && copy.masterQualificationVersion == 1,
                "Copy must replace old container values and retain its own new-format false record.");
        });
        Run("原身体和宿主清理写当前版本，保留当前身份也不会在迁移中补资格", () =>
        {
            Pawn host = Body("Host"); var comp = MasterTraining(host, PawnIdentity.Master, true);
            SSCMasterQualificationUtility.ClearPersonalityQualification(host);
            SSCMasterQualificationMigration.MigratePawn(host);
            Assert(!comp.masterQualificationUnlocked && comp.masterQualificationVersion == 1 && comp.pawnIdentity == PawnIdentity.Master,
                "Qualification clearing must not depend on resetting the current identity.");
        });
        Run("无法保存个人资格的宿主在植入前拒绝，保留身体和凝胶", () =>
        {
            Pawn host = Body("Unsupported"); host.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            var gel = Gel(); gel.masterQualificationUnlocked = true;
            Assert(!ExcretionUtility.InheritEverything(host, gel), "Qualified insertion requires an SSC component.");
            Assert(host.LabelShort == "Unsupported" && host.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) && !gel.parent.Destroyed,
                "Rejected insertion must happen before clearing the host or consuming the gel.");
            Assert(Log.Errors.Count == 1, "Missing component must report the failed preservation."); Log.Errors.Clear();
        });
        Run("损坏身份快照在清理宿主资格之前拒绝", () =>
        {
            Pawn host = Body("Host"); var comp = MasterTraining(host, PawnIdentity.Master, true);
            var gel = Gel(); gel.sscIdentity = (PawnIdentity)99;
            Assert(!ExcretionUtility.InheritEverything(host, gel), "Invalid saved identity must be rejected.");
            Assert(comp.masterQualificationUnlocked && comp.pawnIdentity == PawnIdentity.Master && host.LabelShort == "Host" && !gel.parent.Destroyed,
                "Invalid identity must not partially replace the personality or consume its gel.");
            Assert(Log.Errors.Count == 1, "Invalid saved identity must report the rejected insertion."); Log.Errors.Clear();
        });
    }
}
