// AI 辅助生成：旅队阶段维持只适配查询与准入，摄入和库存清理由原版旅队执行。
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>复用锁链配置和原版旅队库存／摄入；不创建地图任务、补给队列或新的保存字段。</summary>
    internal static class SSCPnaCaravanMaintenanceUtility
    {
        // 原生摄入会调用药物组件及其他模组；同一角色在本次调用栈中只能进入一次。
        private static readonly HashSet<Pawn> processing = new();

        /// <summary>世界旅队独立准入；离图残留任务不代表忙碌，夜间休息则等待下一轮原生检查。</summary>
        private static bool CanMaintain(SSCPnaMaintenanceContext context, Caravan caravan)
        {
            Pawn pawn = context?.Pawn;
            return Scribe.mode == LoadSaveMode.Inactive && caravan != null && caravan.Spawned && !caravan.Destroyed &&
                caravan.IsPlayerControlled && caravan.needs != null && pawn != null && !pawn.Spawned &&
                caravan.ContainsPawn(pawn) && pawn.GetCaravan() == caravan &&
                SSCPnaMaintenanceUtility.IsCurrent(context) && SSCPnaMaintenanceUtility.Eligible(pawn) &&
                pawn.drugs != null && pawn.needs != null && !pawn.DevelopmentalStage.Baby() &&
                !pawn.Downed && !pawn.InMentalState && !pawn.health.hediffSet.InLabor() &&
                SSCPnaMaintenanceUtility.NeedsDose(pawn, context.Stage) && !caravan.NightResting;
        }

        /// <summary>只接受仍在本旅队实际成员背包内的可摄入原液；验证后重新读取持有者以覆盖查询回调。</summary>
        private static bool TryGetOwner(Thing drug, Caravan caravan, out Pawn owner)
        {
            owner = null;
            if (drug == null || drug.Destroyed || drug.Spawned || drug.stackCount <= 0 ||
                drug.def != SSCPnaMaintenanceUtility.DrugDefinition || !drug.IngestibleNow) return false;
            owner = CaravanInventoryUtility.GetOwnerOf(caravan, drug);
            return owner != null && owner.GetCaravan() == caravan && owner.inventory?.innerContainer.Contains(drug) == true;
        }

        /// <summary>先查自己的背包，再查包括驮畜在内的原生共享库存；跳过同Def但当前不可服用的栈。</summary>
        private static Thing FindDrug(Pawn pawn, Caravan caravan, out Pawn owner)
        {
            owner = null;
            if (SSCPnaMaintenanceUtility.DrugDefinition == null) return null;
            var inventory = pawn.inventory?.innerContainer;
            // 查询可能进入组件或其他补丁；快照避免容器在回调内变化破坏枚举。
            if (inventory != null)
                foreach (Thing drug in new List<Thing>(inventory))
                    if (TryGetOwner(drug, caravan, out owner) && owner == pawn) return drug;

            // AllInventoryItems返回原版共用的临时列表，不能跨验证回调直接持有其枚举器。
            foreach (Thing drug in new List<Thing>(CaravanInventoryUtility.AllInventoryItems(caravan)))
                if (TryGetOwner(drug, caravan, out owner)) return drug;
            owner = null;
            return null;
        }

        /// <summary>每次原版个人服药检查至多追加一份；原政策先运行，其效果参与本次阶段判断。</summary>
        internal static bool TryMaintain(Pawn pawn, Caravan caravan)
        {
            if (pawn == null || !processing.Add(pawn)) return false;
            try
            {
                // 搜索前只捕获一次权限；搜索后仍比较这条原链，不为新主人／新设置重新签发凭据。
                var context = SSCPnaMaintenanceUtility.Capture(pawn);
                if (!CanMaintain(context, caravan)) return false;
                Thing drug = FindDrug(pawn, caravan, out Pawn owner);
                if (drug == null || !TryGetOwner(drug, caravan, out Pawn currentOwner) || currentOwner != owner ||
                    !CanMaintain(context, caravan)) return false;

                // 不手动扣栈、写负荷或刷新缓存；原版IngestDrug调用Thing.Ingested及既有PNA药效，
                // 并在最后一份消耗后清理持有容器、刷新旅队库存。即使仍不足，本轮也不循环追补。
                caravan.needs.IngestDrug(pawn, drug, owner);
                return true;
            }
            finally
            {
                // 无药、撤权或外部摄入抛出异常都必须释放；下一次原生检查可以正常重试。
                processing.Remove(pawn);
            }
        }
    }
}
