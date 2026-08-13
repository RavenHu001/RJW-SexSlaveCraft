using RimWorld;
using UnityEngine;
using Verse;

// EN: This file handles will break and ritual enslavement math.
// EN: It converts corruption gain and training score into will damage, then turns that state into a slave-conversion chance.
// CN: 这个文件负责意志击穿和仪式转奴的数值逻辑。
// CN: 它会把恶堕增量和调教分数换算成意志损耗，再把当前状态换算成转奴概率。
namespace SexSlaveCraft
{
    public static class WillReductionUtility
    {
        public static void ApplyWillReductionByCorruptionBand(Pawn master, Pawn slave, float corruptionGain, bool isRitual)
        {
            if (slave?.guest == null || slave.IsSlave) return;
            if (!slave.IsPrisonerOfColony && !slave.IsColonist) return;

            // EN: Step 1: read current will, then map this corruption gain to the matching will-break band.
            // CN: 步骤 1：先读取当前意志，再把这次恶堕增量映射到对应的意志击穿档位。
            float currentWill = EnsureValidWill(slave);
            if (currentWill <= 0f) return;

            int band = GetCorruptionBand(corruptionGain);
            float reduction = GetDailyWillReduction(currentWill, band);
            if (isRitual)
            {
                // EN: The Binding Ritual hits harder than daily training, so ritualPhase multiplies the will break.
                // CN: 绑定仪式比日常调教下手更重，所以 ritualPhase 会继续放大意志击穿。
                reduction *= GetRitualStageMultiplier(slave);
            }

            reduction = Mathf.Min(reduction, currentWill);
            if (reduction <= 0f) return;

            float oldWill = slave.guest.will;
            slave.guest.will = Mathf.Max(0f, slave.guest.will - reduction);

            // EN: Step 2: show the vanilla prisoner-will feedback so the player can see the break immediately.
            // CN: 步骤 2：抛出原版囚犯意志反馈，让玩家能立刻看到这次意志击穿结果。
            string text = "TextMote_WillReduced".Translate(oldWill.ToString("F1"), slave.guest.will.ToString("F1"));
            MoteMaker.ThrowText((master.DrawPos + slave.DrawPos) / 2f, master.Map, text, 8f);

            if (slave.IsPrisonerOfColony && slave.guest.will <= 0f && oldWill > 0f)
            {
                TaggedString taggedString = "MessagePrisonerWillBroken".Translate(master, slave);
                if (slave.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.AttemptRecruit))
                {
                    taggedString += " " + "MessagePrisonerWillBroken_RecruitAttempsWillBegin".Translate();
                }

                Messages.Message(taggedString, slave, MessageTypeDefOf.PositiveEvent);
            }
        }

        public static float CalculateEnslaveChance(Pawn sexSlave, float score, float corruption, bool isRitual, out string detail)
        {
            float currentWill = EnsureValidWill(sexSlave);
            float scoreN = Mathf.Clamp01(score / 75f);
            float corruptionN = Mathf.Clamp01((corruption - 0.2f) / 0.8f);

            // EN: Clamp the will term so naturally low-will races do not become guaranteed sex slaves too early.
            // CN: 对意志项做钳制，避免先天低意志种族过早进入“必定转奴”的区间。
            float willForChance = Mathf.Clamp(currentWill, 15f, 80f);
            float willBreakN = 1f - Mathf.Clamp01(willForChance / 80f);

            float chance = (scoreN * 0.40f) + (willBreakN * 0.30f) + (corruptionN * 0.30f);

            float baseWill = GetBaseWillForPawn(sexSlave);
            if (baseWill <= 15f)
            {
                chance = Mathf.Min(chance, isRitual ? 0.90f : 0.75f);
            }

            chance = Mathf.Clamp01(chance);
            detail = "SSC_EnslaveChanceDetail".Translate(
                chance.ToString("P1"),
                scoreN.ToString("P0"),
                willBreakN.ToString("P0"),
                corruptionN.ToString("P0"));
            return chance;
        }

        private static int GetCorruptionBand(float corruptionGain)
        {
            float percent = corruptionGain * 100f;
            if (percent >= 8f) return 8;
            if (percent >= 6f) return 6;
            if (percent >= 4f) return 4;
            return 2;
        }

        private static float GetDailyWillReduction(float currentWill, int band)
        {
            switch (band)
            {
                case 8: return 0.8f + (currentWill * 0.25f);
                case 6: return 0.6f + (currentWill * 0.2f);
                case 4: return 0.4f + (currentWill * 0.125f);
                default: return 0.2f + (currentWill * 0.1f);
            }
        }

        private static float GetRitualStageMultiplier(Pawn pawn)
        {
            int phase = pawn?.TryGetComp<CompSexSlaveTraining>()?.ritualPhase ?? 0;
            if (phase <= 0) return 1.0f;
            if (phase == 1) return 1.05f;
            if (phase == 2) return 1.10f;
            if (phase == 3) return 1.15f;
            if (phase == 4) return 1.20f;
            return 1.25f;
        }

        private static float GetBaseWillForPawn(Pawn pawn)
        {
            if (pawn?.kindDef?.initialWillRange != null && pawn.kindDef.initialWillRange.HasValue)
            {
                FloatRange range = pawn.kindDef.initialWillRange.Value;
                return Mathf.Max(1f, (range.min + range.max) * 0.5f);
            }

            if (pawn?.guest != null && pawn.guest.will > 0f)
            {
                return pawn.guest.will;
            }

            return 10f;
        }

        private static float EnsureValidWill(Pawn pawn)
        {
            if (pawn?.guest == null) return 0f;
            if (pawn.guest.will < 0f)
            {
                pawn.guest.will = GetBaseWillForPawn(pawn);
            }

            return Mathf.Max(0f, pawn.guest.will);
        }
    }
}
