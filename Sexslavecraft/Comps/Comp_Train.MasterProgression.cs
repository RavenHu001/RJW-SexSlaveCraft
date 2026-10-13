// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 经验只保存在主人 Hediff；此版本仅区分旧档缺字段与现代明确无记录。
        public int masterProgressionVersion = SSCMasterProgressionUtility.CurrentVersion;

        /// <summary>新角色默认已初始化，旧存档缺字段时读入版本 0，稍后一次性迁移。</summary>
        public void ExposeMasterProgression()
        {
            // 当前版本也必须写入存档；提取或清空后的 Master + 无 Hediff，
            // 依靠这个版本与缺字段旧主人区分，而不是再存一份成长存在标志。
            Scribe_Values.Look(ref masterProgressionVersion, "masterProgressionVersion", 0);
        }
    }
}
