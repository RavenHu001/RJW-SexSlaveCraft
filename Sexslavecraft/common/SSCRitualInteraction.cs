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
    /// <summary>为 1.7 绑定仪式在 RJW 返回的动作及部位组合中选择一个阶段动作。</summary>
    /// <remarks>调教员和性奴始终分别对应 RJW 的 Initiator 和 Recipient。Reverse 标记和接触方向沿用该动作的解析结果。</remarks>
    internal static class SSCRitualInteraction
    {
        // 阶段索引与固定保底共用六阶段顺序。此处只描述性奴侧的目标部位类别。
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

        /// <summary>准备当前阶段的 RJW 动作。筛选无结果时解析该阶段的原固定动作。</summary>
        /// <returns>已有可执行的动作 Def 时返回 true，包括固定动作的空部位解析结果。</returns>
        internal static bool TryChoose(Pawn trainer, Pawn trainee, int phase, out SexProps props)
        {
            // 每个阶段新建 SexProps，由本次选择填入动作、部位缓存和接触方向。
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
                // 固定 Def 写入后，第三方部位解析仍可能抛出异常。此时保留原动作，并为 RJW 提供空部位结果。
                try { return RitualTrainingUtility.ApplyPhaseToSexProps(props, trainer, trainee, phase); }
                catch (Exception ex) when (props.dictionaryKey != null)
                {
                    SSCLog.WarningImportant($"[SSC_RITUAL] Fixed interaction could not resolve parts, continuing without contact: {ex}");
                    ClearContact(props);
                    return true;
                }
            }

            // RJW 的 resolved setter 同步动作 Def。直接交付筛选出的整组部位，避免随后按同一 Def 再挑一组。
            props.resolved = chosen;
            props.isRevese = chosen.Interaction.HasInteractionTag(SexInteractionTag.Reverse);
            if (SSCLog.VerboseEnabled)
            {
                var trainerParts = string.Join(",", chosen.InitiatorParts.Select(part => part.Family));
                var traineeParts = string.Join(",", chosen.RecipientParts.Select(part => part.Family));
                var contacts = string.Join(",", chosen.Penetrations.Select(pair =>
                    $"{(pair.Item1.Owner == trainer ? "trainer" : "trainee")}:{pair.Item1.Family}->{pair.Item2.Family}"));
                SSCLog.Verbose($"[SSC_RITUAL] Selected phase {phase + 1}: {props.dictionaryKey.defName}, trainerParts=[{trainerParts}], traineeParts=[{traineeParts}], contacts=[{contacts}]");
            }
            return true;
        }

        /// <summary>在合格候选中优先取调教员阴部参与的组合，同优先级沿用 RJW 返回顺序。</summary>
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
            // 每条返回值是一组具体部位，双方归属和可用性必须在这一组里同时成立。
            if (candidate?.Interaction?.Def == null || candidate.Initiator != trainer || candidate.Recipient != trainee ||
                candidate.InitiatorParts == null || candidate.RecipientParts == null || candidate.Penetrations == null ||
                candidate.InitiatorParts.Count == 0 || candidate.RecipientParts.Count == 0 ||
                candidate.InitiatorParts.Any(part => !UsableBy(part, trainer)) ||
                candidate.RecipientParts.Any(part => !UsableBy(part, trainee))) return false;

            bool Target(ILewdablePart part) => part.Owner == trainee && IsTargetPart(phase, part.Family);
            bool hasTargetContact = false;
            foreach (var pair in candidate.Penetrations)
            {
                if (pair?.Item1 == null || pair.Item2 == null ||
                    !((UsableBy(pair.Item1, trainer) && UsableBy(pair.Item2, trainee)) ||
                      (UsableBy(pair.Item1, trainee) && UsableBy(pair.Item2, trainer)))) return false;
                // Item1 是接触来源。肛部和阴部阶段若有目标配对，性奴的目标部位必须处于接纳端。
                if ((phase is 4 or 5) && Target(pair.Item1)) return false;
                hasTargetContact |= Target(pair.Item1) || Target(pair.Item2);
            }
            if (hasTargetContact) return true;

            // 通用标签可枚举出多种器官，例如 MutualMasturbation 会列出多种可接纳部位。
            // 目标部位没有接触配对时，以 Def 的明确部位要求及本次参与列表共同确认。
            var requirement = candidate.Interaction.Extension?.recipientRequirement;
            return candidate.RecipientParts.Any(part => Target(part) &&
                (requirement?.genitalFamilies?.Any(family => family.AsLewdPartFamily() == part.Family) == true ||
                 requirement?.partFamilies?.Any(family => family.AsLewdPartFamily() == part.Family) == true));
        }

        private static bool UsableBy(ILewdablePart part, Pawn owner)
            => part != null && part.Owner == owner && part.BodyPart != null && LewdPartHelper.IsAvailable(owner, part);

        private static bool IsGenital(ILewdablePart part) => part.Family is LewdablePartFamily.Penis or
            LewdablePartFamily.Vagina or LewdablePartFamily.MaleOvipositor or LewdablePartFamily.FemaleOvipositor;

        /// <summary>判断解析结果是否含有参与部位或接触端点。明确指定部位的非插入动作可只有参与列表。</summary>
        internal static bool HasContact(SexInteractionResolved resolved) => resolved != null &&
            (resolved.InitiatorParts.Count != 0 || resolved.RecipientParts.Count != 0 || resolved.Penetrations.Count != 0);

        /// <summary>保留已选动作与角色，以空列表标记本阶段无可用的部位结果。</summary>
        internal static void ClearContact(SexProps props)
        {
            // RJW 读取 null 缓存时会尝试重新解析。显式空结果可让固定动作沿原场景流程执行。
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

    /// <summary>为阶段 Job 保存 RJW 未序列化的原选部位及有序接触端点。</summary>
    /// <remarks>档案使用原 Pawn、身体记录和器官引用，供同一动作读档后恢复。仪式开发奖励仍由整场结算负责。</remarks>
    public sealed class SSCRitualInteractionParts : IExposable
    {
        private InteractionDef interaction;
        // 部位表先存发起方和接收方列表，再补入只出现在接触配对中的端点。
        // contacts 每两个索引是一条来源到接纳端的配对，顺序沿用 RJW 的 Penetrations。
        private List<SSCOrdinarySexPartsSnapshot.Part> participants;
        private List<int> contacts;
        private int initiatorCount;
        private int recipientCount;

        /// <summary>捕获当前已解析的组合。空结果或引用无法完整记录时返回 null。</summary>
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
            // 复用已保存的同一身体引用。部分 RJW 动作的接触端点会超出双方原始参与列表。
            var descriptor = SSCOrdinarySexPartsUtility.DescribePart(endpoint);
            int index = descriptor == null ? -1 : participants.FindIndex(descriptor.Same);
            if (index < 0)
            {
                index = participants.Count;
                participants.Add(descriptor);
            }
            contacts.Add(index);
        }

        /// <summary>在当前双方身体上找回原部位，并在所有引用和端点均有效后恢复整组解析结果。</summary>
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
                // 身体或器官替换后，即使同类别仍可使用，也须由调用方按空接触继续原动作。
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
