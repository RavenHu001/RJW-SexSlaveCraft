// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCRitualInteraction
    {
        internal static bool IsTargetPart(int phase, LewdablePartFamily family) => phase switch
        {
            0 => family == LewdablePartFamily.Hand,
            1 => family == LewdablePartFamily.Foot,
            2 => family is LewdablePartFamily.Mouth or LewdablePartFamily.Tongue or LewdablePartFamily.Beak,
            3 => family == LewdablePartFamily.Breasts,
            4 => family == LewdablePartFamily.Anus,
            5 => family == LewdablePartFamily.Vagina,
            _ => false
        };

        internal static bool TryChoose(Pawn trainer, Pawn trainee, int phase, out SexProps props)
        {
            props = new SexProps(trainer, trainee);
            SexInteractionResolved chosen;
            try
            {
                chosen = Choose(SexInteractionFinder.FindInteractions(props), trainer, trainee, phase);
            }
            catch (Exception ex)
            {
                SSCLog.WarningImportant($"[SSC_RITUAL] Stage selection failed, using fixed interaction: {ex}");
                chosen = null;
            }
            if (chosen == null)
            {
                try { return RitualTrainingUtility.ApplyPhaseToSexProps(props, trainer, trainee, phase); }
                catch (Exception ex) when (props.dictionaryKey != null)
                {
                    SSCLog.WarningImportant($"[SSC_RITUAL] Fixed interaction could not resolve parts, continuing without contact: {ex}");
                    ClearContact(props);
                    return true;
                }
            }

            props.resolved = chosen;
            props.isRevese = chosen.Interaction.HasInteractionTag(SexInteractionTag.Reverse);
            return true;
        }

        internal static SexInteractionResolved Choose(IEnumerable<SexInteractionResolved> candidates, Pawn trainer, Pawn trainee, int phase)
        {
            SexInteractionResolved firstMatch = null;
            foreach (var candidate in candidates)
            {
                if (!Matches(candidate, trainer, trainee, phase)) continue;
                if (candidate.InitiatorParts.Any(IsGenital) || candidate.Penetrations.Any(pair =>
                    (pair.Item1.Owner == trainer && IsGenital(pair.Item1)) ||
                    (pair.Item2.Owner == trainer && IsGenital(pair.Item2)))) return candidate;
                firstMatch ??= candidate;
            }
            return firstMatch;
        }

        private static bool Matches(SexInteractionResolved candidate, Pawn trainer, Pawn trainee, int phase)
        {
            if (candidate?.Interaction?.Def == null || candidate.Initiator != trainer || candidate.Recipient != trainee ||
                candidate.InitiatorParts == null || candidate.RecipientParts == null || candidate.Penetrations == null ||
                candidate.InitiatorParts.Count == 0 || candidate.RecipientParts.Count == 0 ||
                candidate.InitiatorParts.Any(part => !UsableBy(part, trainer)) ||
                candidate.RecipientParts.Any(part => !UsableBy(part, trainee))) return false;

            bool Target(ILewdablePart part) => part.Owner == trainee && IsTargetPart(phase, part.Family);
            bool participates = candidate.RecipientParts.Any(Target);
            foreach (var pair in candidate.Penetrations)
            {
                if (pair?.Item1 == null || pair.Item2 == null ||
                    !((UsableBy(pair.Item1, trainer) && UsableBy(pair.Item2, trainee)) ||
                      (UsableBy(pair.Item1, trainee) && UsableBy(pair.Item2, trainer)))) return false;
                if ((phase is 4 or 5) && Target(pair.Item1)) return false;
                participates |= Target(pair.Item1) || Target(pair.Item2);
            }
            return participates;
        }

        private static bool UsableBy(ILewdablePart part, Pawn owner)
            => part != null && part.Owner == owner && part.BodyPart != null && LewdPartHelper.IsAvailable(owner, part);

        private static bool IsGenital(ILewdablePart part) => part.Family is LewdablePartFamily.Penis or
            LewdablePartFamily.Vagina or LewdablePartFamily.MaleOvipositor or LewdablePartFamily.FemaleOvipositor;

        internal static bool HasContact(SexInteractionResolved resolved) => resolved != null &&
            (resolved.InitiatorParts.Count != 0 || resolved.RecipientParts.Count != 0 || resolved.Penetrations.Count != 0);

        internal static void ClearContact(SexProps props)
        {
            props.resolved = new SexInteractionResolved
            {
                Initiator = props.initiator,
                Recipient = props.recipient,
                Interaction = props.interaction,
                InitiatorParts = new List<ILewdablePart>(),
                RecipientParts = new List<ILewdablePart>(),
                Penetrations = new List<Tuple<ILewdablePart, ILewdablePart>>()
            };
        }
    }

    /// <summary>RJW saves the Def without its resolved parts. Preserve the selected parts and ordered contacts.</summary>
    public sealed class SSCRitualInteractionParts : IExposable
    {
        private InteractionDef interaction;
        private List<SSCOrdinarySexPartsSnapshot.Part> participants;
        private List<int> contacts;
        private int initiatorCount;
        private int recipientCount;

        internal static SSCRitualInteractionParts Capture(SexInteractionResolved resolved)
        {
            if (!SSCRitualInteraction.HasContact(resolved)) return null;
            var parts = new SSCRitualInteractionParts
            {
                interaction = resolved.Interaction?.Def,
                participants = resolved.InitiatorParts.Concat(resolved.RecipientParts)
                    .Select(SSCOrdinarySexPartsUtility.DescribePart).ToList(),
                initiatorCount = resolved.InitiatorParts.Count,
                recipientCount = resolved.RecipientParts.Count,
                contacts = new List<int>()
            };
            foreach (var pair in resolved.Penetrations)
            {
                parts.AddContactEndpoint(pair.Item1);
                parts.AddContactEndpoint(pair.Item2);
            }
            return parts.participants.Contains(null) ? null : parts;
        }

        private void AddContactEndpoint(ILewdablePart endpoint)
        {
            var descriptor = SSCOrdinarySexPartsUtility.DescribePart(endpoint);
            int index = descriptor == null ? -1 : participants.FindIndex(descriptor.Same);
            if (index < 0)
            {
                index = participants.Count;
                participants.Add(descriptor);
            }
            contacts.Add(index);
        }

        internal bool Restore(SexProps props)
        {
            if (interaction == null || props?.dictionaryKey != interaction || props.initiator == null || props.recipient == null || participants == null ||
                initiatorCount <= 0 || recipientCount <= 0 || initiatorCount > participants.Count - recipientCount ||
                contacts == null || contacts.Count % 2 != 0 || contacts.Any(i => i < 0 || i >= participants.Count)) return false;

            var initiatorParts = new LewdParts(props.initiator, populateAll: true).AllParts;
            var recipientParts = new LewdParts(props.recipient, populateAll: true).AllParts;
            var restored = new List<ILewdablePart>(participants.Count);
            foreach (var descriptor in participants)
            {
                if (descriptor?.IsAttached() != true || (descriptor.Owner != props.initiator && descriptor.Owner != props.recipient)) return false;
                var available = descriptor.Owner == props.initiator ? initiatorParts : recipientParts;
                var part = available.Find(candidate => descriptor.Same(SSCOrdinarySexPartsUtility.DescribePart(candidate)) &&
                    LewdPartHelper.IsAvailable(descriptor.Owner, candidate));
                if (part == null) return false;
                restored.Add(part);
            }

            var initiator = restored.GetRange(0, initiatorCount);
            var recipient = restored.GetRange(initiatorCount, recipientCount);
            if (initiator.Any(part => part.Owner != props.initiator) || recipient.Any(part => part.Owner != props.recipient)) return false;
            var penetrations = new List<Tuple<ILewdablePart, ILewdablePart>>();
            for (int i = 0; i < contacts.Count; i += 2)
            {
                var from = restored[contacts[i]];
                var to = restored[contacts[i + 1]];
                if (from.Owner == to.Owner) return false;
                penetrations.Add(Tuple.Create(from, to));
            }
            props.resolved = new SexInteractionResolved
            {
                Initiator = props.initiator, Recipient = props.recipient, Interaction = props.interaction,
                InitiatorParts = initiator, RecipientParts = recipient, Penetrations = penetrations
            };
            return true;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref interaction, "interaction");
            Scribe_Collections.Look(ref participants, "participants", LookMode.Deep);
            Scribe_Collections.Look(ref contacts, "contacts", LookMode.Value);
            Scribe_Values.Look(ref initiatorCount, "initiatorCount");
            Scribe_Values.Look(ref recipientCount, "recipientCount");
        }
    }
}
