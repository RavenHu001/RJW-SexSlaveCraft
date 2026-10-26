// AI 辅助生成：只保存真实事件的先后顺序，不保存命令、人格授权或待结算请求。
using System;
using System.Threading;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>独立于诊断编号的历史排序入口；查询、复制及读档只观察既存水位。</summary>
    public static class SSCRomanceMoodOrder
    {
        // 隔离机制宿主可能没有 Game；该水位不属于存档，也不替代有游戏时的保存组件。
        private static long isolatedOrder;
        private static SSCRomanceMoodOrderGameComponent Current => Verse.Current.Game?.GetComponent<SSCRomanceMoodOrderGameComponent>();

        /// <summary>仅在真实事件已确认时调用；技术失败允许空号，溢出时拒绝而不回绕成旧事件。</summary>
        public static long Next()
        {
            var component = Current;
            if (component != null) return component.Next();
            while (true)
            {
                long old = Interlocked.Read(ref isolatedOrder);
                long next = checked(old + 1L);
                if (Interlocked.CompareExchange(ref isolatedOrder, next, old) == old) return next;
            }
        }

        /// <summary>恢复已保存历史时抬高水位，不修改条目或创造新事件，损坏的非正序号不作为时间。</summary>
        public static void Observe(long order)
        {
            if (order <= 0) return;
            var component = Current;
            if (component != null) { component.Observe(order); return; }
            while (true)
            {
                long old = Interlocked.Read(ref isolatedOrder);
                if (old >= order || Interlocked.CompareExchange(ref isolatedOrder, order, old) == old) return;
            }
        }
    }

    /// <summary>每个存档独立的发生顺序水位；不枚举世界、不发放心情，也不加入 Tick 工作。</summary>
    public sealed class SSCRomanceMoodOrderGameComponent : GameComponent
    {
        private long latest, observed;
        /// <summary>由游戏创建组件，不从上一个游戏或诊断日志继承序号。</summary>
        public SSCRomanceMoodOrderGameComponent(Game game) { }
        /// <summary>只增加真实事件水位；同 Tick 的多个事件仍有严格先后。</summary>
        internal long Next() => latest = checked(latest + 1L);
        /// <summary>离体人格等历史可能先于组件加载完成，另存已见水位防止后续读档覆盖。</summary>
        internal void Observe(long order) { observed = Math.Max(observed, order); latest = Math.Max(latest, observed); }
        /// <summary>保存独立水位；读档收尾取已保存和已观察的最大值，不重排任何历史条目。</summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref latest, "sscRomanceMoodOccurrenceOrder", 0L);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) latest = Math.Max(Math.Max(0L, latest), observed);
        }
    }
}
