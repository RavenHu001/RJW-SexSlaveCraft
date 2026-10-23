// AI 辅助生成
using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>天灯节式取料：准备时由原版 Job 领取，正式阶段前只消费主持者背包中的材料。</summary>
    public class RitualStageAction_ConsumeCosmeticMaterials : RitualStageAction
    {
        // 与 DutyDef 中的两个原版取料节点保持一致；预检、足量判断和消费共用这些数量。
        private const int FluidCount = 20, HerbalCount = 2;
        private static ThingDef Fluid => DefDatabase<ThingDef>.GetNamedSilentFail("Purple_Aphrodisiac_Nanofluid");
        internal static string MissingMaterialsReason => "SSC_Cosmetic_MaterialsRequired".Translate(FluidCount, HerbalCount);

        // 到场门槛和最终消费只认可主持者实际背包，不把尚未领取的地面材料算作已备齐。
        public static bool MaterialsReady(Pawn host) => host?.inventory != null && Fluid != null &&
            host.inventory.Count(Fluid) >= FluidCount && host.inventory.Count(ThingDefOf.MedicineHerbal) >= HerbalCount;

        private static bool CanFetch(Pawn host, ThingDef material, int count)
        {
            if (material == null || host.inventory == null) return false;
            // 已有库存抵扣领取缺额；足量时无需再寻找地面堆。
            int needed = count - host.inventory.Count(material);
            if (needed <= 0) return true;
            // 与原版 JobGiver_TakeCountToInventory 对齐：一个地面堆须能补足缺额，预检不生成或预留 Job。
            return GenClosest.ClosestThingReachable(host.Position, host.Map, ThingRequest.ForDef(material),
                PathEndMode.Touch, TraverseParms.For(host), 9999f,
                t => t.stackCount >= needed && !t.IsForbidden(host) && host.CanReserve(t, 10, needed)) != null;
        }

        public static AcceptanceReport MaterialReport(TargetInfo target, Pawn host)
        {
            // 开窗和提交时只确认选中主持者有能力备齐两种材料，不消费、不提前锁料。
            if (!target.IsValid || host == null || !host.Spawned || host.Map != target.Map ||
                !CanFetch(host, Fluid, FluidCount) || !CanFetch(host, ThingDefOf.MedicineHerbal, HerbalCount))
                return MissingMaterialsReason;
            return true;
        }

        private static void Consume(Pawn host, ThingDef material, int count)
        {
            // 原版 Inventory.RemoveCount 在当前构建只处理首堆；按实际背包堆调用原版 Take，保证混合旧库存也足额消费。
            var inventory = host.inventory.innerContainer;
            // 拆分和销毁会修改容器，先快照匹配堆；逐堆扣到足量即返回，保留多余库存。
            foreach (Thing stack in inventory.Where(t => t.def == material).ToList())
            {
                int take = Math.Min(count, stack.stackCount);
                inventory.Take(stack, take).Destroy(DestroyMode.Vanish);
                count -= take;
                if (count == 0) return;
            }
            // 已通过同时足量检查仍扣不足属于技术异常，由原版调用链报错，不补偿先前消费。
            throw new InvalidOperationException("Cosmetic inventory changed during consumption.");
        }

        public override void Apply(LordJob_Ritual ritual)
        {
            // 挂在瞬时 essential 阶段的 postAction：正式职责已初始化，正式首 tick 尚未执行。
            Pawn host = ritual.PawnWithRole(SSCCosmeticUtility.HostRole);
            if (!MaterialsReady(host))
            {
                Messages.Message(MissingMaterialsReason, ritual.selectedTarget, MessageTypeDefOf.RejectInput, false);
                // 此处直接调用取消入口，不会经过原版 interruptedAction 转换，先放下尚未消费的材料。
                ritual.CurrentStage.interruptedAction?.Apply(ritual);
                // 切换后的回调再立即走原版取消，ended 防重；清理后不再有本次 GotoToil 分配职责。
                ritual.ApplyOutcome(0f, showFinishedMessage: false, showFailedMessage: true, cancelled: true);
                return;
            }
            // 两种材料先同时确认；使用原版库存消费，不远程扣地面或其他人的背包。
            Consume(host, Fluid, FluidCount);
            Consume(host, ThingDefOf.MedicineHerbal, HerbalCount);
        }

        // 动作无实例状态；阶段恢复由原版保存，读档不额外消费或记录事务标记。
        public override void ExposeData() { }
    }
}
