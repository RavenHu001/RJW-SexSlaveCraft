using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>事件仅标记通知来源，不增加任何许可；枚举值入存档后保持稳定。</summary>
    internal enum SSCRestrictionEvent { None, TradeConsensual, TradeForced, Dog }

    /// <summary>负责候选 Job 到实际运行驱动的事件交接，以及实际开始之后的一次性通知。</summary>
    internal sealed class SSCRestrictionJobEvents
    {
        private sealed class PendingEvent
        {
            public int JobId;
            public Pawn Actor, Target;
            public SSCRestrictionEvent Source;
            public readonly List<int> Waits = new List<int>();
        }

        // GetCachedDriver 只用于预检；StartJob 会另建运行驱动，因此待领取信息必须属于 Job。
        // 弱表避免未调度的候选长期存活，编号再防止对象池复用 Job 后带入旧事件。
        private static readonly ConditionalWeakTable<Job, PendingEvent> PendingEvents = new ConditionalWeakTable<Job, PendingEvent>();
        private SSCRestrictionEvent source;
        private bool notified;
        /// <summary>区分自动事件与普通命令，避免自动事件被拒绝时伪装成玩家命令刷出提示。</summary>
        public bool IsEvent => source != SSCRestrictionEvent.None;

        /// <summary>预检通过后登记候选事件；此时尚未打断任何工作，也不显示已发生提示。</summary>
        public static void Prepare(Pawn actor, Job job, SSCRestrictionEvent eventSource)
        {
            PendingEvents.Remove(job);
            PendingEvents.Add(job, new PendingEvent { JobId = job.loadID, Actor = actor, Source = eventSource });
        }

        /// <summary>登记事件本次创建的等待；只接收仍属于同一 Job 编号和发起者的登记。</summary>
        public static void RegisterWait(Pawn actor, Job job, Pawn target, Job wait)
        {
            if (job == null || wait == null || !PendingEvents.TryGetValue(job, out PendingEvent pending) ||
                pending.JobId != job.loadID || pending.Actor != actor) return;
            pending.Target = target;
            if (!pending.Waits.Contains(wait.loadID)) pending.Waits.Add(wait.loadID);
        }

        /// <summary>仅实际安装的运行驱动领取一次；缓存驱动和其他发起者不能提前取走事件及等待归属。</summary>
        public void ClaimPending(JobDriver_Sex driver, SSCRestrictionJobPreparation preparation)
        {
            if (driver.job == null || !PendingEvents.TryGetValue(driver.job, out PendingEvent pending)) return;
            if (pending.JobId != driver.job.loadID) { PendingEvents.Remove(driver.job); return; }
            if (pending.Actor != driver.pawn || driver.pawn?.jobs?.curDriver != driver) return;
            source = pending.Source;
            foreach (int id in pending.Waits) preparation.Register(pending.Target, id);
            PendingEvents.Remove(driver.job);
        }

        /// <summary>调度未被接收时删除待领取信息，防止同一任务再次调度时沿用已撤销的事件。</summary>
        public static void CancelPending(Job job)
        {
            if (job != null) PendingEvents.Remove(job);
        }

        /// <summary>沿用既有事件及通知存档键，恢复已开始场景时不会重复宣布事件发生。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref source, "sscRestrictionEvent", SSCRestrictionEvent.None);
            Scribe_Values.Look(ref notified, "sscRestrictionEventNotified", false);
        }

        /// <summary>由守卫确认实际 Start 后通知一次；交易收益仍归成交逻辑，通知不追加许可或成长。</summary>
        public void NotifyStarted(Pawn actor, Pawn target)
        {
            if (!IsEvent || notified) return;
            notified = true;
            switch (source)
            {
                case SSCRestrictionEvent.TradeConsensual:
                    Messages.Message("SSC_Message_TradeConsensualSex".Translate(actor.LabelShort, target.LabelShort),
                        new LookTargets(actor, target), MessageTypeDefOf.PositiveEvent);
                    break;
                case SSCRestrictionEvent.TradeForced:
                    Messages.Message("SSC_Message_TradeRapeOccurred".Translate(target.LabelShort, actor.LabelShort),
                        new LookTargets(actor, target), MessageTypeDefOf.NegativeEvent);
                    break;
                case SSCRestrictionEvent.Dog:
                    Messages.Message(Strings.Message_DogAnimalInteractionTriggered(actor.LabelShort, target.LabelShort),
                        new LookTargets(actor, target), MessageTypeDefOf.NeutralEvent);
                    break;
            }
        }
    }
}
