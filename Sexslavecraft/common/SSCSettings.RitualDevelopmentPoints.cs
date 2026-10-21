// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public partial class SSCSettings
    {
        // 全局偏好默认关闭；缺少新键的旧配置同样关闭，不迁移或追补任何历史仪式收益。
        public bool enableRitualDevelopmentPoints = false;

        /// <summary>独立读写仪式演化点偏好，供主设置入口调用；不写入角色或仪式存档。</summary>
        internal void ExposeRitualDevelopmentPointsSettings()
        {
            // 显式使用 false 作为加载缺省值，保证升级不会自动开启新的文化收益来源。
            Scribe_Values.Look(ref enableRitualDevelopmentPoints, "enableRitualDevelopmentPoints", false);
        }
    }
}
