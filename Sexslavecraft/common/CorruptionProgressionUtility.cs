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
        {
            Need_Corruption need = sexSlave.needs.TryGetNeed<Need_Corruption>();
            if (need == null) return null;

            // EN: Step 1: read current corruption and collect every newly triggered stage-change message.
            // CN: 步骤 1：读取当前恶堕值，并收集这次新触发的全部阶段变化文本。
            float corruption = need.CurLevel;
            List<string> changes = new List<string>();

            ProcessSharedCorruptionProgression(master, sexSlave, isRitual, changes, corruption);
            if (isRitual)
            {
                // EN: The Binding Ritual gets extra progression rules beyond normal daily training.
                // CN: 绑定仪式除了日常调教共享阶段外，还会额外处理仪式专属变化。
                ProcessRitualEnslavement(master, sexSlave, score, changes, corruption);
                ProcessRitualCorruptionProgression(sexSlave, changes, corruption);
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

        private static void ProcessSharedCorruptionProgression(Pawn master, Pawn sexSlave, bool isRitual, List<string> changes, float corruption)
        {
            if (corruption < 0.1f) return;

            Hediff_BridleOfSexSlave existingBridle = SSCBondUtility.GetBridle(master);
            bool hadBridleTarget = existingBridle != null && existingBridle.targets.Contains(sexSlave);
            bool hadChain = SSCBondUtility.GetChain(sexSlave) != null;

            // EN: Bind updates both sides atomically and refuses to steal an existing bond.
            // CN: Bind 会同时更新主从两侧，并拒绝覆盖已经属于其他主人的锁链。
            if (!SSCBondUtility.Bind(master, sexSlave)) return;

            if (isRitual && !hadBridleTarget)
                changes.Add(Strings.Bond_BridleAdded(master.LabelShort, sexSlave.LabelShort));
            if (isRitual && !hadChain)
                changes.Add(Strings.Bond_ChainAdded(sexSlave.LabelShort, master.LabelShort));
        }

        private static void ProcessRitualEnslavement(Pawn master, Pawn sexSlave, float score, List<string> changes, float corruption)
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
                changes.Add(chanceDetail);
                currentWillText = sexSlave?.guest != null ? sexSlave.guest.will.ToString("F2") : "null";
                SSCLog.Important($"[SSC 仪式转奴] 概率计算: target={sexSlave?.LabelShort ?? "null"}, 恶堕={corruption:F3}, score={score:F2}, chance={enslaveChance:P2}, will={currentWillText}, detail={chanceDetail}");

                bool rollSuccess = Rand.Chance(enslaveChance);
                SSCLog.Important($"[SSC 仪式转奴] 掷骰结果: target={sexSlave?.LabelShort ?? "null"}, 成功率={enslaveChance:P2}, 掷骰通过={rollSuccess}");

                bool enslaveSuccess = false;
                if (rollSuccess)
                {
                    enslaveSuccess = GenGuest.TryEnslavePrisoner(master, sexSlave);
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

        private static void ProcessRitualCorruptionProgression(Pawn sexSlave, List<string> changes, float corruption)
        {
            // EN: Ritual-only change: the slave chain keeps tightening as corruption deepens.
            // CN: 仪式专属变化：恶堕越深，锁链束缚就会越紧。
            if (corruption >= 0.3f)
            {
                float increase = corruption < 0.5f ? 0.2f : 0.3f;
                // EN: Once corruption climbs high enough, the slave chain tightens further and pushes the pawn deeper into the bond.
                // CN: 当恶堕继续升高时，锁链束缚会进一步加深，把这个 Pawn 往更深的主从关系里推进。
                Hediff_ChainOfSexSlave.IncreaseChainSeverity(sexSlave, increase);
            }

            // The SexSlave trait is a historical mirror maintained by Need_Corruption.
            // Gameplay progression and ritual advancement are authoritative on the chain Hediff.
        }

        private static bool HasHediff(Pawn pawn, string defName)
        {
            return pawn.health.hediffSet.hediffs.Any(h => h.def.defName == defName);
        }
    }
}
