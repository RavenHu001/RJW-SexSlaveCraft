// AI 辅助生成：PNA 枪械一次输入、性奴活化及有链无伤分流。
using System.Linq;
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
        public HediffDef combatBuff;          // 仅兼容旧 XML 字段；不再发放旧强化
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
        // 与一次输入认领一样，只属于当前 Apply 调用；finally 恢复外层状态，不缓存身体信息。
        private bool protectedHit;

        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            // DamageWorker 会复用；每次 Apply 单独开始认领，保存外层状态以支持嵌套命中。
            bool previous = exposureApplied;
            bool previousProtection = protectedHit;
            exposureApplied = false;
            protectedHit = thing is Pawn pawn && SSCPnaExposureUtility.IsRooted(pawn);
            try { return base.Apply(dinfo, thing); }
            // 不论原伤害流程如何退出，都恢复外层认领状态，避免吞掉后续独立命中。
            finally { exposureApplied = previous; protectedHit = previousProtection; }
        }

        /// <summary>首次有效部位回调认领一次输入；同次传播的后续回调只继续处理原伤害。</summary>
        protected bool ClaimExposure()
        {
            // 在调用共享入口之前标记，避免输入引发的健康回调重复认领同次暴露。
            if (exposureApplied) return false;
            exposureApplied = true;
            return true;
        }

        /// <summary>负荷只输入一次；治疗与普通伤害仍按原有部位回调次数执行。</summary>
        protected void FinishHit(Pawn pawn, bool protectedAtHit, SSCPnaExposureResult exposure,
            PNA_DWExtension ext, DamageInfo dinfo, DamageResult result, bool showHealingText)
        {
            // 同步回调可能改链；命中开始已有保护或输入后新获有效链，都不能走枪伤分支。
            // 已解绑的身体不再治疗，原保护命中仍直接结束，不补一份普通伤害。
            // 保护随整次 Apply 的传播保留；首部位回调解绑也不让后续部位改成枪伤。
            protectedHit |= protectedAtHit || SSCPnaExposureUtility.IsRooted(pawn);
            if (protectedHit)
            {
                if (SSCPnaExposureUtility.IsRooted(pawn))
                {
                    var injuries = pawn.health.hediffSet.hediffs
                        .OfType<Hediff_Injury>().Where(i => i.CanHealNaturally()).ToList();
                    if (injuries.Any())
                    {
                        injuries.RandomElement().Heal(ext.healAmount);
                        if (showHealingText) ShowHitText(pawn, "SSC_Pna_WeaponHealed");
                    }
                    // 保留命中性奴的强化反馈，改由成功的性奴负荷输入驱动；不再添加旧 Buff。
                    // 治疗也可能触发回调，显示前重新核对当前主人、锁链和真实负荷载体。
                    if (exposure.Accepted && exposure.Rooted && !SSCIdentityUtility.IsMaster(pawn) &&
                        SSCPnaExposureUtility.IsRooted(pawn) && SSCPnaExposureUtility.GetRootedRecord(pawn) != null)
                        ShowHitText(pawn, "SSC_Pna_WeaponActivated");
                }
                return;
            }
            // 只有普通分支调用原版伤口处理；保留原伤害量、穿甲设置与每部位触发次数。
            DamageInfo trueDinfo = new DamageInfo(dinfo);
            trueDinfo.SetAmount(ext.trueDamageAmount);
            trueDinfo.SetIgnoreArmor(true);
            base.ApplySpecialEffectsToPart(pawn, ext.trueDamageAmount, trueDinfo, result);
        }

        private static void ShowHitText(Pawn pawn, string key)
        {
            // 离图身体不生成地图特效；沿用原飘字样式与时长，只替换为四语资源。
            if (pawn.Spawned && pawn.Map != null)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, key.Translate().ToString(), 3f);
        }
    }

    // 高阶入口保留兼容，但武器与弹丸 XML 继续禁用，不借此次接通开放。
    public class DamageWorker_HighTier : DamageWorker_SSCPna
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            var ext = dinfo.Def.GetModExtension<PNA_DWExtension>();
            if (ext == null) return;
            // 记录本次回调开始时的保护；输入后的健康回调解绑也不能让本次命中追加枪伤。
            bool protectedAtHit = SSCPnaExposureUtility.IsRooted(pawn);
            var exposure = new SSCPnaExposureResult();
            if (ClaimExposure()) exposure = SSCPnaExposureUtility.ApplyExposure(pawn,
                ext.fixedSeverity / SSCPnaMath.MaximumSeverity, SSCPnaExposureSource.Weapon);
            FinishHit(pawn, protectedAtHit, exposure, ext, dinfo, result, false);
        }
    }

    public class DamageWorker_LowTier : DamageWorker_SSCPna
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            var ext = dinfo.Def.GetModExtension<PNA_DWExtension>();
            if (ext == null) return;
            bool protectedAtHit = SSCPnaExposureUtility.IsRooted(pawn);
            var exposure = new SSCPnaExposureResult();
            // 有链与无链使用同一剂量；仅第一次有效部位回调读取抗性并输入。
            // 主人跳过抗性快照的建立，避免在共享免疫入口外留下受药状态。
            bool firstExposure = ClaimExposure();
            if (firstExposure) SSCPnaExposureUtility.RefreshBodyState(pawn);
            if (firstExposure && Scribe.mode == LoadSaveMode.Inactive &&
                SSCPnaExposureUtility.IsValidTarget(pawn) && !SSCIdentityUtility.IsMaster(pawn))
            {
                float divisor = 1f;
                Hediff record = pawn.health.hediffSet.GetFirstHediffOfDef(ext.recordHediff);
                if (record != null) divisor = record.Severity;
                else
                {
                    // 在本次活化、治疗之前读取意识，保留 >2 快照与 0.2 保底的现有算法。
                    float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                    if (consciousness > 2f)
                    {
                        record = pawn.health.AddHediff(ext.recordHediff);
                        record.Severity = consciousness;
                        divisor = consciousness;
                    }
                    else divisor = Mathf.Max(consciousness, .2f);
                }
                // 非法旧快照沿用 1 的回退；原始单次上限仍为 1，除以 2 换算归一化负荷。
                if (!SSCPnaMath.IsFinite(divisor) || divisor <= 0f) divisor = 1f;
                float severity = ext.baseSeverity / divisor;
                if (severity > 1f) severity = 1f;
                exposure = SSCPnaExposureUtility.ApplyExposure(pawn,
                    severity / SSCPnaMath.MaximumSeverity, SSCPnaExposureSource.Weapon);
            }
            FinishHit(pawn, protectedAtHit, exposure, ext, dinfo, result, true);
        }
    }
}
