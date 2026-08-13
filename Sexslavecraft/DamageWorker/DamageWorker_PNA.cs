using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine; // 用于 Mathf.Max
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    // ==========================================
    // 1. 统一配置类 (存放XML参数)
    // ==========================================
    public class PNA_DWExtension : DefModExtension
    {
        // --- 核心判定 ---
        public HediffDef adapterHediff;       // [开关] 只有拥有这个Hediff的人才会被治疗

        // --- 效果 ---
        public HediffDef combatBuff;          // [有体质] 给的增益
        public HediffDef debuff;              // [无体质] 给的减益 (Debuff)

        // --- 数值 ---
        public float healAmount;              // [有体质] 治疗量
        public float trueDamageAmount;        // [无体质] 真实伤害量

        // --- 高级版参数 ---
        public float fixedSeverity;           // 固定Debuff强度

        // --- 低级版参数 ---
        public HediffDef recordHediff;        // 抗性快照
        public float baseSeverity;            // 基础Debuff强度
    }

    // ==========================================
    // 2. 高级版逻辑 (固定数值)
    // ==========================================
    public class DamageWorker_HighTier : DamageWorker_AddInjury
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            var ext = dinfo.Def.GetModExtension<PNA_DWExtension>();
            if (ext == null) return;

            // === 判定 1：目标有特殊体质 (给糖) ===
            if (ext.adapterHediff != null && pawn.health.hediffSet.HasHediff(ext.adapterHediff))
            {
                // A. 治疗
                var injuries = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>().Where(i => i.CanHealNaturally()).ToList();
                if (injuries.Any())
                {
                    injuries.RandomElement().Heal(ext.healAmount);
                    //MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Healed", 3f);
                }

                // B. 加战斗 Buff
                HealthUtility.AdjustSeverity(pawn, ext.combatBuff, 1.0f);
                //MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Buffed", 3f);

                // C. 结束 (不造成伤害)
                return;
            }

            // === 判定 2：目标无特殊体质 (给棒槌) ===
            // 不管是敌人、野生动物、还是没有体质的队友，通通进这里
            else
            {
                // A. 施加 Debuff (固定数值)
                HealthUtility.AdjustSeverity(pawn, ext.debuff, ext.fixedSeverity);

                // B. 造成真实伤害
                DamageInfo trueDinfo = new DamageInfo(dinfo);
                trueDinfo.SetAmount(ext.trueDamageAmount);
                trueDinfo.SetIgnoreArmor(true);
                base.ApplySpecialEffectsToPart(pawn, ext.trueDamageAmount, trueDinfo, result);
            }
        }
    }

    // ==========================================
    // 3. 低级版逻辑 (抗性算法)
    // ==========================================
    public class DamageWorker_LowTier : DamageWorker_AddInjury
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            var ext = dinfo.Def.GetModExtension<PNA_DWExtension>();
            if (ext == null) return;

            // === 判定 1：目标有特殊体质 ===
            if (ext.adapterHediff != null && pawn.health.hediffSet.HasHediff(ext.adapterHediff))
            {
                // 逻辑同上：治疗 + Buff
                var injuries = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>().Where(i => i.CanHealNaturally()).ToList();
                if (injuries.Any())
                {
                    injuries.RandomElement().Heal(ext.healAmount);
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Healed", 3f);
                }

                HealthUtility.AdjustSeverity(pawn, ext.combatBuff, 1.0f);
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Buffed", 3f);
                return;
            }

            // === 判定 2：目标无特殊体质 ===
            else
            {
                // A. 施加 Debuff (算法计算)
                float divisor = 1.0f;
                Hediff record = pawn.health.hediffSet.GetFirstHediffOfDef(ext.recordHediff);

                if (record != null)
                {
                    divisor = record.Severity; // 读快照
                }
                else
                {
                    float currentConsciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                    if (currentConsciousness > 2.0f)
                    {
                        // 强敌建立快照
                        record = pawn.health.AddHediff(ext.recordHediff);
                        record.Severity = currentConsciousness;
                        divisor = currentConsciousness;
                    }
                    else
                    {
                        // 弱者保底 0.2
                        divisor = Mathf.Max(currentConsciousness, 0.2f);
                    }
                }

                float amountToAdd = ext.baseSeverity / divisor;
                if (amountToAdd > 1.0f) amountToAdd = 1.0f;

                HealthUtility.AdjustSeverity(pawn, ext.debuff, amountToAdd);

                // B. 造成真实伤害
                DamageInfo trueDinfo = new DamageInfo(dinfo);
                trueDinfo.SetAmount(ext.trueDamageAmount);
                trueDinfo.SetIgnoreArmor(true);
                base.ApplySpecialEffectsToPart(pawn, ext.trueDamageAmount, trueDinfo, result);
            }
        }
    }
}