// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>只消费原版活动范围内可访问的地面堆；查询与消费使用相同规则，不持久预留。</summary>
    public class RitualStageAction_ConsumeCosmeticMaterials : RitualStageAction
    {
        private static List<Thing> AvailableStacks(TargetInfo target, Pawn host, ThingDef material)
        {
            if (!target.IsValid || host == null || !host.Spawned || host.Map != target.Map || material == null)
                return new List<Thing>();
            return target.Map.listerThings.ThingsOfDef(material).Where(t => t.Spawned && !t.Destroyed && t.Map == target.Map &&
                t.stackCount > 0 && GatheringsUtility.InGatheringArea(t.Position, target.Cell, target.Map) &&
                !t.IsForbidden(host) && host.CanReserveAndReach(t, PathEndMode.Touch, Danger.Deadly)).ToList();
        }

        private static AcceptanceReport Gather(TargetInfo target, Pawn host, out List<Thing> fluid, out List<Thing> herbs)
        {
            fluid = AvailableStacks(target, host, DefDatabase<ThingDef>.GetNamedSilentFail("Purple_Aphrodisiac_Nanofluid"));
            herbs = AvailableStacks(target, host, ThingDefOf.MedicineHerbal);
            if (fluid.Sum(t => t.stackCount) < 20 || herbs.Sum(t => t.stackCount) < 2)
                return "SSC_Cosmetic_MaterialsRequired".Translate(20, 2);
            return true;
        }

        public static AcceptanceReport MaterialReport(TargetInfo target, Pawn host) => Gather(target, host, out _, out _);

        private static void Consume(List<Thing> stacks, int count)
        {
            foreach (Thing stack in stacks)
            {
                int take = Math.Min(count, stack.stackCount);
                stack.SplitOff(take).Destroy(DestroyMode.Vanish);
                count -= take;
                if (count == 0) return;
            }
            throw new InvalidOperationException("Cosmetic material stacks changed during consumption.");
        }

        public override void Apply(LordJob_Ritual ritual)
        {
            var report = Gather(ritual.selectedTarget, ritual.PawnWithRole(SSCCosmeticUtility.HostRole), out var fluid, out var herbs);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, ritual.selectedTarget, MessageTypeDefOf.RejectInput, false);
                // 切换后的回调再立即走原版取消，ended 防重；清理后不再有本次 GotoToil 分配职责。
                ritual.ApplyOutcome(0f, showFinishedMessage: false, showFailedMessage: true, cancelled: true);
                return;
            }
            // 同一回调内确认两项足量后各消费一次；异常由原版转换日志处理，无退款／重试。
            Consume(fluid, 20);
            Consume(herbs, 2);
        }

        public override void ExposeData() { }
    }
}
