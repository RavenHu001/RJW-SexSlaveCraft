// AI 辅助生成：PNA 武器共用意识抗性、剂量换算与快照管理，物理流程由各入口保留。
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只描述既有抗性兼容约定，不选择伤害、治疗或药效准入分支。</summary>
    internal enum SSCPnaWeaponResistancePolicy { LauncherCompatibility, Coating }

    /// <summary>一次调用的只读抗性样本；无全局状态，也不随存档保存。</summary>
    internal readonly struct SSCPnaWeaponResistanceSample
    {
        internal readonly float Divisor;
        internal readonly float NewSnapshotSeverity;
        internal readonly SSCPnaWeaponResistancePolicy Policy;

        internal SSCPnaWeaponResistanceSample(float divisor, float newSnapshotSeverity,
            SSCPnaWeaponResistancePolicy policy)
        {
            Divisor = divisor;
            NewSnapshotSeverity = newSnapshotSeverity;
            Policy = policy;
        }
    }

    /// <summary>共用抗性算法；取样和写入分开，使两种物理入口保留各自的结算时点。</summary>
    internal static class SSCPnaWeaponDoseUtility
    {
        /// <summary>只读取已有快照或当前意识；准入、身体协调及调用时点由调用者决定。</summary>
        internal static SSCPnaWeaponResistanceSample Capture(Pawn pawn, HediffDef recordDef,
            SSCPnaWeaponResistancePolicy policy)
        {
            Hediff record = pawn.health.hediffSet.GetFirstHediffOfDef(recordDef);
            if (record != null) return new SSCPnaWeaponResistanceSample(record.Severity, 0f, policy);

            float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            // 旧枪保留原有 Mathf 及非有限读数的处理顺序，不能顺带改成涂覆入口的清理规则。
            // 涂覆武器对非有限意识直接用 1，不为该读数建立新快照。
            if (policy == SSCPnaWeaponResistancePolicy.Coating && !SSCPnaMath.IsFinite(consciousness))
                return new SSCPnaWeaponResistanceSample(1f, 0f, policy);
            if (consciousness > 2f)
                return new SSCPnaWeaponResistanceSample(consciousness, consciousness, policy);

            float divisor = policy == SSCPnaWeaponResistancePolicy.LauncherCompatibility
                ? Mathf.Max(consciousness, .2f) : Math.Max(consciousness, .2f);
            return new SSCPnaWeaponResistanceSample(divisor, 0f, policy);
        }

        /// <summary>旧枪使用原始严重度；保持先除抗性、钳制到 1、最后除以满值 2 的计算顺序。</summary>
        internal static float FromSeverity(float baseSeverity, SSCPnaWeaponResistanceSample sample)
            => Calculate(baseSeverity, sample.Divisor, 1f, SSCPnaMath.MaximumSeverity);

        /// <summary>涂覆武器已经使用归一化负荷，单次上限为 0.5。</summary>
        internal static float FromLoad(float baseLoad, SSCPnaWeaponResistanceSample sample)
            => Calculate(baseLoad, sample.Divisor, .5f, 1f);

        private static float Calculate(float amount, float divisor, float maximum, float scale)
        {
            if (!SSCPnaMath.IsFinite(divisor) || divisor <= 0f) divisor = 1f;
            float dose = amount / divisor;
            if (dose > maximum) dose = maximum;
            // 不在此统一过滤非法基础量：旧枪的历史输入交给共享暴露拒绝，
            // 涂覆入口则在物理处理前检查配置。也不先将 baseLoad 乘 2，避免额外溢出。
            return dose / scale;
        }

        /// <summary>在调用者选定的成功时点保存新快照；返回本次创建的对象，未创建时返回 null。</summary>
        internal static Hediff StoreSnapshot(Pawn pawn, HediffDef recordDef, SSCPnaWeaponResistanceSample sample)
        {
            if (!(sample.NewSnapshotSeverity > 0f)) return null;
            if (sample.Policy == SSCPnaWeaponResistancePolicy.LauncherCompatibility)
            {
                // 保留旧枪可观察的回调顺序：先加入 Def 初始值，再设置意识严重度。
                // 不补一次存在性检查或额外身体刷新，以免改变旧枪的同步嵌套行为。
                Hediff legacy = pawn.health.AddHediff(recordDef);
                legacy.Severity = sample.NewSnapshotSeverity;
                return legacy;
            }

            // 涂覆入口可能在物理伤害期间发生独立嵌套命中，已建立的记录不能被外层覆盖。
            if (pawn.health.hediffSet.GetFirstHediffOfDef(recordDef) != null) return null;
            Hediff record = HediffMaker.MakeHediff(recordDef, pawn);
            record.Severity = sample.NewSnapshotSeverity;
            pawn.health.AddHediff(record);
            return record;
        }

        /// <summary>仅撤销调用者持有的未使用新快照；不清理其他独立命中或原有记录。</summary>
        internal static void RemoveUnusedSnapshot(Pawn pawn, Hediff record)
        {
            if (record != null && pawn?.health?.hediffSet?.hediffs.Contains(record) == true)
                pawn.health.RemoveHediff(record);
        }
    }
}
