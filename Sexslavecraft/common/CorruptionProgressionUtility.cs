using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

// 仪式建立绑定、尝试原版转奴，并按本场起始阶段推进性奴化。旧版保留日常建链。
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
#if SSC_RW17
            // 公共适配入口还有旧调用者。明确拒绝非仪式的进度请求，避免绕过日常结算边界。
            if (!isRitual) return null;
#endif
            Need_Corruption need = sexSlave.needs.TryGetNeed<Need_Corruption>();
            if (need == null) return null;
#if SSC_RW17
            // 无主目标已经有进度。由本场确定的分支处理交接或成长，失效也不会回落到零阶建链。
            if (progress.HasOwnerlessProgress) return progress.ApplyOwnerless(score);
#endif
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            if (isRitual && !progress.CanAdvance()) return null;
#endif

            // 调用方已发放本场恶堕收益。先用当前值判定能否建链，再于建链回调后读取同一需求的最新值。
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
                    // 原版囚犯或殖民者转奴和 SSC 锁链成长各有条件。原版调用也可能触发回调，
                    // 因此 Apply 会复查原链和授权，再读取实际恶堕并尝试推进。
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
#if !SSC_RW17
            if (!isRitual)
            {
                SSCBondUtility.Bind(master, sexSlave);
                return null;
            }
#endif

            bool hadBridleTarget = SSCBondUtility.GetBridle(master)?.targets.Contains(sexSlave) == true;
            bool hadChain = SSCBondUtility.GetChain(sexSlave) != null;
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            // 委派主持可能有仪式权限，但已有其他主人时 BindForRitual 会拒绝写入。
            // null 返回给仪式后，后续转奴和限阶结算按原链及当前许可继续校验。
            if (!progress.TryBindForRitual(out var boundChain) ||
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
                // 概率计算会读取并整理意志。跨过该调用后，再确认这场仪式有资格提交转奴。
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
        /// <summary>用本场额度推进已有性奴化进度，受实际恶堕和下一阶起点限制。</summary>
        /// <remarks>纯数值计算。仪式和发作分别核对参与者及原进度载体，再决定是否写入及发反馈。</remarks>
        internal static float CalculateChainProgress(float severity, float corruption, float? nextStage)
        {
            // 恶堕达到 30% 才能推进。50% 起提高单场额度。已有锁链进度高于任一限制时保留原进度。
            if (!float.IsFinite(severity) || !float.IsFinite(corruption) || corruption < 0.3f) return severity;
            float budget = corruption < 0.5f ? 0.2f : 0.3f;
            float next = UnityEngine.Mathf.Min(severity + budget, corruption);
            // 至多推进到下一阶起点。nextStage 为空供最高阶仪式继续补同阶进度。
            return UnityEngine.Mathf.Max(severity, UnityEngine.Mathf.Min(next, nextStage ?? 1f));
        }

        /// <summary>
        /// 保存一场已认领仪式的进度载体与参与者上下文。完整结算在质量、记忆和恶堕收益回调前捕获；
        /// 直接调用的公共入口在入口捕获。已有绑定的实际主人和本场主持可以是不同的角色。
        /// </summary>
        /// <remarks>
        /// 原需求、进度实例和分支随场次固定。常规成长核对起始阶段，恶堕和同阶严重度取写入前的实际值。
        /// 无主转有主移交原实例持有的最新严重度，从挂链开始由绑定服务核对精确值。
        /// </remarks>
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
#if SSC_RW17
            private readonly Pawn_HealthTracker targetHealth;
            private readonly Hediff_SSCOwnerlessSlave ownerless;
            private readonly Hediff_SSCOwnerlessSlave zeroOwnerless;
            private readonly bool bindOwnerless;
            internal bool HasOwnerlessProgress => ownerless != null;
#endif

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
                // 捕获原链或正式无主记录。任一载体被替换时，本场都不能借新载体领取进度。
                chain = SSCBondUtility.GetChain(target);
                owner = chain?.LinkedPawn;
#if SSC_RW17
                targetHealth = target?.health;
                var incoming = chain == null ? Hediff_SSCOwnerlessSlave.GetPersonalityProgress(target) : null;
                int incomingStage = TrainingOutcomeUtility.GetSexSlaveProgressStage(incoming).Stage;
                if (incomingStage > 0)
                {
                    ownerless = incoming;
                    // 主人和训导官使用同一场仪式。主持身份在收益之前确定，回调变更身份会使原分支失效。
                    bindOwnerless = SSCIdentityUtility.IsMaster(host);
                    startingStage = incomingStage;
                    return;
                }
                zeroOwnerless = incoming;
#endif
                startingStage = TrainingOutcomeUtility.GetSexSlaveProgressStage(chain).Stage;
            }

            private bool MatchesParticipants() => host != null && target != null && !host.Dead && !target.Dead &&
                !host.Destroyed && !target.Destroyed &&
                host.TryGetComp<CompSexSlaveTraining>() == hostComp && target.TryGetComp<CompSexSlaveTraining>() == targetComp &&
                SSCPersonalityRuntimeUtility.IsCurrent(host, hostToken) && SSCPersonalityRuntimeUtility.IsCurrent(target, targetToken);

            // 通用记忆、经验与结算信只校验原参与者和场次。首次建链和本场升阶是预期变化，
            // 所以这些后续收益也可在锁链已改变时继续。场次回查本身也可能触发回调，再确认原人格。
            internal bool CanContinueSettlement() => MatchesParticipants() &&
                (canContinue == null || canContinue() && MatchesParticipants());

            private bool MatchesChain(Hediff_ChainOfSexSlave expected, Pawn expectedOwner) =>
                MatchesParticipants() && target.needs?.TryGetNeed<Need_Corruption>() == need &&
                SSCBondUtility.GetChain(target) == expected && expected?.LinkedPawn == expectedOwner;

            private bool MatchesStartingStage(Hediff_ChainOfSexSlave expected, Pawn expectedOwner) =>
                MatchesChain(expected, expectedOwner) &&
#if SSC_RW17
                // 零阶首次建链保留原严重度规则，同样检查当前再绑定冷却。
                (chain != null || Hediff_SSCBondReleaseAftereffect.Active(target) == null) &&
                // 零阶分支准备建链时也必须保留原类型，避免覆盖质量或收益回调中刚形成的无主进度。
                (chain != null || ownerless != null ||
                    (expected != null || Hediff_SSCOwnerlessSlave.Find(target) == zeroOwnerless) &&
                    TrainingOutcomeUtility.GetSexSlaveProgressStage(Hediff_SSCOwnerlessSlave.Find(target)).Stage == 0) &&
#endif
                (chain == null || (expected == chain && TrainingOutcomeUtility.GetSexSlaveProgressStage(expected).Stage == startingStage));

            private bool CanUseChain(Hediff_ChainOfSexSlave expected, Pawn expectedOwner)
            {
                if (!CanContinueSettlement() || !MatchesStartingStage(expected, expectedOwner)) return false;
                // 正式仪式沿用调教准入。原主人可手动主持，委派主持须满足指派和当前许可。
                // 许可查询返回后再次核对原载体，避免把旧请求落到该查询期间刚换入的对象。
                if ((canContinue != null
#if SSC_RW17
                    || HasOwnerlessProgress
#endif
                    ) &&
                    !SSCRestrictionTrainingUtility.Evaluate(SSCRestrictionTrainingUtility.CreateRequest(host, target, true), false).Allowed)
                    return false;
                return MatchesStartingStage(expected, expectedOwner);
            }

            internal bool CanAdvance() => CanUseChain(chain, owner);

            // 身份刷新可能改变未写链时的恶堕，仍须达到 10% 建链门槛。写入成功后改查实际提交的链。
            internal bool CanWriteBinding(Hediff_ChainOfSexSlave written) =>
                written == null ? CanAdvance() && need.CurLevel >= 0.1f : CanUseChain(written, host);

            internal bool TryBindForRitual(out Hediff_ChainOfSexSlave written)
            {
#if SSC_RW17
                // 旧零阶锁链解绑留下的无主记录也要完成载体交接。保留原首次建链的起始值。
                if (zeroOwnerless != null)
                    return SSCBondUtility.BindNewWithProgress(host, target, zeroOwnerless, SSCDefOf.ChainOfSexSlave?.initialSeverity ?? .2f,
                        () => CanContinueSettlement() && target.needs?.TryGetNeed<Need_Corruption>() == need &&
                            (!SSCBondUtility.IsBoundTo(target, host) || Hediff_SSCOwnerlessSlave.Find(target) != zeroOwnerless ||
                                SSCRestrictionTrainingUtility.Evaluate(SSCRestrictionTrainingUtility.CreateRequest(host, target, true),
                                    requireAssignment: true).Allowed),
                        out written, () => CanWriteBinding(null));
#endif
                return SSCBondUtility.BindForRitual(host, target, CanWriteBinding, out written);
            }

            // 委派主持未建绑，继续检查原主的链。首次建链则检查这场实际写入的链。
            internal bool CanContinueAfterBinding(Hediff_ChainOfSexSlave written)
            {
#if SSC_RW17
                if (HasOwnerlessProgress)
                    return bindOwnerless ? CanUseTransferredChain(written) : CanUseOwnerless();
#endif
                return written == null ? CanAdvance() : CanUseChain(written, host);
            }

#if SSC_RW17
            // 无主主持按调教员称呼。主人主持只有实际绑定后才在明细中称为目标的主人。
            internal string DescribeParticipants() => HasOwnerlessProgress && (!bindOwnerless || !SSCBondUtility.IsBoundTo(target, host))
                ? "SSC_Ritual_OwnerlessTrainerHeader".Translate(host.LabelShort, target.LabelShort).ToString()
                : Strings.Ritual_MasterSlaveHeader(host.LabelShort, target.LabelShort);

            // 挂链时归属变化是本场预期。先复核两端对象和仪式，再分别校验源记录、待写入或已提交的链。
            private bool OwnerlessParticipantsCurrent() => MatchesParticipants() && target.health == targetHealth &&
                need != null && target.needs?.TryGetNeed<Need_Corruption>() == need && SSCIdentityUtility.IsSexSlave(target) &&
                SSCIdentityUtility.IsMaster(host) == bindOwnerless;

            private bool MatchesOwnerless() => OwnerlessParticipantsCurrent() &&
                Hediff_SSCOwnerlessSlave.GetPersonalityProgress(target) == ownerless &&
                (bindOwnerless ? TrainingOutcomeUtility.GetSexSlaveProgressStage(ownerless).Stage > 0
                    : TrainingOutcomeUtility.GetSexSlaveProgressStage(ownerless).Stage == startingStage);

            // 查行为许可可能触发装备等兼容查询。查询后重新核对原需求、载体和主持分支。
            private bool CanUseOwnerless() => CanContinueSettlement() && MatchesOwnerless() &&
                SSCRestrictionTrainingUtility.Evaluate(SSCRestrictionTrainingUtility.CreateRequest(host, target, true), false).Allowed &&
                MatchesOwnerless();

            private bool CanPrepareOwnerlessBinding() => CanUseOwnerless() && need.CurLevel >= .1f;

            private bool OwnerlessBindingContextCurrent()
            {
                if (!CanContinueSettlement() || !OwnerlessParticipantsCurrent()) return false;
                // 源记录仍在且临时锁链已经挂上时，默认仪式准入会给主持实际主人的指派豁免。
                // 在源记录移交前仍按原训练资格和名单核对。移交后只需核对场次、参与者和新绑定。
                if (!SSCBondUtility.IsBoundTo(target, host) || Hediff_SSCOwnerlessSlave.Find(target) != ownerless) return true;
                var request = SSCRestrictionTrainingUtility.CreateRequest(host, target, true);
                return SSCRestrictionTrainingUtility.Evaluate(request, requireAssignment: true).Allowed && OwnerlessParticipantsCurrent();
            }

            private bool CanUseTransferredChain(Hediff_ChainOfSexSlave written) => written != null &&
                CanUseChain(written, host) && OwnerlessParticipantsCurrent() && Hediff_SSCOwnerlessSlave.Find(target) == null &&
                SSCBondUtility.GetBridle(host)?.targets.Contains(target) == true;

            internal string ApplyOwnerless(float score)
            {
                if (!CanUseOwnerless()) return null;
                var changes = new List<string>();
                if (bindOwnerless)
                {
                    if (need.CurLevel < .1f) return null;
                    // 直接移交实际严重度。进度显示和可选的原版转奴都要等双向关系与载体交接完成。
                    float severity = ownerless.Severity;
                    if (!SSCBondUtility.BindNewWithProgress(host, target, ownerless, severity,
                        OwnerlessBindingContextCurrent, out var written, CanPrepareOwnerlessBinding) ||
                        !CanUseTransferredChain(written)) return null;

                    changes.Add(Strings.Bond_BridleAdded(host.LabelShort, target.LabelShort));
                    changes.Add(Strings.Bond_ChainAdded(target.LabelShort, host.LabelShort));
                    changes.Add("SSC_BreakdownSuccess_CurrentProgress".Translate(
                        TrainingOutcomeUtility.GetSexSlaveProgressStage(written).Stage, written.Severity.ToStringPercent()));
                    ProcessRitualEnslavement(host, target, score, changes, need.CurLevel, this, written);
                    return string.Join("\n", changes);
                }

                // 训导官沿用现有委派仪式的原版转奴判定。调用可能改变参与者或归属，先重查许可和起始阶段，
                // 再用普通收益入账后的实际恶堕和单场额度推进原无主实例。
                ProcessRitualEnslavement(host, target, score, changes, need.CurLevel, this, null);
                if (!CanUseOwnerless()) return changes.Count == 0 ? null : string.Join("\n", changes);
                float before = ownerless.Severity;
                var stage = TrainingOutcomeUtility.GetSexSlaveProgressStage(ownerless);
                float next = CalculateChainProgress(before, need.CurLevel, stage.Next);
                if (next > before) ownerless.Severity = next;

                // 本场升阶本身是预期结果。健康通知返回后按实际进度发反馈，并复核实例及主持资格。
                if (!CanContinueSettlement() || !OwnerlessParticipantsCurrent() ||
                    Hediff_SSCOwnerlessSlave.GetPersonalityProgress(target) != ownerless || !SSCIdentityUtility.IsTrainer(host))
                    return changes.Count == 0 ? null : string.Join("\n", changes);
                int reached = TrainingOutcomeUtility.GetSexSlaveProgressStage(ownerless).Stage;
                if (reached > startingStage)
                    changes.Add("SSC_BreakdownSuccess_StageAdvanced".Translate(startingStage, reached));
                else if (ownerless.Severity > before)
                    changes.Add("SSC_BreakdownSuccess_ProgressAdvanced".Translate(reached, before.ToStringPercent(), ownerless.Severity.ToStringPercent()));
                else
                    changes.Add("SSC_BreakdownSuccess_StageUnchanged".Translate());
                return string.Join("\n", changes);
            }
#endif

            internal void Apply(List<string> changes)
            {
                // 首次建链按 Def 初始严重度 20% 进入一阶，用完本场额度。
                // 已有且有主的零阶残链仍按原链处理，本场最多到实际一阶起点。
                if (chain == null || owner == null || need == null || !CanAdvance()) return;

                float corruption = need.CurLevel;
                float before = chain.Severity;
                if (!float.IsFinite(corruption) || !float.IsFinite(before)) return;
                var stage = TrainingOutcomeUtility.GetSexSlaveProgressStage(chain);
                float next = CalculateChainProgress(before, corruption, stage.Next);
                if (next > before) chain.SetProgressSeverity(target, next);

                // 写严重度会派发阶段通知。通知结束后确认原链仍有效，按实际结果选结算反馈。
                // 此处允许阶段已改变，因为实际升阶就是此写入的预期结果。
                if (!CanContinueSettlement() || !MatchesChain(chain, owner)) return;
                int reached = TrainingOutcomeUtility.GetSexSlaveProgressStage(chain).Stage;
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
