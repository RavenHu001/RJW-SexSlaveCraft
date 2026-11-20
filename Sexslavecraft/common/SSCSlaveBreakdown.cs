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
        // 这个基础检查供启动和续行共用。完整的入场条件还须检查锁链和当前阶段。
        internal static bool IsEligibleSubject(Pawn pawn)
            => pawn?.Spawned == true && !pawn.Dead && !pawn.Discarded && !pawn.Suspended &&
               pawn.RaceProps.Humanlike && !pawn.IsSubhuman && !pawn.IsPrisoner &&
               ((pawn.IsColonist && pawn.HostFaction == null) || pawn.IsSlaveOfColony) &&
               (SSCIdentityUtility.GetIdentity(pawn) is PawnIdentity.Unset or PawnIdentity.Slave) &&
               !SSCPersonalityRuntimeUtility.IsMutating(pawn);

        /// <summary>两种恶堕触发、风险警报和开发者启动共用的角色准入。</summary>
        /// <remarks>有链角色须具备真实的主从身份，且当前阶段还有下一阶。主人同图与可行动性留给游荡选址和调教准入。</remarks>
        internal static bool IsEligiblePawn(Pawn pawn)
        {
            if (!IsEligibleSubject(pawn)) return false;
            if (!Utils.HasChainHediff(pawn)) return true;
            var chain = SSCBondUtility.GetChain(pawn);
            return chain != null && MatchesBinding(pawn, chain, chain.LinkedPawn) &&
                TrainingOutcomeUtility.GetSexSlaveProgressStage(chain).Next.HasValue;
        }

        // 比对启动时捕获的锁链实例与目标。换链即使绑回同一个主人也属于新关系，
        // 同一条锁链换主同样撤权。无链待办若在结算过程中首次建链也会失效。
        internal static bool MatchesBinding(Pawn pawn, Hediff_ChainOfSexSlave chain, Pawn owner)
            => chain == null ? !Utils.HasChainHediff(pawn) :
                owner != null && owner != pawn && !owner.Dead && !owner.Destroyed && !owner.Discarded &&
                SSCIdentityUtility.IsMaster(owner) && SSCIdentityUtility.IsSexSlave(pawn) &&
                SSCBondUtility.GetChain(pawn) == chain && chain.LinkedPawn == owner;

        // 满值跨越可以从低于随机门槛的值开始。这里先核对开关、载体、角色资格与冷却，
        // 数值范围和暂时阻塞再由各入口检查，方便满值请求等待小人醒来或结束其他精神状态。
        internal static bool CanConsider(Pawn pawn, Need_Corruption need)
            => Enabled && Now >= 0 && Scribe.mode == LoadSaveMode.Inactive && need != null &&
               IsEligiblePawn(pawn) && GetNeed(pawn) == need &&
               pawn.mindState?.mentalStateHandler is MentalStateHandler handler &&
               handler.CurState is not MentalState_SSCSlaveBreakdown &&
               pawn.TryGetComp<CompSexSlaveTraining>() is CompSexSlaveTraining comp &&
               Now >= comp.SlaveBreakdownCooldownUntil;

        // 这些状态只推迟有效的满值待办。随机入口下次需求刷新时重新判断。
        internal static bool TemporarilyBlocked(Pawn pawn) => pawn.Downed || !pawn.Awake() || pawn.InMentalState;

        // 直接启动仍遵守原版对精神状态的禁用条件，包括教程和阻止精神崩溃的变异体。
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

        /// <summary>在衰减前用实际恶堕掷骰。启动可中断原版任务，之后只允许原需求与人格继续刷新。</summary>
        internal static bool RefreshBeforeDecay(Pawn pawn, Need_Corruption need)
        {
            // 95% 包含在随机区间内。满值由实际跨越触发，满值待办及印记结算优先处理。
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

        /// <summary>只读查询两条恶堕入口。查看警报不会登记满值待办，也不会消耗随机判定。</summary>
        internal static bool IsAtRisk(Pawn pawn)
        {
            Need_Corruption need = GetNeed(pawn);
            if (!(need?.CurLevel >= TriggerLevel) || !CanConsider(pawn, need) || BlockedByVanilla(pawn))
                return false;
            var runtime = SSCSlaveBreakdownGameComponent.Current;
            if (runtime == null) return false;
            // 短期满值待办可在印记重试、睡眠或其他精神状态结束后继续，等待期间仍算有风险。
            if (runtime.HasFullRequest(pawn)) return true;
            // 没有待办时只看实际随机资格。持续满值和低心情都不会产生这条警报。
            return need.CurLevel < need.MaxLevel && !SSCFullCorruptionImprint.IsSettling(pawn, need) &&
                   StateDef?.Worker.StateCanOccur(pawn) == true;
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
