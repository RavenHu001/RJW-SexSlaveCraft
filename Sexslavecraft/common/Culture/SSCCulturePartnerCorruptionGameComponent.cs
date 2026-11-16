// AI 辅助生成：受影响 Pawn 的短冷却独立于场次、来源和人格，不保存可重放事件。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCCulturePartnerCorruptionGameComponent : GameComponent
    {
        private const int CooldownTicks = 600;
        private readonly Game game;
        // 对象是唯一保存维度。更换施加者、Job 或会话都继续使用同一个截止时间。
        private Dictionary<Pawn, long> deadlines = new Dictionary<Pawn, long>();
        // Scribe 在恢复 Pawn 引用前暂存两份列表；只在 PostLoadInit 后释放。
        private List<Pawn> savedPawns;
        private List<long> savedDeadlines;
        private int lastPrune = -1;

        public SSCCulturePartnerCorruptionGameComponent(Game game) { this.game = game; }

        internal static SSCCulturePartnerCorruptionGameComponent Current =>
            Verse.Current.Game?.GetComponent<SSCCulturePartnerCorruptionGameComponent>();

        private int Now => game?.tickManager?.TicksGame ?? -1;

        /// <summary>使用绝对游戏 tick，不依赖 Job 或更新频率；与截止 tick 相等即到期。</summary>
        internal bool CanClaim(Pawn target)
        {
            int now = Now;
            // 加载中的半恢复引用和另一局游戏的旧组件均不能接受结算。
            return now >= 0 && Scribe.mode == LoadSaveMode.Inactive && ReferenceEquals(game, Verse.Current.Game) &&
                SSCCultureStateUtility.IsLiving(target) && (!deadlines.TryGetValue(target, out long end) || end <= now);
        }

        internal bool TryClaim(Pawn target)
        {
            if (!CanClaim(target)) return false;
            int now = Now;
            // 只在实际认领时低频回收，空闲时不遍历世界，也不为到期记录补发事件。
            if (lastPrune < 0 || (long)now - lastPrune >= CooldownTicks) Prune(now);
            deadlines[target] = (long)now + CooldownTicks;
            return true;
        }

        private void Prune(int now)
        {
            // 先收集键再移除，只遍历本来源曾写入的稀疏记录，不读取地图或其他人物。
            List<Pawn> expired = null;
            foreach (var entry in deadlines)
                if (!SSCCultureStateUtility.IsLiving(entry.Key) || entry.Value <= now)
                    (expired ??= new List<Pawn>()).Add(entry.Key);
            if (expired != null)
                foreach (Pawn pawn in expired) deadlines.Remove(pawn);
            lastPrune = now;
        }

        public override void ExposeData()
        {
            // Reference + Value 只保存 Pawn 引用与截止时刻；旧档缺字段、跨引用失效都不生成新冷却。
            if (Scribe.mode == LoadSaveMode.Saving) Prune(Now);
            Scribe_Collections.Look(ref deadlines, "sscCulturePartnerCorruptionDeadlines", LookMode.Reference, LookMode.Value,
                ref savedPawns, ref savedDeadlines, logNullErrors: false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Scribe 已完成跨引用解析；缺节点等同空表。清理仅恢复数据，不发奖也不重新开始冷却。
                deadlines ??= new Dictionary<Pawn, long>();
                Prune(Now);
                savedPawns = null;
                savedDeadlines = null;
            }
        }
    }
}
