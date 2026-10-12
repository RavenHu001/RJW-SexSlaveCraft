using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只协调投放时机及目标地图，不实现空投；奖励始终由原版 DropPods 持有和生成。</summary>
    public sealed class QuestPart_MasterQualificationDelivery : QuestPartActivable
    {
        // 任务根写入独立信号及同一个奖励引用；module 的 Deep 保存责任属于原版空投部分。
        public string deliverSignal;
        public string deliveredSignal;
        public Thing module;
        // 发出状态与重试时刻随任务保存，投放等待读档后不能重新发出已经派送的实物。
        private bool deliveryRequested;
        private int nextAttemptTick;
        private const int RetryInterval = 1200;

        /// <summary>仅在任务进行中且已接受时尝试派送，未投放的同一模块按间隔重试。</summary>
        public override void QuestPartTick()
        {
            // 原版通常只 Tick 有效启用部分，直接调用旧部分时也必须遵守任务和派送状态。
            if (quest == null || quest.State != QuestState.Ongoing || State != QuestPartState.Enabled ||
                deliveryRequested || Find.TickManager.TicksGame < nextAttemptTick)
                return;
            nextAttemptTick = Find.TickManager.TicksGame + RetryInterval;
            // 通过任务内投放信号定位对应原版部分，不额外序列化一个 QuestPart 对象/引用。
            QuestPart_DropPods drop = null;
            foreach (QuestPart part in quest.PartsListForReading)
                if (part is QuestPart_DropPods candidate && candidate.inSignal == deliverSignal)
                { drop = candidate; break; }
            // 损坏或已清理奖励不重新制造替代品；任务持有的实物始终保持唯一。
            if (drop == null || module == null || module.Destroyed) return;

            // 投放前重新取有效基地，原地图丢失则转到另一基地；暂时没有基地时继续等待。
            Map map = SSCMasterEntryUtility.FindDeliveryMap(drop.mapParent?.Map, quest.root);
            if (map == null) return;
            // 在发送信号前更新目标并锁定派送；原版信号处理可在本调用内完成任务及清理部分。
            drop.mapParent = map.Parent;
            deliveryRequested = true;
            Find.SignalManager.SendSignal(new Signal(deliverSignal));
            // 原版信号边界未完成且物品仍未投出时可重试；已进入空投/地图的同一实物不再次发出。
            if (!quest.Historical && !module.SpawnedOrAnyParentSpawned && !module.Destroyed)
                deliveryRequested = false;
        }

        /// <summary>只响应对应原版空投成功信号，关闭本协调部分；任务结束由单独的原版部分负责。</summary>
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
            // 基类保存启用状态及接受信号；额外状态采用稳定键，实物仅保存跨引用。
            base.ExposeData();
            Scribe_Values.Look(ref deliverSignal, "deliverSignal");
            Scribe_Values.Look(ref deliveredSignal, "deliveredSignal");
            Scribe_References.Look(ref module, "module");
            Scribe_Values.Look(ref deliveryRequested, "deliveryRequested", false);
            Scribe_Values.Look(ref nextAttemptTick, "nextAttemptTick", 0);
        }
    }
}
