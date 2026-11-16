using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>用同一组当前值临界点决定恶堕档位、需求刻度和持续心情。</summary>
    internal static class SSCCorruptionStages
    {
        internal static readonly IReadOnlyList<int> Thresholds = Array.AsReadOnly(new[] { 12, 30, 50, 90 });
        private const int FirstBoundTextStage = 4;

        internal static int GetWholePercentage(float level)
        {
            if (!(level > 0f)) return 0;
            if (level >= 1f) return 100;
            // 用 double 做乘法，避免将临界点前的 float 向上舍入。恰好等于整百分点的游戏 float 时归入该百分点。
            int percentage = (int)((double)level * 100d);
            return level >= (percentage + 1) / 100f ? percentage + 1 : percentage;
        }

        internal static int GetIndex(int percentage)
        {
            for (int i = Thresholds.Count - 1; i >= 0; i--)
                if (percentage >= Thresholds[i]) return i + 1;
            return 0;
        }

        internal static string GetLabel(int index) => index switch
        {
            1 => Strings.Stage_Minor,
            2 => Strings.Stage_Moderate,
            3 => Strings.Stage_Severe,
            4 => Strings.Stage_Extreme,
            _ => Strings.Stage_Stable
        };

        private static (int ChainStage, int SatisfiedThrough) GetMoodContext(Pawn pawn)
        {
            if (SSCIdentityUtility.IsMaster(pawn)) return (0, 100);
            if (SSCIdentityUtility.IsSexSlave(pawn))
            {
                var stage = TrainingOutcomeUtility.GetChainStage(SSCBondUtility.GetChain(pawn));
                // 按显示的整数判定，三阶显示 90% 时仍满足。无实际阶段时沿用普通角色的当前档位。
                if (stage.Stage >= 4) return (stage.Stage, 100);
                if (stage.Stage > 0) return (stage.Stage, GetWholePercentage(stage.Next ?? 1f));
            }
            return (0, 0);
        }

        private static int? GetMoodStage(int percentage, int satisfiedThrough)
            => percentage >= Thresholds[0] && percentage > satisfiedThrough ? GetIndex(percentage) - 1 : null;

        internal static ThoughtState GetThoughtState(Pawn pawn)
        {
            var need = pawn?.needs?.TryGetNeed<Need_Corruption>();
            int percentage = GetWholePercentage(need?.CurLevel ?? 0f);
            if (percentage < Thresholds[0]) return ThoughtState.Inactive;
            var (chainStage, satisfiedThrough) = GetMoodContext(pawn);
            int? moodStage = GetMoodStage(percentage, satisfiedThrough);
            if (!moodStage.HasValue) return ThoughtState.Inactive;

            // 锁链文案从中度开始，追加在通用四档之后。两组仍按当前恶堕档选文字。
            int textStage = chainStage > 0 && moodStage.Value > 0
                ? FirstBoundTextStage + moodStage.Value - 1 : moodStage.Value;
            return ThoughtState.ActiveAtStage(textStage);
        }

        /// <summary>把通用或锁链文案的思想索引换算为同一组实际恶堕档位。</summary>
        internal static int GetTierForThoughtStage(int textStage)
            => textStage < FirstBoundTextStage ? textStage + 1 : textStage - FirstBoundTextStage + 2;

        internal static void NotifyLevelChanged(Pawn pawn, Need_Corruption need, float before, float after)
        {
            int previousPercentage = GetWholePercentage(before);
            int currentPercentage = GetWholePercentage(after);
            if (previousPercentage == currentPercentage ||
                (previousPercentage < Thresholds[0] && currentPercentage < Thresholds[0])) return;
            var handler = pawn?.needs?.mood?.thoughts?.situational;
            if (handler == null || pawn.needs.TryGetNeed<Need_Corruption>() != need) return;
            // 同一恶堕档也可能越过本阶上界，例如三阶由显示 90% 变成 91%。
            int satisfiedThrough = GetMoodContext(pawn).SatisfiedThrough;
            if (GetMoodStage(previousPercentage, satisfiedThrough) != GetMoodStage(currentPercentage, satisfiedThrough))
                handler.Notify_SituationalThoughtsDirty();
        }

        internal static void NotifyContextChanged(Pawn pawn)
            => pawn?.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
    }

    public sealed class ThoughtWorker_SSCCorruptionLonging : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p) => SSCCorruptionStages.GetThoughtState(p);
    }

    public sealed class Thought_SituationalSSCCorruptionLonging : Thought_Situational
    {
        protected override float BaseMoodOffset
        {
            get
            {
                int chainStage = SSCIdentityUtility.IsSexSlave(pawn) ? SSCIdentityUtility.GetSexSlaveStage(pawn) : 0;
                if (chainStage == 0) return base.BaseMoodOffset;

                // 两套文案在 Def 中有不同索引。按实际恶堕档与锁链阶段的落差计算扣分。
                return (SSCCorruptionStages.GetTierForThoughtStage(CurStageIndex) - chainStage) switch
                {
                    1 => -3f,
                    2 => -6f,
                    3 => -10f,
                    _ => 0f
                };
            }
        }
    }
}
