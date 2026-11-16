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

        private static int GetSatisfiedThrough(Pawn pawn)
        {
            if (SSCIdentityUtility.IsMaster(pawn)) return 100;
            if (SSCIdentityUtility.IsSexSlave(pawn))
            {
                var stage = TrainingOutcomeUtility.GetChainStage(SSCBondUtility.GetChain(pawn));
                // 按显示的整数判定，三阶显示 90% 时仍满足。无实际阶段时沿用普通角色的当前档位。
                if (stage.Stage >= 4) return 100;
                if (stage.Stage > 0) return GetWholePercentage(stage.Next ?? 1f);
            }
            return 0;
        }

        private static int? GetMoodStage(int percentage, int satisfiedThrough)
            => percentage >= Thresholds[0] && percentage > satisfiedThrough ? GetIndex(percentage) - 1 : null;

        internal static ThoughtState GetThoughtState(Pawn pawn)
        {
            var need = pawn?.needs?.TryGetNeed<Need_Corruption>();
            int percentage = GetWholePercentage(need?.CurLevel ?? 0f);
            if (percentage < Thresholds[0]) return ThoughtState.Inactive;
            int? moodStage = GetMoodStage(percentage, GetSatisfiedThrough(pawn));
            return moodStage.HasValue ? ThoughtState.ActiveAtStage(moodStage.Value) : ThoughtState.Inactive;
        }

        internal static void NotifyLevelChanged(Pawn pawn, Need_Corruption need, float before, float after)
        {
            int previousPercentage = GetWholePercentage(before);
            int currentPercentage = GetWholePercentage(after);
            if (previousPercentage == currentPercentage ||
                (previousPercentage < Thresholds[0] && currentPercentage < Thresholds[0])) return;
            var handler = pawn?.needs?.mood?.thoughts?.situational;
            if (handler == null || pawn.needs.TryGetNeed<Need_Corruption>() != need) return;
            // 同一恶堕档也可能越过本阶上界，例如三阶由显示 90% 变成 91%。
            int satisfiedThrough = GetSatisfiedThrough(pawn);
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
}
