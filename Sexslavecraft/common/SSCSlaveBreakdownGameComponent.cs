// AI 辅助生成：将实际满值的启动移到该 tick 的任务及印记结算之后。
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCSlaveBreakdownGameComponent : GameComponent
    {
        /// <summary>一次实际满值跨越的短期请求，由原需求、健康载体和人格共同持有。</summary>
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

            // 入队必须达到满值。等待期间允许回落到 95%，换人格、进入冷却或达到期限都会使旧请求失效。
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

        /// <summary>写入满值前捕获原载体。同步回调可更换人格或健康载体，同场分化则仍可继续原请求。</summary>
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

        /// <summary>实际值落地后登记待办，印记分化及任务结算完成后再尝试启动。</summary>
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
                    // 等印记事务完成，包括可能在后续需求刷新中进行的重试。之后复查实际锁链及其他准入，
                    // 让同一次跨越先完成分化，而无链且仍合格的小人仍能接着发作。
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

        /// <summary>供需求随机判定或有效满值待办使用。满值来源仅在启动成功后登记为已用。</summary>
        internal bool TryStart(Pawn pawn, bool fromFull = false)
            => StartState(pawn, fromFull, forced: false);

        // 显式测试跳过功能开关、恶堕阈值和冷却。使用同一 PostStart，不制造满值跨越。
        internal bool TryStartForDebug(Pawn pawn)
            => Prefs.DevMode && SSCMod.settings?.enableDebugGizmos == true && Scribe.mode == LoadSaveMode.Inactive &&
               SSCSlaveBreakdown.Now >= 0 && SSCSlaveBreakdown.IsEligiblePawn(pawn) &&
               pawn.mindState?.mentalStateHandler != null && pawn.TryGetComp<CompSexSlaveTraining>() != null &&
               !SSCSlaveBreakdown.TemporarilyBlocked(pawn) && !SSCSlaveBreakdown.BlockedByVanilla(pawn) &&
                StartState(pawn, fromFull: false, forced: true);

        private bool StartState(Pawn pawn, bool fromFull, bool forced)
        {
            MentalStateDef def = SSCSlaveBreakdown.StateDef;
            if (def == null || (!forced && !def.Worker.StateCanOccur(pawn)) ||
                !starting.TryAdd(pawn, (fromFull, false))) return false;
            try
            {
                // 直接向原版状态处理器启动，沿用状态槽、任务中断和开始信。原版低心情抽选有自己的候选入口。
                return pawn.mindState.mentalStateHandler.TryStartMentalState(def, forced: forced) && starting[pawn].Started;
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
