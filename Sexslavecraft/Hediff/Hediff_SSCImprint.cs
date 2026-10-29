// AI 辅助生成：三种互斥 Def 共用类，不另存人格状态或身份锁。
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>三态共用的永久人格载体；形态由 Def 保存，属性由各自 XML 阶段提供。</summary>
    public sealed class Hediff_SSCImprint : Hediff
    {
        /// <summary>同定义重复加入沿用原实例，不相加严重度；不同形态由加入后互斥检查处理。</summary>
        public override bool TryMergeWith(Hediff other) => other?.def == def;

        /// <summary>加入健康列表后保留唯一印记，并为新取得或迁入的分化态执行即时年龄纠正。</summary>
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // 外部重复添加不叠属性；已有另一形态时保留原记录，不从添加顺序推断身份转换。
            if (!ReferenceEquals(SSCImprintUtility.Find(pawn), this))
            { pawn.health.RemoveHediff(this); return; }
            SSCImprintUtility.CorrectAge(pawn);
        }

        /// <summary>保留原版 Hediff 维护，每游戏日按哈希间隔纠正一次外部造成的超龄。</summary>
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            // 正常停龄由年龄推进补丁处理，这里不每 Tick 重写年龄或身份。
            if (pawn.IsHashIntervalTick(GenDate.TicksPerDay, delta)) SSCImprintUtility.CorrectAge(pawn);
        }
    }
}
