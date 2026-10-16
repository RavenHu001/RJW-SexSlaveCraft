// AI 辅助生成
using System;
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>侵染专用的 20% 总预算；评分与倍率在原结算前冻结，部位写入使用通用经验接口。</summary>
    public static class SSCDesireInfestationBodyUtility
    {
        public const float ExperienceFactor = .20f;

        internal sealed class Candidate
        {
            internal BodyPartRecord Part;
            internal HediffDef Def;
            internal readonly List<SSCOrdinarySexPartsSnapshot.Part> Evidence = new List<SSCOrdinarySexPartsSnapshot.Part>();
            internal bool IsAttached() => Evidence.Exists(e => e.IsAttached());
        }

        /// <summary>本次开发的不可变分配；不保存另一份累计经验或独立完成认领。</summary>
        public sealed class Plan
        {
            private readonly Pawn target;
            private readonly List<Candidate> candidates;
            private readonly float share;
            internal Plan(Pawn target, List<Candidate> candidates, float budget)
            { this.target = target; this.candidates = candidates; share = budget / candidates.Count; }

            internal void Apply(Func<bool> isCurrent)
            {
                if (!isCurrent() || !ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BodyPartTraining)) return;
                // 分母已冻结，包含已满记录；失效/饱和候选的份额不会补给其他部位。
                foreach (var candidate in candidates)
                {
                    if (!isCurrent()) return;
                    if (candidate.IsAttached())
                        TrainingExpUtility.TryApplyExperienceToPart(target, candidate.Part, candidate.Def, share,
                            () => isCurrent() && candidate.IsAttached());
                    // Severity/AddHediff 可触发健康和第三方回调；下一个部位必须重新验证整场归属。
                }
            }
        }

        internal static Plan Capture(Pawn source, Pawn target, SSCOrdinarySexPartsSnapshot parts)
        {
            // 可选分量的第三方评分/器官查询失败不应阻断原结算及第二批分量。
            try { return CaptureCore(source, target, parts); }
            catch (Exception ex) { Log.Warning("[SSC InfestationBody] Cannot prepare optional development: " + ex.GetType().Name); return null; }
        }

        private static Plan CaptureCore(Pawn source, Pawn target, SSCOrdinarySexPartsSnapshot parts)
        {
            // 这里只收窄身体开发资格；研究、评分载体或部位不足时不修改整套侵染准入。
            if (Scribe.mode != LoadSaveMode.Inactive || parts?.Parts == null || !SSCDesireInfestationUtility.CanAffect(source, target) ||
                !ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BodyPartTraining) ||
                source.skills == null || target.relations == null ||
                !((parts.Initiator == source && parts.Recipient == target) ||
                  (parts.Initiator == target && parts.Recipient == source))) return null;
            var candidates = new List<Candidate>();
            foreach (var evidence in parts.Parts)
            {
                if (evidence == null || evidence.Owner != target || !evidence.IsAttached()) continue;
                HediffDef def = SSCOrdinarySexPartsUtility.GetExperienceDef(evidence.Family);
                if (def == null) continue;
                Candidate candidate = candidates.Find(c => c.Part == evidence.BodyPart && c.Def == def);
                if (candidate == null) { candidate = new Candidate { Part = evidence.BodyPart, Def = def }; candidates.Add(candidate); }
                candidate.Evidence.Add(evidence);
            }
            if (candidates.Count == 0) return null;
            // 旧评分含随机项；隔离新增取样，不移动 RJW 后续怀孕/记忆等原结算的随机序列。
            float score;
            Rand.PushState();
            try { score = TrainingOutcomeUtility.GetScore(source, target); }
            finally { Rand.PopState(); }
            float budget = TrainingExpUtility.CalculateExperienceBudget(score, ExperienceFactor,
                TrainingExpUtility.GetCorruptionStageMultiplier(target), TrainingOutcomeUtility.GetChainStageMult(target));
            return budget > 0f ? new Plan(target, candidates, budget) : null;
        }
    }
}
