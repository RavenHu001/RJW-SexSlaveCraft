// AI 辅助生成：PNA P2 来源接入与一次有效命中剂量。
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
        // 保留旧 XML 字段以兼容定义；P2 的保护判定统一使用实际有效锁链查询。
        public HediffDef adapterHediff;       // 旧体质配置引用，不替代有效链校验

        // --- 效果 ---
        public HediffDef combatBuff;          // [有体质] 给的增益
        public HediffDef debuff;              // 旧负荷配置引用；P2 输入交由共享入口选择真实载体

        // --- 数值 ---
        public float healAmount;              // [有体质] 治疗量
        public float trueDamageAmount;        // [无体质] 真实伤害量

        // 剂量配置采用原始严重度尺度，传入共享入口前除以满值 2。
        // --- 高级版参数 ---
        public float fixedSeverity;           // 固定Debuff强度

        // --- 低级版参数 ---
        public HediffDef recordHediff;        // 抗性快照
        public float baseSeverity;            // 基础Debuff强度
    }

    // AI 辅助生成：只在当前伤害调用内认领 PNA 输入，不按 tick 合并独立命中。
    public abstract class DamageWorker_SSCPna : DamageWorker_AddInjury
    {
        private bool exposureApplied;

        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            // DamageWorker 会复用；每次 Apply 单独开始认领，保存外层状态以支持嵌套命中。
            bool previous = exposureApplied;
            exposureApplied = false;
            try { return base.Apply(dinfo, thing); }
            // 不论原伤害流程如何退出，都恢复外层认领状态，避免吞掉后续独立命中。
            finally { exposureApplied = previous; }
        }

        /// <summary>首次有效部位回调认领一次输入；同次传播的后续回调只继续处理原伤害。</summary>
        protected bool ClaimExposure()
        {
            // 在调用共享入口之前标记，避免输入引发的健康回调重复认领同次暴露。
            if (exposureApplied) return false;
            exposureApplied = true;
            return true;
        }
    }

    // ==========================================
    // 2. 高级版逻辑 (固定数值)
    // ==========================================
    public class DamageWorker_HighTier : DamageWorker_SSCPna
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            // 配置缺失时不自行推断武器剂量或治疗参数。
            var ext = dinfo.Def.GetModExtension<PNA_DWExtension>();
            if (ext == null) return;

            // 有有效链的身体保留治疗／战斗强化分支，不施加普通负荷与真实伤害。
            if (SSCPnaExposureUtility.IsRooted(pawn))
            {
                // 在可自然恢复的伤口中选一个，沿用原配置治疗量。
                var injuries = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>().Where(i => i.CanHealNaturally()).ToList();
                if (injuries.Any())
                {
                    injuries.RandomElement().Heal(ext.healAmount);
                    //MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Healed", 3f);
                }

                // 强化仍按原分支执行；PNA 输入去重不改变治疗／强化的原触发次数。
                HealthUtility.AdjustSeverity(pawn, ext.combatBuff, 1.0f);
                //MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Buffed", 3f);

                // 提前结束，保持有链分支不进入普通伤害流程。
                return;
            }

            // 无有效链时沿用普通分支，PNA 是否接受输入由共享入口的身体准入决定。
            else
            {
                // 固定原始剂量换算为归一化负荷，同次有效命中只提交一次。
                if (ClaimExposure()) SSCPnaExposureUtility.ApplyExposure(pawn,
                    ext.fixedSeverity / SSCPnaMath.MaximumSeverity, SSCPnaExposureSource.Weapon);

                // 各部位伤害继续执行，不因本次 PNA 已输入而跳过；穿甲语义沿用原代码。
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
    public class DamageWorker_LowTier : DamageWorker_SSCPna
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            // 取得低阶武器配置；抗性快照、治疗与伤害数值仍来自原 XML。
            var ext = dinfo.Def.GetModExtension<PNA_DWExtension>();
            if (ext == null) return;

            // 仅实际有效链触发治疗保护，无主人引用的残链仍进入普通分支。
            if (SSCPnaExposureUtility.IsRooted(pawn))
            {
                // 沿用原低阶治疗与强化，包括原有飘字反馈，不新增性奴负荷收益。
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

            // 普通身体的首次有效部位回调计算意识抗性并提交剂量。
            else
            {
                // 先认领当前命中，后续传播不重算抗性、不重复加 PNA。
                if (ClaimExposure())
                {
                    float divisor = 1.0f;
                    Hediff record = pawn.health.hediffSet.GetFirstHediffOfDef(ext.recordHediff);

                    if (record != null)
                    {
                        // 已有快照继续使用原严重度，避免后续意识下降反向抹掉既有抗性。
                        divisor = record.Severity; // 读快照
                    }
                    else
                    {
                        // 无快照时读取当前意识：大于 2 建立快照，其余保持原 0.2 除数保底。
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

                    // 非法旧快照不产生无限／负剂量；正常快照和 0.2 保底继续沿用。
                    if (!SSCPnaMath.IsFinite(divisor) || divisor <= 0f) divisor = 1f;
                    float amountToAdd = ext.baseSeverity / divisor;
                    if (amountToAdd > 1.0f) amountToAdd = 1.0f;

                    // 单次原始上限仍为 1；除以满值 2 后进入共享输入与实际增量补充。
                    SSCPnaExposureUtility.ApplyExposure(pawn,
                        amountToAdd / SSCPnaMath.MaximumSeverity, SSCPnaExposureSource.Weapon);
                }

                // 传播中的每个原伤害回调仍执行，PNA 一次输入不改变伤害结算次数。
                DamageInfo trueDinfo = new DamageInfo(dinfo);
                trueDinfo.SetAmount(ext.trueDamageAmount);
                trueDinfo.SetIgnoreArmor(true);
                base.ApplySpecialEffectsToPart(pawn, ext.trueDamageAmount, trueDinfo, result);
            }
        }
    }
}
