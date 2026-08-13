using System.Linq;
using Verse;

// EN: Pure SSC owner/bus protection rules shared by reservation-time and start-time Harmony adapters.
// CN: 这是预约阶段与启动阶段 Harmony 入口共用的 SSC 主人/公交车保护规则。
namespace SexSlaveCraft
{
    public sealed class SSCSexInteractionDecision
    {
        public bool Allowed;
        public string Reason;
        public Pawn Aggressor;
        public Pawn Victim;
        public bool IsRape;
        public Hediff_ChainOfSexSlave AggressorChain;
        public Hediff_ChainOfSexSlave VictimChain;
        public bool IsAggressorBus;
        public bool IsVictimBus;

        public static SSCSexInteractionDecision Allow(string reason, Pawn aggressor, Pawn victim, bool isRape)
        {
            return Create(true, reason, aggressor, victim, isRape);
        }

        public static SSCSexInteractionDecision Block(string reason, Pawn aggressor, Pawn victim, bool isRape)
        {
            return Create(false, reason, aggressor, victim, isRape);
        }

        private static SSCSexInteractionDecision Create(bool allowed, string reason, Pawn aggressor, Pawn victim, bool isRape)
        {
            return new SSCSexInteractionDecision
            {
                Allowed = allowed,
                Reason = reason,
                Aggressor = aggressor,
                Victim = victim,
                IsRape = isRape,
                AggressorChain = SSCBondUtility.GetChain(aggressor),
                VictimChain = SSCBondUtility.GetChain(victim),
                IsAggressorBus = BusSpecializationUtility.HasAnyBusState(aggressor),
                IsVictimBus = BusSpecializationUtility.HasAnyBusState(victim)
            };
        }
    }

    public static class SSCSexInteractionPolicy
    {
        public static SSCSexInteractionDecision Evaluate(Pawn aggressor, Pawn victim, bool isRape)
        {
            if (aggressor == null || victim == null || aggressor.health == null || victim.health == null)
                return SSCSexInteractionDecision.Allow("null_safety_bypass", aggressor, victim, isRape);

            SSCSexInteractionDecision decision = SSCSexInteractionDecision.Allow("pass_all_rules", aggressor, victim, isRape);
            Hediff_ChainOfSexSlave aggressorChain = decision.AggressorChain;
            Hediff_ChainOfSexSlave victimChain = decision.VictimChain;

            if (aggressorChain == null && victimChain == null)
                return WithReason(decision, true, "no_chain_on_both");

            if (decision.IsVictimBus)
                return WithReason(decision, true, "victim_is_bus");

            if (decision.IsAggressorBus)
            {
                bool blockRape = isRape && (SSCMod.settings?.protectBusAggressorRape ?? true);
                return WithReason(decision, !blockRape,
                    blockRape ? "aggressor_bus_cannot_rape" : "aggressor_bus_non_rape");
            }

            if (isRape)
            {
                if (aggressorChain != null && (SSCMod.settings?.protectChainedAggressorRape ?? true))
                    return WithReason(decision, false, "aggressor_chain_cannot_rape");

                if (victimChain != null)
                {
                    if (SSCMod.settings?.allowSexSlaveRape ?? false)
                    {
                        bool hasProtectionGear = victim.apparel != null &&
                            victim.apparel.WornApparel.Any(apparel => apparel.GetComp<CompSSRapeCheck>() != null);
                        return WithReason(decision, !hasProtectionGear,
                            hasProtectionGear ? "victim_protection_gear" : "setting_allow_rape_and_no_protection");
                    }

                    bool ownerMatches = victimChain.LinkedPawn == aggressor;
                    return WithReason(decision, ownerMatches,
                        ownerMatches ? "strict_mode_owner_match" : "strict_mode_owner_mismatch");
                }
            }
            else if (SSCMod.settings?.protectNonRapeOwnerOnly ?? true)
            {
                if (aggressorChain != null && aggressorChain.LinkedPawn != victim && !AllowsNonOwnerInteraction(aggressor))
                    return WithReason(decision, false, "non_rape_aggressor_owner_mismatch");

                if (victimChain != null && victimChain.LinkedPawn != aggressor && !AllowsNonOwnerInteraction(victim))
                    return WithReason(decision, false, "non_rape_victim_owner_mismatch");
            }

            return decision;
        }

        private static bool AllowsNonOwnerInteraction(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && comp.AllowsOthersForTrainingOrSex;
        }

        private static SSCSexInteractionDecision WithReason(
            SSCSexInteractionDecision decision,
            bool allowed,
            string reason)
        {
            decision.Allowed = allowed;
            decision.Reason = reason;
            return decision;
        }
    }
}
