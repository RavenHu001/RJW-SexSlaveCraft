// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 只有不足一小时的有效活动余量需要随人格保存；去重时钟不进存档。
        public int fertilityCareTicks;
        private int fertilityCareLastTick = -1;
        private object fertilityCareToken;

        /// <summary>成功活动只结算一次；同Tick的多学生／重复回调不增加双份时间。</summary>
        public void GainFertilityCare(int delta, object token)
        {
            var pawn = parent as Pawn;
            if (delta <= 0 || SSCPersonalityRuntimeUtility.IsMutating(pawn) || !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) ||
                pawn.TryGetComp<CompSexSlaveTraining>() != this || specializationType != SexSlaveSpecializationType.Fertility ||
                specializationProgress >= SpecializationCompletionProgress || FertilitySpecializationUtility.HasFinalState(pawn) ||
                !FertilitySpecializationUtility.MeetsConditions(pawn, out _)) return;
            // 同一人格共用时间游标，课堂/陪玩及重复回调只能覆盖真实经过的时间。
            // 游标不存档，读档后从第一个有效活动delta继续；不会补算离线时间。
            int now = Find.TickManager.TicksGame;
            if (!ReferenceEquals(fertilityCareToken, token)) { fertilityCareToken = token; fertilityCareLastTick = -1; }
            int valid = fertilityCareLastTick < 0 ? delta : Math.Min(delta, Math.Max(0, now - fertilityCareLastTick));
            fertilityCareLastTick = now;
            // 跨日保留不足一小时的整数余量，整小时按当前方向一次性推进，不设日限。
            long total = Math.Clamp(fertilityCareTicks, 0, 2499) + (long)valid;
            fertilityCareTicks = (int)(total % 2500);
            if (total >= 2500) FertilitySpecializationUtility.TryGainProgress(pawn, (total / 2500) * .01f);
        }

        /// <summary>只在人格成功保存并放置后清源，不操作子宫、开关或绑定。</summary>
        internal void ClearFertilityProgressAfterExtraction()
        {
            if (specializationType == SexSlaveSpecializationType.Fertility) SetSpecialization(SexSlaveSpecializationType.None);
            perTypeProgress?.Remove(SexSlaveSpecializationType.Fertility.ToString());
            fertilityCareTicks = 0;
            fertilityCareLastTick = -1;
            fertilityCareToken = null;
        }
    }
}
