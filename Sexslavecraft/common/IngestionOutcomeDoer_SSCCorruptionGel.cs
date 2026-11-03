// AI 辅助生成：份数由原生摄入计算，凝胶只应用一层持续效果。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public sealed class IngestionOutcomeDoer_SSCCorruptionGel : IngestionOutcomeDoer
    {
        public HediffDef effect;
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (ingestedCount <= 0) return;
            if (SSCIdentityUtility.IsMaster(pawn)) { SSCCorruptionGelEffectUtility.RefreshBodyState(pawn); return; }
            SSCCorruptionGelIngestionRuntime.TryApply(pawn, ingested, effect);
        }
    }

    /// <summary>摄入通知早于Doer；凭据从真实Ingested入口捕获，不能在通知换载体后重新授权。</summary>
    internal static class SSCCorruptionGelIngestionRuntime
    {
        internal sealed class Frame
        {
            internal Frame Previous;
            internal Thing Item;
            internal Pawn Pawn;
            internal Pawn_HealthTracker Health;
            internal CompSexSlaveTraining Training;
            internal object Personality;
            internal Hediff_SSCCorruptionGelEffect Owner;
            internal bool Eligible, Claimed, Closed;
        }
        // 原版摄入是同步调用；链表只在当前调用栈存活，不写存档，也不保存额外药效选择。
        [ThreadStatic] private static Frame current;

        internal static Frame Begin(Thing item, Pawn pawn)
        {
            if (item?.def?.ingestible?.outcomeDoers?.Exists(d => d is IngestionOutcomeDoer_SSCCorruptionGel) != true) return null;
            var frame = new Frame { Previous = current, Item = item, Pawn = pawn, Health = pawn?.health,
                Training = pawn?.TryGetComp<CompSexSlaveTraining>(), Personality = SSCPersonalityRuntimeUtility.Capture(pawn),
                Owner = SSCCorruptionGelEffectUtility.Find(pawn),
                Eligible = Scribe.mode == LoadSaveMode.Inactive && SSCCorruptionGelEffectUtility.CanReceive(pawn) };
            current = frame;
            return frame;
        }

        /// <summary>每份真实摄入至多申请一次；脱离摄入的Doer不重拍凭据，显式直接输入仍可使用Apply。</summary>
        internal static bool TryApply(Pawn pawn, Thing item, HediffDef effect)
        {
            Frame frame = current;
            if (frame == null || frame.Closed || frame.Claimed || !frame.Eligible || !ReferenceEquals(frame.Pawn, pawn) ||
                !ReferenceEquals(frame.Item, item) || item.Destroyed || Scribe.mode != LoadSaveMode.Inactive ||
                !ReferenceEquals(pawn.health, frame.Health) || !ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), frame.Training) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, frame.Personality) ||
                !ReferenceEquals(SSCCorruptionGelEffectUtility.Find(pawn), frame.Owner) || !SSCCorruptionGelEffectUtility.CanReceive(pawn)) return false;
            frame.Claimed = true;
            return SSCCorruptionGelEffectUtility.Apply(pawn, effect);
        }

        /// <summary>正常、早退与异常均退栈；嵌套摄入不会覆盖或泄漏外层的原凭据。</summary>
        internal static void End(Frame frame)
        {
            if (frame == null || frame.Closed) return;
            frame.Closed = true;
            while (current?.Closed == true) current = current.Previous;
        }
    }
}
