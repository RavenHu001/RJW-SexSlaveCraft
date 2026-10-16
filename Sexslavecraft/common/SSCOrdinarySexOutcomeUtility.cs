// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次真实普通完成的局部快照与分发；任务适配器提供认领和归属检查。</summary>
    public static class SSCOrdinarySexOutcomeUtility
    {
        public sealed class Context : IDisposable
        {
            public readonly Pawn Initiator, Recipient;
            private readonly CompSexSlaveTraining initiatorComp, recipientComp;
            private readonly PawnIdentity initiatorIdentity, recipientIdentity;
            private readonly object initiatorToken, recipientToken;
            private readonly Func<bool> sceneCurrent, claim;
            private readonly Action release;
            private readonly Direction forward, reverse;
            private bool disposed, attempted, claimed, dispatched;

            internal Context(Pawn initiator, Pawn recipient, bool allowProducer, Func<bool> sceneCurrent,
                Func<bool> claim, Action release)
            {
                Initiator = initiator; Recipient = recipient;
                this.sceneCurrent = sceneCurrent; this.claim = claim; this.release = release;
                initiatorComp = initiator?.TryGetComp<CompSexSlaveTraining>();
                recipientComp = recipient?.TryGetComp<CompSexSlaveTraining>();
                initiatorIdentity = initiatorComp?.pawnIdentity ?? PawnIdentity.Unset;
                recipientIdentity = recipientComp?.pawnIdentity ?? PawnIdentity.Unset;
                initiatorToken = SSCPersonalityRuntimeUtility.Capture(initiator);
                recipientToken = SSCPersonalityRuntimeUtility.Capture(recipient);
                // 原结算前记录两个方向的资格；原结算或第一方向回调新增的能力不能补领本场。
                forward = new Direction(initiator, recipient, allowProducer);
                reverse = new Direction(recipient, initiator, allowProducer);
            }

            public bool IsCurrent => !disposed && Scribe.mode == LoadSaveMode.Inactive && sceneCurrent() &&
                Matches(Initiator, initiatorComp, initiatorIdentity, initiatorToken) &&
                Matches(Recipient, recipientComp, recipientIdentity, recipientToken);

            private static bool Matches(Pawn pawn, CompSexSlaveTraining comp, PawnIdentity identity, object token)
                => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded &&
                   pawn.TryGetComp<CompSexSlaveTraining>() == comp && (comp?.pawnIdentity ?? PawnIdentity.Unset) == identity &&
                   SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) && !SSCPersonalityRuntimeUtility.IsMutating(pawn);

            public bool TryClaim()
            {
                // 先消费适配器拥有的旧场认领，再检查人格；换人格不能让后来调用补领旧场。
                if (disposed || attempted || Scribe.mode != LoadSaveMode.Inactive) return false;
                attempted = true;
                claimed = claim() && IsCurrent;
                return claimed;
            }

            public void Dispatch()
            {
                if (!claimed || dispatched || !IsCurrent) return;
                // 分发自身也可能从健康通知重入；在第一次写入前封住这份上下文。
                dispatched = true;
                forward.Apply(() => IsCurrent);
                if (IsCurrent) reverse.Apply(() => IsCurrent);
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                release?.Invoke();
            }
        }

        private sealed class Direction
        {
            private readonly Pawn source, target;
            private readonly bool infestation, producer;
            private readonly Need_Corruption need;
            public Direction(Pawn source, Pawn target, bool allowProducer)
            {
                this.source = source; this.target = target;
                infestation = SSCDesireInfestationUtility.CanAffect(source, target);
                producer = allowProducer && SSCPnaSourceUtility.IsPnaProducer(source);
                need = target?.needs?.TryGetNeed<Need_Corruption>();
            }
            public void Apply(Func<bool> contextCurrent)
            {
                if (!contextCurrent()) return;
                bool StrongCurrent() => contextCurrent() && SSCDesireInfestationUtility.CanAffect(source, target) &&
                    target.needs?.TryGetNeed<Need_Corruption>() == need;
                // 调用前选择一次完整来源规则；强来源处理后不回退再输入弱来源。
                if (infestation && StrongCurrent())
                {
                    SSCPnaSexSupplyUtility.ApplyInfestation(source, target, StrongCurrent);
                    // PNA 分量未产生效果不等于侵染失效；人格与资格仍有效时独立结算恶堕。
                    if (StrongCurrent()) SSCDesireInfestationUtility.ApplyCorruption(source, target, need, StrongCurrent);
                }
                else if (producer && SSCPnaSourceUtility.IsPnaProducer(source))
                    SSCPnaSexSupplyUtility.ApplyFromProducer(source, target,
                        () => contextCurrent() && SSCPnaSourceUtility.IsPnaProducer(source));
            }
        }

        public static Context Capture(Pawn initiator, Pawn recipient, bool allowProducer,
            Func<bool> sceneCurrent, Func<bool> claim, Action release = null)
            => new Context(initiator, recipient, allowProducer, sceneCurrent, claim, release);
    }
}
