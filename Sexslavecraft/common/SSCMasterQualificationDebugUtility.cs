using Verse;

namespace SexSlaveCraft
{
    /// <summary>设置调试按钮的放置服务；只生成物品，之后仍须由角色正常使用。</summary>
    public static class SSCMasterQualificationDebugUtility
    {
        /// <summary>主菜单、无当前地图或道具定义缺失时禁用生成入口。</summary>
        public static bool CanSpawn(Map map)
        {
            return Current.Game != null && map != null && SSCDefOf.SSC_MasterQualificationItem != null;
        }

        /// <summary>在选中对象附近或地图中央生成一个；失败清理未放置物品，不授予任何角色资格。</summary>
        public static bool TrySpawn(Map map, Thing selected, out Thing spawned)
        {
            // 点击后再次验证宿主条件；失败输出保持为空，调用者不能报告不存在的物品。
            spawned = null;
            if (!CanSpawn(map)) return false;

            // 只使用当前地图上已生成的选中对象；其他地图、容器对象或未选择时回退地图中央。
            IntVec3 center = selected != null && selected.Spawned && selected.Map == map
                ? selected.Position : map.Center;

            // 固定生成一个，原版 Near 放置负责寻找可用格及合并已有堆叠。
            Thing item = ThingMaker.MakeThing(SSCDefOf.SSC_MasterQualificationItem);
            item.stackCount = 1;
            if (!GenPlace.TryPlaceThing(item, center, map, ThingPlaceMode.Near, out spawned))
            {
                // 放置失败清理未进入地图的物品，不保留悬空实例，也不产生成功提示目标。
                if (!item.Destroyed) item.Destroy();
                spawned = null;
                return false;
            }
            // spawned 是原版返回的实际放置对象；合堆时可能不是刚创建的 item。
            return true;
        }
    }
}
