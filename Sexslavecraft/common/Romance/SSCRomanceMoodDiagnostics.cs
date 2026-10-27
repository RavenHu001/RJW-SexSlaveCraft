// AI 辅助生成：只读运行期诊断，不保存、授权、重试或参与心情业务。
using System;
using System.Linq;
using System.Threading;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>统一详细日志入口；只格式化已经捕获的事实，关闭日志时不构建文本。</summary>
    internal static class SSCRomanceMoodDiagnostics
    {
        private static long sequence;
        /// <summary>关联编号不使用游戏随机数，不进入存档或人格／克隆副本。</summary>
        internal static long NextId() => Interlocked.Increment(ref sequence);
        /// <summary>只读稳定 ID，避免 Label 等显示属性在日志中再次触发人物查询。</summary>
        private static string PawnId(Pawn pawn) => pawn == null ? "none" : pawn.thingIDNumber.ToString();
        /// <summary>日志故障不能改变已完成结算，也不能从清理器制造新异常。</summary>
        private static void Write(Func<string> text)
        { try { if (SSCLog.VerboseEnabled) SSCLog.Verbose("[SSC RomanceMood] " + text()); } catch (Exception) { } }

        /// <summary>持续状态只由通知输出；这里表示当前控制分支选择，不冒充原版准入后的获得事件。</summary>
        internal static void Control(Pawn pawn, string old, SSCRomanceMoodBranch branch, bool baseline, string reason)
        {
            if (!SSCLog.VerboseEnabled) return;
            Write(() => $"control pawn={PawnId(pawn)} old={old ?? "none"} selected={branch?.Id ?? "none"} value={branch?.Value ?? 0} baseline={baseline} reason={reason}");
        }

        /// <summary>实际请求完成后输出；新值、旧值与期限来自冻结分支及实际合并回执。</summary>
        internal static void Settlement(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person, Pawn other,
            SSCRomanceMoodSelection selection, SSCRomanceMoodResult result, string reason, SSCRomanceSetting? ownRule)
        {
            if (!SSCLog.VerboseEnabled) return;
            Write(() => $"event id={frame.DiagnosticId} pawn={PawnId(person?.Pawn)} other={PawnId(other)} action={frame.Action} command={frame.Command}" +
                $" decisionKnown={frame.DecisionKnown} original={(frame.DecisionKnown ? frame.OriginalAccepted.ToString() : "unknown")} accepted={(frame.DecisionKnown ? frame.Accepted.ToString() : "unknown")}" +
                $" separated={frame.ProposalSeparated} rule={ownRule?.ToString() ?? "none"} decisionRule={frame.Rule?.ToString() ?? "none"} " +
                $" event={selection?.Branch.Event.ToString() ?? "unknown"} stage={person?.Data?.Stage} masochist={person?.Data?.Masochist}" +
                $" rawOpinion={person?.Data?.RawOpinion} band={person?.Data?.OpinionBand} branch={selection?.Branch.Id ?? "unknown"} value={selection?.Branch.Value}" +
                $" semantic={selection?.SemanticGroup.ToString() ?? "none"} order={frame.OccurrenceOrder} phase={result?.Phase.ToString() ?? "none"}" +
                $" group={selection?.Group.ToString() ?? "none"} nominalTicks={selection?.DurationTicks ?? 0} remainingTicks={result?.RemainingTicks ?? 0}" +
                $" oldBranch={result?.OldBranch ?? "none"} oldValue={result?.OldValue?.ToString() ?? "none"} oldRemainingTicks={result?.OldRemainingTicks ?? 0}" +
                $" result={result?.Kind.ToString() ?? "Skipped"} reason={reason} native={(person?.NativeApplied == true ? "committed" : "preserved")}" +
                $" undoAdd={person?.NativeAddsUndone ?? 0} undoRenew={person?.NativeRenewsUndone ?? 0} restoreCapacity={person?.NativeRemovesUndone ?? 0}" +
                $" removed={Removed(result)}");
        }

        /// <summary>记录实际清理集合；同组更新与跨组阶段清理分开标明，不把预检计划冒充删除结果。</summary>
        private static string Removed(SSCRomanceMoodResult result)
            => result?.Removed == null || result.Removed.Count == 0 ? "none" : string.Join(";", result.Removed.Select(item =>
                $"{PawnId(item.Other)}:{item.SemanticGroup}:{item.BranchId ?? "unknown"}:{item.Value}:{item.RemainingTicks}:{item.Reason}"));

        /// <summary>自然阶段与附带旧伴侣只报告清理，不伪装成受控记忆发放或原版撤销。</summary>
        internal static void Cleanup(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person, Pawn other,
            SSCRomanceMoodPhase phase, SSCRomanceMoodResult result, bool former, string reason)
        {
            if (!SSCLog.VerboseEnabled) return;
            Write(() => $"cleanup id={frame.DiagnosticId} pawn={PawnId(person?.Pawn)} other={PawnId(other)} order={frame.OccurrenceOrder}" +
                $" phase={phase} formerPartner={former} result={result?.Kind.ToString() ?? "Skipped"} reason={reason}" +
                $" native=preserved removed={Removed(result)}");
        }

        /// <summary>一帧退出原因只记录一次；已发生事件的两端回执仍分别输出。</summary>
        internal static void Finish(SSCRomanceMoodFrame frame, string reason)
        {
            if (frame == null || frame.DiagnosticFinished) return;
            frame.DiagnosticFinished = true;
            if (!SSCLog.VerboseEnabled || !(frame.Command || frame.ActorState?.Applicable == true || frame.TargetState?.Applicable == true)) return;
            Write(() => $"finish id={frame.DiagnosticId} actor={PawnId(frame.Actor)} target={PawnId(frame.Target)} reason={reason}");
        }

        /// <summary>尚未取得心情快照的互动与寻路中断也能定位；不因此捕获人格或构造事件。</summary>
        internal static void Attempt(long id, Pawn actor, Pawn target, string reason)
        {
            if (!SSCLog.VerboseEnabled) return;
            Write(() => $"attempt id={id} actor={PawnId(actor)} target={PawnId(target)} reason={reason}");
        }
    }
}
