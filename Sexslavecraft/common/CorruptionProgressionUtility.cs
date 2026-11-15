using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

// EN: This file resolves corruption progression for sex slaves.
// EN: It adds bridle / chain states, upgrades ritual-only traits, and handles Binding Ritual enslavement rolls.
// CN: 这个文件负责处理性奴恶堕成长过程中的阶段变化。
// CN: 它会补上缰绳 / 锁链状态、推进仪式专属特质，并处理绑定仪式里的转奴判定。
namespace SexSlaveCraft
{
    public static class CorruptionProgressionUtility
    {
        public static string ProcessCorruptionProgression(Pawn master, Pawn sexSlave, bool isRitual, float score = 0f)
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            => ProcessCorruptionProgression(master, sexSlave, isRitual, score,
                isRitual ? new RitualChainProgress(master, sexSlave, null) : null);

        internal static string ProcessCorruptionProgression(Pawn master, Pawn sexSlave, bool isRitual, float score,
            RitualChainProgress progress)
#endif
        {
            Need_Corruption need = sexSlave.needs.TryGetNeed<Need_Corruption>();
            if (need == null) return null;
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            if (isRitual && !progress.CanAdvance()) return null;
#endif

            // EN: Step 1: read current corruption and collect every newly triggered stage-change message.
            // CN: 步骤 1：读取当前恶堕值，并收集这次新触发的全部阶段变化文本。
            float corruption = need.CurLevel;
            List<string> changes = new List<string>();

#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            Hediff_ChainOfSexSlave boundChain = ProcessSharedCorruptionProgression(master, sexSlave, isRitual, changes, corruption, progress);
#else
            ProcessSharedCorruptionProgression(master, sexSlave, isRitual, changes, corruption);
#endif
            if (isRitual)
            {
                // EN: The Binding Ritual gets extra progression rules beyond normal daily training.
                // CN: 绑定仪式除了日常调教共享阶段外，还会额外处理仪式专属变化。
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                if (progress.CanContinueAfterBinding(boundChain))
                {
                    ProcessRitualEnslavement(master, sexSlave, score, changes, need.CurLevel, progress, boundChain);
                    progress.Apply(changes);
                }
#else
                ProcessRitualEnslavement(master, sexSlave, score, changes, corruption);
                ProcessRitualCorruptionProgression(sexSlave, changes, corruption);
#endif
            }

            return changes.Count > 0 ? string.Join("\n", changes.ToArray()) : null;
        }

        public static ThoughtDef GetRitualEuphoriaThought(ThoughtDef ritualOutcomeMemory)
        {
            // EN: Map vanilla-style ritual outcome quality to SSC's own ritual euphoria memory ladder.
            // CN: 把偏原版风格的仪式结果档位，映射成 SSC 自己的“仪式欣快”记忆阶梯。
            if (ritualOutcomeMemory == SSCDefOf.SSC_Ritual_Terrible) return SSCDefOf.SSC_Ritual_Euphoria_1;
            if (ritualOutcomeMemory == SSCDefOf.SSC_Ritual_Boring) return SSCDefOf.SSC_Ritual_Euphoria_2;
            if (ritualOutcomeMemory == SSCDefOf.SSC_Ritual_Good) return SSCDefOf.SSC_Ritual_Euphoria_3;
            if (ritualOutcomeMemory == SSCDefOf.SSC_Ritual_Best) return SSCDefOf.SSC_Ritual_Euphoria_4;
            return SSCDefOf.SSC_Ritual_Euphoria_3;
        }

        private static Hediff_ChainOfSexSlave ProcessSharedCorruptionProgression(Pawn master, Pawn sexSlave,
            bool isRitual, List<string> changes, float corruption
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            , RitualChainProgress progress
#endif
        )
        {
            if (corruption < 0.1f) return null;
            if (!isRitual)
            {
                SSCBondUtility.Bind(master, sexSlave);
                return null;
            }

            bool hadBridleTarget = SSCBondUtility.GetBridle(master)?.targets.Contains(sexSlave) == true;
            bool hadChain = SSCBondUtility.GetChain(sexSlave) != null;
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            if (!SSCBondUtility.BindForRitual(master, sexSlave, progress.CanWriteBinding, out var boundChain) ||
                !progress.CanContinueAfterBinding(boundChain)) return null;
#else
            if (!SSCBondUtility.Bind(master, sexSlave)) return null;
            Hediff_ChainOfSexSlave boundChain = null;
#endif

            if (!hadBridleTarget)
                changes.Add(Strings.Bond_BridleAdded(master.LabelShort, sexSlave.LabelShort));
            if (!hadChain)
                changes.Add(Strings.Bond_ChainAdded(sexSlave.LabelShort, master.LabelShort));
            return boundChain;
        }

        private static void ProcessRitualEnslavement(Pawn master, Pawn sexSlave, float score, List<string> changes, float corruption
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            , RitualChainProgress progress, Hediff_ChainOfSexSlave boundChain
#endif
        )
        {
            // EN: Ritual-only change: after enough corruption, the Binding Ritual may convert a prisoner or colonist into a slave.
            // CN: 仪式专属变化：恶堕足够高后，绑定仪式可以尝试把囚犯或殖民者正式转化为奴隶。
            bool canAttemptEnslave = !sexSlave.IsSlave && (sexSlave.IsPrisonerOfColony || sexSlave.IsColonist);
            string currentWillText = sexSlave?.guest != null ? sexSlave.guest.will.ToString("F2") : "null";
            SSCLog.Important($"[SSC 仪式转奴] 候选检查: target={sexSlave?.LabelShort ?? "null"}, 当前恶堕={corruption:F3}, score={score:F2}, 可尝试={canAttemptEnslave}, prisoner={sexSlave?.IsPrisonerOfColony ?? false}, colonist={sexSlave?.IsColonist ?? false}, slave={sexSlave?.IsSlave ?? false}, guestNull={(sexSlave?.guest == null)}, will={currentWillText}");

            bool ritualEnslavementEnabled = SSCMod.settings == null || SSCMod.settings.enableRitualEnslavement;
            if (corruption >= 0.2f && canAttemptEnslave && ritualEnslavementEnabled)
            {
                // EN: Use the same score + will + corruption model as the rest of SSC's conversion logic.
                // CN: 这里复用 SSC 其余转化逻辑使用的“分数 + 意志 + 恶堕”模型。
                // EN: Use the shared conversion formula so Binding Ritual slave conversion stays aligned with the rest of SSC.
                // CN: 这里复用共享转化公式，保证绑定仪式的转奴逻辑和 SSC 其他系统保持一致。
                float enslaveChance = WillReductionUtility.CalculateEnslaveChance(sexSlave, score, corruption, true, out string chanceDetail);
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                if (!progress.CanContinueAfterBinding(boundChain)) return;
#endif
                changes.Add(chanceDetail);
                currentWillText = sexSlave?.guest != null ? sexSlave.guest.will.ToString("F2") : "null";
                SSCLog.Important($"[SSC 仪式转奴] 概率计算: target={sexSlave?.LabelShort ?? "null"}, 恶堕={corruption:F3}, score={score:F2}, chance={enslaveChance:P2}, will={currentWillText}, detail={chanceDetail}");

                bool rollSuccess = Rand.Chance(enslaveChance);
                SSCLog.Important($"[SSC 仪式转奴] 掷骰结果: target={sexSlave?.LabelShort ?? "null"}, 成功率={enslaveChance:P2}, 掷骰通过={rollSuccess}");

                bool enslaveSuccess = false;
                if (rollSuccess)
                {
                    enslaveSuccess = GenGuest.TryEnslavePrisoner(master, sexSlave);
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                    if (!progress.CanContinueSettlement()) return;
#endif
                    string postWillText = sexSlave?.guest != null ? sexSlave.guest.will.ToString("F2") : "null";
                    SSCLog.Important($"[SSC 仪式转奴] 原版转奴调用完成: target={sexSlave?.LabelShort ?? "null"}, apiSuccess={enslaveSuccess}, prisoner={sexSlave?.IsPrisonerOfColony ?? false}, colonist={sexSlave?.IsColonist ?? false}, slave={sexSlave?.IsSlave ?? false}, guestNull={(sexSlave?.guest == null)}, will={postWillText}, faction={sexSlave?.Faction?.Name ?? "null"}");
                }
                else
                {
                    SSCLog.Important($"[SSC 仪式转奴] 未调用原版转奴: target={sexSlave?.LabelShort ?? "null"}, 原因=随机判定失败");
                }

                if (rollSuccess && enslaveSuccess)
                {
                    if (sexSlave.IsSlave)
                    {
                        changes.Add(Strings.Ritual_Enslaved(sexSlave.LabelShort));
                        SoundDefOf.TechprintApplied.PlayOneShot(sexSlave);
                    }
                }
                else
                {
                    string failReason = !rollSuccess ? "随机判定失败" : "原版转奴接口返回 false";
                    SSCLog.WarningImportant($"[SSC 仪式转奴] 转奴失败: target={sexSlave?.LabelShort ?? "null"}, 原因={failReason}, prisoner={sexSlave?.IsPrisonerOfColony ?? false}, colonist={sexSlave?.IsColonist ?? false}, slave={sexSlave?.IsSlave ?? false}");
                    changes.Add(Strings.Ritual_EnslaveFailed(sexSlave.LabelShort));
                }
            }
            else
            {
                string reason;
                if (!ritualEnslavementEnabled) reason = "设置已关闭仪式转奴";
                else if (corruption < 0.2f) reason = $"恶堕不足(cur={corruption:F3})";
                else reason = "身份不满足可转奴条件";

                SSCLog.Important($"[SSC 仪式转奴] 跳过转奴: target={sexSlave?.LabelShort ?? "null"}, 原因={reason}, prisoner={sexSlave?.IsPrisonerOfColony ?? false}, colonist={sexSlave?.IsColonist ?? false}, slave={sexSlave?.IsSlave ?? false}");
            }
        }

#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
        // 在结算回调前捕获原载体。委派主持与锁链主人分别记录。
        internal sealed class RitualChainProgress
        {
            private readonly Pawn host;
            private readonly Pawn target;
            private readonly Func<bool> canContinue;
            private readonly CompSexSlaveTraining hostComp;
            private readonly CompSexSlaveTraining targetComp;
            private readonly object hostToken;
            private readonly object targetToken;
            private readonly Need_Corruption need;
            private readonly Hediff_ChainOfSexSlave chain;
            private readonly Pawn owner;
            private readonly int startingStage;

            internal RitualChainProgress(Pawn host, Pawn target, Func<bool> canContinue)
            {
                this.host = host;
                this.target = target;
                this.canContinue = canContinue;
                hostComp = host?.TryGetComp<CompSexSlaveTraining>();
                targetComp = target?.TryGetComp<CompSexSlaveTraining>();
                hostToken = SSCPersonalityRuntimeUtility.Capture(host);
                targetToken = SSCPersonalityRuntimeUtility.Capture(target);
                need = target?.needs?.TryGetNeed<Need_Corruption>();
                chain = SSCBondUtility.GetChain(target);
                owner = chain?.LinkedPawn;
                startingStage = TrainingOutcomeUtility.GetChainStage(chain).Stage;
            }

            private bool MatchesParticipants() => host != null && target != null && !host.Dead && !target.Dead &&
                !host.Destroyed && !target.Destroyed &&
                host.TryGetComp<CompSexSlaveTraining>() == hostComp && target.TryGetComp<CompSexSlaveTraining>() == targetComp &&
                SSCPersonalityRuntimeUtility.IsCurrent(host, hostToken) && SSCPersonalityRuntimeUtility.IsCurrent(target, targetToken);

            internal bool CanContinueSettlement() => MatchesParticipants() &&
                (canContinue == null || canContinue() && MatchesParticipants());

            private bool MatchesChain(Hediff_ChainOfSexSlave expected, Pawn expectedOwner) =>
                MatchesParticipants() && target.needs?.TryGetNeed<Need_Corruption>() == need &&
                SSCBondUtility.GetChain(target) == expected && expected?.LinkedPawn == expectedOwner;

            private bool MatchesStartingStage(Hediff_ChainOfSexSlave expected, Pawn expectedOwner) =>
                MatchesChain(expected, expectedOwner) &&
                (chain == null || (expected == chain && TrainingOutcomeUtility.GetChainStage(expected).Stage == startingStage));

            private bool CanUseChain(Hediff_ChainOfSexSlave expected, Pawn expectedOwner)
            {
                if (!CanContinueSettlement() || !MatchesStartingStage(expected, expectedOwner)) return false;
                if (canContinue != null &&
                    !SSCRestrictionTrainingUtility.Evaluate(SSCRestrictionTrainingUtility.CreateRequest(host, target, true), false).Allowed)
                    return false;
                return MatchesStartingStage(expected, expectedOwner);
            }

            internal bool CanAdvance() => CanUseChain(chain, owner);

            internal bool CanWriteBinding(Hediff_ChainOfSexSlave written) =>
                written == null ? CanAdvance() && need.CurLevel >= 0.1f : CanUseChain(written, host);

            // 委派主持未建绑，继续检查原主的链。首次建链则检查这场实际写入的链。
            internal bool CanContinueAfterBinding(Hediff_ChainOfSexSlave written) =>
                written == null ? CanAdvance() : CanUseChain(written, host);

            internal void Apply(List<string> changes)
            {
                // 新建链已经用了本场的一阶额度。旧档已有的零阶链仍按原链处理。
                if (chain == null || owner == null || need == null || !CanAdvance()) return;

                float corruption = need.CurLevel;
                float before = chain.Severity;
                if (!float.IsFinite(corruption) || !float.IsFinite(before)) return;
                var stage = TrainingOutcomeUtility.GetChainStage(chain);
                if (corruption >= 0.3f)
                {
                    float budget = corruption < 0.5f ? 0.2f : 0.3f;
                    float next = UnityEngine.Mathf.Min(before + budget, corruption);
                    next = UnityEngine.Mathf.Min(next, stage.Next ?? 1f);
                    if (next > before) chain.SetRitualSeverity(target, next);
                }

                if (!CanContinueSettlement() || !MatchesChain(chain, owner)) return;
                int reached = TrainingOutcomeUtility.GetChainStage(chain).Stage;
                if (reached > startingStage)
                    changes.Add(Strings.Ritual_ChainStageReached(reached));
                else if (chain.Severity > before)
                    changes.Add(Strings.Ritual_ChainProgress(chain.Severity.ToStringPercent()));
                else if (stage.Next.HasValue)
                    changes.Add(Strings.Ritual_ChainStagePending(UnityEngine.Mathf.Max(0.3f, stage.Next.Value).ToStringPercent()));
                else
                    changes.Add(Strings.Ritual_ChainFinalStage);
            }
        }
#else
        /// <summary>
        /// 结算仪式专属的锁链成长；日常调教不会调用此方法。
        /// corruption 是调用方增加本次仪式恶堕收益后读取的当前值，不是历史最高值。
        /// 此处只负责增加进度，自然衰减和退阶由 Need_Corruption.NeedInterval 单独处理。
        /// </summary>
        private static void ProcessRitualCorruptionProgression(Pawn sexSlave, List<string> changes, float corruption)
        {
            // EN: Ritual-only change: the slave chain keeps tightening as corruption deepens.
            // CN: 仪式专属变化：恶堕越深，锁链束缚就会越紧。
            if (corruption >= 0.3f)
            {
                // 共享阶段处理已尝试建立或复用绑定。若仍没有锁链，就没有可增长的目标；
                // 不在这里绕过绑定校验新建一条，也不修改其他身份或 Trait。
                Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(sexSlave);
                if (chain == null) return;

                // 保留原来的单次增量预算：恶堕 [30%, 50%) 最多 +20%，>=50% 最多 +30%。
                // 这是“本次最多能加多少”，还需要检查当前恶堕实际能支持多少新增进度。
                float increase = corruption < 0.5f ? 0.2f : 0.3f;
                // EN: Only add progress supported by current corruption. A repeat ritual must
                // never overshoot an unearned stage or lower an already stronger chain.
                // CN: 只增加当前恶堕能够支持的进度，重复仪式不能越过未达到的阶段，
                // 也不能因为当前恶堕较低而扣掉已有锁链进度。
                // 内层 Max：恶堕低于已有严重度时，可新增空间为 0，而不是一个负增量。
                // 外层 Min：实际增量同时受单次预算和可新增空间限制。
                // 因而：新严重度 = 旧严重度 + min(单次预算, max(0, 当前恶堕 - 旧严重度))。
                // 例：锁链 0.8、恶堕 0.65 => 增量 0，保留 0.8；
                //     锁链 0.4、恶堕 0.5  => 增量 0.1，进入 0.5 阶段。
                // 不能直接把锁链设置为 min(旧严重度 + 预算, 当前恶堕)，否则第一个例子
                // 会把已有 0.8 降至 0.65，反而让仪式本身变成扣减进度的来源。
                increase = UnityEngine.Mathf.Min(increase, UnityEngine.Mathf.Max(0f, corruption - chain.Severity));
                if (increase > 0f)
                    // 通过原有入口写入，继续保留严重度上限钳制及变更日志。
                    Hediff_ChainOfSexSlave.IncreaseChainSeverity(sexSlave, increase);
            }

            // The SexSlave trait is a historical mirror maintained by Need_Corruption.
            // Gameplay progression and ritual advancement are authoritative on the chain Hediff.
        }
#endif

        private static bool HasHediff(Pawn pawn, string defName)
        {
            return pawn.health.hediffSet.hediffs.Any(h => h.def.defName == defName);
        }
    }
}
