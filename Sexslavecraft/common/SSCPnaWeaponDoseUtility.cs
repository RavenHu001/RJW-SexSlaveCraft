// AI 辅助生成：现役 PNA 武器共用意识抗性、归一化剂量与快照管理，物理流程由各入口保留。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次调用的只读抗性样本；无全局状态，也不随存档保存。</summary>
    internal readonly struct SSCPnaWeaponResistanceSample
    {
        internal readonly float Divisor;
        internal readonly float NewSnapshotSeverity;

        internal SSCPnaWeaponResistanceSample(float divisor, float newSnapshotSeverity)
        {
            Divisor = divisor;
            NewSnapshotSeverity = newSnapshotSeverity;
        }
    }

    /// <summary>取样与提交分离：涂覆入口确认开放伤口，太空入口确认可投送后，才建立新快照。</summary>
    internal static class SSCPnaWeaponDoseUtility
    {
        /// <summary>只读取既有快照或命中前意识；准入、身体协调和提交时点由调用者决定。</summary>
        internal static SSCPnaWeaponResistanceSample Capture(Pawn pawn, HediffDef recordDef)
        {
            Hediff record = pawn.health.hediffSet.GetFirstHediffOfDef(recordDef);
            if (record != null) return new SSCPnaWeaponResistanceSample(record.Severity, 0f);

            float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            // 非有限读数用正常意识计算，不为损坏读数建立快照。
            if (!SSCPnaMath.IsFinite(consciousness))
                return new SSCPnaWeaponResistanceSample(1f, 0f);
            if (consciousness > 2f)
                return new SSCPnaWeaponResistanceSample(consciousness, consciousness);
            return new SSCPnaWeaponResistanceSample(Math.Max(consciousness, .2f), 0f);
        }

        /// <summary>输入、输出均为归一化负荷；单次上限 0.5，不先乘严重度尺度以免溢出。</summary>
        internal static float FromLoad(float baseLoad, SSCPnaWeaponResistanceSample sample)
        {
            float divisor = sample.Divisor;
            if (!SSCPnaMath.IsFinite(divisor) || divisor <= 0f) divisor = 1f;
            float dose = baseLoad / divisor;
            return dose > .5f ? .5f : dose;
        }

        /// <summary>在有效投送时点保存新快照；返回本次创建的对象，未创建时返回 null。</summary>
        internal static Hediff StoreSnapshot(Pawn pawn, HediffDef recordDef, SSCPnaWeaponResistanceSample sample)
        {
            if (!(sample.NewSnapshotSeverity > 0f)) return null;
            // 物理或健康回调可能触发独立嵌套命中，不覆盖内层刚建立的记录。
            if (pawn.health.hediffSet.GetFirstHediffOfDef(recordDef) != null) return null;
            // 添加前写好完整严重度，任何同步观察者均不会读到 Def 的默认初值。
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
