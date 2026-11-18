// AI 辅助生成：按受影响 Pawn 和增长途径保存绝对截止时刻。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCCulturePartnerCorruptionGameComponent : GameComponent
    {
        private const int PruneInterval = 1000;
        private readonly Game game;
        // 保留组件类型和原文化保存键，让旧档的文化截止时刻继续生效。凝胶追加独立表。
        private Dictionary<Pawn, long> partnerDeadlines = new();
        private Dictionary<Pawn, long> gelDeadlines = new();
        // 两张表的 Scribe 引用队列分别持有到 PostLoadInit，不能在引用解析前共用或重置。
        private List<Pawn> savedPartnerPawns, savedGelPawns;
        private List<long> savedPartnerDeadlines, savedGelDeadlines;
        private int lastPrune = -1;

        public SSCCulturePartnerCorruptionGameComponent(Game game) { this.game = game; }

        internal static SSCCulturePartnerCorruptionGameComponent Current =>
            Verse.Current.Game?.GetComponent<SSCCulturePartnerCorruptionGameComponent>();

        private int Now => game?.tickManager?.TicksGame ?? -1;

        private Dictionary<Pawn, long> Deadlines(SSCCorruptionGainSource source) =>
            source == SSCCorruptionGainSource.Gel ? gelDeadlines : partnerDeadlines;

        /// <summary>使用绝对游戏 tick；更换施加者、Job 或人格时继续使用同一途径的截止时刻。</summary>
        internal bool CanClaim(Pawn target, SSCCorruptionGainSource source)
        {
            int now = Now;
            // 加载中的半恢复引用和另一局游戏的旧组件均不能接受结算。
            return now >= 0 && Scribe.mode == LoadSaveMode.Inactive && ReferenceEquals(game, Verse.Current.Game) &&
                SSCCultureStateUtility.IsLiving(target) && (!Deadlines(source).TryGetValue(target, out long end) || end <= now);
        }

        internal bool TryClaim(Pawn target, SSCCorruptionGainSource source, int cooldownTicks)
        {
            if (!CanClaim(target, source)) return false;
            if (cooldownTicks == 0) return true;
            int now = Now;
            // 清理频率独立于配置，仅在有实际认领或存读档时遍历两个稀疏表。
            if (lastPrune < 0 || (long)now - lastPrune >= PruneInterval) Prune(now);
            Deadlines(source)[target] = (long)now + cooldownTicks;
            return true;
        }

        private void Prune(int now)
        {
            Prune(partnerDeadlines, now);
            Prune(gelDeadlines, now);
            lastPrune = now;
        }

        private static void Prune(Dictionary<Pawn, long> deadlines, int now)
        {
            // 先收集键再移除，清理只释放引用，也不为到期记录补发事件。
            List<Pawn> expired = null;
            foreach (var entry in deadlines)
                if (!SSCCultureStateUtility.IsLiving(entry.Key) || entry.Value <= now)
                    (expired ??= new List<Pawn>()).Add(entry.Key);
            if (expired != null)
                foreach (Pawn pawn in expired) deadlines.Remove(pawn);
        }

        public override void ExposeData()
        {
            // Reference + Value 只保存 Pawn 引用与绝对截止时刻。更新 XML 后已有截止时刻继续生效。
            if (Scribe.mode == LoadSaveMode.Saving) Prune(Now);
            Scribe_Collections.Look(ref partnerDeadlines, "sscCulturePartnerCorruptionDeadlines", LookMode.Reference, LookMode.Value,
                ref savedPartnerPawns, ref savedPartnerDeadlines, logNullErrors: false);
            Scribe_Collections.Look(ref gelDeadlines, "sscCorruptionGelDeadlines", LookMode.Reference, LookMode.Value,
                ref savedGelPawns, ref savedGelDeadlines, logNullErrors: false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Scribe 已完成跨引用解析；缺节点等同空表。清理仅恢复数据，不发奖也不重新开始冷却。
                partnerDeadlines ??= new Dictionary<Pawn, long>();
                gelDeadlines ??= new Dictionary<Pawn, long>();
                Prune(Now);
                savedPartnerPawns = savedGelPawns = null;
                savedPartnerDeadlines = savedGelDeadlines = null;
            }
        }
    }
}
