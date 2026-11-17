// AI 辅助生成：奴性爆发的准入、恶堕刷新和只读警报共用实际恶堕值。
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal static class SSCSlaveBreakdown
    {
        internal const string DefName = "SSC_SlaveBreakdown";
        internal const float TriggerLevel = 0.95f;
        internal const float RearmBelowLevel = 0.94f;
        internal const int NeedIntervalTicks = 150;
        internal const int DurationTicks = 6 * GenDate.TicksPerHour;
        internal const int CooldownTicks = GenDate.TicksPerDay;

        internal static MentalStateDef StateDef => DefDatabase<MentalStateDef>.GetNamedSilentFail(DefName);
        internal static bool Enabled => SSCMod.settings?.EnableSlaveBreakdown == true;
        internal static int Now => Current.Game?.tickManager?.TicksGame ?? -1;
        internal static Need_Corruption GetNeed(Pawn pawn) => pawn?.needs?.TryGetNeed<Need_Corruption>();

        // IsColonistPlayerControlled 会在发作期间返回 false。直接检查归属，排除临时借调的殖民者。
        internal static bool IsEligiblePawn(Pawn pawn)
            => pawn?.Spawned == true && !pawn.Dead && !pawn.Discarded && !pawn.Suspended &&
               pawn.RaceProps.Humanlike && !pawn.IsSubhuman && !pawn.IsPrisoner &&
               ((pawn.IsColonist && pawn.HostFaction == null) || pawn.IsSlaveOfColony) &&
               (SSCIdentityUtility.GetIdentity(pawn) is PawnIdentity.Unset or PawnIdentity.Slave) &&
               !Utils.HasChainHediff(pawn) && !SSCPersonalityRuntimeUtility.IsMutating(pawn);

        // 满值增长可从任意较低值直接跨入，捕获事件时这里只检查身份与冷却。
        internal static bool CanConsider(Pawn pawn, Need_Corruption need)
            => Enabled && Now >= 0 && Scribe.mode == LoadSaveMode.Inactive && need != null &&
               IsEligiblePawn(pawn) && GetNeed(pawn) == need &&
               pawn.mindState?.mentalStateHandler is MentalStateHandler handler &&
               handler.CurState is not MentalState_SSCSlaveBreakdown &&
               pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp &&
               Now >= comp.SlaveBreakdownCooldownUntil;

        internal static bool TemporarilyBlocked(Pawn pawn) => pawn.Downed || !pawn.Awake() || pawn.InMentalState;

        internal static bool BlockedByVanilla(Pawn pawn)
            => pawn.mindState.mentalStateHandler.MentalBreaksBlocked() ||
               (pawn.IsMutant && pawn.mutant.Def.preventsMentalBreaks) ||
               (TutorSystem.TutorialMode && pawn.Faction == Faction.OfPlayer);

        internal static bool CanStart(Pawn pawn)
        {
            Need_Corruption need = GetNeed(pawn);
            return need?.CurLevel >= TriggerLevel && CanConsider(pawn, need) &&
                   !TemporarilyBlocked(pawn) && !BlockedByVanilla(pawn);
        }

        /// <summary>在衰减前按原始值掷骰。启动可经原版中断任务，之后只允许原载体继续刷新。</summary>
        internal static bool RefreshBeforeDecay(Pawn pawn, Need_Corruption need)
        {
            if (!(need.CurLevel >= TriggerLevel && need.CurLevel < need.MaxLevel)) return true;
            var runtime = SSCSlaveBreakdownGameComponent.Current;
            if (runtime == null || runtime.HasFullRequest(pawn) || SSCFullCorruptionImprint.IsSettling(pawn, need) ||
                StateDef?.Worker.StateCanOccur(pawn) != true ||
                !Rand.MTBEventOccurs(1.5f, GenDate.TicksPerDay, NeedIntervalTicks)) return true;

            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            runtime.TryStart(pawn);
            return !pawn.Dead && !pawn.Destroyed && !pawn.Discarded &&
                   GetNeed(pawn) == need && SSCPersonalityRuntimeUtility.IsCurrent(pawn, token);
        }

        /// <summary>观察真实的数值变化，不把读档或替换过程的临时清零当成回落。</summary>
        internal static void NotifyLevelChanged(Pawn pawn, Need_Corruption need, float before, float after)
        {
            bool cancel = before >= TriggerLevel && after < TriggerLevel;
            bool rearm = (before < RearmBelowLevel) != (after < RearmBelowLevel);
            if ((!cancel && !rearm) || Scribe.mode != LoadSaveMode.Inactive || GetNeed(pawn) != need ||
                SSCPersonalityRuntimeUtility.IsMutating(pawn)) return;
            if (cancel) SSCSlaveBreakdownGameComponent.Current?.CancelFullRequest(pawn);
            if (rearm && pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp)
                comp.SlaveBreakdownFullTriggerUsed = false;
        }

        internal static bool IsAtRisk(Pawn pawn)
        {
            Need_Corruption need = GetNeed(pawn);
            if (!(need?.CurLevel >= TriggerLevel) || !CanConsider(pawn, need) || BlockedByVanilla(pawn))
                return false;
            if (SSCSlaveBreakdownGameComponent.Current?.HasFullRequest(pawn) == true) return true;
            if (StateDef?.Worker.StateCanOccur(pawn) != true) return false;
            if (need.CurLevel < need.MaxLevel) return true;

            // 持续满值且没有跨越待办时，只剩原版低心情的发作机会。
            MentalBreaker breaker = pawn.mindState.mentalBreaker;
            return pawn.needs.mood != null && DebugSettings.enableRandomMentalStates && breaker.CanDoRandomMentalBreaks &&
                   !QuestUtility.AnyQuestDisablesRandomMoodCausedMentalBreaksFor(pawn) &&
                   (breaker.BreakMinorIsImminent || breaker.BreakMajorIsImminent || breaker.BreakExtremeIsImminent);
        }
    }

    public sealed class Alert_SSCSlaveBreakdownRisk : Alert
    {
        private readonly List<Pawn> culprits = new();

        public Alert_SSCSlaveBreakdownRisk() => defaultPriority = AlertPriority.High;

        public override string GetLabel() => "SSC_SlaveBreakdown_RiskLabel".Translate();

        public override TaggedString GetExplanation()
            => "SSC_SlaveBreakdown_RiskExplanation".Translate(string.Join("\n", culprits.Select(pawn => pawn.LabelShortCap)));

        public override AlertReport GetReport()
        {
            culprits.Clear();
            if (!SSCSlaveBreakdown.Enabled || SSCSlaveBreakdownGameComponent.Current == null) return false;
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                    if (SSCSlaveBreakdown.IsAtRisk(pawn)) culprits.Add(pawn);
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
