// AI 辅助生成：将实际满值的启动移到该 tick 的任务及印记结算之后。
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCSlaveBreakdownGameComponent : GameComponent
    {
        internal sealed class FullRequest
        {
            internal readonly Pawn Pawn;
            internal readonly Need_Corruption Need;
            internal readonly int CancellationVersion;
            private readonly CompSexSlaveTraining comp;
            private readonly Pawn_HealthTracker health;
            private readonly object token;
            private readonly long expiresAt;

            internal FullRequest(Pawn pawn, Need_Corruption need, CompSexSlaveTraining comp, int cancellationVersion)
            {
                Pawn = pawn;
                Need = need;
                CancellationVersion = cancellationVersion;
                this.comp = comp;
                health = pawn.health;
                token = SSCPersonalityRuntimeUtility.Capture(pawn);
                expiresAt = (long)SSCSlaveBreakdown.Now + GenDate.TicksPerHour;
            }

            internal bool IsCurrent => SSCSlaveBreakdown.Now < expiresAt &&
                Need.CurLevel >= SSCSlaveBreakdown.TriggerLevel && SSCSlaveBreakdown.CanConsider(Pawn, Need) &&
                Pawn.health == health && Pawn.TryGetComp<CompSexSlaveTraining>() == comp &&
                !comp.SlaveBreakdownFullTriggerUsed && SSCPersonalityRuntimeUtility.IsCurrent(Pawn, token);
        }

        // 待办和启动调用栈只存在于本局运行期间。冷却与满值回落状态由 Pawn 自己保存。
        private readonly Dictionary<Pawn, FullRequest> fullRequests = new();
        private readonly List<FullRequest> refresh = new();
        // 原版可能返回 true，但 PostStart 未运行或 SSC 身份和 Hediff 没有安装完成。
        // 只有 PostStart 提交成功才把 Started 写为 true。满值来源另用于更新直发记录。
        private readonly Dictionary<Pawn, (bool FromFull, bool Started)> starting = new();
        private int fullRequestCancellationVersion;

        public SSCSlaveBreakdownGameComponent(Game game) { }

        internal static SSCSlaveBreakdownGameComponent Current
            => Verse.Current.Game?.GetComponent<SSCSlaveBreakdownGameComponent>();

        internal FullRequest CaptureFullCrossing(Pawn pawn, Need_Corruption need)
        {
            int version = fullRequestCancellationVersion;
            if (!SSCSlaveBreakdown.CanConsider(pawn, need) || SSCSlaveBreakdown.StateDef == null ||
                starting.ContainsKey(pawn) || HasFullRequest(pawn)) return null;
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            // 恢复的人格可真实处于重启线以下，即使恢复过程本身不制造新的满值跨越。
            if (need.CurLevel < SSCSlaveBreakdown.RearmBelowLevel) comp.SlaveBreakdownFullTriggerUsed = false;
            return comp.SlaveBreakdownFullTriggerUsed ? null : new FullRequest(pawn, need, comp, version);
        }

        internal void QueueFullCrossing(FullRequest request)
        {
            if (request.IsCurrent && request.Need.CurLevel == request.Need.MaxLevel && !HasFullRequest(request.Pawn) &&
                request.CancellationVersion == fullRequestCancellationVersion)
                fullRequests[request.Pawn] = request;
        }

        internal bool HasFullRequest(Pawn pawn) => fullRequests.TryGetValue(pawn, out FullRequest request) && request.IsCurrent;

        internal void CancelFullRequest(Pawn pawn, Need_Corruption need = null)
        {
            if (need == null || (fullRequests.TryGetValue(pawn, out FullRequest request) && request.Need == need))
                fullRequests.Remove(pawn);
        }

        internal void CancelAllFullRequests()
        {
            // 需求写入可触发回调。关闭也要撤销已捕获、尚未交还到队列的旧请求。
            fullRequestCancellationVersion++;
            fullRequests.Clear();
        }

        public override void GameComponentTick()
        {
            if (fullRequests.Count == 0 || refresh.Count != 0) return;
            // 启动会触发 StopAll 和信件回调。用快照迭代，且只消费自己看到的那份待办。
            refresh.AddRange(fullRequests.Values);
            try
            {
                foreach (FullRequest request in refresh)
                {
                    Pawn pawn = request.Pawn;
                    if (!IsRegistered(request)) continue;
                    bool canAttempt = request.IsCurrent && !SSCSlaveBreakdown.BlockedByVanilla(pawn);
                    if (canAttempt && (SSCFullCorruptionImprint.IsSettling(pawn, request.Need) ||
                        SSCSlaveBreakdown.TemporarilyBlocked(pawn))) continue;
                    try
                    {
                        if (canAttempt) TryStart(pawn, fromFull: true);
                    }
                    finally
                    {
                        if (IsRegistered(request)) fullRequests.Remove(pawn);
                    }
                }
            }
            finally { refresh.Clear(); }
        }

        private bool IsRegistered(FullRequest request)
            => fullRequests.TryGetValue(request.Pawn, out FullRequest current) && current == request;

        internal bool TryStart(Pawn pawn, string reason = null, bool causedByMood = false, bool fromFull = false)
        {
            MentalStateDef def = SSCSlaveBreakdown.StateDef;
            if (def?.Worker.StateCanOccur(pawn) != true || !starting.TryAdd(pawn, (fromFull, false))) return false;
            try
            {
                return pawn.mindState.mentalStateHandler.TryStartMentalState(def, reason, causedByMood: causedByMood) && starting[pawn].Started;
            }
            finally { starting.Remove(pawn); }
        }

        /// <summary>身份与效果就绪后确认启动，在原版任务中断和发信之前清理本场的满值待办。</summary>
        internal void NotifyStarted(Pawn pawn)
        {
            CancelFullRequest(pawn);
            if (!starting.TryGetValue(pawn, out var attempt)) return;
            starting[pawn] = (attempt.FromFull, true);
            if (attempt.FromFull &&
                pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp)
                comp.SlaveBreakdownFullTriggerUsed = true;
        }
    }
}
