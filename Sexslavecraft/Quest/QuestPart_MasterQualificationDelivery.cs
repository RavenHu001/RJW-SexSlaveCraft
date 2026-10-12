using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只协调投放时机及目标地图，不实现空投；奖励始终由原版 DropPods 持有和生成。</summary>
    public sealed class QuestPart_MasterQualificationDelivery : QuestPartActivable
    {
        public string deliverSignal;
        public string deliveredSignal;
        public Thing module;
        private bool deliveryRequested;
        private int nextAttemptTick;
        private const int RetryInterval = 1200;

        public override void QuestPartTick()
        {
            if (quest == null || quest.State != QuestState.Ongoing || State != QuestPartState.Enabled ||
                deliveryRequested || Find.TickManager.TicksGame < nextAttemptTick)
                return;
            nextAttemptTick = Find.TickManager.TicksGame + RetryInterval;
            QuestPart_DropPods drop = null;
            foreach (QuestPart part in quest.PartsListForReading)
                if (part is QuestPart_DropPods candidate && candidate.inSignal == deliverSignal)
                { drop = candidate; break; }
            if (drop == null || module == null || module.Destroyed) return;

            // 投放前重新取有效基地，原地图丢失则转到另一基地；暂时没有基地时继续等待。
            Map map = SSCMasterEntryUtility.FindDeliveryMap(drop.mapParent?.Map, quest.root);
            if (map == null) return;
            drop.mapParent = map.Parent;
            deliveryRequested = true;
            Find.SignalManager.SendSignal(new Signal(deliverSignal));
            // 原版信号边界未完成且物品仍未投出时可重试；已进入空投/地图的同一实物不再次发出。
            if (!quest.Historical && !module.SpawnedOrAnyParentSpawned && !module.Destroyed)
                deliveryRequested = false;
        }

        protected override void ProcessQuestSignal(Signal signal)
        {
            if (signal.tag == deliveredSignal) Complete();
        }

        public override void Cleanup()
        {
            base.Cleanup();
            // 历史任务不再追踪可被使用/销毁的实物；其未投放清理由原版空投所有者负责。
            module = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref deliverSignal, "deliverSignal");
            Scribe_Values.Look(ref deliveredSignal, "deliveredSignal");
            Scribe_References.Look(ref module, "module");
            Scribe_Values.Look(ref deliveryRequested, "deliveryRequested", false);
            Scribe_Values.Look(ref nextAttemptTick, "nextAttemptTick", 0);
        }
    }
}
