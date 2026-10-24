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
                Func<bool> claim, Action release, SSCOrdinarySexPartsSnapshot parts, Func<bool> partsCurrent)
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
                forward = new Direction(initiator, recipient, allowProducer, parts, partsCurrent);
                reverse = new Direction(recipient, initiator, allowProducer, parts, partsCurrent);
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
            private readonly float infestationDose;
            private readonly Need_Corruption need;
            private readonly SSCDesireInfestationBodyUtility.Plan bodyPlan;
#if SSC_RW17
            private readonly SSCUterusDevelopmentGrowthUtility.Plan uterusPlan;
            private readonly SSCStomachDevelopmentGrowthUtility.Plan stomachPlan;
#endif
            private readonly SSCDesireInfestationFeedbackUtility.Plan feedbackPlan;
            private readonly Func<bool> partsCurrent;
            /// <summary>在原结算前冻结同一供给方向的资格、评分及各独立分量。</summary>
            public Direction(Pawn source, Pawn target, bool allowProducer, SSCOrdinarySexPartsSnapshot parts, Func<bool> partsCurrent)
            {
                this.source = source; this.target = target;
                infestation = SSCDesireInfestationUtility.CanAffect(source, target);
                producer = allowProducer && SSCPnaSourceUtility.IsPnaProducer(source);
                // 与其他完成前快照一起冻结剂量；原结算及第一方向回调改写器官不能改变本次输入。
                if (infestation) infestationDose = SSCPnaSexSupplyUtility.CaptureInfestationDose(source);
                need = target?.needs?.TryGetNeed<Need_Corruption>();
                // 反馈先冻结当前 C/O；后续评分查询、RJW 新记忆及本场侵染增量不重判本档。
                if (infestation) feedbackPlan = SSCDesireInfestationFeedbackUtility.Capture(source, target, need);
                this.partsCurrent = partsCurrent;
                // 与 PNA/C 同一原结算前边界冻结评分、倍率和候选；无可靠部位只留下空开发计划。
                if (infestation && partsCurrent?.Invoke() == true)
#if SSC_RW17
                {
                    // 各独立开发项目共用一次原结算前评分；旧评分的随机项不推进 RJW 随机序列。
                    float? capturedScore = null;
                    float Score()
                    {
                        if (capturedScore.HasValue) return capturedScore.Value;
                        Rand.PushState();
                        try { capturedScore = TrainingOutcomeUtility.GetScore(source, target); }
                        finally { Rand.PopState(); }
                        return capturedScore.Value;
                    }
                    bodyPlan = SSCDesireInfestationBodyUtility.Capture(source, target, parts, Score);
                    uterusPlan = SSCUterusDevelopmentGrowthUtility.CaptureInfestation(source, target, parts, Score);
                    stomachPlan = SSCStomachDevelopmentGrowthUtility.CaptureInfestation(source, target, parts, Score);
                }
#else
                    bodyPlan = SSCDesireInfestationBodyUtility.Capture(source, target, parts);
#endif
            }
            /// <summary>只在整场完成认领内执行，逐个分量复查原场景与人格归属。</summary>
            public void Apply(Func<bool> contextCurrent)
            {
                if (!contextCurrent()) return;
                bool StrongCurrent() => contextCurrent() && SSCDesireInfestationUtility.CanAffect(source, target) &&
                    target.needs?.TryGetNeed<Need_Corruption>() == need;
                // 调用前选择一次完整来源规则；强来源处理后不回退再输入弱来源。
                if (infestation && StrongCurrent())
                {
                    SSCPnaSexSupplyUtility.ApplyInfestation(source, target, infestationDose, StrongCurrent);
                    // PNA 分量未产生效果不等于侵染失效；人格与资格仍有效时独立结算恶堕。
                    if (StrongCurrent()) SSCDesireInfestationUtility.ApplyCorruption(source, target, need, StrongCurrent);
                    // 恶堕达到上限仍可开发；每条身体写入复查人格、需求和原部位凭据。
                    if (StrongCurrent() && bodyPlan != null)
                        bodyPlan.Apply(() => StrongCurrent() && partsCurrent?.Invoke() == true);
#if SSC_RW17
                    // 六部位和子宫各用自己的系数；仍共享本场认领，训练 Job 不会进入此出口。
                    if (StrongCurrent() && uterusPlan != null)
                        uterusPlan.Apply(() => StrongCurrent() && partsCurrent?.Invoke() == true);
                    // 新项目与其他侵蚀分量独立；写入仍复查原身体、场景及严格口交证据。
                    if (StrongCurrent() && stomachPlan != null)
                        stomachPlan.Apply(() => StrongCurrent() && partsCurrent?.Invoke() == true);
#endif
                    // 无研究、无可靠部位或开发已满均不取消反馈；原版 Lovin 也走同一独立分量。
                    if (StrongCurrent() && feedbackPlan != null) feedbackPlan.Apply(StrongCurrent);
                }
                else if (producer && SSCPnaSourceUtility.IsPnaProducer(source))
                    SSCPnaSexSupplyUtility.ApplyFromProducer(source, target,
                        () => contextCurrent() && SSCPnaSourceUtility.IsPnaProducer(source));
            }
        }

        public static Context Capture(Pawn initiator, Pawn recipient, bool allowProducer,
            Func<bool> sceneCurrent, Func<bool> claim, Action release = null)
            // 保留第二批已公开的六参数签名，原版 Lovin 和既有调用不需要身体凭据。
            => Capture(initiator, recipient, allowProducer, sceneCurrent, claim, release, null, null);

        public static Context Capture(Pawn initiator, Pawn recipient, bool allowProducer,
            Func<bool> sceneCurrent, Func<bool> claim, Action release,
            SSCOrdinarySexPartsSnapshot parts, Func<bool> partsCurrent)
            => new Context(initiator, recipient, allowProducer, sceneCurrent, claim, release, parts, partsCurrent);
    }
}
