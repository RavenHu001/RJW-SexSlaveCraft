using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>绑定仪式只让观众提供助兴加成，出席门槛按本场实际经过的 tick 计算。</summary>
    public class RitualOutcomeComp_BindingSpectatorCount : RitualOutcomeComp_ParticipantCount
    {
        private static readonly AccessTools.FieldRef<LordJob_Ritual, int> ElapsedTicks
            = AccessTools.FieldRefAccess<LordJob_Ritual, int>("ticksPassed");

        // 原版只在有时长结束触发器的阶段增加 TicksPassedWithProgress；SSC 的两个
        // 阶段由送达/工作结束触发，所以该进度始终为零。ticksPassed 则每帧递增且随 Lord 存档。
        public static int AttendanceDurationTicks(LordJob_Ritual ritual)
            => ritual == null ? 0 : Math.Max(0, ElapsedTicks(ritual));

        // 保留原版 MakeData 和出席记录的存档格式，兼容旧档中的观众记录。
        // 显式排除两主角，避免强制角色与观众列表重叠时被原版 RoleForPawn 优先视为观众。
        private static bool IsSpectator(RitualRoleAssignments assignments, Pawn pawn)
            => pawn?.RaceProps?.Humanlike == true && assignments != null &&
                pawn != assignments.FirstAssignedPawn("master") && pawn != assignments.FirstAssignedPawn("slave");

        /// <summary>观众抵达本场分配的观看站位，也应视为出席，不只依赖通用聚会区域。</summary>
        private static bool AtSpectatorPosition(Pawn pawn)
        {
            PawnDuty duty = pawn.mindState?.duty;
            return duty?.def == DutyDefOf.Spectate && duty.focus.IsValid &&
                pawn.Position.InHorDistOf(duty.focus.Cell, 1f);
        }

        public override void Tick(LordJob_Ritual ritual, RitualOutcomeComp_Data data, float progressPerTick)
        {
            var presence = data as RitualOutcomeComp_DataThingPresence;
            if (presence == null || ritual?.assignments == null || progressPerTick <= 0f) return;

            // 只采集实际属于本场 Lord 的观众。未到场、离场、倒地以及两主角不积累时间。
            foreach (Pawn pawn in ritual.PawnsToCountTowardsPresence)
            {
                if (!IsSpectator(ritual.assignments, pawn) || !pawn.Spawned || pawn.Dead || pawn.Downed ||
                    pawn.Map != ritual.Map || !ritual.assignments.PawnSpectating(pawn)) continue;
                if (!GatheringsUtility.InGatheringArea(pawn.Position, ritual.Spot, pawn.MapHeld) &&
                    !AtSpectatorPosition(pawn)) continue;

                presence.presentForTicks.TryGetValue(pawn, out float ticks);
                // 分子、分母都用实际经过的 tick，不把阶段进度速度当成观看时间。
                presence.presentForTicks[pawn] = ticks + 1f;
            }
        }

        public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            var presence = data as RitualOutcomeComp_DataThingPresence;
            int duration = AttendanceDurationTicks(ritual);
            if (presence == null || duration <= 0) return 0f;

            // durationTicks 是等待六阶段完成的超长占位值，不能作为观众出席门槛。
            float minimumPresence = duration / 2f;
            int count = 0;
            foreach (var entry in presence.presentForTicks)
                if (entry.Key is Pawn pawn && IsSpectator(ritual.assignments, pawn) &&
                    entry.Value >= minimumPresence)
                    count++;

            return curve == null ? count : Math.Min(count, MaxValue);
        }

        public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget,
            RitualObligation obligation, RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
        {
            int count = 0;
            if (assignments != null)
                foreach (Pawn pawn in assignments.SpectatorsForReading)
                    if (IsSpectator(assignments, pawn)) count++;

            float quality = curve.Evaluate(count);
            return new QualityFactor
            {
                label = label.CapitalizeFirst(),
                count = count + " / " + Math.Max(MaxValue, count),
                qualityChange = ExpectedOffsetDesc(true, quality),
                quality = quality,
                positive = true,
                priority = 4f
            };
        }
    }
}
