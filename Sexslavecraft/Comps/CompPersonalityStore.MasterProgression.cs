// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public partial class CompPersonalityStore
    {
        // -1 是明确没有成长，0 是拥有基础阶段；版本独立于上一批主人资格。
        public float masterGrowthPoints = SSCMasterProgressionUtility.NoRecord;
        public int masterProgressionSnapshotVersion = SSCMasterProgressionUtility.CurrentVersion;

        /// <summary>凝胶仅保存重建所需的经验与格式版本，不序列化健康实例或绑定。</summary>
        public void ExposeMasterProgressionSnapshot()
        {
            // 新对象默认当前版本，Scribe 缺字段时读版本 0；经验默认 -1，
            // 避免把“未保存成长”与真实的基础阶段零经验混在一起。
            Scribe_Values.Look(ref masterGrowthPoints, "masterGrowthPoints", SSCMasterProgressionUtility.NoRecord);
            Scribe_Values.Look(ref masterProgressionSnapshotVersion, "masterProgressionSnapshotVersion", 0);
            // 身份字段已由主组件读入，等 PostLoadInit 才按独立成长版本解释旧快照。
            if (Scribe.mode == LoadSaveMode.PostLoadInit) NormalizeMasterProgressionSnapshot();
        }

        /// <summary>旧主人凝胶仅一次补零经验；现代主人无记录必须保持为空。</summary>
        public void NormalizeMasterProgressionSnapshot()
        {
            if (masterProgressionSnapshotVersion < SSCMasterProgressionUtility.CurrentVersion)
            {
                // 上一批已有资格版本的凝胶仍可能没有成长字段；仅此版本决定是否补基础阶段。
                // 迁移完成后，Master + -1 是合法空记录，不能再次按身份推导。
                masterGrowthPoints = sscIdentity == PawnIdentity.Master ? 0f : SSCMasterProgressionUtility.NoRecord;
                masterProgressionSnapshotVersion = SSCMasterProgressionUtility.CurrentVersion;
            }
            // 损坏的负值和非有限值统一视为无记录；有效经验只截断到现行上限。
            masterGrowthPoints = masterGrowthPoints < 0f || float.IsNaN(masterGrowthPoints) || float.IsInfinity(masterGrowthPoints)
                ? SSCMasterProgressionUtility.NoRecord : SSCMasterProgressionUtility.NormalizeExperience(masterGrowthPoints);
        }

        /// <summary>旧角色先迁移再采集；重复采集覆盖旧值，包括退出主人后保留的成长。</summary>
        public void CaptureMasterProgression(Pawn pawn)
        {
            // 先让旧主人得到应有的基础记录，再采集；只读取 Hediff，不要求成长此刻生效。
            // 每次覆盖两个标量，复用凝胶组件采集无记录人格时不会残留上一次的经验。
            SSCMasterProgressionMigration.MigratePawn(pawn);
            masterGrowthPoints = SSCMasterProgressionUtility.GetExperience(pawn);
            masterProgressionSnapshotVersion = SSCMasterProgressionUtility.CurrentVersion;
        }

        /// <summary>普通凝胶复制与加工保留源值和缺字段版本，不与克隆初始化混用。</summary>
        public void CopyMasterProgressionSnapshot(CompPersonalityStore other)
        {
            // 同时保留版本和经验，让旧副本在恢复时独立迁移；这里不改来源对象，
            // 也不复制 Hediff 实例、身体部位或主奴引用。
            masterGrowthPoints = other.masterGrowthPoints;
            masterProgressionSnapshotVersion = other.masterProgressionSnapshotVersion;
        }
    }
}
